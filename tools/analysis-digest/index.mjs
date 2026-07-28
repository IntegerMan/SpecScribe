#!/usr/bin/env node
// Story 25.4 — the agent-consumable findings channel.
//
// Fetches this repository's current SonarCloud findings and writes them to
// `.specscribe/analysis/` as an ADR 0023 `AnalysisObservation` digest: one small index
// plus one shard per source file, so an agent reads only the files it is about to touch.
//
// DEV-TIME ONLY. Nothing here runs during `specscribe generate`, nothing here is imported
// by `src/SpecScribe`, and nothing here writes into the generated site. The golden
// fingerprint cannot move because of this file.
//
// NO TOKEN, ANYWHERE. `IntegerMan_SpecScribe` is a public free-tier project and every
// endpoint below answers anonymously — the same credential-free method
// `docs/SonarCloudSetup.md` § Triaging findings already documents. This script never reads
// an environment token, never prompts for one, and never writes one. (NFR12.)
//
// Usage:
//   node tools/analysis-digest/index.mjs                  refresh the digest
//   node tools/analysis-digest/index.mjs --check-staleness <rev>
//                                                         print the provenance block that
//                                                         WOULD be emitted for <rev>, write
//                                                         nothing (verification affordance)
//   node tools/analysis-digest/index.mjs --help

import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync, rmSync, renameSync, existsSync } from 'node:fs';
import { dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

// --- configuration ---------------------------------------------------------------------
// Constants, not credentials. Overridable so the tool is testable against another public
// project without editing it; there is deliberately no token knob to override.
const PROJECT = process.env.SPECSCRIBE_SONAR_PROJECT || 'IntegerMan_SpecScribe';
// `organization` is REQUIRED on api/rules/show — omitting it returns an error, not a rule.
const ORGANIZATION = process.env.SPECSCRIBE_SONAR_ORG || 'integerman-github';
const BASE = 'https://sonarcloud.io/api';
const PAGE_SIZE = 500;
// Sonar enforces a hard `p * ps <= 10000` ceiling and returns an error past it. At 1,488
// issues that is 3 pages against a 20-page budget. This is asserted rather than assumed so a
// future volume increase FAILS LOUDLY instead of silently truncating the digest.
const MAX_OFFSET = 10000;
const FETCH_TIMEOUT_MS = 30000;
const RULE_FETCH_CONCURRENCY = 4; // be polite: not 86 parallel requests
// Bump whenever the SHAPE of a cached rule value changes. A cache that survives a shape
// change silently serves the old shape forever — this bit already caught a stale `helpUri`
// during development, so it earns its keep.
const RULE_CACHE_VERSION = 2;

// --- the ADR 0023 severity model -------------------------------------------------------
// Ported from `spike/findings/map_to_model.py` (Story 25.3 evidence). That file is throwaway
// reference, NOT a dependency: nothing here shells out to it, imports it, or reads it.

/** Normalized scale, ascending. SARIF 2.1.0 `result.level` verbatim (ADR 0023 Decision 3). */
const SEVERITY = ['none', 'note', 'warning', 'error'];

/**
 * MQR (`impacts[]`) severity -> normalized. THIS is the axis the normalizer reads.
 * Sonar has frozen the legacy fields, and the two axes disagree on 54.6% of this repo's
 * issues — normalizing from the legacy axis reorders the majority of the backlog.
 * Collapse cost, stated rather than hidden: BLOCKER and HIGH both become `error`, so the
 * single BLOCKER on this repo is invisible at normalized granularity and survives only in
 * `severity.provider`. That is the price of an externally-specified scale, paid knowingly.
 */
const MQR_TO_NORM = {
  BLOCKER: 'error',
  HIGH: 'error',
  MEDIUM: 'warning',
  LOW: 'note',
  INFO: 'note',
};

/** Legacy (frozen) axis -> normalized. Used ONLY as a recorded fallback when `impacts` is absent. */
const LEGACY_TO_NORM = {
  BLOCKER: 'error',
  CRITICAL: 'error',
  MAJOR: 'warning',
  MINOR: 'note',
  INFO: 'note',
};

/**
 * Mandatory text label per level. UX-DR17 (severity is NEVER signalled by color alone) is
 * satisfied by the CONTRACT — the label ships in the payload so no surface can forget it.
 */
const SEVERITY_LABEL = { error: 'Error', warning: 'Warning', note: 'Note', none: 'None' };

const SCHEMA = {
  name: 'specscribe.analysis-observation',
  version: '1.0.0',
  profileOf: 'sarif-2.1.0',
  adr: 'docs/adrs/0023-agent-facing-analysis-observation-contract.md',
};

// --- small helpers ---------------------------------------------------------------------

const HERE = dirname(fileURLToPath(import.meta.url));

function repoRoot() {
  try {
    return execFileSync('git', ['rev-parse', '--show-toplevel'], {
      cwd: HERE,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
  } catch {
    return resolve(HERE, '..', '..'); // tools/analysis-digest -> repo root
  }
}

function git(root, args) {
  try {
    return execFileSync('git', args, {
      cwd: root,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
  } catch {
    return null; // not computable — every caller treats null as "unknown", never as "fine"
  }
}

async function getJson(url) {
  const res = await fetch(url, {
    headers: { Accept: 'application/json' },
    signal: AbortSignal.timeout(FETCH_TIMEOUT_MS),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status} ${res.statusText} for ${url}`);
  return res.json();
}

// --- fetch -----------------------------------------------------------------------------

/**
 * All UNRESOLVED issues. `resolved=false` is mandatory: an unfiltered response includes
 * CLOSED issues on paths the `sonar.exclusions` list removed — 1,598 against 1,420 on
 * 2026-07-27 — so omitting it triages ~180 issues that no longer exist.
 */
async function fetchIssues() {
  const issues = [];
  let total = null;
  for (let page = 1; ; page++) {
    if (page * PAGE_SIZE > MAX_OFFSET) {
      throw new Error(
        `Sonar's p*ps<=${MAX_OFFSET} ceiling reached at page ${page} (${issues.length} of ${total} fetched). ` +
          `The digest would be TRUNCATED. Narrow the query or page by a facet — do not raise ps.`
      );
    }
    const url =
      `${BASE}/issues/search?componentKeys=${encodeURIComponent(PROJECT)}` +
      `&resolved=false&ps=${PAGE_SIZE}&p=${page}`;
    const body = await getJson(url);
    total = body.total ?? body.paging?.total ?? null;
    issues.push(...(body.issues ?? []));
    if (!body.issues?.length || (total !== null && issues.length >= total)) break;
  }
  if (total !== null && issues.length !== total) {
    throw new Error(`Paging mismatch: fetched ${issues.length} but Sonar reported ${total}.`);
  }
  return issues;
}

/** Newest analysis: its `revision` is the ONLY honest staleness anchor (ADR 0023 Decision 6). */
async function fetchLatestAnalysis() {
  const body = await getJson(
    `${BASE}/project_analyses/search?project=${encodeURIComponent(PROJECT)}&ps=1`
  );
  const a = body.analyses?.[0];
  if (!a) throw new Error('api/project_analyses/search returned no analyses.');
  return { revision: a.revision ?? null, date: a.date ?? null };
}

/**
 * `rule.name` and `helpUri` are ABSENT from api/issues/search — each distinct rule costs a
 * separate api/rules/show call. Metadata is near-static, so the cache is effectively
 * permanent and only NEW rules cost anything on a refresh.
 */
async function fetchRules(ruleKeys, cache) {
  const missing = ruleKeys.filter((k) => !cache[k]);
  const out = { ...cache };
  let cursor = 0;
  const worker = async () => {
    while (cursor < missing.length) {
      const key = missing[cursor++];
      try {
        const body = await getJson(
          `${BASE}/rules/show?organization=${encodeURIComponent(ORGANIZATION)}` +
            `&key=${encodeURIComponent(key)}`
        );
        const r = body.rule ?? {};
        out[key] = {
          // Verbatim. For `external_roslyn:*` Sonar's own name is literally "roslyn:CA1310" —
          // not a human sentence. That is the provider's value and it is carried as-is;
          // inventing a nicer one would be fabricating provider data. The observation's
          // `message` carries the descriptive text in those cases.
          name: r.name ?? null,
          // Sonar exposes NO helpUri field on api/rules/show (verified: the payload has 24
          // keys and none of them is a URL). The rule's permalink in this organization is
          // used instead — verified HTTP 200, and it resolves for every rule repo present
          // here (csharpsquid, external_roslyn, javascript, typescript, css, Web,
          // jssecurity). A per-vendor guess like rules.sonarsource.com/<lang>/RSPEC-<n> was
          // rejected: it could not be verified from this machine and it does not cover the
          // CS*/SYSLIB*/xUnit* keys at all.
          // `?open=` alone is enough (verified 200); the `&rule_key=` duplicate the Sonar UI
          // adds costs ~45 B on EVERY observation and buys nothing.
          helpUri:
            `https://sonarcloud.io/organizations/${encodeURIComponent(ORGANIZATION)}/rules` +
            `?open=${encodeURIComponent(key)}`,
          repo: r.repo ?? null,
          lang: r.langName ?? r.lang ?? null,
        };
      } catch (err) {
        // A rule we cannot describe is a rule we describe as unknown — never a failed run,
        // and never a silently blank name that reads as "this rule has no name".
        out[key] = { name: null, helpUri: null, unresolved: String(err.message || err) };
      }
    }
  };
  await Promise.all(
    Array.from({ length: Math.min(RULE_FETCH_CONCURRENCY, Math.max(missing.length, 1)) }, worker)
  );
  return { rules: out, fetched: missing.length };
}

// --- mapping: SonarCloud issue -> AnalysisObservation -----------------------------------

function normalizePath(component) {
  // Sonar's `component` is `PROJECT:path`. A component with NO ':' is a project-level issue
  // with no file at all — it is routed to the unlocated shard, never dropped.
  const s = String(component ?? '');
  const i = s.indexOf(':');
  if (i < 0) return null;
  const p = s.slice(i + 1).replace(/\\/g, '/');
  return p.length ? p : null;
}

function fromSonar(issue, rules) {
  const path = normalizePath(issue.component);
  const tr = issue.textRange ?? {};

  // `flows[]` is flows-OF-locations (two levels). Flatten to SARIF's own flat shape.
  // NO CAP HERE — capping is a surface concern (Story 26.4), and if a surface ever caps it
  // MUST emit an explicit truncation count. Silent truncation is forbidden (ADR 0023 D4).
  const relatedLocations = [];
  for (const flow of issue.flows ?? []) {
    for (const loc of flow.locations ?? []) {
      const ltr = loc.textRange ?? {};
      relatedLocations.push({
        path: normalizePath(loc.component) ?? loc.component ?? null,
        startLine: ltr.startLine ?? null,
        startColumn: ltr.startOffset ?? null,
        endLine: ltr.endLine ?? null,
        endColumn: ltr.endOffset ?? null,
        message: loc.msg ?? null,
      });
    }
  }

  // The ARRAY question: keep every provider pair verbatim, derive the normalized level from
  // the MAX so a multi-impact observation can never normalize below its worst quality.
  // 14 live issues carry TWO impacts, so a scalar field is lossy TODAY, not hypothetically.
  const impacts = issue.impacts ?? [];
  let normalized;
  const provider = [];
  let severityFallback = null;
  if (impacts.length) {
    normalized = impacts
      .map((i) => MQR_TO_NORM[i.severity] ?? 'note')
      .reduce((a, b) => (SEVERITY.indexOf(b) > SEVERITY.indexOf(a) ? b : a), 'none');
    // ⚠ SONAR RETURNS `impacts[]` IN NON-DETERMINISTIC ORDER. Measured 2026-07-28: the same
    // issue came back as [MAINTAINABILITY, RELIABILITY] on one fetch and [RELIABILITY,
    // MAINTAINABILITY] on the next, flipping 7 shards between two states on otherwise
    // identical input. `severity.normalized` is a MAX so it is already order-independent, but
    // the carried array is not — so it is sorted here. The order of a set of impact pairs has
    // no meaning, so this is lossless.
    //
    // This matters well beyond a tidy diff: Story 26.4 puts this shape into the Epic 22 IR,
    // which IS covered by the golden fingerprint. Unsorted, those 14 multi-impact issues would
    // make the fingerprint flap at random with no source change.
    const sorted = [...impacts].sort(
      (a, b) =>
        String(a.softwareQuality).localeCompare(String(b.softwareQuality)) ||
        String(a.severity).localeCompare(String(b.severity))
    );
    for (const i of sorted) {
      provider.push({ axis: 'mqr', softwareQuality: i.softwareQuality, severity: i.severity });
    }
  } else {
    // Recorded loss, never a silent one: `impacts` was verified present on 1,488/1,488 issues
    // on 2026-07-28. If it ever goes missing we fall back to the FROZEN legacy axis and SAY SO.
    normalized = LEGACY_TO_NORM[issue.severity] ?? 'note';
    severityFallback = 'impacts[] absent — normalized from the FROZEN legacy axis';
  }
  // The legacy pair rides along verbatim in every case: the two axes disagree on 54.6% of
  // issues, so dropping either makes two surfaces order the backlog differently by design.
  provider.push({ axis: 'legacy', severity: issue.severity ?? null, type: issue.type ?? null });

  const ruleId = issue.rule ?? null; // already "{repo}:{id}"
  const meta = (ruleId && rules[ruleId]) || {};

  const obs = {
    provider: 'sonarcloud',
    // SARIF's separate classification axis, PINNED rather than left undefined the way Sonar
    // left its two severity axes (ADR 0023 Decision 3).
    kind: 'fail',
    rule: {
      id: ruleId,
      // Inlined per observation, deliberately: ADR 0023 Decision 3 rejected raw SARIF partly
      // BECAUSE a `result` carries only a `ruleIndex` into an out-of-line catalogue and is
      // therefore not self-describing. A single observation handed to an agent must stand alone.
      name: meta.name ?? null,
      helpUri: meta.helpUri ?? null,
    },
    severity: {
      normalized,
      label: SEVERITY_LABEL[normalized],
      provider,
    },
    location: {
      path,
      startLine: tr.startLine ?? issue.line ?? null,
      startColumn: tr.startOffset ?? null,
      endLine: tr.endLine ?? null,
      endColumn: tr.endOffset ?? null,
    },
    relatedLocations,
    message: issue.message ?? null,
    // ADR 0023 Decision 5 mandates the BLOCK with a non-nullable `basis`; it does not mandate
    // computing the join. Story 25.4 D5: attachment is declared unavailable and NOT computed.
    // Computing it needs `generate --deep-git` and the 10x fan-out bounding rule is explicitly
    // Story 26.5's design decision and the owner's to approve. `unavailable` means
    // "not computed here", which is a different fact from `none` ("genuinely unattached").
    attachment: { basis: 'unavailable', entities: [], confidence: null, entityCount: 0 },
  };
  if (severityFallback) obs.severity.fallback = severityFallback;
  return obs;
}

// Deliberately NOT carried, so nobody re-adds them thinking they were an oversight:
//   assignee            — no people scoreboard (a standing project rule).
//   key                 — server-assigned and NOT stable across re-analysis of a moved line;
//                         carrying it would imply an identity it does not have.
//   hash                — Sonar's line-content hash, not portable.
//   effort / debt       — Sonar-specific, no analogue in the other proven provider.
//   cleanCodeAttribute  — Sonar-only taxonomy; making it structural would make the model
//                         Sonar-shaped, which is the one thing ADR 0023 exists to prevent.

// --- provenance ------------------------------------------------------------------------

/**
 * Revision-first staleness. A TIMESTAMP CANNOT ANSWER THIS and on live data actively
 * misleads: Story 25.3 observed an analysis whose timestamp read "an hour ago" while its
 * revision was two commits behind HEAD.
 *
 * `isStale` FAILS CLOSED — it is `true` whenever it cannot be computed. A staleness field
 * that fails open defeats its own purpose.
 */
function buildProvenance(root, analysis) {
  const workingTreeRevision = git(root, ['rev-parse', 'HEAD']);
  const porcelain = git(root, ['status', '--porcelain']);
  const workingTreeDirty = porcelain === null ? true : porcelain.length > 0;

  let commitsBehind = null;
  if (analysis.revision && workingTreeRevision) {
    const n = git(root, ['rev-list', '--count', `${analysis.revision}..${workingTreeRevision}`]);
    // null when not computable — e.g. the analysis revision simply does not exist locally on
    // a shallow or unfetched clone. Unknown is recorded as unknown, never as zero.
    commitsBehind = n !== null && /^\d+$/.test(n) ? Number(n) : null;
  }

  const staleReasons = [];
  if (!analysis.revision) staleReasons.push('analysis-revision-unknown');
  if (!workingTreeRevision) staleReasons.push('working-tree-revision-unknown');
  if (commitsBehind === null && analysis.revision && workingTreeRevision) {
    staleReasons.push('commits-behind-not-computable');
  }
  if (commitsBehind !== null && commitsBehind > 0) staleReasons.push('analysis-behind-working-tree');
  // A dirty tree IS a staleness condition: every line number below is anchored to
  // `analysisRevision`, and uncommitted edits move them. It is listed as its own reason so a
  // consumer can tell "the analysis is old" from "your edits have moved the lines".
  if (workingTreeDirty) staleReasons.push('working-tree-dirty');

  return {
    provider: 'sonarcloud',
    project: PROJECT,
    analysisRevision: analysis.revision,
    analysisDate: analysis.date,
    workingTreeRevision,
    workingTreeDirty,
    // READ-TIME STALENESS. A frozen `isStale: false` becomes a LIE the moment the next commit
    // lands. The consumer rule, stated in CLAUDE.md: if `git rev-parse HEAD` differs from
    // `evaluatedAtRevision`, the digest is stale REGARDLESS of what `isStale` says.
    evaluatedAtRevision: workingTreeRevision,
    isStale: staleReasons.length > 0,
    staleReasons,
    commitsBehind,
    attachment: {
      // The digest-level counterpart of every record's `attachment.basis`.
      basis: 'unavailable',
      reason:
        'Story 25.4 D5: the code->planning join is not computed here. It requires ' +
        '`generate --deep-git` and its ~10x fan-out bounding rule is Story 26.5\'s decision.',
    },
  };
}

// --- shard layout ----------------------------------------------------------------------

const UNSAFE_SEGMENT = /^(\.|\.\.)$/;

/** Repo-relative source path -> shard path, or null when the path is not safe to write. */
function shardFor(path) {
  if (!path) return null;
  if (/^[A-Za-z]:/.test(path) || path.startsWith('/')) return null; // absolute — never
  const segments = path.split('/');
  if (segments.some((s) => s.length === 0 || UNSAFE_SEGMENT.test(s))) return null;
  return `files/${path}.json`;
}

function emptyByLevel() {
  return { error: 0, warning: 0, note: 0, none: 0 };
}

function tally(into, level) {
  into[level] = (into[level] ?? 0) + 1;
}

/**
 * Per-file `byLevel` maps omit zero counts. THE CONTRACT RULE, stated once and applied
 * everywhere below the top level: **a level absent from a `byLevel` map means zero.**
 * `totals.byLevel` deliberately keeps all four keys — it is the one place a zero is itself
 * informative, and it is a single object rather than 201 of them.
 */
function pruneByLevel(byLevel) {
  const out = {};
  for (const level of ['error', 'warning', 'note', 'none']) {
    if (byLevel[level]) out[level] = byLevel[level];
  }
  return out;
}

/**
 * Pretty to `maxDepth`, compact below it. One observation per line, one file-index entry per
 * line — diffable and skimmable — without paying the ~2x that fully indenting 1,488 nested
 * records costs. That cost is not theoretical: fully-indented shards measured 2.11 MB total
 * with a 137 KB worst case, against 1.10 MB / 71 KB this way.
 */
function stringify(value, depth = 0, maxDepth = 2) {
  if (depth >= maxDepth || value === null || typeof value !== 'object') return JSON.stringify(value);
  const pad = ' '.repeat(depth + 1);
  const close = ' '.repeat(depth);
  if (Array.isArray(value)) {
    if (!value.length) return '[]';
    return `[\n${value.map((v) => pad + stringify(v, depth + 1, maxDepth)).join(',\n')}\n${close}]`;
  }
  const keys = Object.keys(value);
  if (!keys.length) return '{}';
  const body = keys
    .map((k) => `${pad}${JSON.stringify(k)}: ${stringify(value[k], depth + 1, maxDepth)}`)
    .join(',\n');
  return `{\n${body}\n${close}}`;
}

// --- main ------------------------------------------------------------------------------

function printHelp() {
  console.log(`specscribe analysis-digest — Story 25.4

  node tools/analysis-digest/index.mjs
      Refresh .specscribe/analysis/ from SonarCloud (anonymous; no token).

  node tools/analysis-digest/index.mjs --check-staleness <revision>
      Print the provenance block that WOULD be emitted if the latest analysis sat at
      <revision>. Writes nothing. Exists so the stale path can be exercised on a repo
      whose analysis happens to be current.

  node tools/analysis-digest/index.mjs --help

The digest is gitignored (.gitignore's \`.specscribe\` entry, ADR 0014) and is dev-time
tooling only: it touches no generated output, so the golden fingerprint cannot move.`);
}

async function main() {
  const argv = process.argv.slice(2);
  if (argv.includes('--help') || argv.includes('-h')) {
    printHelp();
    return 0;
  }

  const root = repoRoot();
  const outDir = join(root, '.specscribe', 'analysis');

  const checkIdx = argv.indexOf('--check-staleness');
  if (checkIdx >= 0) {
    const rev = argv[checkIdx + 1];
    if (!rev) {
      console.error('--check-staleness needs a revision.');
      return 1;
    }
    const prov = buildProvenance(root, { revision: rev, date: '(hypothetical)' });
    console.log(JSON.stringify(prov, null, 2));
    return 0;
  }

  // ---- fetch. EVERY network failure lands here, and the digest on disk is untouched. ----
  let issues, analysis, ruleResult;
  const cachePath = join(outDir, '.rules-cache.json');
  let cache = {};
  if (existsSync(cachePath)) {
    try {
      const raw = JSON.parse(readFileSync(cachePath, 'utf8'));
      // A corrupt OR out-of-version cache costs 86 calls, never a wrong digest.
      cache = raw?.cacheVersion === RULE_CACHE_VERSION ? (raw.rules ?? {}) : {};
    } catch {
      cache = {};
    }
  }
  try {
    analysis = await fetchLatestAnalysis();
    issues = await fetchIssues();
    const ruleKeys = [...new Set(issues.map((i) => i.rule).filter(Boolean))].sort();
    ruleResult = await fetchRules(ruleKeys, cache);
  } catch (err) {
    // ⚠ THE most important behavior in this file. On ANY fetch failure we leave the existing
    // digest exactly as it was, print one line, and exit 0. We NEVER write an empty or
    // partial digest, because an empty digest reads as "this code is clean" — the single most
    // dangerous output this tool could produce. Absent is not clean; absent is UNKNOWN.
    const had = existsSync(join(outDir, 'index.json'));
    console.log(
      `analysis-digest: could not reach SonarCloud (${err.message || err}). ` +
        (had
          ? 'The existing digest was left untouched — check its provenance before trusting it.'
          : 'No digest was written. Absent means UNKNOWN, not clean.')
    );
    return 0;
  }

  const rules = ruleResult.rules;
  const provenance = buildProvenance(root, analysis);

  // ---- map ----
  const observations = issues.map((i) => fromSonar(i, rules));

  const files = new Map(); // path -> {count, byLevel, shard, observations}
  const unlocated = [];
  for (const obs of observations) {
    const path = obs.location.path;
    const shard = shardFor(path);
    if (!path || !shard) {
      // Project-level issues (no `:` in the component) and any path unsafe to write are a
      // ROUTED POPULATION, never a residue. They are counted in totals like everything else.
      if (path && !shard) obs.unroutedReason = 'path-not-safe-for-shard-filesystem-layout';
      else obs.unroutedReason = 'project-level-issue-with-no-file';
      unlocated.push(obs);
      continue;
    }
    let entry = files.get(path);
    if (!entry) {
      entry = { count: 0, byLevel: emptyByLevel(), shard, observations: [] };
      files.set(path, entry);
    }
    entry.count++;
    tally(entry.byLevel, obs.severity.normalized);
    entry.observations.push(obs);
  }

  // Sort each shard's observations by source position. Two reasons: a reader walking a file
  // wants them in line order, and it removes any dependence on the API's result ordering.
  // (`relatedLocations` is deliberately NOT sorted — a flow is an ordered sequence.)
  for (const entry of files.values()) {
    entry.observations.sort(
      (a, b) =>
        (a.location.startLine ?? 0) - (b.location.startLine ?? 0) ||
        (a.location.startColumn ?? 0) - (b.location.startColumn ?? 0) ||
        String(a.rule.id).localeCompare(String(b.rule.id)) ||
        String(a.message).localeCompare(String(b.message))
    );
  }

  const totals = { observations: observations.length, files: files.size, byLevel: emptyByLevel() };
  for (const obs of observations) tally(totals.byLevel, obs.severity.normalized);

  const unlocatedByLevel = emptyByLevel();
  for (const obs of unlocated) tally(unlocatedByLevel, obs.severity.normalized);

  // ---- write ATOMICALLY: build a temp tree, then swap. An interrupted run must never leave
  // a half-written digest that an agent reads as authoritative. ----
  const tmpDir = join(root, '.specscribe', `analysis.tmp-${process.pid}`);
  const oldDir = join(root, '.specscribe', `analysis.old-${process.pid}`);
  rmSync(tmpDir, { recursive: true, force: true });
  mkdirSync(tmpDir, { recursive: true });

  const write = (rel, value) => {
    const dest = join(tmpDir, rel.split('/').join(sep));
    const text = stringify(value) + '\n';
    mkdirSync(dirname(dest), { recursive: true });
    writeFileSync(dest, text, 'utf8');
    return Buffer.byteLength(text, 'utf8');
  };

  // Every shard carries the FULL provenance block. This is deliberate and it is the reason
  // the mirrored layout is safe: an agent constructs a shard path from the file it is about
  // to touch and reads it WITHOUT the index, so a shard that could not report its own
  // staleness would be a shard that lies by omission.
  const shardProvenance = provenance;

  let shardBytes = 0;
  const shardSizes = [];
  const fileIndex = {};
  const unroutedAtWrite = [];
  for (const [path, entry] of [...files.entries()].sort(([a], [b]) => (a < b ? -1 : 1))) {
    try {
      const n = write(entry.shard, {
        schema: SCHEMA,
        path,
        count: entry.count,
        byLevel: pruneByLevel(entry.byLevel),
        provenance: shardProvenance,
        observations: entry.observations,
      });
      shardBytes += n;
      shardSizes.push(n);
      fileIndex[path] = {
        count: entry.count,
        byLevel: pruneByLevel(entry.byLevel),
        shard: entry.shard,
      };
    } catch (err) {
      // A shard we cannot write (path length, reserved name, permissions) is REROUTED to
      // unlocated with the reason recorded — never silently dropped.
      for (const obs of entry.observations) {
        obs.unroutedReason = `shard-write-failed: ${err.code || err.message}`;
        unroutedAtWrite.push(obs);
      }
    }
  }
  for (const obs of unroutedAtWrite) {
    unlocated.push(obs);
    tally(unlocatedByLevel, obs.severity.normalized);
    delete fileIndex[obs.location.path];
  }
  if (unroutedAtWrite.length) {
    totals.files = Object.keys(fileIndex).length;
    console.log(
      `analysis-digest: ${unroutedAtWrite.length} observation(s) could not be sharded and were ` +
        `routed to unlocated.json with the reason recorded.`
    );
  }

  const unlocatedBytes = write('unlocated.json', {
    schema: SCHEMA,
    count: unlocated.length,
    byLevel: pruneByLevel(unlocatedByLevel),
    provenance: shardProvenance,
    // "Unlocated" is a routed population with a designed destination (Story 26.6's analysis
    // hub), not a leftover. An empty array here means "there were none", which is a fact.
    observations: unlocated,
  });

  const index = {
    schema: SCHEMA,
    provenance,
    totals: { ...totals, unlocated: unlocated.length },
    // Path -> shard. The shard path is also DERIVABLE (`files/<path>.json`), so an agent that
    // already knows which file it is touching can skip the index entirely; the explicit field
    // exists so the derivation never has to be guessed if a path ever needs escaping.
    files: fileIndex,
    unlocatedShard: 'unlocated.json',
  };
  const indexBytes = write('index.json', index);
  write('.rules-cache.json', { cacheVersion: RULE_CACHE_VERSION, rules });

  // swap
  if (existsSync(outDir)) renameSync(outDir, oldDir);
  mkdirSync(dirname(outDir), { recursive: true });
  renameSync(tmpDir, outDir);
  rmSync(oldDir, { recursive: true, force: true });

  const shardCount = Object.keys(fileIndex).length;
  console.log(
    `analysis-digest: ${totals.observations} observations (${totals.byLevel.error} error, ` +
      `${totals.byLevel.warning} warning, ${totals.byLevel.note} note, ${totals.byLevel.none} none) ` +
      `across ${shardCount} shards + ${unlocated.length} unlocated.`
  );
  console.log(
    `  index ${indexBytes} B | shards ${shardBytes} B total, median ${median(shardSizes)} B, ` +
      `max ${Math.max(0, ...shardSizes)} B | unlocated ${unlocatedBytes} B | ` +
      `${ruleResult.fetched} rule(s) fetched, ${Object.keys(rules).length} cached`
  );
  console.log(
    `  provenance: analysis ${short(provenance.analysisRevision)} (${provenance.analysisDate}) | ` +
      `tree ${short(provenance.workingTreeRevision)}${provenance.workingTreeDirty ? ' DIRTY' : ''} | ` +
      `commitsBehind ${provenance.commitsBehind} | isStale ${provenance.isStale}` +
      (provenance.staleReasons.length ? ` [${provenance.staleReasons.join(', ')}]` : '')
  );
  return 0;
}

function short(sha) {
  return sha ? String(sha).slice(0, 7) : '(unknown)';
}

function median(nums) {
  if (!nums.length) return 0;
  const s = [...nums].sort((a, b) => a - b);
  const m = Math.floor(s.length / 2);
  return s.length % 2 ? s[m] : Math.round((s[m - 1] + s[m]) / 2);
}

process.exitCode = await main();
