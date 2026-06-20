---
name: points-reconciliation
description: Reconcile a customer's BlastPoints loyalty balance after a complex visit (purchases, returns, promo multipliers, expiry) and convert the net balance to store credit. Use when an associate asks about a member's points, or after a return that should claw back previously earned points. The skill writes and runs a short Python snippet that applies the base earn rate, promo multipliers, tier bonus, clawbacks, and expiry, then prints a points ledger and the redeemable store-credit value.
---

# BlastPoints Loyalty Reconciliation (BlastBox Omega)

This skill reconciles a member's **BlastPoints** after a visit that involves any
mix of earning, returns (clawback), promo events, and expiry, and converts the
net balance to store credit. The points math must be exact, so you **generate and
run a short Python snippet** — never estimate points by hand.

## When to use

Use this skill when an associate asks things like:
- "How many BlastPoints does this member have after today?"
- "They returned a console — claw back the points they earned on it."
- "What's that worth in store credit?"

Confirm the member's **tier** with the Membership MCP / Store Policy Agent first,
and confirm any **promo multiplier** in effect with the Store Policy Agent
(Policy RAG MCP, `search_policy "BlastPoints promo"`).

## The points rules (single source of truth)

These mirror the BlastPoints Loyalty Program Terms held by the Store Policy Agent:

1. **Base earn:** 10 points per $1 of merchandise.
2. **Promo multiplier:** promo events (e.g. **Triple BLAST Weekend** = 3x)
   multiply the **base** earn **before** the tier bonus is applied.
3. **Tier bonus on earned points:** Plus **+0%**, Plus Extra **+20%**, MEGA
   **+50%**.
4. **Clawback:** returned items remove points using the **same** formula (the
   promo multiplier and tier bonus that applied to the original purchase).
5. **Expiry:** points older than 90 days while the account was inactive are
   removed (provided as a number).
6. **Redemption:** **1,000 points = $5.00** store credit, in whole 1,000-point
   blocks. The net balance never goes below 0.

## Workflow

### 1. Gather the inputs

Get the **tier** (`plus` / `extra` / `mega`), the **prior balance**, the list of
**purchases** (`amount`, `promo_multiplier`), the list of **returns** to claw back
(`amount`, `promo_multiplier`), and any **expired points**.

### 2. Generate and run this Python

Write and execute a snippet like the following, substituting the real values:

```python
TIER_BONUS = {"plus": 0.0, "extra": 0.20, "mega": 0.50}

def earn(amount, promo, bonus):
    return int(round(10.0 * amount * promo * (1.0 + bonus)))

# --- inputs (from the Membership MCP, Policy RAG MCP, and the associate) ---
tier = "mega"
prior_balance = 4200
purchases = [
    {"amount": 499.99, "promo_multiplier": 3.0},   # Triple BLAST Weekend
    {"amount": 59.99,  "promo_multiplier": 1.0},
]
returns = [
    {"amount": 79.99,  "promo_multiplier": 3.0},    # clawback
]
expired_points = 800

bonus = TIER_BONUS[tier]
lines, earned, clawback = [], 0, 0
for p in purchases:
    pts = earn(p["amount"], p["promo_multiplier"], bonus)
    earned += pts
    lines.append(f"Earn ${p['amount']:.2f} x{p['promo_multiplier']:g} promo +{int(bonus*100)}% tier = +{pts}")
for r in returns:
    pts = earn(r["amount"], r["promo_multiplier"], bonus)
    clawback += pts
    lines.append(f"Clawback ${r['amount']:.2f} (returned) = -{pts}")
if expired_points:
    lines.append(f"Expired points = -{int(expired_points)}")

net = max(int(prior_balance + earned - clawback - int(expired_points)), 0)
blocks = net // 1000
store_credit = blocks * 5.0

print("\n".join(lines))
print(f"Prior balance: {int(prior_balance)}")
print(f"Net balance:   {net} BlastPoints")
print(f"Redeemable:    {int(blocks)} x 1,000 = ${store_credit:.2f} store credit")
```

### 3. Report the result

- State the **net BlastPoints balance** and the **store-credit value**.
- Show the ledger lines (earn / clawback / expired) so the associate can verify.
- Offer to apply the redeemable store credit to the current transaction.

## Worked example

MEGA member, prior balance 4,200, a Triple BLAST Weekend purchase of $499.99 (3x)
plus a $59.99 item, a clawback on a returned $79.99 day-one item (3x), and 800
expired points:

- Earn: $499.99 x3 x1.5 = **22,500**; $59.99 x1 x1.5 = **900**
- Clawback: $79.99 x3 x1.5 = **-3,600**
- Expired: **-800**
- Net = 4,200 + 23,400 - 3,600 - 800 = **23,200 BlastPoints**
- Redeemable: 23 x 1,000 = **$115.00** store credit
