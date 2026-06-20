# BlastBox Omega — Demo Scenarios

This demo ships **two** scenarios. Full, scripted walkthroughs (turn‑by‑turn
prompts, orchestration, and verified numbers) live in [`evals/`](./evals); this
file is the short index.

## The cast

| Component | Kind | Tools / contents |
| --- | --- | --- |
| **Store Associate Assistant** | Parent agent (flagship) | **Membership MCP** + **Order Management MCP**; owns the refund / points / slip skills; delegates to the connected agents. |
| **Returns & Service Assistant** | Parent agent (warm‑up) | **Membership MCP** (`get_membership`, `reissue_card`) + the membership‑card skill. |
| **Store Policy Agent** | Connected agent | **Policy RAG MCP** (`search_policy`, `get_tier_refund_policy`) + BlastPass policy PDFs. |
| **Inventory & Fullfilment Agent** | Connected agent | **Warehouse MCP** (`check_stock`, `find_alternatives`, `get_restock_date`, `check_game_compatibility`, `get_console_exclusives`). |
| **Skills (runtime Python)** | Skills | `membership-card-png`, `card-reissue`, `prorated-refund-calculator`, `points-reconciliation`, `slip-pdf-generator`. |

### The lore (all invented / mock)

- Console: **BlastBox Omega** (retro‑future gaming console); **MEGA Edition** is the upsell tier.
- Membership: **BlastPass** — **Plus** ($79.99), **Plus Extra** ($129.99), **Plus Extra MEGA!!!** ($199.99).
- Loyalty: **BlastPoints** — 10 pts/$1, tier bonus, promo events like **Triple BLAST Weekend** (3×).
- Headline member: `MEGA-BLAST-1024` — **Jordan Pixel**, BlastPass Plus Extra.

---

## 🟢 Scenario 0 — Self‑Serve BlastPass Card Reissue (warm‑up)

A signed‑in member lost their BlastPass card and self‑serves a replacement from
the MegaBlast member portal. The **simplest** end‑to‑end shape — **one MCP + one
skill + a file** — with identity verified **before** any state changes and one
capability spotlighted per turn.

- **Agent:** Returns & Service Assistant
- **Flow:** `get_membership` (verify) → `reissue_card` (deactivate old serial,
  mint new) → `membership-card-png` skill renders the new card.
- **Output:** `blastpass_card.png` — tier‑colored CR80 card showing the **new** serial.
- **Full script:** [`evals/self-serve-card-reissue.md`](./evals/self-serve-card-reissue.md)

---

## 🟣 Scenario 1 — The Block Party Trade‑Up (flagship)

The kitchen‑sink associate demo: one customer, one visit, every pillar firing —
two parent MCPs, two connected agents (each with its own MCP), three
runtime‑Python skills, the `get_console_exclusives` upsell tool, and a generated
settlement PDF. The associate rules the defect, prices the trade‑up, closes the
customer on MEGA‑only titles, reconciles BlastPoints, and prints the settlement.

- **Agent:** Store Associate Assistant (delegates to Store Policy + Inventory & Fullfilment)
- **Expected headline numbers (verified against the bundled scripts):**
  - Membership prorated refund = **$76.66**
  - Upgrade difference = **$100.00**; **net due from customer = $23.34**
  - BlastPoints after Triple BLAST Weekend = **21,400 pts → $105.00** store credit
- **Output:** `blastbox_slip.pdf` (settlement).
- **Full script:** [`evals/flagship-block-party-trade-up.md`](./evals/flagship-block-party-trade-up.md)
  · runbook: [`evals/flagship-block-party-trade-up-runbook.md`](./evals/flagship-block-party-trade-up-runbook.md)

---

## How the two scenarios map to the Modern Agent Experience

| Pillar | Where you see it |
| --- | --- |
| **Connected agents** | Flagship: Store Associate Assistant → Store Policy + Inventory & Fullfilment. |
| **Multiple MCP servers** | Membership, Order Management, Policy RAG, Warehouse — inline MCP connectors with mock data. |
| **Skills (runtime Python)** | Card render, prorated refund, points reconciliation, slip PDF — Python generated and run in the agent sandbox. |
| **File generation** | BlastPass membership card PNG (warm‑up); settlement slip PDF (flagship). |

To run them, import the solution and follow [`README.md`](./README.md), then paste
the prompts from the eval files into the agent **Test** pane. Everything here is
fictional mock data for demonstration only.
