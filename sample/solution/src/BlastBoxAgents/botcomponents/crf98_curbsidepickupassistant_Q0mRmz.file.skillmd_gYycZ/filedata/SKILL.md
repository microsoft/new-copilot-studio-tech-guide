---
name: curbside-slip-pdf
description: Render the completed agentic curbside bay recommendation as a customer-facing one-page PDF.
---

# Curbside Recommendation PDF

Use this skill only after the agent has presented the completed recommendation
and the customer has explicitly asked for a PDF copy. The skill renders that
completed recommendation. It does not inspect other bays, classify weather,
calculate queues, apply membership, or select, assign, reserve, confirm, or
guarantee a bay.

Always run the bundled `curbside_slip.py` script. Never hand-draw the PDF or
replace the script with generated rendering code.

## Inputs

Pass only these final reader-facing values:

- `--order`, `--customer`, and canonical `--store`
- selected `--bay`
- current `--orders-ahead` and `--expected-wait-seconds` from queue status
- optional `--eta-minutes`, using the selector's `remaining_eta_minutes`
- selector `weather_summary` as concise `--conditions`
- selector `recommendation_reason` as concise `--reason`
- `--out blastbox_curbside_slip.pdf`

The PDF contains the order number as a scannable Code 128 barcode. It presents
the queue, wait, ETA, and ETA margin once. Do not recreate or restate timing in
the conditions or reason text.

Never pass a BlastPass ID, `tier_code`, membership display name, payment,
email, `occupied_by`, other bays, raw tool output, assignment confirmation,
staging time, order items, or invented weather.

When no weather fact is confirmed, use concise neutral conditions such as
`Current pickup conditions did not change the bay comparison.` Never mention
missing fields, unavailable weather, APIs, or limitations.

## Example

```bash
python3 curbside_slip.py \
  --order ORD-10502 \
  --customer "Jordan Pixel" \
  --store "BlastBox Pixel Heights" \
  --bay 2 \
  --orders-ahead 2 \
  --expected-wait-seconds 60 \
  --eta-minutes 20 \
  --conditions "Rain and strong winds are expected around arrival." \
  --reason "Bay 2 provides the best available weather protection and queue." \
  --out blastbox_curbside_slip.pdf
```

The script prints:

```text
WROTE blastbox_curbside_slip.pdf
```

Return only the generated PDF file to the customer. Do not add a separate
recommendation paragraph on a successful flow.
