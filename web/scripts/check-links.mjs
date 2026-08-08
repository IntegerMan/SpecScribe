#!/usr/bin/env node
// `npm run check:links` — Story 23.3 AC #4.
//
// The 23.1 spike proved rendering but NOT navigability: the IR's own hrefs (`code/…`, `adrs/…`,
// `epics.html`) did not resolve against the spike's route space, which is also why `crawlLinks: true`
// aborts the build. This closes that half.
//
// Walks every prerendered page, parses every `<a href>`, skips external / anchor-only / mailto, resolves
// the rest RELATIVE TO THE PAGE'S OWN PATH — which is the whole point of routes mirroring the IR's
// output-relative paths verbatim — and asserts the target exists in the emitted output.
//
// ── It gates against a PINNED BASELINE of the dangling links that already existed ───────────────────────
//
// ⚠️ THIS GATE COULD NOT FAIL BETWEEN STORY 23.6 AND 2026-08-08. [Story 23.3 code review]
//
// It used to compare two trees and fail on a REGRESSION — a link that resolved in the golden site C# wrote
// and dangled in the Nuxt output. Story 23.6 deleted the C# page writer, so both sides collapsed onto the
// same directory (`goldenFiles = nuxtFiles = siteFiles`). The classifier and the exit condition were left
// untouched, and because `scan()` is deterministic in `(root, files)`, the two link maps became the SAME MAP:
// the gating bucket `!resolved && goldenResolved` reduced to `!x && x` and became unreachable. Every dangling
// link was filed as `inherited` ("not this story's") and `process.exit(regressions.length > 0)` was a
// constant 0. Proven by running the classifier over a single dangling link: regressions 0, exit 0. That is
// the vacuous-oracle class ADR 0033 §Decision 5 forbids.
//
// The replacement keeps the property that made the original design right — the site carries ~1,000 dangling
// internal links that predate this story (source-file `…/epics.md` targets the portal never rewrites, and a
// renderer bug emitting NESTED anchors, `<a href="../../<a href="…">…</a>">`), and a gate that failed on
// those would fail this story for defects it did not cause. So the known set is PINNED, and the gate asks the
// one question that is actually about regression:
//
//   does this build introduce a dangling link that was not already in the baseline?
//
// A newly-dangling link FAILS, named with its page. A link in the baseline that now resolves is reported as
// `fixed` — not a failure, but a prompt to re-pin, because a baseline that is never shrunk decays into a
// permanent exemption.
//
// ── How this sits against ADR 0033 ──────────────────────────────────────────────────────────────────────
//
//   localizes failure   yes — every failure names the page and the href, never a bare count.
//   reviewable diff     yes — `npm run pin:links` rewrites a sorted JSON list, so re-pinning shows WHICH
//                       links changed, not a hex bump.
//   sibling-proof       ⚠️ PARTIALLY, and unlike `check:parity` this is deliberate. `check:parity` freezes
//                       its corpus so a doc edit cannot redden it. A link gate cannot do that and still be a
//                       link gate: it has to run over the live site, so a sibling story that adds a page
//                       with a broken link WILL turn this red. That is the gate working — but it means a red
//                       run here is not automatically YOUR bug. The failure list names the page, so check
//                       whose surface it is before assuming.
//
// Run `npm run generate` first, then `npm run pin:links` once to establish the baseline.

import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { assertFullRun, DANGLING_BASELINE, MEASUREMENTS_DIR, pad, readOrNull, walk } from './harness-lib.mjs'
import { assertRealCorpus, classifyAgainstBaseline, scan } from './links-lib.mjs'

assertFullRun('check:links')

const ir = await import('../ir/adapter.ts')

/**
 * ⚠️ [Story 23.6] THIS GATE IS ONE-SIDED NOW, AND ITS OTHER SIDE HAD SILENTLY GONE EMPTY.
 *
 * It used to compare the Nuxt output (`.output/public`) against the GOLDEN site C# wrote (`ir.IR_DIR`). Two
 * things happened to that comparison:
 *
 *  1. C# stopped writing pages, so `ir.IR_DIR` IS the Nuxt-rendered site. The story's Dev Notes anticipated
 *     this and accepted it — "a one-sided link check is still a link check, but say so in the run".
 *  2. Worse and NOT anticipated: `.output/public` is empty on every `build:package` artefact (that mode exists
 *     to carry no prerendered HTML — see check-a11y.mjs for the full reason), so the "nuxt" column walked zero
 *     pages and the gate reported "no regressions" by comparing an empty set against everything.
 *
 * Both sides therefore collapse into one honest question: does every internal link on the GENERATED SITE
 * resolve? That is what this now asks, over the real emitted pages, with a hard failure when there are none.
 */
const siteFiles = walk(ir.IR_DIR)
assertRealCorpus('check:links', ir.IR_DIR, siteFiles)

const nuxt = scan(ir.IR_DIR, siteFiles)

// ── Compare against the pinned baseline ────────────────────────────────────────────────────────────────
//
// One scan, one tree. The second `scan()` that used to stand in for the golden site was removed with the
// classifier it fed: after Story 23.6 it read the same directory with the same file list, so it could only
// ever produce the same map.

const baselineRaw = readOrNull(DANGLING_BASELINE)
if (baselineRaw === null) {
  console.error('check:links — NO BASELINE PINNED.')
  console.error(`  Expected: ${DANGLING_BASELINE}`)
  console.error('')
  console.error('  This gate fails on a dangling link that is NOT already in the baseline, so without one it')
  console.error('  cannot tell a pre-existing defect from a new one. It fails CLOSED rather than passing,')
  console.error('  because a link gate that silently passes is exactly the defect this replaced.')
  console.error('')
  console.error('  Fix: generate the site, then run `npm run pin:links` and commit the result.')
  process.exit(1)
}

/** `{ generatedBy, count, dangling: ["<page>\t<href>", …] }` — sorted, so re-pinning gives a reviewable diff. */
const baseline = JSON.parse(baselineRaw)
const known = new Set(baseline.dangling ?? [])

const { newlyDangling, stillDangling, fixed } = classifyAgainstBaseline(nuxt.links, known)

const distinct = (rows) => new Set(rows.map((r) => r.href)).size

const lines = []
const say = (s = '') => {
  lines.push(s)
  console.log(s)
}

say('')
say('Story 23.3 AC #4 — internal link resolution over the generated site')
say('')
say('  ⚠️ ONE-SIDED since Story 23.6. C# no longer writes pages, so there is no second, "golden" tree to')
say('  compare against — the generated site IS the Nuxt render. The question this answers is therefore')
say('  "does every internal link resolve?", not "did the migration break a link?". The comparison columns')
say('  were retired rather than left printing the same numbers twice.')
say('')
say(pad('  pages walked', 26) + nuxt.pages)
say(pad('  files emitted', 26) + nuxt.files)
say(pad('  <a href> total', 26) + nuxt.counts.total)
say(pad('  external/anchor', 26) + nuxt.counts.skipped)
say(pad('  internal', 26) + nuxt.counts.internal)
say(pad('  resolved', 26) + nuxt.counts.resolved)
say(pad('  dangling', 26) + nuxt.counts.dangling)
say('')

say(pad('  baseline pinned', 26) + `${known.size}  (${baseline.pinnedAt ?? 'date not recorded'})`)
say(pad('  still dangling', 26) + stillDangling.length)
say(pad('  NEWLY dangling', 26) + `${newlyDangling.length}${newlyDangling.length > 0 ? '   <-- fails' : ''}`)
say(pad('  fixed since pinning', 26) + fixed.length)
say('')

if (newlyDangling.length > 0) {
  say(`${newlyDangling.length} link(s) dangle that were NOT in the baseline — this build introduced them:`)
  say('')
  say(pad('href', 62) + 'on page')
  say('-'.repeat(110))
  for (const r of newlyDangling.slice(0, 40)) say(pad(r.href.slice(0, 60), 62) + r.page)
  if (newlyDangling.length > 40) {
    say(`… and ${newlyDangling.length - 40} more (all in measurements/links.json).`)
  }
  say('')
  say('  If one of these is a page a SIBLING story owns, it is theirs to fix — this gate runs over the live')
  say('  site and cannot be frozen the way `check:parity` is. If they are genuinely accepted debt, re-pin')
  say('  with `npm run pin:links` and say in the story record what you accepted and why.')
  say('')
} else {
  say(`No new dangling links. ${stillDangling.length} known reference(s) remain from the baseline.`)
  say('')
}

if (stillDangling.length > 0) {
  const byHref = new Map()
  for (const r of stillDangling) byHref.set(r.href, (byHref.get(r.href) ?? 0) + 1)
  say('Known dangling targets, carried from the baseline:')
  say('')
  for (const [href, n] of [...byHref].sort((a, b) => b[1] - a[1]).slice(0, 12)) {
    say(`  ${pad(n, 6)}${href.slice(0, 96)}`)
  }
  if (byHref.size > 12) say(`  … and ${byHref.size - 12} more distinct targets.`)
  say('')
  say('  Two known causes, both in shipped C# and both worth their own follow-up:')
  say('    · links to SOURCE files (`…/epics.md`) that the portal never rewrites to their `.html` page;')
  say('    · NESTED anchors — `<a href="../../<a href="…">…</a>">` — from a link rewriter running twice.')
  say('')
}

if (fixed.length > 0) {
  say(`${fixed.length} baseline entr(y/ies) no longer dangle — re-pin so the baseline shrinks with the debt:`)
  say('')
  for (const f of fixed.slice(0, 10)) say(`  ${pad(f.href.slice(0, 60), 62)}${f.page}`)
  if (fixed.length > 10) say(`  … and ${fixed.length - 10} more (all in measurements/links.json).`)
  say('')
  say('  `npm run pin:links`. A baseline that only ever grows is an exemption, not a record.')
  say('')
}

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(join(MEASUREMENTS_DIR, 'links.txt'), `${lines.join('\n')}\n`, 'utf8')
writeFileSync(
  join(MEASUREMENTS_DIR, 'links.json'),
  `${JSON.stringify(
    {
      generatedBy: 'web/scripts/check-links.mjs',
      site: { pages: nuxt.pages, files: nuxt.files, counts: nuxt.counts },
      baseline: { file: 'web/measurements/links-baseline.json', count: known.size, pinnedAt: baseline.pinnedAt ?? null },
      newlyDangling,
      fixed,
      stillDanglingCount: stillDangling.length,
      stillDanglingDistinct: [...new Set(stillDangling.map((r) => r.href))].sort(),
    },
    null,
    2,
  )}\n`,
  'utf8',
)
console.log('  wrote measurements/links.txt + measurements/links.json')
console.log('')

process.exit(newlyDangling.length > 0 ? 1 : 0)
