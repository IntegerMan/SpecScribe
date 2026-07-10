---
title: 'About page, footer, and log-page formatting polish'
type: 'feature'
created: '2026-07-10'
status: 'done'
review_loop_iteration: 0
context: []
baseline_commit: '399b422e75052b9764127d8b6df61a78e6f521ec'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The site-wide footer reads clunkily (`Generated using SpecScribe · About on 2026-07-10 17:14` — the "About" link sits mid-sentence, is vaguely labelled, and the timestamp is machine-formatted). The About page and the Diagnostics ("log") page render off-style: full-bleed panels with no side gutters and default-blue links. The About page's own metadata is also stale: wrong description text, wrong author name, and a plain-text (non-linked) author. Finally, the displayed version is a hand-typed `0.1.0` with no signal that this is a pre-release build and no way to tell which build produced a given site.

**Approach:** Restructure `RenderFooter` so the credit reads `Generated using SpecScribe on {friendly date} · View generation details`, with the About link moved to the end and relabelled, and the timestamp formatted human-friendly. Give the About and Diagnostics pages the site's standard centered content column (gutters) and route their links through the site palette. Update the product metadata (csproj description + author) and render the author as a hyperlink. Mark the build pre-release semver-correctly (`0.1.0-preview`) and surface a dynamic build identifier — the build date plus short commit hash — with a visible "Preview" badge on the About page.

## Boundaries & Constraints

**Always:**
- About-page metadata (version, description, author) stays sourced from the assembly attributes generated from the csproj — the single source of truth. Do not hardcode the description/author string in `AboutTemplater`.
- The author URL is not a standard assembly attribute; add it as a code constant alongside `PathUtil.RepositoryUrl`, mirroring that pattern.
- The version base (`0.1.0-preview`) stays a csproj value; the build identifier (date + commit hash) is derived at runtime from assembly attributes — never hand-composed into a string literal.
- The build date + commit hash are best-effort: if either is absent (e.g. built outside a git checkout, or the metadata attribute is missing), degrade gracefully — omit the missing part, and omit the whole Build row if both are absent. Never render an empty `()` or a crash.
- The footer link still targets `about.html`, resolved through the existing `relativePrefix` `../` math so nested pages stay correct.
- Reuse existing CSS variables/tokens for link colors (`--teal` / `--rust`); no new literal colors.
- Every one of the ~15 `RenderFooter` call sites and the `PathUtilTests` footer tests must be updated to the new signature — no stragglers passing the old `trailingHtml`.

**Ask First:**
- None expected. If the csproj `<Authors>` value does not flow through to `AssemblyCompanyAttribute` after a rebuild (author still shows old value), pause and confirm before adding an explicit `<Company>`.

**Never:**
- Do not change the RenderParity harness or its regexes (footer is out of its scope).
- Do not touch the top nav bar, breadcrumb, or the About→Diagnostics in-content link target.
- Do not add JavaScript.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Footer, root page | `RenderFooter()` (no prefix) | `Generated using <a href="…github…">SpecScribe</a> on {friendly date} &middot; <a href="about.html">View generation details</a>` | N/A |
| Footer, nested page | `RenderFooter("../")` | About href resolves to `../about.html`; rest identical | N/A |
| Friendly date | `DateTime.Now` = 2026-07-10 17:14 | Renders e.g. `July 10, 2026 at 5:14 PM` (no raw `yyyy-MM-dd HH:mm`) | N/A |
| About author | assembly author = "Matthew-Hope Eland" | Rendered as `<a href="https://MattEland.dev">Matthew-Hope Eland</a>` | If author empty, omit the row (existing behavior) |
| About/Diagnostics link color | page rendered | in-content links use `--teal`, hover `--rust` — never default blue | N/A |
| Version display | InformationalVersion `0.1.0-preview+a1b2c3d…`, BuildDate metadata `2026-07-10` | Version row `0.1.0-preview`; Build row `2026-07-10 · a1b2c3d`; a "Preview" badge by the title | N/A |
| Version, no git / no date | InformationalVersion `0.1.0-preview` (no `+sha`), no BuildDate attr | Version row `0.1.0-preview`; Build row omitted entirely | Omit missing parts; never empty `()` |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/PathUtil.cs` -- `RenderFooter` (footer markup + date), `RepositoryUrl` const; add `AuthorUrl` const.
- `src/SpecScribe/SpecScribe.csproj` -- `<Version>`/`<Description>`/`<Authors>` product metadata; add a `BuildDate` `AssemblyMetadata` stamp.
- `src/SpecScribe/AboutTemplater.cs` -- About page; `ProductMetadata` record (version parsing, adds commit hash + build date); renders author link, version, Preview badge, Build row; adds gutter class to `<main>`.
- `src/SpecScribe/DiagnosticsTemplater.cs` -- Diagnostics ("log") page; adds gutter class to `<main>`.
- `src/SpecScribe/assets/specscribe.css` -- add shared content-column class + in-content link styling for these pages.
- 13 other `*Templater.cs` / `SiteGenerator.cs` / `HtmlRenderAdapter.cs` / `HtmlTemplater.cs` -- `RenderFooter` call sites to migrate to the new signature.
- `tests/SpecScribe.Tests/PathUtilTests.cs` -- two `RenderFooter` tests to update.
- `tests/SpecScribe.Tests/AboutTemplaterTests.cs` -- reflected-metadata assertions (still pass; author is now link text).

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/PathUtil.cs` -- change `RenderFooter(string trailingHtml, string relativePrefix = "")` to `RenderFooter(string relativePrefix = "")`; format the timestamp internally as a friendly date (e.g. `MMMM d, yyyy 'at' h:mm tt`); emit `Generated using <a>SpecScribe</a> on {date} &middot; <a href="{prefix}about.html">View generation details</a>`. Add `public const string AuthorUrl = "https://MattEland.dev";`.
- [x] Migrate every `RenderFooter($"on {DateTime.Now:…}", prefix?)` call site (AboutTemplater, DiagnosticsTemplater, ActionItemsTemplater, CommitDayTemplater, DeepAnalyticsTemplater, GitInsightsTemplater, HtmlRenderAdapter, HtmlTemplater, RequirementsTemplater ×2, RetroTemplater ×2, SiteGenerator, SprintTemplater) to the new signature — root pages `RenderFooter()`, nested `RenderFooter(prefix)`.
- [x] `src/SpecScribe/SpecScribe.csproj` -- set `<Version>0.1.0-preview</Version>`, `<Description>A static site generator that builds interactive dashboards from spec-driven development projects and git repositories</Description>`, `<Authors>Matthew-Hope Eland</Authors>`; add `<ItemGroup><AssemblyMetadata Include="BuildDate" Value="$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd'))" /></ItemGroup>` so a build stamps its date into a readable attribute.
- [x] `src/SpecScribe/AboutTemplater.cs` -- extend `ProductMetadata`: keep the pre-release-labelled `Version` (split InformationalVersion at `+`, take `[0]` — preserves `-preview`); add `CommitHash` (first 7 chars of the part after `+`, if present) and `BuildDate` (from the `BuildDate` `AssemblyMetadataAttribute`, if present); add `AuthorUrl` from `PathUtil.AuthorUrl`. In `RenderPage`: render author `<dd>` as a link to `AuthorUrl`; add a "Preview" badge beside the H1 when the version carries a pre-release label; add a `Build` `cap-row` showing `{BuildDate} · {CommitHash}` (omit an absent part, omit the whole row if both absent); add the shared gutter class to `<main id="main-content">`.
- [x] `src/SpecScribe/DiagnosticsTemplater.cs` -- add the same shared gutter class to `<main id="main-content">`.
- [x] `src/SpecScribe/assets/specscribe.css` -- add `.info-page { max-width: 860px; margin: 0 auto; padding: 0 1.5rem 2.5rem; }` (matches the header/body column) and `.info-page a { color: var(--teal); } .info-page a:hover { color: var(--rust); }`; add a small `.preview-badge` pill styled with existing tokens (e.g. `--gold` on `--parchment`).
- [x] `tests/SpecScribe.Tests/PathUtilTests.cs` -- update the two `RenderFooter` tests to the new signature; assert the `View generation details` link text, the `about.html` / `../about.html` href, and the SpecScribe credit link.

**Acceptance Criteria:**
- Given any generated page, when its footer renders, then it reads `Generated using SpecScribe on {friendly date} · View generation details` with the details link at the end pointing to the (prefix-resolved) About page.
- Given the About page, when it renders, then it shows the new description sentence and the author "Matthew-Hope Eland" as a link to `https://MattEland.dev`, all inside a centered gutter column with teal/rust links.
- Given the About page, when it renders, then the version shows `0.1.0-preview` with a "Preview" badge and a Build line carrying the build date and short commit hash (each part present only when available).
- Given the Diagnostics page, when it renders, then its content sits in the same centered gutter column with site-styled links.
- Given the test suite, when `dotnet test` runs, then all tests pass.

## Design Notes

Friendly date example: `DateTime.Now.ToString("MMMM d, yyyy 'at' h:mm tt")` → `July 10, 2026 at 5:14 PM`. Centralizing the format in `RenderFooter` also removes the `on {DateTime.Now:yyyy-MM-dd HH:mm}` string duplicated across 15 call sites.

The `.info-page` column is set to `860px` to align its left/right edges with the existing `.doc-header` (860px) so the About/Diagnostics header and panels share one column edge. Wide diagnostics tables still scroll internally via `.chart-panel { overflow-x: auto }`.

Version parsing: deterministic SDK builds append `+<full-sha>` to `AssemblyInformationalVersion` (the current code trims it — we now surface it). With `<Version>0.1.0-preview</Version>`, InformationalVersion becomes `0.1.0-preview+<sha>`; splitting at `+` yields `0.1.0-preview` for the Version field, and the first 7 chars of the suffix give the short hash. The `BuildDate` `AssemblyMetadata` value is read via `asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildDate")?.Value`.

Golden-diff note: the About page's version/build line now varies per build+commit (like the footer clock). It is stable for a given commit on a given day, but a byte-parity site diff across commits will need this line normalized alongside the existing footer-clock / `?v=` / CRLF normalizations.

## Verification

**Commands:**
- `dotnet build src/SpecScribe/SpecScribe.csproj` -- expected: builds clean (regenerates assembly metadata from the new csproj values).
- `dotnet test` -- expected: all tests green, including the updated footer tests.

**Manual checks:**
- Regenerate the demo site and open `about.html` + `diagnostics.html`: gutters present, links teal (rust on hover), footer reads `… on July 10, 2026 at 5:14 PM · View generation details`, author is a link to MattEland.dev, description is the new sentence, version shows `0.1.0-preview` with a Preview badge and a Build line (date · short hash).

## Suggested Review Order

**Footer restructure (entry point)**

- Single source for the footer markup + culture-invariant friendly date; drops the old `trailingHtml` param.
  [`PathUtil.cs:102`](../../src/SpecScribe/PathUtil.cs#L102)

- The new author-URL constant, mirroring `RepositoryUrl`.
  [`PathUtil.cs:93`](../../src/SpecScribe/PathUtil.cs#L93)

**Product metadata & version derivation**

- Splits the informational version — keeps the `-preview` label, extracts the short commit hash.
  [`AboutTemplater.cs:55`](../../src/SpecScribe/AboutTemplater.cs#L55)

- Record shape: `AuthorUrl`/`CommitHash`/`BuildDate` plus `BuildLabel` + `IsPrerelease` derivations.
  [`AboutTemplater.cs:21`](../../src/SpecScribe/AboutTemplater.cs#L21)

- Pre-release version label (makes the package semver-correctly pre-release).
  [`SpecScribe.csproj:19`](../../src/SpecScribe/SpecScribe.csproj#L19)

- Build-date stamped into a readable assembly attribute.
  [`SpecScribe.csproj:29`](../../src/SpecScribe/SpecScribe.csproj#L29)

**About page rendering**

- Preview badge, shown only when the version is pre-release.
  [`AboutTemplater.cs:102`](../../src/SpecScribe/AboutTemplater.cs#L102)

- Build row, omitted cleanly when both date and hash are absent.
  [`AboutTemplater.cs:120`](../../src/SpecScribe/AboutTemplater.cs#L120)

- The shared gutter column applied to `<main>`.
  [`AboutTemplater.cs:110`](../../src/SpecScribe/AboutTemplater.cs#L110)

**Shared page styling**

- `.info-page` gutter/link column + the `.preview-badge` pill (existing tokens only).
  [`specscribe.css:604`](../../src/SpecScribe/assets/specscribe.css#L604)

- Diagnostics page adopts the same gutter column.
  [`DiagnosticsTemplater.cs:164`](../../src/SpecScribe/DiagnosticsTemplater.cs#L164)

**Tests (peripherals)**

- Golden fingerprint: new footer/build normalizers + regenerated constant.
  [`SiteGeneratorAdapterTests.cs:183`](../../tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs#L183)

- Footer link/label + nested-href assertions.
  [`PathUtilTests.cs:34`](../../tests/SpecScribe.Tests/PathUtilTests.cs#L34)
