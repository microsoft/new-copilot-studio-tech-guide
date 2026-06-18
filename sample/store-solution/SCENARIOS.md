# BlastBox Omega — Store Associate Demo Scenarios

> **Modern Agent Experience** showcase. These scenarios demonstrate the four
> pillars of the new default Copilot Studio agent experience working together:
> **connected agents**, **multiple inline MCP servers**, **skills that generate
> and run Python at runtime**, and **file generation** — driven by genuinely
> complex, multi-turn requests from a store associate.

## The cast

| Component | Kind | Tools / contents |
| --- | --- | --- |
| **Returns & Service Assistant** | Parent agent | Orchestrates everything; owns the skills; relays questions to/from the associate. |
| **Store Policy Agent** | Connected agent | **Policy RAG MCP** (`search_policy`, `get_tier_refund_policy`) + 3 BlastPass policy PDFs as knowledge. Always confirms the membership tier first. |
| **Inventory & Fulfillment Agent** | Connected agent | **Warehouse MCP** (`check_stock`, `get_fulfillment_status`, `find_alternatives`, `get_restock_date`). |
| **Order Management MCP** | MCP (on parent) | `search_orders`, `get_order`, `get_shipment`, `request_return`, `get_return_status`. |
| **Membership MCP** | MCP (on parent) | `get_membership`, `cancel_membership`, `reissue_card`. |
| **Skills (parent, generated-Python)** | Skills | `membership-card-png`, `membership-card-pdf`, `prorated-refund-calculator`, `bundle-settlement-calculator`, `points-reconciliation`, `return-eligibility-evaluator`. |
| **File generation** | Output | Printable BlastPass membership card, Return/RMA slip, itemized settlement statement, loyalty statement. |

### The lore (all invented / mock)

- Console: **BlastBox Omega** (retro-future gaming console).
- Membership: **BlastPass**, three tiers — **Plus** ($79.99), **Plus Extra**
  ($129.99), **Plus Extra MEGA!!!** ($199.99).
- Loyalty: **BlastPoints** — 10 pts/$1, tier bonus (Extra +20%, MEGA +50%),
  promo events like **Triple BLAST Weekend** (3x base).
- Mock membership IDs: `MEGA-BLAST-1024` (Extra, headline $76.66 refund),
  `-2048` (MEGA, cooling-off full $199.99), `-4096` (Plus, $39.99),
  `-8192` (MEGA, $38.33).

---

## Scenario 0 — Self-Serve BlastPass Card Reissue  🟢 (warm-up)

*A signed-in member lost their BlastPass card and self-serves a replacement from
the MegaBlast member portal.*

**Showcases:** the **simplest** end-to-end shape — **one MCP + one skill + a file** —
rebuilt for conversational clarity: identity is verified **before** any state
changes, and exactly **one capability is spotlighted per turn**. No connected
agents, no policy, no math. Member voice (no associate), two clean turns.

**Cast:** Parent (BlastPass Concierge) · Membership MCP (`get_membership`,
`reissue_card`) · `membership-card-png` · digital membership card PNG.

> Full transcript: [`evals/self-serve-card-reissue.md`](evals/self-serve-card-reissue.md).

### Flow

1. **Member (signed in):** "I lost my BlastPass card — can you send me a new one?
   My member ID is `MEGA-BLAST-1024`."
2. **Parent → Membership MCP:** `get_membership("MEGA-BLAST-1024")` → Jordan Pixel,
   **Plus Extra**, console serial `OMEGA-7F3A-1024`, card on file
   `BLAST-7F3A-1024`. The agent does **not** change anything yet.
3. **Parent → Member (verification gate):** asks for the **last 4 of the console
   serial** to confirm identity before touching the card. *(End of Turn 1.)*
4. **Member:** "1024." → **Parent** confirms it matches `OMEGA-7F3A-**1024**`.
5. **Parent → Membership MCP:** `reissue_card("MEGA-BLAST-1024", reason: "lost")`
   → deactivates `BLAST-7F3A-1024`, mints a new `card_serial`, keeps the membership
   **active**, queues the physical card for mail (ETA +7 days), returns a reissue
   confirmation number.
6. **Parent runs `membership-card-png`** — generates and runs Python (`matplotlib`)
   that renders a CR80 digital card using the `get_membership` details + the
   **`new_card_serial`** from `reissue_card`: member name, tier badge
   (Plus Extra = purple), member ID, console serial, dates, a barcode keyed to the
   new serial, and the perks line.
7. **Parent → Member:** "You're verified — here's **`blastpass_card.png`** to save
   now. Your old card is deactivated; the physical replacement ships by mail." *(End
   of Turn 2.)*

### Why it's the right opener

- **One of every pillar that scales:** a single MCP lookup → an identity check → a
  single state-changing MCP call → one skill that emits a file — the same shape the
  harder scenarios repeat and stack.
- **Clarity-first pacing:** Turn 1 only verifies; Turn 2 only resolves. Nothing is
  mutated before the member is confirmed, so the audience learns the moving parts
  (MCP → verify → MCP → skill → file) one beat at a time.
- The card's accent color, perks line, and barcode are driven entirely by the MCP's
  `tier_code` and the freshly minted `new_card_serial`, so it visibly reacts to real
  data, not a static template.

### Expected output: **`blastpass_card.png`** (tier-colored digital card showing the **new** serial)

---

## Scenario 1 — The Prorated BlastPass Breakup

*A customer wants to cancel their BlastPass membership mid-term. How much do they
get back?*

**Showcases:** connected-agent relay (parent ↔ Store Policy Agent ↔ associate),
Policy RAG MCP + Membership MCP, the `prorated-refund-calculator` skill running
Python at runtime, and RMA-slip file generation.

**Cast:** Parent · Store Policy Agent (Policy RAG MCP) · Membership MCP ·
`prorated-refund-calculator` · RMA slip.

### Flow

1. **Associate:** "A customer wants to cancel their BlastPass and get a refund —
   member `MEGA-BLAST-1024`."
2. **Parent → Store Policy Agent:** asks for the cancellation/refund policy. The
   Policy Agent's instructions say **ask the tier first**, so it calls
   `search_policy("prorated refund allowed")` and replies: *"Which tier? The rule
   differs for Plus / Plus Extra / MEGA."*
3. **Parent → Associate (relay):** "Which BlastPass tier is it?"
4. **Associate:** "Just the Extra."
5. **Parent → Store Policy Agent:** tier = extra → `get_tier_refund_policy("extra")`
   → returns: prorated allowed, monthly value 129.99/12, **$10 cancellation fee**,
   no welcome credit, 14-day cooling-off rule.
6. **Parent → Membership MCP:** `get_membership("MEGA-BLAST-1024")` → annual_price
   129.99, months_remaining 8, cancellation_fee 10, nonrefundable_credit 0,
   within_cooling_off false.
7. **Parent runs `prorated-refund-calculator`** — generates and runs Python:
   `8 × (129.99/12) − $10 = $86.66 − $10 =` **$76.66**.
8. **Parent → Associate:** "The customer is owed **$76.66**. Want me to cancel and
   print the slip?" On yes → `cancel_membership` + generate the RMA slip file.

### Expected headline number: **$76.66**

---

## Scenario 2 — The MEGA Bundle Meltdown  ⭐ (flagship)

*"They bought the whole MEGA Bundle last month, opened everything, hated it, and
now they want to return all of it, cancel BlastPass, AND put it on store credit."*

**Showcases:** the deepest back-and-forth — **three** MCPs + **two** connected
agents + **two** skills chained, two runtime-Python passes, and a generated
itemized **settlement statement** file. This is the "sophistication" demo.

**Cast:** Parent · Order Management MCP · Store Policy Agent (Policy RAG MCP) ·
Inventory & Fulfillment Agent (Warehouse MCP) · Membership MCP ·
`return-eligibility-evaluator` · `bundle-settlement-calculator` ·
`prorated-refund-calculator` · settlement statement file.

### Flow

1. **Associate:** "Order `BBX-90210` — they want to return the entire MEGA Bundle
   and cancel their BlastPass membership, all to store credit."
2. **Parent → Order Management MCP:** `get_order("BBX-90210")` → line items:
   opened console $499.99, two opened OmegaGrip controllers $69.99 ea, a sealed
   Galaxy Smash cartridge $59.99, a **day-one MEGA exclusive** "Nebula Crash"
   $79.99 (opened), and a **defective** OmegaVision headset $129.99.
3. **Parent → Store Policy Agent:** `search_policy("return window restocking fee
   day-one exclusive defective")` → returns the restocking rule (15% on opened
   hardware, waived if defective), the **non-returnable day-one exclusive** rule,
   and the **store-credit +10% goodwill bonus** rule.
4. **Parent runs `return-eligibility-evaluator`** per line → flags Nebula Crash as
   **non-returnable**, headset as a **defective warranty return** (fee waived),
   the rest as opened-hardware (15% restock).
5. **Parent → Membership MCP:** `get_membership("MEGA-BLAST-1024")` (Plus Extra) →
   then **`prorated-refund-calculator`** computes the membership refund **$76.66**
   (8 unused months × $10.8325 − $10 fee).
6. **Parent runs `bundle-settlement-calculator`** — generates and runs Python over
   all lines with `payout = store_credit`, `tax = 8.75%`, and the membership
   refund folded in:
   - Merchandise net **$733.95**, tax **$64.22**, **+10% store-credit bonus
     $73.40**, membership **$76.66**¹ → **settlement total $948.23 to store
     credit**.
7. **Parent → Inventory & Fulfillment Agent (optional upsell):** customer asks if
   a different console is in stock to exchange into → `check_stock` /
   `find_alternatives` → offers an in-stock alternative or a restock date.
8. **Parent → Associate:** prints the **itemized settlement statement** (every
   line, fees, bonus, membership proration, grand total) and offers to execute the
   returns (`request_return`) and `cancel_membership`.

> ¹ The bundle skill adds the membership refund with **no bonus and no tax**. With
> the Plus Extra refund of $76.66 the store-credit settlement total is **$948.23**
> (vs **$874.83** to original tender, which omits the 10% bonus).

### Why it's a great demo

- **Two skills chain:** the membership refund from one skill becomes an input to
  the bundle settlement skill.
- **Policy actually changes the math:** the day-one exclusive zeroes a line, the
  defective flag waives a fee, and the store-credit choice adds the 10% bonus —
  all sourced from the Policy RAG MCP, not guessed.
- **Every pillar fires:** 3 MCPs, 2 connected agents, runtime Python ×2, file out.

---

## Scenario 3 — The Triple BLAST Weekend Reckoning

*"This member went wild during Triple BLAST Weekend, then returned one big item.
What are their BlastPoints actually worth now?"*

**Showcases:** the `points-reconciliation` skill with promo multipliers, tier
bonus, **clawback on a return**, and expiry — runtime Python — plus a Policy RAG
lookup for the live promo, ending in a loyalty statement file.

**Cast:** Parent · Store Policy Agent (Policy RAG MCP) · Membership MCP ·
Order Management MCP · `points-reconciliation` · loyalty statement file.

### Flow

1. **Associate:** "Member `MEGA-BLAST-2048` is asking how many BlastPoints they
   have after the weekend — and they just returned a console."
2. **Parent → Membership MCP:** `get_membership` → tier = **MEGA** (+50% bonus),
   confirms identity.
3. **Parent → Store Policy Agent:** `search_policy("BlastPoints promo multiplier
   Triple BLAST Weekend expiry")` → confirms **3x base** during the promo, the
   **clawback** rule on returns, and **1,000 pts = $5** redemption.
4. **Parent → Order Management MCP:** `get_order` / `get_return_status` → the
   returned item was a $79.99 day-one purchase made **during** the promo (3x).
5. **Parent runs `points-reconciliation`** — generates and runs Python:
   - prior 4,200; earn $499.99×3×1.5 = **22,500** and $59.99×1×1.5 = **900**;
   - clawback $79.99×3×1.5 = **−3,600**; expired **−800**;
   - **net = 23,200 BlastPoints → 23 × $5 = $115.00 store credit**.
6. **Parent → Associate:** reports the net balance and the redeemable value, shows
   the ledger, and offers to apply the **$115.00** to today's transaction and print
   a **loyalty statement**.

### Expected headline numbers: **23,200 pts → $115.00**

---

## Scenario 4 — The Cross-Town Console Quest

*A defective day-one console needs a warranty swap, but it's out of stock locally.*

**Showcases:** the **Inventory & Fulfillment Agent** as the star — `check_stock`,
`find_alternatives`, `get_restock_date` — combined with a Policy RAG warranty
ruling and the `return-eligibility-evaluator`, ending in an exchange/transfer slip.

**Cast:** Parent · Store Policy Agent (Policy RAG MCP) · Inventory & Fulfillment
Agent (Warehouse MCP) · Order Management MCP · `return-eligibility-evaluator` ·
exchange/transfer slip file.

### Flow

1. **Associate:** "Customer's BlastBox Omega died after 10 days — they want a
   replacement, not a refund."
2. **Parent → Store Policy Agent:** `search_policy("defective hardware warranty
   swap 30 days")` → **defective hardware within 30 days is a free warranty swap,
   not a return** (no restocking fee).
3. **Parent runs `return-eligibility-evaluator`** → decision **ELIGIBLE** as a
   defect/warranty swap, resolution = exchange.
4. **Parent → Inventory & Fulfillment Agent:** `check_stock` for the same SKU →
   **0 locally**. The agent calls `find_alternatives` (offers the MEGA edition as
   an upgrade swap) and `get_restock_date` (e.g. 4 days) and, if asked,
   `get_fulfillment_status` for an inbound transfer.
5. **Parent → Associate (relay):** "Out of stock here. Options: (a) same console
   restocks in **4 days**, (b) ship from a nearby store, or (c) upgrade swap to the
   MEGA edition now. Which one?"
6. **Associate:** picks one → **Parent → Order Management MCP** `request_return`
   (as a defective exchange) and generates the **exchange/transfer slip** with the
   reservation/restock details.

### Why it's a great demo

- The Inventory & Fulfillment Agent carries the conversation, showing a second
  connected agent with its own MCP doing real branching work.
- Policy turns a "return" into a **warranty swap** — the orchestration adapts.

---

## How each demo maps to the four pillars

| Pillar | Scenario 0 | Scenario 1 | Scenario 2 | Scenario 3 | Scenario 4 |
| --- | --- | --- | --- | --- | --- |
| **Connected agents** | — | Policy | Policy + Inventory | Policy | Policy + Inventory |
| **Multiple MCP servers** | Membership | Policy + Membership | Order + Policy + Warehouse + Membership | Policy + Membership + Order | Policy + Warehouse + Order |
| **Skill runs Python at runtime** | membership-card | prorated-refund | bundle-settlement (+ prorated-refund) | points-reconciliation | return-eligibility |
| **File generation** | digital membership card (PNG) | RMA slip | settlement statement | loyalty statement | exchange/transfer slip |
| **Complex multi-turn request** | 2-turn verify→reissue warm-up | tier relay | full bundle + membership + payout choice | promo + clawback + expiry | warranty swap + stock branching |

---

## Running the demos

1. Import `StoreModernAgent-built.zip` into the target environment.
2. Attach the 3 policy PDFs (`policy-docs/`) as **Store Policy Agent** knowledge.
3. Create the inline MCP connectors (`connectors/*-inline/`) and wire them as tools:
   Membership + Order Management on the parent, Policy RAG on the Store Policy
   Agent, Warehouse on the Inventory & Fulfillment Agent. (See
   `connectors/README` notes — create via the maker UI: Import OpenAPI → Code tab →
   enable custom code → upload `script.csx` **after** enabling → Create.)
4. Add the parent skills from `skills/` (paste each `SKILL.md`; the embedded Python
   is the canonical logic — the agent generates/runs it at runtime).
5. Publish, then run the scripted associate prompts above.

> All data is mock. The MCP servers run entirely inside the connector (C# custom
> code) — no external services. The reference Python in `skills/*/` and
> `skill-scripts/` is validated and matches the numbers quoted here.
