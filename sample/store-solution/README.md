# BlastBox Omega — Modern Agent Experience sample

A complete, importable Microsoft Copilot Studio demo built on the **Modern Agent
Experience** (the new default agent experience, formerly "Enhanced Task
Completion"). One **parent agent** orchestrates **connected agents**, **multiple
inline MCP servers**, **skills that generate and run Python at runtime**, and
**file generation** — all running inside the Power Platform, with no external
servers to host.

Everything is mock retail data for a fictional retro‑gaming store, **BlastBox
Omega**, and its **BlastPass** membership program.

> **Verified:** `StoreModernAgent.zip` imports and publishes cleanly into a fresh
> Power Platform environment. After import you create a few **No‑auth**
> connections and re‑add three MCP tools (the connectors ship inside the
> solution) — full steps below.

---

## What's in the box

| Component | Kind | What it does |
| --- | --- | --- |
| **Returns & Service Assistant** | Parent agent | Orchestrates everything; owns the skills; relays questions to/from the associate. |
| **Store Policy Agent** | Connected agent | Looks up return/markdown policy via **Policy RAG MCP**; grounded by BlastPass policy PDFs. |
| **Inventory & Fulfillment Agent** | Connected agent | Checks stock, alternatives, and restock dates via **Warehouse MCP**. |
| **Sales & Performance Agent** | Connected agent | Sell‑through and catalog analytics via **Sales & Performance MCP**. |
| **Merch Insights Assistant** | Parent agent | Markdown‑review parent for the store‑manager scenario. |
| **Skills** | Generated‑Python | `membership-card-pdf`, `prorated-refund-calculator`, `bundle-settlement-calculator`, `slip-pdf-generator`, `sales-analysis-chart`, `markdown-optimizer`, `merch-report-pdf`, and more. |
| **MCP connectors** | Inline custom code | Membership, Policy RAG, Warehouse, Sales & Performance, Order Management — each an inline C# MCP server (`script.csx`) with mock data. |

The packaged solution is **`StoreModernAgent.zip`** (unmanaged). The unpacked
source lives in **`src/`**, the connector source in
**`../connectors/*-inline/`**, the skill source in **`skills/`**, and the demo
policy PDFs in **`policy-docs/`**.

---

## Prerequisites

- A Power Platform environment with **Microsoft Copilot Studio** enabled and
  **Dataverse** provisioned.
- Permission to import solutions and create connections (System Customizer or
  Environment Maker).
- The environment must allow agents that use **MCP** tools / custom connectors
  with custom code.

---

## 1. Import the solution

1. Go to **[make.powerapps.com](https://make.powerapps.com)** and select your
   target environment (top‑right environment picker).
2. **Solutions → Import solution → Browse**, and choose
   `StoreModernAgent.zip` from this folder.
3. Click **Next**, then **Import**. The import runs for a few minutes and brings
   in all **5 agents**, all **6 MCP connectors**, and the skills.

You can also import from the CLI:

```bash
pac solution import --path ./StoreModernAgent.zip --publish-changes
```

---

## 2. Create the MCP connections

The connectors import with their connections **off**. Create a
**No‑authentication** connection for each (Copilot Studio → an agent →
**Tools**, or make.powerapps.com → **Connections → + New connection**, search the
connector name):

- **Membership MCP**
- **Policy RAG MCP**
- **Warehouse MCP**
- **Sales & Performance MCP**
- **Order Management MCP**

Each is an inline MCP server (no secrets, no auth) — just click **Create**.

---

## 3. Add the MCP tools to the agents

**The connectors themselves are already imported**, so this is a quick tool‑add,
not a connector rebuild. Add each MCP tool to its agent once after import:

| Agent | Add this tool |
| --- | --- |
| **Returns & Service Assistant** | **Membership MCP** Server |
| **Store Policy Agent** | **Policy RAG MCP v2** Server |
| **Inventory & Fulfillment Agent** | **Warehouse MCP** Server |
| **Sales & Performance Agent** | **Sales & Performance MCP** Server |

For each: open the agent → **Tools → + Add a tool → Model Context Protocol** →
search the connector name and press **Enter** → pick the **`<Name> MCP Server`**
tile → select/create the **No‑auth** connection from step 2 → **Add** → **Save**.

> Why this step exists: a native Copilot Studio MCP tool stores an
> **environment‑specific connector id** inside the agent. Solution import remaps
> the connection *reference* but not that connector id, so a freshly imported tool
> still points at the source environment and the agent won't see it at runtime.
> Re‑adding the tool in the designer writes the correct local connector id and
> binds the connection in one step. The agents' instructions already describe the
> tools, so they light up as soon as the tool is added.

---

## 4. Publish the agents

Publish **child agents first**, then the parents, so delegation resolves:

1. **Store Policy Agent** → **Publish**
2. **Inventory & Fulfillment Agent** → **Publish**
3. **Sales & Performance Agent** → **Publish**
4. **Returns & Service Assistant** (parent) → **Publish**
5. **Merch Insights Assistant** (parent) → **Publish**

If a connected‑agent or tool chip looks empty after import, open the agent,
re‑select the connection on the tool, **Save**, then **Publish**.

---

## 5. Try the scenarios

Open an agent and click **Test** (or **Preview**). Copy‑paste a prompt below.
Full scripted walkthroughs and the exact expected numbers are in
[`SCENARIOS.md`](./SCENARIOS.md) and [`evals/`](./evals).

### 🟢 Warm‑up — BlastPass card reprint
> **Agent:** Returns & Service Assistant
>
> *"A customer lost their BlastPass card — can you print a new one for member
> `MEGA-BLAST-2048`?"*

Looks up the member via **Membership MCP** and runs the `membership-card-pdf`
skill to generate a tier‑colored **`blastpass_card.pdf`**. Works as soon as the
Membership MCP connection is created (step 2).

### 🟣 Store associate — the MEGA bundle meltdown
> **Agent:** Returns & Service Assistant
>
> *"A customer with a defective BlastBox Omega wants to return it and upgrade to
> the MEGA edition. Member `MEGA-BLAST-1024`. What's the settlement and can we do
> the swap today?"*

Relays the membership‑tier question, pulls policy from the **Store Policy Agent**,
checks stock/upgrade availability via the **Inventory & Fulfillment Agent**, runs
the refund/settlement Python skills, and generates a printable **RMA slip**.
Headline numbers: refund **$76.66**, upgrade credit **$100.00**, net **$23.34**.

### 🟠 Store manager — end‑of‑quarter markdown review
> **Agent:** Merch Insights Assistant
>
> *"It's end of quarter — review our slow movers and recommend markdowns within
> policy, then generate the report."*

Delegates to the **Sales & Performance Agent**, pulls promo guardrails from the
**Store Policy Agent**, runs `sales-analysis-chart` + `markdown-optimizer`, and
produces **`merch_review.pdf`** with charts and a recommendation table.

---

## Troubleshooting

- **A connected‑agent or skill chip is empty after import.** Open the parent
  agent, re‑add the connected agent / skill, **Save**, then **Publish**. Copilot
  Studio sometimes needs the parent re‑saved once the children are published.
- **An MCP tool shows a connection error, or the agent doesn't call a tool it
  should.** Re‑add the tool from step 3 (open the agent → **Tools → + Add a
  tool**, pick the **`<Name> MCP Server`** tile, select the **No‑auth**
  connection), **Save**, then **Publish**. Re‑adding rewrites the
  environment‑specific connector id that solution import can't remap.
- **A tool's runtime tool‑list looks short.** Click **Refresh connector** on the
  tool, **Save**, and **Publish**.

---

## How it maps to the Modern Agent Experience

| Pillar | Where you see it |
| --- | --- |
| **Connected agents** | Returns & Service Assistant → Store Policy + Inventory; Merch Insights → Sales & Performance + Store Policy. |
| **Multiple MCP servers** | Five inline MCP connectors, each with mock retail data. |
| **Skills (runtime Python)** | Markdown skills that generate and run Python (`reportlab`, `matplotlib`) in the agent sandbox. |
| **File generation** | Membership card, RMA slip, settlement statement, markdown report — all produced as PDFs. |

Everything here is fictional mock data for demonstration only.
