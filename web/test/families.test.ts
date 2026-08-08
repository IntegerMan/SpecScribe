import { readFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { ALL_FAMILIES, resolveFamily, type IrFamily } from '../ir/families'

/**
 * The completeness gate for Story 23.4's family migration. [AC #1]
 *
 * The failure this exists to catch is the story's central risk: a page family that quietly keeps falling
 * through to `PassThroughSurface` while the record claims it was migrated. A pass-through renders correctly,
 * links correctly, passes the link and a11y harnesses, and is invisible in every measurement except a
 * deliberate count — exactly the profile of Story 23.3's double-wrapped band.
 *
 * So this asserts against the REAL manifest rather than a fixture list: every page in the generated IR must
 * resolve to a real family, and `pass-through` must be empty. A hand-written fixture would only ever prove
 * that the table matches itself.
 *
 * ⚠️ Skips (rather than fails) when no IR is present. `vitest.config.ts` sets `SPECSCRIBE_PACKAGE_BUILD=1`
 * for the whole run precisely so unit tests need no generated portal, and this file must not reintroduce that
 * dependency for everyone. The trade-off is stated so it is not mistaken for coverage: on a machine with no
 * `SpecScribeOutput/`, this gate does not run. CI generates the portal before the web tests, so it runs there.
 *
 * ⚠️⚠️ **`SPECSCRIBE_IR_DIR` is the OUTPUT ROOT, and the reader appends `spa/` itself.**
 * [Story 23.4 code review, finding F-4] This file used to treat the variable as the `spa/` directory, so
 * setting it — the documented way to point the suite at another checkout's output, and what `render-lib.mjs`
 * and `experiment-two-ir.mjs` both do — resolved `<root>/manifest.json`, found nothing, and SKIPPED the gate.
 * A skip is green. The one action that makes this gate meaningful was the action that disabled it. Every other
 * consumer already had it right: `ir/adapter.ts` (`join(IR_DIR, 'spa', …)`), `scripts/ir-content-lib.mjs`
 * (`pathResolve(irDir, 'spa', …)`), `NuxtPrerender.cs` (`GetFullPath(outputRoot)`).
 */
const OUTPUT_ROOT = process.env.SPECSCRIBE_IR_DIR ?? join(import.meta.dirname, '..', '..', 'SpecScribeOutput')
const MANIFEST = join(OUTPUT_ROOT, 'spa', 'manifest.json')

type Manifest = { entry: string; pages: Record<string, unknown> }

const manifest: Manifest | null = existsSync(MANIFEST)
  ? (JSON.parse(readFileSync(MANIFEST, 'utf8')) as Manifest)
  : null

describe('resolveFamily', () => {
  it('classifies the four families Story 23.3 migrated', () => {
    expect(resolveFamily('index.html', 'index.html')).toBe('dashboard')
    expect(resolveFamily('epics.html', 'index.html')).toBe('epics-index')
    expect(resolveFamily('epics/epic-23.html', 'index.html')).toBe('epic-detail')
    expect(resolveFamily('epics/story-23-4.html', 'index.html')).toBe('story-detail')
  })

  it('identifies the dashboard by the manifest entry, not by the name "index.html"', () => {
    // Story 23.5's two-IR experiment rendered a DIFFERENT project's IR. A dashboard hard-coded to
    // `index.html` is the class of assumption that breaks there, so the entry is threaded through.
    expect(resolveFamily('home.html', 'home.html')).toBe('dashboard')
    expect(resolveFamily('index.html', 'home.html')).not.toBe('dashboard')
  })

  it('groups by owning templater, not by path prefix', () => {
    // One templater (HtmlTemplater.BuildDocPage) ⇒ one family, across five different path shapes.
    for (const p of [
      'adrs/0023-some-decision.html',
      'implementation-artifacts/23-4-a-story.html',
      'planning-artifacts/epics.html',
      'specs/spec-specscribe.html',
      'readme.html',
      'project-context.html',
    ]) {
      expect(resolveFamily(p, 'index.html'), p).toBe('doc-prose')
    }

    // And conversely: two unrelated-looking paths that share the activity-list vocabulary.
    expect(resolveFamily('commits/2026-07-29.html', 'index.html')).toBe('commit-day')
    expect(resolveFamily('timeline.html', 'index.html')).toBe('commit-day')
  })

  it('does not confuse epics.html with planning-artifacts/epics.html', () => {
    // The same basename at two depths, with genuinely different owners. A basename-based table would fuse
    // them and render the planning doc through the epics-index component.
    expect(resolveFamily('epics.html', 'index.html')).toBe('epics-index')
    expect(resolveFamily('planning-artifacts/epics.html', 'index.html')).toBe('doc-prose')
  })

  it('keeps the follow-up vocabulary together across its three entry points', () => {
    expect(resolveFamily('follow-ups/group-epic-23.html', 'index.html')).toBe('follow-up')
    expect(resolveFamily('follow-ups/action-1-some-item.html', 'index.html')).toBe('follow-up')
    expect(resolveFamily('action-items.html', 'index.html')).toBe('follow-up')
    expect(resolveFamily('deferred-work.html', 'index.html')).toBe('follow-up')
  })

  it('returns pass-through only for a genuinely unknown path', () => {
    expect(resolveFamily('some-page-nobody-planned.html', 'index.html')).toBe('pass-through')
  })

  it('lets a page keep its OWN family even when it is also the manifest entry', () => {
    // [Story 23.4 code review, finding F-20] The entry check used to precede every other rule, so a project
    // whose entry page is also a page this table names rendered it through DashboardSurface, stamped it
    // `data-ir-family="dashboard"`, and ran `dashboardContract` against a prose page — emitting the
    // "no [data-hierarchy] mount point" warning on every build. That is the project-independence case the
    // `entry` parameter exists for, failing in the opposite direction.
    expect(resolveFamily('epics.html', 'epics.html')).toBe('epics-index')
    expect(resolveFamily('readme.html', 'readme.html')).toBe('doc-prose')
    // A page with no family of its own still becomes the dashboard when it IS the entry — unchanged.
    expect(resolveFamily('home.html', 'home.html')).toBe('dashboard')
  })
})

/**
 * The router-completeness gate. [Story 23.4 code review, finding F-15]
 *
 * `pages/[...path].vue` types its map as `Record<IrFamily, Component>`, which the story cites as making a
 * missing component a type error. Nothing type-checks in this project: there is no `typecheck` script, no
 * `vue-tsc` dependency, and no typecheck step in any workflow — `nuxt build` runs Vite/esbuild, which strips
 * types without checking them. So that guarantee existed only in an editor.
 *
 * Adding a type-checker is a dependency decision ADR 0010 reserves for the owner. Reading the router as TEXT
 * needs nothing, runs in the suite that already exists, and catches the exact regression the type was meant
 * to: a family in the classifier with no component behind it.
 */
describe('every IrFamily has a component in the router', () => {
  const router = readFileSync(join(import.meta.dirname, '..', 'pages', '[...path].vue'), 'utf8')
  const map = router.slice(router.indexOf('const SURFACES'), router.indexOf('const surface'))

  it.each(ALL_FAMILIES)('routes %s', (family) => {
    // Keys are quoted only when hyphenated, so accept both forms.
    const key = new RegExp(`(^|[{\\s])'?${family}'?\\s*:`, 'm')
    expect(
      key.test(map),
      `IrFamily "${family}" has no entry in pages/[...path].vue's SURFACES map, so it would render as ` +
        `undefined. Add a component for it.`,
    ).toBe(true)
  })

  it('has no entry the union does not name — a stale key is a component nothing can reach', () => {
    const keys = [...map.matchAll(/^\s+'?([a-z-]+)'?\s*:/gm)].map((m) => m[1]!)
    expect(keys.length).toBeGreaterThan(0)
    expect([...keys].sort()).toEqual([...ALL_FAMILIES].sort())
  })
})

describe.skipIf(manifest === null)('every page in the generated IR resolves to a real family', () => {
  it('leaves NOTHING on pass-through', () => {
    const m = manifest!

    // ⚠️ Non-vacuity guard FIRST. [Story 23.4 code review, finding F-4]
    // With `pages === {}` the loop below never runs, `stragglers` is `[]`, and the assertion passes having
    // classified nothing — "nothing falls through to PassThroughSurface" reported over an empty corpus. That
    // is the failure mode this repository has shipped repeatedly, and the guard for it was already written
    // twice elsewhere (`validateOracle` in `parity-lib.mjs`, `SHARED_PRIMITIVES.length > 0` in
    // `ir-content-lib.test.mjs`) and simply not applied here.
    const pageCount = Object.keys(m.pages).length
    expect(
      pageCount,
      'the IR manifest carried ZERO pages — this gate would pass vacuously, which is worse than failing',
    ).toBeGreaterThan(0)

    const byFamily = new Map<IrFamily, string[]>()
    for (const path of Object.keys(m.pages)) {
      const family = resolveFamily(path, m.entry)
      const bucket = byFamily.get(family) ?? []
      bucket.push(path)
      byFamily.set(family, bucket)
    }

    const stragglers = byFamily.get('pass-through') ?? []
    expect(
      stragglers,
      `${stragglers.length} IR page(s) still fall through to PassThroughSurface — they are NOT migrated, ` +
        `whatever the story record says. First 20:\n  ${stragglers.slice(0, 20).join('\n  ')}`,
    ).toEqual([])

    // A tally in the output, so a run of this gate doubles as the AC #1 per-family count and nobody has to
    // trust a number typed into a story file by hand.
    const tally = [...byFamily.entries()]
      .sort((a, b) => b[1].length - a[1].length)
      .map(([f, ps]) => `${f}: ${ps.length}`)
      .join(', ')
    console.log(`[families] ${Object.keys(m.pages).length} IR pages — ${tally}`)
  })
})
