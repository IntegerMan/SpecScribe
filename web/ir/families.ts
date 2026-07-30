/**
 * The IR path → surface family map. [Story 23.4 AC #1]
 *
 * ## Why this is a table and not a longer ternary ladder
 *
 * Story 23.3 branched four families inline in `pages/[...path].vue`. At four that reads fine; at fourteen a
 * nested ternary is unreviewable, and — more importantly — it cannot be *tested for completeness*. The whole
 * risk of this story is a page family that quietly keeps falling through to the pass-through while the story
 * record claims it was migrated. A table plus `resolveFamily` makes "which families exist" a value the suite
 * can assert against the real manifest (see `test/families.test.ts`), instead of a control-flow shape.
 *
 * ## Why the boundaries are TEMPLATERS, not path prefixes
 *
 * The obvious taxonomy is one family per path prefix, which yields eleven near-identical wrappers. That would
 * be the wrong kind of honesty — `IrSurface.vue`'s own doc comment says so, and it is right: what a family
 * component can legitimately own is the markup vocabulary its family *injects*, and that vocabulary is
 * produced by a C# templater, not by a directory name.
 *
 * `adrs/`, `implementation-artifacts/`, `planning-artifacts/`, `specs/`, `readme.html` and
 * `project-context.html` are all rendered by ONE templater — `HtmlTemplater.BuildDocPage` — so they inject one
 * vocabulary (`doc-header` + `article` + optional `toc-sidebar`) and they get ONE component. Splitting them
 * into six would produce six identical `<style scoped>` blocks that then drift. Conversely `timeline.html`
 * groups with `commits/**` because `TimelineTemplater` and `CommitDayTemplater` share the activity-list
 * vocabulary, even though their paths look unrelated.
 *
 * So: the FAMILY is the classification (recorded in `data-ir-family`, one per templater boundary, useful to
 * the harnesses and to a live inspection); the COMPONENT is whoever owns that vocabulary's styling.
 */

/** Every family this projection knows how to render. `pass-through` is the un-migrated fallback. */
export type IrFamily =
  | 'dashboard'
  | 'epics-index'
  | 'epic-detail'
  | 'story-detail'
  | 'doc-prose'
  | 'requirement'
  | 'follow-up'
  | 'commit-detail'
  | 'commit-day'
  | 'code-file'
  | 'insight'
  | 'portal-meta'
  | 'sprint'
  | 'retro'
  | 'pass-through'

/**
 * The root-level singletons, by owning templater. A root page is one page, so an exact-match set is both the
 * cheapest and the most auditable form — a regex over `insight|meta` names would silently capture a future
 * page whose name merely rhymed.
 */
const INSIGHT_PAGES = new Set([
  // Chart/analytics surfaces. `code-map.html` and `git-insights.html` are the manifest's two declared
  // `oversizedPages` entries (3.45 MB and 2.65 MB of chunk) — they live here, and anything that walks this
  // family must expect them.
  'cadence.html',
  'code-map.html',
  'deep-analytics.html',
  'git-insights.html',
  'impact-map.html',
  'risk-quadrant.html',
  'traceability.html',
  'work-graph.html',
])

/**
 * Pages ABOUT the portal and its vocabulary. Grouped because they share a plain prose-section vocabulary and
 * because they share a constraint that matters: `how-to-read` and `design-system` deliberately do not run
 * through the C# reference linkifier (they define the glossary, so they must not self-expand it), and
 * `about-sdd*` likewise. Their regions therefore carry no `<abbr>` expansions and no FR/story links, and a
 * reader comparing them against a doc-prose page must not read that as a defect.
 */
const PORTAL_META_PAGES = new Set([
  'about.html',
  'about-sdd.html',
  'design-system.html',
  'diagnostics.html',
  'how-to-read.html',
])

/**
 * Root pages rendered by `HtmlTemplater.BuildDocPage` — the same templater as every `adrs/` and
 * `implementation-artifacts/` page, so they are doc-prose despite sitting at the root.
 */
const DOC_PROSE_PAGES = new Set(['project-context.html', 'readme.html'])

/** `epics/epic-{N}.html` — `EpicsViewBuilder`'s path shape. */
const EPIC_DETAIL = /^epics\/epic-[^/]+\.html$/
/** `epics/story-{id}.html`, dots already replaced by dashes — `StoryEpicLinkifier.StoryPagePath`. */
const STORY_DETAIL = /^epics\/story-[^/]+\.html$/

/**
 * Classifies one IR path.
 *
 * @param path an IR page key — an output-relative path with its `.html` extension, forward slashes, no
 *   leading slash. This is the manifest key verbatim (ADR 0017).
 * @param entry the manifest's entry page, so the dashboard is identified structurally rather than by
 *   hard-coding `index.html` (a project could name its entry differently, and the two-IR experiment in
 *   Story 23.5 is exactly the case that would break).
 */
export function resolveFamily(path: string, entry: string): IrFamily {
  if (path === entry) return 'dashboard'
  if (path === 'epics.html') return 'epics-index'
  if (EPIC_DETAIL.test(path)) return 'epic-detail'
  if (STORY_DETAIL.test(path)) return 'story-detail'

  // Directory families, most populous first — this is a cheap linear scan per page either way, but ordering
  // it this way keeps the hot paths (follow-ups 411, commit 300, code 264) at the front.
  if (path.startsWith('follow-ups/')) return 'follow-up'
  if (path.startsWith('commit/')) return 'commit-detail'
  if (path.startsWith('code/')) return 'code-file'
  if (path.startsWith('implementation-artifacts/')) return 'doc-prose'
  if (path.startsWith('requirements/')) return 'requirement'
  if (path.startsWith('adrs/')) return 'doc-prose'
  if (path.startsWith('commits/')) return 'commit-day'
  if (path.startsWith('planning-artifacts/')) return 'doc-prose'
  if (path.startsWith('specs/')) return 'doc-prose'
  if (path.startsWith('retros/')) return 'retro'

  // Root singletons.
  if (path === 'action-items.html') return 'follow-up'
  if (path === 'requirements.html') return 'requirement'
  if (path === 'retros.html') return 'retro'
  if (path === 'sprint.html') return 'sprint'
  if (path === 'timeline.html') return 'commit-day'
  if (path === 'deferred-work.html') return 'follow-up'
  if (path === 'ideas.html' || path.startsWith('ideas/')) return 'doc-prose'
  if (path === 'test-artifacts.html') return 'doc-prose'
  if (INSIGHT_PAGES.has(path)) return 'insight'
  if (PORTAL_META_PAGES.has(path)) return 'portal-meta'
  if (DOC_PROSE_PAGES.has(path)) return 'doc-prose'
  // `about-sdd-{framework}.html` — one page per known SDD framework, added to as frameworks are supported,
  // so this is a prefix rather than another six entries in PORTAL_META_PAGES.
  if (path.startsWith('about-sdd-')) return 'portal-meta'

  return 'pass-through'
}
