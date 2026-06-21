#!/usr/bin/env bash
# 10_env — ensure exactly ONE copilot-adilei* env: delete any existing one(s),
# then mint a fresh Early Release (Developer SKU) US env WITH Dataverse.
# Exports ENV_ID + ORG_URL to the state file.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"

TS="$(date +%m%d%H%M%S)"
NEW_NAME="${ENV_NAME_PREFIX}-${TS}"

# --- 1. delete any pre-existing copilot-adilei* env (one-at-a-time rule) ------
log "Looking for existing ${ENV_NAME_PREFIX}* environments to remove"
TOKEN="$(bap_token)"
EXISTING="$(curl -s "$BAP_ADMIN/providers/Microsoft.BusinessAppPlatform/scopes/admin/environments?api-version=$BAP_API_VERSION&\$expand=properties" \
  -H "Authorization: Bearer $TOKEN" \
  | ENV_NAME_PREFIX="$ENV_NAME_PREFIX" python3 -c "
import sys,os,json
pref=os.environ['ENV_NAME_PREFIX']
for e in json.load(sys.stdin).get('value',[]):
    if (e.get('properties',{}).get('displayName','') or '').startswith(pref):
        print(e['name'])
")"
for eid in $EXISTING; do
  warn "Deleting existing env $eid"
  pac admin delete --environment "$eid" 2>&1 | tail -2 || \
    curl -s -X DELETE "$BAP_ADMIN/providers/Microsoft.BusinessAppPlatform/scopes/admin/environments/$eid?api-version=$BAP_API_VERSION" -H "Authorization: Bearer $TOKEN" >/dev/null
done

# --- 2. create fresh Developer (== Early Release / Frequent) US env -----------
log "Creating fresh Early Release Developer env: $NEW_NAME (region $ENV_REGION)"
CREATE_OUT="$(pac admin create \
  --name "$NEW_NAME" \
  --type "$ENV_TYPE" \
  --region "$ENV_REGION" \
  --currency "$ENV_CURRENCY" \
  --language "$ENV_LANGUAGE" \
  --domain "$NEW_NAME" 2>&1)"
echo "$CREATE_OUT" | tail -6
ORG_URL="$(printf '%s\n' "$CREATE_OUT" | grep -oE 'https://[a-z0-9-]+\.crm[0-9]*\.dynamics\.com/?' | head -1)"
ENV_ID="$(printf '%s\n' "$CREATE_OUT" | grep -oE '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' | head -1)"
[ -n "$ENV_ID" ] || die "could not parse Environment ID from create output"
[ -n "$ORG_URL" ] || die "could not parse Org URL from create output"

# --- 3. verify Early Release cadence + Dataverse readiness -------------------
CAD="$(curl -s "$BAP_ADMIN/providers/Microsoft.BusinessAppPlatform/scopes/admin/environments/$ENV_ID?api-version=$BAP_API_VERSION&\$expand=properties" \
  -H "Authorization: Bearer $TOKEN" \
  | python3 -c "import sys,json;p=json.load(sys.stdin)['properties'];print(p.get('updateCadence',{}).get('id'),'|',p.get('linkedEnvironmentMetadata',{}).get('instanceState'))")"
echo "  cadence|dataverse: $CAD"
case "$CAD" in
  Frequent\ *Ready*) ok "Early Release env ready" ;;
  *) warn "Unexpected cadence/state: $CAD (continuing)";;
esac

state_set ENV_ID "$ENV_ID"
state_set ORG_URL "$ORG_URL"
ok "Env: $NEW_NAME  id=$ENV_ID  url=$ORG_URL"
