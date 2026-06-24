#!/usr/bin/env python3
"""BlastPass membership refund + upgrade-settlement calculator.

Two subcommands, both driven by values that come straight from the Membership MCP
(get_membership), the Store Policy Agent, and the Warehouse MCP. The arithmetic
must be exact, so always run this script rather than computing in your head.

  refund   Prorated (or cooling-off) refund for cancelling a BlastPass membership.
  settle   Net due / refund when a defective unit is credited toward a pricier
           model and the membership refund is applied as store credit.

Refund rules (BlastPass Membership Refund Policy):
  1. Cooling-off: if --within-cooling-off is set, refund the full annual price,
     no fee, no proration.
  2. Otherwise: refund the value of the whole unused months remaining in the
     12-month term, minus the tier cancellation fee.
  3. Subtract any non-refundable welcome credit (MEGA tier only).
  4. The refund is never negative.

Usage:
    python3 blastpass_refund.py refund \\
        --annual-price 129.99 --months-remaining 8 --cancellation-fee 10 \\
        [--nonrefundable-credit 0] [--within-cooling-off]

    python3 blastpass_refund.py settle \\
        --defective-credit 399.99 --upgrade-price 499.99 --membership-credit 76.66
"""

import argparse
import sys

TERM_MONTHS = 12


def money(value):
    cents = int(value * 100 + (0.5 if value >= 0 else -0.5))
    return cents / 100.0


def compute_refund(annual_price, months_remaining, cancellation_fee,
                   nonrefundable_credit=0.0, within_cooling_off=False):
    lines = [f"Annual price: ${annual_price:.2f}"]
    if within_cooling_off:
        refund = money(annual_price)
        lines.append("Within cooling-off period -> full refund, no fee.")
        return {"refund": refund, "basis": "cooling_off", "lines": lines}

    monthly = annual_price / TERM_MONTHS
    prorated = money(months_remaining * monthly)
    lines.append(f"Monthly value: ${annual_price:.2f} / {TERM_MONTHS} = ${monthly:.4f}")
    lines.append(f"Prorated: {months_remaining} unused months x ${monthly:.4f} = ${prorated:.2f}")
    refund = prorated
    if cancellation_fee > 0:
        lines.append(f"Cancellation fee: -${cancellation_fee:.2f}")
        refund = money(refund - cancellation_fee)
    if nonrefundable_credit > 0:
        lines.append(f"Non-refundable welcome credit: -${nonrefundable_credit:.2f}")
        refund = money(refund - nonrefundable_credit)
    refund = max(refund, 0.0)
    return {"refund": refund, "basis": "prorated", "lines": lines}


def compute_settlement(defective_credit, upgrade_price, membership_credit):
    upgrade_difference = money(upgrade_price - defective_credit)
    net = money(upgrade_difference - membership_credit)
    lines = [
        f"Defective unit warranty credit: ${defective_credit:.2f}",
        f"Upgrade model price: ${upgrade_price:.2f}",
        f"Upgrade difference: ${upgrade_price:.2f} - ${defective_credit:.2f} = ${upgrade_difference:.2f}",
        f"BlastPass refund applied as store credit: -${membership_credit:.2f}",
    ]
    return {"net": net, "lines": lines}


def _print_lines(lines):
    print("\n".join(lines))
    print("-" * 36)


def main(argv):
    parser = argparse.ArgumentParser(description="BlastPass refund + settlement calculator.")
    sub = parser.add_subparsers(dest="command", required=True)

    r = sub.add_parser("refund", help="Prorated / cooling-off membership refund.")
    r.add_argument("--annual-price", type=float, required=True)
    r.add_argument("--months-remaining", type=int, required=True)
    r.add_argument("--cancellation-fee", type=float, default=0.0)
    r.add_argument("--nonrefundable-credit", type=float, default=0.0)
    r.add_argument("--within-cooling-off", action="store_true")

    s = sub.add_parser("settle", help="Upgrade settlement net due / refund.")
    s.add_argument("--defective-credit", type=float, required=True)
    s.add_argument("--upgrade-price", type=float, required=True)
    s.add_argument("--membership-credit", type=float, default=0.0)

    args = parser.parse_args(argv[1:])

    if args.command == "refund":
        result = compute_refund(
            annual_price=args.annual_price,
            months_remaining=args.months_remaining,
            cancellation_fee=args.cancellation_fee,
            nonrefundable_credit=args.nonrefundable_credit,
            within_cooling_off=args.within_cooling_off,
        )
        _print_lines(result["lines"])
        print(f"REFUND DUE: ${result['refund']:.2f}")
        return 0

    result = compute_settlement(
        defective_credit=args.defective_credit,
        upgrade_price=args.upgrade_price,
        membership_credit=args.membership_credit,
    )
    _print_lines(result["lines"])
    net = result["net"]
    if net >= 0:
        print(f"NET DUE FROM CUSTOMER: ${net:.2f}")
    else:
        print(f"REFUND TO CUSTOMER: ${-net:.2f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
