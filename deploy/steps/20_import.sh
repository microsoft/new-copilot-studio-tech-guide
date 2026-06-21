#!/usr/bin/env bash
# 20_import — import the BlastBoxDemo solution and publish customizations.
# The 4 inline MCP connectors compile to a Microsoft-managed function-app pool on
# publish; that pool can be capacity-exhausted ("Unable to find an unassigned
# function app in '<region>'"), so we retry with backoff.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load
[ -n "${ENV_ID:-}" ] || die "ENV_ID not set (run 10_env first)"

ZIP="$REPO_ROOT/$SOLUTION_ZIP"
MAX_ATTEMPTS="${IMPORT_MAX_ATTEMPTS:-6}"
BACKOFF="${IMPORT_BACKOFF_SECONDS:-120}"

log "Importing $SOLUTION_NAME into $ENV_ID (up to $MAX_ATTEMPTS attempts)"
attempt=1
while :; do
  echo "--- import attempt $attempt/$MAX_ATTEMPTS $(date +%T) ---"
  OUT="$(pac solution import --path "$ZIP" --environment "$ENV_ID" \
          --force-overwrite --publish-changes --max-async-wait-time 60 2>&1 || true)"
  if printf '%s' "$OUT" | grep -qiE "imported|completed successfully|succeeded"; then
    ok "Solution imported + published."
    break
  fi
  if printf '%s' "$OUT" | grep -qi "Unable to find an unassigned function app"; then
    REGION="$(printf '%s' "$OUT" | grep -oi "unassigned function app in '[^']*'" | head -1)"
    warn "Custom-code compute pool exhausted ($REGION). Microsoft-side capacity."
  else
    printf '%s\n' "$OUT" | tail -8
  fi
  if [ "$attempt" -ge "$MAX_ATTEMPTS" ]; then
    die "solution import failed after $MAX_ATTEMPTS attempts"
  fi
  warn "retrying in ${BACKOFF}s..."
  sleep "$BACKOFF"
  attempt=$((attempt+1))
done

log "Waiting ${APIM_PROPAGATION_SECONDS}s for APIM propagation"
sleep "$APIM_PROPAGATION_SECONDS"
ok "Import step complete."
