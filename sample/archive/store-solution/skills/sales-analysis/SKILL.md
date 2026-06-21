---
name: sales-analysis
description: Turn the RAW weekly sales rows and catalog facts from the Sales & Performance MCP into merchandising analytics by running the bundled sales_analysis.py script. Use first in an end-of-quarter markdown review, when a manager wants top sellers and slow-moving / dead stock over a period. The MCP returns raw data only, so the script derives velocity, weeks-of-cover, and margin, then prints the top sellers and slow movers as structured JSON. It renders NO charts (the merch-report-html skill owns all visuals) and does NOT decide discounts (hand the slow movers to markdown-optimizer).
---

# Sales Analysis (data, not charts)

This skill converts **raw** sales data into the analytics a markdown review needs by
**running the bundled `sales_analysis.py` script**. The **Sales & Performance MCP**
deliberately exposes only raw primitives (`query_sales`, `get_catalog`) - there is no
`get_slow_movers` or `get_velocity` - so the script derives the metrics itself.

It renders **no charts** on purpose: the interactive **merch-report-html** skill owns
all the visuals at the end, which keeps the review fast. This skill just computes the
numbers and prints the structured hand-offs.

## When to use

**First** in an end-of-quarter markdown review, whenever a manager wants to see top
sellers and slow / dead stock over a window.

## Inputs (fetch from the Sales & Performance Agent, then pass as flags)

1. **Raw weekly rows** - `query_sales(start_week, end_week)` for the window (default the
   full 8 weeks `2026-04-12` ... `2026-05-31`). Each row is
   `{sku, name, category, week, units, revenue}`.
2. **Catalog facts** - `get_catalog()` -> `{sku, name, price, unit_cost, stock_on_hand}`.

## Run the bundled script

```bash
python3 sales_analysis.py \
    --woc-threshold 8 \
    --rows '[{"sku":"SKU-VR-GOGGLES","name":"OmegaVision VR Headset","category":"accessory","week":"2026-04-12","units":1,"revenue":199.99}]' \
    --catalog '[{"sku":"SKU-VR-GOGGLES","name":"OmegaVision VR Headset","price":199.99,"unit_cost":70.00,"stock_on_hand":48}]'
```

It prints a compact summary, then two structured lines:

- `TOP_SELLERS_JSON=[{name, units, velocity, revenue}]`
- `SLOW_MOVERS_JSON=[{sku, name, price, cost, weeks_of_cover, margin, units}]`

## Metrics it derives (for your report, not for you to recompute)

Over a window of `W` weeks, per SKU: `velocity = units_total / W`,
`weeks_of_cover = stock_on_hand / velocity`, `margin% = (price - cost) / price * 100`.
**Slow movers** = weeks-of-cover above the threshold (default 8), sorted descending.
**Top sellers** = highest units.

## Next steps

- Pass `SLOW_MOVERS_JSON` (it already carries `sku, name, price, cost`) to the
  **markdown-optimizer** skill, together with the guardrails from the Store Policy Agent.
- Keep `TOP_SELLERS_JSON` and `SLOW_MOVERS_JSON` so the **merch-report-html** skill can
  render them into the final interactive report.
- Do **not** recommend discounts here - that needs the Store Policy Agent's guardrails.

## Notes

- The bundled script is `sales_analysis.py`; it uses only the Python standard library
  (no matplotlib, no network).
- Never invent numbers - every value derives from the MCP results you fetched. All data
  is mock; for demo purposes only.
