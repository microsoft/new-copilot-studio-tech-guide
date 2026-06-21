#!/usr/bin/env bash
# deploy.sh — repeatable BlastBox Omega deployment into an EXISTING environment.
#
# Imports the BlastBoxDemo solution, deploys each connector's inline code, creates
# a no-auth MCP connection per connector, publishes the agents, and validates the
# two packaged scenarios end-to-end. It does NOT create or delete environments —
# point it at an env you already have (see config.env "Target environment").
#
# ONE manual step remains afterwards (no supported API): re-attach each agent's
# MCP server in the Copilot Studio UI. Step 70 prints exactly what to do, and it
# is also printed at the end of every run. See deploy/README.md.
#
# Usage:
#   TARGET_ENV_URL=https://<org>.crm4.dynamics.com/ TARGET_ENV_ID=<guid> deploy/deploy.sh
#   START_AT=40 deploy/deploy.sh     # resume from a given step number
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DEPLOY_DIR/lib/common.sh"

START_AT="${START_AT:-0}"

on_error() {
  local code=$?
  warn "deploy failed (exit $code)"
  warn "the target environment is left untouched (this pipeline never deletes envs)."
  exit "$code"
}
trap on_error ERR

run_step() { # number script
  local n="$1" script="$2"
  [ "$n" -ge "$START_AT" ] || { log "skip $script (START_AT=$START_AT)"; return; }
  log "STEP $n — $script"
  bash "$DEPLOY_DIR/steps/$script"
}

bash "$DEPLOY_DIR/steps/00_preflight.sh"   # resolves + records the target env
run_step 20 "20_import.sh"
run_step 30 "30_connectors.sh"
run_step 40 "40_connections.sh"
run_step 50 "50_publish_agents.sh"
run_step 60 "60_validate.sh"

state_load
ok "DEPLOY COMPLETE — env $ENV_ID ($ORG_URL)."
# Always print the one manual UI step (connections are in place, tools loaded;
# each agent's MCP server must be re-attached once in the UI — no API for this).
bash "$DEPLOY_DIR/steps/70_manual_steps.sh"
