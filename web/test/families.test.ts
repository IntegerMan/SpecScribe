import { readFileSync, existsSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import { resolveFamily, type IrFamily } from '../ir/families'

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
 */
const IR_DIR = process.env.SPECSCRIBE_IR_DIR ?? join(import.meta.dirname, '..', '..', 'SpecScribeOutput', 'spa')
const MANIFEST = join(IR_DIR, 'manifest.json')

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
})

describe.skipIf(manifest === null)('every page in the generated IR resolves to a real family', () => {
  it('leaves NOTHING on pass-through', () => {
    const m = manifest!
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
