#!/usr/bin/env bash
# 50_publish_agents — publish all four bots via the Dataverse PvaPublish action
# (NOT `pac copilot publish`, which throws a client-side parse error while
# polling). Children are published before parents so the connected-agent graph
# is live when the parents go up.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load
[ -n "${ORG_URL:-}" ] || die "ORG_URL not set"

botid_of() { # schemaname
  dv GET "$ORG_URL" "bots?\$select=botid,name,schemaname&\$filter=schemaname%20eq%20'$1'" \
    | python3 -c "import sys,json;v=json.load(sys.stdin).get('value',[]);print(v[0]['botid'] if v else '')"
}

publish_bot() { # schemaname
  local schema="$1" id
  id="$(botid_of "$schema")"
  [ -n "$id" ] || die "bot not found: $schema"
  local resp; resp="$(dv POST "$ORG_URL" "bots($id)/Microsoft.Dynamics.CRM.PvaPublish" '{}')"
  if [ -z "$resp" ] || ! printf '%s' "$resp" | grep -qi error; then
    ok "published $schema ($id)"
  else
    printf '%s\n' "$resp" | head -c 400; echo
    die "publish failed for $schema"
  fi
}

log "Publishing connected (child) agents first"
for s in "${AGENT_SCHEMA_CHILDREN[@]}"; do publish_bot "$s"; done

log "Publishing parent agents"
for s in "${AGENT_SCHEMA_PARENTS[@]}"; do publish_bot "$s"; done

ok "All agents published."
