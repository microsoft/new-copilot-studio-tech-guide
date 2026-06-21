#!/usr/bin/env bash
# 60_validate — run the two packaged scenarios end-to-end against the freshly
# deployed agents and assert the key numbers from the README transcripts.
#
# Surface: the Copilot Studio portal *preview canvas*, driven by playwright-cli
# (the modern-agent runtime; the raw conversation API returns the degraded
# classic fallback). Sign-in needs a one-time human MFA approval — everything
# else is scripted. Reuses the `-s=cs` playwright session so auth persists.
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/lib/common.sh"
state_load
[ -n "${ENV_ID:-}" ]  || die "ENV_ID not set"
[ -n "${ORG_URL:-}" ] || die "ORG_URL not set"
command -v playwright-cli >/dev/null || die "playwright-cli not found (see ~/.copilot/skills/test-copilot-agent)"

PREVIEW_BASE="https://copilotstudio.preview.microsoft.com/environments/$ENV_ID/agents"
PW_SESSION="${PW_SESSION:-cs}"

botid_of() { dv GET "$ORG_URL" "bots?\$select=botid&\$filter=schemaname%20eq%20'$1'" \
  | python3 -c "import sys,json;v=json.load(sys.stdin).get('value',[]);print(v[0]['botid'] if v else '')"; }

# Read the whole chat transcript text.
read_transcript() {
  playwright-cli -s="$PW_SESSION" --raw eval \
    "() => { const l=document.querySelector('[aria-label=\"Chat messages\"]')||document.querySelector('[role=log]'); return l?l.innerText:document.body.innerText; }" 2>/dev/null
}

# Send one utterance and wait for the streamed reply to settle.
send_turn() { # text
  local input_ref
  playwright-cli -s="$PW_SESSION" snapshot >/tmp/pw_snap.txt 2>/dev/null
  input_ref="$(grep -oE 'e[0-9]+' /tmp/pw_snap.txt | tail -1)"  # best-effort; refined below
  # Prefer the labelled chat input.
  input_ref="$(python3 - <<'PY'
import re,sys
s=open('/tmp/pw_snap.txt').read()
m=re.search(r'(e\d+)[^\n]*Chat message input',s) or re.search(r'textbox[^\n]*Chat message input[^\n]*(e\d+)',s)
print(m.group(1) if m else '')
PY
)"
  [ -n "$input_ref" ] || { warn "could not find chat input ref"; return 1; }
  playwright-cli -s="$PW_SESSION" fill "$input_ref" "$1" --submit >/dev/null 2>&1
  sleep 12
}

assert_contains() { # haystack needle label
  if printf '%s' "$1" | grep -qiF "$2"; then ok "  found: $3"; return 0
  else warn "  MISSING: $3 ($2)"; return 1; fi
}

open_agent() { # schemaname
  local id; id="$(botid_of "$1")"; [ -n "$id" ] || die "no botid for $1"
  log "Opening preview canvas for $1"
  playwright-cli -s="$PW_SESSION" open "$PREVIEW_BASE/$id/preview" >/dev/null 2>&1
  sleep 8
  local snap; snap="$(playwright-cli -s="$PW_SESSION" snapshot 2>/dev/null)"
  if printf '%s' "$snap" | grep -qiE "Sign in|Enter password|Approve"; then
    warn "Sign-in required. Complete sign-in (MFA approval) in the -s=$PW_SESSION browser, then re-run validation."
    warn "See ~/.copilot/skills/test-copilot-agent/SKILL.md for the auth flow."
    return 2
  fi
  sleep 4
}

fails=0

# --- Scenario A: Self-Serve Card Reissue (Returns & Service Assistant) --------
if open_agent "Default_draft_IsBewO"; then
  send_turn "Hi — I lost my BlastPass card. Can you send me a new one?"
  send_turn "Sure, it's MEGA-BLAST-1024."
  send_turn "It's Jordan Pixel, and the last 4 are 1024."
  T="$(read_transcript)"
  log "Scenario A — Card Reissue assertions"
  assert_contains "$T" "MEGA-BLAST-1024" "membership lookup" || fails=$((fails+1))
  assert_contains "$T" "blastpass_card" "card png artifact" || fails=$((fails+1))
else fails=$((fails+1)); fi

# --- Scenario B: Block Party Trade-Up (Store Associate Assistant) -------------
if open_agent "cr26e_storeassociateassistant_ZQHUx1"; then
  send_turn "My BlastBox console is dead and I want to cancel my BlastPass."
  send_turn "It was a manufacturing fault — it just stopped powering on."
  send_turn "He's torn about it, what else can you offer?"
  send_turn "Do it — process the return and cancellation."
  T="$(read_transcript)"
  log "Scenario B — Block Party assertions"
  assert_contains "$T" "76.66" "prorated refund \$76.66" || fails=$((fails+1))
  assert_contains "$T" "105" "points reconciliation \$105.00" || fails=$((fails+1))
  assert_contains "$T" "23.34" "net due \$23.34" || fails=$((fails+1))
  assert_contains "$T" "blastbox_slip" "slip pdf artifact" || fails=$((fails+1))
else fails=$((fails+1)); fi

if [ "$fails" -eq 0 ]; then ok "Both scenarios validated e2e."; else
  die "$fails validation assertion(s) failed — see output above"; fi
