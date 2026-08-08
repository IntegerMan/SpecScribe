// Shared link-resolution logic for `check:links` and `pin:links`. [Story 23.3 AC #4; code review 2026-08-08]
//
// ONE scanner, imported by both the gate and the command that pins its baseline — the same arrangement
// `ir-content-lib.mjs` has with its extractor and checker, and for the same reason: if the pin script had its
// own copy of `scan()`, the two could disagree about what "dangling" means and the baseline would silently
// stop matching the thing it exempts.
//
// Zero npm dependencies (ADR 0010): Node built-ins and plain string work, no HTML parser.

import { readFileSync } from 'node:fs'
import { join, posix } from 'node:path'

/** External, in-page, or non-navigational. Not this harness's business. */
export function isSkippable(href) {
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
export function decodeHref(href) {
  return href
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
}

/**
 * Every internal link on every page of the tree, as `page\thref` -> resolved path | null.
 *
 * Keyed by page AND href so a link can be tracked individually rather than only by totals — a count that
 * happens to match can still be hiding one newly-broken link and one newly-fixed one.
 */
export function scan(root, files) {
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
      const ok = emitted.has(resolvedPath) || emitted.has(`${resolvedPath.replace(/\/$/, '')}/index.html`)
      counts[ok ? 'resolved' : 'dangling'] += 1
      links.set(`${page}\t${raw}`, ok ? resolvedPath : null)
    }
  }
  return { root, pages: pages.length, files: files.length, counts, links }
}

/**
 * Splits the scanned links against a pinned baseline. PURE, and exported so it can be tested directly.
 *
 * ⚠️ It is a pure function for a specific reason. The bucket this replaces lived inline in `check-links.mjs`
 * and was `!resolved && goldenResolved` over two maps that Story 23.6 made identical — so it silently became
 * unreachable and the gate could not fail for months, with nothing able to notice. A classifier that can be
 * called with two hand-written inputs is a classifier a test can prove still fails on a new dangling link,
 * which `test/links-lib.test.mjs` does. [Story 23.3 code review 2026-08-08]
 *
 * @param links   `page\thref` -> resolved path | null, from `scan()`
 * @param known   the baseline's `danglingKey` strings
 */
export function classifyAgainstBaseline(links, known) {
  const newlyDangling = []
  const stillDangling = []
  const live = new Set()

  for (const [key, resolved] of links) {
    if (resolved) continue
    const tab = key.indexOf('\t')
    const page = key.slice(0, tab)
    const href = key.slice(tab + 1)
    live.add(key)
    if (known.has(key)) stillDangling.push({ page, href })
    else newlyDangling.push({ page, href })
  }

  // In the baseline but no longer dangling — the link was fixed, or its page or href is gone. Not a failure;
  // reported so the baseline shrinks with the debt instead of ossifying into a blanket exemption.
  const fixed = [...known]
    .filter((k) => !live.has(k))
    .map((k) => {
      const tab = k.indexOf('\t')
      return { page: k.slice(0, tab), href: k.slice(tab + 1) }
    })

  return { newlyDangling, stillDangling, fixed }
}

/**
 * Refuses to treat a near-empty tree as a clean site.
 *
 * Shared by the gate and the pin command so neither can be run against a stub: pinning an empty baseline
 * would be worse than a vacuous gate, because it would then look deliberate.
 */
export function assertRealCorpus(label, dir, files) {
  const pages = files.filter((f) => f.endsWith('.html'))
  if (pages.length < 50) {
    console.error(
      `${label} — VACUOUS: only ${pages.length} page(s) under ${dir}. A link conclusion over an empty set ` +
        'proves nothing. Re-run the generate first.',
    )
    process.exit(1)
  }
  return pages
}
