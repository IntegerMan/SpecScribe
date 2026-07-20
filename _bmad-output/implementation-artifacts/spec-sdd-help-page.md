---
title: 'Spec-Driven Development help page'
type: 'feature'
created: '2026-07-20'
status: 'done'
baseline_commit: '58d38ba44a07701a71d99351e56c3449b62ccbb5'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Conference demos and first-time visitors lack a single portal page that orients them to Spec-Driven Development: which frameworks SpecScribe understands, common slash-commands to move work forward, and how to install a framework when it is missing.

**Approach:** Rebrand and extend the existing How-to-Read page into **Spec-Driven Development**. Keep the stable output path `how-to-read.html`. Add always-present framework tabs (presence via tab color), a static curated command list and methodology flowchart per supported framework, muted “Coming Soon” stubs for planned frameworks, and hard-coded install docs + one-liner when a supported framework is absent. Preserve the existing reading-order and glossary sections where they still apply.

## Boundaries & Constraints

**Always:**
- Page is written every full generate (same always-written contract as today).
- Keep output path `how-to-read.html`; change H1, nav label, QuickLinks label, breadcrumb, and Icons concept key to **Spec-Driven Development**.
- Framework tabs are always rendered (never omit a tab because the framework is missing).
- Tab color (and text, not color alone) indicates Present / Absent / Coming Soon.
- Supported for v1 presence + content: **BMad Method** and **BMad GDS (Game Dev Studio)**. Detection is independent per tab (both can be Present in one repo).
- Command lists and install one-liners are **static hard-coded** copy on this page (not driven by `module-help.csv`).
- Methodology diagram is a static Mermaid state/flowchart for the BMad Method sequence: brief → PRD → epics/stories → develop → review → retrospective. GDS present panel may use a shorter GDS-oriented static diagram or the same spine with GDS-labeled stages.
- Absent supported framework: muted tab + link to official docs + one-line install command.
- Planned frameworks: muted tab + “Coming Soon” panel only (no fake install guidance).
- Bypass `ApplyReferenceLinks` / abbreviation expansion on this page (existing How-to-Read rule).
- Zero-JS tab switching via CSS radio pattern (new class names; do not reuse `.code-tabs` selectors as-is).
- Accessibility: keyboard-focusable tabs; presence state not conveyed by color alone.

**Ask First:**
- Changing the output filename away from `how-to-read.html`.
- Adding or renaming tab roster entries beyond the README Supported frameworks table.
- Replacing the curated command shortlist with live `module-help.csv` data.
- Display label “Game Design Studio” vs existing product label “Game Dev Studio” (use **Game Dev Studio** unless human overrides).

**Never:**
- Do not implement Spec Kit / GSD / GSD-Pi / Superpowers adapters or detection in this change.
- Do not remove reading-order or glossary; fold them under or below the new SDD content without deleting their contracts.
- Do not break Home/Project nav reachability of this page.
- Do not load Mermaid CDN when the page has no Mermaid block.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Both Method + GDS installed | `_bmad/` has `bmm` and `gds` (manifest and/or `module-help.csv`) | Both tabs Present (active color); each panel shows its static commands + diagram; no install CTA | N/A |
| Method only | `_bmad/bmm` present, no `gds` | Method Present; GDS Absent with docs link + `npx bmad-method install --modules gds` | N/A |
| GDS only | `_bmad/gds` present, no `bmm` | GDS Present; Method Absent with docs link + `npx bmad-method install` | N/A |
| No `_bmad/` | Bare repo / output-only | Method + GDS tabs Absent with install CTAs; planned tabs Coming Soon; reading-order/glossary degrade as today | Page still written |
| Planned framework tab | Always | Muted tab; panel body is “Coming Soon” only | N/A |
| Default tab | Method Present | Method selected | If Method Absent and GDS Present, default to GDS; if both Absent, default to Method |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/HowToReadTemplater.cs` -- rebrand + framework tabs, static command panels, Mermaid block, install CTAs; keep reading-order/glossary appenders
- `src/SpecScribe/SiteNav.cs` -- nav/QuickLinks/Icons label → Spec-Driven Development; keep `HowToReadOutputPath`
- `src/SpecScribe/Icons.cs` -- concept-key string rename with SiteNav
- `src/SpecScribe/ModuleContext.cs` -- add independent Present checks for Method vs GDS (do not rely solely on single-winner `Detect`)
- `src/SpecScribe/Mermaid.cs` -- optional small helper for the static SDD methodology diagram; `Block` + `InitScript` from templater
- `src/SpecScribe/assets/specscribe.css` -- new `.sdd-tabs*` (or similar) Present/Absent/ComingSoon styles modeled on code-tabs radio pattern
- `src/SpecScribe/SiteGenerator.cs` -- pass whatever presence flags the templater needs from `WriteHowToRead`
- `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` -- rebrand + tab presence/absent/coming-soon + install CTA cases
- `tests/SpecScribe.Tests/ModuleContextTests.cs` -- independent Method/GDS presence helpers
- `tests/SpecScribe.Tests/SiteNavTests.cs`, `IconsTests.cs`, `RenderParityTests.cs`, `SiteGeneratorAdapterTests.cs` -- label/fingerprint updates

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/ModuleContext.cs` -- add pure helpers that report Method present and GDS present independently from `_bmad/` (manifest name and/or module-help.csv / skill-prefix signals already used by Detect) -- dual-install and single-module repos both truthfully color tabs
- [x] `src/SpecScribe/HowToReadTemplater.cs` -- rebrand titles/copy to Spec-Driven Development; render always-on framework tabstrip + panels (static commands, Mermaid methodology diagram when Present, install CTA when Absent, Coming Soon when planned); append `Mermaid.InitScript()` only when a Mermaid block is present; keep reading-order + glossary sections
- [x] `src/SpecScribe/Mermaid.cs` -- add a static SDD methodology diagram helper (stateDiagram or flowchart) used by the templater
- [x] `src/SpecScribe/SiteNav.cs` + `Icons.cs` -- rename user-facing label to Spec-Driven Development; keep path constant
- [x] `src/SpecScribe/SiteGenerator.cs` -- wire presence flags into `WriteHowToRead` / `RenderPage`
- [x] `src/SpecScribe/assets/specscribe.css` -- Present (active), Absent (muted), Coming Soon styles; zero-JS radio tab behavior; non-color state cue (badge/text)
- [x] `tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs` (+ ModuleContext/SiteNav/Icons/parity/fingerprint as needed) -- cover I/O matrix rows and rebrand labels
- [x] Hard-code install targets on Absent panels: Method → https://github.com/bmad-code-org/BMAD-METHOD + `npx bmad-method install`; GDS → https://github.com/bmad-code-org/bmad-module-game-dev-studio (or official GDS docs) + `npx bmad-method install --modules gds`

**Acceptance Criteria:**
- Given a full generate, when the site is written, then `how-to-read.html` exists and nav/QuickLinks label it Spec-Driven Development.
- Given Method and/or GDS installed, when the page loads, then corresponding tabs are Present-styled and show a bulleted static command list plus a Mermaid methodology diagram.
- Given a supported framework is missing, when its tab is selected, then the panel shows a docs link and the hard-coded one-line install command (no empty/broken panel).
- Given planned frameworks (Spec Kit, GSD, GSD-Pi, Superpowers), when any of their tabs is selected, then the panel shows Coming Soon only.
- Given no `_bmad/`, when the page is generated, then Method and GDS tabs are Absent (not omitted) and planned tabs remain Coming Soon.
- Given keyboard-only use, when focusing the tabstrip, then tabs are reachable and the selected panel is apparent without relying on color alone.

## Spec Change Log

## Design Notes

**Tab roster (v1):** BMad Method | BMad GDS | Spec Kit | GSD | GSD-Pi | Superpowers — matches README Supported frameworks.

**Curated Method command shortlist (static; edit only with human approval if changing meaning):**
- `/bmad-help` — guided help
- `/bmad-product-brief` — product brief
- `/bmad-prd` — PRD
- `/bmad-create-epics-and-stories` — epics & stories
- `/bmad-create-story` — story ready for dev
- `/bmad-dev-story` / `/bmad-quick-dev` — implement
- `/bmad-code-review` — review
- `/bmad-retrospective` — epic retrospective

**Curated GDS shortlist (static):**
- `/bmad-help` — guided help
- `/bmgd-gdd` — game design document
- `/bmgd-narrative` — narrative design
- `/bmgd-quick-dev` (or current Quick Flow skill id) — prototype / quick flow

**Presence ≠ primary Detect:** `ModuleContext.Detect` still picks one primary module for glossary/commands elsewhere; this page’s tab colors use the new independent Present helpers so dual installs show both as Present.

**Mermaid on synthesized pages:** emit `Mermaid.Block(...)` in the body; append `Mermaid.InitScript()` after the footer only when `Mermaid.ContainsBlock` is true.

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj --filter "FullyQualifiedName~HowToRead|FullyQualifiedName~ModuleContext|FullyQualifiedName~SiteNav|FullyQualifiedName~Icons"` -- expected: all pass
- `dotnet test` -- expected: full suite green (incl. golden fingerprint updates if asserted)

**Manual checks:**
- Generate against this repo (Method present, GDS likely absent): open Spec-Driven Development page; Method tab Present with commands + diagram; GDS Absent with install one-liner; planned tabs Coming Soon.

## Suggested Review Order

**Page shell & tabs**

- Entry: always-on framework tabstrip with Present/Absent/Coming Soon badges
  [`HowToReadTemplater.cs:72`](../../src/SpecScribe/HowToReadTemplater.cs#L72)

- Static Method commands + Mermaid diagram vs install CTA
  [`HowToReadTemplater.cs:120`](../../src/SpecScribe/HowToReadTemplater.cs#L120)

- Static GDS commands + shorter methodology spine
  [`HowToReadTemplater.cs:148`](../../src/SpecScribe/HowToReadTemplater.cs#L148)

**Presence detection**

- Independent Method/GDS Present helpers (manifest OR module-help.csv)
  [`ModuleContext.cs:174`](../../src/SpecScribe/ModuleContext.cs#L174)

- Wire presence flags into the always-written page
  [`SiteGenerator.cs:3142`](../../src/SpecScribe/SiteGenerator.cs#L3142)

**Diagrams & chrome**

- Static Method and GDS Mermaid flowcharts
  [`Mermaid.cs:67`](../../src/SpecScribe/Mermaid.cs#L67)

- Zero-JS radio tabs; badge text so state is not color-only
  [`specscribe.css:6122`](../../src/SpecScribe/assets/specscribe.css#L6122)

- Nav/QuickLinks rebrand; path stays `how-to-read.html`
  [`SiteNav.cs:141`](../../src/SpecScribe/SiteNav.cs#L141)

**Tests**

- Presence, default tab, install CTAs, Coming Soon, Mermaid gating
  [`SiteGeneratorHowToReadTests.cs:130`](../../tests/SpecScribe.Tests/SiteGeneratorHowToReadTests.cs#L130)
