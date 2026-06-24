---
name: prorated-refund-calculator
description: Calculate the exact prorated refund when a customer cancels a BlastPass membership, and the net settlement when a defective unit is traded up to a pricier model. Use after the Store Policy Agent confirms the refund rule and the Membership MCP has returned the membership details. The skill runs a bundled Python script so the arithmetic is exact.
---

# Prorated Refund Calculator (BlastPass)

This skill computes the **exact** refund owed when a customer cancels their
BlastPass membership, and the **net settlement** when a defective unit is credited
toward a pricier model. The math must be exact and consistent, so you **run the
bundled `blastpass_refund.py` script** — never compute the refund in your head, and
never write your own code.

## When to use

Use this skill in a BlastPass cancellation or trade-up, once you have:

1. The **Store Policy Agent** has confirmed the member's tier and that a
   **prorated refund** (or cooling-off full refund) applies.
2. The **Membership MCP** (`get_membership`) has returned the membership record,
   including `annual_price`, `months_remaining`, `cancellation_fee`,
   `nonrefundable_credit`, and `within_cooling_off`.

Do **not** invent any of these numbers. They come from the Membership MCP and the
Store Policy Agent.

## The refund rules (single source of truth)

1. **Cooling-off (full refund).** If `within_cooling_off` is true, refund the full
   `annual_price`. No fee, no proration.
2. **Prorated cancellation (the usual case).** Otherwise refund the value of the
   whole unused months left on the 12-month term, then subtract the tier
   cancellation fee.
3. **Non-refundable credit.** Subtract `nonrefundable_credit` (only the MEGA tier
   has one). For other tiers it is 0.
4. The refund is **never negative**.

## Workflow

### 1. Compute the membership refund

Take `annual_price`, `months_remaining`, `cancellation_fee`,
`nonrefundable_credit`, and `within_cooling_off` from the `get_membership` result,
and run:

```bash
python3 blastpass_refund.py refund \
  --annual-price 129.99 --months-remaining 8 --cancellation-fee 10
```

Add `--nonrefundable-credit <n>` for MEGA members, or `--within-cooling-off` when
the membership is still within the cooling-off window. The script prints the
breakdown and a `REFUND DUE: $...` line.

Worked example: BlastPass Plus Extra, `annual_price 129.99`, `months_remaining 8`,
`cancellation_fee 10` -> monthly $10.8325, prorated $86.66, minus $10 = **$76.66**.

### 2. (Trade-up only) Compute the upgrade settlement

When the cancellation happens alongside a console upgrade — the defective unit is
credited under warranty and the customer puts the value toward a pricier model,
applying the membership refund as store credit — run:

```bash
python3 blastpass_refund.py settle \
  --defective-credit 399.99 --upgrade-price 499.99 --membership-credit 76.66
```

Use after (a) the Store Policy Agent confirms the defective unit is credited at its
purchase price with no restocking fee, (b) the Warehouse MCP returns the upgrade
model's price, and (c) the prorated refund above is known. The script prints the
breakdown and a `NET DUE FROM CUSTOMER: $...` (or `REFUND TO CUSTOMER: $...`) line.

Worked example: defective base console credited $399.99, MEGA edition $499.99,
membership refund $76.66 applied as credit -> upgrade difference $100.00, net
**$23.34 due from the customer**.

### 3. Report the result

- State the **refund due** and (if a trade-up) the **net due / refund** clearly.
- Show the breakdown lines so the associate can verify how it was derived.
- Offer to cancel the membership via the Membership MCP (`cancel_membership`) and
  to hand the final numbers to the `slip-pdf-generator` skill to print the slip.
