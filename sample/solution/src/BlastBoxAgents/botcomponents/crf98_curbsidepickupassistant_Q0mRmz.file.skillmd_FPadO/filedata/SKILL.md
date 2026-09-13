---
name: curbside-bay-selector
description: Reason over weather, one current bay snapshot, optional ETA, and verified membership to select and explain one curbside bay.
---

# Curbside Bay Selector and Reasoner

Use this skill after the order and store are verified. Store opening status and
hours do not affect whether this skill runs.

This skill owns the complete bay decision, queue interpretation, ETA comparison,
same-session arrival-time calculation, and evidence-based recommendation
explanation. Do not reveal private chain-of-thought. Return only evidence
grounded in supplied facts.

## Operations

The calling agent must specify exactly one operation:

- `recommend`: Use after live weather has been attempted and Curb Pickup MCP has
  returned exactly one current bay snapshot. Evaluate all candidates before
  selecting one.
- `queue_status`: Use after a recommendation for questions about the queue,
  readiness, remaining time, a new ETA, or an arrival-time adjustment. Keep the
  selected bay unchanged and calculate deterministic queue progress from the
  original `queue_observed_at` at one completed order every 30 seconds. Do not
  accept weather or other bays, call `list_bays`, or reselect a bay.

Use `recommend` only for the initial decision, after a no-available-bay result,
or when the customer explicitly asks to discard the current assignment and
select a different bay. Queue and ETA follow-ups do not reselect the bay; the
selected bay is sticky for the conversation. Retained-weather follow-ups are
handled by the calling agent without invoking this skill.

## Inputs

Use only:

- verified order number, customer name, canonical store name, and store timezone
- operation
- fresh timezone-aware `store_local_time`
- either a new optional customer ETA in minutes or the existing absolute
  `target_local_time`, never both
- optional `target_adjustment_minutes` only when the customer explicitly says
  they will arrive earlier or later than the existing target
- optional exact verified `tier_code`
- for `recommend` only: available MSN Weather values and exactly one `list_bays`
  snapshot
- for `queue_status` only: selected `bay_id`, original `queue_observed_at`,
  original `initial_orders_ahead`, optional `previous_queue_update_time` copied
  from `latest_queue_status.current_local_time`, and no other bay data

Treat a missing or invalid `tier_code` as `standard`. Never infer membership from
a Pass ID, displayed tier, customer statement, order, or identity.

Never retrieve new data or call another tool or agent. For `recommend`, never
call `list_bays` again. Order and customer values are output context only; they
never influence which bay is selected.

## Canonical same-session state

Every successful `recommend` or `queue_status` result must include a
`canonical_state` object containing the complete current state:

- `assigned_bay_id`
- `queue_observed_at`
- `initial_orders_ahead`
- `target_local_time`, which may be null
- `latest_queue_status`, which may be null after the initial recommendation

The complete successful `recommend` result is not a field inside
`canonical_state`. The calling agent retains it separately as
`recommendation_evidence`. A later `queue_status` result updates canonical queue
state but never replaces `recommendation_evidence`.

Initialize this state only from a successful `recommend` result. Preserve values
exactly as returned, including identifier type, timestamp, timezone offset,
numeric type, and null. Never initialize or update state from customer wording,
assistant prose, examples, failed calls, partial results, or another
conversation.

For the active assignment, `assigned_bay_id`, `queue_observed_at`, and
`initial_orders_ahead` are immutable. A successful `queue_status` result may
replace only `target_local_time` and `latest_queue_status`. Failed, cancelled,
malformed, or permission-denied calls change no state.

For `queue_status`, copy the three immutable fields from the supplied canonical
state. Never replace `queue_observed_at` with the current time, replace
`initial_orders_ahead` with a reduced count, or infer a missing value. The
calling agent must not invoke `queue_status` when required state is missing,
ambiguous, or conflicting. If it is invoked with invalid state despite that
guard, return `status: state_unavailable` without running Python or fabricating
state, with only this `customer_message`: `I can't safely update that pickup
queue because the current assignment state is unavailable. Please start a new
pickup request.`

Clear all state only for a new conversation or a new order. For an explicit
request to discard the current assignment and select another bay, clear the bay
and queue fields but preserve `target_local_time` until the new recommendation
replaces it. Do not clear state for queue questions, time-left questions, ETA
changes, tool failures, or ordinary follow-ups. `canonical_state` is internal
structured output and must never be included in `customer_message`.

## Weather-policy step

Run the bundled `blastbox_staging.py` script before evaluating bays. Python owns
weather policy, deterministic arrival-time arithmetic, and elapsed queue
arithmetic. Never pass it order, store name, membership, ETA-derived bay
assumptions, occupancy, candidate bays, or a selected bay.

For available or partial weather, pass every available Metric weather value and
omit unavailable flags:

```bash
python3 blastbox_staging.py assess \
  --weather-available true \
  --eta-minutes 20 \
  --current-cap "Cloudy" \
  --temp 30 --feels 33 --visibility 8 \
  --wind-dir 45 --wind-speed 25 --wind-gust 50 \
  --day-cap "Rain showers" --day-precip 70 \
  --day-summary "Periods of rain are expected." \
  --night-cap "Mostly clear" --night-precip 10 \
  --night-summary "Mostly clear overnight." \
  --now 2026-09-02T18:50:00-05:00 \
  --store-timezone America/Chicago \
  --sunrise 2026-09-02T06:20:00-05:00 \
  --sunset 2026-09-02T19:20:00-05:00
```

Use only these weather fields:

- current weather: condition caption, temperature, feels-like temperature,
  visibility, wind direction, wind speed, and wind gust
- today's forecast: day condition, precipitation chance, and summary; night
  condition, precipitation chance, and summary
- almanac: sunrise and sunset only

Do not pass UV, humidity, pressure, dewpoint, sky code, METAR weather code,
provider icons, symbols, daily high or low, moon data, or coordinates.

Python determines `selected_period` from `target_local_time` when an arrival
target exists, otherwise from fresh store-local current time:

- `day` when `sunrise <= reference time < sunset`
- `night` otherwise
- unknown only when the reference time, sunrise, or sunset is unavailable

When the period is unknown but forecast values exist, Python retains the
more precipitation-exposed day or night forecast instead of discarding both.

Sunrise and sunset are internal classification evidence. Never include them in
customer text.

Python sets `effective_temperature` to feels-like temperature when available,
otherwise current temperature. Use it for both heat and cold evaluation. In
customer text, mention feels-like only when both values exist and differ by at
least 3 degrees.

Omit `--eta-minutes` when the customer provides no ETA. When no live weather
value is available, run the same applicable ETA or target-time flags with:

```bash
python3 blastbox_staging.py assess --weather-available false
```

For an explicit reselect or a retry after no bay was available, preserve the
original absolute arrival target instead of restarting the original relative
ETA:

```bash
python3 blastbox_staging.py assess \
  --weather-available true \
  --target-local-time 2026-09-02T19:10:00-05:00 \
  --now 2026-09-02T19:00:00-05:00 \
  --store-timezone America/Chicago
```

When the customer explicitly changes an existing target by a relative amount,
also pass `--target-adjustment-minutes`. A positive value means later and a
negative value means earlier. When the customer supplies a completely new ETA,
pass only `--eta-minutes`; it replaces the previous target.

For a plain `queue_status` request, run:

```bash
python3 blastbox_staging.py queue \
  --queue-observed-at 2026-09-02T19:00:00-05:00 \
  --initial-orders-ahead 3 \
  --target-local-time 2026-09-02T19:10:00-05:00 \
  --now 2026-09-02T19:00:30-05:00 \
  --store-timezone America/Chicago
```

For a completely new relative ETA, omit `--target-local-time` and run the same
queue command with only the replacement ETA:

```bash
python3 blastbox_staging.py queue \
  --queue-observed-at 2026-09-02T19:00:00-05:00 \
  --initial-orders-ahead 3 \
  --eta-minutes 30 \
  --now 2026-09-02T19:00:30-05:00 \
  --store-timezone America/Chicago
```

For an explicit delay or earlier-arrival adjustment, pass the retained target
and the adjustment together:

```bash
python3 blastbox_staging.py queue \
  --queue-observed-at 2026-09-02T19:00:00-05:00 \
  --initial-orders-ahead 3 \
  --target-local-time 2026-09-02T19:10:00-05:00 \
  --target-adjustment-minutes 10 \
  --now 2026-09-02T19:00:30-05:00 \
  --store-timezone America/Chicago
```

Never pass `--eta-minutes` with `--target-local-time`. Never pass
`--target-adjustment-minutes` without `--target-local-time`.

The `queue` command rejects weather and candidate-bay inputs and returns:

```json
{
  "operation": "queue_status",
  "current_local_time": "2026-09-02T19:00:30-05:00",
  "queue_observed_at": "2026-09-02T19:00:00-05:00",
  "queue_seconds_per_order": 30,
  "initial_orders_ahead": 3,
  "completed_orders": 1,
  "orders_ahead": 2,
  "expected_wait_seconds": 60,
  "estimated_curb_ready_time": "2026-09-02T19:01:30-05:00",
  "queue_status": "in_progress",
  "target_local_time": "2026-09-02T19:10:00-05:00",
  "remaining_eta_seconds": 600,
  "remaining_eta_minutes": 10,
  "target_status": "future",
  "eta_margin_seconds": 540,
  "eta_status": "before_arrival"
}
```

For `queue_status`, choose exactly one mutually exclusive template in this
priority order:

- `queue_status: ready`, including an initial queue of zero:
  `🚗 **Queue update:** The queue ahead of your assigned bay has cleared.`
- no completed order yet and one order remains:
  `🚗 **Queue update:** There is still one order ahead. About 30 seconds remains.`
- no completed order yet and more than one order remains:
  `🚗 **Queue update:** There are still [initial_orders_ahead] orders ahead. About [formatted expected_wait_seconds] remains.`
- progress with more than one order remaining:
  `🚗 **Queue update:** There are now [orders_ahead] orders ahead, down from [initial_orders_ahead]. About [formatted expected_wait_seconds] remains.`
- progress with one order remaining:
  `🚗 **Queue update:** There is now one order ahead, down from [initial_orders_ahead]. About 30 seconds remains.`

When `previous_queue_update_time` is present, add
`Since your last update at [formatted previous_queue_update_time], ` immediately
after `🚗 **Queue update:**`. This timestamp comes only from the retained
`latest_queue_status.current_local_time`; never infer it from conversation text.
For every non-ready result, append
`Estimated curb-ready time is [formatted estimated_curb_ready_time].` Use the
exact Python field and do not calculate the timestamp in the selector.

When a new ETA or adjustment was supplied, add one short sentence confirming the
updated remaining arrival time. For a plain queue or time-left question, do not
state the obvious ETA countdown; report queue progress only. Return
`status: queue_status`, the retained bay ID, Python queue/timing fields,
the complete `canonical_state`, and `customer_message`. Set
`canonical_state.latest_queue_status` to the complete current Python queue result,
including `current_local_time` for use as the next follow-up's remembered update.
Use Python's `eta_margin_seconds` and `eta_status`; never reconstruct them from
rounded minutes.
Return no weather assessment or new recommendation.

For `recommend`, preserve Python's customer-safe weather evidence inside
`weather_assessment`: `assessment_basis`, the recommendation
`evidence_target_local_time`, `current_conditions`, `selected_forecast`,
`selected_period`, `active_conditions`, and effective temperature. Keep
limitations and unevaluated fields internal.

Python returns:

```json
{
  "weather_available": true,
  "weather_status": "complete",
  "assessment_basis": "expected_arrival",
  "target_local_time": "2026-09-02T19:10:00-05:00",
  "remaining_eta_seconds": 1200,
  "remaining_eta_minutes": 20,
  "target_status": "future",
  "selected_period": "day",
  "current_conditions": {
    "condition": "Cloudy",
    "temperature": 30,
    "feels_like": 33,
    "visibility": 8,
    "wind_direction": 45,
    "wind_speed": 25,
    "wind_gust": 50
  },
  "selected_forecast": {
    "condition": "Rain showers",
    "precipitation_chance": 70,
    "summary": "Periods of rain are expected."
  },
  "effective_temperature": 33,
  "temperature_source": "feels",
  "primary_band": "WET",
  "active_conditions": ["WET", "WINDY"],
  "preferred_bay_attributes": {
    "covered": true,
    "lit": null,
    "good_drainage": true,
    "high_wind_protection": true,
    "facing_away_from_wind_degrees": 225.0,
    "shorter_distance_to_entrance": true
  },
  "reason_codes": [
    "PRECIPITATION_PROTECTION",
    "WIND_PROTECTION"
  ],
  "unevaluated_conditions": []
}
```

`primary_band` follows
`WET > HOT > COLD > WINDY > DARK > LOW_VISIBILITY > BENIGN` for concise
summarization only. Never let precedence suppress another active condition or
applicable bay preference.

For `weather_status: partial`, use every known active condition and attribute.
Never treat an unevaluated condition as false and never discard confirmed
weather facts because another field is missing. A partial assessment with
confirmed non-hazardous conditions may still use `primary_band: BENIGN`. For
`weather_status: unavailable`, apply no weather preference and reason from
availability and queue only. Missing and unevaluated fields always remain
internal.

## Bay reasoning

Apply these rules in order:

1. Exclude every bay where `occupied` is true.
2. If no bay remains, return `status: no_available_bay`. Do not select a bay.
3. Start with all unoccupied bays as the candidate set.
4. Process active weather conditions in Python's returned order.
5. For `WET`, `HOT`, or `COLD`, prefer `covered: true`. Apply the covered filter
   once even when several of these conditions are active, but preserve every
   active condition in the explanation.
6. For `WET`, rank `drainage` as `good > fair > poor` and retain candidates with
   the best available drainage value.
7. For `WINDY`, rank `wind_protection` as `high > medium > low` and retain
   candidates with the best available protection value. Then, when
   `facing_away_from_wind_degrees` exists, calculate each retained candidate's
   smallest circular angular difference and retain the closest candidate or tied
   closest candidates.
8. For `DARK` or `LOW_VISIBILITY`, prefer `lit: true`.
9. When any active condition exists, retain candidates with the shortest
   `distance_to_entrance_m` after applying the other weather preferences.
10. Apply a weather filter only when at least one current candidate has a valid
   value that satisfies it.
   If none does, retain the current candidates and record that exact preference
   in `weather_degradations`.
11. Never discard all known weather reasoning because one condition is
   unevaluated. Record each unevaluated condition as a limitation without
   inventing a result.
12. After weather suitability, retain candidates with the lowest
    `expected_wait_seconds`.
13. Only when those waits are equal, apply membership:
    - `extra` or `mega` prefers `priority: true`
    - `standard` or `plus` prefers `priority: false`
    Apply that preference only when at least one tied candidate satisfies it.
    Otherwise retain the complete tied candidate set and record that membership
    did not resolve the tie.
14. Membership never overrides occupancy, weather suitability, or a shorter
    queue.
15. If candidates remain tied, retain the shortest `distance_to_entrance_m`.
16. If candidates still remain tied, select the lowest `bay_id`.

For `recommend`, use `orders_ahead`, `expected_wait_seconds`, and
`queue_observed_at` exactly as returned by MCP. Do not recalculate them during
bay selection. For `queue_status`, pass the original queue anchors to Python and
use its returned elapsed queue values unchanged.

Treat `wind_protection`, `drainage`, and `distance_to_entrance_m` as comparison
data only. Never ask Python to evaluate bays.

## Queue and ETA reasoning

Generate exact queue wording:

- zero: `There are no orders ahead.`
- one: `There is one order ahead.`
- more than one: `There are [n] orders ahead.`

When ETA is present, use `remaining_eta_seconds`, not the original relative ETA
or rounded minutes, for every queue comparison:

- wait seconds less than or equal to ETA seconds: `eta_status: before_arrival`
- wait seconds greater than ETA seconds: `eta_status: may_extend_beyond_arrival`

When ETA is absent, return `eta_status: not_provided` and never invent an arrival
time, clock time, or availability time. When `target_status` is `due_or_passed`,
use zero as the remaining ETA and state that any positive wait may extend beyond
the expected arrival.

## Decision explanation

The explanation must be composed from the actual decision evidence. It must not
be a fixed sentence.

Include:

- why the selected bay remained available
- every active weather condition that affected its preferred attributes
- every weather preference that could not be satisfied
- how drainage, wind protection, lighting, or entrance distance affected the
  comparison when applicable
- why it won the queue comparison
- whether verified membership or lowest bay ID actually resolved a tie

Do not claim that membership affected the result unless
`membership_resolved_tie` is true. Do not mention priority otherwise. Do not
claim a weather attribute that Python did not return or the selected bay does not
have. Keep unevaluated conditions and missing weather fields in structured
decision evidence only; never include them in `recommendation_reason`.

## Customer message

Compose one complete `customer_message` for the calling agent to present
unchanged. Write it as a fixed, easy-to-scan five-block customer response with
one blank line between blocks. The format, labels, block order, and line breaks
are mandatory; do not collapse the response into one paragraph.

For `recommend`, `customer_message` is the only customer-facing recommendation
text. Structured
fields support orchestration, diagnostics, and PDF rendering. The calling agent
must not display those fields separately or compose a second recommendation.
Return no preface, progress update, duplicate queue summary, or transition such
as `I'll pull your order details` or `Wait — here is your recommendation`.

Return a `weather_icon` selected only from confirmed weather evidence:

- `WET`: `🌧️`
- `HOT`: `🌡️`
- `COLD`: `❄️`
- `WINDY`: `💨`
- `DARK`: `🌙`
- `LOW_VISIBILITY`: `🌫️`
- `BENIGN` with a confirmed current or selected-forecast condition: `🌤️`
- no confirmed current or selected-forecast condition: an empty string

When several conditions are active, use the icon for `primary_band`; describe
every confirmed condition that affected selection in the prose.

Return two free-text fields composed only after deterministic comparison:

- `weather_summary`: a concise natural summary using every useful confirmed
  fact that is relevant to the expected pickup. Lead with useful current
  conditions. Include selected-forecast facts only when they materially affect
  bay selection, customer guidance, or conditions expected around the retained
  arrival target. When included, connect them explicitly to the pickup with
  wording such as `Around your expected arrival`; do not append a general daily
  or overnight weather report. Include actual temperature, feels-like only when
  it differs by at least 3 degrees, visibility when relevant, and current wind.
  Do not produce a raw field list or repeat equivalent facts.
- `customer_guidance`: concise practical guidance grounded in the selected
  bay's cover, drainage, wind protection, facing, lighting, and entrance
  distance. Mention only attributes that are relevant to confirmed conditions.
  Do not state the bay number in this field.

Free text never changes the selected bay. Never mention unavailable values,
unevaluated conditions, omitted fields, API behavior, or internal limitations.

Track `weather_narrowed_candidates` as true only when an applied weather filter
reduced the candidate count. Track `queue_narrowed_candidates` as true only when
the shortest-wait step reduced the candidate count.

Return one deterministic `decision_driver` using this precedence:

1. `membership_tie_break` when verified membership resolved an equal-wait tie.
2. `lowest_bay_id_tie_break` when the lowest bay ID resolved the final tie.
3. `weather_and_queue` when both narrowing flags are true.
4. `weather` when only `weather_narrowed_candidates` is true.
5. `queue` when only `queue_narrowed_candidates` is true, including when a
   retained weather preference matched every candidate and therefore did not
   narrow the set.
6. `distance_tie_break` when entrance distance was the final candidate-reducing
   step.
7. `availability` when occupancy left only one candidate before weather and
   queue reasoning, so neither narrowing flag is true.

When ETA is present, return signed `eta_margin_seconds` as
`remaining_eta_seconds` minus `expected_wait_seconds`. A positive value is the
remaining buffer, zero matches the expected arrival, and a negative value is
seconds beyond it. When ETA is absent, return `eta_margin_seconds: null`.

Compose `customer_message` in exactly this order:

1. Recommendation and weather block: start with `weather_icon` when nonempty,
   address the customer by first name when available, and say
   `[Name], here is your curbside pickup plan for [canonical store].` Continue on the same
   line with `weather_summary`. For benign weather, describe the available
   current and selected-period facts without claiming that missing conditions
   were evaluated. When no weather fact is confirmed, omit only the weather
   detail and never explain the omission. When any current or selected-forecast
   fact is confirmed, `weather_summary` must be nonempty and included here.
2. Queue block: start exactly with `🚗 **Queue:**`. State orders ahead and
   expected wait, then explain how the assigned bay's queue compares with the
   retained candidates. Claim the shortest queue only when supported by the
   comparison. Do not state the bay number. Do not repeat these queue facts in
   another block.
3. ETA block, only when ETA exists: start exactly with `⏱️ **ETA:**`. State the
   customer's arrival estimate and whether the previously stated wait leaves a
   buffer, exactly matches the ETA, or may extend beyond arrival. Do not repeat
   the wait value from the Queue block. Include the absolute
   `eta_margin_seconds`. Format it naturally as minutes and seconds. Use wording
   such as `That leaves a 19-minute buffer before your expected arrival.` Never describe
   a positive margin as merely "inside" the ETA. Never use wording such as
   `while the current wait is [value]` because the Queue block already states it.
4. Assignment block: start exactly with
   `💡 **Assigned Bay is [bay_id]:**`. This is the only place in the complete
   customer message that may state the bay number. Synthesize the actual decision intelligence:
   availability, weather suitability, queue comparison, and the decisive
   `decision_driver`, then add `customer_guidance`. Explain why this bay is
   preferable to the alternatives, not merely that it is available. Mention membership only for
   `membership_tie_break`. Mention lowest bay ID only when it actually resolved
   the final tie. Do not repeat exact orders-ahead, wait, or ETA values here.
   Describe an unoccupied bay as `available`, never `open`; `open` can be
   confused with store opening status.
5. Disclaimer block: use exactly
   `This is a current assignment rather than a reservation, so weather, occupancy, or queue changes may affect it.`

Blocks 1, 2, 4, and 5 are always required for a recommendation. Block 3 is
required when ETA exists and must be omitted when ETA is absent. Put exactly one
blank line between included blocks. Do not add any other heading, label, bullet,
preface, summary, or closing sentence.

Before returning, verify that `customer_message` contains exactly one Queue
label, at most one ETA label, exactly one Assigned Bay label, and exactly one
disclaimer. It must not contain any customer-facing text before the
recommendation sentence.

Use only confirmed available evidence in `customer_message`. Keep
`weather_limitations` and `unevaluated_conditions` in structured output for
internal reasoning, diagnostics, and graceful selection, but never expose
missing readings, unavailable field names, or unevaluated conditions to the
customer.

Do not add an overall heading, bullets, an order number, raw field names, or any
labels other than the required `Queue`, `ETA`, and `Assigned Bay is [bay_id]`
labels. Never repeat the assigned bay number in the recommendation, Queue, ETA,
guidance, or disclaimer blocks.
Never mention store opening status, store closure, posted hours, or local store
time. Do not ask whether the customer wants a PDF; the calling agent owns that question.

## Output

Return this structure:

```json
{
  "status": "recommended",
  "order_number": "ORD-10502",
  "customer_name": "Jordan Pixel",
  "store_name": "BlastBox Pixel Heights",
  "selected_bay": {
    "bay_id": 2,
    "covered": true,
    "lit": true,
    "facing": 180,
    "wind_protection": "high",
    "drainage": "good",
    "distance_to_entrance_m": 20,
    "orders_ahead": 2,
    "queue_seconds_per_order": 30,
    "expected_wait_seconds": 60,
    "expected_wait_minutes": 1.0
  },
  "weather_assessment": {
    "weather_status": "complete",
    "assessment_basis": "expected_arrival",
    "evidence_target_local_time": "2026-09-02T19:10:00-05:00",
    "primary_band": "WET",
    "selected_period": "day",
    "effective_temperature": 33,
    "current_conditions": {
      "condition": "Cloudy",
      "temperature": 30,
      "feels_like": 33,
      "visibility": 8,
      "wind_direction": 45,
      "wind_speed": 25,
      "wind_gust": 50
    },
    "selected_forecast": {
      "condition": "Rain showers",
      "precipitation_chance": 70,
      "summary": "Periods of rain are expected."
    },
    "active_conditions": ["WET", "WINDY"],
    "reason_codes": [
      "PRECIPITATION_PROTECTION",
      "WIND_PROTECTION"
    ],
    "unevaluated_conditions": []
  },
  "decision_evidence": {
    "weather_matches": [
      "Covered for expected precipitation",
      "Good drainage for expected precipitation",
      "High wind protection and closest available facing for strong wind",
      "Shortest weather-exposed walk among the suitable bays"
    ],
    "weather_degradations": [],
    "weather_limitations": [],
    "queue_comparison": "Current wait for the bay meeting the retained weather preferences",
    "weather_narrowed_candidates": true,
    "queue_narrowed_candidates": false,
    "membership_resolved_tie": false,
    "lowest_bay_id_resolved_tie": false
  },
  "weather_icon": "🌧️",
  "decision_driver": "weather",
  "queue_observed_at": "2026-09-02T18:50:00-05:00",
  "initial_orders_ahead": 2,
  "canonical_state": {
    "assigned_bay_id": 2,
    "queue_observed_at": "2026-09-02T18:50:00-05:00",
    "initial_orders_ahead": 2,
    "target_local_time": "2026-09-02T19:10:00-05:00",
    "latest_queue_status": null
  },
  "eta_margin_seconds": 1140,
  "target_local_time": "2026-09-02T19:10:00-05:00",
  "remaining_eta_seconds": 1200,
  "remaining_eta_minutes": 20,
  "target_status": "future",
  "weather_summary": "It is currently cloudy and 30°, feeling like 33°, with winds of 25 km/h and gusts up to 50 km/h. The daytime forecast calls for rain showers with a 70% chance of precipitation.",
  "customer_guidance": "It provides cover, good drainage, strong wind protection, and the best available orientation for the current wind.",
  "customer_message": "🌧️ Jordan, here is your curbside pickup plan for BlastBox Pixel Heights. It is currently cloudy and 30°, feeling like 33°, with winds of 25 km/h and gusts up to 50 km/h. The daytime forecast calls for rain showers with a 70% chance of precipitation.\n\n🚗 **Queue:** There are two orders ahead, with an expected wait of one minute. This is the queue that applies after the weather requirements were applied.\n\n⏱️ **ETA:** You expect to arrive in 20 minutes. That leaves a 19-minute buffer before your expected arrival.\n\n💡 **Assigned Bay is 2:** It provides cover, good drainage, strong wind protection, and the best available orientation for the current wind.\n\nThis is a current assignment rather than a reservation, so weather, occupancy, or queue changes may affect it.",
  "eta_status": "before_arrival",
  "recommendation_reason": "The assigned bay is available, covered, has good drainage, provides high wind protection, and has the best available orientation for the current wind."
}
```

For no available bay, return:

```json
{
  "status": "no_available_bay",
  "selected_bay": null,
  "canonical_state": {
    "assigned_bay_id": null,
    "queue_observed_at": null,
    "initial_orders_ahead": null,
    "target_local_time": "2026-09-02T19:10:00-05:00",
    "latest_queue_status": null
  },
  "target_local_time": "2026-09-02T19:10:00-05:00",
  "remaining_eta_minutes": 20,
  "target_status": "future",
  "weather_icon": "",
  "decision_driver": null,
  "eta_margin_seconds": null,
  "customer_message": "No curbside bay is currently available. Please try again shortly.",
  "recommendation_reason": "No curbside bay is currently available."
}
```

Return the timing fields even when no bay is available so the calling agent can
retain an arrival target created or adjusted during that decision run. Use null
timing values when the customer has no ETA.

For invalid or unavailable canonical state, return:

```json
{
  "status": "state_unavailable",
  "customer_message": "I can't safely update that pickup queue because the current assignment state is unavailable. Please start a new pickup request."
}
```

Never return or expose a BlastPass ID, `tier_code`, membership display name,
`occupied_by`, other customer details, payment, order items, raw tool output, or
private reasoning.
