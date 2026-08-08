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
 * Every member of {@link IrFamily}, as a VALUE. [Story 23.4 code review, finding F-15]
 *
 * The story's stated defence against "a family added to the classifier without a component" is that
 * `pages/[...path].vue`'s `Record<IrFamily, Component>` makes the omission a type error. It does — in an
 * editor. There is no `typecheck` script, no `vue-tsc` dependency and no typecheck step in any workflow, and
 * `nuxt build` goes through Vite/esbuild, which STRIPS types without checking them. So that guarantee ran
 * nowhere, and adding a type-checker is a dependency decision `web/`'s zero-dep posture (ADR 0010) reserves
 * for the owner.
 *
 * This list is the dependency-free half: a value the suite can iterate, so `test/families.test.ts` can assert
 * the router actually handles every family. The `satisfies` clause keeps it honest in both directions — add a
 * member to the union without adding it here and the type check fails at the same place a reviewer is already
 * looking.
 */
export const ALL_FAMILIES = [
  'dashboard',
  'epics-index',
  'epic-detail',
  'story-detail',
  'doc-prose',
  'requirement',
  'follow-up',
  'commit-detail',
  'commit-day',
  'code-file',
  'insight',
  'portal-meta',
  'sprint',
  'retro',
  'pass-through',
] as const satisfies readonly IrFamily[]

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
  // ⚠️ The entry check is deliberately NOT first any more. [Story 23.4 code review, finding F-20]
  // `if (path === entry) return 'dashboard'` used to precede every other rule, so a project whose manifest
  // entry is also a page this table names — `epics.html`, `readme.html`, `about.html` are all plausible entry
  // points for a project without a generated dashboard — rendered that page through `DashboardSurface`,
  // stamped it `data-ir-family="dashboard"`, and ran `dashboardContract` against it, emitting the
  // "carries no [data-hierarchy] mount point" warning on every single build. That is precisely the
  // project-independence case the `entry` parameter was added for (Story 23.5's two-IR experiment), failing
  // in the other direction. A page with a family of its own keeps it; `entry` decides only the leftovers.
  const own = resolveKnownFamily(path)
  if (own !== null) return own
  if (path === entry) return 'dashboard'
  return 'pass-through'
}

/** The table proper: every family this projection can name from the path alone, or `null` for none. */
function resolveKnownFamily(path: string): IrFamily | null {
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
  // ⚠️ `ideas*` and `test-artifacts.html` are PORTAL-META, not doc-prose. [Story 23.4 code review, F-20]
  // They were classified `doc-prose`, which contradicts this file's own stated rule — the family IS the owning
  // templater, and these are `IdeasTemplater`'s and `TestArtifactsTemplater`'s vocabulary (`.ta-*`,
  // `.module-coverage-*`), not `HtmlTemplater.BuildDocPage`'s `doc-header`/`doc-body`/`toc-sidebar`. Two
  // concrete consequences of the old mapping: `data-ir-family` reported a value that was false for those
  // pages, misleading a live inspection and any harness bucketing by it; and owner decision D5's authored
  // prose stylesheet, once it lands scoped to `DocProseSurface`, would have applied to two surfaces that are
  // not prose — the "a plain scoped rule matches injected markup NOWHERE and fails silently" hazard reached
  // from the other side. `portal-meta` is the right home: pages ABOUT the portal, sharing a plain
  // section vocabulary, and its component makes no doc-prose structural claim.
  if (path === 'ideas.html' || path.startsWith('ideas/')) return 'portal-meta'
  if (path === 'test-artifacts.html') return 'portal-meta'
  if (INSIGHT_PAGES.has(path)) return 'insight'
  if (PORTAL_META_PAGES.has(path)) return 'portal-meta'
  if (DOC_PROSE_PAGES.has(path)) return 'doc-prose'
  // `about-sdd-{framework}.html` — one page per known SDD framework, added to as frameworks are supported,
  // so this is a prefix rather than another six entries in PORTAL_META_PAGES.
  if (path.startsWith('about-sdd-')) return 'portal-meta'

  return null
}
