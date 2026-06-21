#!/usr/bin/env bash
# deploy.sh — one-shot, repeatable BlastBox Omega deployment.
#
# Mints a fresh Early Release (Developer SKU) environment with Dataverse, imports
# the BlastBoxDemo solution, publishes customizations, creates the MCP
# connections, binds them, publishes the agents, and validates the two packaged
# scenarios end-to-end. On any failure the freshly minted environment is deleted
# so nothing is left dangling. Success = zero manual configuration outside this
# script.
#
# Usage:
#   deploy/deploy.sh                 # full run (mint -> ... -> validate)
#   KEEP_ON_FAILURE=1 deploy/deploy.sh   # keep env on failure (for debugging)
#   SKIP_ENV=1 deploy/deploy.sh      # reuse env from .deploy-state.env (skip 10_env)
#   START_AT=40 deploy/deploy.sh     # resume from a given step number
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$DEPLOY_DIR/lib/common.sh"

KEEP_ON_FAILURE="${KEEP_ON_FAILURE:-0}"
SKIP_ENV="${SKIP_ENV:-0}"
START_AT="${START_AT:-0}"

cleanup_on_error() {
  local code=$?
  warn "deploy failed (exit $code)"
  state_load
  if [ "$KEEP_ON_FAILURE" = "1" ]; then
    warn "KEEP_ON_FAILURE=1 — leaving env ${ENV_ID:-<none>} for inspection"
  elif [ -n "${ENV_ID:-}" ]; then
    warn "tearing down freshly minted env ${ENV_ID}"
    bash "$DEPLOY_DIR/steps/99_teardown.sh" "$ENV_ID" || true
  fi
  exit "$code"
}
trap cleanup_on_error ERR

run_step() { # number script
  local n="$1" script="$2"
  [ "$n" -ge "$START_AT" ] || { log "skip $script (START_AT=$START_AT)"; return; }
  log "STEP $n — $script"
  bash "$DEPLOY_DIR/steps/$script"
}

bash "$DEPLOY_DIR/steps/00_preflight.sh"
[ "$SKIP_ENV" = "1" ] || run_step 10 "10_env.sh"
run_step 20 "20_import.sh"
run_step 40 "40_connections.sh"
run_step 50 "50_publish_agents.sh"
run_step 60 "60_validate.sh"

state_load
ok "DEPLOY COMPLETE — env $ENV_ID ($ORG_URL) deployed and validated."
