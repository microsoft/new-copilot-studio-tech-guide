# Flagship Scenario — "The Block Party Trade-Up"

> **The kitchen-sink associate demo.** One customer, one visit, every pillar of the
> Modern Agent Experience firing together: **two parent MCPs**, **three connected
> agents — each with its own MCP underneath**, **four runtime-Python skills (Python as
> a bundled resource, never inline)**, the **new `get_console_exclusives` upsell tool**,
> and **two generated files** (a settlement **PDF** and a sales **PNG** chart).
>
> A customer who runs a neighbourhood retro-gaming league walks up to the returns &
> service desk with a dead console and a shopping list. The **Returns & Service
> Assistant** (associate-facing) orchestrates the whole thing: rules the defect, prices
> the trade-up, upsells the MEGA-only AAA titles, sanity-checks the upsell against real
> sales data, reconciles the customer's BlastPoints, and prints the settlement.

## Cast on stage

| Component | Kind | MCP / tools | Role in this run |
| --- | --- | --- | --- |
| **Returns & Service Assistant** | Parent agent | **Membership MCP** (`get_membership`, `cancel_membership`), **Order Management MCP** (`get_order`, `request_return`) | Talks to the associate; owns the skills; relays Q&A between the connected agents and the associate. |
| **Store Policy Agent** | Connected agent | **Policy RAG MCP** (`search_policy`, `get_tier_refund_policy`) | Rules defective-vs-accidental; supplies the proration rule. Asks the defect-cause question first. |
| **Inventory & Fulfillment Agent** | Connected agent | **Warehouse MCP** (`check_stock`, `find_alternatives`, `get_restock_date`, `check_game_compatibility`, **`get_console_exclusives`** ← new) | Stock + transfer picture, game→model compatibility, and the **upsell** titles. |
| **Sales & Performance Agent** | Connected agent | **Sales & Performance MCP** (`query_sales`, `get_catalog`) | Confirms the MEGA Edition is the right thing to push (velocity, margin, weeks-of-cover). |
| **prorated-refund-calculator** | Skill (Python resource: `blastpass_refund.py`) | — | Membership proration refund. |
| **sales-analysis-chart** | Skill (Python resource: `sales_chart.py`) | — | Derives velocity / weeks-of-cover / margin and renders the buy-side charts. |
| **points-reconciliation** | Skill (Python resource: `points_reconciliation.py`) | — | BlastPoints earn / promo / expiry. |
| **slip-pdf-generator** | Skill (Python resource) | — | Markdown slip → settlement **PDF** (reportlab). |
| **File generation** | Output | — | `blastbox_slip.pdf` (settlement) + `top_sellers.png` / `slow_movers.png` (sales). |

### Fixtures (all mock)

- **Customer:** Jordan Pixel, member **`MEGA-BLAST-1024`**, **BlastPass Plus Extra**
  ($129.99/yr), 8 unused months, $10 cancellation fee, outside the 14-day cooling-off.
  Runs the **"Pixel Block Party"** neighbourhood gaming league.
- **Hardware:** base **BlastBox Omega** $399.99 (**out of stock** locally, restock ~4
  days); **MEGA Edition** $499.99 (**5 in stock**, Seattle-WA1).
- **MEGA-only AAA titles** (from `get_console_exclusives("mega")`): *MEGA Lizards from
  Outer Space*, *Galactic Tax Evader VII: Audit Protocol*, *Mecha-Granny: Knitpocalypse*
  — none run on the base console.
- **Loyalty:** it's the week after **Triple BLAST Weekend** (3× base points). Jordan's
  prior balance 4,200 pts; 800 pts just expired.

## Expected headline numbers (verified against the bundled scripts)

- Membership prorated refund = **$76.66**  (8 × $10.8325 − $10)  ← `blastpass_refund.py`
- Upgrade difference = **$100.00**  ($499.99 − $399.99)
- **Net due from customer = $23.34**  ($100.00 − $76.66 store credit)
- BlastPoints after the weekend = **21,400 pts → $105.00** store credit
  (4,200 + 18,000 earned − 800 expired)  ← `points_reconciliation.py`

---

## Turn 1 — The request → the defect-cause question (round-trip)

### 🧑‍💼 Associate

> Customer's running a gaming-league block party this weekend and his main console
> just died about 10 days after he bought it — he's got the receipt. He wants a
> replacement if one's in stock, otherwise a refund, and while he's here he wants to
> cancel his BlastPass, member ID MEGA-BLAST-1024. Walk me through all of it.

### ⚙️ Orchestration

1. **Returns & Service Assistant** → **Store Policy Agent** → Policy RAG MCP ·
   `search_policy("defective console warranty vs accidental damage")`. The Policy
   Agent's rule is to **establish the defect cause first**, so it returns a *question*,
   not a ruling.
2. → **Membership MCP** · `get_membership("MEGA-BLAST-1024")` (read-only) — Jordan
   Pixel, Plus Extra, 8 months unused, $10 fee, cooling-off expired.
3. **Reasoning:** *"I can't rule on the console until I know the cause. Relay the Policy
   Agent's question before touching anything."*

### 🤖 Returns & Service Assistant

> Happy to handle the whole visit. One thing decides how the console is treated:
>
> - a **manufacturing defect** (won't power on, no physical damage) → **free warranty
>   swap, no restocking fee**, vs.
> - **accidental / physical / liquid damage** → standard return, possible fee.
>
> **Which is it — a manufacturing fault, or could it be accidental damage?**

> **Pillar:** connected-agent → parent → associate **question round-trip**. Nothing is
> ruled or changed yet.

---

## Turn 2 — Ruling + stock + the upsell

### 🧑‍💼 Associate

> Manufacturing fault — just won't power on, no physical or liquid damage.

### ⚙️ Orchestration

1. → **Store Policy Agent** · confirms **defective within 30 days = free warranty swap,
   no restocking fee**.
2. → **Inventory & Fulfillment Agent** → Warehouse MCP · `check_stock` (base = **0
   local**), `get_restock_date` (~4 days), `find_alternatives` (**MEGA Edition, $499.99,
   5 in stock, Seattle-WA1**).
3. → **Inventory & Fulfillment Agent** · **`get_console_exclusives("mega")`** — returns
   the three MEGA-only AAA titles **with a ready-to-say pitch each** (the **upsell**).
4. → **prorated-refund-calculator** skill — runs **`blastpass_refund.py`** →
   `$129.99 ÷ 12 = $10.8325; × 8 = $86.66; − $10 =` **$76.66**.

### 🤖 Returns & Service Assistant

> Confirmed — **manufacturing defect inside 30 days → free warranty swap, no restocking
> fee.** 👍
>
> - The **base console is out of stock** here (restock ~4 days).
> - The **MEGA Edition is in stock now** at **$499.99** (base was $399.99).
>
> Worth a word to the customer for the block party — the MEGA Edition unlocks three
> titles the base console **can't run at all**:
>
> 1. **MEGA Lizards from Outer Space** — *"200 neon space-lizards on screen at once with
>    a co-op buddy screaming next to you; won't run on the base console."*
> 2. **Galactic Tax Evader VII: Audit Protocol** — *"You're an interstellar accountant
>    dodging audits in real time — critics call it 'Grand Theft Spreadsheet.'"*
> 3. **Mecha-Granny: Knitpocalypse** — *"A battle-mech granny knitting the apocalypse
>    back together in 4K. The yarn engine only spins up on MEGA."*
>
> On the membership: Plus Extra, 8 unused months, $10 fee → prorated refund **$76.66**.
>
> So he can **wait ~4 days** for a like-for-like swap, **upgrade to MEGA now**, or take
> a **refund**. If he upgrades, I can put the $76.66 toward it. Which way?

> **Pillars:** second connected agent (Inventory) with its own MCP; the **new
> `get_console_exclusives` upsell tool**; first **Python-resource skill** run.

---

## Turn 3 — "Is the MEGA push actually worth it?" (Sales & Performance Agent)

### 🧑‍💼 Associate

> He's tempted by the MEGA. Before I push it — and before I reorder for the block-party
> rush — is the MEGA Edition actually our strongest play right now, or am I about to
> over-order? Show me the numbers.

### ⚙️ Orchestration

1. → **Sales & Performance Agent** → Sales & Performance MCP · `query_sales(2026-04-12,
   2026-05-31)` (raw weekly units/revenue) + `get_catalog()` (price, unit_cost,
   stock_on_hand). The MCP returns **raw data only**.
2. → **sales-analysis-chart** skill — saves those results to `rows.json` / `catalog.json`
   and runs **`sales_chart.py`**, which derives velocity, **weeks-of-cover**, and margin
   itself and renders **`top_sellers.png`** + **`slow_movers.png`**.
3. *(optional)* → **Inventory & Fulfillment Agent** · `get_inventory_aging()` to enrich
   the slow-mover read (no inbound + high weeks-of-cover = clearance signal).

### 🤖 Returns & Service Assistant

> Pulled the last 8 weeks. 📈
>
> - **Top seller: BlastBox Omega MEGA Edition** — fastest unit velocity in the console
>   category and a healthy margin; **weeks-of-cover is low**, so reordering ahead of the
>   block party is justified, not over-ordering.
> - **Slow mover: the last-gen Cadet bundle** — sitting well past the 8-week cover line
>   with no inbound PO; that's the clearance candidate, not the MEGA.
>
> Charts attached — **`top_sellers.png`** and **`slow_movers.png`**. Net: pushing the
> MEGA Edition is the right call, and a modest MEGA reorder is safe.

> **Pillars:** third connected agent (Sales) with its own MCP doing real branching
> analysis; a **Python-resource skill** that turns raw MCP rows into derived metrics and
> a **PNG file**.

---

## Turn 4 — Execute → settle → loyalty → PDF

### 🧑‍💼 Associate

> Do it: warranty-swap him into the MEGA Edition, cancel the BlastPass and apply the
> $76.66 as store credit toward the upgrade, and tell me what he owes. Also — after
> Triple BLAST Weekend, what are his BlastPoints worth, in case he wants to knock down
> the balance? Then print me the slip as a PDF.

### ⚙️ Orchestration

1. → **Inventory & Fulfillment Agent** · `check_game_compatibility` confirms all three
   titles **require the MEGA Edition**.
2. → **Order Management MCP** · `request_return` (defective console, warranty exchange).
3. → **Membership MCP** · `cancel_membership("MEGA-BLAST-1024", 76.66)` — cancels
   BlastPass; refund issued as **store credit**.
4. → **points-reconciliation** skill — runs **`points_reconciliation.py`**:
   prior 4,200; earn $499.99 × 3× (Triple BLAST) × 1.2 (Plus Extra) = **18,000**;
   expired **−800** → **21,400 pts → 21 × $5 = $105.00** store credit.
5. → **slip-pdf-generator** skill — runs reportlab → **`blastbox_slip.pdf`**.

### 🤖 Returns & Service Assistant

> All done — here's the breakdown:
>
> - **Defective console** (manufacturing warranty) credited at **$399.99**, no fee.
> - **MEGA Edition** $499.99 → **upgrade difference $100.00**.
> - **BlastPass Plus Extra cancelled** → prorated refund **$76.66** applied as store
>   credit.
> - **Net due from customer: $23.34.**
>
> 🎮 **BlastPoints after the weekend: 21,400** (worth **$105.00**). He can redeem
> **$23.34** of that to walk out owing **$0** and keep ~**$81.66** in points — or pay
> the $23.34 and keep all 21,400.
>
> 🧾 Slip generated as a **PDF** — `blastbox_slip.pdf` — with the swap, the cancellation,
> the settlement math, and the three MEGA-exclusive titles noted as suggested add-ons.

> **Pillars:** parent **MCP** state changes (`request_return`, `cancel_membership`); two
> more **Python-resource skills** (points + PDF); **file generation** (PDF).

---

## How this scenario maps to the pillars

| Pillar | Where it fires |
| --- | --- |
| **Connected agents (each with its own MCP)** | Store Policy Agent · Policy RAG MCP (T1–T2); Inventory & Fulfillment Agent · Warehouse MCP (T2, T4); Sales & Performance Agent · Sales & Performance MCP (T3) |
| **Multiple MCP servers** | Membership + Order Management (parent) · Policy RAG · Warehouse · Sales & Performance — **five MCPs** |
| **Connected-agent → parent → user round-trip** | Defect-cause question (T1) |
| **Skill runs Python — as a bundled resource, not inline** | `blastpass_refund.py` (T2), `sales_chart.py` (T3), `points_reconciliation.py` (T4), `slip-pdf-generator` (T4) |
| **New tool built for this demo** | `get_console_exclusives` on the Warehouse MCP (T2) |
| **File generation** | `top_sellers.png` / `slow_movers.png` (T3) + `blastbox_slip.pdf` (T4) |
| **Complex, multi-turn request** | 4 turns: rule → upsell → data-backed buy decision → settle + loyalty + file |

## Why it's the flagship

- **Three connected agents, three MCPs under them** — Policy *rules*, Inventory *stocks
  and upsells*, Sales *justifies the push with real velocity/margin* — plus two parent
  MCPs. The orchestration genuinely fans out and comes back.
- **The upsell is data-driven twice over:** `get_console_exclusives` gives the associate
  the words, and the Sales & Performance Agent + `sales_chart.py` prove the MEGA Edition
  is the right thing to push (and the Cadet bundle is the thing to clear).
- **Every skill keeps Python as a resource file** — the agent passes MCP results to a
  bundled, validated script; it never hand-rolls arithmetic or inlines a script.
- **It ends with artifacts a real associate keeps:** a settlement PDF and two buy-side
  charts — and a loyalty twist that lets the customer walk out owing nothing.

## Notes

- All data is mock; serials, prices, game titles, and confirmation numbers are
  illustrative. Headline numbers ($76.66 / $100.00 / **$23.34**; **21,400 pts → $105.00**)
  are reproduced by the bundled scripts `blastpass_refund.py` and
  `points_reconciliation.py`.
- This is the **associate-facing** agent (separate from the self-serve BlastPass
  Concierge). It reuses the backed-up associate skills/connected agents in
  `agents-cli/_associate-skill-library/` plus the new `get_console_exclusives` tool.
- Build order: (1) add `get_console_exclusives` to the Warehouse MCP, (2) attach the four
  Python-resource skills to the parent, (3) wire the three connected agents with their
  MCPs, (4) run the 4-turn transcript in Preview and capture actuals.
