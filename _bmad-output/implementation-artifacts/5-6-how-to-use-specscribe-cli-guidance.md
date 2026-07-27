---
baseline_commit: 5a96f711c8f10654e011cac23a5823079634d565
---

# Story 5.6: How to use SpecScribe — CLI Generate and Watch Guidance

Status: in-progress

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a first-time visitor opening the portal Help menu,
I want "How to use SpecScribe" to explain how to generate and refresh the site from the CLI (and where settings live),
so that demos and onboarding cover both reading the portal and producing it — without scattering CLI docs only in the README.

## Context & Origin (read first)

Seeded 2026-07-20 from the About SDD redesign: Help → "How to use SpecScribe" ([how-to-read.html](../../src/SpecScribe/HowToReadTemplater.cs)) currently covers **reading order + glossary only**. This story adds a third section — generating/refreshing the site from the CLI — so the page lives up to its title ("how to use SpecScribe," not "how to read this portal"). The templater's own doc comment says as much today:

> "CLI generate/watch guidance is intentionally out of scope here (Epic 5 follow-on story)." — [HowToReadTemplater.cs:9](../../src/SpecScribe/HowToReadTemplater.cs)

That comment is this story. Remove/replace it once the section lands.

**This is a content/rendering story, not a CLI story.** No `SiteSettings`/`Commands.cs`/`ForgeOptions` changes — you are documenting the CLI surface those files already expose, not changing it.

## Acceptance Criteria

1. **Given** a full generate
   **When** I open Help → How to use SpecScribe
   **Then** the page still includes the honest reading-order and glossary sections (NFR8)
   **And** it adds a concise "Generate with SpecScribe" section covering at least `generate` and `watch` with smart defaults aligned to Stories 5.1–5.3
   **And** it links to About Spec-Driven Development for framework orientation (not duplicating that matrix).

2. **Given** directory-scoped settings and/or CLI overrides from Story 5.2
   **When** the How to use page describes configuration
   **Then** it names the same effective settings surface users see on Diagnostics (Story 4.8)
   **And** copy stays framework-agnostic in shared chrome (NFR8).

## Tasks / Subtasks

- [x] **Task 1 — Add the "Generate with SpecScribe" section (AC: #1)**
  - [x] In [HowToReadTemplater.cs](../../src/SpecScribe/HowToReadTemplater.cs), add a new private `AppendGenerateSection` method (mirror `AppendReadingOrder`/`AppendGlossary`'s shape: `h2` with a stable `id`, appended into the same `sections` `StringBuilder` in `RenderPage`) and call it **between** `AppendReadingOrder` and `AppendGlossary` — reading order and "how to produce the site" are both onboarding flows; glossary/commands are reference material consulted later. This section is unconditional (always renders — `generate`/`watch` always exist, unlike the gated reading-order steps), so it needs no availability guard.
  - [x] Content (keep to EXPERIENCE.md's voice: active voice, numbers over adjectives, no marketing fluff — [EXPERIENCE.md:61-71](../../_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md)):
    - One-shot vs. continuous: `specscribe generate` builds once; `specscribe watch` keeps rebuilding as source files change.
    - Smart defaults (AC1 "aligned to 5.1–5.3"): for a supported repo layout, no flags are required — source and output roots are auto-discovered. State this as present-tense fact, not a promise; it is already true today (`ForgeOptions.Resolve`'s walk-up discovery predates Epic 5 — see Dev Notes).
    - Overrides: name `--source`, `--adrs`, `--output` for a non-standard layout, and point to `specscribe generate --help` for the full option list rather than reproducing every flag (avoids drift when the flag surface grows).
  - [x] Update the page's meta description ([HowToReadTemplater.cs:21](../../src/SpecScribe/HowToReadTemplater.cs), currently "…a suggested reading order and a glossary of the terms used throughout") and the `doc-subtitle`/intro paragraph ([:41](../../src/SpecScribe/HowToReadTemplater.cs), [:54-57](../../src/SpecScribe/HowToReadTemplater.cs)) to mention generating/refreshing the site, not just reading it. These currently promise only "reading order and glossary" — leaving them as-is after this story would make the page's own subtitle inaccurate.
  - [x] Remove the "CLI generate/watch guidance is intentionally out of scope" line from the class doc comment ([HowToReadTemplater.cs:9](../../src/SpecScribe/HowToReadTemplater.cs)) and replace with an accurate one-line description of the new section.
  - [x] The existing "For framework overviews and SpecScribe support, see [About Spec-Driven Development]" link ([HowToReadTemplater.cs:57](../../src/SpecScribe/HowToReadTemplater.cs)) already satisfies AC1's "links to About SDD" clause — do not duplicate the About SDD support matrix inside the new section; a single sentence pointer is enough if the new section wants its own, but it's not required.

- [x] **Task 2 — Name the same effective-settings surface as Diagnostics, framework-agnostically (AC: #2)**
  - [x] Reuse the **exact field labels** `DiagnosticsTemplater.RenderConfig` already renders ([DiagnosticsTemplater.cs:264-272](../../src/SpecScribe/DiagnosticsTemplater.cs)) — "Source root", "ADR location", "Output directory", "README included", "Deep-git analytics", "External source base" — so a reader can map this page's prose directly onto what they'll see after a real run. Don't invent parallel/renamed terms.
  - [x] State that settings can be saved per-repository via the interactive menu's "Configure paths" (persisted to `.specscribe`, gitignored/personal — never committed) and that CLI flags always override the saved value for a single run (the CLI-wins precedence `SettingsStore.ApplyTo` already implements — see [5-2 story](5-2-directory-scoped-settings-with-interactive-and-cli-parity.md) Dev Notes: "the persistence primitives already exist... do not rebuild them").
  - [x] Add one link to `diagnostics.html` (`SiteNav.DiagnosticsOutputPath`) as "see exactly what a given run resolved" — the definitive per-run values, not restated here.
  - [x] **Framework-agnostic (NFR8):** no methodology-specific nouns (no "BMad", no `_bmad-output`, no slash-command names) — this page renders identically regardless of detected module, exactly like `AppendReadingOrder`/`AppendGlossary` already do (they describe *slots*, not *BMad's* slots). Say "your spec artifacts directory" / "source root", not any framework's actual folder name.

- [x] **Task 3 — Fix the now-stale Help quick-link blurb (AC: #1, regression)**
  - [x] [SiteNav.cs:146](../../src/SpecScribe/SiteNav.cs) — the command-palette/quick-links entry for this page reads `"Reading order and glossary for this portal."` This becomes inaccurate once the page also covers generating the site. Update the blurb to mention both (e.g. `"Reading order, glossary, and how to generate the site."`). Leave the nav label itself ("How to use SpecScribe", [:142](../../src/SpecScribe/SiteNav.cs)) unchanged — it was already generic enough.

- [x] **Task 4 — Tests + golden regen**
  - [x] Extend [SiteGeneratorHowToReadTests.cs](../../tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs) (follow its existing fixture/assertion style — plain `Assert.Contains`/`DoesNotContain` on rendered HTML, no new fixture needed):
    - New section renders with a stable heading and id, and mentions both `specscribe generate` and `specscribe watch`.
    - Section names `--source`, `--adrs`, `--output` and points to `--help` rather than reproducing every flag.
    - Section names the Diagnostics field labels (or a strict subset) and links `href="diagnostics.html"`.
    - `Assert.DoesNotContain` any BMad-specific token (mirror the file's existing `Assert.DoesNotContain("sdd-tab", …)` / `Assert.DoesNotContain("class=\"mermaid\"", …)` discipline) — e.g. assert the new section text doesn't contain `"_bmad-output"` or `"BMad"`.
    - Existing tests (`HowToRead_ReadingOrder_*`, `HowToRead_Glossary_*`, `HowToRead_BypassesApplyReferenceLinks`, `HowToRead_NavAndH1LabeledHowToUseSpecScribe`) must stay green unmodified in behavior — only the subtitle/intro text assertions (if any exist against the exact current wording) need updating for Task 1's copy change; check none currently pin the literal subtitle sentence before assuming a change is needed.
  - [x] Add/extend a `SiteNavTests.cs` assertion for the updated quick-link blurb (Task 3) if an existing test pins the old string; otherwise add one.
  - [x] `how-to-read.html` is written on every full run ([HowToReadTemplater.cs:9](../../src/SpecScribe/HowToReadTemplater.cs): "Written on every full run"), so this story **will** move the golden content fingerprint ([SiteGeneratorAdapterTests.cs:224](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs), `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`). Regenerate it and record the new hash in Completion Notes. **Confirm the hash is stable across two clean runs before locking it into the test constant** (stale-build first-captured-hash trap — [memory: golden-diff-normalization-gotchas]).
  - [x] Run the full suite: `dotnet test` from repo root; all existing tests stay green.

### Review Findings

- [x] [Review][Patch] SiteNav quick-link blurb lists sections in stale order — "Reading order, glossary, and how to generate the site." names glossary before generate, but the rendered page order (this story's own RenderPage restructuring) is Reading order → Generate → Glossary/Commands. Fixed: reworded to "Reading order, how to generate the site, and glossary." in `SiteNav.cs` and the pinning assertion in `SiteNavTests.cs`. [src/SpecScribe/SiteNav.cs:198]
- [x] [Review][Patch] Dangling "— and" grammar fragment in the no-module-content doc-subtitle — "Orientation for a first visit — and how to generate this site yourself." reads as an incomplete sentence next to the true-branch phrasing. Fixed: reworded to "Orientation for a first visit — including how to generate this site yourself." [src/SpecScribe/HowToReadTemplater.cs:62]
- [x] [Review][Patch] Golden-fingerprint provenance comment miscounts the story's file set — says "this story's four files" but the story's own File List has five. Fixed: corrected to "five files". [tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:980]
- [x] [Review][Defer] Doc-subtitle/intro `hasModuleContent` gate can overclaim reading-order OR glossary content — `hasModuleContent = readingOrder.Length > 0 || reference.Length > 0` licenses copy that promises BOTH "the reading order" and "glossary below," but each half is independently gated; a modeled module with no reading-order pages (or, more rarely, no glossary/commands) gets copy promising a section that isn't there. Pre-existing ambiguity (the original single `sections.Length > 0` gate had the same shape); this story added a third promise ("then generate the site yourself") on top of it without tightening the check. [src/SpecScribe/HowToReadTemplater.cs:49-81] — deferred, pre-existing
- [x] [Review][Defer] `<meta description>` tag never varies with `hasModuleContent` — it's built at the top of `RenderPage` before `hasModuleContent` is known, so it always claims "a suggested reading order... and a glossary of the terms used throughout" even on an undetected-module run where neither renders. Pre-existing (the original meta description was equally unconditional); this story only added the (always-true) generate clause to the same static string. [src/SpecScribe/HowToReadTemplater.cs:23-26] — deferred, pre-existing

**⚠️ Verification blocked at patch time (2026-07-27):** `dotnet build` currently fails on unrelated, uncommitted concurrent-session work (`ImpactMapTemplater.cs` referencing undefined members also touched in `Charts.cs`/`HierarchyExplorer.cs`/`DashboardViewBuilder.cs` — none of which this story or its patches touch). Per this repo's shared-main convention, that in-progress work was left alone rather than reset/reverted. As a result the 3 patches above (all rendered-text changes to `how-to-read.html` and the sitewide quick-link tooltip band) could NOT be verified via `dotnet test`, and the golden content fingerprint constant was NOT regenerated — it is now stale on top of these patches in addition to whatever later sibling stories already moved it. **Re-run `dotnet test --filter FullyQualifiedName~GoldenContentFingerprint` once the build is unblocked**, regenerate the constant, and confirm it twice before locking in.

## Dev Notes

### This story is not hard-blocked on 5.1/5.2/5.3 landing first — but check current source before writing copy

The seed note says "expand… once Epic 5's CLI surface (5.1–5.3) is the source of truth for flags and defaults," expressing a *sequencing preference*, not a hard gate. In practice:

- `generate`/`watch` and their auto-discovery **already exist and predate Epic 1** ([5-1 story](5-1-cli-generate-and-watch-modes-with-smart-defaults.md) Dev Notes: "already exists... do not rebuild the command surface"). Story 5.1 hardens exit codes and non-interactive feedback; it does not rename flags or change discovery semantics.
- `.specscribe` persistence, CLI-wins precedence, and the interactive "Configure paths" flow **already exist** ([5-2 story](5-2-directory-scoped-settings-with-interactive-and-cli-parity.md) Dev Notes: "The persistence primitives already exist... do not rebuild them"). Story 5.2 formalizes provenance and closes a CLI-vs-interactive parity gap; it does not change the settings vocabulary this page needs to name.

So this story can land in any order relative to 5.1/5.2/5.3 without going stale on the *facts* it states. **Still verify against the live source at implementation time** — read [SiteSettings.cs](../../src/SpecScribe/SiteSettings.cs) for the current `[CommandOption]` set (as of this story's authoring: `-s|--source`, `-a|--adrs`, `-o|--output`, `-p|--project-name`, `--no-readme`, `--deep-git`, `--spa`, `--code-url`, `--serve`) and [DiagnosticsTemplater.cs](../../src/SpecScribe/DiagnosticsTemplater.cs) for the current config row labels, rather than trusting this story's snapshot if either sibling story has landed and changed something in the meantime (e.g. a new `--show-config` or `--today-policy` flag from 5.2/5.5).

### Framework-agnostic chrome (NFR8) — the load-bearing constraint

> "Insight surfaces and guidance affordances… are framework-agnostic in shared rendering: framework-specific content flows through the adapter contract… and surfaces degrade gracefully." — NFR8, [epics.md:99](../../_bmad-output/planning-artifacts/epics.md)

`HowToReadTemplater` already honors this: `AppendReadingOrder` names *slots* ("Readme", "ADRs", "Epics") not BMad's literal folder names, and `AppendCommandLegend` explicitly attributes slash commands to "your detected methodology" rather than hardcoding BMad's. The new Generate section must follow the same discipline — it describes SpecScribe's CLI (framework-agnostic by construction, since the CLI itself doesn't vary per module), but avoid slipping in a BMad-specific example path or folder name while writing it.

### Why this lives in `HowToReadTemplater`, not a new page/file

The page is called from exactly one site: `SiteGenerator.WriteHowToRead` ([SiteGenerator.cs:3160-3166](../../src/SpecScribe/SiteGenerator.cs)), passing `(nav, _module.Docs, _module.Glossary, _module.Commands)`. No new parameters are needed for this story — the new section needs no module-derived data (it's static CLI documentation), so it doesn't touch that call site or its signature.

### What NOT to do

- Don't reproduce the full `--help` flag table on this page — link to `specscribe generate --help` instead (Task 1). Restating every flag creates a second place that drifts when Epic 5 adds options.
- Don't add a Toc/sidebar to this page — it doesn't use `Toc.WrapWithSidebar` today (unlike Epics/Story/Retro pages) and this story doesn't change that; the page is short enough that four `h2` sections read fine without one.
- Don't add JS or a `<details>` disclosure for the new section — the page has none today and this content doesn't need progressive disclosure.
- Don't touch `SiteSettings.cs`, `Commands.cs`, `ForgeOptions.cs`, or `SettingsStore.cs` — this is a documentation story about the CLI, not a CLI change.

### Project Structure Notes

- Sole edit target for the page itself: `src/SpecScribe/HowToReadTemplater.cs` (new private method + subtitle/intro/doc-comment text edits).
- Secondary edit: `src/SpecScribe/SiteNav.cs:146` (one string literal — the quick-link blurb).
- No new files, no new nav entries, no new CSS classes needed — reuse `howtoread-panel`/existing `h2`/`p` styling already applied to this page's other sections.
- Tests: `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` (primary), `tests/SpecScribe.Tests/SiteNavTests.cs` (quick-link blurb, if pinned), `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` (golden fingerprint regen).

### Testing standards summary

- xUnit `[Fact]` under `tests/SpecScribe.Tests/`, temp-dir fixtures via `Directory.CreateTempSubdirectory` — mirror `SiteGeneratorHowToReadTests`'s existing constructor/`Dispose` pattern, don't add a new fixture class.
- Prefer `Assert.Contains`/`Assert.DoesNotContain` on rendered HTML strings (the file's established style) over parsing HTML.
- The golden content fingerprint is an expected-delta guardrail here, not a byte-identical one (unlike 5.5's AC1) — this story *intends* to change `how-to-read.html`; the test just needs a fresh, confirmed-stable hash.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 5.6] — story statement + ACs + seeding rationale (lines 887-910).
- [Source: _bmad-output/planning-artifacts/epics.md:99] — NFR8 (framework-agnostic shared rendering).
- [Source: src/SpecScribe/HowToReadTemplater.cs] — the page templater (primary edit target); doc comment at :9 names this story's boundary explicitly.
- [Source: src/SpecScribe/SiteNav.cs:135-149] — Help nav entries + quick-links (blurb edit target at :146).
- [Source: src/SpecScribe/DiagnosticsTemplater.cs:109-140,256-272] — `DiagnosticsConfig`/`RenderConfig`, the field-label vocabulary AC2 requires reuse of.
- [Source: src/SpecScribe/SiteSettings.cs] — current CLI option surface (verify live before writing copy).
- [Source: src/SpecScribe/SiteGenerator.cs:3160-3166] — `WriteHowToRead` call site (unchanged signature).
- [Source: tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs] — existing test file to extend.
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:224] — golden fingerprint test to regenerate.
- [Source: _bmad-output/implementation-artifacts/5-1-cli-generate-and-watch-modes-with-smart-defaults.md] — confirms `generate`/`watch`/auto-discovery predate Epic 5 (not gated).
- [Source: _bmad-output/implementation-artifacts/5-2-directory-scoped-settings-with-interactive-and-cli-parity.md] — confirms `.specscribe`/CLI-precedence already exist (not gated).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md:61-71] — Voice & Tone for the new copy.

## Questions for the Owner (raised at story close, not blocking)

1. **Section order.** I placed "Generate with SpecScribe" between Reading order and Glossary (both are onboarding flows; glossary/commands are reference material). If you'd rather it come last (purely additive, don't disturb the existing "start here" flow), that's a one-line move — flag at review.
2. **Depth of the settings mention.** AC2 asks the page to "name" the effective-settings surface, which I've scoped to naming the Diagnostics field labels + a link, not restating precedence rules or `.specscribe` mechanics in full. If you want a fuller "how settings work" walkthrough here (vs. keeping this page a pointer to Diagnostics), say so — I kept it light to avoid two competing sources of truth for settings behavior.

## Dev Agent Record

### Agent Model Used

claude-opus-5 (dev-story workflow)

### Debug Log References

- `dotnet test --filter "FullyQualifiedName~SiteGeneratorHowToReadTests|FullyQualifiedName~SiteNavTests"` → 58 passed / 0 failed.
- `dotnet test --filter "FullyQualifiedName~GoldenContentFingerprint"` × 2 → same hash both runs (see Completion Notes).
- `dotnet run --project src/SpecScribe -- generate` → 375 pages, `generated=375 updated=0 skipped=4 errors=0`, exit 0.
- Live DOM check on the generated `how-to-read.html` (browser pane, `javascript_tool`): four `h2`s in order `reading-order → generate → glossary → commands`; `diagnostics.html` link present in the panel; no horizontal overflow at either document or `.howtoread-panel` level; no console errors.
- `dotnet test` (full suite) → 2347 passed / 0 failed / 3 skipped.

### Completion Notes List

**Scope held.** Content/rendering only. `SiteSettings.cs`, `Commands.cs`, `ForgeOptions.cs`, `SettingsStore.cs` untouched — the CLI surface was read, not changed. No new files, no new CSS classes, no JS, no `<details>`, no Toc.

**The unconditional section forced a restructure of the honesty gate.** `RenderPage` previously built ONE `sections` builder and used `sections.Length > 0` to pick between a "here's the reading order and glossary" subtitle/intro and a degraded "content appears as the project grows" pair. Appending an always-renders section into that same builder would have made the gate permanently true and silently killed the degraded branch — the page would then promise a reading order and glossary to an undetected repo that has neither. Fixed by splitting into `readingOrder` + `reference` builders whose combined length is the (unchanged) module-content signal, with `AppendGenerateSection` composed between them and **outside** it. Both branches now render the Generate section; both tell the truth about what else is there. `HowToRead_GenerateSection_IsFrameworkAgnostic_AndRendersWithoutADetectedModule` pins the degraded branch specifically (asserts the Generate `h2` present, reading-order/glossary `h2`s absent, and `"Start with the reading order"` NOT in the page).

**AC#2 labels verified against live output, not the story's snapshot.** The six named labels — Source root, ADR location, Output directory, README included, Deep-git analytics, External source base — were confirmed as literal `<dt>` values in the freshly generated `diagnostics.html`. Note the live config `<dl>` has since grown two more rows the story's snapshot didn't list (`Detected framework`, and Story 5.5's `Date-page "today" policy`, which sits between Deep-git analytics and README included); AC#2 asks for the same *surface*, not an exhaustive mirror, so the six were kept and the new test asserts each one is BOTH in this page's prose AND a real `<dt>` on `diagnostics.html` — so a future rename of either side breaks the test rather than drifting silently.

**One deviation from the task text, deliberate: `.specscribe` is not described as "gitignored".** Task 2 said "persisted to `.specscribe`, gitignored/personal — never committed". SpecScribe does not write a `.gitignore` entry — this repo's own `.gitignore:488` has one because a human added it. Stating "gitignored" as fact in shared chrome would be false for any project that hasn't done that. Copy says instead: "one per checkout, so ignore it in version control if you'd rather not share your local paths" — actionable and true everywhere.

**Two additions beyond the task list, both verified live in `SiteSettings.cs`:** `--show-config` (Story 5.2) is named as the way to see resolved provenance without generating, and the section states the CLI-wins precedence in one clause. Both are AC1's "aligned to 5.1–5.3" and neither reproduces the flag table.

**Flag-table drift guard.** `HowToRead_GenerateSection_NamesPathOverrides_AndDefersToHelpForTheRest` asserts the section does NOT name `--project-name`, `--no-readme`, `--spa`, `--code-url`, or `--today-policy`, so a future well-meant addition re-creates the second-source-of-truth this story's Dev Notes forbids and fails instead.

**Section-scoped assertions.** The NFR8 "no BMad token" checks run against a `GenerateSectionOf` slice (`h2#generate` → next `h2`), not the whole page — on a detected repo the nav bar and glossary both legitimately say "BMad Method", so a whole-page `DoesNotContain` would be unsatisfiable, and on the bare fixture it would pass for the wrong reason.

**Golden fingerprint regenerated: `f9c79d98…` → `9232e3f5e0d2aee38c1cd4e76829b0633aadc8331462a8d55b531c2cdb820f68`.** Confirmed identical across two repeated runs before locking in (stale-build first-hash trap). Two rendered deltas: (1) `how-to-read.html`'s new section + reworded meta description / subtitle / intro; (2) the `SiteNav` quick-link blurb, which renders as that entry's `data-tooltip` in the white key-views band, so every page carrying that band shifted by one attribute value. No asset bytes, no file-set change. PROVENANCE (shared main): captured on a tree carrying only this story's five files plus another session's untracked `spike/plotly/` scratch directory, which lives outside the temp fixture and cannot reach the hash.

**Verification limit recorded honestly:** the browser pane would not composite frames in this session, so no screenshot was captured. Verification was done on the live generated page via DOM/computed-geometry inspection (`javascript_tool`) and rendered-text extraction rather than by eye — sufficient for a prose section reusing the panel's existing `h2`/`p` styling, but the owner-verification pass should still look at it.

**Open Questions #1 and #2 (from story close) both answered as authored:** section placed between Reading order and Glossary; settings kept to naming the Diagnostics labels + one link + the precedence clause, not a full settings walkthrough. Both remain one-line changes if the owner prefers otherwise.

### File List

- `src/SpecScribe/HowToReadTemplater.cs` — modified (new `AppendGenerateSection`; `RenderPage` split into `readingOrder`/`reference` builders + `hasModuleContent` gate; meta description, both subtitles, both intro paragraphs; class doc comment)
- `src/SpecScribe/SiteNav.cs` — modified (one string literal: the How-to-use quick-link blurb, `:188`)
- `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` — modified (5 new `[Fact]`s + `GenerateSectionOf` helper + class doc comment)
- `tests/SpecScribe.Tests/SiteNavTests.cs` — modified (1 new `[Fact]` pinning the quick-link blurb)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — modified (golden content fingerprint constant + provenance comment)

## Change Log

| Date | Change |
| --- | --- |
| 2026-07-24 | Story implemented. Added the unconditional "Generate with SpecScribe" section to `how-to-read.html` (generate/watch, no-flags default, `--source`/`--adrs`/`--output` + `--help`, Diagnostics field labels + link, `.specscribe` persistence and CLI-wins precedence, `--show-config`); reworded the page's meta description, subtitle, and intro; fixed the stale Help quick-link blurb; 6 new tests; golden content fingerprint regenerated to `9232e3f5…`. Full suite 2347 passed / 0 failed / 3 skipped. Status → review. |
