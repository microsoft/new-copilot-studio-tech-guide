---
name: points-reconciliation
description: Reconcile a customer's BlastPoints loyalty balance after a complex visit (purchases, returns, promo multipliers, expiry) and convert the net balance to store credit. Use when an associate asks about a member's points, or after a return that should claw back previously earned points. The skill runs a bundled Python script that applies the base earn rate, promo multipliers, tier bonus, clawbacks, and expiry, then prints a points ledger and the redeemable store-credit value.
---

# BlastPoints Loyalty Reconciliation (BlastBox Omega)

This skill reconciles a member's **BlastPoints** after a visit that involves any
mix of earning, returns (clawback), promo events, and expiry, and converts the
net balance to store credit. The points math must be exact, so you **run the
bundled `points_reconciliation.py` script** — never estimate points by hand, and
never write your own code.

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
**purchases** (each `amount` plus its `promo_multiplier`), the list of **returns**
to claw back (same shape), and any **expired points**.

### 2. Run the bundled script

Pass the inputs to `points_reconciliation.py`. Purchases and returns are JSON
lists of `{"amount": <dollars>, "promo_multiplier": <x>}`:

```bash
python3 points_reconciliation.py \
  --tier extra \
  --prior-balance 4200 \
  --purchases '[{"amount": 499.99, "promo_multiplier": 3.0}]' \
  --expired-points 800
```

Add `--returns '[{"amount": 79.99, "promo_multiplier": 3.0}]'` when items are
being returned and their points clawed back. The script prints a JSON object with
the ledger, `net_balance`, `redeemable_blocks`, and `store_credit_value`.

### 3. Report the result

- State the **net BlastPoints balance** and the **store-credit value**.
- Show the ledger lines (earn / clawback / expired) so the associate can verify.
- Offer to apply the redeemable store credit to the current transaction.

## Worked example

Plus Extra member, prior balance 4,200, a Triple BLAST Weekend console purchase of
$499.99 (3x), and 800 expired points:

- Earn: $499.99 x 3 x 1.2 (Plus Extra +20%) = **18,000**
- Expired: **-800**
- Net = 4,200 + 18,000 - 800 = **21,400 BlastPoints**
- Redeemable: 21 x 1,000 = **$105.00** store credit
