#!/usr/bin/env node
// `npm run experiment:two-ir` — Story 23.5 AC #4's load-bearing experiment.
//
// ── The question ───────────────────────────────────────────────────────────────────────────────────────
//
// Epic 23's gate (23.1 spike report §Gate) states the packaging problem as a binary: either the shipped
// artefact becomes a client-rendered SPA over the IR (forfeiting the PRD's NFR-5, cited as NFR6 throughout
// Epic 23 per the recorded collision), or `specscribe generate` invokes Node at run time (forfeiting the
// self-contained model of ADR 0005/0006).
//
// That binary is false IF — and only if — ONE prebuilt Nitro artefact can render MANY different projects.
// The toolchain (202 MB of `node_modules` + a Vite build) and the runtime (a ~2.2 MB `.output/server`) are
// separable, but nobody had ever tested whether the separated runtime is project-independent.
//
// This harness tests exactly that, and a REFUTATION is an equally valid outcome: it eliminates the
// strategy rather than failing the story.
//
// ── The method ─────────────────────────────────────────────────────────────────────────────────────────
//
//   1. `npm run build:package` ONCE — the project-INDEPENDENT artefact. (This harness never builds.)
//   2. Copy ONLY `.output/` to an isolated directory — no source, no `node_modules`, no `web/`.
//   3. Boot `node .output/server/index.mjs` with `SPECSCRIBE_IR_DIR` pointed at IR **A**, drive every route
//      from A's OWN manifest, and check each response.
//   4. Kill it, restart pointed at IR **B** — a DIFFERENT PROJECT's output — and do the same.
//
// Steps 3/4 drive the routes EXTERNALLY, one request per route, which is the shape SpecScribe itself would
// use: it emitted the manifest, so it already knows every route. This is deliberately NOT `nuxt generate`.
//
// ── What counts as a pass, per route ───────────────────────────────────────────────────────────────────
//
//   · HTTP 200
//   · RENDERED  — a `<main>` region is present and non-trivial, so a 200-with-empty-shell cannot pass.
//   · CORRECT   — `emitted.includes(page.region.mainInnerHtml)`. This is `measure:parity`'s VERBATIM check
//                 and it is the right oracle here. An earlier draft of this harness compared against each
//                 IR's own golden STATIC page instead and reported 46 false failures: for the
//                 dashboard/epics families the IR is deliberately a MORE COMPLETE render than the static
//                 page (`measure:parity` scores that golden≠IR delta 143/189 and attributes it to Epic 22).
//                 Comparing against the golden page therefore measures an inherited capture delta, not
//                 whether THIS artefact rendered THIS project.
//   · NO-JS     — no Nuxt hydration payload. Matched against real `<script>` TAGS, never as a substring:
//                 several `code/**` pages render source files that MENTION `_payload.json` and
//                 `window.__NUXT__` as prose, and a substring test failed six of them. 23.3 hit the same
//                 trap with `data-hierarchy` and documented it.
//
// The portal's own vanilla `specscribe.js` is EXPECTED and is not a hydration script — it is loaded by us
// (ADR 0012's Hierarchy Explorer) and is the same file the C# portal ships.
//
// ── Why a subprocess per IR ────────────────────────────────────────────────────────────────────────────
//
// `ir/adapter.ts` resolves `IR_DIR` from `process.env` at MODULE SCOPE, so one process can only ever see
// one IR — importing it twice would silently measure IR A twice, which is precisely the failure this
// experiment exists to rule out. So the parent re-execs itself once per IR with `SPECSCRIBE_IR_DIR` set,
// and each child imports the real adapter and reports JSON. Process isolation, not a reimplementation.
//
// Usage:
//   node scripts/experiment-two-ir.mjs --server <dir-with-.output> --ir A=<dir> --ir B=<dir> [--routes N]
//
// `--routes N` samples the first N routes per IR (0 = all). Sampling is REPORTED, so a truncated run can
// never be published as a full one — the discipline `check:links` and `measure:parity` already use.

import { spawn, spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { join, resolve } from 'node:path'
import { MEASUREMENTS_DIR, mainRegion, pad, walk } from './harness-lib.mjs'

const SELF = fileURLToPath(import.meta.url)

/**
 * Sentinel separating the child's structured result from anything else on its stdout.
 *
 * Nitro writes its own banner to the child's stdout, so the result cannot simply BE the stdout, and it
 * cannot be delimited by whitespace either — the JSON is full of it. The parent takes everything after the
 * LAST occurrence, so a sentinel appearing in rendered content could not shift the parse.
 */
const RESULT = '\n@@TWO-IR-RESULT@@'

// ═══ CHILD MODE ═══════════════════════════════════════════════════════════════════════════════════════
//
// `--child <label> <serverDir> <port> <routeLimit>`, with SPECSCRIBE_IR_DIR already set by the parent.

if (process.argv[2] === '--child') {
  const [, , , label, serverDir, portRaw, limitRaw] = process.argv
  const port = Number(portRaw)
  const routeLimit = Number(limitRaw)

  // The real adapter, against THIS child's SPECSCRIBE_IR_DIR. Node 24 strips the TS types on import — the
  // same mechanism `measure-parity.mjs` relies on.
  const ir = await import('../ir/adapter.ts')

  const allPaths = ir.site.paths
  const paths = routeLimit > 0 ? allPaths.slice(0, routeLimit) : allPaths

  const base = `http://127.0.0.1:${port}`

  // A port already in use is the nastiest failure this harness can have, so rule it out BEFORE spawning.
  // The readiness loop below accepts any HTTP response as proof of listening, and Nitro's EADDRINUSE exit is
  // asynchronous — so a foreign server answers first, the loop breaks, and every route then fails the
  // content oracle. The run prints `VERDICT: REFUTED`: a false refutation of project-independence, measured
  // against a server that was never the artefact. [Story 23.5 code review 2026-08-08]
  try {
    await fetch(base, { signal: AbortSignal.timeout(1500) })
    process.stdout.write(
      RESULT +
        JSON.stringify({
          fatal:
            `port ${port} is already in use — something is listening there and this harness cannot tell it ` +
            `apart from the artefact. Free the port, or pass --port <n> to move both children.`,
        }),
    )
    process.exit(0)
  } catch {
    /* nothing listening — this is the expected path */
  }

  const entry = join(serverDir, '.output', 'server', 'index.mjs')
  const proc = spawn(process.execPath, [entry], {
    cwd: serverDir,
    env: {
      ...process.env,
      // Blank the BUILD flag explicitly. This experiment exists to prove the SERVING path renders a real
      // IR, and the flag stubs the manifest empty — inheriting it from an operator's shell would have
      // measured an empty artefact and called it a result. The artefact now refuses to boot under it
      // (`server/plugins/refuse-leaked-package-build.ts`), so without this line a stale export turns a
      // legitimate run into a confusing hard failure. [Story 23.5 code review 2026-08-08]
      SPECSCRIBE_PACKAGE_BUILD: '',
      PORT: String(port),
      NITRO_PORT: String(port),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  const serverLog = []
  proc.stdout.on('data', (d) => serverLog.push(String(d)))
  proc.stderr.on('data', (d) => serverLog.push(String(d)))

  const bootStart = performance.now()
  const deadline = Date.now() + 60_000
  for (;;) {
    if (proc.exitCode !== null) {
      process.stdout.write(
        RESULT + JSON.stringify({ fatal: `server exited early (${proc.exitCode})`, serverLog: serverLog.join('') }),
      )
      process.exit(0)
    }
    try {
      // ANY HTTP response means the server is listening — including a 500. Waiting for a healthy status
      // hangs for the full timeout on a project whose entry page legitimately fails to render, and then
      // reports "server did not listen", which is the wrong diagnosis entirely.
      await fetch(`${base}/`, { signal: AbortSignal.timeout(2000) })
      break
    } catch {
      /* not up yet */
    }
    if (Date.now() > deadline) {
      proc.kill()
      process.stdout.write(
        RESULT + JSON.stringify({ fatal: 'server did not listen within 60 s', serverLog: serverLog.join('') }),
      )
      process.exit(0)
    }
    await new Promise((r) => setTimeout(r, 150))
  }
  const bootMs = performance.now() - bootStart

  /**
   * Nuxt hydration markers. `specscribe.js` is OURS and expected — it is not a hydration script.
   *
   * ⚠️ Every pattern here must match a MECHANISM, never a mention. This portal renders its own source and
   * its own design docs, so the literal strings `window.__NUXT__`, `_payload.json` and `__NUXT_DATA__` all
   * appear as PROSE on real pages — inside `<code>`, and inside JSON data islands (which are themselves
   * `<script type="application/json">`, so "is it in a script tag" is not a discriminator). Two earlier
   * drafts of this function failed 6 and then 1 route that way. Hence: require the ASSIGNMENT, require the
   * payload script's id ATTRIBUTE, require a real `src`/`href`.
   */
  function hydrationMarkers(html) {
    const found = []
    if (/<script[^>]+id="__NUXT_DATA__"/.test(html)) found.push('__NUXT_DATA__ payload script')
    if (/window\.__NUXT__\s*=/.test(html)) found.push('window.__NUXT__= assignment')
    if (/<(?:script|link)[^>]+(?:src|href)="[^"]*_payload\.json/.test(html)) found.push('_payload.json (tag)')
    if (/<script[^>]+src="[^"]*\/_nuxt\/[^"]*\.js"/.test(html)) found.push('/_nuxt/*.js (script tag)')
    return found
  }

  const failures = []
  const latencies = []
  let ok = 0
  const wall = performance.now()

  for (const p of paths) {
    const t0 = performance.now()
    let res
    let html
    try {
      res = await fetch(`${base}/${p}`, { signal: AbortSignal.timeout(60_000) })
      html = await res.text()
    } catch (err) {
      failures.push({ path: p, kind: 'request', why: `request failed: ${err.message}` })
      continue
    }
    latencies.push(performance.now() - t0)

    if (res.status !== 200) {
      failures.push({ path: p, kind: 'status', why: `HTTP ${res.status}` })
      continue
    }
    const emitted = mainRegion(html)
    if (!emitted) {
      failures.push({ path: p, kind: 'empty', why: '200 with no <main> region — an empty shell' })
      continue
    }
    if (emitted.length < 200) {
      failures.push({ path: p, kind: 'empty', why: `<main> is only ${emitted.length} B — not a rendered page` })
      continue
    }
    const markers = hydrationMarkers(html)
    if (markers.length) {
      failures.push({ path: p, kind: 'hydration', why: `hydration markers: ${markers.join(', ')}` })
      continue
    }
    // CORRECTNESS: the IR's own bytes are IN this page. See the header for why this, not the golden page.
    let expected
    try {
      expected = ir.page(p).region.mainInnerHtml
    } catch (err) {
      failures.push({ path: p, kind: 'ir', why: `IR could not resolve this page: ${err.message}` })
      continue
    }
    // ⚠️ `emitted`, NOT `html`. Comparing against the whole page passes when the IR's bytes are present
    // ANYWHERE in it — including re-parented OUTSIDE `<main>`, which is exactly the region-splicing
    // regression `test/region-split.test.ts` documents across 187 pages. That oracle cannot see the defect
    // this experiment's verdict is quoted for. [Story 23.5 code review 2026-08-08]
    if (!emitted.includes(expected)) {
      failures.push({
        path: p,
        kind: 'content',
        why: `emitted <main> does not contain this IR's own content verbatim (IR ${expected.length} B, emitted <main> ${emitted.length} B)`,
      })
      continue
    }
    ok += 1
  }

  const wallMs = performance.now() - wall
  proc.kill()
  latencies.sort((a, b) => a - b)

  process.stdout.write(
    RESULT +
      JSON.stringify({
        label,
        irDir: ir.IR_DIR,
        siteTitle: ir.site.title,
        // Project IDENTITY, as distinct from the display string above. `siteTitle` cannot carry this: two
        // checkouts of one project at different revisions — the cheapest and most reproducible way to obtain
        // two genuinely different IRs — share a title while their route sets differ entirely, and two
        // unrelated projects can collide on a title. The route set is what "a different project" actually
        // means here. [Story 23.5 code review 2026-08-08]
        pathsDigest: createHash('sha256').update([...allPaths].sort().join('\n')).digest('hex').slice(0, 16),
        routesInManifest: allPaths.length,
        routesRequested: paths.length,
        sampled: routeLimit > 0 && routeLimit < allPaths.length,
        ok,
        failures,
        bootMs,
        wallMs,
        medianMs: latencies.length ? latencies[Math.floor(latencies.length / 2)] : 0,
        p95Ms: latencies.length ? latencies[Math.floor(latencies.length * 0.95)] : 0,
      }),
  )
  process.exit(0)
}

// ═══ PARENT MODE ══════════════════════════════════════════════════════════════════════════════════════

const argv = process.argv.slice(2)
const irs = []
let serverDir = null
let routeLimit = 0
let basePort = 3123

for (let i = 0; i < argv.length; i += 1) {
  const a = argv[i]
  // A flag left as the FINAL argument used to read `undefined` and then fail four different ways, none of
  // them naming the flag: `--server` threw a raw ERR_INVALID_ARG_TYPE out of `resolve`; `--ir` threw
  // "cannot read properties of undefined" out of `.indexOf`; `--routes` became `NaN`, and `NaN > 0` is
  // false, so it SILENTLY meant "all routes" with `sampled: false` — the opposite of the sampling
  // discipline this harness commits to; and `--port` became `NaN`, so both children got `PORT="NaN"` and
  // the run reported "server did not listen within 60 s" a minute later, which is the exact misdiagnosis
  // the readiness loop's own comment says it was written to avoid. [Story 23.5 code review 2026-08-08]
  const value = () => {
    const v = argv[++i]
    if (v === undefined) throw new Error(`${a} expects a value, but it was the last argument`)
    return v
  }
  if (a === '--server') serverDir = resolve(value())
  else if (a === '--ir') {
    const raw = value()
    const eq = raw.indexOf('=')
    if (eq < 0) throw new Error(`--ir expects <label>=<dir>, got "${raw}"`)
    irs.push({ label: raw.slice(0, eq), dir: resolve(raw.slice(eq + 1)) })
  } else if (a === '--routes') {
    const raw = value()
    routeLimit = Number(raw)
    if (!Number.isInteger(routeLimit) || routeLimit < 0) {
      throw new Error(`--routes expects a non-negative integer, got "${raw}"`)
    }
  } else if (a === '--port') {
    const raw = value()
    basePort = Number(raw)
    if (!Number.isInteger(basePort) || basePort < 1 || basePort > 65_535) {
      throw new Error(`--port expects a port number in 1–65535, got "${raw}"`)
    }
  } else throw new Error(`unknown argument "${a}"`)
}

if (!serverDir) throw new Error('--server <dir> is required (the directory CONTAINING .output/)')
if (irs.length < 2) throw new Error('at least two --ir <label>=<dir> pairs are required — the point is TWO IRs')

if (!existsSync(join(serverDir, '.output', 'server', 'index.mjs'))) {
  throw new Error(
    `No prebuilt server under ${serverDir}.\n` +
      `Run \`npm run build:package\`, then copy .output/ into an empty directory.`,
  )
}

// The isolation claim is only meaningful if the server directory really is isolated. Assert it rather than
// trusting the operator: a stray `node_modules` or `nuxt.config.ts` alongside `.output/` would silently
// turn this into a test of the DEVELOPMENT tree, which proves nothing about the shipped artefact.
for (const forbidden of ['node_modules', 'nuxt.config.ts', 'package.json', 'ir', 'pages', 'components']) {
  if (existsSync(join(serverDir, forbidden))) {
    throw new Error(
      `${serverDir} contains "${forbidden}" — this is not an isolated artefact, so a pass here would not ` +
        `support the claim. Copy ONLY .output/ into an empty directory.`,
    )
  }
}

// A prebuilt artefact that still carries PRERENDERED PAGES is not project-independent, and testing it
// produces a false pass rather than an error: Nitro serves `public/` static files AHEAD of the SSR route,
// so project A's baked `/index.html` is returned verbatim when the server is pointed at project B — HTTP
// 200, wrong project. Story 23.5 hit exactly that on the first run.
const publicDir = join(serverDir, '.output', 'public')
if (existsSync(publicDir)) {
  const stray = walk(publicDir).filter((f) => f.toLowerCase().endsWith('.html'))
  if (stray.length) {
    throw new Error(
      `${publicDir} contains ${stray.length} prerendered HTML page(s) — e.g. ${stray.slice(0, 3).join(', ')}.\n` +
        `Those shadow the renderer, so a pass here would be measuring baked output, not rendering.\n` +
        `Rebuild the project-independent artefact with:  npm run build:package`,
    )
  }
}

const results = []
for (let i = 0; i < irs.length; i += 1) {
  const { label, dir } = irs[i]
  process.stdout.write(`\n▶ IR ${label} — ${dir}\n`)
  const child = spawnSync(
    process.execPath,
    [SELF, '--child', label, serverDir, String(basePort + i), String(routeLimit)],
    {
      cwd: join(SELF, '..', '..'),
      // The child imports the real adapter to read `site.paths`, so a leaked build flag would stub that
      // manifest empty and the run would drive ZERO routes. Blanked here for the same reason it is blanked
      // around the server spawn. [Story 23.5 code review 2026-08-08]
      env: { ...process.env, SPECSCRIBE_PACKAGE_BUILD: '', SPECSCRIBE_IR_DIR: dir },
      encoding: 'utf8',
      maxBuffer: 256 * 1024 * 1024,
    },
  )
  // The child's own stderr (Vue SSR render errors) is diagnostic, not structured — surface a digest only.
  const nul = (child.stdout ?? '').lastIndexOf(RESULT)
  if (nul < 0) {
    throw new Error(
      `child for IR ${label} produced no result.\n--- stdout ---\n${child.stdout}\n--- stderr ---\n` +
        `${(child.stderr ?? '').slice(-4000)}`,
    )
  }
  const parsed = JSON.parse(child.stdout.slice(nul + RESULT.length))
  if (parsed.fatal) throw new Error(`IR ${label}: ${parsed.fatal}\n${(parsed.serverLog ?? '').slice(-4000)}`)
  results.push(parsed)
}

// ── Report ───────────────────────────────────────────────────────────────────────────────────────────────

const lines = []
const say = (s = '') => {
  lines.push(s)
  console.log(s)
}

say()
say('Story 23.5 AC #4 — one prebuilt artefact, two different projects')
say()
say(`Prebuilt server: ${join(serverDir, '.output', 'server', 'index.mjs')}`)
say(`Isolation:       no node_modules, no source, no nuxt.config.ts, no prerendered HTML alongside .output/`)
say(`Routes driven:   externally, one request per route from each IR's OWN manifest (not \`nuxt generate\`)`)
say(`Oracle:          emitted page CONTAINS that IR's own <main> inner HTML verbatim`)
say()
say(
  pad('IR', 5) +
    pad('project', 22) +
    pad('routes', 12) +
    pad('pass', 12) +
    pad('boot', 9) +
    pad('median', 9) +
    pad('p95', 9) +
    'wall',
)
say('-'.repeat(90))
for (const r of results) {
  say(
    pad(r.label, 5) +
      pad(r.siteTitle.slice(0, 20), 22) +
      pad(String(r.routesRequested) + (r.sampled ? `/${r.routesInManifest}` : ''), 12) +
      pad(`${r.ok}/${r.routesRequested}`, 12) +
      pad(`${r.bootMs.toFixed(0)} ms`, 9) +
      pad(`${r.medianMs.toFixed(1)} ms`, 9) +
      pad(`${r.p95Ms.toFixed(1)} ms`, 9) +
      `${(r.wallMs / 1000).toFixed(1)} s`,
  )
}
say('-'.repeat(90))
say()

if (results.some((r) => r.sampled)) {
  say('⚠ SAMPLED RUN — --routes was set. These numbers do not describe a full site pass.')
  say()
}

// Identity is the ROUTE SET, not the display title — see `pathsDigest` in the child for why.
const distinct = new Set(results.map((r) => r.pathsDigest)).size
let verdict = distinct < results.length ? 'INVALID' : 'CONFIRMED'
if (verdict === 'INVALID') {
  say(
    `✗ The IRs do not describe distinct projects (${distinct} distinct route set(s) across ${results.length} IRs).\n` +
      `  A pass would prove nothing — point --ir at genuinely different projects.`,
  )
  say()
}

// ZERO ROUTES IS NOT A PASS. `verdict` starts at CONFIRMED and only the failures loop below can move it, so
// an IR whose manifest carries no pages produced `ok = 0`, `failures = []`, a printed "rendered BOTH
// projects correctly", and exit 0 — having issued no HTTP request at all. This harness asserts isolation,
// asserts no baked HTML and reports sampling; a run that proved nothing was the one hole left in that
// discipline. [Story 23.5 code review 2026-08-08]
const routeless = results.filter((r) => r.routesRequested === 0)
if (routeless.length) {
  verdict = 'INVALID'
  say(
    `✗ ${routeless.map((r) => `IR ${r.label} (${r.siteTitle})`).join(', ')} drove ZERO routes — an empty ` +
      `manifest.\n  Nothing was requested, so nothing was demonstrated. Check SPECSCRIBE_IR_DIR and that ` +
      `the IR was generated with \`--spa\`.`,
  )
  say()
}

for (const r of results) {
  if (!r.failures.length) continue
  if (verdict === 'CONFIRMED') verdict = 'REFUTED'
  const byKind = {}
  for (const f of r.failures) (byKind[f.kind] ??= []).push(f)
  say(`✗ IR ${r.label} (${r.siteTitle}) — ${r.failures.length} failing route(s):`)
  for (const [kind, list] of Object.entries(byKind)) {
    say(`    [${kind}] ${list.length} route(s)`)
    for (const f of list.slice(0, 5)) say(`      ${f.path}\n        ${f.why}`)
    if (list.length > 5) say(`      … and ${list.length - 5} more (all in the JSON).`)
  }
  say()
}

if (verdict === 'CONFIRMED') {
  say('VERDICT: CONFIRMED — one prebuilt `.output/server`, built with NO IR present at all, rendered BOTH')
  say('         projects correctly and without a hydration payload, driven externally by route. The')
  say('         toolchain and the runtime ARE separable, and the artefact is project-independent.')
} else if (verdict === 'REFUTED') {
  say('VERDICT: REFUTED — the prebuilt artefact did NOT render every route of every project. Read the')
  say('         failure kinds above before concluding: a `status`/`ir` failure on ONE project is a')
  say('         project-independence defect in a component, which is a narrower (and fixable) result than')
  say('         "prebuilt artefacts cannot work".')
} else {
  // The arm this chain used to be missing. Without it an INVALID run wrote a `two-ir.txt` carrying a table,
  // a `✗` line, and NO `VERDICT:` line — the one line a reader scans for — while `two-ir.json` recorded
  // `"verdict":"INVALID"`. Two artifacts of one run disagreeing about what happened is precisely the class
  // of reporting gap this story was reviewed for. [Story 23.5 code review 2026-08-08]
  say('VERDICT: INVALID — this run cannot support a conclusion either way. The inputs did not satisfy the')
  say('         experiment\'s own preconditions (see the ✗ line(s) above), so neither a pass nor a failure')
  say('         here would say anything about project-independence. Fix the inputs and re-run.')
}

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(join(MEASUREMENTS_DIR, 'two-ir.txt'), lines.join('\n') + '\n')
writeFileSync(
  join(MEASUREMENTS_DIR, 'two-ir.json'),
  JSON.stringify({ verdict, server: serverDir, results }, null, 2),
)
say()
say('  wrote measurements/two-ir.txt + measurements/two-ir.json')

process.exitCode = verdict === 'CONFIRMED' ? 0 : 1
