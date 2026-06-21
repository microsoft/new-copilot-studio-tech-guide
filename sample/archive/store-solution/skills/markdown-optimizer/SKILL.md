---
name: markdown-optimizer
description: Recommend a discount percentage and new price for each slow-moving product by running the bundled markdown_optimizer.py script, respecting pricing guardrails that come from the Store Policy Agent (never hardcode them). Use after sales-analysis-chart has identified the slow movers and after fetching the markdown guardrails via the Store Policy Agent's get_markdown_policy. The script applies the max-discount cap and margin floor, computes each new price and margin, flags items that cannot be meaningfully promoted within the floor for clearance instead, and renders a recommended-discount chart.
---

# Markdown Optimizer (BlastBox Omega)

This skill decides **how much to mark down** each slow-moving product by **running the
bundled `markdown_optimizer.py` script**. You do **not** write any Python and you do
**not** hardcode the pricing rules: the guardrails come from the **Store Policy Agent**
via `get_markdown_policy(markdown_type)` and are passed to the script as CLI flags. This
keeps all policy in one place (the Policy Agent owns it).

## When to use

After `sales-analysis-chart` has produced the structured `slow_movers` list, and after
you have fetched the guardrails from the **Store Policy Agent**. Use it whenever a
manager asks what to mark down and by how much within policy.

## Inputs (gather these first, then pass them as flags)

1. **Slow movers** - the `slow_movers` JSON from `sales-analysis-chart`: a list of
   `{sku, name, price, cost}` (extra keys are ignored).
2. **Guardrails** - call the **Store Policy Agent** - `get_markdown_policy("promo")`. It
   returns `max_discount_pct`, `margin_floor_pct`, and `min_useful_promo_discount_pct`
   (e.g. `30`, `15`, `10`). Pass all three. Do **not** invent or hardcode them. For a
   clearance pass, call `get_markdown_policy("clearance")` and use `--markdown-type clearance`.

## Run the bundled script

For a promo markdown, pass the slow movers and the three guardrails:

```bash
python3 markdown_optimizer.py \
    --markdown-type promo \
    --max-discount 30 --margin-floor 15 --min-useful 10 \
    --slow-movers '[{"sku":"SKU-VR-NEBULA","name":"Nebula VR Headset","price":199.99,"cost":70.00},
                    {"sku":"SKU-GALAXY-PAD","name":"Galaxy Pad Controller","price":49.99,"cost":18.00}]'
```

For a clearance pass (no promo math, every item flagged for a manager decision):

```bash
python3 markdown_optimizer.py --markdown-type clearance \
    --slow-movers '[{"sku":"SKU-OMEGA-CORE","name":"Omega Core Console","price":399.99,"cost":320.00}]'
```

The script prints one line per item, then `RECOMMENDATIONS_JSON=<json>`, and writes
`markdowns.png` (a bar chart of the recommended promo discounts).

## What the script does (for your report, not for you to reimplement)

- **floor price** = `cost / (1 - margin_floor_pct/100)` - the lowest price that still
  keeps the required gross margin.
- **recommended discount %** = `min(max_discount_pct, floor-allowed %)`. When the cap
  binds you get the headline discount; when the **floor** binds the new margin equals the
  floor (`binding` says which).
- Items whose floor-allowed discount is below `min_useful_promo_discount_pct` come back
  with `flag_clearance: true` - they can't be meaningfully promoted, so they should be
  cleared out via a **clearance** pass (a separate decision requiring a manager flag).

## Report

- Present a table: product, current price, recommended discount %, new price, new margin,
  and whether the **margin floor** or **max-discount cap** was binding.
- Call out any item **flagged for clearance**, noting clearance is a separate manager
  decision (`get_markdown_policy("clearance")`).
- Hand the finalized promo set (`RECOMMENDATIONS_JSON`) to **merch-report-pdf** for the
  shareable report.

## Notes

- The bundled script is `markdown_optimizer.py`; it depends only on `matplotlib`
  (available in the runtime) and prints recommendations even if the chart is skipped.
- The guardrail numbers MUST come from the Store Policy Agent. If they are missing, fetch
  them before running - do not guess.
- All data is mock; recommendations are for demo purposes only.
