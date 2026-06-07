#!/usr/bin/env python3
"""BlastPass membership refund calculator (reference implementation).

Scenario 1 - a store associate is cancelling a customer's BlastPass console
membership and needs the prorated refund amount.

In production the agent CANNOT run a bundled script file. Instead the skill's
SKILL.md instructs the agent to *generate and run* an equivalent Python snippet
on the fly. This file is the canonical reference for that logic: keep the skill
instructions and this script in sync.

The Store Policy Agent supplies the tier, the annual price, the cancellation fee,
and the proration rule (from its policy documents). This calculator turns those
inputs into an exact refund figure.

Refund rules (BlastPass Membership Refund Policy):
  1. Cooling-off: within 14 days of activation AND under 2 hours of streaming
     used  ->  full refund of the annual price, no fee.
  2. Otherwise: prorated refund for whole UNUSED months remaining in the
     12-month term, minus the tier's cancellation fee (never below 0).
  3. BlastPass Plus Extra MEGA!!! includes a one-time $20 welcome credit that is
     non-refundable and is deducted from any prorated refund.

Usage:
    python3 blastpass_refund.py \\
        --tier extra \\
        --start-date 2026-02-26 \\
        --current-date 2026-06-06 \\
        [--annual-price 129.99] \\
        [--cancellation-fee 10] \\
        [--hours-used 12] \\
        [--non-refundable-credit 0]

Tiers (defaults if --annual-price / --cancellation-fee are omitted):
    plus  -> BlastPass Plus              $79.99/yr,  fee $0
    extra -> BlastPass Plus Extra        $129.99/yr, fee $10
    mega  -> BlastPass Plus Extra MEGA!!! $199.99/yr, fee $25, $20 non-refundable
"""

import argparse
import datetime as _dt
import sys

TERM_DAYS = 365
TERM_MONTHS = 12
COOLING_OFF_DAYS = 14
COOLING_OFF_MAX_HOURS = 2

TIERS = {
    "plus": {"name": "BlastPass Plus", "annual_price": 79.99, "fee": 0.0, "credit": 0.0},
    "extra": {"name": "BlastPass Plus Extra", "annual_price": 129.99, "fee": 10.0, "credit": 0.0},
    "mega": {"name": "BlastPass Plus Extra MEGA!!!", "annual_price": 199.99, "fee": 25.0, "credit": 20.0},
}


def _parse_date(value):
    try:
        return _dt.date.fromisoformat(value)
    except ValueError:
        raise argparse.ArgumentTypeError(
            f"'{value}' is not a valid date (expected YYYY-MM-DD)."
        )


def _money(value):
    cents = int(value * 100 + (0.5 if value >= 0 else -0.5))
    return cents / 100.0


def compute_refund(
    tier_name,
    annual_price,
    cancellation_fee,
    start_date,
    current_date,
    hours_used,
    non_refundable_credit=0.0,
):
    days_used = (current_date - start_date).days
    lines = [f"Tier: {tier_name} (${annual_price:.2f}/yr)"]
    lines.append(f"Days since activation: {days_used}")

    # Rule 1 - cooling-off full refund.
    if days_used <= COOLING_OFF_DAYS and hours_used < COOLING_OFF_MAX_HOURS:
        refund = _money(annual_price)
        lines.append(
            f"Within {COOLING_OFF_DAYS}-day cooling-off period and under "
            f"{COOLING_OFF_MAX_HOURS}h used -> full refund."
        )
        return {"refund": refund, "basis": "cooling_off", "lines": lines}

    # Rule 2 - prorate whole unused months.
    unused_days = max(TERM_DAYS - days_used, 0)
    unused_full_months = unused_days // 30
    monthly = annual_price / TERM_MONTHS
    prorated = _money(monthly * unused_full_months)
    lines.append(
        f"Unused days: {unused_days} -> {unused_full_months} whole unused months"
    )
    lines.append(
        f"Monthly value: ${monthly:.4f} x {unused_full_months} = ${prorated:.2f}"
    )

    refund = prorated
    if cancellation_fee > 0:
        lines.append(f"Cancellation fee: -${cancellation_fee:.2f}")
        refund = _money(refund - cancellation_fee)

    # Rule 3 - non-refundable welcome credit (MEGA tier).
    if non_refundable_credit > 0:
        lines.append(f"Non-refundable welcome credit: -${non_refundable_credit:.2f}")
        refund = _money(refund - non_refundable_credit)

    refund = max(refund, 0.0)
    return {"refund": refund, "basis": "prorated", "lines": lines}


def _format(result):
    out = list(result["lines"])
    out.append("-" * 36)
    out.append(f"REFUND DUE: ${result['refund']:.2f}")
    return "\n".join(out)


def main(argv):
    parser = argparse.ArgumentParser(description="BlastPass membership refund calculator.")
    parser.add_argument("--tier", choices=sorted(TIERS), help="plus | extra | mega")
    parser.add_argument("--start-date", required=True, type=_parse_date)
    parser.add_argument("--current-date", required=True, type=_parse_date)
    parser.add_argument("--annual-price", type=float)
    parser.add_argument("--cancellation-fee", type=float)
    parser.add_argument("--hours-used", default=0.0, type=float)
    parser.add_argument("--non-refundable-credit", type=float)
    args = parser.parse_args(argv[1:])

    defaults = TIERS.get(args.tier, {})
    tier_name = defaults.get("name", args.tier or "Unknown tier")
    annual_price = args.annual_price if args.annual_price is not None else defaults.get("annual_price")
    cancellation_fee = (
        args.cancellation_fee if args.cancellation_fee is not None else defaults.get("fee", 0.0)
    )
    credit = (
        args.non_refundable_credit
        if args.non_refundable_credit is not None
        else defaults.get("credit", 0.0)
    )

    if annual_price is None:
        print("Error: provide --tier or --annual-price so the price is known.")
        return 1
    if args.current_date < args.start_date:
        print("Error: current date is before the activation date.")
        return 1

    result = compute_refund(
        tier_name=tier_name,
        annual_price=annual_price,
        cancellation_fee=cancellation_fee or 0.0,
        start_date=args.start_date,
        current_date=args.current_date,
        hours_used=args.hours_used,
        non_refundable_credit=credit or 0.0,
    )
    print(_format(result))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
