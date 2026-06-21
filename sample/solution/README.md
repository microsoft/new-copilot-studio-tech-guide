# BlastBox Omega — Demo Solution

This folder contains the **portable Copilot Studio solution** behind the BlastBox Omega
demo, plus everything you need to import and run it in your own environment.

| File / folder | What it is |
| --- | --- |
| `BlastBoxDemo_1_0_0_4.zip` | The **unmanaged solution** — import this. |
| `src/` | The **unpacked** solution source (via `pac solution unpack`), for inspection/diffing. |
| `README.md` | This import guide. |

> **v1.0.0.4.** Rebuilt from the source environment so the package is complete and
> portable. Fixes vs `1_0_0_1`: (1) the **Store Associate Assistant** now ships its
> **Order Management MCP + Membership MCP v2** tools (they were missing before, so Block
> Party Trade-Up could not complete); (2) **Store Policy Agent** ships the **Policy RAG
> MCP v2** connector it actually references (the duplicate non-v2 `…PFO` tool that caused
> "Tool call · unknown" was excluded); (3) the Store Policy tool was converted from the
> legacy `TaskDialog` format to `McpTool` so it renders in the modern UI; (4) the package
> carries **no connection references** — connections are created per environment and
> resolve via `authMode: Maker`, so no binding step is needed. For a fully scripted
> deploy, use `deploy/` instead of these manual steps.

## What's inside

**Connectors (4 inline MCP servers — custom code, no external hosting):**

| Connector | Schema name |
| --- | --- |
| Membership MCP v2 | `cat_membership-20mcp-20v2` |
| Order Management MCP | `new_order-20management-20mcp` |
| Policy RAG MCP v2 | `new_policy-20rag-20mcp-20v2` |
| Warehouse MCP | `new_warehouse-20mcp` |

**Agents (4 bots):**

| Agent | Role | Tools / connected agents |
| --- | --- | --- |
| **Store Associate Assistant** (`cr26e_storeassociateassistant_ZQHUx1`) | Parent — **flagship** (Block Party Trade-Up) | Order Management MCP + Membership MCP v2; connected: Store Policy, Inventory & Fulfillment; skills: prorated-refund-calculator, points-reconciliation, slip-pdf-generator |
| **Returns & Service Assistant** (`Default_draft_IsBewO`) | Parent — **self-serve** (Card Reissue) | Membership MCP v2; connected: Store Policy; skills: card-reissue, membership-card-png |
| **Store Policy Agent** (`Default_StorePolicyAgent_s9s-u8`) | Connected child | Policy RAG MCP v2 |
| **Inventory & Fulfillment Agent** (`Default_InventoryFullfilmentAgent_X-w2GP`) | Connected child | Warehouse MCP |

**Agent → MCP map**

- **Store Associate Assistant** → Order Management MCP + Membership MCP v2
  (+ connected: Store Policy → Policy RAG v2, Inventory & Fulfillment → Warehouse)
- **Returns & Service Assistant** → Membership MCP v2 (+ connected: Store Policy → Policy RAG v2)
- **Store Policy Agent** → Policy RAG MCP v2
- **Inventory & Fulfillment Agent** → Warehouse MCP

## Prerequisites

- A Power Platform environment with **Dataverse** and **Copilot Studio** enabled.
- Permission to import solutions and create connections in that environment.

## Import steps

### 1. Import the solution

In the [Power Apps maker portal](https://make.powerapps.com) → **Solutions** → **Import
solution** → upload `BlastBoxDemo_1_0_0_4.zip` → **Next** → **Import**.

### 2. Deploy the connectors' inline code

The four MCP connectors are *inline custom-code* connectors. Importing the solution
**registers** them but does **not** compile/deploy their code, so their tools will not
load yet. Deploy the code for each connector (this is the step the scripted `deploy/`
pipeline automates via `pac connector update`):

```bash
pac connector download --connector-id <id> --environment <SOURCE_URL> --outputDirectory ./c
pac connector update   --connector-id <id> --environment <TARGET_URL> \
  --api-definition-file ./c/apiDefinition.json \
  --api-properties-file ./c/apiProperties.json \
  --script-file ./c/script.csx
```

> `pac connector update` can report "succesfully" without actually deploying — verify the
> connector's `modifiedon` advanced past its `createdon`, and re-run if it didn't. Allow
> ~30–60 seconds for APIM propagation. **Until the code is deployed, MCP tools will not
> load** and agents report "we couldn't find the resource you requested."

### 3. Create the MCP connections

Each connector needs a **connection** before an agent can call it. In **Copilot Studio**,
open each agent's **Tools**, or go to **Power Apps → Connections → New connection**, and
create one connection for each of the four connectors:

- Membership MCP v2
- Order Management MCP
- Policy RAG MCP v2
- Warehouse MCP

> No manual binding is needed: the tools use `authMode: Maker`, so the runtime resolves
> the maker's connection for each connector automatically once it exists.

### 4. Publish the agents

Publish **all four** agents so the connected-agent graph is live:

- Store Associate Assistant (parent)
- Returns & Service Assistant (parent)
- Store Policy Agent (connected child)
- Inventory & Fulfillment Agent (connected child)

> Connected (child) agents must be **published** for the parents to delegate to them.

### 5. Validate — run the two scenarios

Open each parent agent in the **Test** pane and run the scripted transcripts:

**Self-Serve Card Reissue** (Returns & Service Assistant) — member `MEGA-BLAST-1024`:
1. "Hi — I lost my BlastPass card. Can you send me a new one?"
2. "Sure, it's MEGA-BLAST-1024."
3. "It's Jordan Pixel, and the last 4 are 1024."

Expect: `get_membership` → identity check → `reissue_card` → `membership-card-png` →
`blastpass_card.png`.

**Block Party Trade-Up** (Store Associate Assistant) — member `MEGA-BLAST-1024`:
1. Dead console + cancel BlastPass request → defect-cause question (Store Policy Agent).
2. "Manufacturing fault…" → ruling + stock check (Inventory) + prorated refund **$76.66**.
3. "He's torn…" → `get_console_exclusives` closes the upsell.
4. "Do it…" → `request_return`, `cancel_membership`, points reconciliation
   (**21,400 pts → $105.00**), `slip-pdf-generator` → `blastbox_slip.pdf`.
   Net due **$23.34**.

## Notes

- All data is mock; serials, prices, and confirmation numbers are illustrative.
- `src/` is the `pac solution unpack` output of the same zip — use it to diff or inspect
  the connector code and agent definitions; you do **not** import `src/` directly.
