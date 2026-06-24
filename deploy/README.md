# BlastBox Omega — repeatable deploy (`deploy/`)

One cross-platform Node script (`deploy.mjs`) that deploys the BlastBox solution
into an **existing** Power Platform environment. It imports the solution, deploys
each MCP connector's inline custom code, creates the no-auth MCP connections,
publishes the agents, and prints the one manual UI step that remains.

It does **not** create or delete environments. You point it at an env you already
have, either interactively or with `--env-id` / `--env-url`.

> ⚠️ **Use at your own peril.** This automates against live Power Platform,
> Dataverse and BAP APIs (including undocumented BAP endpoints). Read the script
> before running it.
>
> The env-lifecycle helpers we use for our own testing (`steps/10_env.sh` to mint,
> `steps/99_teardown.sh` to delete) are **not committed** and are gitignored. We do
> not ship scripts that create or delete environments.

## Requirements

- **Node 18+** (uses global `fetch` and `node:crypto`). No npm install needed.
- **pac CLI**, authenticated, with access to the target env. On this machine pac
  needs `DOTNET_ROOT` pointing at a .NET 10 runtime (`/Users/administrator/.dotnet`);
  set it in your environment or pass `--dotnet-root <path>`.
- **az CLI** installed. You do **not** need to log in first: the script reads the
  tenant from the pac profile you pick and runs `az login --tenant <that tenant>` for
  you if az isn't already signed in to it. az is used for the REST calls pac cannot
  make: the BAP no-auth connection creation and the Dataverse import/connector/publish
  verification calls.

## Usage

Fully interactive (pick the pac auth profile, confirm the env, deploy):

```bash
node deploy/deploy.mjs
```

After you pick the profile, the script offers that profile's **connected env** (the
one `pac org who` shows) as the default target — just confirm to deploy into it. Say
no to instead filter the tenant's environments by a name/url substring and pick a
different one.

Non-interactive (or partly):

```bash
node deploy/deploy.mjs \
  --profile 1 \
  --env-id <env-guid> \
  --env-url https://<org>.crm.dynamics.com/ \
  --yes

# resume from a step:
node deploy/deploy.mjs --start-at connectors      # import | connectors | connections | publish | manual
```

| Flag | Meaning |
| --- | --- |
| `--profile <n>` | pac auth list index to use. Omit to choose interactively. |
| `--env-id <guid>` / `--env-url <url>` | Target env. Omit and the script offers the profile's connected env as the default, or lets you filter the env list (the tenant can have thousands of envs, so you filter by a name/url substring first). |
| `--start-at <step>` | Resume at `import`, `connectors`, `connections`, `publish` or `manual`. |
| `--yes` | Do not pause for confirmations. |
| `--dotnet-root <path>` | Set `DOTNET_ROOT` for the pac child processes. |

## What the script does

1. **Import** `sample/solution/BlastBoxDemo_*.zip` (`pac solution import`). The
   custom-code compile routinely exceeds pac's 30-minute client timeout while the
   server job keeps running, so success is verified **server-side** (polling
   Dataverse for the solution), with retry/backoff for the function-app capacity
   error.
2. **Connectors:** deploy each MCP connector's inline `.csx` from the files bundled
   in `sample/solution/connectors/<slug>/`. Import registers the connectors but does
   not compile their code, so each is `pac connector update`-d and verified by
   watching its Dataverse `modifiedon` advance. `pac connector update` can hang for
   30+ minutes on these connectors even when the server compile succeeds, so it runs
   in its own process group with a deadline and the step resolves the moment
   `modifiedon` advances (it never blocks on a hung pac).
3. **Connections:** create one no-auth connection per connector via the BAP REST API
   (idempotent: an existing Connected connection is reused). No binding is needed,
   `authMode: Maker` resolves any Connected connection.
4. **Publish:** `PvaPublish` the 4 bots, children before parents.
5. **Manual step:** print the one UI action that has no supported API (below).

All connector code ships **in the repo** (`sample/solution/connectors/`), so the
script works from a fresh clone with no source environment.

## Manual post-install step (required)

Everything above is automated. The one thing the script cannot do (there is no
supported API for it) is finalise each agent's MCP tool wiring, which the modern
Copilot Studio authoring canvas only does when a maker re-attaches the MCP server in
the UI. The connections already exist and the tool definitions are present; this
re-attach is what makes the published agents surface the tools at runtime.

`deploy.mjs` prints these instructions at the end of every run (or run
`node deploy/deploy.mjs --start-at manual ...` to re-print).

1. Open **Copilot Studio** (https://copilotstudio.microsoft.com/) in the deployed env.
2. For **each** agent below: open it, go to **Tools**, **remove** the listed MCP
   server, then **Add a tool, Model Context Protocol**, pick the connector and select
   the **existing Connected connection** (do not create a new one), **Save**, then
   **Publish**.

   | Agent | MCP server(s) to remove + re-add |
   | --- | --- |
   | Store Policy Agent | Policy RAG MCP v2 |
   | Inventory & Fulfillment Agent | Warehouse MCP |
   | Returns & Service Assistant | Membership MCP v2 |
   | Store Associate Assistant | Order Management MCP, Membership MCP v2 |

After re-attaching, the tools load and both packaged scenarios (Block Party Trade-Up
and Self-Serve Card Reissue) work end to end.

## Skills (upload manually if they are not working)

The agents use bundled Python skills. Solution import carries the skill definitions,
but it does not always register the compiled skill bundle in the target env, so a
skill can land looking present yet fail to run. If a skill is not working in the
deployed env, re-upload it through the portal: open the agent, go to **Skills**,
**Add a skill**, and upload the matching zip from `sample/solution/skills/`. The
portal compiles and registers the bundle fresh, which fixes the broken reference.
Then **Publish** the agent.

| Agent | Skill | Artifact in `sample/solution/skills/` |
| --- | --- | --- |
| Store Associate Assistant | prorated-refund-calculator | `prorated-refund-calculator.zip` |
| Store Associate Assistant | points-reconciliation | `points-reconciliation.zip` |
| Store Associate Assistant | slip-pdf-generator | `slip-pdf-generator.zip` |
| Returns & Service Assistant | membership-card-png | `membership-card-png.zip` |
| Returns & Service Assistant | card-reissue | `card-reissue.md` (inline, paste the markdown) |

Each `.zip` is a flat archive of the skill's `SKILL.md` plus its bundled Python
script, the exact format the **Add a skill** uploader expects. `card-reissue` is an
inline skill with no Python, so it ships as a single `SKILL.md` you paste in rather
than a zip you upload.

## Configuration

`deploy/manifest.json` holds the declarative metadata: solution name and zip,
connectors directory, import retry/timeout settings, and the connector and agent
tables (slug, display name, connector id, agent schema name, role, MCP server). Edit
it if the solution contents change. The connector files themselves live under
`sample/solution/connectors/<slug>/` (`apiDefinition.json`, `apiProperties.json`,
`script.csx`).

## Architecture decision: connectors stay IN the solution

We keep the 4 connectors inside the solution (rather than an agents-only solution
plus `pac connector create`) because solution import **preserves each connector's
identity** (the connectorid / apiName the agents' tools reference). `pac connector
create` would mint **new** connector identities, leaving the imported agents' tools
pointing at connectors that don't exist and forcing manual UI re-wiring. With
connectors in the solution, the only thing import misses is compiling the inline
code, which the connectors step fixes by deploying the bundled `.csx`.

## Findings (the "deployment process" problems, resolved)

1. **Import exceeds pac's 30-minute client timeout.** Compiling the inline MCP
   connectors routinely trips `The request channel timed out ... 00:30:00`, even
   though the **server-side** import job keeps running and completes. The import step
   polls Dataverse for the solution and treats its presence as success instead of
   trusting pac's exit code. (Also: the US custom-code function-app pool can be
   capacity-exhausted, `Unable to find an unassigned function app in 'East US'`, so we
   deploy to **EU**.)

2. **Import does not deploy the connectors' inline code.** Solution import registers
   each custom-code connector but does not compile/deploy its `.csx`; the connector's
   `modifiedon` stays equal to `createdon` and tools fail to load. Fix: `pac connector
   update` per connector from the bundled files, verifying `modifiedon` advanced.
   `pac connector update` can also report "succesfully" without deploying, or hang for
   30+ minutes, so the step verifies via `modifiedon` and uses a deadline plus retry.

3. **Tool binding needs no UI binding and no guid-matching.** With `authMode: Maker`,
   the runtime resolves any Connected maker connection for the tool's connector.
   Connections whose ids don't match the guids baked into the tool data still load
   tools, so the connections step just creates one no-auth connection per connector.
   The agents' `connectorId` keeps the source-env hash and still resolves.

4. **Store Policy's tool was authored in the legacy format.** It shipped as
   `kind: TaskDialog` / `InvokeExternalAgentTaskAction`, which the modern Copilot
   Studio UI does not render as an MCP tool (the other three are `kind: McpTool`).
   Fixed at the source agent (now `kind: McpTool`), so the solution export carries the
   fix.
