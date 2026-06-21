#!/usr/bin/env bash
# 70_manual_steps.sh — print the one manual post-install step.
#
# Everything the script can automate is done by the time this runs: the solution
# is imported, every connector's inline code is deployed, a Connected no-auth
# connection exists for each connector, and all agents are published. The tools
# are present and their connections resolve.
#
# What the script CANNOT do today: the modern Copilot Studio authoring canvas
# only finalises an agent's MCP tool wiring when a maker re-attaches the MCP
# server in the UI. Until then the published agent may not surface the tools at
# runtime. This is a per-agent UI action and there is no supported API for it.
#
# This step is read-only: it just prints the instructions (also at the end of a
# full deploy.sh run). Re-run it any time: bash deploy/steps/70_manual_steps.sh
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$DEPLOY_DIR/lib/common.sh"
state_load

ENV_LABEL="${ENV_ID:-<env>}"
PORTAL_URL="https://copilotstudio.microsoft.com/"

cat <<EOF

================================================================================
  MANUAL POST-INSTALL STEP (required — one time, in the UI)
================================================================================
The deploy is complete: solution imported, connector code deployed, no-auth
connections Connected, and all agents published. One manual step remains that has
no supported API — you must re-attach each agent's MCP server in the authoring UI.

Open Copilot Studio:  ${PORTAL_URL}
Environment:          ${ENV_LABEL}
  org URL:            ${ORG_URL:-<run 10_env first>}

For EACH agent below that has an MCP tool:

  1. Open the agent  ->  Tools.
  2. Remove the listed MCP server/tool.
  3. Add it back (Add a tool -> Model Context Protocol -> pick the connector,
     choose the existing Connected connection — do NOT create a new one).
  4. Save and Publish the agent.

  Agent                                MCP connector to remove + re-add
  -----------------------------------  --------------------------------
  Store Policy Agent                   Policy RAG MCP v2
  Inventory & Fulfillment Agent        Warehouse MCP
  Returns & Service Assistant          Order Management MCP, Membership MCP v2
  Store Associate Assistant            (orchestrates the children — republish
                                        after the children above are re-saved)

Connections already exist for every connector, so just select the existing one
when re-adding — there is nothing to authenticate.

After re-attaching, the tools load and both packaged scenarios work end to end.
================================================================================

EOF
