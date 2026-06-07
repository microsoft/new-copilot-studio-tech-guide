---
name: sales-analysis-chart
description: Turn RAW weekly sales rows and catalog facts into merchandising analytics and visual charts. Use when a store manager wants to see top sellers and slow-moving / dead stock over a period. The skill computes velocity, weeks-of-cover, and margin itself (the Sales & Performance MCP returns raw data only), then generates and runs Python (matplotlib) to render PNG bar charts for top sellers and slow movers. It does NOT decide discounts — hand the derived slow-mover list to the markdown-optimizer skill.
---

# Sales Analysis & Charting

This skill converts **raw** sales data into the analytics a markdown review needs, and draws
the charts. The **Sales & Performance MCP** deliberately exposes only raw primitives
(`query_sales`, `get_catalog`) — there is no `get_slow_movers` or `get_velocity`. You build
the query window, then **derive the metrics yourself** in Python.

## Inputs (gather these first)

1. **Raw weekly rows** — call the **Sales & Performance Agent** ·
   `query_sales(start_week, end_week)` for the review window (default: the full 8 weeks
   `2026-04-12` … `2026-05-31`). Each row is `{sku, name, category, week, units, revenue}`.
2. **Catalog facts** — `get_catalog()` → `{sku, name, category, price, unit_cost, stock_on_hand}`.
3. **(Optional) inventory aging** — the **Inventory & Fulfillment Agent** ·
   `get_inventory_aging()` → `weeks_in_stock`, `inbound_po`, `aging_bucket`. Use it to enrich
   the slow-mover read (no inbound + high weeks-of-cover = stronger markdown/clearance signal).

## Metrics you must derive (do NOT expect a tool to return these)

For each SKU over the window of `W` weeks:

- **units_total** = Σ weekly units
- **velocity** = `units_total / W`  (units per week)
- **weeks_of_cover** = `stock_on_hand / velocity`  (how long current stock lasts)
- **margin %** = `(price - unit_cost) / price * 100`
- **revenue_total** = Σ weekly revenue

**Top sellers** = highest `units_total` (or revenue). **Slow movers / dead stock** =
`weeks_of_cover` above a threshold (use **8 weeks**), sorted descending.

## Generate and run this Python

Fill `rows`, `catalog` (and optional `aging`) with the MCP results, then run a snippet like:

```python
import matplotlib
matplotlib.use("Agg")  # headless — required
import matplotlib.pyplot as plt

# --- paste MCP results here ---
rows = [ {"sku": "...", "name": "...", "category": "...", "week": "...", "units": 0, "revenue": 0.0} ]
catalog = [ {"sku": "...", "name": "...", "price": 0.0, "unit_cost": 0.0, "stock_on_hand": 0} ]

WOC_THRESHOLD = 8.0
cat_by = {c["sku"]: c for c in catalog}
weeks = sorted({r["week"] for r in rows})
W = max(1, len(weeks))

metrics = []
for sku, c in cat_by.items():
    srows = [r for r in rows if r["sku"] == sku]
    units_total = sum(r["units"] for r in srows)
    revenue_total = round(sum(r["revenue"] for r in srows), 2)
    velocity = units_total / W
    stock = c.get("stock_on_hand", 0)
    woc = (stock / velocity) if velocity > 0 else float("inf")
    margin = (c["price"] - c["unit_cost"]) / c["price"] * 100 if c["price"] else 0
    metrics.append({"sku": sku, "name": c["name"], "units": units_total,
                    "revenue": revenue_total, "velocity": round(velocity, 2),
                    "weeks_of_cover": round(woc, 1), "stock": stock,
                    "price": c["price"], "cost": c["unit_cost"], "margin": round(margin, 1)})

top = sorted(metrics, key=lambda m: -m["units"])[:3]
slow = sorted([m for m in metrics if m["weeks_of_cover"] > WOC_THRESHOLD],
              key=lambda m: -m["weeks_of_cover"])

PURPLE, ORANGE = "#6b2fb5", "#e8731a"

fig, ax = plt.subplots(figsize=(7, 4))
ax.barh([m["name"] for m in top][::-1], [m["units"] for m in top][::-1], color=PURPLE)
ax.set_xlabel("Units sold"); ax.set_title(f"Top Sellers (last {W} weeks)")
fig.tight_layout(); fig.savefig("top_sellers.png", dpi=120); plt.close(fig)

fig, ax = plt.subplots(figsize=(7, 4))
ax.barh([m["name"] for m in slow][::-1], [m["weeks_of_cover"] for m in slow][::-1], color=ORANGE)
ax.axvline(WOC_THRESHOLD, color="grey", ls="--", lw=1)
ax.set_xlabel("Weeks of cover"); ax.set_title("Slow Movers — weeks of cover")
fig.tight_layout(); fig.savefig("slow_movers.png", dpi=120); plt.close(fig)

import json
print("TOP:", [(m["name"], m["units"]) for m in top])
print("SLOW:", [(m["name"], m["weeks_of_cover"]) for m in slow])
# structured handoff for the markdown-optimizer skill (sku, name, price, cost are what it needs)
print("SLOW_MOVERS_JSON=" + json.dumps(slow))
print("WROTE top_sellers.png, slow_movers.png")
```

## Report

- Present a short table of top sellers (units, velocity) and slow movers
  (weeks-of-cover, margin), and show/attach the two PNG charts.
- Pass the **slow-mover list** (with `price` and `cost`) to the **markdown-optimizer** skill.
- Do **not** recommend discounts here — that needs the Store Policy Agent's guardrails.

## Notes

- Always set `matplotlib.use("Agg")` before `pyplot` (headless sandbox).
- If `matplotlib` is unavailable, fall back to a `reportlab`-drawn bar chart, but prefer matplotlib.
- Never invent numbers — every value derives from the MCP results you fetched.
