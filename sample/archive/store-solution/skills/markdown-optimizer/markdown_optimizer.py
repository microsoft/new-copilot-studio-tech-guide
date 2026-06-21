#!/usr/bin/env python3
"""BlastBox Omega - Markdown Optimizer (bundled skill asset).

Decides HOW MUCH to mark down each slow-moving product, respecting pricing
guardrails that are passed in from the Store Policy Agent. The guardrails are
NOT hardcoded here - they arrive as CLI flags so policy stays owned by the
Policy Agent.

Promo rule (per item, markdown_type == "promo"):
  * floor_price          = cost / (1 - margin_floor_pct/100)   lowest price that
                           still keeps the required gross margin.
  * floor_allowed_pct    = (price - floor_price) / price * 100  discount the floor
                           permits.
  * rec_discount_pct     = min(max_discount_pct, floor_allowed_pct).
  * binding              = "margin_floor" when the floor caps the discount,
                           else "max_discount".
  * flag_clearance       = True when floor_allowed_pct < min_useful_promo_discount_pct
                           (can't be meaningfully promoted -> clear it out instead).

Clearance (markdown_type == "clearance"): no margin floor, so no promo discount is
computed; every item is flagged as needing a manager clearance decision.

Outputs:
  * a human-readable table (one line per item),
  * RECOMMENDATIONS_JSON=<json> for downstream handoff (e.g. the PDF report),
  * markdowns.png - a bar chart of recommended promo discounts (unless --no-chart).
"""
import argparse
import json
import sys


def optimize(items, max_discount_pct, margin_floor_pct, min_useful_pct):
    out = []
    for m in items:
        price = float(m["price"])
        cost = float(m["cost"])
        floor_price = cost / (1 - margin_floor_pct / 100.0)
        floor_allowed = (price - floor_price) / price * 100.0
        rec = min(max_discount_pct, max(0.0, floor_allowed))
        binding = "margin_floor" if floor_allowed < max_discount_pct else "max_discount"
        if binding == "margin_floor":
            new_price = round(floor_price + 0.005, 2)  # sit just at/above the floor
        else:
            new_price = round(price * (1 - rec / 100.0), 2)
        new_margin = (new_price - cost) / new_price * 100.0
        out.append({
            "sku": m.get("sku"),
            "name": m.get("name", m.get("sku", "")),
            "price": round(price, 2),
            "cost": round(cost, 2),
            "rec_discount_pct": round(rec, 1),
            "new_price": new_price,
            "new_margin": round(new_margin, 1),
            "binding": binding,
            "floor_allowed_pct": round(floor_allowed, 1),
            "flag_clearance": floor_allowed < min_useful_pct,
        })
    return out


def clearance(items):
    out = []
    for m in items:
        out.append({
            "sku": m.get("sku"),
            "name": m.get("name", m.get("sku", "")),
            "price": round(float(m["price"]), 2),
            "cost": round(float(m["cost"]), 2),
            "rec_discount_pct": None,
            "new_price": None,
            "new_margin": None,
            "binding": "clearance",
            "requires_manager_flag": True,
            "flag_clearance": True,
        })
    return out


def render_table(recs, markdown_type):
    lines = []
    for r in recs:
        if markdown_type == "clearance" or r.get("rec_discount_pct") is None:
            lines.append(f'{r["name"][:26]:26}  CLEARANCE - needs manager flag  '
                         f'(${r["price"]:.2f}, cost ${r["cost"]:.2f})')
        else:
            tag = "  -> FLAG CLEARANCE" if r["flag_clearance"] else ""
            lines.append(f'{r["name"][:26]:26} -{r["rec_discount_pct"]:4.1f}%  '
                         f'${r["price"]:.2f} -> ${r["new_price"]:.2f}  '
                         f'margin {r["new_margin"]:.1f}%  ({r["binding"]}){tag}')
    return "\n".join(lines)


def render_chart(recs, out_path):
    promo = [r for r in recs if not r["flag_clearance"] and r.get("rec_discount_pct")]
    if not promo:
        return None
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    fig, ax = plt.subplots(figsize=(7, 4))
    ax.bar([r["name"][:16] for r in promo],
           [r["rec_discount_pct"] for r in promo], color="#6b2fb5")
    ax.set_ylabel("Recommended discount %")
    ax.set_title("Promo Markdowns")
    fig.tight_layout()
    fig.savefig(out_path, dpi=120)
    plt.close(fig)
    return out_path


def main():
    ap = argparse.ArgumentParser(description="BlastBox Omega markdown optimizer")
    ap.add_argument("--slow-movers", required=True,
                    help='JSON list of {sku,name,price,cost} slow movers')
    ap.add_argument("--markdown-type", choices=["promo", "clearance"], default="promo")
    ap.add_argument("--max-discount", type=float,
                    help="max_discount_pct from get_markdown_policy('promo')")
    ap.add_argument("--margin-floor", type=float,
                    help="margin_floor_pct from get_markdown_policy('promo')")
    ap.add_argument("--min-useful", type=float,
                    help="min_useful_promo_discount_pct from get_markdown_policy('promo')")
    ap.add_argument("--out", default="markdowns.png", help="chart output path")
    ap.add_argument("--no-chart", action="store_true", help="skip chart rendering")
    a = ap.parse_args()

    items = json.loads(a.slow_movers)

    if a.markdown_type == "clearance":
        recs = clearance(items)
    else:
        missing = [f for f, v in (("--max-discount", a.max_discount),
                                  ("--margin-floor", a.margin_floor),
                                  ("--min-useful", a.min_useful)) if v is None]
        if missing:
            ap.error("promo requires policy guardrails from the Store Policy Agent: "
                     + ", ".join(missing))
        recs = optimize(items, a.max_discount, a.margin_floor, a.min_useful)

    print(render_table(recs, a.markdown_type))
    print("RECOMMENDATIONS_JSON=" + json.dumps(recs))

    if not a.no_chart and a.markdown_type == "promo":
        try:
            path = render_chart(recs, a.out)
            if path:
                print(f"WROTE {path}")
        except Exception as e:  # chart is optional; never fail the recommendation
            print(f"(chart skipped: {e})", file=sys.stderr)


if __name__ == "__main__":
    main()
