# Store-Associate Script — Defective Console → Warranty Swap → MEGA Upgrade (with Upsell)

> **Scenario A (associate-facing).** A customer is standing at the returns desk with a
> **dead BlastBox Omega** console. They want it handled, and while they're there they
> want to **cancel their BlastPass** membership. The associate talks to the
> **Returns & Service Assistant** (associate-facing), which orchestrates everything:
> rules the defect as a **warranty swap**, computes the **prorated membership refund**,
> **upsells** the customer to the **MEGA edition** by surfacing **two new AAA titles
> that only run on MEGA**, applies the refund as store credit toward the upgrade, and
> prints the slip.
>
> This is the associate counterpart to the self-serve card-reissue warm-up: same store
> world, but **associate voice**, two connected agents, two runtime-Python skills, and a
> **new upsell tool** on the Warehouse MCP.

## Cast on stage

| Component | Kind | Role in this run |
| --- | --- | --- |
| **Returns & Service Assistant** | Parent agent | Talks to the **associate**; owns the Membership MCP + skills; relays Q&A between the connected agents and the associate. |
| **Store Policy Agent** | Connected agent | **Policy RAG MCP** (`search_policy`, `get_tier_refund_policy`). Asks the defect-cause question; rules defective-vs-accidental. |
| **Inventory & Fulfillment Agent** | Connected agent | **Warehouse MCP** (`check_stock`, `find_alternatives`, `get_restock_date`, `check_game_compatibility`, **`get_console_exclusives`** ← new upsell tool). |
| **Membership MCP** | Parent tool | `get_membership`, `cancel_membership`. |
| **prorated-refund-calculator** | Skill (runtime Python) | Prorate the membership refund **and** net the upgrade settlement. |
| **slip-pdf-generator** | Skill (runtime Python) | Markdown slip template → **PDF** (reportlab). |
| **File generation** | Output | A swap/RMA slip **PDF** with the full money breakdown. |

### The new upsell tool

**`get_console_exclusives(model)`** on the Warehouse MCP returns the AAA titles that
**only run on a given console model** — plus a short, ready-to-say **pitch** for each —
so the associate can make the upgrade an easy "yes." For the **MEGA edition** it returns
three exclusives:

| Title | Genre | Why it's MEGA-only |
| --- | --- | --- |
| **MEGA Lizards from Outer Space** | Co-op chaos shooter | Needs the MEGA edition's extra VRAM for 200 on-screen lizards. |
| **Galactic Tax Evader VII: Audit Protocol** | Open-world heist sim | Real-time audit physics only run on the MEGA co-processor. |
| **Mecha-Granny: Knitpocalypse** | Action roguelite | 4K knitting simulation requires the MEGA edition's yarn engine. |

> The base BlastBox Omega runs **none** of these. That's the upsell hook.

### Associate upsell pitch (the short script per game)

These are the `pitch` strings `get_console_exclusives("mega")` returns — one or two
punchy lines the associate can read straight to the customer:

- **MEGA Lizards from Outer Space** — *"Picture two hundred neon space-lizards on
  screen at once and a co-op buddy screaming next to you. It's the #1 couch-chaos
  shooter of the year — and it physically will not run on the base console."*
- **Galactic Tax Evader VII: Audit Protocol** — *"You're an interstellar accountant on
  the run, dodging audits in real time across a living open galaxy. Critics call it
  'Grand Theft Spreadsheet.' MEGA-exclusive, because the audit physics melt anything
  smaller."*
- **Mecha-Granny: Knitpocalypse** — *"A sweet old lady in a battle-mech knits the
  apocalypse back together, one 4K scarf at a time. It's hilarious, it's brutal, and
  the yarn engine only spins up on MEGA."*

> **One-liner the associate can lead with:** *"Quick heads-up — the MEGA edition plays
> three big titles your current console literally can't load. Want the 20-second
> pitch?"*

**Member on file:** `MEGA-BLAST-1024` — **Jordan Pixel**, **BlastPass Plus Extra**
($129.99/yr), **8 unused months**, **$10** cancellation fee, $0 non-refundable credit,
**outside** the 14-day cooling-off window.

**Hardware:** base **BlastBox Omega** = **$399.99** (out of stock locally, restock ~4
days); **MEGA edition** = **$499.99** (in stock now).

## Expected headline numbers

- Membership prorated refund = **$76.66**  (8 × $10.8325 − $10 fee)
- Upgrade difference = **$100.00**  ($499.99 − $399.99)
- **Net due from customer = $23.34**  ($100.00 − $76.66 store credit)

---

## Turn 1 — The request → the defect-cause question (round-trip)

### 🧑‍💼 Associate

> A customer's BlastBox Omega died about 10 days after they bought it — they've got
> the receipt. They want a replacement if one's in stock, otherwise a refund. While
> they're here they also want to cancel their BlastPass, member ID MEGA-BLAST-1024.
> Walk me through the whole thing.

### ⚙️ Orchestration (what the agent did)

1. **Returns & Service Assistant** → **Store Policy Agent** → Policy RAG MCP ·
   `search_policy("defective console warranty vs accidental damage")`. The Policy
   Agent's rule is to **establish the defect cause first**, so it returns a *question*
   rather than a ruling.
2. → **Membership MCP** · `get_membership("MEGA-BLAST-1024")` (read-only) — Jordan
   Pixel, Plus Extra, 8 months unused, $10 fee, cooling-off expired.
3. **Returns & Service Assistant** (reasoning): *"I can't rule on the console until I
   know the cause. I'll relay the Policy Agent's question to the associate before I
   touch anything."*

### 🤖 Returns & Service Assistant

> Happy to handle all of it. One thing first — policy treats these two cases
> differently:
>
> - a **manufacturing defect** (won't power on, no physical damage) → **free warranty
>   swap, no restocking fee**, vs.
> - **accidental / physical / liquid damage** → standard return, possible restocking
>   fee.
>
> **Which is it — a manufacturing fault, or could it be accidental damage?**

> **Why one turn for this:** the only job here is the **connected-agent → parent →
> associate** round-trip. Nothing is ruled, refunded, or swapped until the cause is
> known.

---

## Turn 2 — Ruling + refund + the upsell

### 🧑‍💼 Associate

> Manufacturing fault — it just won't power on, no physical or liquid damage.

### ⚙️ Orchestration (what the agent did)

1. → **Store Policy Agent** · confirms **defective within 30 days = free warranty
   swap, no restocking fee**.
2. → **Inventory & Fulfillment Agent** → Warehouse MCP · `check_stock` (base console =
   **0 local**), `get_restock_date` (~4 days), `find_alternatives` (**MEGA edition,
   $499.99, in stock now**).
3. → **Inventory & Fulfillment Agent** · **`get_console_exclusives("mega")`** —
   returns the three MEGA-only AAA titles (the **upsell** hook).
4. → **prorated-refund-calculator** skill — **runtime Python**:
   `$129.99 ÷ 12 = $10.8325; × 8 = $86.66; − $10 fee =` **$76.66**.

### 🤖 Returns & Service Assistant

> Confirmed: that's a **manufacturing defect inside 30 days → free warranty swap, no
> restocking fee.** 👍
>
> Here's the situation:
>
> - The **same base console is out of stock** (restock in ~4 days).
> - The **MEGA edition is in stock right now** at **$499.99** (the base was $399.99).
>
> Worth mentioning to the customer — the MEGA edition unlocks titles the base console
> **can't run at all**:
>
> 1. **MEGA Lizards from Outer Space** — co-op chaos shooter
> 2. **Galactic Tax Evader VII: Audit Protocol** — open-world heist sim
> 3. **Mecha-Granny: Knitpocalypse** — action roguelite
>
> On the membership: BlastPass **Plus Extra** for MEGA-BLAST-1024 (Jordan Pixel), 8
> unused months, $10 fee, cooling-off expired → prorated refund **$76.66**
> ($10.8325 × 8 = $86.66, − $10).
>
> So the customer can **wait ~4 days** for a like-for-like swap, **upgrade to MEGA
> now**, or take a **refund**. If they upgrade, I can put the $76.66 membership refund
> toward it. Which way do you want to go?

> **Why one turn for this:** the warranty ruling, the stock picture, the **upsell**
> (the two new MEGA-exclusive AAA games), and the refund math all land together so the
> associate can present one clear set of options. Nothing has been charged or cancelled
> yet.

---

## Turn 3 — Upgrade swap → cancel membership → settlement → PDF

### 🧑‍💼 Associate

> The customer's sold — they want the MEGA edition so they can play "Galactic Tax
> Evader VII" and "Mecha-Granny." Do the warranty swap into the MEGA, cancel the
> BlastPass, apply the membership refund as store credit toward the upgrade, and tell
> me what they owe. Then print me the slip as a PDF.

### ⚙️ Orchestration (what the agent did)

1. → **Inventory & Fulfillment Agent** · `check_game_compatibility` confirms both
   titles **require the MEGA edition** (won't run on base).
2. → **Membership MCP** · `cancel_membership("MEGA-BLAST-1024", 76.66)` — cancels
   BlastPass, returns a cancellation confirmation; refund issued as **store credit**.
3. → **prorated-refund-calculator** skill — **runtime Python** nets the upgrade:
   `$499.99 − $399.99 = $100.00 upgrade diff; − $76.66 store credit =` **$23.34**.
4. → **slip-pdf-generator** skill — **runtime Python (reportlab)** renders the
   markdown slip template → **`blastbox_slip.pdf`**.

### 🤖 Returns & Service Assistant

> Done — here's the full breakdown:
>
> - **Defective console** (manufacturing warranty) credited at **$399.99**, no
>   restocking fee.
> - **MEGA edition** at **$499.99** → **upgrade difference $100.00**.
> - **BlastPass Plus Extra cancelled** (MEGA-BLAST-1024) → prorated refund **$76.66**
>   applied as **store credit**.
> - **Net due from customer: $23.34.**
>
> 🧾 I've generated the swap/RMA slip as a **PDF** — `blastbox_slip.pdf` — with the
> console swap, the membership cancellation, and the money breakdown. The two
> MEGA-exclusive titles are noted on the slip as suggested add-ons.

> **Why one turn for this:** once the customer commits, the state changes
> (`cancel_membership`), the settlement math, and the file generation are a single
> uninterrupted beat — the associate gets one clean resolution and a printable slip.

---

## Expected output

**`blastbox_slip.pdf`** — a swap/RMA slip listing: the defective console (warranty
credit $399.99, no fee), the MEGA edition ($499.99), the upgrade difference ($100.00),
the membership cancellation + prorated refund ($76.66 store credit), the **net due
$23.34**, and the two MEGA-exclusive AAA titles as suggested add-ons.

## Why it's a strong associate demo

- **Every pillar fires:** connected-agent ↔ parent ↔ associate round-trip (T1),
  back-and-forth with **both** connected agents (T1–T3), **runtime Python** twice
  (prorate + settlement) plus reportlab for the PDF, a parent **MCP** doing real state
  change (`cancel_membership`), and **file generation** (T3).
- **Policy changes the math:** the defect ruling waives the restocking fee, turning a
  "return" into a **warranty swap**.
- **The upsell is data-driven:** the **`get_console_exclusives`** tool turns "we're out
  of stock" into an **upgrade opportunity** by naming AAA titles the base console can't
  run — the associate closes a bigger sale, and the customer's refund softens the price.

## Notes

- **New tool to build:** `get_console_exclusives(model)` on the Warehouse MCP, returning
  the MEGA-only AAA titles above. Keep `check_game_compatibility` for the per-title
  confirmation in Turn 3.
- Audience is **associate voice** — the agent talks to the associate ("tell the
  customer…", "what do you want to do?"), not directly to the shopper. This is the
  separate associate-facing agent, distinct from the self-serve BlastPass Concierge.
- All data is mock; serials, prices, game titles, and confirmation numbers are
  illustrative. Headline numbers ($76.66 / $100.00 / **$23.34**) match the
  `prorated-refund-calculator` logic.
