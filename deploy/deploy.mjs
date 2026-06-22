#!/usr/bin/env node
// deploy.mjs — cross-OS (Windows / macOS / Linux) deploy for the BlastBox Omega
// demo solution into an EXISTING Power Platform environment.
//
// It is interactive: pick the pac auth profile, then pick the target environment
// from a list (or pass --env-id / --env-url). It then:
//   1. imports the solution (sample/solution/*.zip),
//   2. deploys each MCP connector's inline custom-code from the files bundled in
//      the repo (sample/solution/connectors/<slug>/) — NO source environment is
//      needed, so it works from a fresh clone,
//   3. creates a no-auth connection per connector (BAP REST API),
//   4. publishes the agents (children first),
//   5. prints the one manual UI step (re-attach each agent's MCP server).
//
// Requirements: Node 18+, pac CLI (authenticated). The script picks the az tenant
// from the chosen pac profile and runs `az login` for you if az isn't already signed
// in to it. On this machine pac needs DOTNET_ROOT — set it in the environment or pass
// --dotnet-root.
//
// Usage:
//   node deploy/deploy.mjs                       # fully interactive
//   node deploy/deploy.mjs --env-id <guid> --env-url https://<org>.crm.dynamics.com/
//   node deploy/deploy.mjs --profile 3           # preselect pac auth index
//   node deploy/deploy.mjs --yes                 # don't pause for confirmations
//   node deploy/deploy.mjs --start-at connectors # resume: import|connectors|connections|publish|manual

import { spawnSync, spawn } from 'node:child_process';
import { createInterface } from 'node:readline/promises';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import { randomUUID } from 'node:crypto';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..');
const manifest = JSON.parse(readFileSync(join(__dirname, 'manifest.json'), 'utf8'));
const isWin = process.platform === 'win32';

// pac connector update can hang for 30+ min on custom-code MCP connectors even when
// the server-side compile succeeds. Cap it, then verify success via Dataverse modifiedon.
const CONNECTOR_UPDATE_TIMEOUT_MS = (manifest.connectorUpdateTimeoutMinutes || 8) * 60_000;

// ---------- args ----------
const argv = process.argv.slice(2);
const getArg = (name) => { const i = argv.indexOf(`--${name}`); return i >= 0 ? argv[i + 1] : undefined; };
const hasFlag = (name) => argv.includes(`--${name}`);
const OPT = {
  envId: getArg('env-id'),
  envUrl: getArg('env-url'),
  profile: getArg('profile'),
  dotnetRoot: getArg('dotnet-root'),
  startAt: getArg('start-at') || 'import',
  yes: hasFlag('yes'),
};

// ---------- env for child processes ----------
const childEnv = { ...process.env };
const defaultDotnet = '/Users/administrator/.dotnet';
if (OPT.dotnetRoot) childEnv.DOTNET_ROOT = OPT.dotnetRoot;
else if (!childEnv.DOTNET_ROOT && existsSync(defaultDotnet)) childEnv.DOTNET_ROOT = defaultDotnet;

// ---------- logging ----------
const paint = (code, s) => `\x1b[${code}m${s}\x1b[0m`;
const log  = (s) => console.log(paint('1;34', '==>') + ' ' + s);
const ok   = (s) => console.log(paint('1;32', '[ok]') + ' ' + s);
const warn = (s) => console.error(paint('1;33', '[warn]') + ' ' + s);
const die  = (s) => { console.error(paint('1;31', '[error]') + ' ' + s); process.exit(1); };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---------- shell ----------
function quoteArg(a) {
  a = String(a);
  return isWin ? '"' + a.replace(/"/g, '""') + '"' : "'" + a.replace(/'/g, `'\\''`) + "'";
}
// Run a command (cross-OS via shell). Returns { code, stdout, stderr, out, timedOut }.
// opts.timeoutMs: kill the child after this many ms (pac can hang for 30+ min on
// custom-code connector/solution operations even when the server-side job is fine).
function sh(cmd, args = [], opts = {}) {
  const line = [cmd, ...args.map(quoteArg)].join(' ');
  const r = spawnSync(line, {
    shell: true, encoding: 'utf8', maxBuffer: 128 * 1024 * 1024, env: childEnv,
    timeout: opts.timeoutMs || 0, killSignal: 'SIGKILL',
  });
  const timedOut = r.error && (r.error.code === 'ETIMEDOUT' || r.signal === 'SIGKILL');
  return { code: r.status ?? 1, stdout: r.stdout || '', stderr: r.stderr || '', out: (r.stdout || '') + (r.stderr || ''), timedOut: !!timedOut };
}

// Run a command with the terminal attached (stdio inherited) — for interactive
// flows like `az login` that open a browser / device-code prompt. Returns { code }.
function shInteractive(cmd, args = []) {
  const line = [cmd, ...args.map(quoteArg)].join(' ');
  const r = spawnSync(line, { shell: true, stdio: 'inherit', env: childEnv });
  return { code: r.status ?? 1 };
}

// Run a command asynchronously in its OWN process group so it (and any grandchildren
// like the dotnet host pac spawns) can be killed reliably. spawnSync's `timeout`
// only kills the immediate shell and then blocks waiting for the orphaned grandchild
// to close the stdout pipe — so a hung `pac` ignores it.
//
// IMPORTANT: we wait for pac to EXIT NATURALLY. `pac connector update` does work
// AFTER the Dataverse `modifiedon` blob write (it compiles/binds the custom-code
// execution); killing it the moment `modifiedon` advances leaves the code deployed
// but NOT executable (tools then return an empty `[{"jsonrpc":"2.0"}]` envelope).
// So the deadline is only a last-resort hang guard, never an early-success kill.
// Resolves { code, exited, timedOut }.
function shAsyncRace(cmd, args, { timeoutMs } = {}) {
  return new Promise((resolve) => {
    const line = [cmd, ...args.map(quoteArg)].join(' ');
    const child = spawn(line, { shell: true, env: childEnv, detached: !isWin, stdio: 'ignore' });
    let done = false;
    const kill = () => {
      try { if (!isWin && child.pid) process.kill(-child.pid, 'SIGKILL'); } catch {}
      try { child.kill('SIGKILL'); } catch {}
    };
    const finish = (res) => { if (done) return; done = true; clearTimeout(deadline); resolve(res); };
    child.on('exit', (c) => finish({ code: c ?? 1, exited: true, timedOut: false }));
    child.on('error', () => finish({ code: 1, exited: false, timedOut: false }));
    // Hang guard only: if pac never returns, kill the whole group and report a timeout.
    const deadline = setTimeout(() => { kill(); finish({ code: 124, exited: false, timedOut: true }); },
      timeoutMs || 0);
  });
}

// ---------- prompts ----------
async function ask(q) {
  const rl = createInterface({ input: process.stdin, output: process.stdout });
  const a = await rl.question(q);
  rl.close();
  return a.trim();
}
async function chooseFromList(items, render, prompt) {
  items.forEach((it, i) => console.log(`  [${i + 1}] ${render(it)}`));
  const a = await ask(prompt);
  const n = parseInt(a, 10);
  if (!(n >= 1 && n <= items.length)) die(`invalid selection: ${a}`);
  return items[n - 1];
}
async function confirm(q) {
  if (OPT.yes) return true;
  const a = (await ask(`${q} [y/N] `)).toLowerCase();
  return a === 'y' || a === 'yes';
}

// ---------- tokens (cached) ----------
const POWERAPPS = 'https://service.powerapps.com/';
const tokenCache = new Map();
function azToken(resource) {
  const cached = tokenCache.get(resource);
  if (cached && cached.exp > Date.now() + 60_000) return cached.tok;
  const r = sh('az', ['account', 'get-access-token', '--resource', resource, '--query', 'accessToken', '-o', 'tsv']);
  const tok = r.stdout.trim();
  if (!tok) die(`could not get an az token for ${resource}.\n${r.stderr}\nMake sure az is logged into the target env's tenant: az login --tenant <tenant>`);
  tokenCache.set(resource, { tok, exp: Date.now() + 45 * 60_000 });
  return tok;
}

// ---------- REST helpers ----------
function dvResource(orgUrl) { return orgUrl.endsWith('/') ? orgUrl : orgUrl + '/'; }
async function dv(method, orgUrl, path, body) {
  const tok = azToken(dvResource(orgUrl));
  const url = orgUrl.replace(/\/$/, '') + '/api/data/v9.2/' + path;
  const res = await fetch(url, {
    method,
    headers: {
      Authorization: `Bearer ${tok}`, Accept: 'application/json',
      'OData-MaxVersion': '4.0', 'OData-Version': '4.0',
      'Content-Type': 'application/json; charset=utf-8',
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json = null; try { json = text ? JSON.parse(text) : null; } catch { /* non-json */ }
  return { status: res.status, json, text };
}

// ============================================================================
// Selection
// ============================================================================
function parsePacAuthList() {
  const r = sh('pac', ['auth', 'list']);
  if (r.code !== 0) die(`pac auth list failed (is pac installed and DOTNET_ROOT set?).\n${r.out}`);
  const rows = [];
  for (const line of r.stdout.split('\n')) {
    const m = line.match(/^\s*\[(\d+)\]/);
    if (!m) continue;
    const index = m[1];
    const active = /\*/.test(line.slice(0, line.indexOf(']') + 6));
    const email = (line.match(/[^\s]+@[^\s]+/) || [''])[0];
    const url = (line.match(/https:\/\/\S+/) || [''])[0];
    rows.push({ index, active, email, url, raw: line.trim() });
  }
  if (!rows.length) die('no pac auth profiles found. Run: pac auth create');
  return rows;
}

function parsePacAdminList() {
  const r = sh('pac', ['admin', 'list']);
  if (r.code !== 0) die(`pac admin list failed.\n${r.out}`);
  const guidRe = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;
  const urlRe = /https:\/\/\S+?\.dynamics\.com\//i;
  const envs = [];
  for (const line of r.stdout.split('\n')) {
    const id = (line.match(guidRe) || [])[0];
    const url = (line.match(urlRe) || [])[0];
    if (!id || !url) continue; // data rows only
    // Columns are separated by runs of 2+ spaces; env names never contain 2+ spaces.
    let tokens = line.trim().split(/\s{2,}/);
    const active = tokens[0] === '*';
    if (active) tokens = tokens.slice(1);
    // The id+url share a single-space-separated token; Type is the token after it.
    const idIdx = tokens.findIndex((t) => guidRe.test(t) && urlRe.test(t));
    const type = idIdx >= 0 ? (tokens[idIdx + 1] || '').trim() : '';
    envs.push({ name: (tokens[0] || '').trim(), type, url, id, active });
  }
  return envs;
}

async function selectProfile() {
  const profiles = parsePacAuthList();
  let chosen;
  if (OPT.profile) {
    chosen = profiles.find((p) => p.index === String(OPT.profile));
    if (!chosen) die(`--profile ${OPT.profile} not found in pac auth list`);
  } else {
    log('Select the pac auth profile to deploy with:');
    chosen = await chooseFromList(
      profiles,
      (p) => `${p.active ? '* ' : '  '}${p.email || p.raw}  ${p.url}`,
      'Profile number: ',
    );
  }
  const sel = sh('pac', ['auth', 'select', '--index', chosen.index]);
  if (sel.code !== 0) die(`pac auth select failed.\n${sel.out}`);
  ok(`Using pac profile [${chosen.index}] ${chosen.email}`);
  return chosen;
}

// The az tokens (Dataverse + BAP) must come from the SAME Entra tenant that owns
// the target env, or the REST calls 401. pac doesn't expose the tenant GUID, but the
// active profile's account domain IS a valid `az login --tenant` value, so we use it
// (and skip the login if az is already signed in to that tenant's domain).
function pacTenantDomain() {
  const r = sh('pac', ['org', 'who', '--json']);
  let email = '';
  try { email = (JSON.parse(r.stdout) || {}).UserEmail || ''; } catch { /* ignore */ }
  const m = email.match(/@(\S+)$/);
  return m ? m[1].toLowerCase() : null;
}
function azDomains() {
  const r = sh('az', ['account', 'show', '--query', '{d:tenantDefaultDomain,u:user.name}', '-o', 'json']);
  if (r.code !== 0) return [];
  try {
    const j = JSON.parse(r.stdout) || {};
    return [j.d, (j.u || '').split('@')[1]].filter(Boolean).map((s) => s.toLowerCase());
  } catch { return []; }
}
async function ensureAz() {
  const tenant = pacTenantDomain();
  const cur = azDomains();
  if (tenant && cur.includes(tenant)) {
    ok(`az already signed in to the env tenant (${tenant}).`);
    return;
  }
  if (!tenant && cur.length) {
    warn(`could not resolve the env tenant from pac; using the current az session (${cur[0]}). If REST calls 401, run: az login --tenant <tenant>`);
    return;
  }
  if (tenant && cur.length) warn(`az is signed in to a different tenant (${cur[0]}); the env needs ${tenant}. Signing you in to the right one...`);
  else log(`az is not signed in${tenant ? ` to ${tenant}` : ''}. Launching az login...`);
  const args = ['login'];
  if (tenant) args.push('--tenant', tenant);
  args.push('--allow-no-subscriptions');
  if (shInteractive('az', args).code !== 0) die('az login failed. Log in manually then re-run: az login --tenant <tenant>');
  tokenCache.clear();
  const after = azDomains();
  ok(`az signed in${after.length ? ` (${after[0]})` : ''}.`);
}

async function selectEnv() {
  if (OPT.envId && OPT.envUrl) {
    ok(`Target env from args: ${OPT.envId} (${OPT.envUrl})`);
    return { id: OPT.envId, url: OPT.envUrl.endsWith('/') ? OPT.envUrl : OPT.envUrl + '/', name: '(from --env-url)' };
  }
  const manualEntry = async () => {
    const id = await ask('Environment ID (guid): ');
    const url = await ask('Org URL (https://<org>.crm.dynamics.com/): ');
    return { id: id.trim(), url: url.trim().endsWith('/') ? url.trim() : url.trim() + '/', name: '(manual)' };
  };
  log('Fetching environments for this profile...');
  const all = parsePacAdminList();
  if (!all.length) {
    warn('could not list environments — enter the target manually.');
    return manualEntry();
  }
  log(`${all.length} environments visible. You can filter by name/url substring.`);
  let envs = all;
  // With large tenants an unfiltered list is unusable — filter until it's short.
  while (envs.length > 30) {
    const f = await ask(`Filter (substring of name or url), or "id" to type an id/url directly: `);
    if (f.toLowerCase() === 'id') return manualEntry();
    const needle = f.toLowerCase();
    envs = all.filter((e) => e.name.toLowerCase().includes(needle) || e.url.toLowerCase().includes(needle));
    if (!envs.length) { warn('no matches; showing all again.'); envs = all; }
  }
  log('Select the TARGET environment (or the last option to type an id/url):');
  const picked = await chooseFromList(
    [...envs, { manual: true }],
    (e) => (e.manual ? '— enter an env id / url manually —' : `${e.name}  [${e.type}]  ${e.url}`),
    'Environment number: ',
  );
  if (picked.manual) return manualEntry();
  return picked;
}

// ============================================================================
// Steps
// ============================================================================
async function solutionPresent(orgUrl) {
  const r = await dv('GET', orgUrl, `solutions?$select=uniquename&$filter=uniquename eq '${manifest.solutionName}'`);
  return !!(r.json && r.json.value && r.json.value.length);
}

async function stepImport(env) {
  const zip = join(REPO_ROOT, manifest.solutionZip);
  if (!existsSync(zip)) die(`solution zip not found: ${zip}`);
  const { maxAttempts, backoffSeconds, pollMinutes } = manifest.import;
  log(`Importing ${manifest.solutionName} into ${env.url} (up to ${maxAttempts} attempts)`);
  for (let attempt = 1; ; attempt++) {
    console.log(`--- import attempt ${attempt}/${maxAttempts} ${new Date().toLocaleTimeString()} ---`);
    const r = sh('pac', ['solution', 'import', '--path', zip, '--environment', env.url,
      '--force-overwrite', '--publish-changes', '--max-async-wait-time', '60']);
    if (/imported|completed successfully|succeeded/i.test(r.out)) { ok('Solution imported + published (pac reported success).'); break; }

    if (/channel timed out|exceeded the allotted timeout/i.test(r.out)) {
      warn(`pac hit its client timeout; polling server-side for up to ${pollMinutes}m...`);
      const deadline = Date.now() + pollMinutes * 60_000;
      let found = false;
      while (Date.now() < deadline) { if (await solutionPresent(env.url)) { found = true; break; } await sleep(30_000); }
      if (found) { ok('Solution present server-side — import succeeded despite client timeout.'); break; }
    }
    if (await solutionPresent(env.url)) { ok('Solution present server-side — treating import as successful.'); break; }

    const capacity = r.out.match(/Unable to find an unassigned function app in '[^']*'/i);
    if (capacity) warn(`Custom-code compute pool exhausted (${capacity[0]}). Microsoft-side capacity — retrying.`);
    else console.log(r.out.split('\n').slice(-8).join('\n'));

    if (attempt >= maxAttempts) die(`solution import failed after ${maxAttempts} attempts`);
    warn(`retrying in ${backoffSeconds}s...`);
    await sleep(backoffSeconds * 1000);
  }
  log(`Waiting ${manifest.apimPropagationSeconds}s for APIM propagation`);
  await sleep(manifest.apimPropagationSeconds * 1000);
  ok('Import step complete.');
}

async function connectorModifiedOn(orgUrl, connectorId) {
  const r = await dv('GET', orgUrl, `connectors?$select=modifiedon&$filter=connectorid eq ${connectorId}`);
  return r.json && r.json.value && r.json.value[0] ? r.json.value[0].modifiedon : '';
}

async function stepConnectors(env) {
  log('Deploying each connector\'s inline custom-code from the repo');
  for (const c of manifest.connectors) {
    const dir = join(REPO_ROOT, manifest.connectorsDir, c.slug);
    const apiDef = join(dir, 'apiDefinition.json');
    const apiProps = join(dir, 'apiProperties.json');
    const script = join(dir, 'script.csx');
    for (const f of [apiDef, apiProps, script]) if (!existsSync(f)) die(`missing bundled connector file: ${f}`);

    const before = await connectorModifiedOn(env.url, c.connectorId);
    if (!before) die(`[${c.displayName}] connector not found in target (id ${c.connectorId}). Did import succeed?`);

    let deployed = false;
    for (let attempt = 1; attempt <= 3 && !deployed; attempt++) {
      log(`[${c.displayName}] pac connector update (attempt ${attempt}; waiting for pac to finish, hang-guard ${Math.round(CONNECTOR_UPDATE_TIMEOUT_MS / 60000)}m)`);
      // Let pac run to NATURAL completion: it compiles/binds the custom-code execution
      // AFTER writing the modifiedon blob. Killing it early leaves the code deployed but
      // not executable (tools return an empty [{"jsonrpc":"2.0"}] envelope). The deadline
      // is only a hang guard; a timeout is a hard failure we retry, not a success.
      const r = await shAsyncRace('pac',
        ['connector', 'update', '--connector-id', c.connectorId, '--environment', env.url,
          '--api-definition-file', apiDef, '--api-properties-file', apiProps, '--script-file', script],
        { timeoutMs: CONNECTOR_UPDATE_TIMEOUT_MS });
      if (r.timedOut) { warn(`[${c.displayName}] pac connector update did not finish within the hang guard — killed and retrying`); await sleep(10_000); continue; }
      if (r.code !== 0) { warn(`[${c.displayName}] pac connector update exited ${r.code} — retrying`); await sleep(10_000); continue; }
      const after = await connectorModifiedOn(env.url, c.connectorId);
      if (after && after !== before) { ok(`[${c.displayName}] connector updated (modifiedon ${before} -> ${after})`); deployed = true; break; }
      warn(`[${c.displayName}] pac reported success but modifiedon did not advance (${before}) — retrying`);
      await sleep(10_000);
    }
    if (!deployed) warn(`[${c.displayName}] could not confirm a clean connector update. The tool may return an empty MCP response — re-run the connectors step.`);
  }
  ok('Connector code step complete.');
}

async function apiNameOfConnector(orgUrl, connectorId) {
  const r = await dv('GET', orgUrl, `connectors?$select=connectorinternalid&$filter=connectorid eq ${connectorId}`);
  return r.json && r.json.value && r.json.value[0] ? (r.json.value[0].connectorinternalid || '') : '';
}
async function connectedConnections(envId, apiName) {
  const tok = azToken(POWERAPPS);
  const url = `https://api.powerapps.com/providers/Microsoft.PowerApps/apis/${apiName}/connections?api-version=2016-11-01&$filter=environment eq '${envId}'`;
  const res = await fetch(url, { headers: { Authorization: `Bearer ${tok}` } });
  if (!res.ok) return [];
  const j = await res.json();
  return (j.value || []).filter((c) => (c.properties?.statuses || []).some((s) => s.status === 'Connected')).map((c) => c.name);
}
async function putConnection(envId, apiName, displayName) {
  const tok = azToken(POWERAPPS);
  const conn = randomUUID().replace(/-/g, '').toLowerCase();
  const url = `https://api.powerapps.com/providers/Microsoft.PowerApps/apis/${apiName}/connections/${conn}?api-version=2016-11-01&$filter=environment eq '${envId}'`;
  const body = { properties: { environment: { id: `/providers/Microsoft.PowerApps/environments/${envId}`, name: envId }, displayName } };
  const res = await fetch(url, { method: 'PUT', headers: { Authorization: `Bearer ${tok}`, 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  if (res.status === 200 || res.status === 201) return conn;
  throw new Error(`HTTP ${res.status}: ${await res.text()}`);
}

async function stepConnections(env) {
  log(`Ensuring a no-auth connection for ${manifest.connectors.length} connectors`);
  for (const c of manifest.connectors) {
    const apiName = await apiNameOfConnector(env.url, c.connectorId);
    if (!apiName) die(`could not resolve apiName for '${c.displayName}' (${c.connectorId}) — is it imported?`);
    const existing = (await connectedConnections(env.id, apiName))[0];
    if (existing) { ok(`${c.displayName} -> already Connected (${existing})`); continue; }
    let made = '';
    for (let attempt = 1; attempt <= 4 && !made; attempt++) {
      try { made = await putConnection(env.id, apiName, `${c.displayName} Connection`); }
      catch (e) { warn(`${c.displayName} connection attempt ${attempt} failed: ${String(e.message).slice(0, 160)}`); await sleep(15_000); }
    }
    if (!made) die(`connection create failed for '${c.displayName}' after retries`);
    ok(`${c.displayName} -> created connection ${made} (Connected)`);
  }
  ok('All connectors have a Connected no-auth connection.');
}

async function botIdOf(orgUrl, schemaName) {
  const r = await dv('GET', orgUrl, `bots?$select=botid&$filter=schemaname eq '${schemaName}'`);
  return r.json && r.json.value && r.json.value[0] ? r.json.value[0].botid : '';
}
async function publishBot(orgUrl, agent) {
  const id = await botIdOf(orgUrl, agent.schemaName);
  if (!id) die(`bot not found: ${agent.schemaName}`);
  const r = await dv('POST', orgUrl, `bots(${id})/Microsoft.Dynamics.CRM.PvaPublish`, {});
  if (r.status >= 400 || (r.json && r.json.error)) die(`publish failed for ${agent.displayName}: ${r.text.slice(0, 300)}`);
  ok(`published ${agent.displayName} (${id})`);
}
async function stepPublish(env) {
  log('Publishing connected (child) agents first');
  for (const a of manifest.agents.filter((a) => a.role === 'child')) await publishBot(env.url, a);
  log('Publishing parent agents');
  for (const a of manifest.agents.filter((a) => a.role === 'parent')) await publishBot(env.url, a);
  ok('All agents published.');
}

function stepManual(env) {
  const rows = manifest.agents.map((a) => `  ${a.displayName.padEnd(34)} ${a.mcp.join(', ')}`).join('\n');
  console.log(`
================================================================================
  MANUAL POST-INSTALL STEP (required — one time, in the UI)
================================================================================
The deploy is complete: solution imported, connector code deployed, no-auth
connections Connected, and all agents published. One manual step has no supported
API — re-attach each agent's MCP server in the authoring UI.

Open Copilot Studio:  https://copilotstudio.microsoft.com/
Environment:          ${env.id}
  org URL:            ${env.url}

For EACH agent below: open it -> Tools -> remove the listed MCP server -> Add a
tool -> Model Context Protocol -> pick the connector and choose the EXISTING
Connected connection (do not create a new one) -> Save -> Publish.

  Agent                              MCP server(s) to remove + re-add
  ---------------------------------  --------------------------------
${rows}

Connections already exist for every connector, so just select the existing one
when re-adding. After re-attaching, the tools load and both scenarios work.
================================================================================
`);
}

// ============================================================================
// Main
// ============================================================================
const STEPS = ['import', 'connectors', 'connections', 'publish', 'manual'];
async function main() {
  if (!STEPS.includes(OPT.startAt)) die(`--start-at must be one of: ${STEPS.join(', ')}`);
  log('BlastBox Omega deploy (cross-OS). This deploys into an EXISTING environment.');

  // preflight: pac runnable (az is handled after profile select, see ensureAz)
  if (sh('pac', ['auth', 'list']).code !== 0) die('pac is not runnable. Install pac and set DOTNET_ROOT.');

  await selectProfile();
  await ensureAz();
  const env = await selectEnv();

  // verify we can mint a Dataverse token for this env (right az tenant)
  azToken(dvResource(env.url));
  ok(`Ready to deploy into: ${env.name} ${env.url}`);
  if (!(await confirm('Proceed?'))) die('aborted by user');

  const from = STEPS.indexOf(OPT.startAt);
  if (from <= 0) await stepImport(env);
  if (from <= 1) await stepConnectors(env);
  if (from <= 2) await stepConnections(env);
  if (from <= 3) await stepPublish(env);

  ok(`DEPLOY COMPLETE — ${env.url}`);
  stepManual(env);
}

main().catch((e) => die(e.stack || String(e)));
