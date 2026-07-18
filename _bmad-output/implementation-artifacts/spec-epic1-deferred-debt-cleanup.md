---
title: 'Epic 1 deferred — linkifier case/digit pins, nav scroll-margin unify, close settled items'
type: 'bugfix'
created: '2026-07-18'
status: 'done'
baseline_commit: '8b54167778de81144a2e907a05ff4293a70a04ba'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open Epic 1 deferred items remain: (1) `RequirementLinkifier.RefPattern` is case-sensitive so lowercase `fr6`/`nfr2` never linkify despite case-insensitive `ById`; (2) multi-digit token boundaries (`FR6` vs `FR60`) have no regression pin; (3) sticky-nav `scroll-margin-top` still hardcodes `5rem`/`4.5rem` beside `var(--nav-offset)`; (4) Story 1.3 scope-bleed note about Pages workflow + README is historical noise; (5) detail-page `<h1>` “unescaped title” note is stale after Story 6.2 `TitleHtml` / `RenderInline` projection.

**Approach:** Make linkify case-insensitive and pin digit boundaries with tests; unify remaining scroll-margin hardcodes onto `--nav-offset` (including `.code-line`); close item 4 as accepted historical bleed and item 5 as superseded (do not wrap `TitleHtml` in `PathUtil.Html`); reconcile `deferred-work.md`.

## Boundaries & Constraints

**Always:**
- Lowercase/mixed-case known FR/NFR/UX-DR tokens link; preserve authored casing in link text; hrefs stay on `req.Slug`.
- Multi-digit IDs never partial-match a shorter/longer known id (`FR6` ↛ `FR60` and reverse).
- Sticky-nav scroll targets that clear the nav use only `var(--nav-offset)` (today `6.5rem` / mobile `5.5rem`) — no parallel rem hardcodes for that intent.
- Item 4: ledger close only — no revert of `.github/workflows/publish-docs-live-pages.yml` or README Pages docs.
- Item 5: titles remain opaque `TitleHtml` from `MarkdownConverter.RenderInline`; closing supersedes the deferred “wrap in `PathUtil.Html`” prescription.
- Mark all five deferred bullets resolved under their existing story-1-2 / 1-3 / 1-4 review sections with this spec key.

**Ask First:**
- Changing Markdig HTML-passthrough policy for titles (encode raw HTML tags while keeping markdown) — out of this cleanup unless renegotiated.
- Raising `--nav-offset` itself or redesigning sticky nav height.

**Never:**
- Wrap epic/story `TitleHtml` in `PathUtil.Html` (double-escapes markdown titles).
- Hyphenated ids (`FR-1`), bare `DR{n}`, or rewriting inside protected spans/anchors.
- Reverting or splitting the long-merged Pages/README commits for item 4.
- Changing linkify beyond case-flag + tests; no CSS token redesign.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Lowercase known id | text `see fr6`, model has FR6 | link; visible text stays `fr6`; href uses slug | N/A |
| Mixed-case known id | `Nfr2` / `Ux-Dr1` when known | link; casing preserved in text | N/A |
| Unknown lowercase | `fr99` unknown | unchanged plain text | N/A |
| Longer digit id | only FR6 known; text `FR60` | no link | N/A |
| Shorter digit id | only FR60 known; text `FR6` | no link | N/A |
| Scroll-margin unify | `.ac-criterion`, `.req-index .section-divider[id]`, `.code-line` | `scroll-margin-top: var(--nav-offset)` | N/A |
| Title markdown | epic/story title with `**bold**` | `<h1>` contains `<strong>`, not `&lt;strong&gt;` | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/RequirementLinkifier.cs:26` -- `RefPattern`; add `RegexOptions.IgnoreCase` (keep `Compiled`; UX-DR arm unchanged)
- `src/SpecScribe/RequirementsModel.cs:92-94` -- `ById` already `OrdinalIgnoreCase` (must stay)
- `tests/SpecScribe.Tests/LinkifierTests.cs` -- add case + multi-digit pins beside `Linkify_DoesNotMatchPartialTokens`
- `src/SpecScribe/assets/specscribe.css` -- `--nav-offset` (~36, mobile ~4524); hardcodes at `.code-line` (~1027), `.ac-criterion` (~2279), `.req-index .section-divider[id]` (~4347)
- `src/SpecScribe/HtmlRenderAdapter.Epics.cs` / `EpicsViewBuilder.cs` / `EpicsParser.cs` -- `TitleHtml` via `RenderInline` (item 5 close evidence; no emit change)
- `.github/workflows/publish-docs-live-pages.yml`, `README.md` -- item 4 evidence only
- `_bmad-output/implementation-artifacts/deferred-work.md` -- five Epic 1 open bullets

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/RequirementLinkifier.cs` -- Add `IgnoreCase` to `RefPattern` options so FR/NFR/UX-DR matching aligns with `ById`
- [x] `tests/SpecScribe.Tests/LinkifierTests.cs` -- Pin lowercase/mixed-case known links; pin `FR60`≠`FR6` both directions; keep existing partial-token coverage
- [x] `src/SpecScribe/assets/specscribe.css` -- Replace the three sticky-nav `scroll-margin-top` rem hardcodes with `var(--nav-offset)`
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Resolve all five bullets with this spec key: (1) IgnoreCase+tests, (2) digit pin, (3) CSS unify, (4) accepted historical bleed, (5) superseded by TitleHtml/RenderInline — not PathUtil.Html
- [x] Optional pin: assert epic/story `<h1>` still carries inline markdown HTML (not double-escaped) if a cheap existing RenderEpic/RenderStory fixture can host it without new golden sprawl
- [x] `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` -- Refresh golden content fingerprint after CSS + deferred-ledger render changes

**Acceptance Criteria:**
- Given a known requirement and lowercase prose reference, when linkify runs, then a link is emitted with authored casing preserved
- Given only `FR6` (resp. only `FR60`) known, when prose contains the other id, then that token stays unlinked
- Given AC / req-index divider / code-line in-page anchors, when CSS is inspected, then their `scroll-margin-top` is `var(--nav-offset)` only
- Given the five deferred bullets, when this ships, then each is marked resolved with audit trail (items 4–5 closed without product code churn beyond optional title pin)

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~LinkifierTests"` -- expected: all pass including new case/digit pins
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green
- `rg "scroll-margin-top:" src/SpecScribe/assets/specscribe.css` -- expected: no `5rem`/`4.5rem` leftovers for sticky-nav targets; remaining uses are `var(--nav-offset)`

## Suggested Review Order

**Case-insensitive linkify**

- Entry point: IgnoreCase + CultureInvariant on RefPattern; casing preserved via m.Value
  [`RequirementLinkifier.cs:28`](../../src/SpecScribe/RequirementLinkifier.cs#L28)

- Lowercase/mixed-case known ids link; unknown lowercase stays plain
  [`LinkifierTests.cs:74`](../../tests/SpecScribe.Tests/LinkifierTests.cs#L74)

- FR6/FR60 digit boundaries + coexistence + lowercase skipId
  [`LinkifierTests.cs:97`](../../tests/SpecScribe.Tests/LinkifierTests.cs#L97)

**Sticky-nav scroll-margin unify**

- `.code-line`, `.ac-criterion`, req-index dividers all use `--nav-offset`
  [`specscribe.css:1027`](../../src/SpecScribe/assets/specscribe.css#L1027)

**TitleHtml / deferred ledger**

- Pin: TitleHtml in `<h1>` must not be PathUtil-double-escaped
  [`HtmlRenderAdapterTests.cs:242`](../../tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs#L242)

- Five Epic 1 bullets closed (items 4–5 process/superseded)
  [`deferred-work.md:283`](deferred-work.md#L283)

- Golden fingerprint refreshed for CSS + ledger render bytes
  [`SiteGeneratorAdapterTests.cs:473`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L473)
