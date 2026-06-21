#!/usr/bin/env bash
# 99_teardown — delete the environment recorded in state. Invoked by deploy.sh's
# ERR trap on failure; can also be run manually to clean up.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load

EID="${1:-${ENV_ID:-}}"
[ -n "$EID" ] || { warn "no ENV_ID to delete"; exit 0; }

log "Deleting environment $EID"
if pac admin delete --environment "$EID" 2>&1 | tail -3; then
  ok "Environment deleted."
else
  TOKEN="$(bap_token)"
  curl -s -X DELETE "$BAP_ADMIN/providers/Microsoft.BusinessAppPlatform/scopes/admin/environments/$EID?api-version=$BAP_API_VERSION" \
    -H "Authorization: Bearer $TOKEN" >/dev/null && ok "Deleted via BAP." || warn "delete may have failed"
fi
# clear state so the next run starts clean
rm -f "$STATE_FILE"
