import importlib.util
import inspect
import json
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile


SOLUTION = Path(__file__).resolve().parents[1]
BOTCOMPONENTS = SOLUTION / "src" / "BlastBoxAgents" / "botcomponents"
WEATHER_SCRIPT = (
    BOTCOMPONENTS
    / "crf98_curbsidepickupassistant_Q0mRmz.file.blastboxstagingpy_RnlUT"
    / "filedata"
    / "blastbox_staging.py"
)
SELECTOR_SKILL = (
    BOTCOMPONENTS
    / "crf98_curbsidepickupassistant_Q0mRmz.file.skillmd_FPadO"
    / "filedata"
    / "SKILL.md"
)
AGENT_CONFIG = (
    SOLUTION
    / "src"
    / "BlastBoxAgents"
    / "bots"
    / "crf98_curbsidepickupassistant_Q0mRmz"
    / "configuration.json"
)
CURB_CONNECTOR_SCRIPT = (
    SOLUTION / "connectors" / "curb-delivery-mcp" / "script.csx"
)
CURB_CONNECTOR_SOLUTION_SCRIPT = (
    SOLUTION
    / "src"
    / "BlastBoxConnectors"
    / "Connectors"
    / "cat_curb-20pickup-20mcp_customcodeblobcontent.csx"
)
PDF_SCRIPT = (
    BOTCOMPONENTS
    / "crf98_curbsidepickupassistant_Q0mRmz.file.curbsideslippy_81DiC"
    / "filedata"
    / "curbside_slip.py"
)
PDF_SKILL = (
    BOTCOMPONENTS
    / "crf98_curbsidepickupassistant_Q0mRmz.file.skillmd_gYycZ"
    / "filedata"
    / "SKILL.md"
)
PACKAGE_VALIDATOR = (
    SOLUTION.parents[1] / "deploy" / "validate_solution_packages.py"
)


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Could not load {name} from {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


weather = load_module("blastbox_staging", WEATHER_SCRIPT)
pdf_skill = load_module("curbside_slip", PDF_SCRIPT)
package_validator = load_module(
    "validate_solution_packages", PACKAGE_VALIDATOR
)


class ActiveConditionTests(unittest.TestCase):
    def detect(self, **overrides):
        values = {
            "current_cap": "Clear",
            "forecast_cap": "Sunny",
            "forecast_precip": 0.0,
            "temp": 20.0,
            "feels": 20.0,
            "visibility": 10.0,
            "wind_speed": 10.0,
            "wind_gust": 15.0,
            "is_night": False,
        }
        values.update(overrides)
        return weather.detect_active_conditions(**values)

    def test_each_condition(self):
        self.assertEqual(self.detect(current_cap="Rain")[0], ["WET"])
        self.assertEqual(self.detect(feels=38.0)[0], ["HOT"])
        self.assertEqual(self.detect(temp=2.0, feels=None)[0], ["COLD"])
        self.assertEqual(self.detect(wind_gust=40.0)[0], ["WINDY"])
        self.assertEqual(self.detect(is_night=True)[0], ["DARK"])
        self.assertEqual(
            self.detect(visibility=4.9)[0], ["LOW_VISIBILITY"]
        )

    def test_all_active_conditions_preserve_precedence_order(self):
        active, unevaluated, _, _ = self.detect(
            current_cap="Rain",
            feels=40,
            wind_gust=60,
            forecast_precip=80,
            is_night=True,
            visibility=2,
        )
        self.assertEqual(
            active, ["WET", "HOT", "WINDY", "DARK", "LOW_VISIBILITY"]
        )
        self.assertEqual(unevaluated, [])

    def test_boundaries(self):
        self.assertNotIn("HOT", self.detect(feels=37.9)[0])
        self.assertIn("HOT", self.detect(feels=38.0)[0])
        self.assertNotIn("WINDY", self.detect(wind_gust=39.9)[0])
        self.assertIn("WINDY", self.detect(wind_gust=40.0)[0])
        self.assertNotIn("WET", self.detect(forecast_precip=59)[0])
        self.assertIn("WET", self.detect(forecast_precip=60)[0])
        self.assertNotIn(
            "LOW_VISIBILITY", self.detect(visibility=5.0)[0]
        )
        self.assertIn(
            "LOW_VISIBILITY", self.detect(visibility=4.9)[0]
        )

    def test_current_or_selected_forecast_caption_detects_wet(self):
        self.assertIn("WET", self.detect(current_cap="Rain")[0])
        self.assertIn("WET", self.detect(forecast_cap="Snow showers")[0])
        self.assertNotIn(
            "WET",
            self.detect(current_cap="Clear", forecast_cap="Sunny")[0],
        )

    def test_feels_like_is_primary_with_temperature_fallback(self):
        result = self.detect(temp=40, feels=20)
        self.assertNotIn("HOT", result[0])
        self.assertEqual(result[2:], (20, "feels"))
        fallback = self.detect(temp=40, feels=None)
        self.assertIn("HOT", fallback[0])
        self.assertEqual(fallback[2:], (40, "temp"))

    def test_missing_values_are_unevaluated(self):
        active, unevaluated, effective, source = self.detect(
            current_cap=None,
            forecast_cap=None,
            forecast_precip=None,
            temp=None,
            feels=None,
            visibility=None,
            wind_speed=None,
            wind_gust=None,
            is_night=None,
        )
        self.assertEqual(active, [])
        self.assertEqual(
            unevaluated,
            [
                "WET",
                "HOT",
                "COLD",
                "WINDY",
                "DARK",
                "LOW_VISIBILITY",
            ],
        )
        self.assertIsNone(effective)
        self.assertIsNone(source)


class CombinedPreferenceTests(unittest.TestCase):
    def test_wet_and_dark_combines_cover_and_light(self):
        attributes = weather.preferred_attributes(["WET", "DARK"], 180)
        self.assertIs(attributes["covered"], True)
        self.assertIs(attributes["lit"], True)
        self.assertIsNone(attributes["facing_away_from_wind_degrees"])

    def test_wet_windy_dark_combines_all_attributes(self):
        attributes = weather.preferred_attributes(
            ["WET", "WINDY", "DARK"], 45
        )
        self.assertEqual(
            attributes,
            {
                "covered": True,
                "lit": True,
                "good_drainage": True,
                "high_wind_protection": True,
                "facing_away_from_wind_degrees": 225.0,
                "shorter_distance_to_entrance": True,
            },
        )

    def test_wet_and_windy_combine_cover_and_facing(self):
        attributes = weather.preferred_attributes(["WET", "WINDY"], 90)
        self.assertEqual(
            attributes,
            {
                "covered": True,
                "lit": None,
                "good_drainage": True,
                "high_wind_protection": True,
                "facing_away_from_wind_degrees": 270.0,
                "shorter_distance_to_entrance": True,
            },
        )

    def test_windy_and_dark_combine_facing_and_light(self):
        attributes = weather.preferred_attributes(["WINDY", "DARK"], 270)
        self.assertEqual(
            attributes,
            {
                "covered": None,
                "lit": True,
                "good_drainage": None,
                "high_wind_protection": True,
                "facing_away_from_wind_degrees": 90.0,
                "shorter_distance_to_entrance": True,
            },
        )

    def test_cold_windy_dark_combines_all_attributes(self):
        attributes = weather.preferred_attributes(
            ["COLD", "WINDY", "DARK"], 350
        )
        self.assertEqual(
            attributes,
            {
                "covered": True,
                "lit": True,
                "good_drainage": None,
                "high_wind_protection": True,
                "facing_away_from_wind_degrees": 170.0,
                "shorter_distance_to_entrance": True,
            },
        )

    def test_wind_wraparound(self):
        attributes = weather.preferred_attributes(["WINDY"], 350)
        self.assertEqual(
            attributes["facing_away_from_wind_degrees"], 170.0
        )

    def test_missing_wind_direction_preserves_other_preferences(self):
        attributes = weather.preferred_attributes(
            ["WET", "WINDY", "DARK"], None
        )
        self.assertIs(attributes["covered"], True)
        self.assertIs(attributes["lit"], True)
        self.assertIsNone(attributes["facing_away_from_wind_degrees"])


class AssessmentTests(unittest.TestCase):
    def assess(self, **overrides):
        values = {
            "weather_available": True,
            "eta_minutes": 20,
            "current_cap": "Clear",
            "temp": 20.0,
            "feels": 20.0,
            "visibility": 10.0,
            "wind_dir": 180.0,
            "wind_speed": 10.0,
            "wind_gust": 15.0,
            "day_cap": "Sunny",
            "day_precip": 0.0,
            "day_summary": "Sunny conditions are expected.",
            "night_cap": "Clear",
            "night_precip": 0.0,
            "night_summary": "Clear conditions are expected overnight.",
            "now_iso": "2026-09-02T15:40:00-05:00",
            "store_timezone": "America/Chicago",
            "sunrise_iso": "2026-09-02T06:20:00-05:00",
            "sunset_iso": "2026-09-02T19:20:00-05:00",
        }
        values.update(overrides)
        return weather.assess_weather(**values)

    def test_exact_result_shape(self):
        self.assertEqual(
            set(self.assess()),
            {
                "weather_available",
                "weather_status",
                "assessment_basis",
                "target_local_time",
                "remaining_eta_seconds",
                "remaining_eta_minutes",
                "target_status",
                "selected_period",
                "current_conditions",
                "selected_forecast",
                "effective_temperature",
                "temperature_source",
                "primary_band",
                "active_conditions",
                "preferred_bay_attributes",
                "reason_codes",
                "unevaluated_conditions",
            },
        )

    def test_complete_benign_result(self):
        result = self.assess()
        self.assertEqual(result["weather_status"], "complete")
        self.assertEqual(result["primary_band"], "BENIGN")
        self.assertEqual(result["active_conditions"], [])
        self.assertEqual(result["reason_codes"], ["NO_WEATHER_PREFERENCE"])

    def test_multi_condition_result(self):
        result = self.assess(
            current_cap="Cloudy",
            wind_gust=60,
            wind_dir=45,
            day_cap="Rain showers",
            day_precip=80,
            now_iso="2026-09-02T18:50:00-05:00",
        )
        self.assertEqual(result["primary_band"], "WET")
        self.assertEqual(
            result["active_conditions"], ["WET", "WINDY"]
        )
        self.assertEqual(
            result["preferred_bay_attributes"],
            {
                "covered": True,
                "lit": None,
                "good_drainage": True,
                "high_wind_protection": True,
                "facing_away_from_wind_degrees": 225.0,
                "shorter_distance_to_entrance": True,
            },
        )
        self.assertEqual(
            result["reason_codes"],
            [
                "PRECIPITATION_PROTECTION",
                "WIND_PROTECTION",
            ],
        )

    def test_arrival_time_selects_day_or_night(self):
        day = self.assess(eta_minutes=None)
        night = self.assess(
            eta_minutes=30,
            now_iso="2026-09-02T19:00:00-05:00",
        )
        self.assertEqual(day["selected_period"], "day")
        self.assertEqual(day["selected_forecast"]["condition"], "Sunny")
        self.assertEqual(night["selected_period"], "night")
        self.assertEqual(night["selected_forecast"]["condition"], "Clear")
        self.assertIn("DARK", night["active_conditions"])

    def test_eta_uses_elapsed_time_across_spring_dst(self):
        result = self.assess(
            now_iso="2026-03-08T01:50:00-06:00",
            sunrise_iso="2026-03-08T07:00:00-05:00",
            sunset_iso="2026-03-08T18:00:00-05:00",
        )
        self.assertEqual(
            result["target_local_time"], "2026-03-08T03:10:00-05:00"
        )

    def test_eta_uses_elapsed_time_across_fall_dst(self):
        result = self.assess(
            now_iso="2026-11-01T01:50:00-05:00",
            sunrise_iso="2026-11-01T07:00:00-06:00",
            sunset_iso="2026-11-01T17:00:00-06:00",
        )
        self.assertEqual(
            result["target_local_time"], "2026-11-01T01:10:00-06:00"
        )

    def test_absolute_target_is_reused_without_resetting_eta(self):
        result = self.assess(
            eta_minutes=None,
            target_local_time="2026-09-02T16:00:00-05:00",
            now_iso="2026-09-02T15:50:00-05:00",
        )
        self.assertEqual(
            result["target_local_time"], "2026-09-02T16:00:00-05:00"
        )
        self.assertEqual(result["remaining_eta_minutes"], 10)
        self.assertEqual(result["remaining_eta_seconds"], 600)
        self.assertEqual(result["target_status"], "future")

    def test_target_adjustment_changes_absolute_target(self):
        result = self.assess(
            eta_minutes=None,
            target_local_time="2026-09-02T16:00:00-05:00",
            target_adjustment_minutes=10,
            now_iso="2026-09-02T15:50:00-05:00",
        )
        self.assertEqual(
            result["target_local_time"], "2026-09-02T16:10:00-05:00"
        )
        self.assertEqual(result["remaining_eta_minutes"], 20)

    def test_expired_target_clamps_remaining_eta_to_zero(self):
        result = self.assess(
            eta_minutes=None,
            target_local_time="2026-09-02T15:40:00-05:00",
            now_iso="2026-09-02T15:50:00-05:00",
        )
        self.assertEqual(result["remaining_eta_minutes"], 0)
        self.assertEqual(result["remaining_eta_seconds"], 0)
        self.assertEqual(result["target_status"], "due_or_passed")

    def test_weather_unavailable_still_calculates_remaining_eta(self):
        result = weather.assess_weather(
            weather_available=False,
            target_local_time="2026-09-02T16:00:00-05:00",
            now_iso="2026-09-02T15:50:00-05:00",
            store_timezone="America/Chicago",
        )
        self.assertEqual(result["weather_status"], "unavailable")
        self.assertEqual(result["remaining_eta_minutes"], 10)
        self.assertEqual(
            result["target_local_time"], "2026-09-02T16:00:00-05:00"
        )

    def test_relative_and_absolute_eta_are_mutually_exclusive(self):
        with self.assertRaisesRegex(ValueError, "cannot be used together"):
            self.assess(
                target_local_time="2026-09-02T16:00:00-05:00"
            )

    def test_missing_sunset_retains_known_weather(self):
        result = self.assess(
            current_cap="Rain",
            sunset_iso=None,
        )
        self.assertEqual(result["weather_status"], "partial")
        self.assertIn("WET", result["active_conditions"])
        self.assertIn("DARK", result["unevaluated_conditions"])
        self.assertIs(result["preferred_bay_attributes"]["covered"], True)

    def test_missing_almanac_uses_more_precipitation_exposed_forecast(self):
        result = self.assess(
            current_cap="Clear",
            day_cap="Sunny",
            day_precip=5,
            night_cap="Rain showers",
            night_precip=70,
            sunset_iso=None,
        )
        self.assertIsNone(result["selected_period"])
        self.assertEqual(
            result["selected_forecast"]["condition"], "Rain showers"
        )
        self.assertIn("WET", result["active_conditions"])
        self.assertIn("DARK", result["unevaluated_conditions"])

    def test_next_day_sunrise_pair_classifies_evening_as_night(self):
        result = self.assess(
            eta_minutes=30,
            now_iso="2026-09-02T20:00:00-05:00",
            sunrise_iso="2026-09-03T06:20:00-05:00",
            sunset_iso="2026-09-02T19:20:00-05:00",
        )
        self.assertEqual(result["selected_period"], "night")
        self.assertIn("DARK", result["active_conditions"])

    def test_missing_wind_direction_retains_windy(self):
        result = self.assess(wind_gust=60, wind_dir=None)
        self.assertEqual(result["weather_status"], "partial")
        self.assertIn("WINDY", result["active_conditions"])
        self.assertIn("WIND_DIRECTION", result["unevaluated_conditions"])
        self.assertIsNone(
            result["preferred_bay_attributes"][
                "facing_away_from_wind_degrees"
            ]
        )

    def test_missing_precipitation_evidence_is_partial_but_keeps_facts(self):
        result = self.assess(
            current_cap=None,
            day_cap=None,
            day_precip=None,
        )
        self.assertEqual(result["weather_status"], "partial")
        self.assertIn("WET", result["unevaluated_conditions"])
        self.assertEqual(result["primary_band"], "BENIGN")
        self.assertEqual(result["effective_temperature"], 20)

    def test_effective_temperature_prefers_feels_with_temp_fallback(self):
        feels = self.assess(temp=40, feels=20)
        fallback = self.assess(temp=40, feels=None)
        self.assertEqual(feels["effective_temperature"], 20)
        self.assertEqual(feels["temperature_source"], "feels")
        self.assertNotIn("HOT", feels["active_conditions"])
        self.assertEqual(fallback["effective_temperature"], 40)
        self.assertEqual(fallback["temperature_source"], "temp")
        self.assertIn("HOT", fallback["active_conditions"])

    def test_low_visibility_prefers_lighting_and_shorter_distance(self):
        result = self.assess(visibility=2)
        self.assertIn("LOW_VISIBILITY", result["active_conditions"])
        self.assertIs(result["preferred_bay_attributes"]["lit"], True)
        self.assertIs(
            result["preferred_bay_attributes"][
                "shorter_distance_to_entrance"
            ],
            True,
        )

    def test_weather_unavailable(self):
        result = weather.assess_weather(weather_available=False)
        self.assertFalse(result["weather_available"])
        self.assertEqual(result["weather_status"], "unavailable")
        self.assertEqual(result["primary_band"], "UNKNOWN")
        self.assertEqual(result["reason_codes"], ["QUEUE_ONLY"])

    def test_all_missing_live_values_returns_partial(self):
        result = weather.assess_weather(weather_available=True)
        self.assertEqual(result["weather_status"], "partial")
        self.assertEqual(result["primary_band"], "PARTIAL")
        self.assertEqual(result["active_conditions"], [])

    def test_negative_eta_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "zero or greater"):
            self.assess(eta_minutes=-1)

    def test_timezone_aware_values_are_required_when_supplied(self):
        with self.assertRaisesRegex(ValueError, "timezone-aware"):
            self.assess(now_iso="2026-09-02T15:40:00")

    def test_eta_only_calculation_uses_elapsed_time_across_dst(self):
        spring = weather.calculate_remaining_eta(
            target_local_time="2026-03-08T03:10:00-05:00",
            now_iso="2026-03-08T01:50:00-06:00",
            store_timezone="America/Chicago",
        )
        fall = weather.calculate_remaining_eta(
            target_local_time="2026-11-01T01:10:00-06:00",
            now_iso="2026-11-01T01:50:00-05:00",
            store_timezone="America/Chicago",
        )
        self.assertEqual(spring["remaining_eta_minutes"], 20)
        self.assertEqual(fall["remaining_eta_minutes"], 20)
        self.assertEqual(spring["remaining_eta_seconds"], 1200)
        self.assertEqual(fall["remaining_eta_seconds"], 1200)

    def test_eta_only_interface_strictly_excludes_bay_inputs(self):
        parameters = inspect.signature(
            weather.calculate_remaining_eta
        ).parameters
        for forbidden in (
            "bays",
            "bay_id",
            "occupancy",
            "queue",
            "expected_wait_minutes",
            "weather_available",
        ):
            self.assertNotIn(forbidden, parameters)

    def test_queue_status_decrements_every_thirty_seconds(self):
        first = weather.calculate_queue_status(
            queue_observed_at="2026-09-02T15:00:00-05:00",
            initial_orders_ahead=3,
            now_iso="2026-09-02T15:00:29-05:00",
            store_timezone="America/Chicago",
            target_local_time="2026-09-02T15:20:00-05:00",
        )
        second = weather.calculate_queue_status(
            queue_observed_at="2026-09-02T15:00:00-05:00",
            initial_orders_ahead=3,
            now_iso="2026-09-02T15:00:30-05:00",
            store_timezone="America/Chicago",
            target_local_time="2026-09-02T15:20:00-05:00",
        )
        ready = weather.calculate_queue_status(
            queue_observed_at="2026-09-02T15:00:00-05:00",
            initial_orders_ahead=3,
            now_iso="2026-09-02T15:01:30-05:00",
            store_timezone="America/Chicago",
            target_local_time="2026-09-02T15:20:00-05:00",
        )
        self.assertEqual(first["orders_ahead"], 3)
        self.assertEqual(second["orders_ahead"], 2)
        self.assertEqual(second["expected_wait_seconds"], 60)
        self.assertEqual(
            second["estimated_curb_ready_time"],
            "2026-09-02T15:01:30-05:00",
        )
        self.assertEqual(ready["orders_ahead"], 0)
        self.assertEqual(ready["queue_status"], "ready")
        self.assertEqual(
            ready["estimated_curb_ready_time"],
            "2026-09-02T15:01:30-05:00",
        )

    def test_queue_status_updates_eta_without_resetting_queue(self):
        result = weather.calculate_queue_status(
            queue_observed_at="2026-09-02T15:00:00-05:00",
            initial_orders_ahead=3,
            now_iso="2026-09-02T15:00:30-05:00",
            store_timezone="America/Chicago",
            eta_minutes=30,
        )
        self.assertEqual(result["orders_ahead"], 2)
        self.assertEqual(result["remaining_eta_minutes"], 30)
        self.assertEqual(result["remaining_eta_seconds"], 1800)
        self.assertEqual(result["eta_margin_seconds"], 1740)
        self.assertEqual(result["eta_status"], "before_arrival")
        self.assertEqual(
            result["target_local_time"], "2026-09-02T15:30:30-05:00"
        )

    def test_queue_eta_margin_uses_exact_seconds(self):
        result = weather.calculate_queue_status(
            queue_observed_at="2026-09-02T15:00:00-05:00",
            initial_orders_ahead=1,
            now_iso="2026-09-02T15:00:10-05:00",
            store_timezone="America/Chicago",
            target_local_time="2026-09-02T15:00:20-05:00",
        )
        self.assertEqual(result["remaining_eta_seconds"], 10)
        self.assertEqual(result["expected_wait_seconds"], 30)
        self.assertEqual(result["eta_margin_seconds"], -20)
        self.assertEqual(result["eta_status"], "may_extend_beyond_arrival")

    def test_python_strictly_excludes_nonweather_inputs(self):
        parameters = inspect.signature(weather.assess_weather).parameters
        for forbidden in (
            "order",
            "customer",
            "store",
            "tier_code",
            "membership",
            "bays",
            "occupancy",
            "queue",
            "expected_wait_minutes",
            "wx",
            "rain_chance",
            "uv",
            "humidity",
        ):
            self.assertNotIn(forbidden, parameters)


class WeatherCliTests(unittest.TestCase):
    def live_command(self):
        return [
            sys.executable,
            str(WEATHER_SCRIPT),
            "assess",
            "--weather-available",
            "true",
            "--eta-minutes",
            "20",
            "--current-cap",
            "Cloudy",
            "--temp",
            "30",
            "--feels",
            "33",
            "--visibility",
            "8",
            "--wind-dir",
            "45",
            "--wind-speed",
            "25",
            "--wind-gust",
            "50",
            "--day-cap",
            "Rain showers",
            "--day-precip",
            "70",
            "--day-summary",
            "Periods of rain are expected.",
            "--night-cap",
            "Mostly clear",
            "--night-precip",
            "10",
            "--night-summary",
            "Mostly clear overnight.",
            "--now",
            "2026-09-02T18:50:00-05:00",
            "--store-timezone",
            "America/Chicago",
            "--sunrise",
            "2026-09-02T06:20:00-05:00",
            "--sunset",
            "2026-09-02T19:20:00-05:00",
        ]

    def test_cli_outputs_combined_json(self):
        result = subprocess.run(
            self.live_command(), check=True, capture_output=True, text=True
        )
        parsed = json.loads(result.stdout)
        self.assertEqual(parsed["active_conditions"], ["WET", "WINDY"])
        self.assertEqual(parsed["selected_period"], "day")
        self.assertIs(
            parsed["preferred_bay_attributes"]["good_drainage"], True
        )

    def test_cli_supports_partial_weather(self):
        command = self.live_command()
        sunset_index = command.index("--sunset")
        del command[sunset_index : sunset_index + 2]
        result = subprocess.run(
            command, check=True, capture_output=True, text=True
        )
        parsed = json.loads(result.stdout)
        self.assertEqual(parsed["weather_status"], "partial")
        self.assertIn("DARK", parsed["unevaluated_conditions"])
        self.assertEqual(
            parsed["current_conditions"]["condition"], "Cloudy"
        )
        self.assertEqual(parsed["primary_band"], "WET")

    def test_cli_weather_unavailable(self):
        result = subprocess.run(
            [
                sys.executable,
                str(WEATHER_SCRIPT),
                "assess",
                "--weather-available",
                "false",
            ],
            check=True,
            capture_output=True,
            text=True,
        )
        self.assertEqual(
            json.loads(result.stdout)["weather_status"], "unavailable"
        )

    def test_cli_calculates_remaining_eta_without_bays(self):
        result = subprocess.run(
            [
                sys.executable,
                str(WEATHER_SCRIPT),
                "remaining",
                "--target-local-time",
                "2026-09-02T16:00:00-05:00",
                "--now",
                "2026-09-02T15:50:30-05:00",
                "--store-timezone",
                "America/Chicago",
            ],
            check=True,
            capture_output=True,
            text=True,
        )
        parsed = json.loads(result.stdout)
        self.assertEqual(parsed["operation"], "eta_only")
        self.assertEqual(parsed["remaining_eta_minutes"], 10)
        self.assertEqual(
            parsed["target_local_time"], "2026-09-02T16:00:00-05:00"
        )

    def test_cli_remaining_rejects_bay_inputs(self):
        result = subprocess.run(
            [
                sys.executable,
                str(WEATHER_SCRIPT),
                "remaining",
                "--target-local-time",
                "2026-09-02T16:00:00-05:00",
                "--now",
                "2026-09-02T15:50:00-05:00",
                "--store-timezone",
                "America/Chicago",
                "--bays",
                "[]",
            ],
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("unrecognized arguments", result.stderr)

    def test_bay_membership_and_order_flags_are_rejected(self):
        for flag, value in (
            ("--tier-code", "mega"),
            ("--order", "ORD-10502"),
            ("--bays", "[]"),
        ):
            result = subprocess.run(
                self.live_command() + [flag, value],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("unrecognized arguments", result.stderr)


class SkillContractTests(unittest.TestCase):
    def test_skill_is_selector_and_reasoner(self):
        text = SELECTOR_SKILL.read_text(encoding="utf-8")
        self.assertIn("name: curbside-bay-selector", text)
        self.assertIn("owns the complete bay decision", text)
        self.assertIn("Evaluate all candidates before", text)
        self.assertIn("selecting one.", text)
        self.assertIn(
            "Otherwise retain the complete tied candidate set", text
        )

    def test_skill_covers_structured_evidence(self):
        text = SELECTOR_SKILL.read_text(encoding="utf-8")
        for field in (
            "active_conditions",
            "weather_matches",
            "weather_degradations",
            "weather_limitations",
            "queue_comparison",
            "membership_resolved_tie",
            "recommendation_reason",
        ):
            self.assertIn(field, text)

    def test_skill_separates_recommendation_and_queue_status_operations(self):
        text = SELECTOR_SKILL.read_text(encoding="utf-8")
        self.assertIn("## Operations", text)
        self.assertIn("`recommend`:", text)
        self.assertIn("`queue_status`:", text)
        self.assertIn(
            "Do not\n  accept weather or other bays, call `list_bays`", text
        )
        self.assertIn(
            "selected bay is sticky for the conversation", text
        )
        self.assertIn(
            "python3 blastbox_staging.py queue", text
        )
        self.assertIn(
            "For a completely new relative ETA", text
        )
        self.assertIn(
            "pass the retained target\nand the adjustment together", text
        )
        self.assertIn(
            "Never pass\n`--target-adjustment-minutes` without "
            "`--target-local-time`",
            text,
        )
        self.assertIn(
            "Return no weather assessment or new recommendation", text
        )
        self.assertIn(
            "Return the timing fields even when no bay is available", text
        )
        self.assertIn("remaining_eta_minutes", text)
        self.assertIn("remaining_eta_seconds", text)
        self.assertIn("eta_status", text)
        self.assertIn("target_status", text)
        for field in (
            "--current-cap",
            "--visibility",
            "--wind-speed",
            "--day-cap",
            "--day-precip",
            "--day-summary",
            "--night-cap",
            "--night-precip",
            "--night-summary",
            "--sunrise",
            "--sunset",
        ):
            self.assertIn(field, text)
        self.assertIn("effective_temperature", text)
        self.assertIn("good_drainage", text)
        self.assertIn("high_wind_protection", text)
        self.assertIn("shorter_distance_to_entrance", text)
        self.assertIn("weather_summary", text)
        self.assertIn("customer_guidance", text)
        self.assertIn(
            "selected-forecast facts only when they materially affect", text
        )
        self.assertIn("Around your expected arrival", text)
        self.assertIn(
            "do not append a general daily\n  or overnight weather report",
            text,
        )
        self.assertIn("## Canonical same-session state", text)
        self.assertIn("Every successful `recommend` or `queue_status` result", text)
        self.assertIn("`canonical_state` object", text)
        self.assertIn(
            "`assigned_bay_id`, `queue_observed_at`, and\n"
            "`initial_orders_ahead` are immutable",
            text,
        )
        self.assertIn("return `status: state_unavailable`", text)
        self.assertIn(
            "complete current Python queue result", text
        )
        self.assertIn("recommendation_evidence", text)
        self.assertIn(
            "never replaces `recommendation_evidence`", text
        )
        self.assertIn("no completed order yet", text)
        self.assertIn("including an initial queue of zero", text)
        self.assertIn("There is still one order ahead", text)
        self.assertIn("There is now one order ahead", text)
        self.assertIn("after a no-available-bay result", text)
        self.assertNotIn("renewed weather suitability", text)

    def test_selector_owns_single_customer_message(self):
        text = SELECTOR_SKILL.read_text(encoding="utf-8")
        self.assertIn("Compose one complete `customer_message`", text)
        self.assertIn(
            "fixed, easy-to-scan five-block customer response", text
        )
        self.assertIn(
            "do not collapse the response into one paragraph", text
        )
        self.assertIn(
            "Put exactly one\nblank line between included blocks", text
        )
        self.assertIn(
            "start exactly with `🚗 **Queue:**`", text
        )
        self.assertIn(
            "start exactly with `⏱️ **ETA:**`", text
        )
        self.assertIn(
            "`💡 **Assigned Bay is [bay_id]:**`", text
        )
        self.assertIn("Explain why this bay is", text)
        self.assertIn("preferable to the alternatives", text)
        self.assertIn(
            "Do not add an overall heading, bullets, an order number", text
        )
        self.assertIn(
            "Never mention store opening status",
            text,
        )
        self.assertNotIn("the store is open", text)
        self.assertIn(
            "the calling agent owns that question", text
        )
        self.assertIn(
            "For `recommend`, `customer_message` is the only customer-facing "
            "recommendation",
            text,
        )
        for icon in ("🌧️", "🌡️", "❄️", "💨", "🌙", "🌤️", "🚗", "⏱️"):
            self.assertIn(icon, text)
        for driver in (
            "weather",
            "queue",
            "weather_and_queue",
            "membership_tie_break",
            "lowest_bay_id_tie_break",
            "distance_tie_break",
            "availability",
        ):
            self.assertIn(driver, text)
        self.assertIn("weather_narrowed_candidates", text)
        self.assertIn("queue_narrowed_candidates", text)
        self.assertIn(
            "matched every candidate and therefore did not", text
        )
        self.assertIn("eta_margin_seconds", text)
        self.assertIn("never expose", text)
        self.assertIn("missing readings, unavailable field names", text)
        self.assertIn(
            "never include them in `recommendation_reason`", text
        )
        self.assertIn(
            "current assignment rather than a reservation", text
        )
        self.assertIn(
            "There are two orders ahead, with an expected wait of one minute",
            text,
        )
        self.assertIn(
            "That leaves a 19-minute buffer before your expected arrival",
            text,
        )
        self.assertIn(
            "This is the only place in the complete", text
        )

    def test_main_instructions_only_orchestrate(self):
        segments = json.loads(
            AGENT_CONFIG.read_text(encoding="utf-8")
        )["agentSettings"]["instructions"]["segments"]
        instructions = "\n\n".join(segment["value"] for segment in segments)
        self.assertIn("curbside-bay-selector", instructions)
        self.assertNotIn("BAY DECISION", instructions)
        self.assertNotIn("FINAL PDF", instructions)
        self.assertEqual(len(segments), 1)
        self.assertIn("SAME-SESSION STATE", instructions)
        self.assertIn("Assignment state:", instructions)
        self.assertIn("Recommendation evidence:", instructions)
        self.assertIn("PDF state:", instructions)
        self.assertIn(
            "recommendation_evidence: the complete latest successful recommend "
            "result, never a queue_status result",
            instructions,
        )
        self.assertIn(
            "A successful queue_status result may replace only "
            "target_local_time and latest_queue_status",
            instructions,
        )
        self.assertIn(
            "Generating or declining a PDF sets only pdf_eligible to false",
            instructions,
        )
        self.assertIn("ROUTING PRECEDENCE", instructions)
        self.assertIn(
            "Do not clear the old assignment unless the replacement succeeds",
            instructions,
        )
        self.assertIn(
            "No active assigned bay after no_available_bay", instructions
        )
        self.assertIn(
            "A request combining an ETA change and a weather question",
            instructions,
        )
        self.assertIn(
            "Never mention store opening status or hours",
            instructions,
        )
        self.assertNotIn("The store is currently closed", instructions)
        self.assertNotIn(
            "Then stop before weather, bay lookup, skill invocation, or PDF generation",
            instructions,
        )
        self.assertIn("CUSTOMER OUTPUT", instructions)
        self.assertIn(
            "The selector alone owns recommendation formatting",
            instructions,
        )
        self.assertIn(
            "Call MSN Weather Get current weather exactly once and Get forecast for today exactly once",
            instructions,
        )
        self.assertIn(
            "wind_protection, drainage, distance_to_entrance_m",
            instructions,
        )
        self.assertIn(
            "Would you like a copy of the delivery slip?",
            instructions,
        )
        self.assertIn("RETAINED WEATHER FOLLOW-UPS", instructions)
        self.assertIn(
            'Examples include "Will the weather affect my pickup?" and '
            '"Will it be raining when I arrive?"',
            instructions,
        )
        self.assertIn(
            "Make no tool calls", instructions
        )
        for forbidden_call in (
            "MSN Weather",
            "get_store_location",
            "list_bays",
            "curbside-bay-selector",
            "Python",
        ):
            self.assertIn(forbidden_call, instructions)
        self.assertIn(
            "Answer directly from recommendation_evidence",
            instructions,
        )
        self.assertIn(
            "Use one short paragraph of at most two sentences", instructions
        )
        self.assertIn(
            "Never use bullets, numbered lists, headings, labels", instructions
        )
        self.assertIn(
            "without mentioning retained evidence, refreshes, tools, APIs",
            instructions,
        )
        self.assertIn(
            "weather_assessment.selected_forecast", instructions
        )
        self.assertIn(
            "Never expose weather_limitations, weather_degradations, "
            "unevaluated_conditions",
            instructions,
        )
        self.assertIn(
            "I don't have confirmed information about that in the current "
            "pickup plan.",
            instructions,
        )
        self.assertIn(
            "Only when pdf_eligible is true and the customer explicitly agrees",
            instructions,
        )
        self.assertIn(
            "Use both queue inputs from the same snapshot",
            instructions,
        )
        self.assertIn(
            "previous_queue_update_time", instructions
        )
        self.assertIn(
            "copy selector customer_message exactly once",
            instructions,
        )
        self.assertIn(
            "Remain silent while tools execute",
            instructions,
        )
        self.assertNotIn(
            "return its completed recommendation as a PDF", instructions
        )

    def test_curb_connector_treats_store_status_as_informational(self):
        text = CURB_CONNECTOR_SCRIPT.read_text(encoding="utf-8")
        self.assertIn(
            "Treat store hours and open status as informational facts", text
        )
        self.assertNotIn(
            "If the store is closed, stop before weather or bay selection", text
        )

    def test_curb_connector_returns_complete_deterministic_bay_data(self):
        text = CURB_CONNECTOR_SCRIPT.read_text(encoding="utf-8")
        solution_text = CURB_CONNECTOR_SOLUTION_SCRIPT.read_text(
            encoding="utf-8"
        )
        for field in (
            "wind_protection",
            "drainage",
            "distance_to_entrance_m",
            "queue_observed_at",
            "queue_seconds_per_order",
            "expected_wait_seconds",
        ):
            self.assertIn(field, text)
        self.assertIn("ordersAhead * 30", text)
        self.assertIn("ordersAhead / 2.0", text)
        orders = [
            int(value)
            for value in re.findall(r'""orders_ahead"":\s*(\d+)', text)
        ]
        self.assertEqual(len(orders), 17)
        self.assertTrue(all(6 <= value <= 10 for value in orders))
        self.assertNotIn("OrdersAheadRandom", text)
        self.assertNotIn("OrdersAheadLock", text)
        self.assertEqual(
            text, solution_text
        )

    def test_pdf_skill_requires_explicit_customer_request(self):
        text = PDF_SKILL.read_text(encoding="utf-8")
        self.assertIn(
            "the customer has explicitly asked for a PDF copy", text
        )


class RecommendationPdfTests(unittest.TestCase):
    def command(self, output):
        return [
            sys.executable,
            str(PDF_SCRIPT),
            "--order",
            "ORD-10502",
            "--customer",
            "Jordan Pixel",
            "--store",
            "BlastBox Pixel Heights",
            "--bay",
            "2",
            "--orders-ahead",
            "2",
            "--expected-wait-seconds",
            "60",
            "--eta-minutes",
            "20",
            "--conditions",
            "Rain and after-dark conditions are expected around arrival.",
            "--reason",
            "This covered and lit bay has the shortest current queue.",
            "--out",
            str(output),
        ]

    def test_queue_wording(self):
        self.assertEqual(pdf_skill._queue_text(0), "There are no orders ahead.")
        self.assertEqual(pdf_skill._queue_text(1), "There is one order ahead.")
        self.assertEqual(pdf_skill._queue_text(2), "There are 2 orders ahead.")

    def test_eta_comparison(self):
        self.assertIn("19 minutes", pdf_skill._eta_text(60, 20))
        self.assertIn("aligns", pdf_skill._eta_text(1200, 20))
        self.assertIn("30 seconds past", pdf_skill._eta_text(1230, 20))
        self.assertIsNone(pdf_skill._eta_text(3, None))

    def test_pdf_skill_requires_barcode_and_single_timing_display(self):
        text = PDF_SKILL.read_text(encoding="utf-8")
        self.assertIn("scannable Code 128 barcode", text)
        self.assertIn("queue, wait, ETA, and ETA margin once", text)
        self.assertNotIn("--weather-source", text)
        self.assertNotIn("--bay-covered", text)
        self.assertNotIn("--bay-lit", text)

    def test_cli_generates_pdf(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "blastbox_curbside_slip.pdf"
            result = subprocess.run(
                self.command(output), check=True, capture_output=True, text=True
            )
            self.assertEqual(result.stdout.strip(), f"WROTE {output}")
            self.assertTrue(output.read_bytes().startswith(b"%PDF"))
            self.assertGreater(output.stat().st_size, 1000)

    def test_cli_rejects_inconsistent_queue_wait(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "bad.pdf"
            command = self.command(output)
            wait_index = command.index("--expected-wait-seconds") + 1
            command[wait_index] = "61"
            result = subprocess.run(command, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("multiplied by 30", result.stderr)
            self.assertFalse(output.exists())

    def test_assignment_inputs_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "bad.pdf"
            result = subprocess.run(
                self.command(output) + ["--confirmation", "CURB-10502-2"],
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("unrecognized arguments", result.stderr)
            self.assertFalse(output.exists())


class SolutionPackageTests(unittest.TestCase):
    def test_committed_solution_packages_are_importable(self):
        package_validator.validate_package(
            SOLUTION / "BlastBoxConnectors_1_0_0_1.zip",
            SOLUTION / "src" / "BlastBoxConnectors",
        )
        package_validator.validate_package(
            SOLUTION / "BlastBoxAgents_1_0_0_1.zip",
            SOLUTION / "src" / "BlastBoxAgents",
        )

    def test_validator_rejects_zipped_unpacked_layout(self):
        with tempfile.TemporaryDirectory() as directory:
            invalid = Path(directory) / "invalid.zip"
            with ZipFile(invalid, "w") as archive:
                archive.writestr("Other/Solution.xml", "<Version>1</Version>")
                archive.writestr("Other/Customizations.xml", "")
            with self.assertRaisesRegex(
                ValueError, "Pack with 'pac solution pack'"
            ):
                package_validator.validate_package(
                    invalid, SOLUTION / "src" / "BlastBoxAgents"
                )


if __name__ == "__main__":
    unittest.main()
