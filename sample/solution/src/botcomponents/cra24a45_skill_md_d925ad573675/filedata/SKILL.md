---
name: slip-pdf-generator
description: Generate a printable BlastBox return / RMA / exchange slip as a PDF for the associate to hand to the customer. Use at the end of a return, warranty swap, membership cancellation, or upgrade once all amounts are final. The skill runs a bundled Python script that renders the confirmed slip values into a real PDF file.
---

# Return / RMA Slip PDF Generator

This skill produces the **printable slip** that closes out a transaction at the
returns desk. You collect the final, already-confirmed values, then **run the
bundled `slip_pdf.py` script** to render the slip to a **PDF** using `reportlab`.
Do not hand-draw the PDF, write your own rendering code, or invent amounts — every
value comes from earlier steps (Policy Agent ruling, Membership MCP, Warehouse MCP,
and the `prorated-refund-calculator` skill).

## When to use

Use this skill **last**, after every amount is final:

- the eligibility / warranty ruling is decided (Store Policy Agent),
- any membership refund is computed (`prorated-refund-calculator`),
- any upgrade settlement / net-due is computed (`prorated-refund-calculator`),
- the customer has agreed to the outcome.

Do **not** use it to *decide* anything — it only renders the agreed result.

## Slip types

Set `slip_type` to the right label:

- `WARRANTY SWAP` — defective item replaced under warranty.
- `RETURN / REFUND` — item returned for a refund.
- `EXCHANGE / UPGRADE` — item swapped for a different model with a price difference.
- `MEMBERSHIP CANCELLATION` — BlastPass cancelled (often combined with the above).

## Workflow

### 1. Assemble the confirmed values

Build a JSON object with the final values. Every field is optional except those
that apply to this transaction. Keep amounts formatted to 2 decimals with a `$`
prefix.

| Field | Meaning |
| --- | --- |
| `slip_type` | one of the labels above |
| `rma_ref` | RMA / reference number |
| `date` | `YYYY-MM-DD` |
| `store` / `associate` | store name/number and associate/desk |
| `customer_name` | customer name |
| `member_id` / `member_tier` | BlastPass id and tier |
| `items` | list of `[item, sku, disposition, amount]` rows |
| `settlement` | list of `[label, amount]` rows (upgrade difference, refund applied as credit, etc.) |
| `net_label` / `net_amount` | e.g. `NET DUE FROM CUSTOMER` / `$23.34`, or `REFUND TO CUSTOMER` / `$76.66` |
| `notes` | free-text notes |

### 2. Run the bundled script

Pass the JSON to `slip_pdf.py`:

```bash
python3 slip_pdf.py --slip-json '<json>' --output blastbox_slip.pdf
```

For the upgrade-settlement example (defective base console → MEGA edition, with a
BlastPass refund applied as store credit):

```bash
python3 slip_pdf.py --output blastbox_slip.pdf --slip-json '{
  "slip_type": "EXCHANGE / UPGRADE",
  "rma_ref": "RMA-OMEGA-1024",
  "date": "2026-06-07",
  "store": "BlastBox #214 - Seattle",
  "associate": "Associate Desk",
  "customer_name": "Jordan Pixel",
  "member_id": "MEGA-BLAST-1024",
  "member_tier": "BlastPass Plus Extra",
  "items": [
    ["BlastBox Omega (defective)", "SKU-OMEGA", "Warranty credit", "$399.99"],
    ["BlastBox Omega MEGA Edition", "SKU-OMEGA-MEGA", "Upgrade (new)", "$499.99"]
  ],
  "settlement": [
    ["Upgrade difference ($499.99 - $399.99)", "$100.00"],
    ["BlastPass Plus Extra refund applied as store credit", "-$76.66"]
  ],
  "net_label": "NET DUE FROM CUSTOMER",
  "net_amount": "$23.34",
  "notes": "Manufacturing defect confirmed within 30 days - no restocking fee. BlastPass Plus Extra (MEGA-BLAST-1024) cancelled; 8 unused months prorated."
}'
```

The script writes the PDF and prints `WROTE blastbox_slip.pdf`. If the JSON is
long, you may write it to a file and pass `--slip-file slip.json` instead.

### 3. Report

- Confirm the PDF was written and give its filename.
- Restate the **net amount** (due or refunded) and the slip type.
- Offer to email or reprint the slip if needed.

## Notes

- The script uses `reportlab` (the available PDF library). No network calls.
- The layout fits a single page for a normal transaction.
- Pass plain text in the values; the script escapes `&` for you.
