# Story 8.2: Canonical Status Model with Portal-Wide Legend

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer scanning any page,
I want every status badge to use one canonical vocabulary per entity type,
so that I never have to mentally map between competing status words.

## Acceptance Criteria

1.
**Given** the projection model defines one canonical lifecycle per entity type (requirement, epic, story)
**When** any framework's artifacts are projected
**Then** the framework's native status vocabulary maps to the canonical lifecycle at the mapping seam, with the mapping documented
**And** no framework-specific status label is hard-coded in shared rendering (NFR8). [Source: epics.md#Story 8.2; FR20, NFR8]

2.
**Given** any badge, chart segment, or legend renders a status
**When** the page is generated
**Then** the status routes through the `--status-*` token system so a given state always gets the same word and the same color everywhere
**And** a status-legend affordance reachable from any badge explains what each stage means. [Source: epics.md#Story 8.2; FR20, UX-DR17]

3.
**Given** the classifier encounters a native status with no canonical mapping
**When** projection runs
**Then** the entity renders in a visible "unrecognized" state rather than being silently mislabeled
**And** generation completes with a non-fatal notice. [Source: epics.md#Story 8.2; FR20, NFR2]

---

## Developer Context

**This is the first substantive story of Epic 8 (Dashboard Command Center) and a foundation stone for Epics 8–10 — Story 8.1 is a short cross-surface integration spike ahead of it, added 2026-07-14.** Epic 8's later stories and Epics 9–10 all lean on the canonical status vocabulary this story hardens — the epics file says so explicitly for Story 8.3 ("they use the same canonical status vocabulary as Story 8.2", epics.md:1252). So the deliverable here is **not** a new feature surface so much as *locking and self-explaining the status system that already exists*, then extending it in three precise ways.

**The good news: the hard part is already built.** SpecScribe already has a single status→stage classifier (`StatusStyles.cs`) and a single stage→color source (the `--status-*` CSS tokens). Story 1.5 established this so the sunburst, donuts, funnel, and badges can never disagree on what color a stage is. Do **not** rebuild that — study it, then extend it. [Source: `src/SpecScribe/StatusStyles.cs`, `src/SpecScribe/assets/specscribe.css:33-40`; project memory — status-token system]

### The three real deliverables (everything else is audit + documentation)

1. **Document + lock the canonical lifecycle** (AC #1). Make `StatusStyles` the explicitly-documented *native-vocabulary → canonical-lifecycle* mapping seam, one canonical lifecycle per entity type, and audit shared rendering so **no framework-specific status word is hard-coded** in a templater (every visible status word comes from a `StatusStyles.*Label(...)` method, never an inline string literal in a `.cs` templater).

2. **A status-legend affordance reachable from any badge** (AC #2) — the one genuinely *new* surface. Two parts (owner-selected design direction below): a **hover/focus tooltip on every badge** naming what that stage means, plus a **compact always-visible six-stage legend key** rendered once per page so the meaning is present even without hovering.

3. **A visible "unrecognized" status state + a non-fatal generation notice** (AC #3), replacing today's *silent* fallback where an unmapped status quietly becomes "drafted"/"pending".

### Owner-selected design decisions (do not re-litigate)

**Legend affordance form → badge tooltip + shared legend key.** Every `StatusStyles.Badge(...)` gains a hover/focus tooltip (via the existing body-level `.ss-tooltip` / `js-tip` + `data-tip` plumbing) that names what that lifecycle stage means. Additionally, a compact six-stage legend **key** (swatch + icon + word + one-line meaning) renders once per page so the vocabulary is legible without any interaction. This reuses infrastructure that already exists (see "Reuse, don't reinvent") and is literally "reachable from any badge." [Owner decision, this story]

**Adapter-abstraction scope → harden `StatusStyles` in place; defer the formal adapter contract to Epic 4.** Do **NOT** build a new `ICanonicalStatusAdapter` / framework-adapter contract in this story. Story 4.1 (`ready-for-dev`) owns the shared adapter contract — and reading it **confirms this boundary**: 4.1's `IArtifactAdapter` is an *ingestion* seam that emits the already-parsed models (`EpicsModel`, `SprintStatus`, …) with **raw native `Status` strings intact** — it explicitly does *not* map status vocabulary ("do not re-shape the existing models", templaters unchanged, byte-for-byte identical output). So native→canonical status mapping is **not** adapter-ingestion work; it stays downstream in `StatusStyles`. For 8.2, **`StatusStyles` *is* the mapping seam** — AC #1's "adapter layer" is satisfied by making `StatusStyles` the single, documented native→canonical mapping point (BMad is today's only "framework"; its keyword mapping stays). Structure/document the seam so a foreign adapter (Stories 4.3–4.7) can later supply its own per-framework native→canonical status map without rewriting `StatusStyles` — but do **not** build that injection now (no second vocabulary exists yet). Make the seam explicit, documented, and self-explaining — not speculatively abstract. [Owner decision, this story; see "Relationship to Story 4.1" below]

### Relationship to Story 4.1 (both `ready-for-dev` — coordinate)

Story 4.1 (Shared Framework Adapter Contract) is now written and `ready-for-dev`; it and 8.2 touch adjacent seams, so read this before starting:

- **No status-mapping conflict.** 4.1's `IArtifactAdapter`/`ArtifactBundle` is ingestion only and carries **raw parsed models** — it does not classify status. 8.2's `StatusStyles` work runs entirely in the downstream projection/rendering path on those same models. The two do not overlap on status logic; 8.2 stays valid whether 4.1 has landed or not. [Source: `_bmad-output/implementation-artifacts/4-1-shared-framework-adapter-contract-and-projection-path.md` — Scope Decision, Dev Notes]
- **They DO overlap on the non-fatal-diagnostics channel (AC #3).** 4.1 introduces a typed **`AdapterDiagnostic`** with categories `Unsupported | Malformed | Skipped | Error`, formalizing today's ad-hoc `GenerationOutcome.Error` events into one contract. An **unrecognized status is conceptually an `Unsupported` diagnostic** (a valid artifact carrying an unmapped field value). Coordinate by sequencing:
  - **If 4.1 has landed first:** route 8.2's unrecognized-status notice through the `AdapterDiagnostic` channel (`Unsupported` category) rather than emitting a bespoke `GenerationEvent` — reuse the typed contract, don't fork it.
  - **If 8.2 lands first:** use the existing `GenerationEvent`/`ConsoleUi.PrintInitialSummary` path (as specified below); leave a `// TODO(4.1): fold into AdapterDiagnostic` marker so 4.1 subsumes it into the typed channel.
  - Either way, **do not end up with two parallel notice mechanisms** — whichever story lands second reconciles them. Note your choice in Completion Notes.
- **Shared "seam Epic 4 generalizes" idiom.** 4.1 documents `ArtifactCoverage.Specs`, `ModuleContext.Detect`, and `CommandCatalog` as existing "one seam a future adapter swaps" precedents. `StatusStyles` should read as another member of that family — a documented seam a foreign adapter's status map will feed, matching that established idiom (not a competing abstraction). [Source: `4-1-...md` Dev Notes — "existing generalization seams"]

### Scope boundaries (read carefully)

- **This is not the glossary — but build the legend as a reusable seam for the one that's coming.** Epic 10 Story 10.3 (FR29) owns the full "how to read this portal" glossary/orientation page + first-use acronym expansion + adapter-supplied vocabulary. **10.3 is deliberately NOT drafted yet**: it's a legibility layer that annotates surfaces still being reshaped by Epics 8–9 and Story 10.1 (nav overhaul / Structure-page retirement), so specifying it now would target a moving IA. That is fine for 8.2 — but it means 8.2's per-badge tooltip + legend key are the portal's **first "define a term where it appears" surface, and 10.3 will generalize them.** So:
  - Keep 8.2's affordance the **status lifecycle stages only** — a focused status key, not a general glossary. Do **not** build a standalone glossary page here.
  - Build the tooltip + legend-key rendering as a **single, small, documented, reusable helper** (e.g. the `StatusStyles.StageMeaning`/`LegendKey` seam), rendered from **exactly one code location** — explicitly so 10.3 can later *absorb, extend, or link to* it rather than reverse-engineering or duplicating a one-off. A bespoke, hard-to-reuse legend here would force a refactor (or a confusing second "what does this mean?" affordance) when 10.3 lands.
  - **Forward-coordination:** when 10.3 is eventually drafted (after its upstream UI-overhaul epics), flag this seam into its context as the existing vocabulary-explanation primitive to build on. Leave a short `// 10.3: vocabulary-explanation seam — extend, don't duplicate` marker at the helper so the thread isn't lost.
- **Don't touch the chart legends' zero-suppression.** The dashboard's Epic-Status and Requirements chart legends deliberately suppress zero-count rows (Story 1.5). The **new status-legend key is different**: it is a static *reference* key that always shows all six stages (plus deferred), regardless of counts, because its job is to teach the vocabulary — not summarize this project's data. Keep these two concepts separate. [Source: project memory — status-token system]
- **`ForRequirement` can't be "unrecognized."** Requirement status is a parsed enum (`RequirementStatus`), already mapped upstream by `RequirementsParser`. The "unrecognized" state (AC #3) applies only to the **free-text** classifiers where a raw string arrives unmapped: story `Status:` (`ForStatus`) and the sprint-status ledger (`ForSprint`). Scope the new behavior there. [Source: `src/SpecScribe/StatusStyles.cs:84-91`]
- **Leave `ForDoc` alone (mostly).** `ForDoc`/`DocLabel` intentionally render a planning document's *own* self-reported word (`final`, `draft`, …) title-cased — it is explicitly **not** a lifecycle claim (see its XML doc). An unmapped doc status is *expected*, not an error, so do **not** flood the unrecognized-notice with every freeform doc word. The unrecognized state is about **lifecycle-tracked entities** (AC #1's requirement/epic/story), which flow through `ForStatus`/`ForSprint`. [Source: `src/SpecScribe/StatusStyles.cs:137-161`]

### The critical distinction for AC #3: *absent* status vs. *present-but-unmapped* status

Today `ForStatus(null)` and `ForStatus("")` fall back to **"drafted"** — a story listed in `epics.md` with no implementation artifact yet. **Preserve that.** An *absent* status legitimately means "drafted, not yet classified." The new "unrecognized" state is **only** for a status string that is genuinely present but matches none of the known keywords (e.g. `Status: frobnicated`). So:

- `null` / empty / whitespace → **"drafted"** (unchanged — a not-yet-started story).
- Non-empty string matching a known keyword → its existing stage (unchanged).
- **Non-empty string matching *nothing* → new "unrecognized" state + notice** (this is the change).

Getting this boundary wrong would reclassify every not-yet-drafted story as "unrecognized" and bury the portal in false notices. Cover it with a test.

---

## Technical Requirements (Dev Agent Guardrails)

### DO

- **Keep `StatusStyles` the single classifier.** All new logic (unrecognized detection, stage meanings, the legend key HTML) lives in or beside `StatusStyles`, never re-implemented in a templater. The whole point of this story is one seam.
- **Document the canonical lifecycle explicitly.** Add/extend XML-doc on `StatusStyles` stating: the canonical six-stage lifecycle (`pending → drafted → ready → active → review → done`, plus `deferred` for requirements), which entity types use which classifier, and that this class **is the native-vocabulary → canonical-lifecycle mapping seam** future adapters (Epic 4) plug into. This documentation *is* AC #1's "the mapping documented."
- **Route the legend key swatches through `--status-*` tokens.** The key's colored swatches must use `var(--status-pending)` … `var(--status-done)` / `var(--status-deferred)` — never literal hex. Same single-source discipline as every chart. [Source: `src/SpecScribe/assets/specscribe.css:33-40`; project memory — status-token system]
- **Pair color + icon + word everywhere (UX-DR17).** The legend key rows and badges already carry the status icon via `StatusStyles.Icon(...)`; keep every status representation color + shape + text, never color-only. The unrecognized state needs its own icon + word too. [Source: `src/SpecScribe/StatusStyles.cs:163-172`; UX-DR17]
- **Extend `StatusStyles.Badge(...)` to attach the tooltip.** Add `js-tip` to the badge's class list and a `data-tip="<stage meaning>"` attribute, sourced from a new stage→meaning method (e.g. `StatusStyles.StageMeaning(cssClass)`). A `<span class="status-badge {cls} js-tip" data-tip="…">` is picked up automatically by the existing tooltip JS (`HOVER = SEG + ", .js-tip"`). Escape the tip text (`PathUtil.Html`). [Source: `src/SpecScribe/StatusStyles.cs:168-172`, `src/SpecScribe/assets/specscribe.js:31-48,80-81`]
- **Render the six-stage legend key once per page.** Add a `StatusStyles.LegendKey()` (or a small helper) that emits all stages (swatch + icon + word + short meaning). The single, deterministic home that already appears on every content page is the shared footer — render it there via `PathUtil.RenderFooter`, or gate it into the status-bearing templaters if you prefer a tighter scope. Whatever you pick, render it in **exactly one place in code** so it can't drift. [Source: `src/SpecScribe/PathUtil.cs:73-74`]
- **Make "unrecognized" a first-class stage in the vocabulary.** Add an `"unrecognized"` css class, a `--status-unrecognized` token with a *visibly distinct, non-lifecycle* treatment (e.g. hatched/outlined neutral — it must NOT read as one of the six real stages), an `Icons.ForStatus` glyph for it, and a `StatusStyles` label (e.g. "Unrecognized"). Keep the entity's **raw native word** as the badge text where one exists (don't blank it) so the reader sees *what* the unmapped value was. [Source: `src/SpecScribe/StatusStyles.cs`, `src/SpecScribe/Icons.cs`, `src/SpecScribe/assets/specscribe.css:33-40`]
- **Surface the non-fatal notice through the typed diagnostics channel — coordinated with Story 4.1.** If 4.1 has landed, route the unrecognized-status notice through its `AdapterDiagnostic` (`Unsupported` category); if 8.2 lands first, use the existing console summary that already prints Skipped/Error counts (`ConsoleUi.PrintInitialSummary`) — a Skipped-style/warning line naming the unmapped value(s) — and leave a `// TODO(4.1): fold into AdapterDiagnostic` marker. **Exactly one** notice mechanism, never two (see "Relationship to Story 4.1"). Generation must still complete successfully (NFR2). Derive the notice **only** from input data so a from-scratch CI regen is identical. [Source: `src/SpecScribe/ConsoleUi.cs:120-146`, `src/SpecScribe/SiteGenerator.cs` (GenerationEvent/Outcome); `4-1-...md` Task 4 (AdapterDiagnostic)]
- **Audit shared rendering for hard-coded status words (NFR8).** Grep every templater for inline status string literals used as visible labels; route any stragglers through `StatusStyles.*Label`. This audit is AC #1's teeth. [Source: `StatusStyles.Badge` call sites — `EpicsTemplater.cs`, `RequirementsTemplater.cs`, `SprintTemplater.cs`, `ActionItemsTemplater.cs`, `RetroActionStyler.cs`, `HtmlTemplater.cs`]

### DON'T

- **DON'T build an adapter contract / `ICanonicalStatusAdapter` / framework registry.** That is Epic 4 (FR1), still backlog. `StatusStyles` is the seam for now (owner decision above). Speculative abstraction here is out of scope.
- **DON'T build a glossary page.** Story 10.3 (FR29) owns that. Keep 8.2's legend a focused *status* key.
- **DON'T change the six lifecycle colors or the existing chart legends.** Sunburst/donut/funnel/req-flow legends stay exactly as they are (including dashboard zero-row suppression). You are *adding* a static reference key and per-badge tooltips, not touching chart legends.
- **DON'T reclassify absent statuses as "unrecognized."** `null`/empty → "drafted" (unchanged). Only a *present, unmatched* string becomes "unrecognized" (see the critical distinction above).
- **DON'T make "unrecognized" reuse a lifecycle color.** It must be visually distinct from pending/drafted/ready/active/review/done, or it silently mislabels — the exact failure AC #3 forbids.
- **DON'T add `tabindex="0"` to every badge to make the tooltip keyboard-reachable.** That would flood the tab order with dozens of non-interactive spans. The **always-visible legend key** is the accessible explanation path (plus each badge already carries its word + icon, not color-only). Treat the badge tooltip as a progressive hover/focus enhancement, not the sole accessible channel. (See a11y note in Dev Notes.)
- **DON'T add a client-side status engine or new JS.** Reuse the one sanctioned `specscribe.js` tooltip node. No new scripts. [Source: project memory — charts are pure SVG + links, no JS]
- **DON'T write back to any source.** Local-first, read-only invariant.

---

## Architecture Compliance

Relevant invariants [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`]:

- **Graceful degradation is contractual** — an unmapped status degrades to a *visible* "unrecognized" badge + non-fatal notice; generation always completes (NFR2). Never throw on an unknown status. [AC #3]
- **Deterministic, generation-time-only output** — the legend key, tooltips, and unrecognized notices derive solely from input artifacts; a from-scratch regen of the same inputs is byte-identical. No per-visitor or cross-build state. (This is also the shape Story 8.8 will formalize for recency signals — keep the discipline.)
- **Single source of truth** — `StatusStyles` (stage) + `--status-*` tokens (color) remain the single sources; this story reinforces them, it does not add a parallel one. [Source: project memory — status-token system]
- **Accessibility is part of the rendering contract** (NFR6, UX-DR16, UX-DR17): status is never color-only; the legend key gives a persistent, non-hover explanation. [UX-DR17]
- **Seed, not invariant** — the Core/Adapters package split in `rendering-architecture.md` is aspirational; the current monolithic `StatusStyles` + per-page templater pattern is the established one. Follow it; don't force the split (and don't pre-build Epic 4's adapter contract). [Source: `_bmad-output/specs/spec-specscribe/rendering-architecture.md`]

---

## Library / Framework Requirements

- **.NET 10 / C#**, `Nullable` + `ImplicitUsings` enabled. **No new NuGet packages.** [Source: `tests/SpecScribe.Tests/SpecScribe.Tests.csproj`]
- **Reuse, don't reinvent (all of this already exists):**
  - `StatusStyles.ForStatus / ForSprint / ForEpic / ForRequirement / *Label / Icon / Badge` — the classifier + badge renderer you are extending. [Source: `src/SpecScribe/StatusStyles.cs`]
  - The `--status-*` CSS tokens in `:root` — the stage→color source; add only `--status-unrecognized` beside them. [Source: `src/SpecScribe/assets/specscribe.css:33-40`]
  - The body-level tooltip: `.js-tip` + `data-tip` → the never-clipped `.ss-tooltip` node, served by `specscribe.js` (`ensureTip`/`activate`, `white-space: pre-line` for multi-line). Badges opt in with the class + attribute; no JS change needed. [Source: `src/SpecScribe/assets/specscribe.js:20-48,80-81`, `src/SpecScribe/assets/specscribe.css:2425-2442`; project memory — tooltip clipping → use .ss-tooltip node]
  - `Icons.ForStatus(cssClass)` — the status glyph seam; add the unrecognized glyph here. [Source: `src/SpecScribe/Icons.cs`, `src/SpecScribe/StatusStyles.cs:163-166`]
  - `PathUtil.RenderFooter` / `PathUtil.Html` — the shared footer (single-source legend-key home) + escaping. [Source: `src/SpecScribe/PathUtil.cs:73-74,81-85`]
  - `ConsoleUi.PrintInitialSummary` + `GenerationEvent`/`GenerationOutcome` — the non-fatal-notice surface. [Source: `src/SpecScribe/ConsoleUi.cs:120-168`, `src/SpecScribe/GenerationReporter.cs`]

---

## File Structure Requirements

**No new production classes are expected** — this story extends existing seams. If a helper grows large, a small `StatusLegend.cs` is acceptable, but prefer methods on `StatusStyles`.

**Modified files (read fully before editing):**

- `src/SpecScribe/StatusStyles.cs` — the heart of this story. Add: canonical-lifecycle documentation (AC #1); an `"unrecognized"` classification path for `ForStatus`/`ForSprint` (present-but-unmatched only); a `StageMeaning(cssClass)` (or `LegendEntries`) source for tooltips + the key; a `LegendKey()` renderer; extend `Badge(...)` to attach `js-tip` + `data-tip`; a label + icon for "unrecognized". **Preserve:** every existing method signature and the `null/empty → "drafted"` fallback. [Source: `src/SpecScribe/StatusStyles.cs`]
- `src/SpecScribe/Icons.cs` — add the `unrecognized` status glyph in `ForStatus`. [Source: `src/SpecScribe/Icons.cs`]
- `src/SpecScribe/assets/specscribe.css` — add `--status-unrecognized` beside the other status tokens; add `.status-badge.unrecognized`, the `.status-legend-key` (or chosen class) rows/swatches, and (if needed) the badge's `js-tip` cursor affordance. Route swatches through the tokens. **Note:** `StylesheetTests.cs` asserts on stylesheet content — add companion assertions if you add classes/tokens it should guard. [Source: `src/SpecScribe/assets/specscribe.css:33-40`, `tests/SpecScribe.Tests/StylesheetTests.cs`]
- `src/SpecScribe/PathUtil.cs` **or** the status-bearing templaters — wherever you render the legend key once. If in the footer, thread it through `RenderFooter` (or a sibling helper called alongside it). [Source: `src/SpecScribe/PathUtil.cs:73-74`]
- `src/SpecScribe/SiteGenerator.cs` — collect unrecognized-status occurrences during epics/sprint parsing and emit the non-fatal `GenerationEvent`(s) so the summary reports them. Keep it additive; don't disturb phase ordering. [Source: `src/SpecScribe/SiteGenerator.cs`]
- Badge call sites, **only if** the audit finds a hard-coded status word: route it through `StatusStyles.*Label`. Expected to be few/none. [Source: the 12 `StatusStyles.Badge` call sites listed above]

**Tests to update (behavior change) / add:**

- `tests/SpecScribe.Tests/StatusStylesTests.cs` — **update** `ForStory_MapsStatusKeywords`: `("something else", …)` now expects `"unrecognized"` (was `"drafted"`); **keep** `(null, "drafted")`. Add explicit cases for the absent-vs-unmapped distinction and for `ForSprint` unmapped → unrecognized. Add tests for `Badge(...)` emitting `js-tip`/`data-tip`, `StageMeaning`, and `LegendKey()` covering all six stages. [Source: `tests/SpecScribe.Tests/StatusStylesTests.cs:27-37`]
- `tests/SpecScribe.Tests/StylesheetTests.cs` — assert `--status-unrecognized` and the legend-key classes are present. [Source: `tests/SpecScribe.Tests/StylesheetTests.cs`]
- A generation-level test (extend an existing `SiteGenerator*Tests` or add one) — a story with a genuinely unmapped `Status:` produces a visible unrecognized badge **and** a non-fatal notice (no `Error` outcome), while a story with no status stays "drafted" with no notice.
- Check `HtmlTemplaterTests`, `SprintTemplaterTests`, `EpicsParserTests`/rendering tests for assertions on `status-badge` HTML that the added `js-tip`/`data-tip` attributes might break; update expectations. [Source: `tests/SpecScribe.Tests/HtmlTemplaterTests.cs`, `SprintTemplaterTests.cs`]

---

## Testing Requirements

Test framework: **xUnit** (`net10.0`). `StatusStyles` logic is pure and unit-testable directly (no IO) — the existing `StatusStylesTests` is your model. Generation-level tests build a temp `_bmad-output` tree and assert on emitted HTML / `GenerateAll` outcomes (`AssertNoErrors` pattern). [Source: `tests/SpecScribe.Tests/StatusStylesTests.cs`, `tests/SpecScribe.Tests/SiteGeneratorTraceabilityTests.cs`]

Cover explicitly:

- **Absent vs. unmapped (the critical distinction):** `ForStatus(null)`/`ForStatus("")` → `"drafted"`; `ForStatus("frobnicated")` → `"unrecognized"`; every known keyword → its existing stage (regression guard).
- **`ForSprint` unmapped:** a sprint-status value matching nothing → `"unrecognized"` (today it silently returns `"pending"` and `SprintLabel` title-cases the word — verify the word is preserved but the *class* is now unrecognized).
- **Badge tooltip wiring:** `Badge(cls, label)` output contains `js-tip` and a `data-tip` equal to `StageMeaning(cls)` (escaped); existing `status-badge {cls}` + icon + label still present.
- **Legend key completeness:** `LegendKey()` renders one row per canonical stage (all six + deferred as applicable), each with a `--status-*`-driven swatch, an icon, the stage word, and its meaning; it does **not** suppress zero-count rows (it's a static reference key).
- **Non-fatal notice (generation-level):** an unmapped story status yields a visible unrecognized badge and a reported non-fatal notice; `GenerateAll` reports **no** `Error` outcome; a story with no status yields no notice.
- **Determinism:** two generations over identical input produce identical output (badges, key, and notice text).
- **Regression:** all pre-existing `StatusStyles`/templater/stylesheet tests pass after updates; the six lifecycle colors and chart legends are unchanged.

**Run:** `dotnet test` from repo root. Then a full generation pass against this actual repo: `dotnet run --project src/SpecScribe` (output lands in `SpecScribeOutput/`, the default — **do not** pass `--output docs/live`; that flag is vestigial/gitignored). Eyeball: hover a badge → tooltip explains the stage; the legend key is present and legible; introduce a temporary bogus `Status:` on a story and confirm it renders unrecognized (distinct treatment) with a console notice, then revert. [Source: project memory — Generate output dir is SpecScribeOutput]

---

## Previous Story Intelligence

**This is Story 8.2 — the first substantive story in Epic 8** (Story 8.1 is a short cross-surface integration spike, non-blocking on this story's content) — so there is no prior *substantive* story in this epic. The relevant prior art is the entire status system, built incrementally; study these before writing:

- **Story 1.5 (Dashboard Insight Polish & Visual Truthfulness)** — introduced the `--status-*` single stage→color source and killed the sunburst/donut color drift. This is the invariant you're reinforcing. [Source: project memory — status-token system; `src/SpecScribe/assets/specscribe.css:33-40`]
- **Story 2.3 (Sprint Status)** — added `StatusStyles.ForSprint`/`SprintLabel` (the yaml lifecycle mapping you're extending with "unrecognized"). Note its deliberate "unknown → title-cased word, not invented color" behavior — your change keeps the *word* but adds the unrecognized *class* + notice. [Source: `src/SpecScribe/StatusStyles.cs:102-135`]
- **Story 2.4 (Planning Artifacts)** — added `ForDoc`/`DocLabel` (the doc's own self-reported word, explicitly *not* a lifecycle claim — the reason it's out of scope for the unrecognized state). [Source: `src/SpecScribe/StatusStyles.cs:137-161`]
- **Story 2.5 (Standardized Iconography)** — added `StatusStyles.Icon` + `StatusStyles.Badge` (the single icon+text badge renderer you're extending with the tooltip) and the "color + icon + word, never icon-only" rule (UX-DR17). [Source: `src/SpecScribe/StatusStyles.cs:163-172`]
- **Story 3.5 / 3.7** — the tooltip node (`.ss-tooltip`/`js-tip`/`data-tip`) and its clipping-safe body-level design you're reusing for badges. [Source: project memory — tooltip clipping → use .ss-tooltip node]

**Recurring lessons that apply here:**

- **Truthfulness over convenience** — the whole status system exists so planned work never reads as done ("green creep"). The unrecognized state is the same principle: an unknown status must never *quietly* read as a real stage. [Source: `src/SpecScribe/StatusStyles.cs:3-5`]
- **"What real input shape does this silently drop?"** is a standing Epic 3 review lesson (parsers silently dropping real inputs — renamed-file undercount, plural-Epics coverage drop). AC #3 is the direct antidote for status: surface the unmapped value instead of coercing it. [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml` action_items, epic 3]
- Use invariant/culture-safe string handling (the codebase has been bitten by culture-sensitive parsing/formatting). `StatusStyles.Normalize` already lower-invariants — stay on that path. [Source: `src/SpecScribe/StatusStyles.cs:174`]

---

## Git Intelligence Summary

Recent history is planning/retro/iteration churn (`Sprint 3 retro, future stories created`, `3.7 review`, `docs: add site-wide UX review`, `New dev`). The tree is clean on `main`. Epics 8–10 were just added to `epics.md` from the site-wide UX review (`spec-site-ux-review-journeys-and-feedback.md`); **they are not yet in `sprint-status.yaml`** — this story's creation adds the Epic 8 block. No in-flight code touches `StatusStyles`, so the change is additive and uncontended. **Heed the worktree rule:** if this runs in a worktree, edit files at the worktree path — `main` has a background auto-committer, so never re-root paths at `C:\Dev\SpecScribe`. [Source: project memory — worktree edits must target the worktree path]

---

## Latest Technical Information

No external libraries or APIs are introduced, so there is no version/security research to fold in. Everything needed — string classification, HTML string-building, the tooltip node, the `--status-*` tokens — already exists in-repo. The only "latest" note is a discipline one: keep all new derived text (stage meanings, notices) built from `System.Globalization.CultureInfo.InvariantCulture` where casing/formatting is involved, matching `StatusStyles.TitleCase`. [Source: `src/SpecScribe/StatusStyles.cs:176-177`]

---

## Project Context Reference

- Epic 8 goal + FR/UX-DR/NFR mapping: [Source: `_bmad-output/planning-artifacts/epics.md:1057-1061,201-203`]
- FR20 (canonical lifecycle + adapter-layer mapping + status legend), FR21, FR25, FR31: [Source: `_bmad-output/planning-artifacts/epics.md:58-59,63,69`]
- NFR8 (insight/guidance surfaces are framework-agnostic; degrade gracefully): [Source: `_bmad-output/planning-artifacts/epics.md:82`]
- UX-DR17 (status never color-only), UX-DR21–24 (Epic 8 UX-DRs): [Source: `_bmad-output/planning-artifacts/epics.md:120,124-127`]
- Site-wide UX review that seeded Epics 8–10: [Source: `_bmad-output/implementation-artifacts/spec-site-ux-review-journeys-and-feedback.md`, `docs/UserJourneys.md`]
- Architecture invariants (graceful degradation, single-source, seed-not-invariant): [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`, `rendering-architecture.md`]
- Status-token discipline, tooltip-node reuse, pure-render/no-JS conventions, SpecScribeOutput default: project memory (status-token system; tooltip clipping → use .ss-tooltip node; charts are pure SVG + links, no JS; Generate output dir is SpecScribeOutput).

---

## Tasks / Subtasks

- [ ] **Task 1 — Document + lock the canonical lifecycle (AC: #1)**
  - [ ] Add/extend `StatusStyles` XML-doc: the canonical six-stage lifecycle, which classifier maps which entity type, and the explicit statement that this class is the native→canonical **mapping seam** future adapters (Epic 4) plug into. (No adapter contract — see scope decision.)
  - [ ] Audit the 12 `StatusStyles.Badge` call sites + templaters for any hard-coded visible status word; route stragglers through `StatusStyles.*Label` (NFR8). Note findings in Completion Notes.
- [ ] **Task 2 — Stage meanings + badge tooltip (AC: #2)**
  - [ ] Add `StatusStyles.StageMeaning(cssClass)` (or a `LegendEntries` list) giving each stage a one-line plain-language meaning.
  - [ ] Extend `StatusStyles.Badge(...)` to add `js-tip` to the class list and `data-tip="{StageMeaning}"` (escaped). Verify the existing tooltip JS picks it up (`HOVER = SEG + ", .js-tip"`), no JS change needed.
- [ ] **Task 3 — Shared six-stage legend key (AC: #2)**
  - [ ] Add `StatusStyles.LegendKey()` rendering all canonical stages: swatch (`var(--status-*)`) + `Icon` + word + meaning. Do **not** suppress zero rows — it's a static reference key.
  - [ ] Render it once per page (single code location — footer via `PathUtil.RenderFooter` recommended). Add `.status-legend-key` styles routing swatches through the tokens.
- [ ] **Task 4 — Unrecognized status state (AC: #3)**
  - [ ] Add `--status-unrecognized` token (visually distinct, non-lifecycle) + `.status-badge.unrecognized` styles + `Icons.ForStatus` glyph + a `StatusStyles` label.
  - [ ] Change `ForStatus`/`ForSprint` so a **present-but-unmatched** string classifies as `"unrecognized"` while **null/empty stays "drafted"** (`ForStatus`) / existing empty behavior (`ForSprint`); preserve the raw native word as the badge text. Add the unrecognized stage to the legend key.
- [ ] **Task 5 — Non-fatal generation notice (AC: #3)**
  - [ ] Collect unrecognized-status occurrences during generation and emit a non-fatal notice; generation still completes with no `Error` outcome. Derive solely from input (deterministic).
  - [ ] **Coordinate with Story 4.1:** if its `AdapterDiagnostic` channel exists, use it (`Unsupported` category); otherwise emit via `GenerationEvent`/`ConsoleUi.PrintInitialSummary` and leave a `// TODO(4.1)` marker. One mechanism only. Record the choice in Completion Notes.
- [ ] **Task 6 — Tests (AC: #1, #2, #3)**
  - [ ] Update `StatusStylesTests` for the absent-vs-unmapped distinction; add badge-tooltip, `StageMeaning`, `LegendKey`, and `ForSprint`-unmapped cases.
  - [ ] Add `--status-unrecognized`/legend-key assertions to `StylesheetTests`; add a generation-level unrecognized-status + non-fatal-notice test; fix any `status-badge` HTML assertions broken by the added attributes.
- [ ] **Task 7 — Full generation pass + manual verify (AC: #1, #2, #3)**
  - [ ] `dotnet test` green; run a real generation (default `SpecScribeOutput/`). Hover a badge → tooltip; confirm the legend key renders; temporarily set a bogus `Status:` → unrecognized badge + console notice, then revert.

## Dev Notes

### Cross-surface note from Story 8.1 (2026-07-14)

Spike traced the live status path: `StatusStyles` + `--status-*` tokens → shared `PageView.BodyHtml` → HTML / webview / SPA. Classifier + badge markup in the body is **shared-path**. Two placement/affordance choices need attention here:

1. **Do not put the legend key only in `PathUtil.RenderFooter`.** That footer is appended by `HtmlRenderAdapter.Render` (HTML shell only). `WebviewRenderAdapter.RenderContent` / `JsonSpaRenderAdapter.RenderContent` emit nav + breadcrumb + `BodyHtml` and **never include the footer**. Prefer rendering `LegendKey()` once into the content region (body/chrome that all three adapters share), or also inject it where webview/SPA wrap content — otherwise the legend is HTML-only by accident.
2. **Badge `js-tip` / `data-tip` tooltips** depend on `specscribe.js`. Webview does not load that script (CSP + documented progressive-enhancement exclusion). The always-visible legend key is the webview-safe channel (matches this story’s a11y note). Optionally add a native `title=` on badges as a non-JS fallback; do not treat webview hover tips as required for AC #2.
3. **CLI:** unrecognized-status notices via `GenerationEvent` / `ConsoleUi.PrintInitialSummary` are correct — CLI does not project lifecycle badges.

- **The one behavior change with a sharp edge:** *absent* status (`null`/empty) stays "drafted"; only a *present, unmatched* string becomes "unrecognized". Encode and test this precisely — the inverse would drown the portal in false unrecognized notices for every not-yet-drafted story.
- **Accessibility of the tooltip:** a `<span>` badge is not keyboard-focusable, so the hover/focus tooltip is a **progressive enhancement**, not the accessible channel. The accessible explanation is the **always-visible legend key** plus each badge's own color+icon+word. Do **not** blanket-add `tabindex="0"` to badges (tab-order noise). If you later want a keyboard-reachable per-badge tip, do it deliberately for a specific interactive badge, not globally.
- **"Unrecognized" must look unrecognized.** Pick a treatment (hatched fill / dashed outline / neutral grey with a distinct glyph) that cannot be confused with any of the six real stages or with `deferred`. If it reads as "pending", you've reintroduced the silent-mislabel that AC #3 exists to kill.
- **Legend key ≠ chart legend.** The chart legends (sunburst/donut/funnel) summarize *this project's counts* and suppress zeros; the status legend key *teaches the vocabulary* and always shows every stage. Keep them separate — don't "helpfully" unify them.
- **Scope guard for later stories:** the full glossary (10.3), the single count source (8.3), paired progress/readiness (8.4), and state-aware next-steps (8.5) are **not** this story. 8.2 gives them the locked, self-explaining status vocabulary they consume.

### Project Structure Notes

- Nearly all change concentrates in `StatusStyles.cs` (+ small edits to `Icons.cs`, `specscribe.css`, the footer/generator). This is intentional: the whole story reinforces "one seam." No package restructure (that's a deferred seed, Epics 4/6), no new adapter contract (Epic 4).
- The status legend key is new HTML but new *reference* HTML, not a new page type — it lives inside the existing page shell.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md:1063-1087`] — Story 8.2 user story + all three ACs.
- [Source: `_bmad-output/planning-artifacts/epics.md:1057-1061,201-203`] — Epic 8 goal + FR/UX-DR/NFR coverage.
- [Source: `_bmad-output/planning-artifacts/epics.md:58,82,120`] — FR20, NFR8, UX-DR17.
- [Source: `src/SpecScribe/StatusStyles.cs`] — the single classifier + `Badge`/`Icon`/`*Label`/`ForStatus`/`ForSprint` to extend; `null→drafted` fallback to preserve.
- [Source: `src/SpecScribe/assets/specscribe.css:33-40`] — the `--status-*` `:root` tokens; add `--status-unrecognized` here.
- [Source: `src/SpecScribe/assets/specscribe.js:20-48,80-81`] — the `.ss-tooltip`/`js-tip`/`data-tip` tooltip node badges opt into (no JS change).
- [Source: `src/SpecScribe/assets/specscribe.css:2425-2442`] — `.ss-tooltip` rendering + `white-space: pre-line`.
- [Source: `src/SpecScribe/Icons.cs`] — `ForStatus` glyph seam; add the unrecognized glyph.
- [Source: `src/SpecScribe/PathUtil.cs:73-74`] — `RenderFooter` (single-source legend-key home) + `Html` escaping.
- [Source: `src/SpecScribe/ConsoleUi.cs:120-168`] — `PrintInitialSummary` (non-fatal notice surface).
- [Source: `src/SpecScribe/GenerationReporter.cs`] — `GenerationOutcome`/event model for the notice.
- [Source: `tests/SpecScribe.Tests/StatusStylesTests.cs:27-37`] — the test to update for the absent-vs-unmapped change.
- [Source: `tests/SpecScribe.Tests/StylesheetTests.cs`] — stylesheet-content assertions to extend.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`, `rendering-architecture.md`] — invariants + "seed, not invariant" (no forced adapter split).
- [Source: `_bmad-output/implementation-artifacts/4-1-shared-framework-adapter-contract-and-projection-path.md`] — the ingestion adapter contract (`IArtifactAdapter`/`ArtifactBundle`/`AdapterDiagnostic`) 8.2 coordinates its AC #3 notice with; confirms status mapping is NOT adapter-ingestion work (stays in `StatusStyles`).
- [Source: `_bmad-output/implementation-artifacts/spec-site-ux-review-journeys-and-feedback.md`] — the UX review that seeded Epics 8–10.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
