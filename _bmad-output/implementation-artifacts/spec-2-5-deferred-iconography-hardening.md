---
title: 'Story 2.5 deferred — iconography hardening (ampersand, Badge escape, tests)'
type: 'bugfix'
created: '2026-07-18T15:37:15-04:00'
status: 'done'
baseline_commit: 'bd132f70893cfbe0005a2b730ed6476495a36ded'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Five open Story 2.5 deferred items weaken iconography guarantees: an orphaned ampersand concept key that once dual-represented with hand-escaped HTML; Badge interpolates `cssClass` unescaped; badge tests no longer prove icon+label share one span; glyph color tests mostly ban `#` rather than requiring `currentColor`; and nothing reverse-checks that every label emitters pass to `Icons.ForConcept` has a curated glyph.

**Approach:** Close all five under this spec: remove the orphaned Direct & Quick-Dev concept (call site already gone; keep IconKey≠DisplayLabel / `PathUtil.Html` as the ampersand rule); HTML-escape Badge `cssClass`; tighten badge + glyph assertions; add an emitter-sourced reverse-coverage test; mark the five deferred bullets RESOLVED.

## Boundaries & Constraints

**Always:**
- Ampersand item: delete unused `"Direct & Quick-Dev Work"` `Icons.ForConcept` arm + its InlineData; fix stale DashboardView doc that still names that band. Display text with `&` must come from `PathUtil.Html(sharedLabel)` or IconKey≠DisplayLabel (existing nav pattern) — never a second hand-typed `&amp;` literal beside the icon key.
- `StatusStyles.Badge` escapes `cssClass` with `PathUtil.Html` (both overloads that interpolate it). Closed status vocabulary callers keep working unchanged for safe tokens.
- Badge co-location: at least the weakened Requirements coverage-card assert (`status-badge drafted` + `>Drafted<`) and the StatusStyles unit Badge asserts prove icon+label live in one span (prefer `Assert.Contains(StatusStyles.Badge(...), …)` or a small helper). Do not boil the ocean across every site-generator smoke check.
- Glyph well-formedness: positive `currentColor` wiring plus ban on `#`, `rgb(`, `hsl(`, and common named fills (`black`/`white`/etc.) in SVG markup. `.ss-icon` CSS rule stays hex-free; strengthen if easy without inventing color properties.
- Reverse coverage: MemberData/theory sourced from real emitters (SiteNav labels, ModuleContext well-known doc labels, ArtifactCoverage ConceptIconKeys, work-mode pills, evidence keys, nav group triggers) — not a second hand-maintained InlineData twin of the switch.
- Mark all five story-2-5 deferred bullets RESOLVED with this spec key.

**Ask First:**
- Reintroducing a home-index "Direct & Quick-Dev Work" band or inventing new concept glyphs beyond cleanup.
- Changing Badge to whitelist-only reject unknown cssClass (escape is enough unless you hit a reason to harden further).

**Never:**
- Rewriting nav/key-view architecture or StatusStyles stage vocabulary.
- Expanding badge co-location fixes into unrelated chart/SVG text asserts.
- Leaving orphaned Direct & Quick-Dev arm "just in case."

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Safe Badge class | `Badge("done", "Done")` | Class attr contains escaped `done`; glyph+label same span | N/A |
| Hostile cssClass | `Badge("done\" onmouseover=\"x", "X")` | `"` / dangerous chars encoded; no attribute breakout | N/A |
| Orphan concept key | `ForConcept("Direct & Quick-Dev Work")` | Empty (unknown) after removal | N/A |
| Ampersand display | IconKey `Epics` + display `Epics & Stories` | Glyph from key; `&` only via `PathUtil.Html` | N/A |
| Named-color glyph | Hypothetical `fill="black"` SVG | AssertWellFormedIcon fails | N/A |
| New nav label without glyph | Emitter list includes uncurated label | Reverse-coverage test fails | N/A |
| Unknown concept | `ForConcept("Some Unrecognized Concept")` | Still empty (unchanged) | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/Icons.cs` -- remove `"Direct & Quick-Dev Work"` arm (~:74); ForConcept switch is reverse-test target
- `src/SpecScribe/StatusStyles.cs` -- `Badge` / 3-arg overload (~:261–264): escape `cssClass`
- `src/SpecScribe/DashboardView.cs` -- stale Direct & Quick-Dev band doc (~:91)
- `src/SpecScribe/HtmlRenderAdapter.cs` -- IconKey + `PathUtil.Html(DisplayLabel)` pattern (~:116); work-mode ForConcept keys (~:211–218); QuickLinkTitle Epics remap (~:406–410)
- `src/SpecScribe/SiteNav.cs` -- emitter of nav/quick-link labels for reverse test
- `src/SpecScribe/ModuleContext.cs` -- well-known doc labels
- `src/SpecScribe/ArtifactCoverage.cs` -- `ConceptIconKey` family list
- `tests/SpecScribe.Tests/IconsTests.cs` -- AssertWellFormedIcon + forward InlineData + new reverse test
- `tests/SpecScribe.Tests/StatusStylesTests.cs` -- Badge unit asserts + cssClass escape case
- `tests/SpecScribe.Tests/RequirementsAndProgressTests.cs` -- weakened `>Drafted<` co-location (~:906–907)
- `tests/SpecScribe.Tests/StylesheetTests.cs` -- `.ss-icon` no-hex rule (~:219–228)
- `tests/SpecScribe.Tests/HtmlRenderAdapterTests.cs` -- strong Badge whole-string model (~:275)
- `_bmad-output/implementation-artifacts/deferred-work.md` -- story-2-5 section (~:329–335)

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/Icons.cs` + `DashboardView.cs` + `IconsTests.cs` -- Drop orphan Direct & Quick-Dev arm/InlineData; fix stale doc; strengthen AssertWellFormedIcon; add emitter-sourced reverse-coverage test
- [x] `src/SpecScribe/StatusStyles.cs` + `StatusStylesTests.cs` -- Escape Badge cssClass; pin escape + same-span Badge asserts
- [x] `tests/SpecScribe.Tests/RequirementsAndProgressTests.cs` (+ StylesheetTests if trivial) -- Restore badge co-location for the drafted coverage-card case; keep `.ss-icon` hex-free with any easy positive assert
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- Strike-through + RESOLVED all five story-2-5 bullets citing this spec
- [x] `tests/SpecScribe.Tests/*` -- Cover I/O matrix rows (hostile cssClass, orphan key empty, reverse emitters, named-color ban)

**Acceptance Criteria:**
- Given the five story-2-5 deferred bullets, when this work ships, then each is marked RESOLVED under the existing review section with this spec key and no open duplicate remains.
- Given Badge receives a cssClass containing `"`, when rendered, then the attribute does not break out and the label+icon remain in one `status-badge` span.
- Given every label/key collected from the agreed emitters, when `Icons.ForConcept` is called, then each returns a well-formed glyph (non-empty, a11y attrs, currentColor, no hard-coded color literals).
- Given the requirements coverage-card drafted badge, when the page HTML is asserted, then one check proves class+icon+label share the badge span (not two independent substring hits).

## Spec Change Log

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~IconsTests|FullyQualifiedName~StatusStylesTests|FullyQualifiedName~RequirementsAndProgressTests"` -- expected: pass including new escape/reverse/co-location cases
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: full suite green

**Manual checks (if no CLI):**
- Confirm story-2-5 section in `deferred-work.md` shows five struck-through RESOLVED bullets citing `spec-2-5-deferred-iconography-hardening`

## Suggested Review Order

**Badge escape**

- Escape cssClass beside tip/label so attribute injection cannot break out
  [`StatusStyles.cs:264`](../../src/SpecScribe/StatusStyles.cs#L264)

- Full-string hostile-class pin (encoded quotes, same-span label)
  [`StatusStylesTests.cs:239`](../../tests/SpecScribe.Tests/StatusStylesTests.cs#L239)

**Orphan ampersand key**

- Remove unused Direct & Quick-Dev ForConcept arm
  [`Icons.cs:71`](../../src/SpecScribe/Icons.cs#L71)

- Stale home-band doc no longer names that dual-rep label
  [`DashboardView.cs:91`](../../src/SpecScribe/DashboardView.cs#L91)

**Glyph + reverse coverage**

- AssertWellFormedIcon requires currentColor and bans color literals
  [`IconsTests.cs:230`](../../tests/SpecScribe.Tests/IconsTests.cs#L230)

- Emitter-sourced MemberData (SiteNav / ModuleContext / ArtifactCoverage / pills)
  [`IconsTests.cs:55`](../../tests/SpecScribe.Tests/IconsTests.cs#L55)

**Co-location assert**

- Coverage-card drafted badge uses full StatusStyles.Badge markup
  [`RequirementsAndProgressTests.cs:907`](../../tests/SpecScribe.Tests/RequirementsAndProgressTests.cs#L907)

**Ledger**

- Five story-2-5 bullets marked RESOLVED under this spec key
  [`deferred-work.md:329`](deferred-work.md#L329)
