#!/usr/bin/env bash
# 20_import — import the BlastBox solution (4 MCP connectors + 4 agents).
#
# Two realities shape this step:
#  1. The inline MCP connectors compile on import; that work routinely exceeds
#     pac's HARD 30-minute client channel timeout ("The request channel timed out
#     ... 00:30:00"), even though the SERVER-side import job keeps running and
#     completes. So we never trust pac's exit code alone — after each attempt we
#     poll Dataverse for the solution and treat its presence as success.
#  2. The custom-code function-app pool can be capacity-exhausted ("Unable to
#     find an unassigned function app in '<region>'") — a Microsoft-side issue we
#     retry with backoff.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load
[ -n "${ENV_ID:-}" ]  || die "ENV_ID not set (run 10_env first)"
[ -n "${ORG_URL:-}" ] || die "ORG_URL not set (run 10_env first)"

ZIP="$REPO_ROOT/$SOLUTION_ZIP"
MAX_ATTEMPTS="${IMPORT_MAX_ATTEMPTS:-6}"
BACKOFF="${IMPORT_BACKOFF_SECONDS:-120}"
POLL_MINUTES="${IMPORT_POLL_MINUTES:-25}"   # how long to wait for the server job after a client timeout

solution_present() {
  dv GET "$ORG_URL" "solutions?\$select=uniquename&\$filter=uniquename%20eq%20'$SOLUTION_NAME'" \
    | python3 -c "import sys,json;print('yes' if json.load(sys.stdin).get('value') else '')"
}

# Poll up to POLL_MINUTES for the solution to appear (server job finishing after
# the client timed out). Returns 0 if it shows up.
wait_for_solution() {
  local deadline=$(( $(date +%s) + POLL_MINUTES*60 ))
  while [ "$(date +%s)" -lt "$deadline" ]; do
    [ -n "$(solution_present)" ] && return 0
    sleep 30
  done
  return 1
}

log "Importing $SOLUTION_NAME into $ORG_URL (up to $MAX_ATTEMPTS attempts)"
attempt=1
while :; do
  echo "--- import attempt $attempt/$MAX_ATTEMPTS $(date +%T) ---"
  OUT="$(pac solution import --path "$ZIP" --environment "$ORG_URL" \
          --force-overwrite --publish-changes --max-async-wait-time 60 2>&1 || true)"

  if printf '%s' "$OUT" | grep -qiE "imported|completed successfully|succeeded"; then
    ok "Solution imported + published (pac reported success)."
    break
  fi

  # pac may have timed out client-side while the server job keeps going.
  if printf '%s' "$OUT" | grep -qiE "channel timed out|exceeded the allotted timeout"; then
    warn "pac hit its 30-min client timeout; polling server-side for up to ${POLL_MINUTES}m..."
    if wait_for_solution; then
      ok "Solution present server-side — import succeeded despite client timeout."
      break
    fi
  fi

  # Even on other errors, the solution may already be there from a prior attempt.
  if [ -n "$(solution_present)" ]; then
    ok "Solution present server-side — treating import as successful."
    break
  fi

  if printf '%s' "$OUT" | grep -qi "Unable to find an unassigned function app"; then
    REGION="$(printf '%s' "$OUT" | grep -oi "unassigned function app in '[^']*'" | head -1)"
    warn "Custom-code compute pool exhausted ($REGION). Microsoft-side capacity."
  else
    printf '%s\n' "$OUT" | tail -8
  fi
  [ "$attempt" -ge "$MAX_ATTEMPTS" ] && die "solution import failed after $MAX_ATTEMPTS attempts"
  warn "retrying in ${BACKOFF}s..."
  sleep "$BACKOFF"
  attempt=$((attempt+1))
done

log "Waiting ${APIM_PROPAGATION_SECONDS}s for APIM propagation"
sleep "$APIM_PROPAGATION_SECONDS"
ok "Import step complete."
