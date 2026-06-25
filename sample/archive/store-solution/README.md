# BlastBox Omega — Modern Agent Experience demo (two scenarios)

A clean, importable Microsoft Copilot Studio demo built on the **Modern Agent
Experience** (the new default agent experience, formerly "Enhanced Task
Completion"). It packages exactly **two tested scenarios**:

1. 🟢 **Self‑Serve BlastPass Card Reissue** (warm‑up) — one MCP + one skill + a
   generated file.
2. 🟣 **Block Party Trade‑Up** (flagship) — a parent agent orchestrating two
   connected agents, multiple inline MCP servers, runtime‑Python skills, and PDF
   file generation.

Everything is mock retail data for a fictional retro‑gaming store, **BlastBox
Omega**, and its **BlastPass** membership program.

> This is a demo solution exported from a working dev
> environment, not a supported product. Connector identities, MCP servers, and
> mock data are environment‑specific — read the **Known caveats** section before
> importing, especially the **Membership MCP v2** connector note.

---

## Verification status

| Step | Status |
| --- | --- |
| Solution builds & exports cleanly (`pac solution export`) | ✅ verified |
| Package contents (4 agents, 5 connectors, 5 skills, no stray agents) | ✅ verified |
| Imports + publishes into a **separate** environment (`org291aa086`) | ✅ verified |
| Live canvas end‑to‑end test of both scenarios | ⏳ **pending** — requires an interactive Microsoft sign‑in (MFA) that must be approved by a human; run the prompts in **§5** after import |
| Membership tool portability (**v2** connector) | ⚠️ see **Known caveats** |

---

## What's in the box

| Component | Kind | What it does |
| --- | --- | --- |
| **Store Associate Assistant** | Parent agent (flagship) | Orchestrates the Block Party Trade‑Up; owns the refund / points / slip skills; delegates to the connected agents. |
| **Returns & Service Assistant** | Parent agent (warm‑up) | Self‑serve card reissue; owns the membership‑card skill. |
| **Store Policy Agent** | Connected agent | Return / tier policy via **Policy RAG MCP**; grounded by BlastPass policy PDFs. |
| **Inventory & Fullfilment Agent** | Connected agent | Stock, alternatives, restock dates via **Warehouse MCP**. |
| **Skills** | Generated‑Python | `membership-card-png`, `card-reissue`, `prorated-refund-calculator`, `points-reconciliation`, `slip-pdf-generator`. |
| **MCP connectors** | Inline custom code | Membership MCP, Order Management MCP, Policy RAG MCP, Policy RAG MCP v2, Warehouse MCP — each an inline C# MCP server with mock data. |

The packaged solution is **`BlastBoxDemo.zip`** (unmanaged). The unpacked source
lives in **`src/`**, the skill source in **`skills/`**, and the demo policy PDFs
in **`policy-docs/`**. Scripted walkthroughs and exact expected numbers are in
[`SCENARIOS.md`](./SCENARIOS.md) and [`evals/`](./evals).

---

## Prerequisites

- A Power Platform environment with **Microsoft Copilot Studio** enabled and
  **Dataverse** provisioned.
- Permission to import solutions and create connections (System Customizer or
  Environment Maker).
- The environment must allow agents that use **MCP** tools / custom connectors
  with custom code.
- For the CLI path: `pac` CLI (`DOTNET_ROOT` pointing at a .NET 10 runtime).

---

## 1. Import the solution

UI: **[make.powerapps.com](https://make.powerapps.com)** → pick your target
environment → **Solutions → Import solution → Browse** → choose
`BlastBoxDemo.zip` → **Next → Import**.

CLI:

```bash
pac auth create --environment <TARGET_ENV_URL>      # or: pac auth select --index <n>
pac solution import --path ./BlastBoxDemo.zip --async false
```

This brings in the **4 agents**, the **5 MCP connectors**, and the skills.
(Verified: imports and publishes cleanly into a fresh/separate environment.)

---

## 2. Create the MCP connections

The connectors import with their connections **off**. Create a
**No‑authentication** connection for each (make.powerapps.com → **Connections →
+ New connection**, search the connector name, **Create** — no secrets, no auth):

- **Membership MCP**
- **Order Management MCP**
- **Policy RAG MCP** (and/or **Policy RAG MCP v2**)
- **Warehouse MCP**

---

## 3. Bind / re‑add the MCP tools on each agent

A native Copilot Studio MCP tool stores an **environment‑specific connector id**
inside the agent. Solution import does **not** remap that id (and this solution
intentionally ships **without** connection references — they are environment
specific). So after import, open each agent and re‑bind its tool to the local
**No‑auth** connection from §2:

| Agent | Tool(s) to bind |
| --- | --- |
| **Store Associate Assistant** | **Order Management MCP** Server, **Membership MCP** Server |
| **Returns & Service Assistant** | **Membership MCP** Server |
| **Store Policy Agent** | **Policy RAG MCP** Server |
| **Inventory & Fullfilment Agent** | **Warehouse MCP** Server |

For each: open the agent → **Tools** → on the MCP tool pick **+ Add a tool →
Model Context Protocol**, search the connector name, choose the
**`<Name> MCP Server`** tile, select the **No‑auth** connection from §2 → **Add**
→ **Save**. The agents' instructions already describe the tools, so they light up
as soon as the tool is bound.

---

## 4. Publish the agents (children first)

Publish **connected agents first**, then the parents, so delegation resolves:

1. **Store Policy Agent** → **Publish**
2. **Inventory & Fullfilment Agent** → **Publish**
3. **Returns & Service Assistant** (parent) → **Publish**
4. **Store Associate Assistant** (parent) → **Publish**

If a connected‑agent or tool chip looks empty after import, open the agent,
re‑select the connection on the tool, **Save**, then **Publish**.

---

## 5. Test the scenarios

Open an agent and click **Test** / **Preview**, then paste a prompt. (These are
the e2e checks that are still **pending** an interactive sign‑in — run them after
import.)

### 🟢 Warm‑up — Self‑Serve BlastPass Card Reissue
> **Agent:** Returns & Service Assistant
>
> *"I lost my BlastPass card — can you reissue it for member `MEGA-BLAST-2048`?"*

Verifies identity via **Membership MCP**, calls `reissue_card` (deactivates the
old serial, mints a new one), then runs the `membership-card-png` skill to render
a tier‑colored **CR80 card PNG** showing the **new** serial.

### 🟣 Flagship — Block Party Trade‑Up
> **Agent:** Store Associate Assistant
>
> *"A customer wants to trade up their BlastBox Omega to the MEGA edition for the
> Block Party event. Member `MEGA-BLAST-1024`. What's the settlement, and can we
> do the swap today?"*

Relays the membership tier, pulls policy from the **Store Policy Agent**, checks
stock via the **Inventory & Fullfilment Agent**, runs the refund / points Python
skills, and generates a printable **RMA slip PDF**.

**Expected headline numbers:** prorated refund **$76.66**, net due **$23.34**,
BlastPoints **21,400 → $105**, return auth **RA‑50022**, plus a generated slip
PDF.

---

## Known caveats

- ⚠️ **Membership MCP "v2" is not portable.** Both parent agents' membership tool
  (`MembershipMCPv2`) points at a connector named **Membership MCP v2**
  (`shared_membership-20mcp-20v2-…`). In the source env this connector exists only
  in the **PowerApps connectivity layer** — it has **no Dataverse `connector`
  record**, so it **cannot be packaged into the solution** (and `pac connector
  list` / Dataverse queries won't show it). The solution therefore ships the v1
  **Membership MCP** (`cat_membership-20mcp`) connector. In a new environment you
  must do **one** of:
  1. **Repoint** each `MembershipMCPv2` tool to the packaged **Membership MCP**
     (v1) connector in the designer (re‑add the tool, pick the v1 `Membership MCP
     Server` tile), **Save**, **Publish** — recommended for portability; or
  2. **Recreate** a *Membership MCP v2* custom connector in the target env before
     import.
- **Connection references are intentionally omitted** from the package. They are
  environment‑specific; binding them per env in §3 avoids stale/orphaned
  references. (During export, orphaned source connection references that pointed
  at a deleted connector were the reason a straight export failed; the clean
  export carries connectors + agents + skills only.)
- **`pac connector list` ≠ the full connector set.** It lists only Dataverse
  `connector` rows. Connectivity‑layer connectors are visible only via the
  PowerApps `…/providers/Microsoft.PowerApps/apis` endpoint.

---

## Maintainers — regenerating the package

The solution was built in the **source env** by creating an empty solution and
adding the two parent agents (Dataverse pulls in the connected agents, skills,
and connectors), then exporting and unpacking:

```bash
export DOTNET_ROOT=/path/to/dotnet            # .NET 10 runtime
pac auth select --index <source-env>

# add the two parents (componentType 'bot'); deps pull the rest
pac solution add-solution-component --solutionUniqueName BlastBoxDemo \
  --component <StoreAssociateAssistant-id> --componentType bot --AddRequiredComponents true
pac solution add-solution-component --solutionUniqueName BlastBoxDemo \
  --component <ReturnsServiceAssistant-id> --componentType bot --AddRequiredComponents true

# add each custom connector by GUID (componentType 372)
pac solution add-solution-component --solutionUniqueName BlastBoxDemo \
  --component <connector-guid> --componentType 372 --AddRequiredComponents true

pac solution export --name BlastBoxDemo --managed false --path ./BlastBoxDemo.zip --overwrite
pac solution unpack --zipfile ./BlastBoxDemo.zip --folder ./src --packagetype Unmanaged --allowDelete true
```

Notes for regenerating:
- `--AddRequiredComponents` pulls a bot's connection **references** but **not** the
  underlying custom connectors — add those separately (componentType **372**).
- If export fails on a connection reference whose connector can't be added (e.g.
  the v2 connector above, or an orphan pointing at a deleted connector), delete
  the offending connection reference, then re‑export. Re‑bind connections per env
  at import time (§2–3).
