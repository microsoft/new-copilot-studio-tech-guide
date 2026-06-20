# Flagship Runbook — "The Block Party Trade-Up" (verified live run)

> Companion to [`flagship-block-party-trade-up.md`](./flagship-block-party-trade-up.md).
> That file is the **scripted scenario**; this file is the **verified replay runbook** —
> the exact associate prompts to paste turn-by-turn, the tool calls that fired on the
> live Copilot Studio canvas, and the actuals observed. Use it to reproduce the demo.

- **Agent:** Store Associate Assistant (`a24a4576-75d1-4a92-9557-31f48ab17d6e`)
- **Environment:** Elad Gal's Env (`6cc0c98e-3fe6-e0d5-8eba-ba51c9da1d13`)
- **Preview canvas:** `https://copilotstudio.preview.microsoft.com/environments/6cc0c98e-3fe6-e0d5-8eba-ba51c9da1d13/agents/a24a4576-75d1-4a92-9557-31f48ab17d6e/preview`
- **Auth:** **Maker** on both parent MCP tools (Membership MCP v2, Order Management MCP) —
  zero consent prompts. Verified working end-to-end.
- **Last verified:** 2026-06-19, full 4-turn run on the live canvas.

---

## How to replay (one turn at a time)

Open the preview canvas, click **New chat**, then paste each prompt below in order and
wait for the agent to finish before sending the next. The agent asks **one question**
mid-flow that you answer with a follow-up prompt: the defect cause (T1 → T2). Everything
else — including the BlastPoints balance — comes from the MCP tools.

> ℹ️ **BlastPoints balance** is now part of the membership record: `get_membership`
> returns `blast_points` (Jordan = **4,200**), and the agent feeds it straight into
> `points-reconciliation` → `21,400 pts → $105.00`. The agent does **not** ask the
> associate for it. (Earlier runs asked because the fixture had no points field; fixed
> by adding `blast_points` to the Membership MCP v2 connector.)

---

## Turn 1 — the request → defect-cause question

**Paste:**

> Customer's running a gaming-league block party this weekend and his main console just
> died about 10 days after he bought it — he's got the receipt. He wants a replacement if
> one's in stock, otherwise a refund, and while he's here he wants to cancel his BlastPass,
> member ID MEGA-BLAST-1024. Walk me through all of it.

**Tools that fire (verified):** Store Policy Agent (`search_policy`) · `get_membership`
· Inventory & Fulfillment Agent (`check_stock` / `find_alternatives`).

**Verified result:** agent gathers policy + membership + inventory in parallel, then
asks the **defect-cause question** (manufacturing fault vs. accidental/physical/liquid).
Reports base **BlastBox Omega out of stock**, **MEGA Edition 5 in stock @ $499.99**,
BlastPass Plus Extra (8 of 12 months, $10 fee, cooling-off expired). Nothing changed yet.

---

## Turn 2 — answer the defect question → ruling + math

**Paste:**

> Manufacturing fault — just won't power on, no physical or liquid damage.

**Tools that fire (verified):** Store Policy Agent (ruling) · **`prorated-refund-calculator`**
skill (Python).

**Verified actuals:**
- Manufacturing-defect ruling → **free warranty swap, no restocking fee**.
- BlastPass proration: `$129.99 ÷ 12 = $10.8325 × 8 = $86.66 − $10` = **$76.66**.
- Upgrade difference `$499.99 − $399.99` = **$100.00**.
- Sweet-deal settlement `$100.00 − $76.66 (store credit)` = **Jordan pays $23.34**.
- Bonus: refund-as-store-credit path offers 10% goodwill → `$399.99 → $439.99`.

---

## Turn 3 — "he can't decide" → the MEGA exclusives close it

**Paste:**

> He's torn — he likes the idea of the MEGA Edition but he's not sure it's worth the extra
> hundred bucks over just waiting the four days for the base console to come back in stock.
> Give me something I can tell him to help him decide.

**Tools that fire (verified):** Inventory & Fulfillment Agent (`get_console_exclusives`).

**Verified result:** three MEGA-only AAA titles, each with a ready-to-say pitch:
- 🦎 **MEGA Lizards from Outer Space**
- 💼 **Galactic Tax Evader VII: Audit Protocol**
- 🧶 **Mecha-Granny: Knitpocalypse**

Reframes the upgrade as **$23.34 out of pocket, today**, vs. 4 days' wait + no exclusives.

---

## Turn 4 — execute → settle → loyalty → PDF

**Paste:**

> Do it: warranty-swap him into the MEGA Edition, cancel the BlastPass and apply the $76.66
> as store credit toward the upgrade, and tell me what he owes. Also — after Triple BLAST
> Weekend, what are his BlastPoints worth, in case he wants to knock down the balance? Then
> print me the slip as a PDF.

**Tools that fire (verified):** `cancel_membership` · `search_orders` · `get_order` ·
**`request_return`** · **`points-reconciliation`** skill · **`slip-pdf-generator`** skill.

> ✅ **No associate question.** The agent reads Jordan's `blast_points` balance (4,200)
> straight off the `get_membership` record from Turn 1 and feeds it into
> `points-reconciliation` automatically — it does **not** ask the associate for the balance.

**Verified actuals:**
- **`request_return`** authorized → RMA (e.g. `RA-5002x`), **$399.99** warranty credit, no fee.
- **`cancel_membership`** → confirmation (e.g. `BPX-CXL-1024-…`), $76.66 as store credit.
- Settlement: MEGA `$499.99` − warranty `$399.99` − BlastPass credit `$76.66`
  = **Jordan pays $23.34**.
- BlastPoints ledger (prior **4,200** from `get_membership`): `+18,000` earned (MEGA @
  Triple BLAST 3× + Plus Extra tier) `− 800` expired = **21,400 pts → $105.00** store credit.
- **Settlement slip PDF** generated (e.g. `/tmp/blastbox_slip_jordan_pixel.pdf`).

> Confirmation numbers (RMA / cancellation IDs) and the PDF path are generated per run and
> will differ; the **headline money/points are deterministic**: $76.66 · $100.00 ·
> **$23.34** · **21,400 pts → $105.00**.

---

## Fix history (what made this work under Maker auth)

This run is the payoff after debugging two issues that blocked the Maker-auth demo:

1. **Order MCP "placeholder.azure-apim.net" 500 / DNS failure.**
   Custom-code MCP connectors store `host: placeholder.azure-apim.net` and
   `scriptOperations: []` in Dataverse by design — APIM is supposed to execute the
   connector's `customcodeblobcontent` at runtime. When the active connector revision's
   code binding goes stale, APIM instead proxies to the literal placeholder host →
   `code 500 "The remote name could not be resolved: 'placeholder.azure-apim.net'"`.
   **Fix:** re-deploy the connector code (`pac connector update --script-file …`) to
   re-bind the active revision. After re-deploy, `search_orders`/`get_order`/`request_return`
   all execute and return live data.

2. **`request_return` skipped at Turn 4.**
   Not a prompt problem — the Order MCP was failing (issue #1) when T4 ran, so the agent
   settled the swap narratively and moved on. **Once the connector executes under Maker,
   T4 calls `request_return` correctly** (verified: RMA authorized, $399.99 credit).

3. **Agent asked the associate for the BlastPoints balance at Turn 4.**
   The Membership MCP v2 fixture had no loyalty balance, so the agent had nowhere to fetch
   it and fell back to asking — which an associate wouldn't know. **Fix:** added a
   `blast_points` field to each membership record and surfaced it in the `get_membership`
   response (Jordan = 4,200), plus an instruction *"never ask the associate for the points
   balance, it is on the membership record."* Now `get_membership` returns the balance and
   the agent feeds it to `points-reconciliation` directly — no question.

**Diagnostic tell:** orchestrator-level rejects fast-fail ~125 ms ("Tool call · unknown");
a call that reaches the connector takes ~1.6–2.2 s. Latency distinguishes a binding/auth
reject from a connector-internal error.

**Status:** both parent MCP tools (Membership MCP v2, Order Management MCP) run under
**Maker** auth with **no consent prompts**. A spare **Order Management MCP v2** connector
(`8320a58c-db6b-f111-ab0f-0022480a52c7`) was created during debugging but is **not bound**
to the agent and is **not needed** — v1 works under Maker. It can be deleted.
