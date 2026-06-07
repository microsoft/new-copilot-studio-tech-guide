---
name: bundle-settlement-calculator
description: Calculate the exact settlement when a customer returns a multi-item BlastBox Omega bundle (console, accessories, games) and optionally cancels a BlastPass membership in the same visit. Use after the Store Policy Agent (Policy RAG MCP) has confirmed which lines are returnable and the restocking/store-credit rules, and after any membership proration refund has been computed. The skill writes and runs a short Python snippet that applies restocking fees, the store-credit goodwill bonus, tax, and the membership refund, then prints an itemized settlement for the slip.
---

# MEGA Bundle Return & Settlement Calculator (BlastBox Omega)

This skill computes the **exact** payout when a customer returns several items at
once — typically a "MEGA Bundle" (console + controllers + games) — and may also
be cancelling their BlastPass membership in the same transaction. The arithmetic
must be exact and consistent, so you **generate and run a short Python snippet**
to do it — never add the lines up in your head.

## When to use

Use this skill when an associate is returning **more than one item together**, or
combining a merchandise return with a membership cancellation, e.g.:
- "They want to return the whole MEGA Bundle and cancel BlastPass too."
- "Refund these four items to store credit and tell me the total."

Run the **Return Eligibility Evaluator** first for any line you are unsure about,
and confirm the rules below with the **Store Policy Agent** (Policy RAG MCP).

## The settlement rules (single source of truth)

These mirror the Store Returns & Exchanges Policy and BlastPass terms held by the
Store Policy Agent:

1. **Restocking fee.** 15% on **opened** consoles/hardware (categories `console`
   and `accessory`) returned like-new. The fee is **waived** when the item is
   defective. Sealed/new items have no fee.
2. **Non-returnable lines.** Day-one MEGA exclusives, revealed digital codes, and
   opened physical games refund **$0** and must be flagged on the slip.
3. **Store-credit goodwill bonus.** If the payout method is **store credit**, add
   a **+10%** bonus on the **eligible merchandise total only**. The bonus does
   **NOT** apply to fees, to tax, or to the membership proration refund.
4. **Sales tax** is refunded on the net merchandise refunded (membership has no
   tax).
5. **Membership proration refund** (from the Prorated Refund Calculator / the
   Membership MCP) is added at the end with **no bonus and no tax**.

## Workflow

### 1. Gather the inputs

For each line, get: name, unit price, qty, category
(`console` / `accessory` / `game` / `digital` / `exclusive`), condition
(`new` / `opened` / `defective`), and whether it is returnable. Then get the
**payout method** (`tender` or `store_credit`), the **tax rate**, and the
**membership proration refund** (or `0` if none).

### 2. Generate and run this Python

Write and execute a snippet like the following, substituting the real values:

```python
import json

def money(v):
    cents = int(v * 100 + (0.5 if v >= 0 else -0.5))
    return cents / 100.0

HARDWARE = {"console", "accessory"}

# --- inputs (from the associate, Policy RAG MCP, and Membership MCP) ---
items = [
    {"name": "BlastBox Omega Console (MEGA Bundle)", "price": 499.99, "qty": 1,
     "category": "console", "condition": "opened"},
    {"name": "OmegaGrip Pro Controller", "price": 69.99, "qty": 2,
     "category": "accessory", "condition": "opened"},
    {"name": "Day-One MEGA Exclusive: Nebula Crash", "price": 79.99, "qty": 1,
     "category": "exclusive", "condition": "opened", "returnable": False},
]
payout_method = "store_credit"      # "tender" or "store_credit"
tax_rate = 0.0875
membership_refund = 76.66           # 0 if not cancelling a membership

lines, merch_net = [], 0.0
for it in items:
    base = money(it["price"] * it.get("qty", 1))
    if not it.get("returnable", True):
        lines.append(f"{it['name']}: NON-RETURNABLE -> $0.00")
        continue
    fee = 0.0
    if it.get("category") in HARDWARE and it.get("condition") == "opened" and not it.get("defective"):
        fee = money(base * 0.15)
    net = money(base - fee)
    merch_net += net
    tag = "defective, fee waived" if it.get("defective") else (f"-15% restock ${fee:.2f}" if fee else "no fee")
    lines.append(f"{it['name']}: ${base:.2f} ({tag}) -> ${net:.2f}")

merch_net = money(merch_net)
tax_refund = money(merch_net * tax_rate)
bonus = money(merch_net * 0.10) if payout_method == "store_credit" else 0.0
total = money(merch_net + tax_refund + bonus + membership_refund)

print("\n".join(lines))
print(f"Merchandise net: ${merch_net:.2f}")
print(f"Tax refund:      ${tax_refund:.2f}")
if bonus:
    print(f"Store-credit +10% bonus: ${bonus:.2f}")
if membership_refund:
    print(f"Membership proration:    ${membership_refund:.2f} (no bonus/tax)")
print(f"SETTLEMENT TOTAL ({payout_method}): ${total:.2f}")
```

### 3. Report the result

- State the **settlement total** and the payout method clearly.
- Show the per-line breakdown and the bonus/tax/membership lines so the associate
  can verify it.
- Offer to generate the printable Return/RMA slip with this itemization, and — if
  a membership was cancelled — to call `cancel_membership` on the Membership MCP.

## Worked example

MEGA Bundle (opened console $499.99, two opened controllers $69.99, one
non-returnable day-one exclusive, one defective headset $129.99), **store credit**,
8.75% tax, plus a $76.66 membership proration refund:

- Merchandise net = **$733.95** (console $424.99 + controllers $118.98 + headset
  $129.99; exclusive $0)
- Tax refund = **$64.22**
- Store-credit +10% bonus = **$73.40**
- Membership proration = **$76.66**
- **Settlement total = $948.23** (to store credit). To original tender it would be
  **$874.83** (no bonus).
