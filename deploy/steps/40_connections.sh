#!/usr/bin/env bash
# 40_connections — create one no-auth connection per MCP connector (via the BAP
# REST API, since `pac connection create` only does service-principal), then bind
# every imported MCP connectionreference to the matching connection.
#
# Why this is needed: the solution ships NO connection records and NO bound
# connectionreferences. On import, Copilot Studio creates connectionreference
# rows from each agent's MCP tool definition, but with an empty connectionid.
# An agent can't call its MCP server until that connectionid points at a real
# connection. We create the connections and PATCH each connectionreference.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load
[ -n "${ENV_ID:-}" ]  || die "ENV_ID not set"
[ -n "${ORG_URL:-}" ] || die "ORG_URL not set"

TOKEN="$(bap_token)"

# Resolve a connector's BAP apiName by display name (custom apis, newest first).
resolve_api_name() { # display-name
  curl -s "$BAP/providers/Microsoft.PowerApps/apis?api-version=$PA_API_VERSION&\$filter=environment%20eq%20'$ENV_ID'" \
    -H "Authorization: Bearer $TOKEN" \
    | WANT="$1" python3 -c "
import sys,os,json
want=os.environ['WANT']
cands=[a for a in json.load(sys.stdin).get('value',[])
       if a.get('properties',{}).get('isCustomApi') and a.get('properties',{}).get('displayName')==want]
cands.sort(key=lambda a:a.get('properties',{}).get('createdTime',''),reverse=True)
print(cands[0]['name'] if cands else '')
"
}

# Create a no-auth connection for an apiName; echo the new connection name.
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

declare -A API_OF CONN_OF
log "Creating no-auth connections for ${#CONNECTOR_DISPLAY_NAMES[@]} connectors"
for disp in "${CONNECTOR_DISPLAY_NAMES[@]}"; do
  api="$(resolve_api_name "$disp")"
  [ -n "$api" ] || die "could not resolve apiName for connector '$disp' (is it imported+published?)"
  conn="$(create_connection "$api" "$disp Connection")" || die "connection create failed for '$disp'"
  API_OF["$disp"]="$api"; CONN_OF["$disp"]="$conn"
  ok "$disp -> api=$api conn=$conn"
done

# Bind every MCP connectionreference whose connectorid matches one of our apis.
log "Binding connectionreferences to connections"
REFS_JSON="$(dv GET "$ORG_URL" "connectionreferences?\$select=connectionreferenceid,connectionreferencelogicalname,connectorid,connectionid")"
bound=0
while IFS=$'\t' read -r refid logical connectorid; do
  [ -n "$refid" ] || continue
  match_conn=""
  for disp in "${CONNECTOR_DISPLAY_NAMES[@]}"; do
    case "$connectorid" in *"/${API_OF[$disp]}") match_conn="${CONN_OF[$disp]}";; esac
  done
  [ -n "$match_conn" ] || continue
  patch="$(printf '{"connectionid":"%s"}' "$match_conn")"
  dv PATCH "$ORG_URL" "connectionreferences($refid)" "$patch" >/dev/null
  ok "bound $logical -> $match_conn"
  bound=$((bound+1))
done < <(printf '%s' "$REFS_JSON" | python3 -c "
import sys,json
for r in json.load(sys.stdin).get('value',[]):
    print('\t'.join([r.get('connectionreferenceid',''),r.get('connectionreferencelogicalname','') or '',r.get('connectorid','') or '']))
")

[ "$bound" -gt 0 ] || die "no connectionreferences matched our connectors — import may not have created them"
ok "Bound $bound connectionreference(s)."
