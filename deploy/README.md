# BlastBox Omega — repeatable deploy (`deploy/`)

One-shot, idempotent deployment of the BlastBoxDemo solution into a **freshly
minted Early Release environment**, with **zero manual configuration**. Mints the
env, imports the solution, publishes, creates + binds the MCP connections,
publishes the agents, validates the two packaged scenarios e2e, and tears the
env down on failure.

> ⚠️ **Use at your own peril.** This automates against live Power Platform /
> Dataverse / BAP admin APIs in the PPlatform tenant. It creates and **deletes**
> environments. Read the script before running it.

## Layout

| File | Role |
| --- | --- |
| `deploy.sh` | Orchestrator. Runs the steps in order; ERR-trap tears down the env on failure. |
| `config.env` | Stable, env-independent identifiers (connector display names, agent schema names, region, timing). |
| `lib/common.sh` | Logging, token helpers (BAP + Dataverse), Dataverse Web API helper, state persistence. |
| `steps/00_preflight.sh` | Asserts tooling, az/pac auth + tenant, solution zip present. |
| `steps/10_env.sh` | Deletes any prior `copilot-adilei*` env (one-at-a-time), mints a fresh Developer (== Early Release) env with Dataverse. |
| `steps/20_import.sh` | `pac solution import --publish-changes`, with retry/backoff for the function-app capacity error. |
| `steps/40_connections.sh` | Creates one no-auth connection per MCP connector and binds the connectionreferences. |
| `steps/50_publish_agents.sh` | `PvaPublish` the 4 bots (children first, then parents). |
| `steps/60_validate.sh` | Drives the two scenarios e2e on the portal preview canvas (playwright-cli) and asserts the README numbers. |
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

- **Env minting + Early Release.** `pac admin create --type Developer --region <r>`
  provisions Dataverse and is inherently Early Release (`updateCadence=Frequent`).
  No cadence flag needed. Teardown via `pac admin delete`.
- **Solution import + publish** of the 4 inline custom-code MCP connectors + 4 agents.
  Use the env **URL** (not id) for `pac solution import --environment`.
- Connector apiName resolution, no-auth connection creation, PvaPublish.

## Known blockers / findings (the "deployment process" problems)

These are why a fully clean e2e is **not yet** achievable from the current
`BlastBoxDemo_1_0_0_1.zip`:

1. **US custom-code compute pool is capacity-exhausted.** Importing into a US
   (`unitedstates`) env fails publish with
   `CustomScriptProvisioningFailed … Unable to find an unassigned function app in 'East US'`.
   This is Microsoft-side pool capacity for *new* inline-MCP provisioning (existing
   envs keep their already-assigned function apps). **Workaround: deploy to an EU
   (`europe`) env** — West Europe pool has capacity; import + publish succeed there.

2. **The packaged solution's agent MCP tool bindings are non-portable.** Each MCP
   tool's `connectorId` / `connectionReference` is hardcoded to the **source env's**
   connector hash (`-5fee08b8354fad177a`). A fresh import gets a new env-specific
   hash (e.g. `-5f10ac44513d214b06`), so the bindings dangle. Import does **not**
   auto-create the MCP connectionreferences. The supported portable pattern is to
   ship connection references as solution components + an import `--settings-file`
   that maps each to a per-env connection; this solution deliberately ships **zero**
   connection references, so binding must be reconstructed post-import.

3. **~~The packaged solution is incomplete.~~ — FIXED in `BlastBoxDemo_1_0_0_2.zip`.**
   The original `1_0_0_1` zip shipped the Store Associate Assistant (Block Party
   flagship) with only 2 connected-agent tools + 3 skills — its **Order Management
   MCP and Membership MCP v2 tools were missing**, so Block Party could not complete.
   The package was **rebuilt from the source env `org5d9d4b6b`** as a new solution
   (`BlastBoxDeploy`) containing all 4 bots, **all 14 botcomponents** (Store Associate
   now has both MCP tools), and the 4 connectors — including the **`Policy RAG MCP v2`**
   connector the Store Policy agent actually references. The stale duplicate
   `…PolicyRAGMCPServer_PFO` tool (cause of "Tool call · unknown") was excluded.
   Proven: imports + publishes cleanly into a fresh EU env, with both Store Associate
   MCP tools present post-import.

**Remaining work (blocker #2):** the rebuilt package deliberately ships **zero
connection references** (portable by convention), so step 40 must, per env: create a
no-auth connection per connector, create the `connectionreference` record with the
exact logical name each tool's `data` expects, recreate the `botcomponent_connectionreference`
binding, and rewrite each tool's `connectorId` to the fresh env hash (source
`-5fee08b8354fad177a` → e.g. `-5f10ac44513d214b06`). Steps 10/20/50/60/99 are already
correct.
