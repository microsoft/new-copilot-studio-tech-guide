#!/usr/bin/env bash
# 40_connections — create one no-auth connection per MCP connector.
#
# With `authMode: Maker` on each agent's MCP tool, the runtime resolves any
# Connected maker connection for the tool's connector — there is NO need to bind
# connectionreference records (proven: connections whose ids don't match the
# guids baked in the tool data still load tools fine). So this step simply makes
# sure exactly one Connected no-auth connection exists per connector.
#
# `pac connection create` only supports service-principal auth, so we use the BAP
# REST API (PUT, self-generated GUID, no parameterValues) — see
# ~/.copilot/skills/powerplatform-noauth-connection.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load
[ -n "${ENV_ID:-}" ]  || die "ENV_ID not set"
[ -n "${ORG_URL:-}" ] || die "ORG_URL not set"

TOKEN="$(bap_token)"

# Resolve a connector's BAP apiName from its Dataverse connectorinternalid (which
# equals the BAP apiName). More robust than display-name matching.
api_name_of_connector() { # connectorId
  dv GET "$ORG_URL" "connectors?\$select=connectorinternalid&\$filter=connectorid%20eq%20$1" \
    | python3 -c "import sys,json;v=json.load(sys.stdin).get('value',[]);print(v[0].get('connectorinternalid','') if v else '')"
}

# List existing Connected connection ids for an apiName.
connected_connections() { # apiName
  curl -s "$BAP/providers/Microsoft.PowerApps/apis/$1/connections?api-version=$PA_API_VERSION&\$filter=environment%20eq%20'$ENV_ID'" \
    -H "Authorization: Bearer $TOKEN" \
    | python3 -c "
import sys,json
for c in json.load(sys.stdin).get('value',[]):
    if any(s.get('status')=='Connected' for s in c.get('properties',{}).get('statuses',[])):
        print(c['name'])
"
}

create_connection() { # apiName displayName
  local api="$1" disp="$2"
  local conn; conn="$(uuidgen | tr -d '-' | tr 'A-Z' 'a-z')"
  local url="$BAP/providers/Microsoft.PowerApps/apis/$api/connections/$conn?api-version=$PA_API_VERSION&\$filter=environment%20eq%20'$ENV_ID'"
  local body
  body="$(ENV_ID="$ENV_ID" DISP="$disp" python3 -c "
import json,os
print(json.dumps({'properties':{'environment':{'id':'/providers/Microsoft.PowerApps/environments/%s'%os.environ['ENV_ID'],'name':os.environ['ENV_ID']},'displayName':os.environ['DISP']}}))")"
  local code
  code="$(curl -s -o /tmp/conn_resp.json -w '%{http_code}' -X PUT "$url" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "$body")"
  [ "$code" = "201" ] || [ "$code" = "200" ] || { cat /tmp/conn_resp.json >&2; return 1; }
  printf '%s' "$conn"
}

log "Ensuring a no-auth connection for ${#CONNECTOR_IDS[@]} connectors"
for pair in "${CONNECTOR_IDS[@]}"; do
  disp="${pair%%::*}"; id="${pair##*::}"
  api="$(api_name_of_connector "$id")"
  [ -n "$api" ] || die "could not resolve apiName for '$disp' ($id) — is it imported?"
  existing="$(connected_connections "$api" | head -1)"
  if [ -n "$existing" ]; then
    ok "$disp -> already Connected ($existing)"
    continue
  fi
  conn="$(create_connection "$api" "$disp Connection")" || die "connection create failed for '$disp'"
  ok "$disp -> created connection $conn (Connected)"
done

ok "All connectors have a Connected no-auth connection."
