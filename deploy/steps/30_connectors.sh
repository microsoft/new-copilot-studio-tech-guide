#!/usr/bin/env bash
# 30_connectors — deploy the inline custom-code for each MCP connector.
#
# Why this is needed: solution import registers the 4 connectors but does NOT
# compile/deploy their inline .csx — the connector's `modifiedon` stays equal to
# its `createdon` and tools fail to load. `pac connector update` (re)uploads the
# apiDefinition + apiProperties + script.csx and triggers the custom-code deploy.
# We download each connector from the SOURCE env (the connector id is preserved
# across solution import, so the same id keys both envs) and update it in TARGET,
# then verify `modifiedon` actually advanced (pac can report success without
# deploying — see deploy/README.md).
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load
[ -n "${ENV_ID:-}" ]  || die "ENV_ID not set (run 10_env first)"
[ -n "${ORG_URL:-}" ] || die "ORG_URL not set"
[ -n "${SOURCE_ENV_URL:-}" ] || die "SOURCE_ENV_URL not set in config.env"

WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

# modifiedon for a connector id (target env).
connector_modifiedon() { # connectorId
  dv GET "$ORG_URL" "connectors?\$select=modifiedon&\$filter=connectorid%20eq%20$1" \
    | python3 -c "import sys,json;v=json.load(sys.stdin).get('value',[]);print(v[0]['modifiedon'] if v else '')"
}

deploy_connector() { # display id
  local disp="$1" id="$2" dir="$WORKDIR/$id"
  mkdir -p "$dir"
  log "[$disp] downloading from source"
  pac connector download --connector-id "$id" --environment "$SOURCE_ENV_URL" \
      --outputDirectory "$dir" >/dev/null 2>&1 \
    || die "[$disp] connector download failed"
  [ -f "$dir/script.csx" ] || die "[$disp] no script.csx downloaded (not a custom-code connector?)"

  local before after attempt
  before="$(connector_modifiedon "$id")"
  for attempt in 1 2 3; do
    log "[$disp] pac connector update (attempt $attempt) — deploying inline code"
    pac connector update --connector-id "$id" --environment "$ORG_URL" \
        --api-definition-file "$dir/apiDefinition.json" \
        --api-properties-file "$dir/apiProperties.json" \
        --script-file "$dir/script.csx" >/dev/null 2>&1 \
      || { warn "[$disp] update attempt $attempt errored"; sleep 10; continue; }
    after="$(connector_modifiedon "$id")"
    if [ -n "$after" ] && [ "$after" != "$before" ]; then
      ok "[$disp] code deployed (modifiedon $before -> $after)"
      return 0
    fi
    warn "[$disp] modifiedon did not advance ($before) — retrying"
    sleep 10
  done
  die "[$disp] connector code did not deploy after 3 attempts (modifiedon stuck at $before)"
}

for pair in "${CONNECTOR_IDS[@]}"; do
  disp="${pair%%::*}"; id="${pair##*::}"
  deploy_connector "$disp" "$id"
done

ok "All connector code deployed."
