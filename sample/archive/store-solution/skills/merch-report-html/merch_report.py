#!/usr/bin/env python3
"""BlastBox Omega - Merch Report (HTML) bundled skill asset.

Renders a single self-contained, interactive HTML report for the end-of-quarter
markdown review. Pure standard library + Jinja2 (3.1.x, available in the runtime).
The Copilot Studio code-interpreter sandbox has NO internet access, so the report
inlines all CSS, JS and SVG charts - it references nothing external. The user
downloads the file and opens it in any browser; the interactivity (sortable
tables, clearance toggle, animated bars) runs client-side with vanilla JS.

Inputs (JSON, from the earlier steps of the review):
  --top-sellers      [{name, units, velocity, revenue}]            (sales-analysis)
  --slow-movers      [{name, sku, weeks_of_cover, price, cost, margin}]  (sales-analysis)
  --recommendations  [{name, sku, price, rec_discount_pct, new_price,
                       new_margin, binding, flag_clearance}]       (markdown-optimizer)
  --policy           {max_discount_pct, margin_floor_pct, min_useful_promo_discount_pct}
  --window           human label, e.g. "Apr 12 - May 31, 2026"
  --weeks            int, analysis window length
  --woc-threshold    float, slow-mover weeks-of-cover threshold (default 8)
  --out              output path (default merch_report.html)

Writes the HTML file and prints a one-line confirmation.
"""
import argparse
import datetime
import html
import json
import os
import sys

CYAN, PURPLE, PINK, ORANGE, GREEN, RED = (
    "#22e0ff", "#b06bff", "#ff2e88", "#ff7a18", "#41f5a8", "#ff5470")
TEMPLATE_NAME = "report_template.html.j2"


# ---- inline SVG horizontal bar chart (server-rendered, no JS charting lib) ----
def svg_bars(items, value_key, label_key, color, unit="", threshold=None,
             threshold_label="", value_fmt="{:.0f}"):
    """Build a themed horizontal-bar SVG. `items` is a list of dicts."""
    if not items:
        return "<p style='color:#837bb0;font-size:13px'>No items.</p>"
    rows = len(items)
    row_h, gap, top_pad, left, right, bottom = 30, 12, 14, 220, 70, 24
    w = 760
    h = top_pad + rows * (row_h + gap) + bottom
    plot_w = w - left - right
    vmax = max((float(it[value_key]) for it in items), default=1.0) or 1.0
    if threshold is not None:
        vmax = max(vmax, float(threshold))
    vmax *= 1.08

    def esc(s):
        return html.escape(str(s))

    parts = [f'<svg class="chart" viewBox="0 0 {w} {h}" role="img" '
             f'preserveAspectRatio="xMinYMin meet" xmlns="http://www.w3.org/2000/svg">']
    parts.append(f'<defs><linearGradient id="g_{color[1:]}" x1="0" y1="0" x2="1" y2="0">'
                 f'<stop offset="0" stop-color="{color}" stop-opacity="0.55"/>'
                 f'<stop offset="1" stop-color="{color}" stop-opacity="1"/></linearGradient></defs>')

    if threshold is not None:
        tx = left + plot_w * (float(threshold) / vmax)
        parts.append(f'<line x1="{tx:.1f}" y1="{top_pad-4}" x2="{tx:.1f}" y2="{h-bottom}" '
                     f'stroke="#837bb0" stroke-dasharray="4 4" stroke-width="1.2"/>')
        parts.append(f'<text x="{tx+4:.1f}" y="{top_pad+6}" fill="#837bb0" '
                     f'font-size="11" font-family="sans-serif">{esc(threshold_label)}</text>')

    for i, it in enumerate(items):
        y = top_pad + i * (row_h + gap)
        val = float(it[value_key])
        bw = max(2.0, plot_w * (val / vmax))
        label = esc(it[label_key])[:34]
        vtxt = value_fmt.format(val) + unit
        parts.append(f'<text x="{left-12}" y="{y+row_h/2+4:.1f}" text-anchor="end" '
                     f'fill="#b9b2e6" font-size="13" font-family="sans-serif">{label}</text>')
        parts.append(f'<rect x="{left}" y="{y}" width="{plot_w}" height="{row_h}" rx="6" '
                     f'fill="#1d1740"/>')
        parts.append(f'<rect class="bar" x="{left}" y="{y}" data-w="{bw:.1f}" width="0" '
                     f'height="{row_h}" rx="6" fill="url(#g_{color[1:]})"/>')
        parts.append(f'<text x="{left+bw+8:.1f}" y="{y+row_h/2+4:.1f}" fill="#f3f0ff" '
                     f'font-size="12.5" font-weight="700" font-family="ui-monospace,monospace">{esc(vtxt)}</text>')
    parts.append('</svg>')
    return "".join(parts)


def load_template_source(script_dir):
    """Prefer the bundled template file; fall back to the embedded copy."""
    path = os.path.join(script_dir, TEMPLATE_NAME)
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            return f.read()
    return EMBEDDED_TEMPLATE


def main():
    ap = argparse.ArgumentParser(description="BlastBox Omega markdown-review HTML report")
    ap.add_argument("--top-sellers", required=True)
    ap.add_argument("--slow-movers", required=True)
    ap.add_argument("--recommendations", required=True)
    ap.add_argument("--policy", required=True)
    ap.add_argument("--window", default="last 8 weeks")
    ap.add_argument("--weeks", type=int, default=8)
    ap.add_argument("--woc-threshold", type=float, default=8.0)
    ap.add_argument("--title", default="End-of-Quarter Markdown Review")
    ap.add_argument("--out", default="merch_report.html")
    a = ap.parse_args()

    top = json.loads(a.top_sellers)
    slow = json.loads(a.slow_movers)
    recs = json.loads(a.recommendations)
    policy = json.loads(a.policy)

    # normalise numeric fields
    for t in top:
        t["units"] = float(t.get("units", 0))
        t["velocity"] = float(t.get("velocity", 0))
        t["revenue"] = float(t.get("revenue", 0))
    for s in slow:
        s["weeks_of_cover"] = float(s.get("weeks_of_cover", 0) or 0)
        s["price"] = float(s.get("price", 0))
        s["margin"] = float(s.get("margin", 0))

    promos = [r for r in recs if not r.get("flag_clearance")]
    clears = [r for r in recs if r.get("flag_clearance")]
    new_margins = [float(r["new_margin"]) for r in promos if r.get("new_margin")]
    deepest = max((float(r.get("rec_discount_pct") or 0) for r in promos), default=0.0)
    kpi = {
        "slow_count": len(slow),
        "promo_count": len(promos),
        "clearance_count": len(clears),
        "deepest_pct": f"{deepest:.0f}",
        "avg_margin": f"{(sum(new_margins)/len(new_margins)):.0f}" if new_margins else "0",
    }

    top_chart = svg_bars(top, "units", "name", CYAN, value_fmt="{:,.0f}")
    slow_chart = svg_bars(slow, "weeks_of_cover", "name", ORANGE, unit="w",
                          threshold=a.woc_threshold,
                          threshold_label=f"{a.woc_threshold:g}-wk threshold",
                          value_fmt="{:.0f}")
    rec_items = [{"name": r["name"], "pct": float(r.get("rec_discount_pct") or 0)}
                 for r in recs]
    rec_chart = svg_bars(rec_items, "pct", "name", PURPLE, unit="%", value_fmt="{:.1f}")

    from jinja2 import Environment, BaseLoader, select_autoescape
    env = Environment(loader=BaseLoader(),
                      autoescape=select_autoescape(["html", "xml"]))
    src = load_template_source(os.path.dirname(os.path.abspath(__file__)))
    tmpl = env.from_string(src)
    out_html = tmpl.render(
        title=a.title, window=a.window, weeks=a.weeks,
        woc_threshold=f"{a.woc_threshold:g}",
        generated_at=datetime.date.today().isoformat(),
        kpi=kpi, policy=policy,
        top_sellers=top, slow_movers=slow, recommendations=recs,
        clearance_names=[c["name"] for c in clears],
        # charts are pre-rendered, trusted SVG -> mark safe via |safe in template
        top_chart_svg=Safe(top_chart), slow_chart_svg=Safe(slow_chart),
        rec_chart_svg=Safe(rec_chart),
    )

    with open(a.out, "w", encoding="utf-8") as f:
        f.write(out_html)
    print(f"WROTE {a.out} ({len(out_html):,} bytes) - "
          f"{kpi['promo_count']} promo markdowns, {kpi['clearance_count']} flagged for clearance")


class Safe(str):
    """Marks pre-rendered SVG as autoescape-safe for Jinja2."""
    def __html__(self):
        return str(self)


# Full report template, embedded so the skill is self-contained even if the
# bundled .j2 asset is not readable from the sandbox working directory.
# (report_template.html.j2 stays the source of truth; keep the two in sync.)
EMBEDDED_TEMPLATE = r'''{# BlastBox Omega - End-of-Quarter Markdown Review report template (Jinja2).
   Self-contained: all CSS/JS/SVG inlined, zero network dependencies (the code
   interpreter sandbox has no internet). Rendered by merch_report.py. #}
<!doctype html>
<html lang="en" data-theme="blastbox">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{ title }} - BlastBox Omega</title>
<style>
  :root{
    --bg:#0b0918; --bg-deep:#070512; --surface:#15112b; --surface-raised:#1d1740;
    --border:#2c2456; --border-subtle:#221c45;
    --text:#f3f0ff; --text-2:#b9b2e6; --text-muted:#837bb0;
    --cyan:#22e0ff; --purple:#b06bff; --pink:#ff2e88; --orange:#ff7a18;
    --green:#41f5a8; --red:#ff5470;
    --brand:linear-gradient(90deg,#22e0ff 0%,#6a8bff 24%,#9b51e0 48%,#ff2e88 74%,#ff7a18 100%);
    --radius:14px;
  }
  *{box-sizing:border-box}
  html,body{margin:0;padding:0}
  body{
    font-family:'DM Sans',system-ui,-apple-system,Segoe UI,Roboto,sans-serif;
    color:var(--text); background:
      radial-gradient(ellipse 60% 50% at 12% 0%, rgba(34,224,255,.07), transparent 60%),
      radial-gradient(ellipse 55% 45% at 88% 8%, rgba(255,46,136,.07), transparent 60%),
      radial-gradient(ellipse 50% 40% at 50% 100%, rgba(155,81,224,.06), transparent 60%),
      var(--bg);
    -webkit-font-smoothing:antialiased; line-height:1.5; min-height:100vh;
  }
  .wrap{max-width:1100px;margin:0 auto;padding:40px 24px 80px}
  .display{font-family:'Orbitron','DM Sans',system-ui,sans-serif;font-weight:800;letter-spacing:.04em}
  /* Header */
  header.hero{
    border:1px solid var(--border); border-radius:var(--radius);
    background:linear-gradient(180deg,var(--surface),var(--bg-deep));
    padding:30px 32px; position:relative; overflow:hidden;
    box-shadow:0 24px 60px rgba(0,0,0,.5), 0 0 50px rgba(155,81,224,.12);
  }
  header.hero::before{content:"";position:absolute;inset:0 0 auto 0;height:4px;background:var(--brand)}
  .eyebrow{font-size:12px;letter-spacing:.22em;text-transform:uppercase;color:var(--cyan);font-weight:700}
  h1{margin:.35em 0 .15em;font-size:30px;line-height:1.1}
  h1 .grad{background:var(--brand);-webkit-background-clip:text;background-clip:text;-webkit-text-fill-color:transparent}
  .sub{color:var(--text-2);font-size:15px}
  .badge{display:inline-block;margin-top:14px;padding:5px 12px;border-radius:999px;font-size:12px;font-weight:600;
    border:1px solid var(--border);background:var(--surface-raised);color:var(--text-2)}
  /* KPI grid */
  .kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(165px,1fr));gap:14px;margin:22px 0 30px}
  .kpi{border:1px solid var(--border);border-radius:var(--radius);background:var(--surface);padding:16px 18px;position:relative;overflow:hidden}
  .kpi::after{content:"";position:absolute;left:0;top:0;bottom:0;width:3px;background:var(--accent,var(--purple))}
  .kpi .v{font-size:26px;font-weight:800;line-height:1.05}
  .kpi .l{font-size:12px;color:var(--text-muted);text-transform:uppercase;letter-spacing:.08em;margin-top:4px}
  /* Sections */
  section{margin-top:34px}
  h2{font-size:19px;margin:0 0 4px;display:flex;align-items:center;gap:10px}
  h2 .dot{width:10px;height:10px;border-radius:3px;background:var(--accent,var(--cyan));box-shadow:0 0 10px var(--accent,var(--cyan))}
  .lead{color:var(--text-2);font-size:14px;margin:0 0 16px}
  .card{border:1px solid var(--border);border-radius:var(--radius);background:var(--surface);padding:20px 22px}
  /* Charts */
  .chart{width:100%;height:auto;display:block}
  .bar{transition:width .9s cubic-bezier(.2,.8,.2,1)}
  /* Tables */
  table{width:100%;border-collapse:collapse;font-size:14px;margin-top:6px}
  th,td{padding:10px 12px;text-align:left;border-bottom:1px solid var(--border-subtle)}
  th{font-size:12px;text-transform:uppercase;letter-spacing:.06em;color:var(--text-muted);cursor:pointer;user-select:none;white-space:nowrap}
  th[data-sort]:hover{color:var(--cyan)}
  th .arrow{opacity:.4;font-size:10px}
  td.num,th.num{text-align:right;font-variant-numeric:tabular-nums;font-family:'JetBrains Mono',ui-monospace,monospace}
  tbody tr:hover{background:rgba(176,107,255,.06)}
  .pill{display:inline-block;padding:2px 9px;border-radius:999px;font-size:11px;font-weight:700;letter-spacing:.02em}
  .pill.cap{background:rgba(34,224,255,.14);color:var(--cyan);border:1px solid rgba(34,224,255,.3)}
  .pill.floor{background:rgba(255,122,24,.14);color:var(--orange);border:1px solid rgba(255,122,24,.3)}
  .pill.clear{background:rgba(255,84,112,.14);color:var(--red);border:1px solid rgba(255,84,112,.35)}
  tr.is-clearance{background:rgba(255,84,112,.05)}
  .down{color:var(--green);font-weight:700}
  .controls{display:flex;gap:14px;align-items:center;margin:2px 0 12px;font-size:13px;color:var(--text-2)}
  .toggle{display:inline-flex;align-items:center;gap:7px;cursor:pointer;user-select:none}
  .toggle input{accent-color:var(--pink);width:15px;height:15px}
  .policy{display:flex;flex-wrap:wrap;gap:10px;margin-top:4px}
  .policy .chip{font-size:12px;border:1px dashed var(--border);border-radius:8px;padding:6px 11px;color:var(--text-2)}
  .policy .chip b{color:var(--text)}
  footer{margin-top:40px;text-align:center;color:var(--text-muted);font-size:12px}
  footer .brand{background:var(--brand);-webkit-background-clip:text;background-clip:text;-webkit-text-fill-color:transparent;font-weight:800}
  .note{font-size:12px;color:var(--text-muted);margin-top:10px}
</style>
</head>
<body>
<div class="wrap">

  <header class="hero">
    <div class="eyebrow">BlastBox Omega &middot; Merch Insights</div>
    <h1 class="display">End-of-Quarter <span class="grad">Markdown Review</span></h1>
    <div class="sub">{{ window }} &middot; {{ weeks }}-week analysis window</div>
    <span class="badge">Generated {{ generated_at }} &middot; mock data, demo only</span>
  </header>

  <div class="kpis">
    <div class="kpi" style="--accent:var(--cyan)"><div class="v countup" data-to="{{ kpi.slow_count }}">0</div><div class="l">Slow movers</div></div>
    <div class="kpi" style="--accent:var(--purple)"><div class="v countup" data-to="{{ kpi.promo_count }}">0</div><div class="l">Promo markdowns</div></div>
    <div class="kpi" style="--accent:var(--red)"><div class="v countup" data-to="{{ kpi.clearance_count }}">0</div><div class="l">Flagged for clearance</div></div>
    <div class="kpi" style="--accent:var(--orange)"><div class="v">{{ kpi.deepest_pct }}%</div><div class="l">Deepest promo cut</div></div>
    <div class="kpi" style="--accent:var(--green)"><div class="v">{{ kpi.avg_margin }}%</div><div class="l">Avg new margin</div></div>
  </div>

  {# ---- Top sellers ---- #}
  <section>
    <h2><span class="dot" style="--accent:var(--cyan)"></span>Top Sellers</h2>
    <p class="lead">Highest unit movers over the window &mdash; protect stock and pricing here.</p>
    <div class="card">
      {{ top_chart_svg }}
      <table id="topTable">
        <thead><tr>
          <th data-sort="str" data-col="0">Product <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="1">Units <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="2">Velocity (u/wk) <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="3">Revenue <span class="arrow">&#9650;&#9660;</span></th>
        </tr></thead>
        <tbody>
        {% for t in top_sellers %}
          <tr>
            <td>{{ t.name }}</td>
            <td class="num" data-v="{{ t.units }}">{{ "{:,}".format(t.units) }}</td>
            <td class="num" data-v="{{ t.velocity }}">{{ "%.1f"|format(t.velocity) }}</td>
            <td class="num" data-v="{{ t.revenue }}">${{ "{:,.0f}".format(t.revenue) }}</td>
          </tr>
        {% endfor %}
        </tbody>
      </table>
    </div>
  </section>

  {# ---- Slow movers ---- #}
  <section>
    <h2><span class="dot" style="--accent:var(--orange)"></span>Slow Movers</h2>
    <p class="lead">Stock above the {{ woc_threshold }}-week weeks-of-cover threshold &mdash; markdown candidates.</p>
    <div class="card">
      {{ slow_chart_svg }}
      <table id="slowTable">
        <thead><tr>
          <th data-sort="str" data-col="0">Product <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="1">Weeks of cover <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="2">Price <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="3">Margin <span class="arrow">&#9650;&#9660;</span></th>
        </tr></thead>
        <tbody>
        {% for s in slow_movers %}
          <tr>
            <td>{{ s.name }}</td>
            <td class="num" data-v="{{ s.weeks_of_cover }}">{{ "%.1f"|format(s.weeks_of_cover) }}w</td>
            <td class="num" data-v="{{ s.price }}">${{ "%.2f"|format(s.price) }}</td>
            <td class="num" data-v="{{ s.margin }}">{{ "%.1f"|format(s.margin) }}%</td>
          </tr>
        {% endfor %}
        </tbody>
      </table>
    </div>
  </section>

  {# ---- Recommendations ---- #}
  <section>
    <h2><span class="dot" style="--accent:var(--purple)"></span>Markdown Recommendations</h2>
    <p class="lead">Policy-bound discounts. Items the floor can't promote meaningfully are flagged for clearance.</p>
    <div class="policy">
      <span class="chip">Max discount <b>{{ policy.max_discount_pct }}%</b></span>
      <span class="chip">Margin floor <b>{{ policy.margin_floor_pct }}%</b></span>
      <span class="chip">Min useful promo <b>{{ policy.min_useful_promo_discount_pct }}%</b></span>
    </div>
    <div class="card" style="margin-top:14px">
      {{ rec_chart_svg }}
      <div class="controls">
        <label class="toggle"><input type="checkbox" id="hideClear"> Hide clearance-flagged items</label>
      </div>
      <table id="recTable">
        <thead><tr>
          <th data-sort="str" data-col="0">Product <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="1">Current <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="2">Discount <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="3">New price <span class="arrow">&#9650;&#9660;</span></th>
          <th class="num" data-sort="num" data-col="4">New margin <span class="arrow">&#9650;&#9660;</span></th>
          <th data-sort="str" data-col="5">Binding</th>
        </tr></thead>
        <tbody>
        {% for r in recommendations %}
          <tr class="{{ 'is-clearance' if r.flag_clearance else '' }}" data-clearance="{{ '1' if r.flag_clearance else '0' }}">
            <td>{{ r.name }}</td>
            <td class="num" data-v="{{ r.price }}">${{ "%.2f"|format(r.price) }}</td>
            <td class="num" data-v="{{ r.rec_discount_pct or 0 }}">{% if r.rec_discount_pct %}<span class="down">&minus;{{ "%.1f"|format(r.rec_discount_pct) }}%</span>{% else %}&mdash;{% endif %}</td>
            <td class="num" data-v="{{ r.new_price or 0 }}">{% if r.new_price %}${{ "%.2f"|format(r.new_price) }}{% else %}&mdash;{% endif %}</td>
            <td class="num" data-v="{{ r.new_margin or 0 }}">{% if r.new_margin %}{{ "%.1f"|format(r.new_margin) }}%{% else %}&mdash;{% endif %}</td>
            <td>
              {% if r.flag_clearance %}<span class="pill clear">CLEARANCE</span>
              {% elif r.binding == 'margin_floor' %}<span class="pill floor">Margin floor</span>
              {% else %}<span class="pill cap">Max-discount cap</span>{% endif %}
            </td>
          </tr>
        {% endfor %}
        </tbody>
      </table>
      {% if clearance_names %}
      <p class="note">&#9888; Clearance is a separate manager decision: {{ clearance_names|join(', ') }}. The margin floor caps these below the minimum useful promo, so they should be cleared out rather than promoted.</p>
      {% endif %}
    </div>
  </section>

  <footer>
    <div class="brand display">BLASTBOX OMEGA</div>
    <div>Merch Insights Assistant &middot; end-of-quarter markdown review &middot; all figures are mock data for demonstration.</div>
  </footer>
</div>

<script>
(function(){
  // Count-up KPI animation
  document.querySelectorAll('.countup').forEach(function(el){
    var to=parseFloat(el.getAttribute('data-to'))||0, t0=null, dur=700;
    function step(ts){ if(!t0)t0=ts; var p=Math.min(1,(ts-t0)/dur);
      el.textContent=Math.round(to*(p<1?(1-Math.pow(1-p,3)):1)); if(p<1)requestAnimationFrame(step);}
    requestAnimationFrame(step);
  });
  // Animate bars from 0 to target width
  requestAnimationFrame(function(){ requestAnimationFrame(function(){
    document.querySelectorAll('.bar[data-w]').forEach(function(b){ b.setAttribute('width', b.getAttribute('data-w')); });
  });});
  // Sortable tables
  function sortTable(table, col, type, asc){
    var tb=table.tBodies[0], rows=Array.prototype.slice.call(tb.rows);
    rows.sort(function(a,b){
      var x=a.cells[col], y=b.cells[col];
      if(type==='num'){ var xv=parseFloat((x.getAttribute('data-v')||x.textContent).replace(/[^0-9.\-]/g,''))||0;
        var yv=parseFloat((y.getAttribute('data-v')||y.textContent).replace(/[^0-9.\-]/g,''))||0; return asc?xv-yv:yv-xv; }
      var xs=x.textContent.trim().toLowerCase(), ys=y.textContent.trim().toLowerCase();
      return asc?(xs<ys?-1:xs>ys?1:0):(xs>ys?-1:xs<ys?1:0);
    });
    rows.forEach(function(r){tb.appendChild(r);});
  }
  document.querySelectorAll('table').forEach(function(table){
    table.querySelectorAll('th[data-sort]').forEach(function(th){
      var asc=true;
      th.addEventListener('click', function(){
        sortTable(table, parseInt(th.getAttribute('data-col'),10), th.getAttribute('data-sort'), asc);
        asc=!asc;
      });
    });
  });
  // Clearance toggle
  var hc=document.getElementById('hideClear');
  if(hc){ hc.addEventListener('change', function(){
    document.querySelectorAll('#recTable tr[data-clearance="1"]').forEach(function(r){ r.style.display=hc.checked?'none':''; });
  });}
})();
</script>
</body>
</html>
'''


if __name__ == "__main__":
    main()
