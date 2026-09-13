#!/usr/bin/env python3
"""Evaluate curbside weather signals and same-session arrival timing.

The agent supplies live Metric weather facts plus a new ETA or retained absolute
arrival target. This script owns weather thresholds, condition precedence,
arrival-time arithmetic, and elapsed queue arithmetic. It never receives order,
membership, occupancy, candidate-bay, or selected-bay data and never selects a
bay.
"""

import argparse
from datetime import datetime, timedelta, timezone, tzinfo
import json
import math
import sys
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError


HOT_FEELS_C = 38.0
COLD_TEMP_C = 2.0
WINDY_GUST_KMH = 40.0
RAIN_CHANCE_PCT = 60
LOW_VISIBILITY_KM = 5.0

WET_CAPTION_TOKENS = (
    "rain",
    "drizzle",
    "shower",
    "thunderstorm",
    "sleet",
    "snow",
    "hail",
    "wintry",
    "freezing precipitation",
)
CONDITION_PRECEDENCE = (
    "WET",
    "HOT",
    "COLD",
    "WINDY",
    "DARK",
    "LOW_VISIBILITY",
)
QUEUE_SECONDS_PER_ORDER = 30
REASON_CODES = {
    "WET": "PRECIPITATION_PROTECTION",
    "HOT": "SHADE_PROTECTION",
    "COLD": "COLD_EXPOSURE_PROTECTION",
    "WINDY": "WIND_PROTECTION",
    "DARK": "LIGHTING",
    "LOW_VISIBILITY": "VISIBILITY_LIGHTING",
}


def detect_active_conditions(
    current_cap,
    forecast_cap,
    forecast_precip,
    temp,
    feels,
    visibility,
    wind_speed,
    wind_gust,
    is_night,
):
    """Return active conditions and normalized temperature evidence."""
    active = []
    unevaluated = []

    captions = " ".join(
        value.lower()
        for value in (current_cap, forecast_cap)
        if value is not None
    )
    wet_detected = (
        any(token in captions for token in WET_CAPTION_TOKENS)
        or (
            forecast_precip is not None
            and forecast_precip >= RAIN_CHANCE_PCT
        )
    )
    if wet_detected:
        active.append("WET")
    elif (
        current_cap is None
        and forecast_cap is None
        and forecast_precip is None
    ):
        unevaluated.append("WET")

    effective_temperature = feels if feels is not None else temp
    temperature_source = (
        "feels" if feels is not None else "temp" if temp is not None else None
    )
    if effective_temperature is None:
        unevaluated.extend(("HOT", "COLD"))
    elif effective_temperature >= HOT_FEELS_C:
        active.append("HOT")
    elif effective_temperature <= COLD_TEMP_C:
        active.append("COLD")

    wind_values = [
        value for value in (wind_speed, wind_gust) if value is not None
    ]
    if not wind_values:
        unevaluated.append("WINDY")
    elif max(wind_values) >= WINDY_GUST_KMH:
        active.append("WINDY")

    if is_night is None:
        unevaluated.append("DARK")
    elif is_night:
        active.append("DARK")

    if visibility is None:
        unevaluated.append("LOW_VISIBILITY")
    elif visibility < LOW_VISIBILITY_KM:
        active.append("LOW_VISIBILITY")

    return (
        active,
        unevaluated,
        effective_temperature,
        temperature_source,
    )


def _parse_aware_iso(value, flag_name):
    normalized = value[:-1] + "+00:00" if value.endswith(("Z", "z")) else value
    try:
        parsed = datetime.fromisoformat(normalized)
    except ValueError as exc:
        raise ValueError(
            f"{flag_name} must be a timezone-aware ISO-8601 value with a UTC offset"
        ) from exc
    if parsed.tzinfo is None or parsed.utcoffset() is None:
        raise ValueError(
            f"{flag_name} must be a timezone-aware ISO-8601 value with a UTC offset"
        )
    return parsed


def _first_sunday_on_or_after(value):
    return value + timedelta(days=(6 - value.weekday()) % 7)


class _PacificFallbackTimezone(tzinfo):
    """US Pacific rules used only when the host has no IANA timezone database."""

    _standard = timedelta(hours=-8)
    _daylight = timedelta(hours=-7)

    def utcoffset(self, value):
        return self._daylight if self.dst(value) else self._standard

    def dst(self, value):
        if value is None:
            return timedelta(0)
        naive = value.replace(tzinfo=None)
        start = _first_sunday_on_or_after(datetime(value.year, 3, 8, 2))
        end = _first_sunday_on_or_after(datetime(value.year, 11, 1, 2))
        if start <= naive < start + timedelta(hours=1):
            return timedelta(hours=1) if value.fold else timedelta(0)
        if end - timedelta(hours=1) <= naive < end:
            return timedelta(0) if value.fold else timedelta(hours=1)
        if start + timedelta(hours=1) <= naive < end - timedelta(hours=1):
            return timedelta(hours=1)
        return timedelta(0)

    def tzname(self, value):
        return "PDT" if self.dst(value) else "PST"

    def fromutc(self, value):
        start = _first_sunday_on_or_after(
            datetime(value.year, 3, 8, 2, tzinfo=self)
        )
        end = _first_sunday_on_or_after(
            datetime(value.year, 11, 1, 2, tzinfo=self)
        )
        standard_time = value + self._standard
        daylight_time = standard_time + timedelta(hours=1)
        if end <= daylight_time < end + timedelta(hours=1):
            return standard_time.replace(fold=1)
        if standard_time < start or daylight_time >= end:
            return standard_time
        if start <= standard_time < end - timedelta(hours=1):
            return daylight_time
        return standard_time


class _CentralFallbackTimezone(_PacificFallbackTimezone):
    """US Central rules used only when the host has no IANA timezone database."""

    _standard = timedelta(hours=-6)
    _daylight = timedelta(hours=-5)

    def tzname(self, value):
        return "CDT" if self.dst(value) else "CST"


FALLBACK_TIMEZONES = {
    "America/Chicago": _CentralFallbackTimezone(),
    "America/Los_Angeles": _PacificFallbackTimezone(),
}


def _resolve_timezone(timezone_id):
    try:
        return ZoneInfo(timezone_id)
    except ZoneInfoNotFoundError:
        fallback = FALLBACK_TIMEZONES.get(timezone_id)
        if fallback is None:
            raise ValueError(f"unsupported --store-timezone: {timezone_id}")
        return fallback


def _resolve_arrival_timing(
    *,
    now_iso,
    store_timezone,
    eta_minutes=None,
    target_local_time=None,
    target_adjustment_minutes=0,
):
    if eta_minutes is not None and eta_minutes < 0:
        raise ValueError("--eta-minutes must be zero or greater")
    if eta_minutes is not None and target_local_time is not None:
        raise ValueError(
            "--eta-minutes and --target-local-time cannot be used together"
        )
    if target_adjustment_minutes and target_local_time is None:
        raise ValueError(
            "--target-adjustment-minutes requires --target-local-time"
        )
    if eta_minutes is None and target_local_time is None:
        return None, None, None, None, "not_provided"
    if now_iso is None or store_timezone is None:
        raise ValueError(
            "--now and --store-timezone are required for ETA calculations"
        )

    store_tz = _resolve_timezone(store_timezone)
    local_now = _parse_aware_iso(now_iso, "--now").astimezone(store_tz)
    if target_local_time is not None:
        target_time = _parse_aware_iso(
            target_local_time, "--target-local-time"
        ).astimezone(store_tz)
        target_time = (
            target_time.astimezone(timezone.utc)
            + timedelta(minutes=target_adjustment_minutes)
        ).astimezone(store_tz)
    else:
        target_time = (
            local_now.astimezone(timezone.utc)
            + timedelta(minutes=eta_minutes)
        ).astimezone(store_tz)

    raw_remaining_seconds = (
        target_time.astimezone(timezone.utc)
        - local_now.astimezone(timezone.utc)
    ).total_seconds()
    remaining_seconds = max(0, math.ceil(raw_remaining_seconds))
    remaining_minutes = math.ceil(remaining_seconds / 60)
    target_status = (
        "future" if raw_remaining_seconds > 0 else "due_or_passed"
    )
    return (
        local_now,
        target_time,
        remaining_seconds,
        remaining_minutes,
        target_status,
    )


def calculate_remaining_eta(
    *,
    target_local_time,
    now_iso,
    store_timezone,
    target_adjustment_minutes=0,
):
    """Calculate same-session ETA without weather, queue, or bay inputs."""
    (
        local_now,
        target_time,
        remaining_seconds,
        remaining_minutes,
        target_status,
    ) = (
        _resolve_arrival_timing(
            now_iso=now_iso,
            store_timezone=store_timezone,
            target_local_time=target_local_time,
            target_adjustment_minutes=target_adjustment_minutes,
        )
    )
    return {
        "operation": "eta_only",
        "current_local_time": local_now.isoformat(),
        "target_local_time": target_time.isoformat(),
        "remaining_eta_seconds": remaining_seconds,
        "remaining_eta_minutes": remaining_minutes,
        "target_status": target_status,
    }


def calculate_queue_status(
    *,
    queue_observed_at,
    initial_orders_ahead,
    now_iso,
    store_timezone,
    eta_minutes=None,
    target_local_time=None,
    target_adjustment_minutes=0,
):
    """Return deterministic same-session queue progress for one retained bay."""
    if initial_orders_ahead < 0:
        raise ValueError("--initial-orders-ahead must be zero or greater")
    store_tz = _resolve_timezone(store_timezone)
    observed = _parse_aware_iso(
        queue_observed_at, "--queue-observed-at"
    ).astimezone(store_tz)
    current = _parse_aware_iso(now_iso, "--now").astimezone(store_tz)
    elapsed_seconds = max(
        0,
        int(
            (
                current.astimezone(timezone.utc)
                - observed.astimezone(timezone.utc)
            ).total_seconds()
        ),
    )
    completed_orders = min(
        initial_orders_ahead,
        elapsed_seconds // QUEUE_SECONDS_PER_ORDER,
    )
    remaining_orders = initial_orders_ahead - completed_orders
    (
        _,
        target_time,
        remaining_eta_seconds,
        remaining_eta_minutes,
        target_status,
    ) = _resolve_arrival_timing(
        now_iso=now_iso,
        store_timezone=store_timezone,
        eta_minutes=eta_minutes,
        target_local_time=target_local_time,
        target_adjustment_minutes=target_adjustment_minutes,
    )
    expected_wait_seconds = remaining_orders * QUEUE_SECONDS_PER_ORDER
    estimated_curb_ready_time = current + timedelta(
        seconds=expected_wait_seconds
    )
    eta_margin_seconds = (
        remaining_eta_seconds - expected_wait_seconds
        if remaining_eta_seconds is not None
        else None
    )
    return {
        "operation": "queue_status",
        "current_local_time": current.isoformat(),
        "queue_observed_at": observed.isoformat(),
        "queue_seconds_per_order": QUEUE_SECONDS_PER_ORDER,
        "initial_orders_ahead": initial_orders_ahead,
        "completed_orders": completed_orders,
        "orders_ahead": remaining_orders,
        "expected_wait_seconds": expected_wait_seconds,
        "estimated_curb_ready_time": estimated_curb_ready_time.isoformat(),
        "queue_status": "ready" if remaining_orders == 0 else "in_progress",
        "target_local_time": (
            target_time.isoformat() if target_time is not None else None
        ),
        "remaining_eta_seconds": remaining_eta_seconds,
        "remaining_eta_minutes": remaining_eta_minutes,
        "target_status": target_status,
        "eta_margin_seconds": eta_margin_seconds,
        "eta_status": (
            "not_provided"
            if eta_margin_seconds is None
            else "before_arrival"
            if eta_margin_seconds >= 0
            else "may_extend_beyond_arrival"
        ),
    }


def preferred_attributes(active_conditions, wind_dir):
    """Accumulate every applicable machine-readable bay preference."""
    attributes = {
        "covered": None,
        "lit": None,
        "good_drainage": None,
        "high_wind_protection": None,
        "facing_away_from_wind_degrees": None,
        "shorter_distance_to_entrance": None,
    }
    if any(
        condition in active_conditions
        for condition in ("WET", "HOT", "COLD")
    ):
        attributes["covered"] = True
    if "WET" in active_conditions:
        attributes["good_drainage"] = True
    if "WINDY" in active_conditions:
        attributes["high_wind_protection"] = True
    if any(
        condition in active_conditions
        for condition in ("DARK", "LOW_VISIBILITY")
    ):
        attributes["lit"] = True
    if "WINDY" in active_conditions and wind_dir is not None:
        attributes["facing_away_from_wind_degrees"] = round(
            (wind_dir + 180.0) % 360.0, 1
        )
    if active_conditions:
        attributes["shorter_distance_to_entrance"] = True
    return attributes


def select_forecast_period(
    reference_iso, store_timezone, sunrise_iso, sunset_iso
):
    """Return day or night for a reference time and almanac boundaries."""
    if (
        reference_iso is None
        or store_timezone is None
        or sunrise_iso is None
        or sunset_iso is None
    ):
        return None
    store_tz = _resolve_timezone(store_timezone)
    reference = _parse_aware_iso(
        reference_iso, "--forecast-reference"
    ).astimezone(store_tz)
    sunrise = _parse_aware_iso(sunrise_iso, "--sunrise").astimezone(
        store_tz
    )
    sunset = _parse_aware_iso(sunset_iso, "--sunset").astimezone(store_tz)
    if sunrise >= sunset:
        return "night" if sunset <= reference < sunrise else "day"
    return "day" if sunrise <= reference < sunset else "night"


def _select_forecast(
    selected_period,
    day_cap,
    day_precip,
    day_summary,
    night_cap,
    night_precip,
    night_summary,
):
    if selected_period == "day":
        return day_cap, day_precip, day_summary
    if selected_period == "night":
        return night_cap, night_precip, night_summary

    candidates = (
        (day_cap, day_precip, day_summary),
        (night_cap, night_precip, night_summary),
    )

    def hazard_score(candidate):
        caption, precipitation, _ = candidate
        caption_score = (
            RAIN_CHANCE_PCT
            if caption is not None
            and any(
                token in caption.lower() for token in WET_CAPTION_TOKENS
            )
            else 0
        )
        return max(caption_score, precipitation or 0)

    return max(candidates, key=hazard_score)


def assess_weather(
    *,
    weather_available,
    eta_minutes=None,
    target_local_time=None,
    target_adjustment_minutes=0,
    current_cap=None,
    temp=None,
    feels=None,
    visibility=None,
    wind_dir=None,
    wind_speed=None,
    wind_gust=None,
    day_cap=None,
    day_precip=None,
    day_summary=None,
    night_cap=None,
    night_precip=None,
    night_summary=None,
    now_iso=None,
    store_timezone=None,
    sunrise_iso=None,
    sunset_iso=None,
):
    """Return a weather-only construct used by the skill for bay reasoning."""
    (
        local_now,
        arrival_target,
        remaining_eta_seconds,
        remaining_eta_minutes,
        target_status,
    ) = (
        _resolve_arrival_timing(
            now_iso=now_iso,
            store_timezone=store_timezone,
            eta_minutes=eta_minutes,
            target_local_time=target_local_time,
            target_adjustment_minutes=target_adjustment_minutes,
        )
    )

    if not weather_available:
        return {
            "weather_available": False,
            "weather_status": "unavailable",
            "assessment_basis": "unavailable",
            "target_local_time": (
                arrival_target.isoformat()
                if arrival_target is not None
                else None
            ),
            "remaining_eta_seconds": remaining_eta_seconds,
            "remaining_eta_minutes": remaining_eta_minutes,
            "target_status": target_status,
            "selected_period": None,
            "current_conditions": {},
            "selected_forecast": {},
            "effective_temperature": None,
            "temperature_source": None,
            "primary_band": "UNKNOWN",
            "active_conditions": [],
            "preferred_bay_attributes": {
                "covered": None,
                "lit": None,
                "good_drainage": None,
                "high_wind_protection": None,
                "facing_away_from_wind_degrees": None,
                "shorter_distance_to_entrance": None,
            },
            "reason_codes": ["QUEUE_ONLY"],
            "unevaluated_conditions": list(CONDITION_PRECEDENCE),
        }

    forecast_reference_iso = (
        arrival_target.isoformat()
        if arrival_target is not None
        else now_iso
    )
    selected_period = select_forecast_period(
        forecast_reference_iso,
        store_timezone,
        sunrise_iso,
        sunset_iso,
    )
    selected_cap, selected_precip, selected_summary = _select_forecast(
        selected_period,
        day_cap,
        day_precip,
        day_summary,
        night_cap,
        night_precip,
        night_summary,
    )

    (
        active_conditions,
        unevaluated_conditions,
        effective_temperature,
        temperature_source,
    ) = detect_active_conditions(
        current_cap,
        selected_cap,
        selected_precip,
        temp,
        feels,
        visibility,
        wind_speed,
        wind_gust,
        selected_period == "night" if selected_period is not None else None,
    )
    if "WINDY" in active_conditions and wind_dir is None:
        unevaluated_conditions.append("WIND_DIRECTION")

    has_weather_evidence = any(
        value is not None
        for value in (
            current_cap,
            temp,
            feels,
            visibility,
            wind_speed,
            wind_gust,
            selected_cap,
            selected_precip,
            selected_summary,
        )
    )
    primary_band = next(
        (
            condition
            for condition in CONDITION_PRECEDENCE
            if condition in active_conditions
        ),
        "BENIGN" if has_weather_evidence else "PARTIAL",
    )
    attributes = preferred_attributes(active_conditions, wind_dir)
    reason_codes = [
        REASON_CODES[condition] for condition in active_conditions
    ]
    if not reason_codes:
        reason_codes = [
            "PARTIAL_WEATHER"
            if unevaluated_conditions
            else "NO_WEATHER_PREFERENCE"
        ]

    return {
        "weather_available": True,
        "weather_status": (
            "partial" if unevaluated_conditions else "complete"
        ),
        "assessment_basis": (
            "expected_arrival"
            if arrival_target is not None
            else "current_conditions"
        ),
        "target_local_time": (
            arrival_target.isoformat()
            if arrival_target is not None
            else None
        ),
        "remaining_eta_seconds": remaining_eta_seconds,
        "remaining_eta_minutes": remaining_eta_minutes,
        "target_status": target_status,
        "selected_period": selected_period,
        "current_conditions": {
            "condition": current_cap,
            "temperature": temp,
            "feels_like": feels,
            "visibility": visibility,
            "wind_direction": wind_dir,
            "wind_speed": wind_speed,
            "wind_gust": wind_gust,
        },
        "selected_forecast": {
            "condition": selected_cap,
            "precipitation_chance": selected_precip,
            "summary": selected_summary,
        },
        "effective_temperature": effective_temperature,
        "temperature_source": temperature_source,
        "primary_band": primary_band,
        "active_conditions": active_conditions,
        "preferred_bay_attributes": attributes,
        "reason_codes": reason_codes,
        "unevaluated_conditions": unevaluated_conditions,
    }


def build_parser():
    parser = argparse.ArgumentParser(
        description="Evaluate curbside weather and arrival timing."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)
    assess = subparsers.add_parser("assess")
    assess.add_argument(
        "--weather-available", choices=("true", "false"), required=True
    )
    assess.add_argument("--eta-minutes", type=int)
    assess.add_argument("--target-local-time")
    assess.add_argument("--target-adjustment-minutes", type=int, default=0)
    assess.add_argument("--current-cap")
    assess.add_argument("--temp", type=float)
    assess.add_argument("--feels", type=float)
    assess.add_argument("--visibility", type=float)
    assess.add_argument("--wind-dir", type=float)
    assess.add_argument("--wind-speed", type=float)
    assess.add_argument("--wind-gust", type=float)
    assess.add_argument("--day-cap")
    assess.add_argument("--day-precip", type=float)
    assess.add_argument("--day-summary")
    assess.add_argument("--night-cap")
    assess.add_argument("--night-precip", type=float)
    assess.add_argument("--night-summary")
    assess.add_argument("--now")
    assess.add_argument("--store-timezone")
    assess.add_argument("--sunrise")
    assess.add_argument("--sunset")
    remaining = subparsers.add_parser("remaining")
    remaining.add_argument("--target-local-time", required=True)
    remaining.add_argument("--now", required=True)
    remaining.add_argument("--store-timezone", required=True)
    remaining.add_argument(
        "--target-adjustment-minutes", type=int, default=0
    )
    queue = subparsers.add_parser("queue")
    queue.add_argument("--queue-observed-at", required=True)
    queue.add_argument("--initial-orders-ahead", type=int, required=True)
    queue.add_argument("--now", required=True)
    queue.add_argument("--store-timezone", required=True)
    queue.add_argument("--eta-minutes", type=int)
    queue.add_argument("--target-local-time")
    queue.add_argument("--target-adjustment-minutes", type=int, default=0)
    return parser


def main():
    args = build_parser().parse_args()
    try:
        if args.command == "remaining":
            result = calculate_remaining_eta(
                target_local_time=args.target_local_time,
                now_iso=args.now,
                store_timezone=args.store_timezone,
                target_adjustment_minutes=args.target_adjustment_minutes,
            )
        elif args.command == "queue":
            result = calculate_queue_status(
                queue_observed_at=args.queue_observed_at,
                initial_orders_ahead=args.initial_orders_ahead,
                now_iso=args.now,
                store_timezone=args.store_timezone,
                eta_minutes=args.eta_minutes,
                target_local_time=args.target_local_time,
                target_adjustment_minutes=args.target_adjustment_minutes,
            )
        else:
            result = assess_weather(
                weather_available=args.weather_available == "true",
                eta_minutes=args.eta_minutes,
                target_local_time=args.target_local_time,
                target_adjustment_minutes=args.target_adjustment_minutes,
                current_cap=args.current_cap,
                temp=args.temp,
                feels=args.feels,
                visibility=args.visibility,
                wind_dir=args.wind_dir,
                wind_speed=args.wind_speed,
                wind_gust=args.wind_gust,
                day_cap=args.day_cap,
                day_precip=args.day_precip,
                day_summary=args.day_summary,
                night_cap=args.night_cap,
                night_precip=args.night_precip,
                night_summary=args.night_summary,
                now_iso=args.now,
                store_timezone=args.store_timezone,
                sunrise_iso=args.sunrise,
                sunset_iso=args.sunset,
            )
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
