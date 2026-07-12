# Story 9.5: Distinct Acceptance-Criteria Blocks and Collapsed Dev Notes

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a reviewer,
I want acceptance criteria visually distinct from surrounding prose and dev notes collapsed by default on long pages,
so that I can diff the contract against the claim quickly.

## Acceptance Criteria

1.
**Given** a story page with acceptance criteria
**When** the page renders
**Then** ACs render as bordered/tinted blocks using existing design tokens, clearly distinct from body prose
**And** the treatment audits and extends spec-ac-panel-and-story-card-polish rather than duplicating it.

2.
**Given** a long story page with dev-notes/dev-record sections
**When** the page renders
**Then** those sections collapse by default and expand on demand
**And** the "On this page" TOC invariant is preserved.

## Context & Scope

Epic 9 completes the requirement → epic → story chain so a reviewer can judge a "done" claim in one glance. This story is the **reviewer-ergonomics** story: it makes the *contract* (the acceptance criteria) pop and pushes the *working detail* (dev notes) out of the way, so a reviewer scanning a story page can line the AC contract up against the completion claim without scrolling past screens of implementation prose.

**Both ACs target ONE surface: the drafted story detail page** — `HtmlRenderAdapter.RenderStoryBody` (src/SpecScribe/HtmlRenderAdapter.Epics.cs:273-357). This is the page rendered for every *drafted* story (a story whose `.md` artifact exists), reached from an epic page's story card. It is **not** the epic page, not the not-yet-drafted placeholder page, and not a generic doc page. `RenderStoryBody` is the single body producer — `EpicsTemplater.RenderStory` (src/SpecScribe/EpicsTemplater.cs:153) calls `HtmlRenderAdapter.Shared.RenderStoryBody(view)` once and the resulting body string is what the HTML site, the VS Code webview, and the SPA all render (the webview adapter only swaps chrome — nav/breadcrumb — around the same body; src/SpecScribe/WebviewRenderAdapter.cs:65-66). **So any change made here propagates to all three delivery surfaces identically, and the RenderParity contract requires it stay byte-identical across them.**

### What already exists (read before touching)

- **The AC panel already renders per-criterion blocks.** `RenderStoryBody` emits a `<div class="chart-panel ac-panel" id="sec-acceptance-criteria">` containing one `<div class="ac-criterion" id="ac-N">` per criterion, each with an `<a class="ac-anchor">AC #N</a>` deep-link target and an `<div class="ac-criterion-body">` (src/SpecScribe/HtmlRenderAdapter.Epics.cs:307-319). **AC #1 is a CSS refinement of these existing blocks, not new markup.** Today `.ac-criterion` has *no resting border or tint* — only the `:target` state (when jumped to via a `(AC: #N)` link) gets a tan background + inset gold bar (src/SpecScribe/assets/specscribe.css:1065-1094, `:target` at :1076). So a criterion at rest reads as plain indented prose — exactly the "not distinct from body prose" defect this AC fixes.
- **Dev Agent Record already collapses.** The "Dev Agent Record" section is peeled out of the artifact (`EpicsParser.ExtractDevAgentRecord`) and rendered by `RenderStoryBody` as `<details class="chart-panel dev-agent-details" id="sec-dev-agent-record"><summary>Dev Agent Record</summary>…</details>` — a native, JS-free, **collapsed-by-default** disclosure (src/SpecScribe/HtmlRenderAdapter.Epics.cs:321-330; CSS caret grammar at specscribe.css:1309-1321). **This is the proven pattern AC #2 extends — do not invent a new disclosure mechanism.** The "dev-record" half of AC #2 is therefore *already satisfied*; the new work is the **"dev-notes" half**.
- **Dev Notes lives in the remainder blob, uncollapsed.** Everything not peeled into a structured panel — "## Context & Scope", "## Tasks / Subtasks", "## Dev Notes" (and its `### Project Structure Notes` / `### References` / testing subsections), etc. — is rendered as one flat markdown block by `EpicsParser.SplitStoryArtifact` (src/SpecScribe/EpicsParser.cs:86-139) into `view.RemainderHtml`, and dropped into the page inside `<article class="doc-body epic-card">…RemainderHtml…</article>` (src/SpecScribe/HtmlRenderAdapter.Epics.cs:340-343). This flat article is where "Dev Notes" currently renders fully expanded.
- **The "On this page" TOC is assembled in rendered order.** `RenderStoryBody` builds an ordered `List<Toc.Entry>`: explicit entries for the structured panels (User Story, Task Breakdown, Acceptance Criteria, Dev Agent Record, …) plus `toc.AddRange(Toc.ExtractHeadings(view.RemainderHtml))` for the remainder's own H2/H3 headings (src/SpecScribe/HtmlRenderAdapter.Epics.cs:343), then wraps everything with `Toc.WrapWithSidebar`. `Toc.ExtractHeadings` finds `<h2|h3 … id="…">text</h2>` **anywhere in the fragment** (regex at src/SpecScribe/Toc.cs:65-85) and **skips any heading without an id**. `Toc.RenderSidebar` dedupes by anchor id (src/SpecScribe/Toc.cs:26-34). This behavior is the lever AC #2's TOC-invariant clause hinges on (see Task 2).

### Owner-selected design directions (locked at create-story)

Two decisions were made with the owner up front (project rule: elicit design intent for any new visual surface — Epic 3 retro action, memory `create-story-elicit-visual-intent`):

1. **Collapse scope = "Dev Notes + References only."** On the story page, the sections that collapse by default are **Dev Notes** and **References**. **Keep "Context & Scope" and "Tasks / Subtasks" expanded** — Context frames the AC contract and Tasks are the completion evidence a reviewer wants at a glance; hiding them would hide "the claim." (Dev Agent Record already collapses — leave it as-is.) NOTE: in the standard BMad story shape, `### References` is authored as an **H3 under `## Dev Notes`**, so collapsing the `## Dev Notes` H2 already subsumes References in most stories; the match set still includes a top-level `## References` H2 defensively, for stories that author it that way. See Task 2 for the precise match rule.
2. **AC-block treatment = "per-criterion card + gold left accent."** Each `.ac-criterion` gets a **subtle resting border, a faint parchment tint, and a thin gold left-accent bar** — echoing the existing `:target` inset-gold cue and the `.review-findings`/`.change-log` left-accent grammar (specscribe.css:1356-1357). This reuses existing tokens (`--border`, the parchment family, `--gold`); it adds **no new `--status-*` token** and **no new color**. The `:target` jump-highlight must stay visibly *stronger* than the new resting treatment so "which criterion did the `(AC: #N)` link land on" is still unmistakable.

### Non-negotiable project principle: framework-agnostic degrade (NFR8)

A foreign-framework story artifact may have no "Dev Notes" section at all, or name its sections differently. The collapse must **degrade to absent, not empty-but-present**: when no heading matches the collapse set, the remainder renders exactly as today (fully expanded, no stray empty `<details>`). Never author a house convention or require a section to exist. [Memory: framework-agnostic is a load-bearing project value; NFR8.]

## Tasks / Subtasks

- [ ] **Task 1 — Per-criterion resting card + gold left accent for AC blocks (AC: #1)**
  - [ ] Audit spec-ac-panel-and-story-card-polish (_bmad-output/implementation-artifacts/spec-ac-panel-and-story-card-polish.md) and the two AC surfaces it governs before writing any CSS: the story-page `.ac-criterion` (this story's target) and the epic-card / placeholder `.ac-block` (src/SpecScribe/HtmlRenderAdapter.Epics.cs:386-394, CSS at specscribe.css:1014+). The spec's "**Always**: Both AC surfaces share the same visual grammar" constraint is binding — **extend the shared grammar, do not fork a story-page-only look that clashes with the epic-card block.**
  - [ ] In src/SpecScribe/assets/specscribe.css, give the resting `.ac-criterion` (currently :1065-1073, no resting frame): a `1px solid var(--border)` outline, `border-radius` consistent with the existing 4px, a faint parchment tint background (use the existing parchment token family — e.g. `--parchment`/`--parchment-dark`; pick the one that reads as "card, but calmer than a chart-panel"), and a **thin gold left-accent bar** (`border-left: 3px solid var(--gold)` or an inset box-shadow in the `:target` idiom already at :1076). Keep `align-items: baseline`, the `.ac-anchor` gold-mono chip, and the `.ac-criterion-body` typography unchanged. Preserve `scroll-margin-top` (:1072) so `#ac-N` jumps still clear the sticky nav.
  - [ ] Make the **`:target` state visibly stronger** than the new resting state (owner decision #2): the jumped-to criterion should still read as "this is the one" — e.g. a deeper tint and/or a bolder gold bar than the resting accent. Adjust specscribe.css:1076 as needed so resting and target don't look identical.
  - [ ] Decide, and record in the CSS comment, whether the epic-card `.ac-block` gets the matching resting accent for visual coherence, or stays as-is (denser card context). Owner's AC scopes the requirement to the *story page* (`.ac-criterion`); coherence with `.ac-block` is a judgment call — keep them from clashing, but do not over-scope. Whatever you choose, note it so review can check it against the spec's shared-grammar rule.
  - [ ] **Webview theming companion (only if needed).** The parchment/gold tokens are host-neutral and already bridged for the webview; but the existing `.ac-criterion:target` has an explicit webview-theme override (src/SpecScribe/assets/specscribe-webview-theme.css:223-224) because the site tan doesn't read on a VS Code dark background. Generate the webview surface (or inspect the theme bridge) and, **if the new resting tint/border is invisible or wrong under `.vscode-dark`/`.vscode-high-contrast`**, add a companion resting-state rule next to the existing `:target` override — same pattern, mapping to `--vscode-*` vars. Do **not** touch the base HTML for this; the webview theme is a second stylesheet, and the HTML must stay byte-identical (memory `story-6-5-webview-theming-live`).
  - [ ] This is CSS-only — no markup change, no view-model change. `StylesheetTests` (tests/SpecScribe.Tests/StylesheetTests.cs) guards the stylesheet; extend it (Task 5).

- [ ] **Task 2 — Collapse the Dev Notes (+ References) remainder sections by default (AC: #2)**
  - [ ] Add a small **pure, deterministic** post-processor that wraps the target H2 sections of an already-rendered remainder-HTML string in a native `<details>` disclosure. Recommended home: a new tiny static helper `src/SpecScribe/CollapsibleSections.cs` (e.g. `static string WrapSections(string remainderHtml, ISet<string> headingSlugs)`), OR a static method on `EpicsParser` beside `SplitStoryArtifact`. Keep it a **pure string→string** function (no I/O, no ordering nondeterminism) — NFR8 requires byte-identical regeneration.
  - [ ] **Match rule:** scan for `<h2 … id="…">…</h2>` whose Markdig auto-id slug is in `{ "dev-notes", "references" }` (Markdig lowercases + hyphenates heading text; "Dev Notes" → `dev-notes`, "References" → `references`). Match on the **id slug**, not raw text, so inline markup in a heading can't break the match. For each match, the section body runs from that `<h2>` up to the next `<h2 …>` (or end of fragment). Wrap the whole section:
    ```
    <details class="collapsible-section" id="{slug}-section">
      <summary><h2 id="{slug}">…heading…</h2></summary>
      …section body…
    </details>
    ```
    — **the original `<h2 id="{slug}">` stays inside `<summary>`** (see the TOC clause below).
  - [ ] **Collapsed by default, unconditionally.** Emit `<details>` (not `<details open>`) — collapsed. Do **not** implement a page-length heuristic ("only on long pages"): a heuristic introduces nondeterminism and a second code path for no real benefit; the Dev Agent Record already collapses unconditionally, so this matches the established pattern and keeps output reproducible. (The AC's "on long pages" is the *motivation*, not a rendering conditional — the short-page cost is a single extra click, which is fine.)
  - [ ] **Preserve the "On this page" TOC invariant (AC: #2, second clause) — this is the #1 review checkpoint.** The rule that keeps every TOC link live: *a TOC entry must never point into hidden content.*
    - The collapsed section's own `<h2 id="{slug}">` lives inside the always-visible `<summary>`, so `Toc.ExtractHeadings` still finds it and its "On this page" link still lands on a visible target. ✅ Keep it.
    - **The buried subheadings (`### Project Structure Notes`, `### References`, testing subsections inside Dev Notes) must NOT remain live TOC links into the now-hidden body.** The clean, self-contained fix: **strip the `id` attribute from every `<h3>` (and deeper) that the wrapper buries inside a collapsed `<details>`.** `Toc.ExtractHeadings` already skips id-less headings (Toc.cs:81-82 requires a matched `id`), so those subheadings simply drop out of the sidebar — no change to the shared `Toc` code needed. Net result: "On this page" lists **Dev Notes** (one live link to the visible summary) and omits its collapsed subsections; expanded sections (Context & Scope, Tasks / Subtasks) keep their full H2+H3 TOC entries unchanged. Document that id-stripping means those subsections lose their deep-anchor even when expanded — this is acceptable (nothing deep-links to `#reuse-map`; the `(AC: #N)` linkifier targets the AC panel, and Source citations target external pages — neither targets remainder H3 ids). Confirm this assumption by grepping for any `href="#project-structure-notes"`-style internal refs before relying on it.
  - [ ] **Apply the wrapper in the shared fragment pipeline** so all three adapters inherit identical output: in `SiteGenerator.BuildStoryPageFragments` (src/SpecScribe/SiteGenerator.cs:726-758), wrap `remainderHtml` **after** the existing linkification passes (`SourceLinkifier.Linkify` at :741 and `LinkifyAcReferences` at :753) — wrap **last**, just before constructing `StoryPageFragments`, so every prior transform operates on flat HTML and the `<details>`/`<summary>` insertion + id-strip is the final mutation. Because `view.RemainderHtml` already carries the `<details>`, `RenderStoryBody`'s existing `Toc.ExtractHeadings(view.RemainderHtml)` call (HtmlRenderAdapter.Epics.cs:343) yields the correct entries automatically — **no `RenderStoryBody` signature or view-model change is required.**
  - [ ] **CSS:** add a `.collapsible-section` rule to src/SpecScribe/assets/specscribe.css reusing the `.dev-agent-details` caret grammar (specscribe.css:1309-1321): hide the native marker, prepend a `▸` that flips to `▾` on `[open]`, cursor:pointer summary. The `<summary>` wraps an `<h2>` — reset the h2's default margins inside the summary and let the summary carry the heading's visual weight so a collapsed Dev Notes reads like the H2 it replaces (not a shrunken uppercase label like the dev-agent summary — this one holds a real section heading). Make sure the `<summary>` layout doesn't leave the `▸`/`▾` caret and the h2 fighting for the same line (flex the summary, or inline the h2).

- [ ] **Task 3 — Webview + SPA parity and golden regeneration (AC: #1, #2)**
  - [ ] The AC-block CSS change is stylesheet-only (byte-identical HTML) — but the collapse wrapper changes the story-page **HTML bytes** on every drafted story. Regenerate and update whatever golden/parity expectations the suite pins: run `dotnet test` and update the assertions in `RenderParityTests`, `RenderSectionParityTests`, `RenderSpaParityTests`, and any story-page fixture assertions (Task 5). The `<details>` sits in the **shared body**, not chrome, so HTML == webview == SPA parity holds and **no new `HostRenderException` / no new registry chrome-exception is needed** (contrast the 3 existing webview chrome exceptions — this is not one). Confirm parity stays green rather than adding an exception.
  - [ ] Verify the SPA adapter's content-region capture (`<main id="main-content">`, memory `story-6-7-spa-adapter-live`) still round-trips the new `<details>` correctly (it consolidates the same body; a native `<details>` is inert HTML, so this should just work — confirm, don't assume).

- [ ] **Task 4 — Verify no regression in AC deep-linking and TOC dedup (AC: #1, #2)**
  - [ ] `(AC: #N)` references in the (still-expanded) Tasks / Subtasks section must still deep-link to `#ac-N` and trigger the `:target` highlight (now visually distinct from resting). Confirm `LinkifyAcReferences` (unchanged) + the stronger `:target` rule interact correctly.
  - [ ] Confirm `Toc.RenderSidebar`'s dedupe-by-anchor (Toc.cs:26-34) still holds with the summary-hosted `<h2 id="dev-notes">` — no duplicate `#dev-notes` entry, no dead link.

- [ ] **Task 5 — Tests (AC: #1, #2)**
  - [ ] `tests/SpecScribe.Tests/StylesheetTests.cs` — assert the new resting `.ac-criterion` frame (border + parchment tint + gold left accent) is present and that `.ac-criterion:target` remains a distinct, stronger rule; assert the `.collapsible-section` caret/summary rule exists.
  - [ ] `tests/SpecScribe.Tests/SiteGeneratorStoryEpicPagesTests.cs` — end-to-end on a rendered drafted story page: (a) the AC panel's criteria carry the resting `.ac-criterion` class and the page still contains `id="ac-1"` anchors; (b) the **Dev Notes** section is wrapped in `<details class="collapsible-section"` and is **collapsed** (`<details` without `open`); (c) **References** (whether its own H2 or an H3 under Dev Notes) ends up inside a collapsed `<details>`; (d) **Context & Scope and Tasks / Subtasks are NOT wrapped** in `<details>` and render expanded; (e) the "On this page" sidebar still contains a link to `#dev-notes` (the summary target) and **no longer** contains links to the buried subsection ids; (f) a fixture story with **no Dev Notes section** produces **no stray `<details class="collapsible-section">`** (NFR8 degrade). Reuse the existing story fixtures in this file / `SiteGeneratorWebviewTests` rather than authoring a new artifact shape.
  - [ ] `tests/SpecScribe.Tests/TocTests.cs` — a unit case proving `Toc.ExtractHeadings` returns the summary-hosted H2 and omits id-stripped subheadings (or, if you extract the wrapper into a testable helper, unit-test the helper directly: correct section boundaries, id retained on the H2, ids stripped on buried H3s, no-match passthrough).
  - [ ] `RenderParityTests` / `RenderSectionParityTests` / `RenderSpaParityTests` — updated so HTML/webview/SPA remain byte-identical on the story body with the new `<details>`.
  - [ ] Run the full suite from repo root (`dotnet test`). Watch `SiteGeneratorStoryEpicPagesTests`, `SiteGeneratorWebviewTests`, `WebviewThemingTests`, `StylesheetTests`, `TocTests`, and the three parity suites for regressions from the byte change.

## Dev Notes

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Story-page body producer (single seam → HTML+webview+SPA) | `HtmlRenderAdapter.RenderStoryBody` | src/SpecScribe/HtmlRenderAdapter.Epics.cs:273-357 |
| Existing per-criterion AC block markup (style, don't rebuild) | `.ac-criterion` / `.ac-anchor` / `.ac-criterion-body` | HtmlRenderAdapter.Epics.cs:307-319; CSS specscribe.css:1065-1094 |
| Proven collapsed-by-default disclosure pattern | `.dev-agent-details` `<details>` + caret CSS | HtmlRenderAdapter.Epics.cs:321-330; specscribe.css:1309-1321 |
| Caret grammar to reuse for `.collapsible-section` | `▸`/`▾` marker-hide pattern | specscribe.css:1309-1321 (also `.requirements-inventory` :1113-1115) |
| Left-accent-bar idiom (gold/rust/teal) | `.review-findings`/`.change-log` `border-left` | specscribe.css:1356-1357 |
| Remainder split (where Dev Notes/Context/Tasks land) | `EpicsParser.SplitStoryArtifact` | src/SpecScribe/EpicsParser.cs:86-139 |
| Shared fragment pipeline (where to apply the wrapper) | `SiteGenerator.BuildStoryPageFragments` | src/SpecScribe/SiteGenerator.cs:726-758 |
| Rendered-order TOC extraction (id-gated) | `Toc.ExtractHeadings` / `Toc.RenderSidebar` | src/SpecScribe/Toc.cs:26-34,65-85 |
| Webview theme override precedent for AC tint | `.ac-criterion:target` vscode rule | src/SpecScribe/assets/specscribe-webview-theme.css:223-224 |
| AC-treatment spec to audit & extend (not duplicate) | spec-ac-panel-and-story-card-polish | _bmad-output/implementation-artifacts/spec-ac-panel-and-story-card-polish.md |

### Guardrails & invariants

- **One body seam, three surfaces.** `RenderStoryBody`'s output is rendered verbatim by the HTML site, the webview (chrome-swapped), and the SPA. `RenderParity` requires byte-identical bodies — never branch on delivery surface inside the body. Put the collapse in the **shared remainder string** (Task 2), not in a surface-specific path.
- **Pure-CSS disclosure only.** The webview CSP blocks non-nonce'd JS, so the collapse MUST be native `<details>`/`<summary>` — zero JS. This matches the existing Dev Agent Record and the Story 10.1 nav-disclosure decision (memory `story-10-1-hierarchical-nav-rearchitecture`, `charting-is-pure-svg-no-js`).
- **No new `--status-*` token, no new color.** AC-block treatment reuses `--border`, the parchment family, and `--gold` (memory `specscribe-status-token-system`: six status tokens are the single stage→color source and are not for this; the AC card is chrome, not a stage color).
- **Never color-only (UX-DR17).** The AC-block distinctness is carried by the border + the gold *bar* (shape), not tint alone, so it survives without color. The collapse state is carried by the `▸`/`▾` caret glyph + native disclosure, not color.
- **TOC invariant is load-bearing.** Every "On this page" link must land on a visible element. The id-on-summary-H2 / strip-buried-H3-ids rule (Task 2) is what guarantees this; it is the first thing review will check. Do not "fix" it by relying on browsers auto-opening `<details>` on fragment navigation — that behavior is not universal.
- **Deterministic output (NFR8).** The wrapper is a pure string transform keyed on a fixed slug set; a from-scratch regeneration must be byte-identical. No timestamps, no iteration-order dependence, no page-length heuristic.
- **Degrade to absent (NFR8).** No matching heading → remainder renders exactly as today; never emit an empty `<details>` or require a section to exist.
- **Golden bytes change on story pages.** The collapse alters story-page HTML → regenerate and update the pinned parity/fixture expectations; the AC-CSS change alone is byte-identical HTML (stylesheet only). Keep `dotnet test` green; do not add a parity exception to paper over an accidental divergence.
- **Coordinate with siblings.** 9.1 (`RenderRequirement`) and 9.4 (verification evidence strip near the status badge) touch adjacent surfaces but not `RenderStoryBody`'s AC panel / remainder. 9.4 is backlog and sequenced before this; if it has landed and added an evidence strip in the kicker-row, apply this story's changes around it, not over it. No hard dependency.

### Project Structure Notes

- Primary code: `src/SpecScribe/assets/specscribe.css` (AC-block resting card + `.collapsible-section` caret), a new small `src/SpecScribe/CollapsibleSections.cs` helper (or a method on `EpicsParser`), and `src/SpecScribe/SiteGenerator.cs` (`BuildStoryPageFragments`, one wrapper call). Possibly `src/SpecScribe/assets/specscribe-webview-theme.css` (resting-tint companion rule, only if the tint doesn't read under vscode themes). **No change expected** to `RenderStoryBody`'s signature, the `StoryPageView` model, `Toc.cs`, or any adapter contract.
- No epics.md / authoring-schema change. No new source files beyond the optional `CollapsibleSections.cs`. Tests in `tests/SpecScribe.Tests/`.
- Generate to `SpecScribeOutput/` by default when verifying — **not** `docs/live` (vestigial/gitignored). [Memory `generate-output-dir-is-specscribeoutput`]

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains`/`Assert.DoesNotContain` on generated HTML strings — the established pattern in `SiteGeneratorStoryEpicPagesTests` and `StylesheetTests`. Run `dotnet test` from repo root.
- Reuse existing story fixtures (`SiteGeneratorStoryEpicPagesTests`, `SiteGeneratorWebviewTests`' `Story11Md`/`Story21Md`) rather than authoring a new artifact — but ensure at least one fixture has a `## Dev Notes` section with `###` subsections so the collapse + TOC-strip path is exercised, and one without (NFR8 degrade path).
- Parity is a hard gate: the three `Render*ParityTests` suites must stay green with the new `<details>` in the shared body.

### Verify before marking review

Generate the portal against this repo's own `_bmad-output` (`SpecScribeOutput/`), open a drafted story page (e.g. `epic-9`'s story 9.3 detail page, or this file once rendered). Confirm: (1) each acceptance criterion reads as a bordered, faintly-tinted card with a gold left accent, clearly distinct from the prose around it, and clicking a `(AC: #N)` link in the tasks below still jumps to that criterion with a *stronger* target highlight; (2) the **Dev Notes** section is collapsed by default behind a `▸ Dev Notes` disclosure and expands on click; **Context & Scope** and **Tasks / Subtasks** remain fully expanded; **Dev Agent Record** still collapses as before; (3) the "On this page" sidebar lists "Dev Notes" as a single working link (jumps to the visible summary) and no longer lists its buried subsections, while every other TOC link still lands on a visible target. Then generate the webview surface (`specscribe webview`) or inspect the theme bridge and confirm the AC card tint reads correctly under a VS Code dark theme; confirm `dotnet test` (incl. the parity suites) is green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.5] (epics.md:1616-1634) — user story + ACs.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9] (epics.md:1530-1534) — epic intent; FR26, UX-DR26, NFR8.
- [Source: _bmad-output/implementation-artifacts/spec-ac-panel-and-story-card-polish.md] — the AC-treatment spec AC #1 must audit and extend, incl. the "both AC surfaces share one visual grammar" constraint.
- [Source: src/SpecScribe/HtmlRenderAdapter.Epics.cs:273-357] — `RenderStoryBody`, the single story-page body producer (AC panel :307-319, Dev Agent Record `<details>` :321-330, remainder article + TOC extract :340-343).
- [Source: src/SpecScribe/EpicsTemplater.cs:153] — the one caller of `RenderStoryBody`.
- [Source: src/SpecScribe/WebviewRenderAdapter.cs:65-66] — webview reuses the shared body, swapping only chrome.
- [Source: src/SpecScribe/EpicsParser.cs:86-139] — `SplitStoryArtifact`, where Dev Notes/Context/Tasks land in the remainder.
- [Source: src/SpecScribe/SiteGenerator.cs:726-758] — `BuildStoryPageFragments`, the shared fragment pipeline + linkify passes to wrap after.
- [Source: src/SpecScribe/Toc.cs:26-34,65-85] — `RenderSidebar` dedupe + `ExtractHeadings` id-gated extraction (the TOC-invariant lever).
- [Source: src/SpecScribe/assets/specscribe.css:1065-1094] — `.ac-criterion` resting + `:target` (AC #1 target).
- [Source: src/SpecScribe/assets/specscribe.css:1309-1321,1356-1357] — `.dev-agent-details` caret grammar + left-accent idiom to reuse.
- [Source: src/SpecScribe/assets/specscribe-webview-theme.css:223-224] — the `.ac-criterion:target` webview override precedent.
- [Source: memory `specscribe-status-token-system`] — six status tokens are not for AC chrome; no new token.
- [Source: memory `charting-is-pure-svg-no-js` / `story-6-5-webview-theming-live`] — pure-CSS disclosure (CSP), webview theme is a separate byte-safe stylesheet.
- [Source: memory `create-story-elicit-visual-intent`] — why the two design directions were elicited up front.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
