#!/usr/bin/env python3
"""BlastBox Omega — MEGA Bundle Return & Settlement calculator (reference logic).

Computes a combined settlement when a customer returns a multi-item bundle and
(optionally) also cancels a BlastPass membership in the same visit.

Rules (mirror the Store Returns & Exchanges Policy + BlastPass terms):
  * Restocking fee: 15% on OPENED consoles/hardware (console, accessory) returned
    like-new; WAIVED if the item is defective; $0 for sealed/new items.
  * Non-returnable lines (day-one MEGA exclusives, revealed digital codes,
    opened physical games) refund $0 and are flagged.
  * Store-credit payout adds a +10% goodwill bonus on the eligible MERCHANDISE
    total only — NOT on fees, NOT on tax, NOT on the membership proration refund.
  * Sales tax is refunded proportionally on the net merchandise refunded.
  * The membership proration refund (already computed elsewhere) is added at the
    end with no bonus and no tax.
"""
import argparse, json


def money(v):
    cents = int(v * 100 + (0.5 if v >= 0 else -0.5))
    return cents / 100.0


HARDWARE = {"console", "accessory"}


def settle(items, payout_method, tax_rate, membership_refund):
    lines = []
    merch_net = 0.0
    for it in items:
        name = it["name"]
        base = money(it["price"] * it.get("qty", 1))
        if not it.get("returnable", True):
            lines.append({"item": name, "base": base, "fee": 0.0,
                          "net": 0.0, "note": "NON-RETURNABLE — $0"})
            continue
        fee = 0.0
        cond = it.get("condition", "new")
        if it.get("category") in HARDWARE and cond == "opened" and not it.get("defective"):
            fee = money(base * 0.15)
        elif cond == "defective" or it.get("defective"):
            fee = 0.0
        net = money(base - fee)
        merch_net += net
        note = "defective — fee waived" if it.get("defective") else (
            "15% restocking (opened)" if fee else "no fee")
        lines.append({"item": name, "base": base, "fee": fee, "net": net, "note": note})

    merch_net = money(merch_net)
    tax_refund = money(merch_net * tax_rate)
    bonus = money(merch_net * 0.10) if payout_method == "store_credit" else 0.0
    total = money(merch_net + tax_refund + bonus + membership_refund)
    return {
        "line_items": lines,
        "merchandise_net": merch_net,
        "tax_refund": tax_refund,
        "store_credit_bonus": bonus,
        "membership_proration_refund": money(membership_refund),
        "payout_method": payout_method,
        "settlement_total": total,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--items", required=True, help="JSON list of line items")
    ap.add_argument("--payout", choices=["tender", "store_credit"], default="tender")
    ap.add_argument("--tax-rate", type=float, default=0.0)
    ap.add_argument("--membership-refund", type=float, default=0.0)
    a = ap.parse_args()
    result = settle(json.loads(a.items), a.payout, a.tax_rate, a.membership_refund)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
