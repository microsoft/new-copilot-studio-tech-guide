# BlastBox Omega — repeatable deploy (`deploy/`)

One-shot, idempotent deployment of the BlastBoxDemo solution into a **freshly
minted Early Release environment**. Mints the env, imports the solution, deploys
the connector code, creates the MCP connections, publishes the agents, and
validates the two packaged scenarios e2e (and tears the env down on failure).

After the script finishes there is **one manual UI step** (it has no supported
API): re-attach each agent's MCP server in Copilot Studio. The script prints
exactly what to do at the end of every run — see
[Manual post-install step](#manual-post-install-step-required) below.

> ⚠️ **Use at your own peril.** This automates against live Power Platform /
> Dataverse / BAP admin APIs in the PPlatform tenant. It creates and **deletes**
> environments. Read the script before running it.

## Layout

| File | Role |
| --- | --- |
| `deploy.sh` | Orchestrator. Runs the steps in order; ERR-trap tears down the env on failure. |
| `config.env` | Stable, env-independent identifiers (connector ids + display names, source env, agent schema names, region, timing). |
| `lib/common.sh` | Logging, token helpers (BAP + Dataverse), Dataverse Web API helper, state persistence. |
| `steps/00_preflight.sh` | Asserts tooling, az/pac auth + tenant, solution zip present. |
| `steps/10_env.sh` | Deletes any prior `copilot-adilei*` env (one-at-a-time), mints a fresh Developer (== Early Release) EU env with Dataverse. |
| `steps/20_import.sh` | `pac solution import --publish-changes`. Verifies success **server-side** (the custom-code compile routinely exceeds pac's hard 30-min client timeout while the server job completes), with retry/backoff for the function-app capacity error. |
| `steps/30_connectors.sh` | Downloads each MCP connector from the source env and `pac connector update`s it into the target to **deploy its inline `.csx`** (import alone does not), verifying `modifiedon` advances. |
| `steps/40_connections.sh` | Creates one no-auth connection per MCP connector (BAP REST API). No binding needed — `authMode: Maker` resolves any Connected connection. |
| `steps/50_publish_agents.sh` | `PvaPublish` the 4 bots (children first, then parents). |
| `steps/60_validate.sh` | Drives the two scenarios e2e on the portal preview canvas (playwright-cli) and asserts the README numbers (one-time human MFA). Run **after** the manual step below. |
| `steps/70_manual_steps.sh` | Read-only. Prints the one manual UI step (re-attach each agent's MCP server). Also printed at the end of `deploy.sh`. |
| `steps/99_teardown.sh` | Deletes the env (used by the ERR trap; also runnable standalone). |

## Prerequisites

- `pac` CLI with `DOTNET_ROOT=/Users/administrator/.dotnet` (.NET 10), authed as `EladG@PPlatform`.
- `az` logged into the PPlatform tenant (`az login --tenant 8a235459-3d2c-415d-8c1e-e2fe133509ad`).
- `playwright-cli` for the e2e step (one-time MFA approval — see `~/.copilot/skills/test-copilot-agent`).

## Usage

```bash
deploy/deploy.sh                 # full run
KEEP_ON_FAILURE=1 deploy/deploy.sh   # keep env on failure for debugging
SKIP_ENV=1 deploy/deploy.sh      # reuse env in .deploy-state.env
START_AT=40 deploy/deploy.sh     # resume from a step
```

---

## What works today

A full automated run (steps 10→50) provisions a fresh EU env to the point where the
solution is imported, every connector's inline code is deployed, a Connected no-auth
connection exists per connector, and all four agents are published. **One manual UI
step then remains** (re-attach each agent's MCP server — see below); after it, the
tools load and the end-to-end scenario check (step 60) passes.

- **Env minting + Early Release.** `pac admin create --type Developer --region europe`
  provisions Dataverse and is inherently Early Release (`updateCadence=Frequent`).
  Teardown via `pac admin delete`.
- **Solution import + publish** of the 4 inline custom-code MCP connectors + 4 agents.
  Use the env **URL** (not id) for `pac solution import`. Import verified server-side
  (see blocker #1 below).
- **Connector code deploy** via `pac connector update` (blocker #2).
- **No-auth connections** via the BAP REST API; tools resolve via `authMode: Maker`,
  so no connectionreference binding is required.

## Manual post-install step (required)

Everything above is automated. The one thing the script cannot do — there is no
supported API for it — is finalise each agent's MCP tool wiring, which the modern
Copilot Studio authoring canvas only does when a maker re-attaches the MCP server in
the UI. The connections already exist and the tool definitions are present; this
re-attach is what makes the published agents surface the tools at runtime.

`deploy.sh` prints these instructions at the end of every run; re-print any time with
`bash deploy/steps/70_manual_steps.sh`.

1. Open **Copilot Studio** (https://copilotstudio.microsoft.com/) in the deployed env.
2. For **each** agent below: open it → **Tools** → **remove** the listed MCP server →
   **Add a tool → Model Context Protocol** → pick the connector and select the
   **existing Connected connection** (do not create a new one) → **Save** → **Publish**.

   | Agent | MCP connector to remove + re-add |
   | --- | --- |
   | Store Policy Agent | Policy RAG MCP v2 |
   | Inventory & Fulfillment Agent | Warehouse MCP |
   | Returns & Service Assistant | Order Management MCP, Membership MCP v2 |
   | Store Associate Assistant | (orchestrates the children — republish after the children are re-saved) |

After re-attaching, run `START_AT=60 deploy/deploy.sh` (or `bash deploy/steps/60_validate.sh`)
to confirm both scenarios pass.

## Architecture decision: connectors stay IN the solution

We keep the 4 connectors inside the solution (rather than an agents-only solution +
`pac connector create`) because solution import **preserves each connector's identity**
(the connectorid/apiName the agents' tools reference). `pac connector create` would mint
**new** connector identities, leaving the imported agents' tools pointing at connectors
that don't exist and forcing manual UI re-wiring. With connectors in the solution, the
only thing import misses is compiling the inline code, which `30_connectors.sh` fixes.

## Findings (the "deployment process" problems, resolved)

1. **Import exceeds pac's 30-min client timeout.** Compiling the inline MCP connectors
   routinely trips `The request channel timed out ... 00:30:00`, even though the
   **server-side** import job keeps running and completes. `20_import.sh` therefore polls
   Dataverse for the solution and treats its presence as success instead of trusting
   pac's exit code. (Also: the US custom-code function-app pool is capacity-exhausted —
   `Unable to find an unassigned function app in 'East US'` — so we deploy to **EU**.)

2. **Import does not deploy the connectors' inline code.** Solution import registers each
   custom-code connector but does **not** compile/deploy its `.csx`; the connector's
   `modifiedon` stays equal to `createdon` and tools fail to load. Fix: `pac connector
   update` per connector (`30_connectors.sh`), downloading from the source env.
   **Caveat:** `pac connector update` can report "succesfully" without deploying — the
   step verifies `modifiedon` actually advanced and retries if not.

3. **Tool binding needs no UI step and no guid-matching.** With `authMode: Maker`, the
   runtime resolves any Connected maker connection for the tool's connector. Connections
   whose ids don't match the guids baked in the tool data still load tools, so
   `40_connections.sh` just creates one no-auth connection per connector. The agents'
   `connectorId` keeps the source-env hash and still resolves.

4. **Store Policy's tool was authored in the legacy format.** It shipped as
   `kind: TaskDialog` / `InvokeExternalAgentTaskAction`, which the modern Copilot Studio
   UI does not render as an MCP tool (the other three are `kind: McpTool`). Fixed at the
   source agent (now `kind: McpTool`), so the solution export carries the fix.
