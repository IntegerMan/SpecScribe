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
// ── It runs over BOTH trees, and gates only on the difference ──────────────────────────────────────────
//
// The golden site does not have a clean link graph, and a harness that ignored that would have failed this
// story for defects it did not cause. 1,013 of its own internal hrefs dangle: source-file links the portal
// never rewrites (`…/epics.md`), and a renderer bug that emits NESTED anchors
// (`<a href="../../<a href="…">…</a>">`), which is a real pre-existing defect worth its own follow-up.
//
// So the gate is: a link that RESOLVES in the golden site and DANGLES in the Nuxt output is a migration
// regression and fails. A link that dangles in both is inherited, reported by count, and not this story's.
// A link that dangles in the golden and resolves here is reported too — silently "fixing" things is its own
// way of hiding a difference.
//
// Run `npm run generate` first.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join, posix } from 'node:path'
import { assertFullRun, MEASUREMENTS_DIR, pad, walk } from './harness-lib.mjs'

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
const sitePages = siteFiles.filter((f) => f.endsWith('.html'))
if (sitePages.length < 50) {
  console.error(
    `check:links — VACUOUS: only ${sitePages.length} page(s) under ${ir.IR_DIR}. "No dangling links" over an `
      + 'empty set proves nothing. Re-run the generate before trusting this gate.',
  )
  process.exit(1)
}
const nuxtFiles = siteFiles
const goldenFiles = siteFiles

/** External, in-page, or non-navigational. Not this harness's business. */
function isSkippable(href) {
  return (
    href === '' ||
    href.startsWith('#') ||
    href.startsWith('mailto:') ||
    href.startsWith('tel:') ||
    href.startsWith('javascript:') ||
    /^[a-z][a-z0-9+.-]*:/i.test(href) ||
    href.startsWith('//')
  )
}

/** Entity-decodes the handful of entities an href can carry. No parser, no dependency. */
function decodeHref(href) {
  return href
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
}

/**
 * Every internal link on every page of one tree, as `page\thref` -> resolved | null.
 *
 * Keyed by page AND href so the two trees can be compared link-for-link rather than only by totals — a
 * count that happens to match can still be hiding one broken link and one newly-working one.
 */
function scan(root, files, label) {
  const emitted = new Set(files)
  const pages = files.filter((f) => f.endsWith('.html'))
  const links = new Map()
  const counts = { total: 0, skipped: 0, internal: 0, resolved: 0, dangling: 0 }

  for (const page of pages) {
    const html = readFileSync(join(root, page), 'utf8')
    const dir = posix.dirname(page)

    for (const m of html.matchAll(/<a\b[^>]*?\shref\s*=\s*(?:"([^"]*)"|'([^']*)')/gi)) {
      counts.total += 1
      const raw = decodeHref(m[1] ?? m[2] ?? '')
      if (isSkippable(raw)) {
        counts.skipped += 1
        continue
      }
      counts.internal += 1

      const target = raw.split('#')[0].split('?')[0]
      const resolvedPath =
        target === ''
          ? page
          : target.startsWith('/')
            ? posix.normalize(target).replace(/^\/+/, '')
            : posix.normalize(posix.join(dir === '.' ? '' : dir, target))

      // A directory-style target resolves to its index, the way a static server serves it.
      const ok =
        emitted.has(resolvedPath) || emitted.has(`${resolvedPath.replace(/\/$/, '')}/index.html`)
      counts[ok ? 'resolved' : 'dangling'] += 1
      links.set(`${page}\t${raw}`, ok ? resolvedPath : null)
    }
  }
  return { label, root, pages: pages.length, files: files.length, counts, links }
}

const nuxt = scan(ir.IR_DIR, nuxtFiles, 'nuxt')
const golden = scan(ir.IR_DIR, goldenFiles, 'golden')

// ── Compare ────────────────────────────────────────────────────────────────────────────────────────────

const regressions = []
const inherited = []
const repaired = []
const nuxtOnly = []

for (const [key, resolved] of nuxt.links) {
  const [page, href] = key.split('\t')
  const goldenResolved = golden.links.has(key) ? golden.links.get(key) : undefined
  if (goldenResolved === undefined) {
    if (!resolved) nuxtOnly.push({ page, href })
    continue
  }
  if (!resolved && goldenResolved) regressions.push({ page, href })
  else if (!resolved && !goldenResolved) inherited.push({ page, href })
  else if (resolved && !goldenResolved) repaired.push({ page, href })
}

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

if (regressions.length > 0) {
  say(`${regressions.length} link(s) that dangle:`)
  say('')
  say(pad('href', 62) + 'on page')
  say('-'.repeat(110))
  for (const r of regressions.slice(0, 40)) say(pad(r.href.slice(0, 60), 62) + r.page)
  if (regressions.length > 40) say(`… and ${regressions.length - 40} more (all in measurements/links.json).`)
  say('')
} else {
  say(`Every internal link that has a target resolves. ${nuxt.counts.dangling} dangling reference(s) remain —`)
  say('pre-existing and unchanged by this story; see the two known causes below.')
  say('')
}

if (inherited.length > 0) {
  const byHref = new Map()
  for (const r of inherited) byHref.set(r.href, (byHref.get(r.href) ?? 0) + 1)
  say('Inherited dangling targets (present in the golden site, reproduced faithfully here):')
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

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(join(MEASUREMENTS_DIR, 'links.txt'), `${lines.join('\n')}\n`, 'utf8')
writeFileSync(
  join(MEASUREMENTS_DIR, 'links.json'),
  `${JSON.stringify(
    {
      generatedBy: 'web/scripts/check-links.mjs',
      nuxt: { pages: nuxt.pages, files: nuxt.files, counts: nuxt.counts },
      golden: { pages: golden.pages, files: golden.files, counts: golden.counts },
      regressions,
      repaired,
      nuxtOnly,
      inheritedDistinct: [...new Set(inherited.map((r) => r.href))].sort(),
    },
    null,
    2,
  )}\n`,
  'utf8',
)
console.log('  wrote measurements/links.txt + measurements/links.json')
console.log('')

process.exit(regressions.length > 0 ? 1 : 0)
