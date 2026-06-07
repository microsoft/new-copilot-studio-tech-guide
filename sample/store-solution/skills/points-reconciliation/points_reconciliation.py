#!/usr/bin/env python3
"""BlastBox Omega — BlastPoints Loyalty Reconciliation calculator (reference logic).

Reconciles a member's BlastPoints after a complex visit (purchases, returns,
promos, expiry) and converts the net balance to store credit.

Rules (mirror the BlastPoints Loyalty Program Terms):
  * Base earn: 10 points per $1 of merchandise.
  * Promo events (e.g. Triple BLAST Weekend) multiply the BASE earn by the promo
    multiplier BEFORE the tier bonus is applied.
  * Tier bonus on earned points: Plus +0%, Plus Extra +20%, MEGA +50%.
  * Returned items are clawed back: points are removed using the SAME formula
    (promo multiplier + tier bonus that applied to the original purchase).
  * Expired points (older than 90 days while the account was inactive) are
    removed as provided.
  * Redemption: 1,000 points = $5.00 store credit (whole 1,000-point blocks).
"""
import argparse, json

TIER_BONUS = {"plus": 0.0, "extra": 0.20, "mega": 0.50}


def earn(amount, promo_multiplier, tier_bonus):
    base = 10.0 * amount
    return int(round(base * promo_multiplier * (1.0 + tier_bonus)))


def reconcile(tier, prior_balance, purchases, returns, expired_points):
    bonus = TIER_BONUS[tier]
    lines = []
    earned = 0
    for p in purchases:
        pts = earn(p["amount"], p.get("promo_multiplier", 1.0), bonus)
        earned += pts
        lines.append({"type": "earn", "amount": p["amount"],
                      "promo_multiplier": p.get("promo_multiplier", 1.0),
                      "points": pts})
    clawback = 0
    for r in returns:
        pts = earn(r["amount"], r.get("promo_multiplier", 1.0), bonus)
        clawback += pts
        lines.append({"type": "clawback", "amount": r["amount"],
                      "promo_multiplier": r.get("promo_multiplier", 1.0),
                      "points": -pts})
    if expired_points:
        lines.append({"type": "expired", "points": -int(expired_points)})

    net_balance = int(prior_balance + earned - clawback - int(expired_points))
    if net_balance < 0:
        net_balance = 0
    blocks = net_balance // 1000
    store_credit = money(blocks * 5.0)
    return {
        "tier": tier,
        "tier_bonus_pct": int(bonus * 100),
        "prior_balance": int(prior_balance),
        "earned": earned,
        "clawback": clawback,
        "expired": int(expired_points),
        "ledger": lines,
        "net_balance": net_balance,
        "redeemable_blocks": int(blocks),
        "store_credit_value": store_credit,
    }


def money(v):
    cents = int(v * 100 + 0.5)
    return cents / 100.0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tier", choices=["plus", "extra", "mega"], required=True)
    ap.add_argument("--prior-balance", type=float, default=0)
    ap.add_argument("--purchases", default="[]", help="JSON list {amount, promo_multiplier}")
    ap.add_argument("--returns", default="[]", help="JSON list {amount, promo_multiplier}")
    ap.add_argument("--expired-points", type=float, default=0)
    a = ap.parse_args()
    print(json.dumps(reconcile(a.tier, a.prior_balance, json.loads(a.purchases),
                               json.loads(a.returns), a.expired_points), indent=2))


if __name__ == "__main__":
    main()
