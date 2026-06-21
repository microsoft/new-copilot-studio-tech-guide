#!/usr/bin/env bash
# 00_preflight — assert tooling, auth, and inputs before touching anything.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"

log "Preflight checks"
require_tools

# .NET 10 present for pac (note: `pac --version` is not a valid command in 2.9.x)
pac auth list >/dev/null 2>&1 || die "pac not runnable (check DOTNET_ROOT=$DOTNET_ROOT)"

# az logged into the expected tenant + identity
AZ_UPN="$(az account show --query user.name -o tsv 2>/dev/null || true)"
AZ_TENANT="$(az account show --query tenantId -o tsv 2>/dev/null || true)"
[ "$AZ_TENANT" = "$TENANT_ID" ] || die "az tenant is '$AZ_TENANT', expected $TENANT_ID. Run: az login --tenant $TENANT_ID"
[ "$AZ_UPN" = "$EXPECTED_UPN" ] || warn "az UPN is '$AZ_UPN' (expected $EXPECTED_UPN) — continuing"

# pac authenticated as the same identity
PAC_UPN="$(pac auth list 2>/dev/null | awk '/\*/{for(i=1;i<=NF;i++) if($i ~ /@/) print $i}' | head -1)"
[ -n "$PAC_UPN" ] || die "no active pac auth profile (pac auth select --index <n>)"
ok "pac active as: $PAC_UPN"

# solution zip present
[ -f "$REPO_ROOT/$SOLUTION_ZIP" ] || die "solution zip not found: $REPO_ROOT/$SOLUTION_ZIP"

# resolve the TARGET environment (this pipeline deploys into an existing env).
# precedence: TARGET_ENV_* (env/config) > already-seeded state.
state_load
RESOLVED_URL="${TARGET_ENV_URL:-${ORG_URL:-}}"
RESOLVED_ID="${TARGET_ENV_ID:-${ENV_ID:-}}"
if [ -z "$RESOLVED_URL" ] || [ -z "$RESOLVED_ID" ]; then
  die "no target environment. Set TARGET_ENV_URL + TARGET_ENV_ID (env or config.env), or seed $STATE_FILE with ORG_URL/ENV_ID."
fi
state_set ORG_URL "$RESOLVED_URL"
state_set ENV_ID  "$RESOLVED_ID"
ok "Target env: $RESOLVED_ID  ($RESOLVED_URL)"

# token sanity
T="$(bap_token)"; [ -n "$T" ] || die "could not acquire BAP token"
ok "Preflight passed."
