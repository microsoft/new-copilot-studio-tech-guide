---
name: merch-report-html
description: Generate the shareable end-of-quarter markdown review as a single self-contained interactive HTML report by running the bundled merch_report.py script. Use as the final step once the analysis and policy-bound markdown recommendations are decided. The script renders top sellers, slow movers, and the markdown recommendations into one BlastBox Omega themed HTML file with inline SVG charts, sortable tables, and a clearance toggle - no internet, no external assets. Hand the file to the manager to download and open in any browser.
---

# Merch Report (interactive HTML)

This skill produces the **shareable report** that closes out the end-of-quarter
markdown review by **running the bundled `merch_report.py` script**. You do **not**
write any Python and you do **not** invent numbers: every value comes from the
earlier steps. The script renders one **self-contained, interactive HTML file**
(BlastBox Omega theme) - all CSS, JavaScript, and SVG charts are inlined, so it
references nothing external and works fully offline once downloaded.

Use this as the **last** step, after `sales-analysis` has produced the top sellers
and slow movers and `markdown-optimizer` has produced the policy-bound
recommendations.

## Inputs (pass the JSON you already have as flags)

| Flag | Source |
| --- | --- |
| `--top-sellers` | top sellers from `sales-analysis` - `[{name, units, velocity, revenue}]` |
| `--slow-movers` | slow movers from `sales-analysis` - `[{name, sku, weeks_of_cover, price, cost, margin}]` |
| `--recommendations` | `RECOMMENDATIONS_JSON` from `markdown-optimizer` - `[{name, sku, price, rec_discount_pct, new_price, new_margin, binding, flag_clearance}]` |
| `--policy` | the guardrails from the Store Policy Agent - `{max_discount_pct, margin_floor_pct, min_useful_promo_discount_pct}` |
| `--window` | the review window label, e.g. `"Apr 12 - May 31, 2026"` |
| `--weeks` | analysis window length (default 8) |
| `--woc-threshold` | slow-mover weeks-of-cover threshold (default 8) |
| `--out` | output path (default `merch_report.html`) |

## Run the bundled script

```bash
python3 merch_report.py \
    --window "Apr 12 - May 31, 2026" --weeks 8 \
    --top-sellers '[{"name":"MEGA Lizards from Outer Space","units":540,"velocity":67.5,"revenue":37794.6}]' \
    --slow-movers '[{"name":"OmegaVision VR Headset","sku":"SKU-VR-GOGGLES","weeks_of_cover":64.0,"price":199.99,"cost":70.0,"margin":65.0}]' \
    --recommendations '[{"name":"OmegaVision VR Headset","sku":"SKU-VR-GOGGLES","price":199.99,"rec_discount_pct":30.0,"new_price":139.99,"new_margin":50.0,"binding":"max_discount","flag_clearance":false}]' \
    --policy '{"max_discount_pct":30,"margin_floor_pct":15,"min_useful_promo_discount_pct":10}'
```

The script writes `merch_report.html` to the working directory and prints a
one-line confirmation (file size + counts). Hand that file back as a **download**.

## What the report contains (so you can describe it)

- A **KPI strip**: slow movers, promo markdowns, items flagged for clearance,
  deepest promo cut, average new margin.
- **Top Sellers** and **Slow Movers** sections, each with an inline SVG bar chart
  (the slow-mover chart marks the weeks-of-cover threshold) and a sortable table.
- A **Markdown Recommendations** section: a discount-depth chart, the policy chips,
  a sortable table showing current price, discount, new price, new margin, and the
  binding constraint, with **clearance-flagged** rows highlighted and a toggle to
  hide them.

## Report back

Tell the manager the interactive report is ready (`merch_report.html`), that it
opens in any browser, and that tables are sortable and clearance items can be
toggled. Summarize the headline: how many promo markdowns, the deepest cut, and
which items were routed to clearance.

## Notes

- The bundled files are `merch_report.py` and `report_template.html.j2`; the script
  uses only the Python standard library plus **Jinja2** (available in the runtime).
  If the template file is missing it falls back to a minimal embedded template.
- The report inlines everything - no CDN, no web fonts, no network. It is safe to
  share and open offline.
- Never invent numbers - every figure comes from the analysis, optimizer, and
  Store Policy Agent results. All data is mock; for demo purposes only.
