#!/usr/bin/env bash
# Common helpers + config loading for the BlastBox deploy pipeline.
# Source this from every step: `source "$DEPLOY_DIR/lib/common.sh"`

set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_ROOT="$(cd "$DEPLOY_DIR/.." && pwd)"
# shellcheck source=../config.env
source "$DEPLOY_DIR/config.env"

export DOTNET_ROOT="${DOTNET_ROOT:-$DOTNET_ROOT_DIR}"
export PATH="$DOTNET_ROOT:$PATH"

# State file holds values discovered at runtime (env id, org url, ...).
STATE_FILE="${STATE_FILE:-$DEPLOY_DIR/.deploy-state.env}"

POWERAPPS_RESOURCE="https://service.powerapps.com/"
BAP="https://api.powerapps.com"
BAP_ADMIN="https://api.bap.microsoft.com"
PA_API_VERSION="2016-11-01"
BAP_API_VERSION="2021-04-01"

# --- logging -----------------------------------------------------------------
log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
ok()   { printf '\033[1;32m[ok]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[error]\033[0m %s\n' "$*" >&2; exit 1; }

# --- state persistence -------------------------------------------------------
state_set() { # key value
  local k="$1" v="$2"
  touch "$STATE_FILE"
  grep -v "^export ${k}=" "$STATE_FILE" > "${STATE_FILE}.tmp" 2>/dev/null || true
  mv "${STATE_FILE}.tmp" "$STATE_FILE"
  printf 'export %s=%q\n' "$k" "$v" >> "$STATE_FILE"
}
state_load() { [ -f "$STATE_FILE" ] && source "$STATE_FILE" || true; }

# --- tokens ------------------------------------------------------------------
bap_token() {
  az account get-access-token --resource "$POWERAPPS_RESOURCE" --query accessToken -o tsv 2>/dev/null \
    || die "failed to get PowerApps/BAP token (is az logged in as $EXPECTED_UPN?)"
}
dataverse_token() { # instanceUrl
  az account get-access-token --resource "${1%/}/" --query accessToken -o tsv 2>/dev/null \
    || die "failed to get Dataverse token for $1"
}

# --- dataverse web api helper ------------------------------------------------
# dv METHOD <instanceUrl> <path> [json-body]
dv() {
  local method="$1" url="$2" path="$3" body="${4:-}"
  local tok; tok="$(dataverse_token "$url")"
  local full="${url%/}/api/data/v9.2/$path"
  local args=(-s -X "$method" "$full"
    -H "Authorization: Bearer $tok"
    -H "Accept: application/json"
    -H "OData-MaxVersion: 4.0" -H "OData-Version: 4.0"
    -H "Content-Type: application/json; charset=utf-8")
  if [ -n "$body" ]; then args+=(-d "$body"); fi
  curl "${args[@]}"
}

require_tools() {
  command -v pac >/dev/null || die "pac CLI not found"
  command -v az  >/dev/null || die "az CLI not found"
  command -v python3 >/dev/null || die "python3 not found"
  command -v curl >/dev/null || die "curl not found"
}
