#!/usr/bin/env node
// `npm run pin:parity` — regenerates the PINNED content-drift corpus and its oracle. [Story 23.6 AC #3]
//
// This is the regeneration command ADR 0033 §Decision 3 requires: "an owner-runnable regeneration path that
// is a command, not a constant-bump … a deliberate, reviewable act producing a reviewable diff". It rewrites
// two committed artifacts:
//
//   web/fixtures/parity-corpus/spa/   the PINNED IR — the renderer's input, frozen
//   web/measurements/parity-pinned.json   the per-route oracle `check:parity` reads back
//
// ── Why the corpus is pinned rather than measured over the live site ────────────────────────────────────
//
// See `parity-lib.mjs`'s header for the full finding. In short: over this repository's own docs the IR is
// the renderer's input AND the region passes through verbatim, so a live content digest cannot tell "the
// content moved" from "the renderer moved". Freezing the input makes every digest move a rendering change by
// construction — and means a sibling story editing a doc can never turn this gate red.
//
// ── What is captured, and the lineage assertion that runs at pin time ───────────────────────────────────
//
// Per route:
//   mainSha  the normalized `<main>` region. Story 23.4 proved the composed region byte-equal to C#'s own
//            rendered page across 1,469 pages, so this value IS what C# produced. ⚠️ THIS SCRIPT ASSERTS
//            THAT EQUALITY AGAINST THE LIVE GOLDEN PAGE while the C# writer still exists — after Story 23.6
//            Task 6 there is no golden side left to check against, so pinning without proving it here would
//            record an unverified number and call it lineage. Pass `--no-lineage` only once the writer is
//            gone, and the oracle then carries `lineage: "carried-forward"` rather than "verified".
//   pageSha  the normalized WHOLE PAGE, from the RENDERER. The old oracle hashed `<main>` only and was
//            therefore blind to `<title>`, meta, the favicon, the footer, `<script src>` tags, the nav
//            toggle, the Mermaid init and the Hierarchy/Graph anti-flash handshakes — exactly what
//            `HtmlRenderAdapter.Render` emitted and Task 6 deletes. Pinned from the renderer, not from C#,
//            because the two were never claimed to agree on chrome.
//
// Usage:
//   npm run pin:parity                    regenerate corpus + oracle (asserts the C# lineage)
//   npm run pin:parity -- --bootstrap     also re-select the route list (rare: only when families change)
//   npm run pin:parity -- --no-lineage    skip the golden comparison (only valid after the writer is deleted)
//   npm run pin:parity -- --oracle-only   re-pin the DIGESTS against the corpus already committed, leaving
//                                         fixtures/parity-corpus/ untouched. The right command when a
//                                         rendering change moved the chrome and the frozen input did not
//                                         move — the diff then shows only what the renderer did. Needs no
//                                         live IR, so no `dotnet run … generate` first.
//
// Zero npm dependencies (ADR 0010).

import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { MEASUREMENTS_DIR, mainRegion, normalizeVolatile, pad, readOrNull } from './harness-lib.mjs'
import { composeIrMain, foldBuildAssets, parityDigest } from './parity-lib.mjs'
import { withRenderer } from './render-lib.mjs'
import { resolveFamily } from '../ir/families.ts'

const argv = process.argv.slice(2)
const BOOTSTRAP = argv.includes('--bootstrap')
const NO_LINEAGE = argv.includes('--no-lineage')
// `--oracle-only` — re-pin the DIGESTS against the corpus already committed, leaving fixtures/parity-corpus/
// byte-untouched.
//
// The stock run rebuilds the fixture from the live IR, which is right when the corpus itself is being
// re-chosen and WRONG when only the renderer moved: it folds however much unrelated repo-content churn has
// accumulated since the last pin into the same diff, so the one thing the diff is supposed to show — what the
// rendering change did — is no longer separable from it. It also silently re-anchors `mainSha` onto fresh
// content, discarding the Story 23.4 lineage those numbers carry, and after Story 23.6 deleted the C# writer
// there is no golden side left to re-prove them against.
//
// So this flag exists for the common case: a Vue/SFC/CSS change moved the chrome, the frozen input did not
// move, and the honest edit is to the oracle alone. `mainSha` is still RECOMPUTED (never copied), and the
// lineage label below is kept only if every one of those numbers comes back byte-identical.
const ORACLE_ONLY = argv.includes('--oracle-only')

if (ORACLE_ONLY && BOOTSTRAP) {
  console.error('\n--oracle-only and --bootstrap are contradictory: one freezes the corpus, the other re-selects it.\n')
  process.exit(1)
}

const FIXTURES = resolve(process.cwd(), 'fixtures')
const ROUTE_LIST = join(FIXTURES, 'parity-corpus.routes.json')
const CORPUS = join(FIXTURES, 'parity-corpus')
const ORACLE = join(MEASUREMENTS_DIR, 'parity-pinned.json')
const ARTEFACT = resolve(process.cwd(), '.output')

// Only the fixture rebuild and the golden lineage comparison read the live IR, and `--oracle-only` does
// neither — so it must not require a `dotnet run … generate` to have happened first. Re-pinning a pure
// rendering change is then a `web/`-only operation, which is what it actually is.
const ir = ORACLE_ONLY ? null : await import('../ir/adapter.ts')

const say = (s = '') => console.log(s)

// ── 1. The route list ───────────────────────────────────────────────────────────────────────────────────
//
// An EXPLICIT committed list, not a rule evaluated at pin time. A "smallest page per family" rule re-selects
// a different page whenever the corpus changes, which silently re-anchors the oracle and hides drift behind
// a fresh baseline. The list is the decision; changing it is a reviewable diff.

let routeList
if (BOOTSTRAP) {
  const byFamily = new Map()
  for (const path of ir.site.paths) {
    const family = resolveFamily(path, ir.site.entry)
    if (!byFamily.has(family)) byFamily.set(family, [])
    byFamily.get(family).push(path)
  }
  routeList = []
  for (const family of [...byFamily.keys()].sort()) {
    // Deterministic: sort by rendered size, take the smallest and the median. Small keeps the committed
    // fixture reviewable; the median guards against a degenerate smallest page being the only sample.
    const paths = byFamily
      .get(family)
      .map((p) => ({ p, n: (readOrNull(join(ir.IR_DIR, p)) ?? '').length }))
      .sort((a, b) => a.n - b.n || a.p.localeCompare(b.p))
      .map((x) => x.p)
    const chosen = [paths[0]]
    if (paths.length > 2) chosen.push(paths[Math.floor(paths.length / 2)])
    for (const p of chosen) routeList.push({ path: p, family })
  }
  // The entry page is the site's richest surface (Hierarchy Explorer, every chart) and must be in the corpus
  // whatever the size rule picks.
  if (!routeList.some((r) => r.path === ir.site.entry)) {
    routeList.unshift({ path: ir.site.entry, family: resolveFamily(ir.site.entry, ir.site.entry) })
  }
  mkdirSync(FIXTURES, { recursive: true })
  writeFileSync(ROUTE_LIST, `${JSON.stringify(routeList, null, 2)}\n`, 'utf8')
  say(`  bootstrapped ${ROUTE_LIST} with ${routeList.length} route(s)`)
} else {
  const raw = readOrNull(ROUTE_LIST)
  if (raw === null) {
    console.error(`\nNo pinned route list at ${ROUTE_LIST}.\n  Create one:  npm run pin:parity -- --bootstrap\n`)
    process.exit(1)
  }
  routeList = JSON.parse(raw)
}

// Only meaningful when the fixture is about to be rebuilt FROM the live IR. Under `--oracle-only` the corpus
// is the one already on disk, so the live IR's contents are irrelevant — and demanding a route be present in
// an IR nothing is going to read would just be a spurious way to fail.
for (const r of ORACLE_ONLY ? [] : routeList) {
  if (!ir.hasPage(r.path)) {
    console.error(
      `\nPinned route "${r.path}" is not in the live IR manifest.\n` +
        `  The corpus cannot be rebuilt from an IR that no longer contains it. Regenerate the portal\n` +
        `  (dotnet run --project src/SpecScribe -- generate --spa --deep-git), or re-bootstrap the list.\n`,
    )
    process.exit(1)
  }
}

// ── 2. Write the pinned IR fixture ──────────────────────────────────────────────────────────────────────

if (ORACLE_ONLY) {
  // The corpus must already be here and be readable: rendering against a corpus that silently does not exist
  // would produce an oracle over nothing, which is the vacuous-success failure ADR 0033 §Decision 5 forbids.
  const manifestRaw = readOrNull(join(CORPUS, 'spa', 'manifest.json'))
  if (manifestRaw === null) {
    console.error(
      `\n--oracle-only needs the committed corpus at ${CORPUS}, and it is absent.\n` +
        `  Re-pin the corpus itself with:  npm run pin:parity\n`,
    )
    process.exit(1)
  }
  const pinnedCount = Object.keys(JSON.parse(manifestRaw).pages ?? {}).length
  if (pinnedCount !== routeList.length) {
    console.error(
      `\nThe committed corpus carries ${pinnedCount} page(s) but the route list names ${routeList.length}.\n` +
        `  The two are out of step, so --oracle-only would pin digests over the wrong corpus.\n` +
        `  Rebuild both with:  npm run pin:parity\n`,
    )
    process.exit(1)
  }
  say('')
  say(`  corpus: UNCHANGED — ${pinnedCount} route(s) read from ${CORPUS}`)
} else {
  const liveManifest = JSON.parse(readFileSync(join(ir.IR_DIR, 'spa', 'manifest.json'), 'utf8'))
  const pinnedPaths = new Set(routeList.map((r) => r.path))

  const PINNED_CHUNK = 'spa/pages-pinned.json'
  const chunkOut = {}
  const pagesOut = {}

  for (const { path } of routeList) {
    const entry = liveManifest.pages[path]
    const chunk = JSON.parse(readFileSync(join(ir.IR_DIR, entry.chunk), 'utf8'))
    chunkOut[path] = chunk[path]
    pagesOut[path] = {
      ...entry,
      chunk: PINNED_CHUNK,
      // Children outside the corpus would make any consumer that resolves them throw. The corpus is a
      // rendering fixture, not a navigable site, and the pages it renders must not depend on absent siblings.
      children: (entry.children ?? []).filter((c) => pinnedPaths.has(c)),
    }
  }

  const pinnedManifest = {
    schemaVersion: liveManifest.schemaVersion,
    siteTitle: liveManifest.siteTitle,
    entry: liveManifest.entry,
    // Nav is CHROME and is kept verbatim: pruning it would change every rendered page's chrome and make the
    // oracle a digest of the fixture's shape rather than of the renderer's behaviour.
    nav: liveManifest.nav,
    pages: pagesOut,
  }

  rmSync(CORPUS, { recursive: true, force: true })
  mkdirSync(join(CORPUS, 'spa'), { recursive: true })
  writeFileSync(join(CORPUS, 'spa', 'manifest.json'), `${JSON.stringify(pinnedManifest, null, 2)}\n`, 'utf8')
  writeFileSync(join(CORPUS, 'spa', 'pages-pinned.json'), `${JSON.stringify(chunkOut, null, 2)}\n`, 'utf8')

  const corpusBytes =
    readFileSync(join(CORPUS, 'spa', 'manifest.json')).length +
    readFileSync(join(CORPUS, 'spa', 'pages-pinned.json')).length
  say('')
  say(`  pinned IR: ${routeList.length} route(s), ${(corpusBytes / 1048576).toFixed(2)} MB -> ${CORPUS}`)
}

// ── 3. Render the pinned corpus and capture the oracle ──────────────────────────────────────────────────

const captured = await withRenderer({ outputDir: ARTEFACT, irDir: CORPUS, port: 3319 }, async (fetchRoute) => {
  const out = []
  for (const { path, family } of routeList) {
    const { status, html } = await fetchRoute(path)
    if (status !== 200) throw new Error(`${path}: the renderer answered HTTP ${status}`)
    const main = mainRegion(html)
    if (main === null) throw new Error(`${path}: the rendered page carries no <main id="main-content"> landmark`)
    out.push({
      path,
      family,
      mainSha: parityDigest(normalizeVolatile(main)),
      pageSha: parityDigest(foldBuildAssets(normalizeVolatile(html))),
      mainBytes: normalizeVolatile(main).length,
      pageBytes: foldBuildAssets(normalizeVolatile(html)).length,
    })
  }
  return out
})

// ── 4. The lineage assertion — run while the C# writer still exists ─────────────────────────────────────

// `--oracle-only` cannot run this comparison and must not pretend to: the golden side is the C# writer, which
// Story 23.6 deleted, and the `.html` files sitting under the output root today were written by this same
// renderer — comparing against them would be the renderer agreeing with itself. The label written in §5
// reflects that honestly instead.
const lineage = []
if (!NO_LINEAGE && !ORACLE_ONLY) {
  for (const row of captured) {
    const goldenHtml = readOrNull(join(ir.IR_DIR, row.path))
    if (goldenHtml === null) {
      console.error(
        `\n${row.path}: no C#-written golden page under ${ir.IR_DIR}.\n` +
          `  The lineage cannot be verified. If the writer has already been deleted, re-run with\n` +
          `  --no-lineage and the oracle will record "carried-forward" instead of "verified".\n`,
      )
      process.exit(1)
    }
    const goldenMain = mainRegion(goldenHtml)
    const goldenSha = parityDigest(normalizeVolatile(goldenMain ?? ''))
    // The IR's own composed region, as a third witness: golden == IR == rendered is the claim Story 23.4
    // proved over 1,469 pages, and this pins it for the corpus at the moment it is still checkable.
    const irSha = parityDigest(composeIrMain(ir.page(row.path).region, normalizeVolatile))
    lineage.push({ path: row.path, goldenSha, irSha, renderedSha: row.mainSha })
  }
  const broken = lineage.filter((l) => l.goldenSha !== l.renderedSha || l.irSha !== l.renderedSha)
  if (broken.length > 0) {
    console.error('')
    console.error(`✗ LINEAGE BROKEN on ${broken.length} of ${lineage.length} route(s) — the renderer does not`)
    console.error('  reproduce what C# writes, so pinning now would freeze a difference and call it correct.')
    console.error('')
    for (const b of broken.slice(0, 15)) {
      console.error(`    ${b.path}`)
      console.error(`      golden=${b.goldenSha}  ir=${b.irSha}  rendered=${b.renderedSha}`)
    }
    process.exit(1)
  }
}

// ── 5. Write the oracle ─────────────────────────────────────────────────────────────────────────────────

// Under `--oracle-only` the corpus is byte-identical to the one the previous oracle was pinned against, so a
// `mainSha` that comes back unchanged is not an inherited number — it is the SAME measurement, re-taken, and
// whatever the previous label claimed about it still describes it exactly. Keeping the label is therefore the
// honest move, and it is conditional on the evidence rather than assumed: the moment any region digest moves,
// the claim stops being about these numbers and the label degrades.
let lineageLabel = NO_LINEAGE ? 'carried-forward' : 'verified-against-csharp-writer'
if (ORACLE_ONLY) {
  const prior = JSON.parse(readOrNull(ORACLE) ?? 'null')
  const priorMain = new Map((prior?.routes ?? []).map((r) => [r.path, r.mainSha]))
  const moved = captured.filter((r) => priorMain.get(r.path) !== r.mainSha)
  if (moved.length === 0) {
    lineageLabel = prior?.lineage ?? 'carried-forward'
  } else {
    lineageLabel = 'carried-forward'
    // Loud, because this is the case the flag is NOT for. `--oracle-only` is meant for chrome-only movement;
    // a moved region means the composed <main> itself changed, and re-pinning it here freezes that change
    // with no golden side left to check it against.
    say('')
    say(`⚠ REGION DRIFT — ${moved.length} of ${captured.length} route(s) changed <main>, not just chrome.`)
    say('  These are NOT a chrome-only re-pin. Confirm the region change is intended before committing:')
    for (const m of moved.slice(0, 15)) {
      say(`    ${m.path}`)
      say(`      was ${priorMain.get(m.path) ?? '(absent)'}  ->  now ${m.mainSha}`)
    }
    say(`  lineage label degraded to "carried-forward".`)
  }
}

const oracle = {
  generatedBy: 'web/scripts/pin-parity.mjs',
  corpus: 'web/fixtures/parity-corpus',
  // Recorded so a future reader knows whether `mainSha` was CHECKED against C#'s own output or merely
  // inherited from a previous pinning. "carried-forward" is honest; silently identical numbers would not be.
  lineage: lineageLabel,
  routes: captured,
}
mkdirSync(dirname(ORACLE), { recursive: true })
writeFileSync(ORACLE, `${JSON.stringify(oracle, null, 2)}\n`, 'utf8')

say('')
say(pad('family', 16) + pad('route', 58) + pad('main', 10) + 'page')
say('-'.repeat(96))
for (const r of captured) {
  say(pad(r.family, 16) + pad(r.path.slice(0, 56), 58) + pad(r.mainSha.slice(0, 8), 10) + r.pageSha.slice(0, 8))
}
say('-'.repeat(96))
say('')
say(`  routes pinned:  ${captured.length}`)
say(`  families:       ${new Set(captured.map((r) => r.family)).size}`)
say(`  lineage:        ${oracle.lineage}${lineage.length > 0 ? ` (${lineage.length}/${lineage.length} verified)` : ''}`)
if (ORACLE_ONLY) say(`  corpus:         UNCHANGED (fixtures/parity-corpus not rewritten)`)
say(`  wrote:          ${ORACLE}`)
say('')
