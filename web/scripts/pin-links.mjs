#!/usr/bin/env node
// `npm run pin:links` — pins the set of internal links that already dangle. [Story 23.3 code review]
//
// The regeneration path ADR 0033 §Decision 3 requires: "an owner-runnable regeneration path that is a
// command, not a constant-bump … a deliberate, reviewable act producing a reviewable diff". It rewrites one
// committed artifact:
//
//   web/measurements/links-baseline.json
//
// ── Why a baseline at all ───────────────────────────────────────────────────────────────────────────────
//
// The generated site carries ~1,000 dangling internal links that nothing in Epic 23 caused: links to SOURCE
// files (`…/epics.md`) the portal never rewrites to their `.html` page, and a renderer bug emitting NESTED
// anchors (`<a href="../../<a href="…">…</a>">`) from a link rewriter running twice. A gate that failed on
// the absolute count would be red from its first run and would stay red, which teaches people to ignore it.
//
// So the known set is recorded here and `check:links` asks the question that is actually about regression:
// does this build introduce a dangling link that was NOT already known? Everything in this file is accepted
// debt — it is a record, not an endorsement, and `check:links` reports entries that have started resolving
// so the list shrinks as the debt is paid rather than ossifying into a blanket exemption.
//
// ⚠️ Re-pinning ACCEPTS whatever currently dangles. Read the diff before committing it. If it grew, say in
// the story record what you accepted and why — this is the same discipline `pin:parity` asks for.
//
// Usage:
//   npm run generate        (or `dotnet run --project src/SpecScribe -- generate`) — produce the site first
//   npm run pin:links       rewrite the baseline from the site on disk
//
// Zero npm dependencies (ADR 0010).

import { mkdirSync, writeFileSync } from 'node:fs'
import { DANGLING_BASELINE, danglingKey, MEASUREMENTS_DIR, pad, readOrNull, walk } from './harness-lib.mjs'
import { assertRealCorpus, scan } from './links-lib.mjs'

const ir = await import('../ir/adapter.ts')

const siteFiles = walk(ir.IR_DIR)
assertRealCorpus('pin:links', ir.IR_DIR, siteFiles)

const site = scan(ir.IR_DIR, siteFiles)

const dangling = [...site.links]
  .filter(([, resolved]) => !resolved)
  .map(([key]) => danglingKey(...key.split('\t')))
  .sort()

// What the previous baseline said, so the console can report the delta rather than just the new total. A
// re-pin that silently grows by 300 is the thing worth noticing, and it should be visible without reading
// the git diff.
const previousRaw = readOrNull(DANGLING_BASELINE)
const previous = previousRaw ? new Set(JSON.parse(previousRaw).dangling ?? []) : null
const current = new Set(dangling)
const added = previous ? dangling.filter((k) => !previous.has(k)) : []
const removed = previous ? [...previous].filter((k) => !current.has(k)) : []

const baseline = {
  generatedBy: 'web/scripts/pin-links.mjs',
  // The DATE only, never a time or a commit sha: a re-pin should diff as the lines that changed, and a
  // volatile field would put a spurious one-line change in every regeneration.
  pinnedAt: new Date().toISOString().slice(0, 10),
  note:
    'Internal links that already dangle. `check:links` fails on a dangling link NOT in this list. Accepted '
    + 'debt, not endorsed: two known causes are source-file links the portal never rewrites, and nested '
    + 'anchors from a link rewriter running twice. Shrink it as they are fixed — re-pin after any repair.',
  count: dangling.length,
  // `<page>\t<href>` per entry, sorted, one per line in the emitted JSON so a re-pin is reviewable.
  dangling,
}

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(DANGLING_BASELINE, `${JSON.stringify(baseline, null, 2)}\n`, 'utf8')

console.log('')
console.log('pin:links — baseline of already-dangling internal links')
console.log('')
console.log(pad('  pages walked', 26) + site.pages)
console.log(pad('  internal links', 26) + site.counts.internal)
console.log(pad('  resolved', 26) + site.counts.resolved)
console.log(pad('  dangling (pinned)', 26) + dangling.length)
if (previous) {
  console.log('')
  console.log(pad('  previously pinned', 26) + previous.size)
  console.log(pad('  newly accepted', 26) + `${added.length}${added.length > 0 ? '   <-- read these before committing' : ''}`)
  console.log(pad('  no longer dangling', 26) + removed.length)
  for (const k of added.slice(0, 15)) {
    const [page, href] = k.split('\t')
    console.log(`    + ${pad(href.slice(0, 58), 60)}${page}`)
  }
  if (added.length > 15) console.log(`    … and ${added.length - 15} more; the full list is the committed diff.`)
} else {
  console.log('')
  console.log('  First pin — there was no previous baseline to compare against.')
}
console.log('')
console.log('  wrote measurements/links-baseline.json')
console.log('  ⚠️ Re-pinning ACCEPTS what currently dangles. Review the diff before committing it.')
console.log('')
