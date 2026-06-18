---
name: sales-analysis-chart
description: Turn RAW weekly sales rows and catalog facts into merchandising analytics and visual charts by running the bundled sales_chart.py script. Use when a store manager wants to see top sellers and slow-moving / dead stock over a period. The Sales & Performance MCP returns raw data only; you save its query_sales and get_catalog results to JSON files and pass them to the script, which derives velocity, weeks-of-cover, and margin itself and renders PNG bar charts for top sellers and slow movers. It does NOT decide discounts — hand the derived slow-mover list to the markdown-optimizer skill.
---

# Sales Analysis & Charting

This skill converts **raw** sales data into the analytics a markdown review needs, and draws
the charts. You do **not** write any Python — you gather the MCP results, save them to JSON
files, and **run the bundled `sales_chart.py` script**. The **Sales & Performance MCP**
deliberately exposes only raw primitives (`query_sales`, `get_catalog`) — there is no
`get_slow_movers` or `get_velocity`; the script derives those for you.

## 1. Gather the inputs (save each MCP result to a file)

1. **Raw weekly rows** — call the **Sales & Performance Agent** ·
   `query_sales(start_week, end_week)` for the review window (default: the full 8 weeks
   `2026-04-12` … `2026-05-31`). Save the returned array to `rows.json`. Each row is
   `{sku, name, category, week, units, revenue}`.
2. **Catalog facts** — `get_catalog()` → save to `catalog.json`. Each row is
   `{sku, name, category, price, unit_cost, stock_on_hand}`.
3. **(Optional) inventory aging** — the **Inventory & Fulfillment Agent** ·
   `get_inventory_aging()` → save the `items` array to `aging.json`. Enriches the
   slow-mover read (no inbound + high weeks-of-cover = stronger markdown/clearance signal).

Write the JSON exactly as the MCP returned it — the script tolerates either a bare array or
an envelope like `{"rows": [...]}` / `{"items": [...]}`.

## 2. Run the bundled script

```bash
python3 sales_chart.py \
    --rows rows.json \
    --catalog catalog.json \
    [--aging aging.json] \
    [--woc 8] \
    [--outdir .]
```

The script computes, for each SKU over the `W`-week window:

- **units_total** = Σ weekly units
- **velocity** = `units_total / W` (units per week)
- **weeks_of_cover** = `stock_on_hand / velocity`
- **margin %** = `(price - unit_cost) / price * 100`
- **revenue_total** = Σ weekly revenue

**Top sellers** = highest `units_total`. **Slow movers / dead stock** = `weeks_of_cover`
above the threshold (default **8 weeks**), sorted descending.

It writes **`top_sellers.png`** and **`slow_movers.png`** to `--outdir`, and prints:

- `TOP:` and `SLOW:` summaries,
- `SLOW_MOVERS_JSON=[...]` — a structured hand-off (`sku, name, price, cost, weeks_of_cover,
  stock`) for the **markdown-optimizer** skill,
- a `WROTE ...` line listing the PNG paths.

## 3. Report

- Present a short table of top sellers (units, velocity) and slow movers
  (weeks-of-cover, margin), and show/attach the two PNG charts.
- Pass the **`SLOW_MOVERS_JSON`** list to the **markdown-optimizer** skill.
- Do **not** recommend discounts here — that needs the Store Policy Agent's guardrails.

## Notes

- The script sets `matplotlib.use("Agg")` before importing `pyplot` (headless sandbox) and
  caps infinite weeks-of-cover (zero-velocity SKUs) for display.
- Never invent numbers — every value derives from the MCP results you saved to the JSON files.
- The bundled script is `sales_chart.py`, validated against the Sales & Performance MCP's
  raw `query_sales` / `get_catalog` shapes.
