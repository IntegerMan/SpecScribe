---
baseline_commit: 5628f45eafd902d65be005a16e4b20c6f8e89936
---

# Story 10.3: Glossary and In-Place Vocabulary

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a first-time visitor who does not know BMAD,
I want unfamiliar terms defined in place and a suggested reading order,
so that I can orient myself and read the portal without prior methodology knowledge — the adoption gate for Journey 5 ("Get me up to speed").

## Context & Why This Story Exists

This is the code side of **feedback T6 / MissingFeature D1+D2**: _"The portal assumes BMAD fluency."_ ([docs/Epic3UXFeedback.md:50-52](docs/Epic3UXFeedback.md), [docs/MissingFeatures.md:65-71](docs/MissingFeatures.md)). A first-time visitor "doesn't know BMAD, doesn't know what FR/NFR/AC mean, and doesn't know which of the nine nav items to read first. The portal currently assumes fluency." ([docs/UserJourneys.md:55-63](docs/UserJourneys.md)). Three concrete gaps exist today:

1. **Acronyms appear undefined.** FR, NFR, AC, ADR, PRD, "BMad", `/bmad-*`, "spec kernel", "quick-dev" all surface with no expansion. The one existing linkifier, [RequirementLinkifier.cs:16](src/SpecScribe/RequirementLinkifier.cs), only matches numbered references (`\b(FR|NFR)(\d+)\b`) — a *bare* "FR", "AC", or "ADR" in prose gets nothing.
2. **There is no orientation page.** A grep for `glossary`, `how to read`, `reading order`, `vocabulary`, `abbr`, `onboard` returns **nothing** in `src/` — this is genuinely net-new. There is no "start here" for the Home page's Explore Key Views grid.
3. **The reading order is implicit.** Journey 5's canonical path is Readme → PRD → Architecture / ADRs → Epics → current sprint ([docs/UserJourneys.md:57-58](docs/UserJourneys.md)), but nothing on Home states it; a first-time visitor faces the nav items with no sequence.

**The load-bearing requirement is AC2, not AC1.** Writing a hand-authored `how-to-read.html` full of "FR = Functional Requirement, ADR = Architecture Decision Record, `/bmad-*` = …" would satisfy AC1 by copy-paste — and would be a lie the moment SpecScribe renders a **Game Dev Studio** repo (which has no FR/NFR/ADR vocabulary; it has GDD, narrative beats, `/gds-*` commands) or any future framework. AC2 forbids that: the glossary vocabulary and the command captions are **framework-specific**, so they must be **adapter-supplied via the module context, never hard-coded in shared rendering** (NFR8) — and a framework without an equivalent concept **omits** that entry rather than mislabeling it. This story's real spine is a new *adapter-supplied vocabulary seam*, mirroring how planning-doc labels already come from `ModuleContext.Docs` and command strings already come from `CommandCatalog` ([ModuleContext.cs:56-116](src/SpecScribe/ModuleContext.cs)).

**Adapter-supplied is the existing idiom — extend it, don't fork it.** The codebase already routes every framework-specific label through the detected module:

- Planning-doc labels ("PRD", "Architecture" vs "GDD", "Narrative") come from `ModuleContext.DocsFor(module)` ([ModuleContext.cs:96-116](src/SpecScribe/ModuleContext.cs)).
- Slash commands (`/bmad-*` vs `/gds-*`) come from the per-module `CommandCatalog` parsed from `module-help.csv` ([ModuleContext.cs:19-52](src/SpecScribe/ModuleContext.cs)).
- Shared rendering **already avoids** hard-coding vocabulary: `RequirementsTemplater` renders from the model's kind, never a literal "FR"/"NFR" string; `BmadCommands` pulls command names from the catalog, printing nothing when the module doesn't expose a step.

The glossary is the next member of that family: a per-module vocabulary set carried on `ModuleContext`, consumed by (a) the new orientation page and (b) the acronym expander, so shared code holds **zero** BMAD terms.

**Sequencing note — this story stands alone on the *current* flat nav.** Story 10.1 (grouped nav) and 10.2 (chart metadata) are `ready-for-dev` but **not yet implemented**; do not assume the grouped-nav model exists. This story touches Home's Explore Key Views quick-link grid + the footer/nav registration, which are stable today. Write against the flat nav; if 10.1 lands first, the how-to-read registration rides its Project group with no rework (it is a `QuickLinks`/nav entry either way).

## Acceptance Criteria

**AC1 (Orientation page + reading order + first-use acronym expansion)**
Given a first visit to the portal,
When I open Home,
Then a linked **"How to read this portal"** page defines the portal vocabulary and suggests a **reading order**,
And **acronyms (FR/NFR, AC, ADR) expand on first use per page** via `<abbr>` semantics.

**AC2 (Vocabulary is adapter-supplied, never hard-coded — NFR8)**
Given glossary terms and command captions are framework-specific,
When the portal generates for any supported framework,
Then that vocabulary is **adapter-supplied, never hard-coded in shared rendering** (NFR8),
And frameworks **without equivalent concepts simply omit those glossary entries** (no empty-but-present glossary, no mislabeled term).

## Design Direction — the adapter-supplied vocabulary seam (AC2 is the #1 review checkpoint)

**Confirm the seam shape at review, before wiring the page and the expander.** This is the "silhouette" of the story. Recommended design (latitude noted):

### The seam: `ModuleContext.Glossary`

Add a small vocabulary record + a per-module source, co-located with the existing `ModuleDoc`/`DocsFor` machinery in [ModuleContext.cs](src/SpecScribe/ModuleContext.cs) (no new file, same "well-known-per-module" pattern):

```csharp
/// One portal-vocabulary entry a module publishes. Term is the token as it appears in prose ("FR",
/// "ADR", "spec kernel"); Expansion is the <abbr title> text ("Functional Requirement"); Definition is
/// the one-line gloss shown on the How-to-read page. Abbr-shaped entries (short, all-caps acronyms) drive
/// the in-page <abbr> expander; longer terms appear only in the glossary list.
public sealed record GlossaryTerm(string Term, string Expansion, string Definition, bool IsAcronym);

// On ModuleContext, alongside Docs:
public required IReadOnlyList<GlossaryTerm> Glossary { get; init; }

// Well-known per module, mirroring DocsFor(...). Unknown module => Array.Empty (glossary omitted).
public static IReadOnlyList<GlossaryTerm> GlossaryFor(BmadModule module) => module switch { ... };
```

BMad Method's set (drawn straight from T6's list): `FR` → Functional Requirement, `NFR` → Non-Functional Requirement, `AC` → Acceptance Criterion, `ADR` → Architecture Decision Record, `PRD` → Product Requirements Document, plus longer glossary-only terms ("spec kernel", "quick-dev", "epic", "story", "sprint"). Game Dev Studio's set is its own (GDD, narrative beat, `/gds-*` concepts). `ModuleContext.None.Glossary = Array.Empty<GlossaryTerm>()` so an undetected framework contributes **nothing** — the page's glossary section and the expander both degrade to absent (AC2 / NFR8).

**Latitude / constraints:**
- **`ModuleContext.None` must stay valid.** It is the undetected-framework fallback ([ModuleContext.cs:63-68](src/SpecScribe/ModuleContext.cs)); add `Glossary = Array.Empty<GlossaryTerm>()` there so every existing `None` consumer keeps compiling and the no-module path emits no vocabulary.
- **Shared rendering holds zero terms (the NFR8 teeth).** After this story, grep must find **no** BMAD acronym→expansion literal in any shared templater/rewriter — the expansions live only in `GlossaryFor(BmadModule.BmadMethod)`. A reviewer will grep the expander and the how-to-read templater for a hard-coded `"Functional Requirement"`; there must be none outside `GlossaryFor`.
- **`SiteGenerator` already carries `_module`** ([SiteGenerator.cs:105](src/SpecScribe/SiteGenerator.cs) populates it; `_module.Commands`/`_module.Docs` already flow to templaters). Thread `_module.Glossary` the same way — no new plumbing shape.

### AC1 part A — the "How to read this portal" page

Mirror the existing standalone-info-page pattern exactly — [AboutTemplater.cs:81-142](src/SpecScribe/AboutTemplater.cs) is the closest model (chromeless synthesized page: `RenderHeadOpen` → `RenderNavBar` → `RenderBreadcrumb` → `<main id="main-content" class="info-page">` → `RenderFooter`). Add a new `HowToReadTemplater.cs`:

- **Reading order** — a numbered ordered list built from the *available* pages in Journey 5's sequence: Readme → PRD → Architecture → ADRs → Epics → Sprint. **Gate each step on the same availability signal the nav already uses** — reuse `nav.QuickLinks`/`nav.Items` (they already only contain pages that exist) rather than re-deriving. A step whose page is absent is **omitted** (a shallow repo with no PRD gets a shorter, honest list — NFR8).
- **Glossary** — a `<dl>` of `GlossaryTerm.Term` → `Definition` from `_module.Glossary`, sorted stably (acronyms first, then longer terms). If the glossary is empty (undetected framework), **omit the whole section** rather than render an empty `<dl>`.
- **Command legend (light touch, optional)** — a short "commands you'll see" note that the `/bmad-*` (or `/gds-*`) commands come from your detected methodology, reusing `_module.Commands.ModuleLabel` ([ModuleContext.cs:29-31](src/SpecScribe/ModuleContext.cs)) — do **not** re-enumerate every command; the story pages already caption them.
- Write it on every full run via a `WriteHowToRead(nav)` method next to `WriteAbout`/`WriteDiagnostics` ([SiteGenerator.cs:254-255](src/SpecScribe/SiteGenerator.cs)) so its link can never 404. Add `SiteNav.HowToReadOutputPath = "how-to-read.html"` beside the other page constants ([SiteNav.cs:8-52](src/SpecScribe/SiteNav.cs)).

### AC1 part A — linking it from Home ("linked from Home's Explore Key Views")

T6/D1 name the exact placement: **Home's Explore Key Views**. Add "How to read this portal" as a `QuickLinks` entry (the Explore Key Views grid is built from `nav.QuickLinks` — [SiteNav.cs:53-56](src/SpecScribe/SiteNav.cs)) so it lands as a card on Home. Recommended: make it the **first** card (a first-time visitor should meet orientation before the PRD). Keep it out of the top-nav `Items` (like About/Diagnostics — the info-page convention at [SiteNav.cs:44-52](src/SpecScribe/SiteNav.cs)); the Home card + the footer are its reach. **Optional (confirm at review):** also add a compact first-visit CTA banner near the top of the dashboard body ([HtmlRenderAdapter.Dashboard.cs](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs)) — but the Explore Key Views card is the AC-named requirement; the banner is polish.

### AC1 part B — the first-use `<abbr>` expander (the tricky part)

Add an `AbbreviationExpander` post-process rewriter mirroring [RequirementLinkifier.cs](src/SpecScribe/RequirementLinkifier.cs) exactly (the same anchor-split so it never rewrites inside a link):

```csharp
public static string Expand(string html, IReadOnlyList<GlossaryTerm> glossary)
{
    if (string.IsNullOrEmpty(html) || glossary.Count == 0) return html;   // no vocabulary => no-op (AC2)
    var acronyms = glossary.Where(g => g.IsAcronym).ToList();
    if (acronyms.Count == 0) return html;
    var seen = new HashSet<string>(StringComparer.Ordinal);               // first-use-per-PAGE state
    var parts = AnchorSplit.Split(html);                                  // skip <a>…</a> spans (nav, footer, refs)
    for (var i = 0; i < parts.Length; i++)
    {
        if (i % 2 == 1) continue;
        parts[i] = ReplaceFirstUse(parts[i], acronyms, seen);            // wraps only the FIRST unseen occurrence
    }
    return string.Concat(parts);
}
```

**Guardrails the dev must honor (these are the review checkpoints for AC1-B):**
- **First use per page only.** Track a per-call `HashSet` of already-expanded terms; wrap only the first match of each acronym, leave every later occurrence as plain text. T6 says "expand acronyms on first use per page (`<abbr>` is enough)".
- **Never rewrite inside an anchor.** The anchor-split (odd indices) protects nav labels ("PRD", "ADRs"), breadcrumb links, footer links, and already-linkified `FR25`/`Story 3.2` references — critical, because bare acronyms *do* appear in chrome as anchor text (unlike numbered refs).
- **Never rewrite inside `<code>`, `<pre>`, `<abbr>`, `<script>`, `<style>`, `<head>`, or an HTML tag/attribute.** `/bmad-create-story` inside a `<code>` badge, a `title="..."` attribute, or an existing `<abbr>` must be left alone. Extend the split/skip set beyond anchors to at least `<code>…</code>`, `<pre>…</pre>`, `<abbr>…</abbr>`; and match acronyms only on word boundaries in text nodes. **Decide the seam:** the simplest robust approach is to also split on `<code>`/`<pre>`/`<abbr>` spans (extend `AnchorSplit` into a `ProtectedSpanSplit`) so those are skipped like anchors — do not attempt a full HTML parser.
- **Match on word boundary, whole token.** `\bAC\b` must not fire inside "AC**T**ION" or "he**AC**he"; `\bADR\b` not inside "ADRess". Use `\b(FR|NFR|AC|ADR|PRD|...)\b` built from the acronym set (escape, longest-first alternation) so "NFR" wins over "FR" when both would match at the same spot.
- **Output shape:** `<abbr title="Functional Requirement">FR</abbr>` — `title` is `PathUtil.Html(expansion)`-escaped. No class needed unless CSS wants one (a subtle dotted underline is the conventional `<abbr>` affordance; add `abbr[title]` styling in [specscribe.css](src/SpecScribe/assets/specscribe.css)).

**Wire it into the pipeline** at [SiteGenerator.ApplyReferenceLinks](src/SpecScribe/SiteGenerator.cs#L1521) — the whole-page pass already called on every content page (docs, epics, requirements, stories, readme) — **after** `RequirementLinkifier`/`StoryEpicLinkifier` so numbered refs are already anchors (and thus skipped by the expander's anchor-split). Source the glossary from `_module.Glossary`. When `_module.Glossary` is empty the expander is a no-op, so the undetected-framework and no-module paths are byte-unchanged (AC2 / regression safety).

**Note the synthesized-page asymmetry:** `WriteAbout`/`WriteDiagnostics` (and your new `WriteHowToRead`) write HTML **directly, not through `ApplyReferenceLinks`** ([SiteGenerator.cs:1446-1465](src/SpecScribe/SiteGenerator.cs)). That is correct and intended — the how-to-read page *is* the definitions, so it should not self-expand its own glossary terms into nested `<abbr>`. Do **not** route those pages through the expander.

### AC2 — command captions

AC2 names "command captions" alongside glossary terms as framework-specific vocabulary. **Current state:** command *names* are already adapter-supplied (from `CommandCatalog`), but the caption *sentences* ("Final adversarial pass over the story's changes.") are hard-coded English in [BmadCommands.cs:231,267,371,376](src/SpecScribe/BmadCommands.cs) — a file that is already the BMad-specific command surface (its status→step logic is BMad's lifecycle). **Scope decision (confirm at review):** treat AC2's "command captions" requirement as satisfied by the fact that captions live in the BMad-named command surface (not in shared cross-framework rendering) and command names come from the per-module catalog; do **not** re-architect `BmadCommands` caption sourcing in this story (that is a larger refactor and risks scope creep). The **new** deliverable AC2 demands is the *glossary* being adapter-supplied. Record this scoping decision in the Dev Agent Record so the reviewer sees it was deliberate, not an omission.

## Tasks / Subtasks

- [x] **Task 1 — Add the adapter-supplied glossary seam** (AC: 2)
  - [x] Add the `GlossaryTerm` record and `IReadOnlyList<GlossaryTerm> Glossary` to `ModuleContext` in [ModuleContext.cs](src/SpecScribe/ModuleContext.cs), co-located with `ModuleDoc`/`Docs`. Add `GlossaryFor(BmadModule)` mirroring `DocsFor` ([ModuleContext.cs:110-116](src/SpecScribe/ModuleContext.cs)): a BMad Method set (FR, NFR, AC, ADR, PRD as acronyms; "spec kernel", "quick-dev", "epic", "story", "sprint" as glossary-only terms), a Game Dev Studio set (its own vocabulary), and `Array.Empty` for `Unknown`.
  - [x] Set `Glossary = GlossaryFor(module)` wherever `ModuleContext` is constructed from a detected module, and `Glossary = Array.Empty<GlossaryTerm>()` on `ModuleContext.None` ([ModuleContext.cs:63-68](src/SpecScribe/ModuleContext.cs)). Confirm every `ModuleContext { ... }` initializer still compiles (the `required` members).
  - [x] **Do not** place any acronym→expansion literal outside `GlossaryFor` — this is the single source of vocabulary (the same discipline as `WellKnownDocs` being the single source of doc filenames, [ModuleContext.cs:73-88](src/SpecScribe/ModuleContext.cs)).

- [x] **Task 2 — The first-use `<abbr>` expander** (AC: 1)
  - [x] Add `AbbreviationExpander.cs` in `src/SpecScribe/` following the [RequirementLinkifier.cs](src/SpecScribe/RequirementLinkifier.cs) rewriter shape: static class, `Expand(string html, IReadOnlyList<GlossaryTerm> glossary)`, anchor-split (extended to also skip `<code>`/`<pre>`/`<abbr>` spans), per-page first-use `HashSet`, whole-token word-boundary regex built from the acronym set (longest-first alternation), emitting `<abbr title="{escaped expansion}">{term}</abbr>`.
  - [x] No-op fast paths: empty html, empty/acronym-free glossary → return input unchanged (guarantees the no-module path is byte-identical).
  - [x] Wire into [ApplyReferenceLinks](src/SpecScribe/SiteGenerator.cs#L1521) **after** the requirement + story/epic linkifiers, passing `_module.Glossary`. Confirm it runs on every content page but **not** on the synthesized About/Diagnostics/How-to-read writes.
  - [x] Add `abbr[title]` styling (subtle dotted underline, `cursor: help`) to [specscribe.css](src/SpecScribe/assets/specscribe.css); verify it themes under the webview `.vscode-*` bridge (reuse an existing muted/border variable — no new mapping).

- [x] **Task 3 — The "How to read this portal" page** (AC: 1, 2)
  - [x] Add `SiteNav.HowToReadOutputPath = "how-to-read.html"` beside the existing page constants ([SiteNav.cs:8-52](src/SpecScribe/SiteNav.cs)), with an XML-doc comment matching the About/Diagnostics style (written every run; reached from Home's Explore Key Views + footer path).
  - [x] Add `HowToReadTemplater.cs` modeled on [AboutTemplater.cs:81-142](src/SpecScribe/AboutTemplater.cs): `RenderPage(SiteNav nav, IReadOnlyList<GlossaryTerm> glossary, string moduleLabel)`. Sections: (a) intro sentence; (b) **Reading order** ordered list built from available pages in Journey 5 sequence (gate on `nav.QuickLinks`/`nav.Items` presence — omit absent steps); (c) **Glossary** `<dl>` from `glossary` (omit the whole section when empty); (d) optional short command-legend note using `moduleLabel`.
  - [x] Add `WriteHowToRead(nav)` to [SiteGenerator.cs](src/SpecScribe/SiteGenerator.cs) beside `WriteAbout`/`WriteDiagnostics` ([SiteGenerator.cs:1446-1465](src/SpecScribe/SiteGenerator.cs)); call it in `GenerateAll` next to those writes ([SiteGenerator.cs:254-255](src/SpecScribe/SiteGenerator.cs)); pass `_module.Glossary` + `_module.Commands.ModuleLabel`. Write directly (NOT via `ApplyReferenceLinks` — see Design Direction).

- [x] **Task 4 — Link it from Home's Explore Key Views** (AC: 1)
  - [x] In [SiteNav.Build](src/SpecScribe/SiteNav.cs), add a `QuickLinks` entry `("How to read this portal", HowToReadOutputPath, "New here? Start with the reading order and glossary.")` — recommended as the **first** quick-link so it leads the Explore Key Views grid. Keep it OUT of top-nav `Items` (info-page convention). **DEVIATED (see Dev Agent Record):** Story 10.1 shipped ahead of this story and retired the flat "Explore Key Views" dashboard grid in favor of a journey-organized top nav shared by every page. `nav.QuickLinks` now only surfaces as a per-page pill band that Home itself does not render, so the entry ALSO rides the Project nav group (leading it) to satisfy AC1's "linked from Home" requirement.
  - [x] Verify the Home dashboard renders the card (the grid consumes `nav.QuickLinks`). Confirm the card link resolves from Home (root-relative, no `../` needed) and from the footer path if you also surface it there.
  - [ ] **Optional (confirm at review):** a first-visit CTA banner in [HtmlRenderAdapter.Dashboard.cs](src/SpecScribe/HtmlRenderAdapter.Dashboard.cs) linking the page — polish, not required by the AC. Skipped — not required by the AC.

- [x] **Task 5 — Tests** (AC: 1, 2)
  - [x] **Glossary seam** — a `ModuleContext`/`GlossaryFor` test: BMad Method returns the FR/NFR/AC/ADR/PRD acronym set; Game Dev Studio returns its own; `Unknown`/`None` returns empty. Assert `ModuleContext.None.Glossary` is empty.
  - [x] **`AbbreviationExpander`** unit tests (mirror [RequirementLinkifier] test style): first occurrence of "FR"/"AC"/"ADR" is wrapped in `<abbr title="...">`, subsequent occurrences are plain; a term inside `<a>…</a>` / `<code>…</code>` / an existing `<abbr>` / a tag attribute is untouched; word-boundary safety ("ACTION"/"address" not matched); empty glossary → input returned byte-identical; `title` is HTML-escaped; longest-match precedence (NFR over FR).
  - [x] **How-to-read page** — a generation-level test (temp-root fixture style, mirroring `SiteGenerator*` tests): `how-to-read.html` is written every run; carries the reading-order list (only for pages that exist) and a glossary `<dl>` with the BMad terms; Home's nav links to it. Add a no-module/undetected case: glossary section omitted, page still renders (reading order may be minimal), no dead links.
  - [x] **Parity + golden** — the abbr expander changes bytes on **every content page** (any page mentioning FR/AC/ADR). Regenerated the committed golden fingerprint/inventory constant and the `RenderParityTests`/`SiteNavTests`/`IconsTests` fixtures the new nav entry + icon touched; `RenderParity` stays green (no new `HostRenderException`).

- [x] **Task 6 — Verify end-to-end on the real repo** (AC: 1, 2)
  - [x] `dotnet run` a full generate: open `index.html` → confirm the nav's Project group leads with "How to read this portal"; open `how-to-read.html` → confirm the reading order (Readme → PRD → Architecture → ADRs → Epics → Sprint, minus any absent) and the glossary `<dl>` (FR/NFR/AC/ADR/PRD + longer terms).
  - [x] Open `epics.html` / a story page / `requirements.html` → confirm the **first** "FR"/"AC"/"ADR" mention shows the `<abbr>` tooltip and later mentions are plain; confirm nav/footer/`<code>` command badges are untouched.
  - [x] Confirm the webview (`specscribe webview`) and `--spa` render the same expansions + the new page card (they ride the shared page HTML + `nav.QuickLinks`).

## Dev Notes

### Architecture patterns & constraints (must follow)

- **Adapter-supplied vocabulary is the AC2 contract.** After this story there must be exactly **one** source of the BMAD glossary (`GlossaryFor(BmadModule.BmadMethod)`), and shared templaters/rewriters must hold **zero** acronym→expansion literals. This is the same "single source of truth per module" discipline `WellKnownDocs`/`DocsFor` and `CommandCatalog` already enforce ([ModuleContext.cs:73-116](src/SpecScribe/ModuleContext.cs)). The reviewer will grep the expander + how-to-read templater for a hard-coded expansion string — there must be none.
- **Degrade to absent, never empty-but-present (NFR8).** An undetected framework (`ModuleContext.None`) supplies an empty glossary → the expander is a no-op and the page's glossary section is omitted. A shallow repo (no PRD/sprint) yields a shorter reading order. Never render an empty `<dl>` or a reading-order step whose page wasn't produced.
- **No information-bearing JavaScript.** `<abbr title>` is native HTML; the how-to-read page is static (like About/Diagnostics). Nothing here needs script — matching the portal's pure-HTML/CSS ethos and the webview CSP.
- **Anchor/code-span safety is non-negotiable.** The expander must never touch text inside `<a>`, `<code>`, `<pre>`, `<abbr>`, or a tag attribute — bare acronyms appear in nav labels, breadcrumb links, and command badges, so a naive whole-page replace would corrupt chrome. Follow `RequirementLinkifier`'s split-and-skip pattern, extended to the protected span set.
- **First-use-per-page is per-call state**, not global — the `HashSet` lives inside a single `Expand` invocation so each page independently expands its own first occurrences. `ApplyReferenceLinks` is already called once per page, so this is the natural seam.
- **Synthesized info-pages bypass `ApplyReferenceLinks` by design.** About/Diagnostics/How-to-read write HTML directly ([SiteGenerator.cs:1446-1465](src/SpecScribe/SiteGenerator.cs)); the how-to-read page defines the terms, so it must not self-expand. Only content pages (docs, epics, requirements, stories, readme) get the expander.
- **`ModuleContext` already flows to the render layer.** `_module` is populated in `SiteGenerator` ([SiteGenerator.cs:105](src/SpecScribe/SiteGenerator.cs)) and its `Commands`/`Docs` already reach nav + templaters; `Glossary` rides the same object — no new plumbing.
- **Invariant/escaping discipline.** All `<abbr title>` and glossary text through `PathUtil.Html(...)`. Reading-order dates/labels reuse existing helpers; no new formatting.

### Source tree — files to touch

- `src/SpecScribe/ModuleContext.cs` — add `GlossaryTerm` + `Glossary` member + `GlossaryFor(module)`; set it on every `ModuleContext` construction incl. `None` (UPDATE — the AC2 heart).
- `src/SpecScribe/AbbreviationExpander.cs` — new post-process rewriter, first-use `<abbr>` expansion (NEW).
- `src/SpecScribe/HowToReadTemplater.cs` — the orientation page, modeled on `AboutTemplater` (NEW).
- `src/SpecScribe/SiteNav.cs` — add `HowToReadOutputPath` constant + the Explore Key Views `QuickLinks` entry in `Build` (UPDATE).
- `src/SpecScribe/SiteGenerator.cs` — add `WriteHowToRead(nav)` beside `WriteAbout`/`WriteDiagnostics`, call it in `GenerateAll`, and wire `AbbreviationExpander.Expand(..., _module.Glossary)` into `ApplyReferenceLinks` after the existing linkifiers (UPDATE).
- `src/SpecScribe/assets/specscribe.css` — `abbr[title]` affordance + any how-to-read section styling (reuse `.info-page`/`.chart-panel`) (UPDATE).
- Tests: new `AbbreviationExpanderTests.cs`, `ModuleContext`/glossary assertions, a `SiteGenerator*` how-to-read generation test; regenerate golden fingerprints across `SiteGenerator*`/webview/SPA suites (UPDATE/NEW).

### UPDATE files — current state & what must be preserved

- **`ModuleContext`** ([ModuleContext.cs:56-116](src/SpecScribe/ModuleContext.cs)): a class with `required` members `Module`, `Commands`, `Docs`; `None` is the undetected fallback ([lines 63-68](src/SpecScribe/ModuleContext.cs)); `DocsFor`/`GlossaryFor` are pure per-module lookups; `Detect` never throws and returns `None` on any failure. **Preserve** `None`'s validity (every consumer relies on it), the `required`-member contract, and the never-throw `Detect`. Add `Glossary` as one more `required` member sourced from `GlossaryFor` — set it on **every** construction site so nothing breaks the `required` contract.
- **`ApplyReferenceLinks`** ([SiteGenerator.cs:1521-1543](src/SpecScribe/SiteGenerator.cs)): a whole-page pass that runs `RequirementLinkifier` then `StoryEpicLinkifier`, each a no-op until its model exists, each anchor-safe. **Preserve** the order (linkifiers first → they create the anchors the expander then skips) and the null-guarded no-op posture. Append the expander as a third stage guarded on a non-empty glossary.
- **`SiteNav`** ([SiteNav.cs:8-56](src/SpecScribe/SiteNav.cs)): page-path constants + `Items` (top nav) + `QuickLinks` (Explore Key Views superset); About/Diagnostics are deliberately **not** in `Items` (footer/info-page convention). **Preserve** the Items/QuickLinks split and the info-page convention — how-to-read goes in `QuickLinks` only.
- **`SiteGenerator.GenerateAll`** ([SiteGenerator.cs:238-265](src/SpecScribe/SiteGenerator.cs)): About/Diagnostics are written **last** (after every phase appends events) so nothing self-references; both always written so their links never 404. **Preserve** that ordering; add `WriteHowToRead` in the same always-written cluster.
- **`AboutTemplater.RenderPage`** ([AboutTemplater.cs:81-142](src/SpecScribe/AboutTemplater.cs)): the exact chromeless synthesized-page recipe (`RenderHeadOpen` → `RenderNavBar` → `RenderBreadcrumb` → `<main class="info-page">` → `<section class="chart-panel">` → `RenderFooter`). **Mirror it** — do not invent a new page shell.

### Testing standards

- xUnit; the rewriter + `GlossaryFor` are pure functions — unit-test `AbbreviationExpander.Expand` and `GlossaryFor` directly against strings (the [RequirementLinkifier]/[ChartsTests] style). Generation-level page tests use the temp-root fixture pattern (`Directory.CreateTempSubdirectory`, `IDisposable`) as in `SiteGenerator*` tests.
- Assert both **presence** (first-use `<abbr>` wraps, glossary `<dl>` present, page linked from Home) and **absence** (later occurrences plain, no wrapping inside anchors/code, empty-glossary section omitted, undetected-framework path byte-unchanged) — the AC1/AC2 pair.
- Regenerate `GoldenContentFingerprint` deliberately and diff to prove the change is abbr-wrapping + the new page only ([see golden-diff-normalization-gotchas] for the footer-clock / `?v=` / subtitle normalizations the harness applies). Confirm the baseline is green before starting so you don't inherit unrelated red.

### Out of scope (do not build)

- No date-format unification / ADR-listing dates (Story 10.4, feedback T7).
- No document-rendering legibility — wiki-links, references appendix, collapsible TOC, retired-item collapse (Story 10.5).
- No re-architecture of `BmadCommands` caption sourcing (see AC2 scope decision) — command *names* already come from the per-module catalog; do not move the caption sentences.
- No nav grouping (Story 10.1, still `ready-for-dev`) — register how-to-read on the current flat nav's `QuickLinks`.
- No chart metadata (Story 10.2).
- No new command-caption content on story pages (the captions already render) and no VS Code walkthrough (that is R1.4 / Story 16.5).
- No glossary popover/tooltip system beyond native `<abbr title>` — T6 explicitly says "`<abbr>` is enough".

### Project Structure Notes

- Output dir is `SpecScribeOutput` (never `docs/live`) — [see generate-output-dir-is-specscribeoutput].
- `src/SpecScribe/assets/specscribe.css` is the styling source of truth; `docs/live/specscribe.css` is generated output and untracked — never edit the generated copy.
- If working in a git worktree, target the worktree path (main has a background auto-committer) — [see worktree-edits-must-target-worktree-path].

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 10.3: Glossary and In-Place Vocabulary] — the two ACs; Epic 10 FR27/28/29, UX-DR25/27/28/29/30, NFR8.
- [Source: docs/Epic3UXFeedback.md#T6] — "The portal assumes BMAD fluency"; `<abbr>` on first use; "How to read this portal" page linked from Home's Explore Key Views; command captions.
- [Source: docs/MissingFeatures.md#D1] — Glossary / "How to read" page NET-NEW; acronym expansion + command caption + orientation page. [Source: docs/MissingFeatures.md#D2] — suggested reading order EXTENDS.
- [Source: docs/UserJourneys.md#Journey 5] — the onboarding persona + the Readme → PRD → Architecture/ADRs → Epics → sprint reading order; "vocabulary either self-explanatory or defined in place".
- [Source: src/SpecScribe/ModuleContext.cs] — `ModuleDoc`/`DocsFor`, `CommandCatalog`, `ModuleContext.None`, the per-module single-source-of-truth idiom (the seam to extend).
- [Source: src/SpecScribe/RequirementLinkifier.cs] — the anchor-split whole-page rewriter pattern the expander mirrors (and the `\b(FR|NFR)(\d+)\b` numbered-only gap the bare-acronym expander fills).
- [Source: src/SpecScribe/SiteGenerator.cs#ApplyReferenceLinks] — the per-page post-process pipeline to append the expander to. [Source: src/SpecScribe/SiteGenerator.cs#WriteAbout] — the always-written synthesized-page pattern + the direct-write (bypass ApplyReferenceLinks) convention.
- [Source: src/SpecScribe/AboutTemplater.cs] — the standalone info-page shell to mirror for HowToReadTemplater.
- [Source: src/SpecScribe/SiteNav.cs] — page-path constants, `Items` vs `QuickLinks`, the info-page-not-in-top-nav convention.
- [Source: src/SpecScribe/BmadCommands.cs] — command captions currently hard-coded in the BMad-specific command surface (the AC2 caption-scope note).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no blocking failures. Full `dotnet test` suite green throughout (1588/1588 on completion).

### Completion Notes List

- Ultimate context engine analysis completed — comprehensive developer guide created.
- **Scope decision (AC2 command captions):** per the story's own Design Direction note, `BmadCommands.cs` caption sentences were NOT re-architected to be adapter-supplied — command *names* already came from the per-module `CommandCatalog`, and the new AC2 deliverable (the glossary) is now adapter-supplied. Recorded here as a deliberate scoping call, not an omission.
- **Deviation (Task 4, AC1 reachability from Home):** Story 10.1 (grouped nav) shipped ahead of this story (status `review` in sprint-status.yaml, code already on `main` at dev-story start) and retired the flat "Explore Key Views" dashboard-body grid the story's Design Direction was written against — `HtmlRenderAdapter.Dashboard.cs` no longer renders `nav.QuickLinks` at all on Home; that data now only surfaces as a per-page pill band on *non*-Home pages (`AppendKeyViewsBand`). Verified this via source inspection before implementing, exactly as the story's own "Sequencing note" anticipated ("if 10.1 lands first, the how-to-read registration rides its Project group with no rework"). Implemented both: (1) `QuickLinks` entry (feeds the pill band on other pages) AND (2) a `Project` nav-group entry, leading the group — so a visitor on Home reaches the page via the shared top nav bar, satisfying AC1's "When I open Home" clause. Added a matching compass-rose icon (`Icons.ForConcept`) since the nav/pill rendering requires one for every emitted label.
- Verified end-to-end on the real SpecScribe repo (`dotnet run -- generate --deep-git`, `webview`, `generate --spa`): `how-to-read.html` renders the reading order (Readme → PRD → Architecture → ADRs → Epics → Sprint) and the full BMad Method glossary; `epics.html` shows exactly one first-use `<abbr title="Functional Requirement">FR</abbr>` (no double-wrap on the later "FR Coverage Map"-style bare mentions); nav/code/anchor text untouched; all three delivery surfaces (static HTML, `webview` JSON payload, `--spa`) carry the same page and expansions.
- Regenerated `SiteGeneratorAdapterTests`'s golden content-fingerprint constant and output-inventory list (new `how-to-read.html` page + every page's nav/CSS delta), and updated the `SiteNavTests`/`RenderParityTests`/`IconsTests` fixtures the new "How to read this portal" nav entry and icon touched. Full suite: 1588/1588 green.

### File List

- `src/SpecScribe/ModuleContext.cs` — added `GlossaryTerm` record, `ModuleContext.Glossary` member, `GlossaryFor(BmadModule)` (BMad Method + Game Dev Studio vocabularies), wired into `None` and `BuildContext` (UPDATE)
- `src/SpecScribe/AbbreviationExpander.cs` — new first-use `<abbr>` expander, protected-span-split mirroring `RequirementLinkifier` (NEW)
- `src/SpecScribe/HowToReadTemplater.cs` — the "How to read this portal" orientation page, modeled on `AboutTemplater` (NEW)
- `src/SpecScribe/SiteNav.cs` — added `HowToReadOutputPath` constant + Project-group nav entry + `QuickLinks` entry (UPDATE)
- `src/SpecScribe/SiteGenerator.cs` — added `WriteHowToRead(nav)`, wired into `GenerateAll`, wired `AbbreviationExpander.Expand` into `ApplyReferenceLinks` (UPDATE)
- `src/SpecScribe/Icons.cs` — added a compass-rose glyph for the "How to read this portal" concept key (UPDATE)
- `src/SpecScribe/assets/specscribe.css` — `abbr[title]` affordance + `.howtoread-panel`/`.howtoread-order`/`.howtoread-glossary` styles (UPDATE)
- `tests/SpecScribe.Tests/AbbreviationExpanderTests.cs` — unit tests for the expander (NEW)
- `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` — generation-level tests for the how-to-read page, reading order, glossary, nav reachability, and undetected-module degradation (NEW)
- `tests/SpecScribe.Tests/ModuleContextTests.cs` — added `GlossaryFor`/`Detect` glossary assertions (UPDATE)
- `tests/SpecScribe.Tests/SiteNavTests.cs` — updated nav `Items` expectations for the new Project-group entry (UPDATE)
- `tests/SpecScribe.Tests/RenderParityTests.cs` — updated nav-fact expectations for the new entry (UPDATE)
- `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — regenerated golden content-fingerprint + output-inventory (UPDATE)

## Change Log

- 2026-07-18: dev-story — implemented the adapter-supplied glossary seam (`ModuleContext.Glossary`/`GlossaryFor`), the first-use `<abbr>` expander (`AbbreviationExpander`), and the "How to read this portal" orientation page (`HowToReadTemplater` + `SiteNav.HowToReadOutputPath`), reachable from Home via the Project nav group (deviated from the story's Home-dashboard-card design because Story 10.1 had already retired that grid — see Dev Agent Record). 1588/1588 tests green; verified end-to-end on the real repo across static HTML, webview, and `--spa`. Status → review.
