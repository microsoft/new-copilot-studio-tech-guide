#!/usr/bin/env python3
"""Derive merchandising metrics from RAW sales data and render charts to PNG.

Bundled skill script: the agent runs this with the Sales & Performance MCP
results saved to JSON files, as CLI flags. The script computes velocity,
weeks-of-cover, margin, and revenue itself (the MCP returns raw data only),
renders the top-sellers and slow-mover bar charts, and prints a structured
hand-off (SLOW_MOVERS_JSON) for the markdown-optimizer skill.

It only *derives and draws* from the values you pass in — it never looks
anything up or invents data.

Usage:
    python3 sales_chart.py \
        --rows rows.json \
        --catalog catalog.json \
        [--aging aging.json] \
        [--woc 8] \
        [--outdir .]

Inputs (JSON files, exactly as the MCP returned them):
    --rows     query_sales(...) rows: [{sku,name,category,week,units,revenue}, ...]
    --catalog  get_catalog():        [{sku,name,category,price,unit_cost,stock_on_hand}, ...]
    --aging    (optional) get_inventory_aging().items:
                                      [{sku,weeks_in_stock,inbound_po,aging_bucket,...}, ...]

Outputs (written to --outdir):
    top_sellers.png   — horizontal bar chart of the top-3 SKUs by units sold
    slow_movers.png   — horizontal bar chart of SKUs above the weeks-of-cover threshold
"""

import argparse
import json
import os
import sys

import matplotlib
matplotlib.use("Agg")  # headless: render straight to a file, no display needed
import matplotlib.pyplot as plt

PURPLE, ORANGE, GREY = "#6b2fb5", "#e8731a", "#888888"


def load_json(path):
    with open(path, "r", encoding="utf-8") as fh:
        data = json.load(fh)
    # Tolerate either a bare array or an envelope like {"rows": [...]} / {"items": [...]}.
    if isinstance(data, dict):
        for key in ("rows", "items", "results", "data", "catalog"):
            if key in data and isinstance(data[key], list):
                return data[key]
        # single object -> wrap
        return [data]
    return data


def derive_metrics(rows, catalog, aging_by):
    cat_by = {c["sku"]: c for c in catalog}
    weeks = sorted({r["week"] for r in rows})
    W = max(1, len(weeks))

    metrics = []
    for sku, c in cat_by.items():
        srows = [r for r in rows if r.get("sku") == sku]
        units_total = sum(r.get("units", 0) for r in srows)
        revenue_total = round(sum(r.get("revenue", 0.0) for r in srows), 2)
        velocity = units_total / W
        stock = c.get("stock_on_hand", 0)
        woc = (stock / velocity) if velocity > 0 else float("inf")
        price = c.get("price", 0) or 0
        cost = c.get("unit_cost", 0) or 0
        margin = ((price - cost) / price * 100) if price else 0
        m = {
            "sku": sku,
            "name": c.get("name", sku),
            "units": units_total,
            "revenue": revenue_total,
            "velocity": round(velocity, 2),
            "weeks_of_cover": (round(woc, 1) if woc != float("inf") else None),
            "stock": stock,
            "price": price,
            "cost": cost,
            "margin": round(margin, 1),
        }
        a = aging_by.get(sku)
        if a:
            m["inbound_po"] = a.get("inbound_po")
            m["aging_bucket"] = a.get("aging_bucket")
        metrics.append(m)
    return metrics, W


def render(metrics, W, woc_threshold, outdir):
    top = sorted(metrics, key=lambda m: -m["units"])[:3]

    def woc_val(m):
        v = m["weeks_of_cover"]
        return float("inf") if v is None else v

    slow = sorted(
        [m for m in metrics if woc_val(m) > woc_threshold],
        key=lambda m: -woc_val(m),
    )

    os.makedirs(outdir, exist_ok=True)
    top_path = os.path.join(outdir, "top_sellers.png")
    slow_path = os.path.join(outdir, "slow_movers.png")

    fig, ax = plt.subplots(figsize=(7, 4))
    ax.barh([m["name"] for m in top][::-1], [m["units"] for m in top][::-1], color=PURPLE)
    ax.set_xlabel("Units sold")
    ax.set_title(f"Top Sellers (last {W} weeks)")
    fig.tight_layout()
    fig.savefig(top_path, dpi=120)
    plt.close(fig)

    if slow:
        # inf weeks-of-cover (zero velocity) can't be plotted; cap at threshold*3 for display
        cap = woc_threshold * 3
        vals = [min(woc_val(m), cap) for m in slow][::-1]
        names = [m["name"] for m in slow][::-1]
        fig, ax = plt.subplots(figsize=(7, 4))
        ax.barh(names, vals, color=ORANGE)
        ax.axvline(woc_threshold, color=GREY, ls="--", lw=1)
        ax.set_xlabel("Weeks of cover")
        ax.set_title("Slow Movers — weeks of cover")
        fig.tight_layout()
        fig.savefig(slow_path, dpi=120)
        plt.close(fig)
    else:
        slow_path = None

    return top, slow, top_path, slow_path


def main():
    p = argparse.ArgumentParser(description="Derive sales metrics and render charts to PNG.")
    p.add_argument("--rows", required=True, help="JSON file of query_sales rows")
    p.add_argument("--catalog", required=True, help="JSON file of get_catalog rows")
    p.add_argument("--aging", help="(optional) JSON file of get_inventory_aging items")
    p.add_argument("--woc", type=float, default=8.0, help="weeks-of-cover threshold (default 8)")
    p.add_argument("--outdir", default=".", help="output directory for the PNGs")
    args = p.parse_args()

    rows = load_json(args.rows)
    catalog = load_json(args.catalog)
    aging = load_json(args.aging) if args.aging else []
    aging_by = {a["sku"]: a for a in aging if isinstance(a, dict) and "sku" in a}

    if not catalog:
        sys.exit("No catalog rows provided — cannot derive metrics.")

    metrics, W = derive_metrics(rows, catalog, aging_by)
    top, slow, top_path, slow_path = render(metrics, W, args.woc, args.outdir)

    print("TOP:", [(m["name"], m["units"]) for m in top])
    print("SLOW:", [(m["name"], m["weeks_of_cover"]) for m in slow])
    # Structured hand-off for the markdown-optimizer skill (needs sku, name, price, cost).
    print("SLOW_MOVERS_JSON=" + json.dumps(
        [{"sku": m["sku"], "name": m["name"], "price": m["price"], "cost": m["cost"],
          "weeks_of_cover": m["weeks_of_cover"], "stock": m["stock"]} for m in slow]))
    wrote = ", ".join(pth for pth in (top_path, slow_path) if pth)
    print("WROTE " + wrote)


if __name__ == "__main__":
    main()
