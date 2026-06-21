#!/usr/bin/env python3
"""BlastBox Omega - Sales Analysis (bundled skill asset).

Turns the RAW weekly sales rows + catalog facts returned by the Sales & Performance
MCP into the merchandising analytics a markdown review needs. The MCP exposes only
raw primitives (query_sales, get_catalog) - there is no get_slow_movers or
get_velocity - so this script DERIVES the metrics deterministically.

It renders NO charts (the interactive merch-report-html skill owns all visuals),
which keeps the review fast. It only computes numbers and prints the structured
hand-offs that the markdown-optimizer and the report consume.

Inputs (JSON, from the Sales & Performance Agent):
  --rows      query_sales rows: [{sku, name, category, week, units, revenue}]
  --catalog   get_catalog rows: [{sku, name, price, unit_cost, stock_on_hand}]
  --woc-threshold   slow-mover weeks-of-cover threshold (default 8)
  --top-n     how many top sellers to report (default 3)

Outputs:
  * a compact metrics summary (top sellers + slow movers),
  * TOP_SELLERS_JSON=<json>  -> [{name, units, velocity, revenue}]
  * SLOW_MOVERS_JSON=<json>  -> [{sku, name, price, cost, weeks_of_cover, margin, units}]
    (sku/name/price/cost are exactly what markdown-optimizer needs next).
"""
import argparse
import json


def derive(rows, catalog, woc_threshold, top_n):
    cat_by = {c["sku"]: c for c in catalog}
    weeks = sorted({r["week"] for r in rows})
    W = max(1, len(weeks))
    metrics = []
    for sku, c in cat_by.items():
        srows = [r for r in rows if r["sku"] == sku]
        units_total = sum(r.get("units", 0) for r in srows)
        revenue_total = round(sum(r.get("revenue", 0.0) for r in srows), 2)
        velocity = units_total / W
        stock = c.get("stock_on_hand", 0)
        woc = (stock / velocity) if velocity > 0 else float("inf")
        price = float(c["price"])
        cost = float(c.get("unit_cost", c.get("cost", 0.0)))
        margin = (price - cost) / price * 100 if price else 0.0
        metrics.append({
            "sku": sku, "name": c["name"], "units": units_total,
            "revenue": revenue_total, "velocity": round(velocity, 2),
            "weeks_of_cover": round(woc, 1) if woc != float("inf") else None,
            "stock": stock, "price": round(price, 2), "cost": round(cost, 2),
            "margin": round(margin, 1),
        })

    top = sorted(metrics, key=lambda m: -m["units"])[:top_n]
    slow = sorted(
        [m for m in metrics if m["weeks_of_cover"] is not None
         and m["weeks_of_cover"] > woc_threshold],
        key=lambda m: -m["weeks_of_cover"])
    return metrics, top, slow, W


def main():
    ap = argparse.ArgumentParser(description="BlastBox Omega sales analysis (data only)")
    ap.add_argument("--rows", required=True,
                    help='query_sales JSON: [{sku,name,week,units,revenue}]')
    ap.add_argument("--catalog", required=True,
                    help='get_catalog JSON: [{sku,name,price,unit_cost,stock_on_hand}]')
    ap.add_argument("--woc-threshold", type=float, default=8.0)
    ap.add_argument("--top-n", type=int, default=3)
    a = ap.parse_args()

    rows = json.loads(a.rows)
    catalog = json.loads(a.catalog)
    metrics, top, slow, W = derive(rows, catalog, a.woc_threshold, a.top_n)

    print(f"Window: {W} weeks")
    print("TOP SELLERS (units / velocity):")
    for m in top:
        print(f'  {m["name"][:30]:30} {m["units"]:>6,} u   {m["velocity"]:>5.1f} u/wk')
    print(f"SLOW MOVERS (> {a.woc_threshold:g}-wk cover):")
    for m in slow:
        print(f'  {m["name"][:30]:30} {m["weeks_of_cover"]:>5.1f} wks  {m["margin"]:>4.1f}% margin')

    top_out = [{"name": m["name"], "units": m["units"],
                "velocity": m["velocity"], "revenue": m["revenue"]} for m in top]
    slow_out = [{"sku": m["sku"], "name": m["name"], "price": m["price"],
                 "cost": m["cost"], "weeks_of_cover": m["weeks_of_cover"],
                 "margin": m["margin"], "units": m["units"]} for m in slow]
    print("TOP_SELLERS_JSON=" + json.dumps(top_out))
    print("SLOW_MOVERS_JSON=" + json.dumps(slow_out))


if __name__ == "__main__":
    main()
