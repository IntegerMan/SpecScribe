---
title: 'Code page: split Relationships and History into their own iconed tabs'
type: 'feature'
created: '2026-07-13'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '1a2b7f5b4a0f59bb6d07e5af48819f65a2459e85'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The in-portal code file page has two pure-CSS tabs — *Insights* and *Code* — and the *Insights* tab crams three distinct things into one view: the "Referenced by" reference graph, the git-signal coverage panels (frequency, contributors, "Often changed with"), and the change-history table. The graph and the history deserve to be first-class, directly-reachable views, and the tab strip carries no icons.

**Approach:** Grow the tab strip to four tabs — **Insights** (default) | **Relationships** | **History** | **Code** — by lifting the reference graph out into a **Relationships** tab and the change-history table out into a **History** tab. Give every tab an inline-SVG icon paired with its existing text label. Per owner decision, the "Often changed with" (coupled files) panel appears in **both** Insights and Relationships. Tabs with no content are omitted (an uncited file with no deep-git data still shows only Code, full-width, exactly as today).

## Boundaries & Constraints

**Always:**
- Pure CSS, no JavaScript — radio `<fieldset>` + `:has(:checked)` toggling.
- Preserve the locked `#L{n}` deep-link: a targeted source line still forces the **Code** panel forward and marks its tab active, regardless of the default-checked tab.
- Default-checked tab = first *present* tab in order Insights → Relationships → History → Code.
- Tab label text always present; icon is decorative (`aria-hidden`, `focusable="false"`, `currentColor`), routed through `Icons.cs`.
- Keep escaping of names/subjects/paths/hashes, and the `code-tab--source` / `code-tabpanel--source` class names (deep-link CSS keys).

**Ask First:** None — tab order (Insights-leads) and coupled-files placement (both) are owner-resolved and frozen above.

**Never:** touch `RenderPlaceholder` (sidebar layout, out of scope); alter source-line markup / `id="L{n}"` / `data-ln` / numbering; make icon-only tabs; change `Charts.ReferenceGraph` internals or the `FileInsight` data shape.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full data | refs + populated `FileInsight` (freq/contributors/coupled/history) | 4 tabs. Insights checked. Insights = freq+contributors+coupled; Relationships = graph+coupled; History = history table; Code = source. | N/A |
| Refs only, no insight | referencedBy set, `insight == null` | 2 tabs: Relationships (checked) + Code. No Insights/History tabs. Byte-identical to null-insight run. | N/A |
| Insight, no refs | `insight` set, `referencedBy` empty | Tabs: Insights (checked), Relationships (only if coupled present, holding just the coupled card), History (if history), Code. | N/A |
| Uncited, no insight, no external | neither refs nor insight | No tab chrome — source renders full-width exactly as today. | N/A |
| Deep link `#L42` | any tabbed page loaded at `code/…#L42` | Code panel forced forward, Code tab active, target line reachable, despite Insights being default-checked. | N/A |
| Coupled files, one resolvable | coupled = resolvable + unresolvable path | Resolvable → `<a>` to code page (in both Insights and Relationships); unresolvable → plain `<code>`, never a dead link. | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/CodeFileTemplater.cs` -- owns the code page; `RenderPage`, `AppendTabs`, `BuildInsightsPanel`, `BuildCoverageSection`, `BuildRelationshipsCard`. Primary edit site.
- `src/SpecScribe/Icons.cs` -- single source of inline-SVG icons; add tab glyphs here (`Svg()` helper, decorative shell).
- `src/SpecScribe/assets/specscribe.css` -- `.code-tabs` / `.code-tab` / `.code-tabpanel` rules (~L594–681); extend panel-toggle + deep-link overrides to 4 modifiers.
- `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` -- unit coverage for the templater; extend with 4-tab assertions.
- `tests/SpecScribe.Tests/SiteGeneratorCodeInsightsTests.cs` -- integration coverage (all `Contains`-based; survives reorg, add a tab-structure check).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Icons.cs` -- add a dedicated `ForCodeTab(string label)` returning glyphs for "Insights" (lightbulb), "Relationships" (node-link/share), "History" (clock-with-rewind), "Code" (angle brackets); unknown → empty string, via the existing `Svg()` shell.
- [x] `src/SpecScribe/CodeFileTemplater.cs` -- decompose `BuildCoverageSection` into reusable pieces: `BuildInsightsPanel` (Advanced-coverage `<section class="code-insights">` = frequency + contributors + coupled, no graph/history), `BuildRelationshipsPanel` (graph card + coupled card), `BuildHistoryPanel` (the `code-history-table` section). Extract `BuildCoupledCard` so the coupled panel renders in both Insights and Relationships. Rewrite `AppendTabs` to be list-driven over a `CodeTab(Mod,Label,Icon,Panel)` record: emit only non-empty tabs, mark the first as `checked`, prepend the icon before the label `<span>`. `RenderPage` assembles the tab list in order Insights, Relationships, History, Code (Code always present); when only Code exists, render source full-width with no tab chrome.
- [x] `src/SpecScribe/assets/specscribe.css` -- extend the panel-show rule to all four `--insights/--relationships/--history/--source` modifiers; add `.code-tab .ss-icon` sizing if needed; generalize the deep-link override to hide every `.code-tabpanel` and re-show `.code-tabpanel--source` (keep equal specificity + later source order so a live `#L{n}` still wins over the checked default). Update the leading block comment from "two views" to four.
- [x] `tests/SpecScribe.Tests/CodeFileTemplaterTests.cs` -- add tests covering the I/O matrix: 4-tab presence + labels + `ss-icon` per tab; Insights default-checked; graph lives in Relationships (not Insights); history table lives in History (not Insights); coupled card appears in both Insights and Relationships; refs-only → Relationships-checked 2-tab; uncited+no-insight → no tab chrome; deep-link modifiers (`code-tab--source`, `code-tabpanel--source`) intact.

**Acceptance Criteria:**
- Given a code page with references and a populated `FileInsight`, when rendered, then four tabs appear (Insights, Relationships, History, Code) each with an inline `ss-icon` and a text label, with Insights checked by default.
- Given the same page, when inspecting panels, then the reference graph is inside the Relationships panel only, the change-history table is inside the History panel only, and the "Often changed with" coupled card appears in both the Insights and Relationships panels.
- Given a page loaded at `code/…#L42`, when it renders, then the Code panel is shown and the Code tab reads active even though Insights is the default-checked tab.
- Given an uncited file with no deep-git insight, when rendered, then no tab chrome appears and the source spans full width (unchanged from today).
- Given a full `dotnet test`, when the suite runs, then all existing code-page tests still pass and the new tab tests pass.

## Design Notes

Tab markup, now list-driven and iconed: `<label class="code-tab code-tab--relationships"><input type="radio" class="code-tab-input" name="{group}"><svg class="ss-icon" …/><span>Relationships</span></label>`.

Deep-link CSS generalization (replaces the per-`--insights` hide; equal specificity to the checked rules, placed after them so a live `#L{n}` wins):
```css
.code-tabs:has(.code-tabpanel--source .code-line:target) .code-tabpanel { display: none; }
.code-tabs:has(.code-tabpanel--source .code-line:target) .code-tabpanel--source { display: block; }
```
Known in-app preview `:has(:target)` quirk: the active-tab pill can look stale on deep-link (panels correct; real Chrome fine) — verify deep-link via markup/tests, not the preview pill.

## Verification

**Commands:**
- `dotnet build` -- expected: clean build, no warnings introduced.
- `dotnet test` -- expected: all tests green, including the new 4-tab cases.

**Manual checks:**
- Generate a site with `--deep-git` over this repo, open a well-cited `.cs` code page: confirm four iconed tabs, Insights default, graph under Relationships, history under History, coupled card in both Insights and Relationships, and that appending `#L{n}` jumps to the Code view.

## Suggested Review Order

**Tab assembly (start here)**

- Entry point: the fixed-order tab list — empty panels drop out, first survivor is default-checked.
  [`CodeFileTemplater.cs:72`](../../src/SpecScribe/CodeFileTemplater.cs#L72)

- The tab record whose `Mod` keys both the `.code-tab--{Mod}` label and `.code-tabpanel--{Mod}` panel (`source` kept for deep-link CSS).
  [`CodeFileTemplater.cs:97`](../../src/SpecScribe/CodeFileTemplater.cs#L97)

- List-driven tab shell: first tab `checked`, icon before each label.
  [`CodeFileTemplater.cs:134`](../../src/SpecScribe/CodeFileTemplater.cs#L134)

**Panel content split**

- Insights = frequency + contributors + coupled card (no graph, no history).
  [`CodeFileTemplater.cs:164`](../../src/SpecScribe/CodeFileTemplater.cs#L164)

- Relationships = graph + the same coupled card (owner: coupled in both tabs).
  [`CodeFileTemplater.cs:222`](../../src/SpecScribe/CodeFileTemplater.cs#L222)

- Coupled card factored out so it stays identical in both tabs.
  [`CodeFileTemplater.cs:243`](../../src/SpecScribe/CodeFileTemplater.cs#L243)

- History = the standalone change-history table (heading bumped h3→h2).
  [`CodeFileTemplater.cs:270`](../../src/SpecScribe/CodeFileTemplater.cs#L270)

**Icons & CSS**

- Four decorative tab glyphs; unknown label → empty string.
  [`Icons.cs:78`](../../src/SpecScribe/Icons.cs#L78)

- Four-modifier panel-show rule.
  [`specscribe.css:669`](../../src/SpecScribe/assets/specscribe.css#L669)

- Generalized `#L{n}` deep-link override: hide every panel, re-show `--source` (equal specificity, later source order).
  [`specscribe.css:680`](../../src/SpecScribe/assets/specscribe.css#L680)

- History heading restyled to match sibling panel headings after the h3→h2 move (review patch).
  [`specscribe.css:984`](../../src/SpecScribe/assets/specscribe.css#L984)

**Tests**

- Four iconed tabs, Insights default; graph in Relationships only; coupled in both.
  [`CodeFileTemplaterTests.cs:348`](../../tests/SpecScribe.Tests/CodeFileTemplaterTests.cs#L348)
