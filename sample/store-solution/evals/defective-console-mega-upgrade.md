# Eval Scenario: "The Defective Console & the MEGA Upgrade"

> **Purpose.** A single end-to-end eval for the **Modern Agent Experience** demo on the
> **Returns & Service Assistant**. It exercises all of: a connected-agent → parent → user
> **question round-trip**, **back-and-forth with two connected agents**, **runtime-generated
> Python** in skills, a **parent MCP tool** (code), and **PDF file generation**.

## Cast

| Component | Kind | Used for |
| --- | --- | --- |
| **Returns & Service Assistant** | Parent agent | Orchestrates; owns skills + Membership MCP; relays Q&A to the associate. |
| **Store Policy Agent** | Connected agent | **Policy RAG MCP** (`search_policy`, `get_tier_refund_policy`). Asks the defect-cause question. |
| **Inventory & Fulfillment Agent** | Connected agent | **Warehouse MCP** (`check_stock`, `find_alternatives`, `get_restock_date`). Surfaces MEGA edition + game→model compatibility. |
| **Membership MCP** | MCP tool (parent) | `get_membership`, `cancel_membership`. |
| `refund-calculator` | Skill (runtime Python) | Prorate refund **and** the upgrade settlement. |
| `slip-pdf-generator` | Skill (runtime Python) | Markdown slip template → **PDF** (reportlab). |

## Fixtures (mock data this eval depends on)

- Member **`MEGA-BLAST-1024`** = Jordan Pixel, BlastPass **Plus Extra**, $129.99/yr,
  8 unused months, $10 cancellation fee, $0 non-refundable credit, **outside** cooling-off.
- Base **BlastBox Omega** console purchase price **$399.99**; **MEGA edition** **$499.99**.
- Game **"MEGA Lizards from Outer Space"** requires the **MEGA edition** (won't run on base).
- Base console **out of stock** locally; restock ~4 days; **MEGA edition in stock now**.

## Expected headline numbers

- Membership prorated refund = **$76.66**  (8 × $10.8325 − $10)
- Upgrade difference = **$100.00**  ($499.99 − $399.99)
- **Net due from customer = $23.34**  ($100.00 − $76.66 store credit)

---

## Transcript (what the associate types / what the agent should answer / tools used)

### Turn 1

**Associate:**
> A customer's BlastBox Omega console died 10 days after purchase — they still have the
> receipt. They want a replacement if you have one in stock, otherwise a refund. While
> they're at the desk they also want to cancel their BlastPass membership, customer id
> MEGA-BLAST-1024. Walk me through the whole thing.

**Agent (expected) — relays the Policy Agent's clarifying question:**
> Before I confirm how this console is handled, I need one detail. Policy distinguishes a
> **manufacturing defect** (free warranty swap, no restocking fee) from
> **accidental/physical/liquid damage** (standard return, possible restocking fee).
> **Is the failure a manufacturing fault (won't power on, no physical damage), or could it
> be accidental/physical damage?**

**Tools/skills:**
1. Store Policy Agent → Policy RAG MCP · `search_policy("defective console warranty vs accidental damage")`
2. Policy Agent returns a **question** to the parent
3. Parent **relays the question to the associate** ← round-trip

**Pass criteria:** the agent does NOT rule yet; it asks the defect-cause question that
originated from the Store Policy Agent.

### Turn 2

**Associate:**
> Manufacturing fault — it just won't power on, no physical or liquid damage.

**Agent (expected):**
> Confirmed manufacturing defect inside 30 days → **free warranty swap, no restocking fee**.
> Same model is **out of stock** (restock ~4 days). Note: the title **"MEGA Lizards from
> Outer Space" requires the MEGA edition** — it won't run on the base console. The **MEGA
> edition is in stock now**.
> Membership MEGA-BLAST-1024 (Jordan Pixel), Plus Extra, 8 unused months, $10 fee, outside
> cooling-off → prorated refund **$76.66** ($129.99÷12=$10.8325; ×8=$86.66; −$10).
> Wait for restock, upgrade to MEGA, or refund?

**Tools/skills:**
1. Store Policy Agent → Policy RAG MCP · confirms warranty-swap ruling
2. Inventory & Fulfillment Agent → Warehouse MCP · `check_stock` (base=0), `find_alternatives`
   (MEGA edition + game compatibility), `get_restock_date`
3. Membership MCP · `get_membership("MEGA-BLAST-1024")`
4. `refund-calculator` skill → **runtime Python** → **$76.66**

**Pass criteria:** warranty ruling = no fee; Inventory surfaces MEGA edition AND the game→MEGA
compatibility; refund = **$76.66** and the calc is shown.

### Turn 3

**Associate:**
> The customer wants the MEGA edition — it runs "MEGA Lizards from Outer Space" and their
> current model can't. Do the upgrade swap, cancel the membership, apply the membership
> refund as store credit toward the upgrade, and tell me what they owe. Then give me the
> slip as a PDF.

**Agent (expected):**
> - Defective console (warranty) credited at **$399.99**, no restocking fee.
> - MEGA edition **$499.99**. Upgrade difference **$100.00**.
> - BlastPass Plus Extra cancelled for MEGA-BLAST-1024; prorated refund **$76.66** applied as
>   store credit.
> - **Net due from customer: $23.34.**
> - Generated the swap/RMA slip as a **PDF** with the breakdown.

**Tools/skills:**
1. Membership MCP · `cancel_membership("MEGA-BLAST-1024", 76.66)`
2. `refund-calculator` skill → **runtime Python** → upgrade settlement **$23.34**
3. `slip-pdf-generator` skill → **runtime Python** renders markdown template → **PDF**

**Pass criteria:** net due = **$23.34**; membership cancelled; a **PDF** slip is produced.

---

## Pillar coverage matrix

| Pillar | Where |
| --- | --- |
| Connected-agent → parent → user **question round-trip** | Turn 1 |
| Back-and-forth with **both** connected agents | Turns 1–2 |
| **Runtime-generated Python** (skills) | `refund-calculator` (×2), `slip-pdf-generator` |
| **Parent MCP / code** | Membership MCP (`get_membership`, `cancel_membership`) |
| **File generation as PDF** | Turn 3 |

## Build checklist (to make the eval runnable)

- [x] Warehouse MCP mock: MEGA edition alternative, prices ($399.99 / $499.99), and
      "MEGA Lizards from Outer Space" → requires MEGA edition.
- [x] Add Membership MCP to the parent's Tools.
- [x] Remove `return-eligibility-evaluator` from the parent (eligibility is the Policy Agent's job).
- [x] Add `slip-pdf-generator` skill (markdown→PDF template).
- [x] Extend `refund-calculator` to also net the upgrade settlement.
- [x] Store Policy Agent instruction: ask the defect-cause question when not provided.
- [x] Run the 3-turn transcript in Preview; capture actual responses below.

## Actual run results

> 📄 **Full verbatim transcript** (associate messages, orchestration steps, and the agent's
> complete responses for all three turns) lives in
> [`defective-console-mega-upgrade-transcript.md`](./defective-console-mega-upgrade-transcript.md).

**Run date:** 2026-06-07 · Live in Preview on the published **Returns & Service Assistant**
(`32638b69-…`), model Claude Sonnet 4.6. All three turns passed.

**Wiring applied to make it runnable:**
- Warehouse MCP re-deployed with the base/MEGA console SKUs, prices, restock, and a new
  `check_game_compatibility` tool.
- Parent: added **Membership MCP Server** tool; removed `return-eligibility-evaluator`,
  `skill1`, `skill2`, and the generic `refund-calculator`; uploaded
  `prorated-refund-calculator` (prorate + upgrade settlement) and `slip-pdf-generator`
  skills; rewrote instructions for the BlastPass/upgrade/PDF flow.
- Store Policy Agent (was an empty shell): added **Policy RAG MCP Server** + instructions
  (including the defect-cause question rule).
- Inventory & Fulfillment Agent (was an empty shell): added **Warehouse MCP Server** +
  instructions. All three agents published.

**Turn 1 — PASS.** Parent called Store Policy Agent, Inventory & Fulfillment Agent, and
`get_membership` in one pass, then **relayed the Policy Agent's defect-cause question** to
the associate (manufacturing vs accidental). Surfaced base console OOS (restock ~Jun 11) +
MEGA Edition $499.99 in stock, and the membership (Jordan Pixel, Plus Extra, $129.99, 8 mo,
$10 fee, cooling-off expired). The connected-agent→parent→user round-trip fired exactly.

**Turn 2 — PASS.** On "manufacturing fault", Policy Agent ruled **free warranty swap, no
restocking fee**. The `prorated-refund-calculator` skill loaded and ran **runtime Python** →
**$76.66** with the calculation shown ($10.8325 × 8 = $86.66 − $10). Agent also pre-computed
the $23.34 upgrade net.

**Turn 3 — PASS.** Inventory Agent's `check_game_compatibility` confirmed **"MEGA Lizards
from Outer Space" requires the MEGA Edition**. `cancel_membership` cancelled the BlastPass
(ref BPX-CXL-1024-20260607). The `slip-pdf-generator` skill ran **reportlab at runtime** and
produced a downloadable **`blastbox_slip.pdf`** chip in the chat. Final **NET DUE FROM
CUSTOMER = $23.34** ($100.00 upgrade − $76.66 store credit).

**Headline numbers verified live:** $76.66 / $100.00 / **$23.34**, plus the PDF.

**Pillars demonstrated:** connected-agent↔parent↔user round-trip (T1); back-and-forth with
both connected agents (T1–T3); runtime-generated Python (prorate, settlement, reportlab PDF);
parent MCP code (Membership `get_membership` + `cancel_membership`); PDF file generation (T3).

_Note:_ during Turn 3 one inline Python attempt hit a sandbox restriction; the agent
recovered using the skill's embedded worked example, and the dedicated `slip-pdf-generator`
reportlab run succeeded and produced the PDF.

