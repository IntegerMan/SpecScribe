---
title: 'Gherkin AC styling, inline Story/Epic links, and placeholder story pages'
type: 'feature'
created: '2026-07-06'
status: 'done'
review_loop_iteration: 0
baseline_commit: '9fbca5e3aaa8593e0b9e188919acdbe1c130b5ad'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story-page Acceptance Criteria panels render Given/When/Then as an undifferentiated wall of prose; inline mentions like "Story 1.5" or "Epic 2" in rendered bodies are inert text; and stories defined in epics.md without an implementation artifact have no page of their own, so nothing can link to them.

**Approach:** (1) Post-process AC HTML so each bold Gherkin keyword (Given/When/Then/And) starts its own visual line with a styled keyword marker, on both story-page AC panels and epic-page AC blocks. (2) Add an anchor-aware whole-page linkifier (mirroring `RequirementLinkifier`) that turns "Story N.M" / "Epic N" mentions into links to their generated pages. (3) Emit a placeholder page at `epics/story-N-M.html` for every story that has no implementation artifact, so every story mention has a real link target.

## Boundaries & Constraints

**Always:**
- Only bold keywords (`<strong>Given|When|Then|And</strong>`) are treated as Gherkin markers, and only inside AC block/criterion HTML — never in general prose.
- Linkify only Story/Epic ids that exist in the parsed `EpicsModel`; unknown ids stay plain text. Never rewrite text already inside an `<a>` span (reuse the `AnchorSplit` pattern). A page never links to itself (story page skips its own id; epic page skips its own number).
- Placeholder pages must NOT change progress semantics: `StoryInfo.ArtifactOutputPath` stays null for undrafted stories so `StoriesWithArtifact`, sunbursts, badges, and story-card behavior are untouched.
- Placeholder uses the same output path a real story page would (`epics/story-{N}-{M}.html`) so a later-drafted artifact overwrites it in place and links never break.
- Gherkin keyword colors use existing palette CSS vars (--gold family), not the --status-* tokens — keywords are not lifecycle stages. Icons, if any, are pure CSS `::before` glyphs; no new JS.
- Placeholder page badge routes through `StatusStyles` ("drafted" class, "Not yet drafted" label style).

**Ask First:**
- Any change to what the epic-page story cards or sunbursts link to.
- Linkifying looser mention forms (e.g. bare "1.5", "Stories 1.4–1.6").

**Never:**
- No JS interaction engine (project rule: charts/enhancements are pure HTML/CSS/SVG).
- Don't restructure `EpicsParser` section parsing or the artifact-resolution pipeline.
- Don't linkify mentions inside `<code>`/`<pre>` command snippets (BMad command guidance like `create-story 2.6` must stay copyable plain text).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Gherkin split | Criterion HTML `<strong>Given</strong> a <strong>When</strong> b <strong>Then</strong> c <strong>And</strong> d` | Each keyword starts a new line wrapped in `<span class="gherkin-kw kw-given">` etc.; first segment gets no leading break | N/A |
| Non-keyword bold | `<strong>Origin &amp; scope:</strong>` inside a criterion | Left untouched | N/A |
| Known story mention | "Sequence this after Story 1.5" in any rendered body | `Story 1.5` becomes a link to `epics/story-1-5.html` (placeholder or real) | N/A |
| Unknown mention | "Story 9.9" / "Epic 42" not in model | Plain text, no broken link | N/A |
| Mention already linked | "Story 1.4" inside an existing `<a>` (TOC, breadcrumb) | Untouched | N/A |
| Self mention | "Story 2.1" on story-2-1.html; "Epic 2" kicker on epic-2.html | Untouched (skip-self) | N/A |
| Undrafted story | Story in epics.md, no artifact file | Placeholder page emitted: breadcrumb, kicker + drafted badge, user story, Gherkin-styled ACs, create-story guidance, link back to epic | N/A |
| Artifact appears later | Watch mode: artifact file added for story | `RegenerateEpics` writes real page over the placeholder at the same path | N/A |
| Multi-digit ids | "Story 1.10", "Epic 12" | Matched and linked correctly (no 1.1 prefix collision) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/EpicsParser.cs` -- `ExtractAcceptanceCriteria` (story-page AC HTML via `RenderInline`), `RenderAcLine` (epic-card AC lines with `.kw` span) — both feed the Gherkin styler.
- `src/SpecScribe/RequirementLinkifier.cs` -- the pattern to mirror: anchor-aware split + token replace.
- `src/SpecScribe/SiteGenerator.cs` -- `ApplyRequirementLinks` whole-page post-process hook; `GenerateEpicsInternal` story loop (skips `ArtifactOutputPath is null` at :347) where placeholder emission slots in.
- `src/SpecScribe/EpicsTemplater.cs` -- `RenderStory` (real page to pattern the placeholder after), `AppendStoryCard`, `StoryAnchorId`; `BmadCommands.InlineGuidance` usage at :377.
- `src/SpecScribe/StatusStyles.cs` -- status→class source; placeholder badge uses "drafted".
- `src/SpecScribe/assets/specscribe.css` -- `.ac-list .kw`, `.ac-criterion-body` rules to extend with `.gherkin-kw` styling.
- `tests/SpecScribe.Tests/LinkifierTests.cs`, `EpicsParserTests.cs`, `SiteGeneratorTraceabilityTests.cs` -- existing test homes.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/GherkinStyler.cs` -- new static class: rewrite `<strong>(Given|When|Then|And)</strong>` in AC HTML into `<span class="gherkin-kw kw-{keyword}">{Keyword}</span>`, inserting a line-break structure before every keyword except the first -- single shared implementation for both AC surfaces.
- [x] `src/SpecScribe/EpicsParser.cs` -- route `ExtractAcceptanceCriteria` HTML and `RenderAcLine` output through `GherkinStyler` (replace the bespoke `.kw` span in `RenderAcLine`) -- consistent treatment on story pages and epic cards. (AC tooltip PlainText is taken from the pre-styling render so stripped text keeps its inter-clause spaces.)
- [x] `src/SpecScribe/StoryEpicLinkifier.cs` -- new anchor-aware post-processor: `\bStory (\d+)\.(\d+)\b` → `epics/story-N-M.html`, `\bEpic (\d+)\b` → `epics/epic-N.html`, only for ids in the model; also skip `<code>`/`<pre>` spans; optional skipStoryId/skipEpicNumber -- mirrors `RequirementLinkifier`. (Also protects whole `<svg>` spans: chart `<title>`/aria text says "Epic N"/"Story N.M" and Mermaid `<pre>` sources carry epic node labels.)
- [x] `src/SpecScribe/SiteGenerator.cs` -- extend the whole-page post-process (renamed `ApplyReferenceLinks`) to also run `StoryEpicLinkifier` when `_epicsModel` exists, passing skip-self for story/epic pages; in `GenerateEpicsInternal`, emit a placeholder page for every story with no artifact -- one hook, every page linkified.
- [x] `src/SpecScribe/EpicsTemplater.cs` -- new `RenderStoryPlaceholder(epic, story, nav, commands)`: head/nav/breadcrumb like `RenderStory`, kicker row with "drafted" `status-badge` ("Not yet drafted"), `UserStoryHtml` lead, Gherkin-styled `AcBlocksHtml`, `InlineGuidance(commands.Command("create-story", story.Id), ...)` panel, back-link to the epic page -- the link target for undrafted stories.
- [x] `src/SpecScribe/assets/specscribe.css` -- `.gherkin-kw` chip style + per-keyword accent classes (`kw-given` teal, `kw-when` gold, `kw-then` rust, `kw-and` muted outline), `.gherkin-line` block presentation, `.story-ref`/`.epic-ref` links mirroring `.req-ref` -- polish without JS.
- [x] `tests/SpecScribe.Tests/GherkinStylerTests.cs` -- unit-test the I/O matrix Gherkin rows (split, non-keyword bold untouched).
- [x] `tests/SpecScribe.Tests/LinkifierTests.cs` -- add StoryEpicLinkifier cases: known/unknown ids, inside-anchor skip, inside-code skip, self-skip, multi-digit.
- [x] `tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs` -- end-to-end (new sibling of the traceability tests, so its fixture doesn't disturb theirs): undrafted story yields placeholder file; "Story N.M"/"Epic N" mentions link; self/unknown/Mermaid/SVG protected; placeholder overwritten by RegenerateEpics once an artifact appears.

**Acceptance Criteria:**
- Given a story artifact whose AC contains bold Given/When/Then/And, when its story page renders, then each keyword begins a new visual line with a distinct styled marker and surrounding prose is unchanged.
- Given epics.md defines a story with no implementation artifact, when the site generates, then `epics/story-N-M.html` exists with the story narrative, styled ACs, a drafted badge, and create-story guidance, and progress counts are identical to before.
- Given any generated page mentions "Story N.M" or "Epic N" that exists in the plan, when the site generates, then the mention is a working relative link (correct `../` depth), while unknown ids, code snippets, existing links, and self-references stay plain.

## Design Notes

Gherkin output shape (keeps `.ac-criterion-body` a single flow container, no nested divs needed):

```html
<span class="gherkin-line"><span class="gherkin-kw kw-given">Given</span> the project contains …</span>
<span class="gherkin-line"><span class="gherkin-kw kw-when">When</span> the site is generated …</span>
```

with `.gherkin-line { display: block; }` — block spans give per-keyword lines without touching the epic-card `<br>`-joined path, which can keep its existing structure and just swap the keyword span.

Linkifier ordering: run StoryEpicLinkifier before RequirementLinkifier or after — either works since neither touches the other's tokens and both are anchor-aware; keep both inside the same post-process helper so no call site is missed.

## Verification

**Commands:**
- `dotnet build SpecScribe.sln` -- expected: clean build, no warnings introduced.
- `dotnet test` -- expected: all tests pass including the three new/extended test files.

**Manual checks (if no CLI):**
- Regenerate the site and open a story page with multi-clause ACs (e.g. story 2.1): keywords on their own lines with markers; "Story 1.5" mention in its AC links to the 1.5 page; open an undrafted story's placeholder (e.g. 2.6) and confirm badge, ACs, and create-story guidance.

## Suggested Review Order

**Inline Story/Epic linkifier (highest-risk: whole-page rewrite)**

- Entry point — the anchor-aware post-processor; read the `ProtectedSplit` comment first (a/code/pre/svg/head/script/style are the never-rewrite regions).
  [`StoryEpicLinkifier.cs:25`](../../src/SpecScribe/StoryEpicLinkifier.cs#L25)

- Per-mention replacement — leading-zero reject + `int.TryParse` guard so odd tokens never mislink or crash the epics pass.
  [`StoryEpicLinkifier.cs:67`](../../src/SpecScribe/StoryEpicLinkifier.cs#L67)

- The one hook every page flows through; story/epic pages pass skip-self ids.
  [`SiteGenerator.cs:511`](../../src/SpecScribe/SiteGenerator.cs#L511)

**Placeholder pages for undrafted stories**

- Emission point — placeholder written at the real page's path; `ArtifactOutputPath` stays null so progress is untouched.
  [`SiteGenerator.cs:354`](../../src/SpecScribe/SiteGenerator.cs#L354)

- The epics dir is rebuilt each pass so a vanished story can't leave a stale placeholder (watch mode).
  [`SiteGenerator.cs:336`](../../src/SpecScribe/SiteGenerator.cs#L336)

- The placeholder template — badge, narrative, styled ACs, create-story guidance, back-link.
  [`EpicsTemplater.cs:328`](../../src/SpecScribe/EpicsTemplater.cs#L328)

**Gherkin AC styling**

- Keyword styler — degrades to in-place marker styling when a keyword is nested, never emitting overlapping tags.
  [`GherkinStyler.cs:27`](../../src/SpecScribe/GherkinStyler.cs#L27)

- Both AC surfaces route through it: story-page criteria and epic-card lines.
  [`EpicsParser.cs:228`](../../src/SpecScribe/EpicsParser.cs#L228)

- Chip styling; note the `kw-when` amber chosen for WCAG-AA contrast on the cream background.
  [`specscribe.css:770`](../../src/SpecScribe/assets/specscribe.css#L770)

**Tests (supporting)**

- Linkifier unit cases: protected spans, overflow, leading-zero, three-part ids, wrapped mentions.
  [`LinkifierTests.cs`](../../tests/SpecScribe.Tests/LinkifierTests.cs)

- End-to-end: placeholder emission/pruning, head-injection safety, mention links, Gherkin styling.
  [`SiteGeneratorStoryEpicPagesTests.cs`](../../tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs)

- Gherkin styler unit cases: split, non-keyword bold, But keyword, nested-tag degrade.
  [`GherkinStylerTests.cs`](../../tests/SpecScribe.Tests/GherkinStylerTests.cs)
