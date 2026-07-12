# Story 9.6: Follow-Up Item Provenance and Resolution Paths

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer at retro time,
I want every action item and deferred-work entry to show where it came from and what closes it,
so that promises visibly leave the list when resolved.

## Acceptance Criteria

1.
**Given** an action item or deferred-work entry
**When** it renders
**Then** it carries provenance (source retro or story) and resolution criteria
**And** it links to the resolving story or spec when one exists.

2.
**Given** multiple items referencing the same underlying obligation across retros
**When** the follow-ups page renders
**Then** they are merged or explicitly cross-linked
**And** items are ordered by age or blocking status rather than flattened by identical affordances.

3.
**Given** a framework without retro or deferred-work artifact types
**When** the portal generates
**Then** these surfaces degrade to absent rather than empty-but-present (NFR8).

## Context & Scope

Epic 9 completes the requirement → epic → story chain and closes the review-follow-through loop. This is the **debt-follow-through** story (user journey 7): it makes the two "follow-up" surfaces — the **Open Action Items page** (retro action items) and the **Deferred Work page** — answer three questions for every item: *where did this come from, what closes it, and has it been closed?* Today both surfaces under-answer those questions, and the deferred-work note in particular is just raw markdown.

**Two distinct surfaces are in scope. Neither is part of the shared `IRenderAdapter` body (dashboard/epics/story) — both are standalone `WriteOutput` pages** (like `sprint.html`, `retros.html`, `diagnostics.html`), so the changes here do **not** touch `RenderStoryBody`, do **not** need a new `HostRenderException`, and do **not** need a `RenderParity` registry exception. They are HTML + SPA only (the VS Code webview renders only the dashboard/epics surfaces, never these pages — so there is **no webview-theming work** in this story).

### Surface 1 — Open Action Items page (`action-items.html`)

- **Rendered by** `ActionItemsTemplater.RenderPage` (src/SpecScribe/ActionItemsTemplater.cs — whole file, ~93 lines), invoked once by `SiteGenerator.WriteActionItems` (src/SpecScribe/SiteGenerator.cs:1395-1407) in the post-pages block.
- **Data source:** `_sprint.OpenActionItems` — the `action_items:` list in `sprint-status.yaml`, parsed into `SprintActionItem(Action, Status, EpicNumber, Owner)` records (src/SpecScribe/SprintStatus.cs:16). `OpenActionItems` filters out `done` items and **preserves file order** (SprintStatus.cs:34). The item's ties to the world are **`Action` (free text)** and **`EpicNumber`** — there is no story field, no resolution field, no resolving-link field. (`Owner` is deliberately not rendered — retro "owners" are LLM personas, Story 2.3 polish #7.)
- **What it already carries (read before touching — do NOT rebuild):** each item renders as an `<li class="action-item-card">` with the action text, an `Epic N` pill, a canonical status badge, a **"From Epic N retrospective →"** provenance link (via `EpicRetroMap`, only when a retro page exists), an **"In deferred-work backlog →"** link (only when `IsDebtRelated(text)` matches the debt-keyword regex `DebtWords` at ActionItemsTemplater.cs:90), and a **"Resolve with AI"** copyable `/bmad-quick-dev` command whose payload embeds the action text.
- **So the "source retro" half of AC #1's provenance already exists.** What this story adds to Surface 1 is: (a) **resolving-story/spec links inside the visible action text** (AC #1); (b) **cross-linking near-duplicate items across retros** (AC #2); (c) **grouped, age-ordered rendering instead of the current flat file-order list** (AC #2).
- **CRITICAL byte-safety trap (already avoided today, must stay avoided):** `WriteActionItems` deliberately does **NOT** run the page through `ApplyReferenceLinks` (SiteGenerator.cs:1402-1405) because the "Resolve with AI" `data-*` copy payload embeds the action text, and a whole-page linkifier would inject `<a>` tags **inside that attribute value** and corrupt the copyable command. Any linkification this story adds to Surface 1 **must be scoped to the visible `<div class="action-item-text">` content only** — never the command payload. Build the linked text fragment, then interpolate it; do not post-process the whole page.

### Surface 2 — Deferred Work page (`deferred-work.md` → `deferred-work.html`)

- **Rendered today** as a *generic markdown doc page* through the normal pages loop (`SiteGenerator.GenerateAll` pages phase, SiteGenerator.cs:158-169 → `GenerateOneInternal` → `HtmlTemplater.RenderPage`). It is a plain rendered document — no cards, no structure, no resolving links beyond whatever `ApplyReferenceLinks` incidentally catches.
- **`WorkInventory`** (src/SpecScribe/WorkInventory.cs) scans `_docs` for `deferred-work.md`, builds a `DeferredWorkEntry(Title, OutputPath, OpenItemCount)` (WorkInventory.cs:29-63), and `CountOpenItems` (WorkInventory.cs:70-87) counts top-level `<li>` minus struck-through (`<del>`) ones for the **home dashboard callout**. This home callout and its count must keep working after this story.
- **The note is already semi-structured** (see the live file at _bmad-output/implementation-artifacts/deferred-work.md): sections are `## Deferred from: <source>` headings; most sections carry a `source_spec: \`N-M-slug.md\`` line naming the originating story; resolved items are written `**[RESOLVED in X]**` + `~~strikethrough~~` (Markdig renders strikethrough as `<del>`). So provenance and resolution state are *authored in prose*, not in a schema — this story surfaces them, it does **not** add an authoring schema (see the load-bearing principle below).

### Owner-selected design directions (locked at create-story)

Elicited with the owner up front, per the project rule to offer named design directions for any new visual surface (Epic 3 retro action; memory `create-story-elicit-visual-intent`). Sibling stories 9.3/9.4/9.5 followed the same pattern.

1. **Deferred Work → full structured card template.** Deferred Work stops being a raw rendered doc and becomes a **custom template that parses the note into structured cards** — each item a card carrying its provenance (the `## Deferred from:` group + `source_spec:` story link), a resolving-story/spec link when named, and a distinct **resolved vs open** visual treatment. Cards reuse the existing `.action-item-card` grammar so the two follow-up surfaces read as siblings. (Chosen over the lighter "enrich-in-place via linkify only" option.)
2. **De-duplication → keep separate + cross-link.** For items referencing the same underlying obligation across retros (the canonical example: the Epic 1 heatmap-debt item repeated in the Epic 1 **and** Epic 2 retro action lists — see `sprint-status.yaml` action_items epic 1 & epic 2), render **both** items but add an **"also raised in Epic N retrospective →"** cross-link between them. Do **not** merge/collapse — a false-positive match must never silently hide a distinct real item. Keep the matching heuristic **conservative** (high similarity threshold) precisely because there is no authored dedup key.
3. **Ordering → group by source retro, age within.** The action-items list stops being a flat file-order list. Items are **grouped under their originating epic retrospective**, groups ordered by **epic number ascending** (lower epic = older = longer-unresolved, surfacing lingering promises first), and items **age-ordered within each group** (preserve file order as the within-group age proxy). Items with **no epic number** fall into a trailing "Unattributed" group so nothing is dropped.

### Non-negotiable project principle: no new authoring schema; degrade to absent (NFR8/NFR2)

Provenance, resolution criteria, and resolving links are **derived by best-effort heuristic over the text that already exists** — the `## Deferred from:` headings, `source_spec:` lines, `[RESOLVED …]`/strikethrough markers, and story/spec mentions in item text. **Do not invent a new YAML field, a new frontmatter key, or a house convention that authors must follow.** This is a load-bearing project value the owner has repeatedly flagged (memory `create-story-elicit-visual-intent`; explicit in the 9.3 and 9.4 create-story notes; candidate for its own ADR if a precise tagged link is ever wanted). Concretely:

- A framework with **no retrospectives** → no `action_items` → `action-items.html` is **not generated** and no nav/home link points at it. Already true (WriteActionItems returns early on empty; SiteGenerator.cs:1397-1398). Preserve it.
- A framework with **no deferred-work note** → no `deferred-work.html`, no home callout. Already true (WorkInventory yields no `Deferred`). Preserve it.
- A deferred-work note that **doesn't follow the `## Deferred from:` shape** (foreign framework, or an early hand-authored note) → the parser must **fall back to rendering the note's body as-is** rather than dropping content or emitting empty cards. Structured cards are an enhancement over a guaranteed plain-render floor, never a replacement that can lose text.

## Tasks / Subtasks

- [ ] **Task 1 — Action Items: resolving-story/spec links in the visible text (AC: #1)**
  - [ ] In `ActionItemsTemplater.RenderPage`, linkify **only** the visible action text (the `<div class="action-item-text">…</div>` content at ActionItemsTemplater.cs:40) so a story/spec/epic named in the text becomes a link to its page. Reuse `StoryEpicLinkifier.Linkify(fragment, epicsModel, prefix, …)` for "Story N.M"/"Epic N" mentions and add resolution for **story-key filename** mentions (`spec-…`, `N-M-slug`) via a small resolver (see Reuse map). Build the linked fragment first, then interpolate it into the card — **do not** run the whole page through a linkifier (the copy-payload corruption trap; Context above + SiteGenerator.cs:1402).
  - [ ] Pass the needed context into `RenderPage` (it currently takes `epicRetroMap`, `commands`, `nav`, `deferredWorkHref`). Add the `EpicsModel` (for `StoryEpicLinkifier`) and a story/spec href resolver (or a prebuilt `IReadOnlyDictionary<string,string>` map source-path→output-url, mirroring how the epics/story path passes a `referenceMap` to `SourceLinkifier`). Keep the signature change minimal and update the one caller (`SiteGenerator.WriteActionItems`, SiteGenerator.cs:1405).
  - [ ] **Resolution criteria (AC #1), honest reading:** retro action items have no separate authored resolution field — the action text *is* the instruction, and the "Resolve with AI" command *is* the resolution path. So the net-new resolution signal here is the **linked resolving story/spec when the text names one**. Where the text names none, do **not** fabricate criteria — the existing "Resolve with AI" command remains the resolution affordance. Do not add an empty "Resolution:" label that would render blank for most items (that violates the designed-absence principle; contrast Story 8.5 / 9.4 empty-state treatment — an absent resolver is simply not shown).

- [ ] **Task 2 — Action Items: group by source retro, age within (AC: #2, owner decision #3)**
  - [ ] Replace the flat `foreach (item in openItems)` loop with **grouped rendering**: group `OpenActionItems` by `EpicNumber`, order groups by epic number ascending, preserve file order within each group (the age proxy), and place `EpicNumber == null` items in a trailing "Unattributed" group. Do the grouping as a **pure, deterministic** transform (stable ordering — no hash-set iteration, no wall-clock) so regeneration is byte-identical (NFR8).
  - [ ] Each group gets a heading that doubles as provenance: e.g. `From the Epic N retrospective` linked to the retro page via `EpicRetroMap` (fall back to a plain, unlinked heading when no retro page exists — same gate the per-item "From Epic N retrospective →" link uses today). This can **replace** the now-redundant per-item "From Epic N retrospective →" link (the group header carries it) — decide and note which, keeping exactly one provenance affordance per item so the page doesn't double up.
  - [ ] Keep the per-item status badge, the debt→deferred-work link, and the "Resolve with AI" command unchanged inside each grouped card.

- [ ] **Task 3 — Action Items: cross-link near-duplicate items across retros (AC: #2, owner decision #2)**
  - [ ] Add a **conservative, pure** near-duplicate detector over `OpenActionItems`: two items from **different** epics whose normalized action text is highly similar (e.g. normalize case/whitespace/punctuation, then require a high token-overlap ratio or a long shared normalized substring — pick a threshold that matches the Epic 1↔Epic 2 heatmap-debt pair without matching merely same-topic items). Keep it deterministic and side-effect-free.
  - [ ] For each detected pair (owner decision: **keep both, cross-link — never merge**), render an **"also raised in Epic N retrospective →"** link on each item pointing at the other item's epic retro page (via `EpicRetroMap`). If the counterpart epic has no retro page, degrade to a plain "also raised in Epic N retrospective" note (no dead link).
  - [ ] Guard against runaway matches: if the heuristic is too eager it will produce misleading cross-links, which is worse than none. Prefer **false negatives over false positives**; a unit test pins the canonical pair and pins that unrelated items do **not** cross-link.

- [ ] **Task 4 — Deferred Work: parse the note into a structured model (AC: #1, #3, owner decision #1)**
  - [ ] Add a new **pure parser** `src/SpecScribe/DeferredWorkParser.cs` (static, never-throws, no I/O) that turns the deferred-work note into a model. Suggested shape:
    - `DeferredWorkModel` = ordered `IReadOnlyList<DeferredWorkGroup>` **plus** an `IsStructured` flag (false → caller renders the plain body fallback).
    - `DeferredWorkGroup(string ProvenanceLabel, string? SourceStoryId, string? SourceStoryHref, IReadOnlyList<DeferredWorkItem> Items)` — from a `## Deferred from: <text>` heading and its optional `source_spec:` line.
    - `DeferredWorkItem(string BodyHtml, bool Resolved, string? ResolvingRef, string? ResolvingHref)` — one top-level list item; `Resolved` when the rendered body contains `<del>` **or** a `[RESOLVED …]` marker; `ResolvingRef/Href` from a `RESOLVED in N.M`/`in Story N.M`/`source_spec` mention resolved to the story/spec page.
  - [ ] **Parse the raw markdown for structure** (headings, `source_spec:` lines, top-level `-` list items) — it is far cleaner than re-parsing rendered HTML for section boundaries — then render **each item's inline/block markdown to HTML** via `MarkdownConverter.RenderBlock` (src/SpecScribe/MarkdownConverter.cs:144) or `RenderInline` (:126) so bold/code/links inside an item survive. Reuse `WorkInventory.CountOpenItems`'s `<del>`-detection idea for the `Resolved` flag; do not duplicate its exact routine, but keep the same "struck-through == resolved" contract so the home callout count and the page agree.
  - [ ] **Provenance/resolving-link resolution:** parse the `N-M-slug` token out of a `## Deferred from: code review of N-M-slug` heading and a `source_spec: \`N-M-slug.md\`` line → story id `N.M` → `StoryEpicLinkifier.StoryPagePath("N.M")` = `epics/story-N-M.html` (a placeholder page exists for every story). For `spec-*.md` sources with a generated page, resolve via the source→output map from `_docs`. **These story-key filenames are exactly what the standard linkifiers do NOT catch** (`ApplyReferenceLinks` runs `RequirementLinkifier` + `StoryEpicLinkifier` only, on "FR/NFR"/"Story N.M"/"Epic N" prose — SiteGenerator.cs:1492-1505); the parser owns filename→page resolution.
  - [ ] **Degrade (NFR8/NFR2):** any parse failure, or a note with **zero** `## Deferred from:` headings, sets `IsStructured = false`; the parser never throws and never drops text.

- [ ] **Task 5 — Deferred Work: render the structured cards + wire the custom page (AC: #1, #3, owner decision #1)**
  - [ ] Add `src/SpecScribe/DeferredWorkTemplater.cs` mirroring `ActionItemsTemplater` (same page skeleton: `PathUtil.RenderHeadOpen` + `nav.RenderNavBar` + `SiteNav.RenderBreadcrumb` + `<main id="main-content">` + `PathUtil.RenderFooter`). Render each group as a section with its provenance header (linked to `SourceStoryHref` when present) and each item as a `.deferred-item-card` (reuse `.action-item-card` grammar). Resolved items get a **distinct, non-color-only** treatment (e.g. a "Resolved" `.status-badge` + reduced emphasis / the `<del>` already in the body), so a reader sees at a glance that "promises visibly leave the list when resolved" (the story's own "so that"). Open items lead; consider a resolved/open split or a per-item resolved badge — keep it obvious which are still outstanding.
  - [ ] **Wiring seam (recommended):** keep `deferred-work.md` flowing through the normal pages loop so it stays registered in `_docs` (WorkInventory + the structure href map depend on that), then add a post-pages `WriteDeferredWork(nav, workInventory)` in `SiteGenerator.GenerateAll` **right beside `WriteActionItems`** (SiteGenerator.cs:236-237) that reads the parsed deferred-work `DocModel` (via `workInventory.Deferred` / the same filename check `WorkInventory` uses), runs it through `DeferredWorkParser`, and **overwrites** the page at the doc's existing `OutputRelativePath` (`WorkInventory.Deferred.OutputPath` = `implementation-artifacts/deferred-work.html`) via `WriteOutput` + `ApplyReferenceLinks`. Running here (not in the pages loop) guarantees `_docs`, `_epicsModel`, and `_epicRetroMap` are fully populated so resolving links never dangle — the same reason `WriteActionItems`/`WriteSprint` run post-pages.
    - **Keep the output path identical** so the home callout (`WorkInventory.Deferred.OutputPath`) and Story 10.1's forthcoming "Follow-ups" nav group both resolve to the same page.
    - **Verify the SPA capture reflects the custom render:** `WriteOutput` captures each page for the SPA whole-site consolidation (memory `story-6-7-spa-adapter-live`); a second `WriteOutput` to the same path must leave the SPA capture holding the *custom* render (last-write-wins). Confirm, don't assume — check `_spaCapture` keying on rebuild.
  - [ ] Structured cards can safely run through `ApplyReferenceLinks` (this page has **no** copyable command payload, unlike action-items) so inline "Story N.M"/"Epic N"/"FR-N" prose mentions in item bodies get linked for free — but the story-key-filename resolution (Task 4) still happens in the parser.

- [ ] **Task 6 — CSS for the deferred-work cards + resolved treatment (AC: #1)**
  - [ ] In `src/SpecScribe/assets/specscribe.css`, add `.deferred-*` rules building on the existing `.action-items-wrap`/`.action-item-card`/`.action-item-meta`/`.action-item-retro` grammar (specscribe.css:3135-3149) so the two follow-up pages read as siblings. Add a **resolved** treatment that is **never color-only** (UX-DR17): carry it with the `<del>` strikethrough already in the body **plus** a "Resolved" `.status-badge` and/or reduced opacity + a check glyph — shape/text, not hue alone.
  - [ ] **No new `--status-*` token, no new color.** Reuse `--border`, the parchment family, `--gold`/`--teal`/`--ink-light`, and the six existing status tokens (memory `specscribe-status-token-system` — the six stage tokens are the single stage→color source and are not for follow-up chrome; the "Resolved" badge should map to the existing `done` status vocabulary, not a new token).
  - [ ] `StylesheetTests` (tests/SpecScribe.Tests/StylesheetTests.cs) guards the stylesheet — extend it (Task 8).

- [ ] **Task 7 — SPA parity + golden fingerprint regeneration (AC: #1, #2)**
  - [ ] `action-items.html` and `deferred-work.html` are standalone `WriteOutput` pages, **not** `IRenderAdapter` bodies, so HTML↔webview↔SPA body parity is **not** in play and **no new `HostRenderException` / registry chrome-exception is needed** (contrast the 3 existing webview chrome exceptions — these pages aren't webview surfaces at all). Confirm the three `Render*ParityTests` suites stay green rather than adding an exception.
  - [ ] Both pages' bytes change → the whole-site golden hash changes. Regenerate `SiteGeneratorAdapterTests.GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`'s `expected` constant (tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:213). **Caution:** a pre-existing golden drift (`977cb973`, from unrelated spec-comment-block-rendering work) has been flagged across Stories 6.11/6.12 — regenerate to the **true current** hash after your change, and don't conflate the pre-existing drift with your own delta (run the suite once on a clean tree first if in doubt). Follow the committed-fingerprint normalization rules in memory `golden-diff-normalization-gotchas`.
  - [ ] Confirm the SPA whole-site consolidation still round-trips both pages (they use `<main id="main-content">`, the capture seam) — a rebuilt SPA bundle should contain the custom deferred-work page, not the old plain-doc render.

- [ ] **Task 8 — Tests (AC: #1, #2, #3)**
  - [ ] `tests/SpecScribe.Tests/DeferredWorkParserTests.cs` (new) — pure-parser unit tests: (a) a fixture with two `## Deferred from:` groups + `source_spec:` lines parses into the right groups with resolved `SourceStoryHref`; (b) a struck-through `~~…~~` / `[RESOLVED …]` item is `Resolved = true` with a resolving ref; (c) a plain open item is `Resolved = false`; (d) a note with **no** `## Deferred from:` heading yields `IsStructured = false` (degrade); (e) empty/garbage input never throws.
  - [ ] `tests/SpecScribe.Tests/` (extend the sprint/site-generator suite that already exercises `action-items.html`, e.g. the file containing `WriteActionItems` coverage) — end-to-end on the generated `action-items.html`: (a) items are grouped under an epic-retro header, groups ordered by epic ascending; (b) an action text naming "Story N.M"/a spec becomes a link **in the visible text**, while the "Resolve with AI" `data-*` payload is **uncorrupted** (assert the payload still contains the raw text with no injected `<a`); (c) the Epic 1↔Epic 2 duplicate pair renders an "also raised in Epic N retrospective →" cross-link on each, and an unrelated item does **not**.
  - [ ] `tests/SpecScribe.Tests/` — end-to-end on the generated `deferred-work.html`: (a) an item renders as a `.deferred-item-card` with a provenance link to the source story page and (when named) a resolving link; (b) a resolved item carries the non-color-only resolved treatment; (c) the home dashboard's deferred-work callout + open-item count still render correctly (WorkInventory unaffected); (d) a fixture note without the `## Deferred from:` shape falls back to a plain body render with no empty cards (NFR8 degrade).
  - [ ] `tests/SpecScribe.Tests/` — degrade paths (AC #3): a run with **no** `action_items` produces **no** `action-items.html`; a run with **no** `deferred-work.md` produces **no** `deferred-work.html` and no home callout. Reuse existing "empty sprint"/"no deferred work" fixtures rather than authoring new project layouts.
  - [ ] `tests/SpecScribe.Tests/StylesheetTests.cs` — assert the new `.deferred-*` card + resolved rules exist and that the resolved treatment is not color-only (a shape/text signal is present).
  - [ ] Run the full suite from repo root (`dotnet test`). Watch the action-items/deferred-work site-generator tests, `StylesheetTests`, the golden fingerprint test, and the three `Render*ParityTests`.

## Dev Notes

### Reuse map (do NOT reinvent)

| Need | Use this | Location |
|------|----------|----------|
| Action-items page producer (extend, don't rebuild) | `ActionItemsTemplater.RenderPage` | src/SpecScribe/ActionItemsTemplater.cs (whole, ~93 lines) |
| Action-items write seam + the no-linkify-the-payload rule | `SiteGenerator.WriteActionItems` | src/SpecScribe/SiteGenerator.cs:1395-1407 (rule comment :1402-1405) |
| Action item record (fields available) | `SprintActionItem(Action, Status, EpicNumber, Owner)` | src/SpecScribe/SprintStatus.cs:16 |
| Open items, file-order, done-filtered | `SprintStatus.OpenActionItems` | src/SpecScribe/SprintStatus.cs:34 |
| Epic → retro page href (provenance link target) | `EpicRetroMap` (`_epicRetroMap`) | src/SpecScribe/SiteGenerator.cs:1276-1278; built in `SetRetros` :1240-1244 |
| Deferred-work note detection + home callout + open count | `WorkInventory` / `DeferredWorkEntry` / `CountOpenItems` | src/SpecScribe/WorkInventory.cs (`Build` :29-63, `CountOpenItems` :70-87) |
| Story id → story page path (for `N-M-slug` filenames) | `StoryEpicLinkifier.StoryPagePath` | src/SpecScribe/StoryEpicLinkifier.cs:46 |
| "Story N.M"/"Epic N" prose linkifier (anchor/code/attr-safe) | `StoryEpicLinkifier.Linkify` | src/SpecScribe/StoryEpicLinkifier.cs (ProtectedSplit) |
| `_bmad-output/…md` source-citation linkifier | `SourceLinkifier.Linkify` | src/SpecScribe/SourceLinkifier.cs |
| Whole-page linkify (Req + StoryEpic) used by every normal page | `SiteGenerator.ApplyReferenceLinks` | src/SpecScribe/SiteGenerator.cs:1492-1505 |
| Render an item's inline/block markdown to HTML | `MarkdownConverter.RenderInline` / `RenderBlock` | src/SpecScribe/MarkdownConverter.cs:126 / :144 |
| Copyable BMad command affordance (keep as-is) | `BmadCommands.RenderLabeledCommand` | (used at ActionItemsTemplater.cs:72) |
| Canonical status badge (for "Resolved" = `done`) | `StatusStyles.Badge` / `StatusStyles.ForSprint` | src/SpecScribe/StatusStyles.cs |
| Standalone-page skeleton (head/nav/breadcrumb/main/footer) | `PathUtil.RenderHeadOpen` + `nav.RenderNavBar` + `SiteNav.RenderBreadcrumb` + `PathUtil.RenderFooter` | pattern in ActionItemsTemplater.cs:18-33,78-81 |
| Post-pages write ordering (why WriteDeferredWork goes there) | `GenerateAll` post-pages block | src/SpecScribe/SiteGenerator.cs:226-241 |
| Action-item CSS grammar to extend | `.action-item-*` | src/SpecScribe/assets/specscribe.css:3135-3149 |
| Whole-site golden fingerprint constant | `SiteGeneratorAdapterTests` | tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:213 |

### Guardrails & invariants

- **No new authoring schema — derive from existing text; degrade to absent.** The load-bearing project principle for Epic 9 (owner-flagged, memory `create-story-elicit-visual-intent`; echoed in 9.3/9.4 create-story notes). Provenance/resolution/dedup are heuristics over `## Deferred from:` headings, `source_spec:` lines, `[RESOLVED]`/`<del>` markers, and story/spec mentions. Never require a YAML/frontmatter field. (AC #3 / NFR8.)
- **Never corrupt the "Resolve with AI" copy payload.** Linkify the *visible* action text only, never the whole action-items page — the `data-*` command payload embeds the action text and a page-wide linkifier injects `<a>` inside the attribute and breaks the copyable command (SiteGenerator.cs:1402-1405). This is the #1 review checkpoint for Surface 1.
- **These are standalone pages, not adapter bodies.** No `RenderStoryBody` change, no `HostRenderException`, no `RenderParity` registry exception, no webview theming (the webview never renders action-items/deferred-work). Keep the three parity suites green without exceptions.
- **Deterministic output (NFR8).** Grouping, dedup detection, and parsing must be pure and order-stable — no hash-set iteration order, no wall-clock, no nondeterministic similarity. From-scratch regeneration must be byte-identical, which is what lets the golden fingerprint pin the output.
- **Conservative dedup — false negatives beat false positives.** A wrong cross-link is worse than a missed one; a wrong *merge* could hide a real item, which is why the owner chose keep-both-and-cross-link. Pin the canonical Epic 1↔Epic 2 pair and a non-match in tests.
- **Preserve the home callout + count.** `WorkInventory.CountOpenItems` (struck-through == resolved) feeds the dashboard; keep `deferred-work.md` in `_docs` and keep the "struck-through == resolved" contract consistent between the page's resolved treatment and the count so the two never disagree.
- **Never color-only (UX-DR17).** The resolved treatment carries shape/text (`<del>` + a "Resolved" badge/glyph), not hue alone.
- **No new `--status-*` token, no new color** (memory `specscribe-status-token-system`); "Resolved" maps onto the existing `done` vocabulary.
- **Golden bytes change on two pages.** Regenerate the fingerprint (SiteGeneratorAdapterTests.cs:213) per memory `golden-diff-normalization-gotchas`; watch for the known pre-existing `977cb973` drift so you regenerate to the true post-change hash, not a conflated one.
- **Coordinate with siblings.** Story 10.1 (ready-for-dev) will add a "Follow-ups" nav group pointing at Action Items + Deferred Work — **keep both pages at their current output paths** so its links resolve (memory `story-10-1-hierarchical-nav-rearchitecture`). Stories 9.1/9.3/9.4/9.5 touch requirement/story pages, not these two follow-up pages — no overlap.

### Project Structure Notes

- New source: `src/SpecScribe/DeferredWorkParser.cs` (pure parser + model records), `src/SpecScribe/DeferredWorkTemplater.cs` (page renderer). Modified: `src/SpecScribe/ActionItemsTemplater.cs` (grouping + cross-link + visible-text linkify), `src/SpecScribe/SiteGenerator.cs` (`WriteActionItems` signature/caller + new `WriteDeferredWork`), `src/SpecScribe/assets/specscribe.css` (`.deferred-*` + resolved). **No change** to `RenderStoryBody`, any `IRenderAdapter`, the `StoryPageView` model, `Toc.cs`, `WorkInventory`'s contract, `sprint-status.yaml`/`deferred-work.md` schemas, or `package.json`.
- Tests in `tests/SpecScribe.Tests/`. Generate to `SpecScribeOutput/` by default when verifying — **not** `docs/live` (vestigial/gitignored; memory `generate-output-dir-is-specscribeoutput`).
- No epics.md / authoring-schema change.

### Testing standards

- xUnit (`tests/SpecScribe.Tests`), `Assert.Contains`/`Assert.DoesNotContain` on generated HTML strings — the established pattern in the sprint/action-items and stylesheet tests. Run `dotnet test` from repo root.
- Pure-parser tests (`DeferredWorkParserTests`) exercise the model directly with hand-built markdown fixtures — cheaper and more precise than end-to-end for boundary/degrade cases; mirror how `WorkInventory.CountOpenItems` and the linkifiers are unit-tested.
- Include at least one fixture per degrade path (no action_items; no deferred-work note; a deferred note without the `## Deferred from:` shape) so NFR8/AC #3 is proven, not assumed.
- Parity is a hard gate: the three `Render*ParityTests` must stay green (these pages aren't adapter bodies, so they should be unaffected — confirm).

### Verify before marking review

Generate the portal against this repo's own `_bmad-output` (`SpecScribeOutput/`). Open **`action-items.html`**: confirm items are grouped under their epic retrospectives (Epic 1's group first), the Epic 1↔Epic 2 heatmap-debt items each show an "also raised in Epic N retrospective →" cross-link, an action text that names a story/spec shows an inline link to that page, and the "Resolve with AI" button still copies an intact command (paste it somewhere and confirm no stray `<a>` markup). Open **`deferred-work.html`**: confirm each item is a card with a provenance link to its source story, resolved items are visibly struck/badged as Resolved (distinct from open items at a glance), and named resolving stories link through. Confirm the **home dashboard** still shows the deferred-work callout with the correct open count. Then confirm `dotnet test` (including the golden fingerprint and the three parity suites) is green.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 9.6] (epics.md:1636-1659) — user story + the three ACs.
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 9] (epics.md:1530-1534) — epic intent; FR30, UX-DR26, NFR8; review + debt-follow-through journeys.
- [Source: _bmad-output/planning-artifacts/epics.md] (epics.md:74, 99, 192) — FR30 (provenance/resolution/dedup + graceful degrade) and NFR8 (framework-agnostic degrade) verbatim.
- [Source: src/SpecScribe/ActionItemsTemplater.cs] — the action-items page producer (visible text :40, meta/provenance :42-62, resolve command :67-74, debt heuristic :85-92).
- [Source: src/SpecScribe/SiteGenerator.cs:1395-1407] — `WriteActionItems` + the never-linkify-the-payload rule (:1402-1405).
- [Source: src/SpecScribe/SiteGenerator.cs:226-241] — post-pages write block where `WriteDeferredWork` belongs (beside `WriteActionItems`).
- [Source: src/SpecScribe/SiteGenerator.cs:1492-1505] — `ApplyReferenceLinks` runs only Req + StoryEpic linkifiers (so story-key filenames need parser-side resolution).
- [Source: src/SpecScribe/SiteGenerator.cs:1240-1244,1276-1278] — `EpicRetroMap` build + accessor (epic → retro page).
- [Source: src/SpecScribe/WorkInventory.cs] — deferred-work detection, `DeferredWorkEntry`, `CountOpenItems` (home callout + open count).
- [Source: src/SpecScribe/SprintStatus.cs:16,34] — `SprintActionItem` fields + `OpenActionItems` (file-order, done-filtered).
- [Source: src/SpecScribe/StoryEpicLinkifier.cs:46] — `StoryPagePath` (story id → `epics/story-N-M.html`).
- [Source: src/SpecScribe/SourceLinkifier.cs] — `_bmad-output/…md` citation linkifier.
- [Source: src/SpecScribe/MarkdownConverter.cs:126,144] — `RenderInline`/`RenderBlock` for item bodies.
- [Source: src/SpecScribe/assets/specscribe.css:3135-3149] — `.action-item-*` grammar to extend for `.deferred-*`.
- [Source: tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs:198-231] — whole-site golden fingerprint (constant :213) to regenerate.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — the live note; its `## Deferred from:` / `source_spec:` / `[RESOLVED]`+`~~strike~~` shape the parser targets.
- [Source: _bmad-output/implementation-artifacts/sprint-status.yaml action_items] — the Epic 1 & Epic 2 heatmap-debt entries: the canonical cross-retro duplicate pair for AC #2.
- [Source: memory `create-story-elicit-visual-intent`] — why the three design directions were elicited; no-new-authoring-schema is load-bearing.
- [Source: memory `specscribe-status-token-system`] — six status tokens are the single stage→color source; no new token for follow-up chrome.
- [Source: memory `story-6-7-spa-adapter-live`] — SPA whole-site capture via `<main id="main-content">`; verify last-write-wins on the deferred-work overwrite.
- [Source: memory `story-10-1-hierarchical-nav-rearchitecture`] — the forthcoming "Follow-ups" nav group; keep both page output paths stable.
- [Source: memory `golden-diff-normalization-gotchas`] — normalization + regeneration rules for the committed fingerprint.
- [Source: memory `generate-output-dir-is-specscribeoutput`] — verify against `SpecScribeOutput/`, never `docs/live`.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
