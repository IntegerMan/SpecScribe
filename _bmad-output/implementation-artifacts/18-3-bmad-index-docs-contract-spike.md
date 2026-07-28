---
baseline_commit: 86b35c267241c15b05c64e3aaa3e13cce58198b2
---

# Story 18.3: BMad Index-Docs Contract Spike

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer wanting per-doc descriptions in the portal,
I want bmad-index-docs' generated index.md format inventoried and pinned as a parseable contract,
so that SpecScribe can consume it as a blurb/metadata source for doc pages without depending on an unstable prose format.

> **Citation policy (adopted from ADR 0015).** Code is cited by **symbol** first; line numbers appear only
> where a symbol cannot identify the site and are marked *(as of 2026-07-26)*. `SiteGenerator.cs` is ~5,000
> lines and under concurrent editing — its line numbers drift within days. `baseline_commit` above pins the
> tree these citations were taken against.

## Why this story exists (read first)

Every doc SpecScribe renders through the generic markdown pass gets a **page** and nothing else — no
description, no list entry, no nav slot. Only **eight** filenames in the entire product carry a human blurb
today, and they are hardcoded C# literals (`ModuleContext.BmadMethodDocs` — 5 entries; `GameDevStudioDocs` — 3).
`bmad-index-docs` is BMad's own answer to "describe every doc in a folder," and it ships in **`core`**, so it
is present in *every* BMad install. This spike asks whether its `index.md` output is stable enough to be a
data source for those blurbs — and, critically, whether SpecScribe should depend on it at all.

**The one-line test for "is this in scope?":** if the change *inventories* real `bmad-index-docs` output,
*writes* an entry grammar SpecScribe would parse, *ranks* the candidate consuming surfaces, or *specifies*
fallback behavior → in. If it *lands* a parser, a `Frontmatter` field, a new page, a new nav entry, or any
`src/**` / `tests/**` change → out; that is the follow-on implementation story (not yet seated) and 18.4's
blurb-metadata half.

### This story is NOT gated by 18.2 — and that is a finding, not an assumption

`bmad-index-docs` belongs to the **`core`** module, not BMM:

- `_bmad/core/module-help.csv:6` — `Core,bmad-index-docs,Index Docs,ID,Use when LLM needs to understand available docs without loading everything.,…`
- `_bmad/_config/skill-manifest.csv:9` — `"bmad-index-docs",…,"core","_bmad/core/bmad-index-docs/SKILL.md"`

Two consequences the follow-on story must inherit:

1. **`index.md` is a cross-module convention.** Unlike TEA's `traceability-matrix.csv` or CIS's session files
   (Story 18.5's targets), it does not belong to any *detected* module. So consuming it must **not** route
   through `ModuleContext.DocsFor` / the `BmadModule` enum / `ModuleDoc`, and it is unaffected by Story 18.2's
   identity fix or ADR 0015. Confirm this, then say it plainly — it is the reason 18.3/18.4 can proceed while
   18.2 is still in flight.
2. **`core` is the one module `ModuleContext.Detect` deliberately excludes** (`ModuleContext.cs:220,229` *(as of
   2026-07-26)*: `.Where(n => !string.Equals(n, "core", …))`). SpecScribe would be adopting a convention from
   the single module it refuses to identify. Not a blocker — but state it, because a later reader will
   otherwise try to "fix" the exclusion.

## Acceptance Criteria

1.
**Given** bmad-index-docs' current output across representative repos
**When** the spike inventories the index.md entry format (line shape, path resolution, description length/style, edge cases like missing docs or nested folders)
**Then** a written contract documents the exact entry grammar SpecScribe should parse, flags any repo-to-repo inconsistencies found, and recommends whether to parse it as-is or request a stricter emission mode from bmad-index-docs.

2.
**Given** the pinned contract
**When** the spike identifies the seam
**Then** it recommends which SpecScribe surface(s) should carry the parsed blurb metadata (doc nav/TOC entries and/or a docs landing page) and the fallback behavior when index.md is absent, stale, or references a moved/deleted file
**And** the follow-on implementation story has an agreed scope boundary.

[Source: `_bmad-output/planning-artifacts/epics.md` §"Story 18.3: BMad Index-Docs Contract Spike"]

## Context & Scope

### 1. The emitter is an LLM prompt, not a formatter — expect divergence as the default finding

`.claude/skills/bmad-index-docs/SKILL.md` is **67 lines total** and is the whole specification. Read it in
full before anything else. Its structure:

- `## EXECUTION` — four prose steps. Step 3 is *"Read each file to understand its actual purpose and create
  brief (3-10 word) descriptions based on the content, not just the filename."*
- `## OUTPUT FORMAT` — a fenced markdown **example**, not a grammar:
  ```markdown
  # Directory Index

  ## Files

  - **[filename.ext](./filename.ext)** - Brief description
  - **[another-file.ext](./another-file.ext)** - Brief description

  ## Subdirectories

  ### subfolder/

  - **[file1.ext](./subfolder/file1.ext)** - Brief description
  ```
- `## VALIDATION` — six prose bullets: `./`-prefixed relative paths, group similar files, read contents for
  descriptions, 3-10 words, alphabetical within groups, skip dotfiles.

**There is no serializer, no schema, no linter, and no test anywhere in the BMad distribution that enforces
this.** The output is whatever an LLM produced on the day it ran. AC #1's clause *"flags any repo-to-repo
inconsistencies found"* should therefore be treated as **expecting** inconsistency and characterizing its
shape and blast radius — not as a search that might come up empty. If the spike finds perfect consistency
across its samples, that is a surprising result and needs more samples, not a conclusion.

The installed copy at `.claude/skills/bmad-index-docs/SKILL.md` is the only materialized copy in this repo —
`_bmad/core/` contains just `config.yaml` + `module-help.csv`; the skill bodies are IDE-projected. Verify the
`files-manifest.csv:223` hash (`a855d706…`) still matches if you need to prove the installed copy is unmodified.

### 2. There is no `index.md` anywhere in this repo — the spike starts with zero samples

`find . -iname "index.md"` (excluding `.git/`, `node_modules/`, `.claude/worktrees/`) returns **nothing**.
`bmad-index-docs` has never been run here. Every one of AC #1's questions — line shape, path resolution,
description style, nested-folder handling — is unanswerable from this repo as it stands.

**The spike must obtain samples.** Two sources, and it needs both:

- **Generated locally.** Run `bmad-index-docs` against at least two structurally different real folders in this
  repo — e.g. `docs/` (7 loose files + an `adrs/` subdir + the gitignored `live/`) and
  `_bmad-output/planning-artifacts/` (loose `.md` files + `briefs/`, `prds/`, `research/`, `ux-designs/`
  subdirs). Two runs over the *same* folder is also worth doing: it directly measures run-to-run determinism,
  which is the single most important property for a parse contract.
- **Found in the wild.** BMad's own repo has `docs/index.md` — see §3.

⚠️ **Owner decision needed before generating (see Open Questions):** generated samples are new files written
into a shared `main`. Default assumption is the spike writes them to the **session scratchpad**, not the repo,
and pastes the verbatim bytes into its Completion Notes. Story 18.1's code review found the opposite approach
fatal: its probe fixtures were written to OS temp and deleted, making *every* external claim unfalsifiable
(*"The empirical basis is unreproducible while Decision 7 mandates pinning fixtures to it"*). **Whatever you
do, the raw bytes, the source URL/path, and the retrieval date must land in this file.**

### 3. BMad's own `docs/index.md` does not match the documented format (hypothesis — reconfirm)

Fetched live 2026-07-26 from
`raw.githubusercontent.com/bmad-code-org/BMAD-METHOD/main/docs/index.md`. Observed shape:

```markdown
---
title: Welcome to the BMad Method
description: AI-driven development framework with specialized agents, guided workflows, and intelligent planning
---

The BMad Method (**B**uild **M**ore **A**rchitect **D**reams) is an AI-driven development framework …

## New Here? Start with a Tutorial

- **[Get Started with BMad](./tutorials/getting-started.md)** — Install and understand how BMad works
- **[Workflow Map](./reference/workflow-map.md)** — Visual overview of BMM phases, workflows, and context management

:::tip[Just Want to Dive In?]
…
:::
```

Divergences from `SKILL.md`'s OUTPUT FORMAT, every one of which is a parse hazard:

| Documented | Observed in BMad's own `docs/index.md` |
|---|---|
| `- **[x](./x)** - description` (ASCII hyphen) | `- **[x](./x)** — description` (**em-dash**) |
| `# Directory Index` | `---` frontmatter with `title:` + `description:`, no H1 at all |
| `## Files` / `## Subdirectories` / `### subfolder/` | Semantic headings (`## New Here? Start with a Tutorial`, `## How to Use These Docs`) |
| List entries only | Prose paragraphs, a markdown table, and Docusaurus `:::note` / `:::tip` admonitions interleaved |
| 3-10 word descriptions | Full sentences |

**Caveat, and it is load-bearing:** this file is plausibly **hand-authored** (it is a published Docusaurus
landing page), not `bmad-index-docs` output. Story 18.1 was burned by exactly this class of error — treating
a tool's own repo/docs as equivalent to a downstream project's real usage. **The spike must distinguish
"index.md written by the skill" from "index.md a human wrote" and say which of its samples are which.** If it
cannot find a single confirmed skill-generated `index.md` in the wild, that itself is the AC #1 answer, and it
argues hard for "request a stricter emission mode" over "parse it as-is."

That distinction is also a *runtime* problem, not just a research one: SpecScribe cannot tell the two apart
either. Whatever grammar the contract pins must degrade safely when handed a hand-written `index.md`.

### 4. SpecScribe already has three blurb mechanisms — rank index.md against them, do not assume it wins

This is the most important framing in the story. AC #1 asks "parse as-is or request a stricter mode"; there is
a third answer — **"neither, derive blurbs from the docs themselves"** — and the code already does it.

**(a) `ModuleDoc.Description` — the incumbent, and its ceiling.**
`public sealed record ModuleDoc(string FileName, string Label, string Description, bool InNav)`
[`ModuleContext.cs:12`]. Populated by two hardcoded arrays — `BmadMethodDocs` (`prd.md` → *"Read the product
requirements."*, `ARCHITECTURE-SPINE.md`, `brief.md`, `DESIGN.md`, `EXPERIENCE.md`) and `GameDevStudioDocs`
(`gdd.md`, `narrative-design.md`, `game-architecture.md`). `SiteNav.Build` matches these **by filename anywhere
in the source tree** and pushes each into `quickLinks` as the `Description` element. It surfaces exactly once:
as `data-tooltip` on a quick-link pill (`HtmlRenderAdapter.AppendKeyViewsBand`, ~`:221` *(as of 2026-07-26)*).
Ceiling: 8 filenames, C# literals, module-gated. index.md's pitch is precisely "generalize this to every doc."

**(b) `ExtractAdrSummary` — the precedent that makes index.md optional.**
`SiteGenerator.ExtractAdrSummary(raw, title)` (~`:4888-4923` *(as of 2026-07-26)*) already derives a one-line
blurb **from the document's own content**, with a two-strategy cascade:
1. find the `## Context` heading (`AdrContextHeadingPattern`), take the first paragraph, skipping blank and
   `IsDecorativeLine` lines, stopping at the next `#`, then `CollapseSummary`;
2. fallback: the descriptive tail after the **last** em-dash / en-dash / spaced-hyphen in the title.

It feeds `AdrEntry.Summary`, which renders as the bolded-title-plus-summary line in the synthesized ADR
landing's `ListRow` rows. **Generalizing this to arbitrary docs requires no external file, no new BMad
dependency, and no staleness problem.** The spike must rank it against index.md explicitly and give reasons —
"index.md descriptions are human/LLM-curated and content-derived summaries are mechanical" is a real argument,
but so is "a dependency on an unversioned LLM-generated file is a liability." Do not omit this option.

**(c) `Frontmatter` — a field that does not exist yet, and BMad's index.md already carries it.**
`Frontmatter` [`Frontmatter.cs`] models `Title`, `Project`, `Date`, `Created`, `Author`, `Version`, `Status`,
`Route`, `Type`, `Id`, `Companions`, `Sources` — and **no `Description`**. BMad's own `docs/index.md` sets
`description:` in frontmatter (§3). A `Frontmatter.Description` would be a per-doc, self-describing,
zero-staleness blurb source. Assess it as a fourth option (and note it composes with (b) as a cascade:
frontmatter → index.md entry → content-derived → none).

### 5. Nothing in the portal lists generic docs today — AC #2's landing page is greenfield

Do not propose "revive the home-index doc bands." **They were deliberately removed.**
`DashboardViewBuilder.KnownIndexGroups` survives only as a folder-classification gate, and its own doc comment
says so (`DashboardViewBuilder.cs:10-19` *(as of 2026-07-26)*): *"home-index bands removed in
spec-declutter-home-dashboard"*, *"Titles are unused post-declutter (home-index bands removed); the prefixes
alone gate `IsWellKnownTopLevelFolder`."*

Current reachability of a generic doc page (`_docs[relative] = MarkdownConverter.Convert(…)` in
`GenerateOneInternal`): rendered to HTML, tracked in the change surface, captured for SPA/webview — and
listed **nowhere**. Not in `SiteNav.Items`, not in `QuickLinks`, not in any list page, and not in the VS Code
outline (`ProjectOutline` is epics/stories only). Confirm this before writing AC #2's recommendation; if you
find a surface I have missed, that surface is the answer.

`IsWellKnownTopLevelFolder` also carries an explicit **do-not-extend** warning ("that was a misdiagnosed Epic 4
debt"). Any recommendation that touches it must acknowledge that comment.

### 6. The synthesized ADR landing is the exact precedent — and the exact collision

`RegenerateAdrs` (~`:1155-1215` *(as of 2026-07-26)*) is the closest thing in the product to "a landing page
over a folder of docs," and it is where a docs landing page must be modelled from:

- It builds a `ListRow`-based list (`ListRow.Render` with summary / status badge / date chip / primary link /
  `list-row-accent-*`) — Story 10.8's shared list grammar. A docs landing page must reuse `ListRow`, not
  hand-roll a `<ul>`.
- It is **synthesized only when nothing else claimed the slot** — the `landingPathAlreadyWritten` flag, set
  **only on a successful write** (a README that exists but fails to render must *not* suppress the fallback,
  or the nav link 404s).
- **It already handles a literal `index.md`.** A record or non-record file that renders to the landing output
  path suppresses synthesis — the code comment names the case explicitly (*"a stray `index.md`"*), and there is
  a regression test for it: `SiteGeneratorWebviewTests.cs:702-706` writes `docs/adrs/index.md` and asserts the
  behavior.

⇒ **The sharpest question in AC #2:** if a folder has an `index.md`, is that file (i) the landing page itself,
(ii) the *data source* for a synthesized landing page, or (iii) both — and what happens when it is both? Under
today's rules an `index.md` under SourceRoot simply renders as a generic page via `PathUtil.ToOutputRelative`,
so a naive "parse index.md for blurbs" ships a duplicate surface: the index rendered as prose *and* its data
re-rendered as a list. Answer this; do not leave it implicit.

### 7. Path resolution — the entry grammar's hardest half

AC #1 names "path resolution" and it is where a parse contract actually fails. The chain a parsed entry must
survive:

1. Entry href is **`./`-relative to the index file's own folder** (`SKILL.md` VALIDATION bullet 1). BMad's own
   sample confirms `./tutorials/getting-started.md`.
2. SpecScribe keys everything by **source-root-relative** path — `_docs` is keyed on `ToSourceRelative(file)`,
   normalized by `PathUtil.NormalizeSlashes`. So `{indexFolder}/{href}` must be resolved and normalized
   (including `../` segments) into that key space.
3. The output href is then `PathUtil.ToOutputRelative(relative)`.

Failure modes to enumerate in the contract, each with its named behavior:

- **Target is not `.md`.** `EnumerateSourceFiles` globs `"*.md"` only — an entry pointing at `.csv`, `.png`,
  or a code file has **no page**, ever. `SKILL.md`'s own example uses `filename.ext`, so this is the common
  case, not an edge case.
- **Target is outside SourceRoot** (e.g. an index in `docs/` linking `../src/...`). No page.
- **Target is `IsIgnored`** (e.g. anything under `docs/live/`).
- **Target was consumed by a dedicated surface.** `bundle.ConsumedSourceRelatives` — story artifacts and retro
  notes have a *better* page than the generic one. An index blurb pointing at one must resolve to the dedicated
  surface or be dropped, never produce a dead generic link.
- **Target is special-routed** — `epics.md` → `epics.html`, requirements → the curated FR/NFR page, ADR records
  → `adrs/`. Same rule.
- **Target moved or was deleted** — AC #2 names this explicitly. This is the staleness case: `index.md` is
  generated on demand and **never** regenerated automatically, so it goes stale the first time anyone renames
  a file.
- **Duplicate / conflicting entries** for the same target.
- **Nested `index.md` files** — one per folder, potentially overlapping claims.
- **Non-UTF8 / CRLF / BOM**, and an `index.md` that is 100% prose with zero parseable entries.

Precedent for the "never produce a broken link" discipline: `SiteNav.Build`'s module-doc loop, which resolves
by file existence, first-wins alphabetically on duplicates, and emits **one `Skipped` diagnostic** rather than
silently dropping the loser.

### 8. Diagnostics vocabulary is closed — five values, do not invent a sixth

`AdapterDiagnosticCategory` [`AdapterDiagnostic.cs`]: `Unsupported`, `Malformed`, `Skipped`, `Error`,
`Informational`. AC #2's fallback behaviors must each name one of these **and draft the message wording**,
matching `BmadArtifactAdapter`'s existing tone.

⚠️ **Story 18.1's code review failed exactly this AC clause** — it listed four non-goals with rationale but
"**no category and no wording for any**," and spent `Skipped` on something that was not a declared non-goal.
Do not repeat it: every fallback in the contract gets a category **and** a drafted string.

Also inherit 18.1's review finding about the **anchor root**: `AdapterDiagnostic.RelativePath` is contractually
**source-root**-relative and gets `DiagnosticAnchorRoot.Source`. An `index.md` under SourceRoot is fine; one
under the ADR root or elsewhere is not, and the webview Problems channel will resolve it to a path that does
not exist. Say which root each proposed diagnostic anchors to.

### 9. Downstream: Story 18.4

Story 18.4 (Forged Ideas List Page, `backlog`) *"depends on 18.3's pinned contract for its blurb-metadata half
but stands alone for the Ideas list surface"* [epics.md, Stories 18.3–18.4 seating comment]. So the contract
this spike pins is consumed by at least two callers. Keep the grammar and the fallback rules **surface-agnostic**
— do not fold them into whichever surface AC #2 recommends. 18.4 also uses `ListRow` per Story 10.8, reinforcing
§6's reuse point.

### 10. Named candidate seams for AC #2 — evaluate and rank all four, pick one, justify the losers

Per this project's create-story convention, visual directions are named up front so the recommendation is a
*choice among stated alternatives* rather than an invention. These are **candidates to rank, not a
pre-selection** — the spike's job is to pick and to say why each loser lost. Adding a fifth is fine if you
name it the same way.

- **D0 — "No dependency."** Derive blurbs from each doc (generalized `ExtractAdrSummary` cascade and/or a new
  `Frontmatter.Description`); `index.md`, if present, becomes optional *enrichment* that overrides the derived
  value. Zero staleness, zero new external contract. Cost: mechanical blurbs, and it does not satisfy the "pin
  a parseable contract" framing of AC #1 on its own.
- **D1 — "Tooltip generalization."** Feed parsed blurbs into the existing `Description` slot on quick links /
  nav entries. Smallest diff, reuses a shipped path, no new page. Cost: `data-tooltip` is hover-only — a blurb
  that only exists on hover is invisible on touch and to most keyboard users, and this project does not signal
  meaning by hover alone any more than by color alone.
- **D2 — "Docs landing page."** A synthesized folder-scoped list page mirroring the ADR landing exactly
  (`ListRow`, `landingPathAlreadyWritten` guard, nav gate on non-empty). Highest value — it also closes §5's
  gap that generic docs are listed nowhere. Cost: a new page, a new nav entry, a new `QuickLinkFamily`
  membership, and it must resolve §6's index-as-page-vs-index-as-data collision.
- **D3 — "Blurb on the page + local-context band."** Attach the blurb to the doc page's own header (a
  subtitle/deck under the H1) and to the sibling entries in the white local-context band. No new page, no new
  list, visible without hover. Cost: a blurb next to the document it describes is the least *useful* placement —
  the reader is already there.

Whichever wins, the recommendation must state the **NFR8 absence rule**: with no `index.md`, the surface is
absent — not empty, not "no description available".

### Deliberate non-goals (seed list — the spike may extend with rationale)

- **Writing the parser**, or any `src/**` / `tests/**` change. The spike pins the grammar; the follow-on story
  implements it.
- **Adding `Frontmatter.Description`**, a `DocModel` blurb field, a new page, or a nav entry — all recorded as
  *candidate* extensions, none landed.
- **Changing `bmad-index-docs` itself.** AC #1 may *recommend* requesting a stricter emission mode upstream;
  the spike does not edit the skill, and it must not, since `.claude/skills/` is installer-managed
  (`files-manifest.csv` carries its hash) and would be overwritten on the next BMad update. Note that
  constraint if you make the recommendation.
- **Reviving the removed home-index doc bands** (§5) — if a recommendation resembles them, say so explicitly
  and justify the reversal.
- **Extending `IsWellKnownTopLevelFolder` / `KnownIndexGroups`** — carries an explicit do-not-extend warning.
- **Any new authoring schema.** SpecScribe never asks users to write SpecScribe-specific files; `index.md` is
  read as-is or not at all.
- **Touching module identity, `BmadModule`, or ADR 0015's surface area** — §"This story is NOT gated by 18.2".
- **An ADR unless a genuine architecture fork is found.** "Where do per-doc blurbs come from, and is an
  external LLM-generated file allowed to be an input to generation?" *could* be one — see Task 6.

## Tasks / Subtasks

- [x] **Task 1 — Read the emitter and the consumption sites in full (AC: #1, #2)**
  - [x] Read `.claude/skills/bmad-index-docs/SKILL.md` in full (67 lines) — it is the entire specification.
  - [x] Confirm the `core`-module ownership claim: `_bmad/core/module-help.csv:6`, `_bmad/_config/skill-manifest.csv:9`, `_bmad/_config/files-manifest.csv:223`. Confirm `_bmad/core/` holds no materialized skill body.
  - [x] Confirm `ModuleContext.Detect` still excludes `core`, and confirm the "18.3 is not gated by 18.2" conclusion against the current state of 18.2 (its story file reads `in-progress`; `sprint-status.yaml` reads `ready-for-dev` — a live drift worth noting, and a signal that a concurrent session may be editing `ModuleContext.cs`).
  - [x] Read `ModuleContext.ModuleDoc` / `BmadMethodDocs` / `GameDevStudioDocs`, `SiteNav.Build`'s module-doc loop, `HtmlRenderAdapter.AppendKeyViewsBand`, `SiteGenerator.ExtractAdrSummary` + `CollapseSummary` + `IsDecorativeLine`, `Frontmatter`, `MarkdownConverter.Convert` / `ExtractFirstH1`, `DocModel`, `ListRow`, `AdapterDiagnostic`, and `RegenerateAdrs`' synthesized-landing block.
  - [x] Verify §5's claim that no surface lists generic docs — grep `SiteNav`, `DashboardViewBuilder`, `ProjectOutline`, and the templaters. If a listing surface exists, it supersedes §5 and probably answers AC #2.

- [x] **Task 2 — Obtain real samples (AC: #1)**
  - [x] Resolve the Open Question on where generated samples may be written **before** generating anything.
  - [x] Run `bmad-index-docs` against `docs/` and `_bmad-output/planning-artifacts/` — two structurally different folders (loose files + subdirs; one with a gitignored subdir, one with four content subdirs).
  - [x] Run it **twice over the same folder** and diff. Run-to-run determinism is the single most decision-relevant property; record the diff verbatim.
  - [x] Find at least one `index.md` in the wild. Start with `bmad-code-org/BMAD-METHOD`'s `docs/index.md` (§3) and reconfirm the fetched shape at today's HEAD. **Record the URL, the retrieval date, and the upstream commit SHA** — 18.1's review named the absence of exactly these as fatal.
  - [x] For every sample, classify it **skill-generated vs hand-authored** and state the evidence. If no confirmed skill-generated sample can be found in the wild, say so — it is a finding, and it drives AC #1's as-is-vs-stricter-mode recommendation.
  - [x] Paste the raw bytes of every sample (or a faithful excerpt with the elision marked) into Completion Notes.

- [x] **Task 3 — Pin the entry grammar (AC: #1)**
  - [x] Write the exact grammar SpecScribe should parse: the entry line shape, which parts are required vs optional, the accepted separators (ASCII `-` vs `–` vs `—` — BMad's own file uses an em-dash where `SKILL.md` documents a hyphen), bold-wrapped vs bare links, and how heading structure (`## Files` / `### subfolder/` vs arbitrary semantic headings) is or is not used.
  - [x] Specify what is **ignored**: prose paragraphs, tables, `:::note`/`:::tip` admonitions, frontmatter, HTML comments, nested lists.
  - [x] Specify description handling: length bound (the skill says 3-10 words; BMad's own file uses full sentences), truncation rule, and whether inline markdown in a description is stripped, escaped, or rendered.
  - [x] Enumerate the repo-to-repo inconsistencies actually observed, each with the sample it came from, and state the parse impact of each.
  - [x] **Recommend: parse as-is, or request a stricter emission mode upstream** — with the reasoning, and noting the §"non-goals" constraint that `.claude/skills/` is installer-managed.

- [x] **Task 4 — Path resolution and failure taxonomy (AC: #1, #2)**
  - [x] Specify the `./`-relative → source-root-relative → output-relative resolution chain (§7), including `../` segments, casing (`OrdinalIgnoreCase` matches the rest of the codebase), and slash normalization.
  - [x] Enumerate every failure mode in §7 and assign each a named behavior: resolve-to-dedicated-surface, drop-silently, drop-with-diagnostic, or keep-blurb-without-link.
  - [x] For each behavior that reports, name the `AdapterDiagnosticCategory`, **draft the message string**, and state its anchor root. (18.1's review failed this exact clause — do not repeat it.)
  - [x] Specify staleness detection: is a stale `index.md` detectable at all (mtime vs referenced files? entry count vs on-disk count?), and what the honest behavior is when it is not.

- [x] **Task 5 — Rank the candidate seams and pick one (AC: #2)**
  - [x] Evaluate **D0 / D1 / D2 / D3** (§10) against: reader value, diff size, hover/a11y viability, staleness exposure, whether it closes §5's "generic docs are listed nowhere" gap, and NFR8 absence behavior.
  - [x] Answer §6's collision explicitly: with an `index.md` present, is it the landing page, the data source, or both — and what suppresses what.
  - [x] Recommend **one** and justify why each loser lost. Do not recommend "all of them."
  - [x] State the NFR8 absence rule for the winner: with no `index.md`, the surface is absent — never an empty list or a "no description available" placeholder.
  - [x] Write the follow-on story's scope boundary (AC #2's final clause): what it lands, what it does not, and which parts 18.4 reuses.

- [x] **Task 6 — Architecture fork check (AC: #2)**
  - [x] Decide whether "an external, LLM-generated, unversioned file is an allowed *input* to generation" is a cross-cutting decision warranting an ADR. Arguments in favor: it is a new class of input (every current input is either authored-by-a-human or derived-from-git); it interacts with AD-4's additive/non-blocking rule and with NFR8. Argument against: it may just be one more optional source under AD-4, requiring no new invariant.
  - [x] If it is a fork, **propose** the ADR (per this project's ADR-trigger discipline) rather than burying the decision in this story's notes. If it is not, say so in one sentence and move on.

- [x] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [x] Write the contract (grammar + inconsistency inventory + as-is-vs-stricter recommendation + resolution chain + failure taxonomy with categories and drafted wording + seam ranking + winner + NFR8 rule + follow-on scope boundary) into this story's **Completion Notes**, mirroring Story 18.1's Completion-Notes-as-deliverable convention.
  - [x] Do **not** land `src/**` / `tests/**` changes. Confirm with `git diff --stat {baseline_commit} HEAD -- src tests` and report the result honestly — 18.1's review caught a `git status` claim its own File List falsified. If the tree is dirty from a concurrent session, say whose changes and do not touch them.
  - [x] Every external claim carries its source URL/path + retrieval date + commit SHA.

### Review Findings

_Code review 2026-07-28 (`/bmad-code-review 18.3`). Three parallel layers: Blind Hunter (adversarial),
Edge Case Hunter, Acceptance Auditor. Scoped to this story's own File List — the story markdown itself and
`sprint-status.yaml`'s 18-3 entry; sibling stories' entries bundled in the same `sprint-status.yaml` file
(18.4, 18.5, 18.6, 25.3, etc.) explicitly excluded. `git diff --stat 86b35c2 HEAD -- src tests` independently
re-confirms zero files touched by this story (all 16 changed files belong to Story 18.2, committed
separately) — the "no `src`/`tests` changes" claim holds on the merits. 6 findings dismissed as noise._

**The spike's core conclusion survived adversarial verification.** Every spot-checked symbol
(`ExtractAdrSummary`, `CollapseSummary`, `RegenerateAdrs`, `landingPathAlreadyWritten`, `ReservedModuleNames`,
`PathUtil.EscapesRepoRoot`, the 5-value `AdapterDiagnosticCategory`, the ADR-index regression test, the
12-field `Frontmatter` with no `Description`) verified exactly against the live repo. The D0 recommendation
("no dependency on `index.md`") is independently supported by the 0/25 determinism measurement, the
GitHub-wide null result, and the format-shape survey. The findings below are about the deliverable's
**completeness, evidentiary calibration, and downstream coordination** — not its central conclusion.

- [x] [Review][Decision] **RESOLVED (owner, 2026-07-28): annotate, keep ADR 0019 as-authored.** Proposed ADR 0019 collides with Story 22.3's own unwritten ADR 0019 claim — `docs/adrs/README.md`'s ADR 0021 entry states verbatim that "0019 is claimed-but-unwritten by BOTH Story 18.3 and Story 22.3," for two unrelated decisions. 18.3 predates this discovery (18.4, which surfaced it, ran after 18.3), so this wasn't an authoring fault. **Owner decision: leave the proposal under 0019 as historical record; a corrective numbering note added to §9** flagging the collision for whoever eventually ratifies it.

- [x] [Review][Decision] **RESOLVED (owner, 2026-07-28): renamed to Story 18.7.** The follow-on story proposal in §8 squatted on an already-shipped, unrelated Story 18.6 (`epics.md`'s real "Module-Aware Artifact Coverage Families," seated after 18.3 concluded). This wasn't wrong when written, but the artifact as it stood still proposed the colliding number. **Owner decision: renumber the proposal.** §8 and §9's sequencing note now read "Story 18.7," with a dated annotation explaining the renumbering and that `18.7` was unclaimed at the time of the fix.

- [x] [Review][Decision] **RESOLVED (owner, 2026-07-28): premise revised.** §9's original argument framed the fork as "human-authored vs. LLM-generated," which doesn't hold — most of SpecScribe's own inputs (including this document) are themselves LLM-generated. **Owner decision: revise before the ADR is written.** §9 now frames the fork as **provenance discipline** (accountable producer, deliberate re-run/commit, no silent regeneration) rather than authorship — `index.md` fails on producer-anonymity (§2), measured field-level instability at authorship time (§4: 0/25), and no re-sync trigger (§6e); a BMad story file fails none of these. The ADR's proposed title and decision sketch were updated to match.

- [x] [Review][Patch] **§0's citation-drift note doesn't acknowledge the original citation was wrong at baseline, not just later-drifted** [§0] — the "NOT gated by 18.2" section cites `ModuleContext.cs:220,229` for the `core`-exclusion `.Where(n => …)` pattern. At `baseline_commit` (86b35c2) that pattern actually sits at lines 205 and 214, not 220/229 — inaccurate before Story 18.2 touched anything. **Applied:** a correction note added directly under the citation-drift note in §0, stating the original citation was already imprecise at baseline.

- [x] [Review][Patch] **§5a's formal grammar contradicts its own prose on whether `separator` is optional** [§5a] — the BNF (`entry := WS* "-" SP+ target SP* separator SP* description? EOL`) marks only `description` as optional; `separator` has no `?`. The next bullet says "the separator **and** the description [are optional] (E8 has neither — a link-only line is a valid entry)." **Applied:** BNF changed to `(separator SP* description?)?`, grouping separator and description under one optional unit, with an inline note explaining the fix.

- [x] [Review][Patch] **§3a/§3b overstate what the dangling-link evidence proves** [§3a, §3b] — the Completion Notes asserted E4's 64% dangling-link rate "**proves**" it isn't skill output and "**conclusively retires**" E2's hand-authored hypothesis. That only rules out literal hallucination, not *stale* skill output (§6e's own failure mode). **Applied:** both §3a's bullet and §10 item 3 rewritten to the accurately-scoped claim ("consistent with hand-authorship and inconsistent with fresh skill output"), each with an inline note recording the softening. AC #1's bottom line is unaffected — independently supported by E11 and §4.

- [x] [Review][Patch] **`DiagnosticAnchorRoot` citation points to the wrong file** [References, §6d] — attributed to `src/SpecScribe/AdapterDiagnostic.cs`, but `enum DiagnosticAnchorRoot` is actually declared in `src/SpecScribe/DiagnosticsTemplater.cs:25`. **Applied:** correction appended to the References entry.

- [x] [Review][Defer] **D2 ("docs landing page") is scored "deferred, not rejected" but left with no concrete follow-on path** [§7, §8] — deferred, pre-existing scope gap. §7 calls D2 "the only option that closes §5's gap" (generic docs listed nowhere), yet §8 gives it one sentence ("D2, sequenced after") with no story number or AC, unlike the blurb-cascade follow-on. Acceptable for a ranking spike whose job was to rank, not seed — but the owner should seat a D2 follow-on story when ready.

- [x] [Review][Defer] **The §5/§6d entry grammar and failure taxonomy are not path-complete despite being framed as "closed"/"every row is complete"** [§5, §6d] — deferred, belongs to the follow-on implementation story. 13 edge cases in the parseable-contract deliverable have no mapped behavior/category/wording: case-insensitive lookup collisions between genuinely distinct same-named-differently-cased files, multiple links on one entry line, malformed/unbalanced link syntax, symlinks, backslash/drive-letter hrefs, leading-slash rooted hrefs (rejected but uncategorized, unlike every other drop case), empty href `[text]()`, URL-decode failure, an entry linking to another `index.md`, F11's tie-break comparison basis, F9/F10 evaluation order, non-ASCII whitespace in the grammar's `SP` token, soft-wrapped continuation lines, and underscore-delimited emphasis in descriptions. None of these change AC #1/#2's conclusions; they're implementation-detail gaps expected to surface once the follow-on story actually builds a parser against this contract. Recommend the follow-on story treat this list as its edge-case starting point.

**Dismissed as noise (6):** AC #1's "repo-to-repo inconsistencies" answered mostly via self-generated samples rather than confirmed wild `bmad-index-docs` output — already transparently disclosed in §4's own "methodological honesty" note; E10's `bmad-document-project` provenance asserted by pattern-match rather than confirmed against that skill's own spec — reasonable inference, doesn't affect the conclusion; Task 7's verification command needing surrounding File-List prose to be meaningful — stylistic, and the File List itself is independently confirmed accurate; several evidence-ledger byte counts/hashes not independently re-verified by this review — self-disclosed reviewer-coverage limitation, not a story defect; the "7 loose files" → "6" self-correction — already caught and fixed by the story itself; an incomplete scratchpad diff file handed to one reviewer during this review — a review-tooling artifact, confirmed not to reflect any actual gap in the story's own File List.

## Dev Notes

### Spike constraints (load-bearing)

- **Reading + sampling, not building.** Evidence comes from `SKILL.md`, real `index.md` samples, and
  `src/SpecScribe/*.cs`. If you catch yourself writing a parser or adding `Frontmatter.Description`, stop.
- **A negative or uncomfortable result is a deliverable.** "The format is not stable enough to parse; recommend
  D0 and treat `index.md` as optional enrichment" is a completely valid outcome of this spike and satisfies
  both ACs. Do not manufacture a contract that the samples do not support.
- **Do not conflate the tool's docs with a downstream project's usage** (18.1's repeated lesson, and §3 is
  exactly that trap).
- **NFR8** [epics.md:137]: *"Insight surfaces and guidance affordances … are framework-agnostic in shared
  rendering: framework-specific content flows through the adapter contract, and surfaces degrade gracefully —
  absent, not broken or misleadingly empty — when a methodology lacks the corresponding artifact."* A repo
  with no `index.md` is honest absence.
- **No hover-only meaning, no color-only meaning.** If the recommendation is D1, that constraint must be
  addressed head-on, not skipped.

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md §AD-1] — one shared projection/rendering core. Parsed blurbs become
  host-neutral view-model data; no surface re-parses `index.md`.
- **AD-2** [§AD-2] — host-neutral view models are the core↔adapter contract. A blurb is decided in C# and
  handed to adapters; nothing is composed in TypeScript.
- **AD-4** [§AD-4] — optional insight providers enrich but never own baseline success. A missing, malformed,
  or unreadable `index.md` must never fail or block generation. This is the AD the whole recommendation lives
  under, and Task 6's fork question turns on whether it already covers this input class.
- **NFR8** [epics.md:137] — absence is absent, not broken or misleadingly empty.

### Anti-patterns to prevent

- Assuming `SKILL.md`'s OUTPUT FORMAT block is a specification. It is one example, produced by a prompt, with
  nothing enforcing it — and BMad's own `docs/index.md` already violates it (§3).
- Concluding "the format is consistent" from a single sample, or from samples this session generated itself
  (one LLM, one day, one prompt — that measures nothing about repo-to-repo variance).
- Treating `index.md` as the *only* possible blurb source. `ExtractAdrSummary` already solves the same problem
  with no external dependency (§4b); a recommendation that never mentions it is incomplete.
- Proposing a docs landing page without resolving what happens when the folder already has an `index.md` — the
  ADR landing's `landingPathAlreadyWritten` guard exists precisely because that case bites (§6).
- Routing blurbs through `ModuleContext` / `ModuleDoc` / `BmadModule`. `index.md` is a `core` convention and
  module-independent; wiring it to module identity re-creates the defect Story 18.2 is fixing.
- Reviving the deliberately-removed home-index doc bands, or extending `KnownIndexGroups`, without naming the
  comments that forbid it (§5).
- Listing fallback behaviors without a diagnostic category and drafted wording (18.1's review caught exactly
  this omission).
- Recording external evidence without URL + retrieval date + commit SHA (18.1's review called this fatal).
- Writing sample `index.md` files into a shared `main` without the owner's go-ahead (Open Question 1).

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/18-3-bmad-index-docs-contract-spike.md`
- Sprint key: `18-3-bmad-index-docs-contract-spike`
- Epic 18 story map: 18.1 `review` (spike, done) · **18.2 `ready-for-dev`/`in-progress`** (identity foundation —
  status drift between the story file and `sprint-status.yaml`; likely a concurrent session) · **18.3 (this
  story)** · 18.4 `backlog` (consumes this contract for its blurb half) · 18.5 `backlog` (gated by 18.2).
- **18.3 does not depend on 18.2** and does not touch its files. But 18.2 is actively editing `ModuleContext.cs`
  — read it, do not write it, and expect it to move under you (CLAUDE.md shared-`main` conditions).
- No `src/`/`tests/` touches expected. No golden-fingerprint movement expected — if `GoldenContentFingerprint`
  moves during this story, it is a concurrent session's, not yours; record whose and do not re-baseline.
- No ADR file expected unless Task 6 concludes a genuine fork.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` §"Epic 18: BMad Module & Expansion Coverage Exploration", §"Story 18.3: BMad Index-Docs Contract Spike", §"Story 18.4: Forged Ideas List Page", and the "Stories 18.3–18.4 added 2026-07-19" seating comment] — verbatim ACs above, and 18.4's stated dependency on this contract.
- [Source: `_bmad-output/planning-artifacts/epics.md:137`] — NFR8 exact wording. (Note: 18.1 cited NFR8 at `:99`, which its code review proved wrong at every commit; `:137` verified at `baseline_commit`.)
- [Source: `.claude/skills/bmad-index-docs/SKILL.md`] — the entire emitter specification: EXECUTION, OUTPUT FORMAT (example, not grammar), VALIDATION.
- [Source: `_bmad/core/module-help.csv:6`, `_bmad/_config/skill-manifest.csv:9`, `_bmad/_config/files-manifest.csv:223`] — `bmad-index-docs` belongs to `core`, present in every BMad install.
- [Source: `src/SpecScribe/ModuleContext.cs` — `ModuleDoc`, `BmadMethodDocs`, `GameDevStudioDocs`, `DocsFor`, and the `core` exclusion in `Detect`] — the incumbent 8-filename hardcoded blurb mechanism, and the module SpecScribe refuses to detect.
- [Source: `src/SpecScribe/SiteNav.cs` — `SiteNav.Build`'s module-doc loop and `QuickLinks`] — where `ModuleDoc.Description` enters the view model; also the first-wins-plus-`Skipped`-diagnostic precedent for duplicate resolution.
- [Source: `src/SpecScribe/HtmlRenderAdapter.cs` — `AppendKeyViewsBand`] — the one place a doc description renders today: `data-tooltip` on a quick-link pill.
- [Source: `src/SpecScribe/SiteGenerator.cs` — `ExtractAdrSummary`, `CollapseSummary`, `IsDecorativeLine`, `AdrContextHeadingPattern`] — the shipped content-derived blurb extractor; the D0 alternative.
- [Source: `src/SpecScribe/SiteGenerator.cs` — `RegenerateAdrs`, the `landingPathAlreadyWritten` guard and the synthesized-landing block] — the model for a docs landing page and the index-as-page collision.
- [Source: `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs:702-706`] — the existing regression for an ADR-root file literally named `index.md`.
- [Source: `src/SpecScribe/Frontmatter.cs`] — the modelled frontmatter fields; **no `Description`** today.
- [Source: `src/SpecScribe/MarkdownConverter.cs` — `Convert`, `ExtractFirstH1`] — title resolution; the frontmatter→H1 cascade a description field would mirror.
- [Source: `src/SpecScribe/DocModel.cs`] — the per-page model a blurb would attach to.
- [Source: `src/SpecScribe/ListRow.cs`] — Story 10.8's shared list-row grammar; mandatory for any list-shaped recommendation.
- [Source: `src/SpecScribe/AdapterDiagnostic.cs`] — the closed five-value `AdapterDiagnosticCategory` vocabulary and the source-root anchoring contract. **[Review][Patch] correction, 2026-07-28:** `enum DiagnosticAnchorRoot` (`None`/`Source`/`Adr`/`Repo`) is declared in `src/SpecScribe/DiagnosticsTemplater.cs:25`, not here — `AdapterDiagnostic.cs` only references `DiagnosticAnchorRoot.Source` as a default parameter value.
- [Source: `src/SpecScribe/DashboardViewBuilder.cs` — `KnownIndexGroups`, `IsWellKnownTopLevelFolder`] — proof the home-index doc bands were removed on purpose, plus the explicit do-not-extend warning.
- [Source: `src/SpecScribe/SiteGenerator.cs` — `EnumerateSourceFiles`, `GenerateOneInternal`, `IsIgnored`] — only `*.md` under SourceRoot becomes a page; the `_docs` key space a parsed href must resolve into.
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` §AD-1, §AD-2, §AD-4] — shared core, host-neutral view models, and the additive/non-blocking rule for optional providers.
- [Source: `docs/adrs/0015-bmad-module-identity-open-world-and-multi-valued.md`] — **Accepted** 2026-07-26; its citation policy (symbols over line numbers) is adopted above. Its subject matter is deliberately out of this story's scope.
- [Source: `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md`, §Review Findings] — the house spike shape, and the specific review failures this story is written to avoid: unrecorded external evidence, missing diagnostic categories/wording, falsified `git status` claims, and drifting line-number citations.
- [Web: `raw.githubusercontent.com/bmad-code-org/BMAD-METHOD/main/docs/index.md`, fetched 2026-07-26] — the only in-the-wild `index.md` located during create-story; diverges from `SKILL.md`'s documented format on five counts (§3) and is **plausibly hand-authored** — reconfirm and classify.
- [Web: `mcpmarket.com/tools/skills/documentation-indexer-9`, `deepwiki.com/bmad-code-org/BMAD-METHOD/12-core-skills-reference`, searched 2026-07-26] — third-party descriptions of the skill; both restate `SKILL.md` and add no format detail. No independent grammar exists upstream.
- **Memory:** [[create-story-elicit-visual-intent]] (named directions up front — §10's D0–D3), [[adr-creation-trigger-gap-epic-10-retro]] (propose, don't bury — Task 6), [[shared-main-concurrent-edit-loss-verify-after-edit]] (18.2 is editing `ModuleContext.cs`), [[owner-verify-iterate-then-epic-end-review-workflow]] (review scoped by File List at epic end).

### Git intelligence summary

No `index.md` has ever existed in this repo (`find` across the tree, and no `index.md` in git history under
`src/`, `docs/`, or `_bmad-output/`), and `src/` contains no code that reads one — the only `index.md` mentions
are `SiteGenerator`'s two comments about a stray file occupying the ADR landing slot and the one webview
regression test for it. This spike starts from a genuinely clean slate on the sample side.

Recent commits are Epic 20/22/23/25 work plus 18.1's code review and ADR 0015's ratification — none touch the
doc-rendering or blurb paths this story studies, with one exception that matters operationally: **Story 18.2 is
in flight against `ModuleContext.cs`**, the file §"This story is NOT gated by 18.2" cites. Per CLAUDE.md's
shared-`main` conditions, read it and expect it to move; never `git reset`/`checkout`/`clean` to tidy up.

## Dev Agent Record

### Agent Model Used

claude-opus-5 (Claude Code, `bmad-dev-story`), 2026-07-27.

### Debug Log References

No production code ran. Evidence is (a) reads of `src/**` at working-tree state, (b) `gh`/`curl` fetches of
public GitHub content, (c) two local executions of `bmad-index-docs` by this agent, written to the session
scratchpad per Open Question 1's stated default. Scratchpad path (session-scoped, not durable):
`…/scratchpad/18-3-samples/`. Every sample's verbatim bytes are reproduced below, so the finding survives the
scratchpad's deletion — this is the explicit fix for Story 18.1's "unreproducible empirical basis" review failure.

### Completion Notes List

---

# THE CONTRACT — `bmad-index-docs` / `index.md` as a blurb source for SpecScribe

**Bottom line, stated first:** *Do not build SpecScribe on `index.md`.* The recommendation is **D0
("No dependency")**. The format is not merely inconsistent — the specific field SpecScribe wants (the
description) is the **least stable part of the file**, the filename `index.md` does **not identify its
producer**, and in this repo's own layout an `index.md` is **not even visible** to the generator at the one path
the tooling would most naturally write it to. `index.md` is retained only as *optional enrichment* where it
happens to be visible. AC #1 is answered "**neither parse as-is nor request a stricter mode** — the third
answer"; AC #2's seam is **D0 + a narrow D2 follow-on**, scoped below.

---

## 0. Evidence ledger (URL / path + retrieval date + commit SHA)

Story 18.1's code review called unrecorded external evidence fatal. Every external claim below traces here.

| # | Artifact | Source | Retrieved | Pin |
|---|---|---|---|---|
| E1 | `bmad-index-docs/SKILL.md` | `.claude/skills/bmad-index-docs/SKILL.md` | 2026-07-27 | sha256 `a855d7060414e73ca4fe8e1a3e1cc4d0f2ce394846e52340bdf5a1317e0d234a` — **matches `_bmad/_config/files-manifest.csv:223` exactly**; installed copy provably unmodified |
| E2 | BMAD-METHOD `docs/index.md` | `raw.githubusercontent.com/bmad-code-org/BMAD-METHOD/main/docs/index.md` | 2026-07-27 | 3718 bytes, sha256 `94b7133e10d4ffd4d537fe8edb77a04f47073c58d6f1012cb312c1f3b0ec9c49`; last commit touching path `0dbfae675b96a0567161172a5d218d6f5f6c3196` (2026-04-20) |
| E3 | BMAD-METHOD `docs/fr/index.md` | same repo/branch | 2026-07-27 | 4979 bytes; translation of E2 |
| E4 | GDS `docs/how-to/index.md` | `bmad-code-org/bmad-module-game-dev-studio`, branch `main` | 2026-07-27 | 1915 bytes, sha256 `34747b6f37f521ba…`; introduced by commit `f01fcbfdb1d638251e5d61bff6a5c81ca9b6e801` (2026-02-08); repo HEAD `f51d4001e4c3947ccd177c83957aaaf9deb0ecb7` |
| E5 | GDS `docs/explanation/index.md` | same | 2026-07-27 | 686 bytes |
| E6 | GDS `docs/reference/index.md` | same | 2026-07-27 | 1323 bytes |
| E7 | GDS `docs/tutorials/index.md` | same | 2026-07-27 | 1788 bytes |
| E8 | `cregis-dev/apex` `_bmad-output/index.md` | GitHub contents API, default branch | 2026-07-27 | 2333 bytes — a **real downstream BMad project's own output-folder index** |
| E9 | `cregis-dev/apex` `docs/index.md` | same | 2026-07-27 | 1521 bytes |
| E10 | `rustrak/rustrak` `docs/index.md` | same | 2026-07-27 | 6764 bytes — output of a **different** BMad skill (self-identifies `workflow_version: 1.2.0`, `Scan level: deep`) |
| E11 | GitHub-wide code search | `gh search code 'path:index.md "# Directory Index"'` and `'"## Subdirectories" path:index.md'` | 2026-07-27 | **0 results each.** Control query `'"# Directory Index"'` (unscoped) returns hits, so the search works and the negative is real |
| E12 | Local run A over `docs/` | this agent executing E1 | 2026-07-27 | verbatim below |
| E13 | Local run B over `docs/` (same folder, second run) | this agent executing E1 | 2026-07-27 | verbatim below |
| E14 | Local run C over `_bmad-output/planning-artifacts/` | this agent executing E1 | 2026-07-27 | verbatim below |

**Citation-drift note.** `baseline_commit` is `86b35c2`; HEAD at implementation was `32fd282`. Story 18.2 landed
in between and **moved one citation this story depended on**: §"NOT gated by 18.2" cites `ModuleContext.cs:220,229`
`.Where(n => !string.Equals(n, "core", …))`. That code no longer exists in that form. Per ADR 0015's symbol-first
policy the current anchor is **`ModuleContext.ReservedModuleNames` / `ModuleContext.IsReservedModuleName`**
(ADR 0015 Decision 1a). The conclusion is *unchanged and strengthened* — see §1.

**[Review][Patch] correction, 2026-07-28:** the `:220,229` line numbers were also wrong at `baseline_commit`
itself, before Story 18.2 touched anything — the `.Where(n => …)` pattern actually sat at `ModuleContext.cs:205`
and `:214` at `86b35c2`. Not just drift from 18.2's rewrite; the original citation was imprecise. Immaterial to
the conclusion (§1's finding stands either way), but recorded here so the drift note doesn't imply the citation
was ever exact.

---

## 1. `core` ownership and the 18.2 gate — CONFIRMED, and the exclusion got stronger

- `_bmad/core/module-help.csv:6` — `Core,bmad-index-docs,Index Docs,ID,…` ✅
- `_bmad/_config/skill-manifest.csv:9` — `"bmad-index-docs",…,"core","_bmad/core/bmad-index-docs/SKILL.md"` ✅
- `_bmad/core/` holds **only** `config.yaml` + `module-help.csv` — no materialized skill body ✅ (bodies are IDE-projected)
- `ModuleContext` still excludes `core` — now via `ReservedModuleNames = { "core", "custom", "scripts" }` plus any
  `_`-prefixed name, and the doc comment states a reserved name is **skipped SILENTLY — it is not an error**.

⇒ **18.3 was not gated by 18.2** (18.2 is now `review`, so the question is moot in practice, but the
*architectural* point stands and the follow-on story inherits it): `index.md` is a cross-module `core`
convention. Consuming it must **not** route through `ModuleContext.DocsFor` / `BmadModule` / `ModuleDoc`.
The irony in the story text is now sharper, not weaker: SpecScribe would be adopting a convention from the one
module it *deliberately and silently* refuses to model.

---

## 2. **NEW, load-bearing finding: `index.md` is a contested filename across ≥3 BMad skills**

This was not in the story's framing and it changes AC #1's answer. Grepping the installed skill set for
`index.md` shows **three distinct writers**:

| Skill | Module | Writes | Format |
|---|---|---|---|
| `bmad-index-docs` | core | `index.md` in a *target folder* | `- **[file.ext](./file.ext)** - 3-10 words` (prose spec, no serializer) |
| `bmad-document-project` | BMM | **`{project_knowledge}/index.md`** — a *master documentation index* | Template-driven: `- [index.md](./index.md) - Master documentation index` — **ASCII hyphen, no bold, filename as link text** |
| `bmad-shard-doc` | core | an `index.md` in the shard destination folder | unspecified in SKILL.md |

`_bmad/bmm/config.yaml` sets `project_knowledge: "{project-root}/docs"`. **So in this very repo,
`bmad-document-project` writes `docs/index.md` — the exact path a `bmad-index-docs` run over `docs/` would also
target.** They collide, silently, last-writer-wins.

E10 (`rustrak/rustrak docs/index.md`) is the live proof: it is `bmad-document-project` output, self-identifying
as `> Generated: 2026-03-10 | Scan level: deep | workflow_version: 1.2.0`, and it contains bullet lines shaped
`- **Type:** Turborepo Monorepo — 6 parts`. **A lenient `- **…** — …` entry regex parses that as an entry with
link text "Type:" and description "Turborepo Monorepo — 6 parts".** That is not a hypothetical mis-parse; it is
the first content section of a real file at the canonical path.

⇒ **The filename `index.md` carries no information about which generator produced it, or whether a generator
produced it at all.** Any contract keyed on the filename is keyed on the wrong thing. This alone disqualifies
"parse as-is."

---

## 3. Format survey — 8 wild samples, 5 distinct shapes, **0 conforming**

Not one sample matches `SKILL.md`'s OUTPUT FORMAT. Not one has `# Directory Index`, `## Files`, or
`## Subdirectories`. **E11: those markers do not appear in any `index.md` on GitHub at all.**

| Sample | H1 | Frontmatter | Entry shape | Separator | Descriptions |
|---|---|---|---|---|---|
| **Documented (E1)** | `# Directory Index` | none | `- **[file.ext](./file.ext)**` | ASCII `-` | 3-10 words |
| E2 BMad docs | *none* (Docusaurus fm) | `title`+`description` | `- **[Title](./path.md)**` | **em-dash `—`** | full sentences |
| E4 GDS how-to | `# How-To Guides` | `title`+`description` | `- **[Title](./path.md)**` | **em-dash** | ~6-9 words |
| E5/E6 GDS expl./ref. | semantic H1 | `title`+`description` | `- **[Title](./path.md)**` | **em-dash** | ~6-9 words |
| E7 GDS tutorials | `# Tutorials` | `title`+`description` | mixed — **11 bullets with NO link at all** | em-dash | mixed |
| E8 apex `_bmad-output` | `# Apex Gateway BMAD Output` | none | `- [Title](./path.md)` — **no bold** | *(none)* | **ZERO descriptions on any entry** |
| E9 apex `docs` | `# Apex Gateway Documentation` | none | `- [Title](./path.md)` — no bold | **ASCII `-`** | Chinese, ~4-8 words |
| E10 rustrak (other skill) | `# Rustrak — …Index` | none | `- **Key:** value` + tables | em-dash | n/a |

**Five separate, independently fatal divergences:**

1. **Separator is not the documented one.** Documented ASCII `-`; 6 of 8 use an **em-dash `—`**; E9 uses ASCII `-`.
   Both must be accepted, which means a description may not contain an unescaped separator — and E10's
   `Turborepo Monorepo — 6 parts` proves descriptions *do*.
2. **Bold-wrapping is optional in practice.** E8/E9 — the two samples from a real downstream BMad project — are
   `- [Title](./x.md)` with no `**`. A grammar requiring bold rejects exactly the real-world case.
3. **Link text is the human title, not the filename.** `SKILL.md`'s example uses `filename.ext` as link text; not
   one wild sample does. So link text is **not** a fallback identifier for the target.
4. **Descriptions are optional and frequently absent.** E8 — a real `_bmad-output/index.md`, structurally the
   closest analogue to what SpecScribe would consume — carries **zero** descriptions across all 22 entries. A
   perfect parser extracts nothing from it.
5. **Headings are semantic, never `## Files`/`## Subdirectories`.** Heading structure cannot be used to locate
   entry blocks, so a parser must scan all list items everywhere — which is what makes E10's `- **Key:** value`
   collision unavoidable rather than filterable.

### 3a. Entries that point at nothing — measured, in BMad's own org

E4 (`bmad-code-org/bmad-module-game-dev-studio`, `docs/how-to/index.md`) lists **11 local `./` entries**. The
folder actually contains **4** `.md` files besides `index.md` (`setup-unity`, `setup-unreal`, `setup-godot`,
`sprint-planning` — verified via the contents API at HEAD `f51d4001`).

⇒ **7 of 11 in-folder entries (64%) are dangling links**, each with a `(coming soon)` description. Plus 3
`../reference/*.md` entries that escape the index's own folder.

Two consequences:

- **This is consistent with hand-authorship and inconsistent with fresh skill output — not literal proof E4 was
  never skill-generated.** `SKILL.md` Step 1 is *"List all files and subdirectories in the target location,"* and
  a directory listing cannot invent entries for files that were never created — so E4's 11 entries against 4
  real files rules out a *fresh* run of the skill. It does not, on its own, rule out a *stale* one (the skill ran
  once, the referenced files were later renamed or deleted — the same failure mode §6e treats as first-class for
  `index.md` in general); neither repo's git history was checked to confirm those files never existed.
  **[Review][Patch] softened, 2026-07-28** — was "proves E4 is not skill output." The same caveat applies to E2:
  upstream `docs/` contains `404.md`, `_STYLE_GUIDE.md`, `roadmap.mdx` and 8 subdirectories, and `docs/index.md`
  links **2** local files and lists none of the rest — strong evidence against a fresh skill run over that
  folder, not a conclusive retirement of every hand-authorship-vs-stale-output possibility.
- **The dangling-link rate is the default, not the edge case.** Any consumer must resolve by **file existence**,
  never by trusting the href. (E5/E6 are, by contrast, complete and accurate 3/3 indexes — so the failure is not
  universal, it is unpredictable, which is worse for a contract.)

### 3b. **No confirmed skill-generated `index.md` exists — anywhere I could reach**

Searched: the whole repo + full git history (zero, as the story predicted); BMad's own two org repos (all 8
samples are hand-authored Docusaurus pages — bulk human docs commits, admonitions, external links, command
tables, phantom entries); four downstream repos carrying real `_bmad/` installs; and GitHub-wide for the
documented markers (E11: **0 hits**).

Per the story's own instruction, this is the AC #1 answer: *"If it cannot find a single confirmed
skill-generated `index.md` in the wild, that itself is the AC #1 answer, and it argues hard for 'request a
stricter emission mode' over 'parse it as-is.'"* — I reach the same premise and one step further (§7).

---

## 4. Determinism — the measurement that decides the story

Two runs of E1 over the **same** folder (`docs/`), same agent, same session, minutes apart.

| Property | Result |
|---|---|
| Entry **hrefs** (common set) | **25 / 25 identical** |
| Entry **descriptions** | **0 / 25 byte-identical** |
| **File set** | differed by 2 — run A emitted a whole `### live/` section (2 entries); run B omitted the subdirectory entirely |
| Structure | A sorted `README.md` last (ASCII: digits before letters); B sorted it **first**. B inserted a prose paragraph *inside* the `### adrs/` block — an unparseable line in entry position |

**This is the whole argument in one line: the href is stable and SpecScribe already knows it from the
filesystem; the description is the only thing SpecScribe wants, and it is 0% reproducible.**

**Methodological honesty (this bound is optimistic, not pessimistic).** These two runs share one model, one day,
one prompt, and run B had run A in context — the story's own anti-pattern list warns that this "measures nothing
about repo-to-repo variance." Correct. It measures *run-to-run* variance under maximally favourable conditions,
and even there the description reproducibility is **zero**. Independent runs, different models, different days
would only be worse. A same-session 0/25 is a floor, and the floor is already disqualifying.

---

## 5. THE ENTRY GRAMMAR (AC #1 deliverable)

Pinned as required, and **explicitly scoped to enrichment-only use** (§7). Written surface-agnostically so
Story 18.4 can consume it without inheriting a surface decision.

### 5a. Recognized entry line

```
entry     := WS* "-" SP+ target SP* (separator SP* description?)? EOL
target    := boldlink | plainlink
boldlink  := "**" "[" text "]" "(" href ")" "**"
plainlink := "[" text "]" "(" href ")"
separator := "—" | "–" | " - "        ; em-dash, en-dash, or SPACE-hyphen-SPACE
description := <rest of line, trimmed>
```
*(**[Review][Patch] fix, 2026-07-28:** `separator` is grouped with `description` under one trailing `?` — both
optional together, matching the very next bullet's E8 case (link-only line, no separator, no description). The
original BNF marked only `description` optional, contradicting that bullet.)*

- **Required:** the leading `-` list marker, and a markdown link whose href is present and non-empty.
- **Optional:** the `**` bold wrapper (E8/E9 omit it); the separator **and** the description (E8 has neither —
  a link-only line is a valid entry with `Description = null`).
- **`" - "` requires surrounding spaces.** A bare `-` is not a separator: filenames and titles routinely contain
  hyphens (`sprint-change-proposal-2026-07-10`), and `SKILL.md`'s own example link text is `filename.ext`.
- **First separator wins**, scanning left to right *after* the closing `)` of the link. Never scan the whole
  line — E4's link text contains a hyphen (`Set up a Unity project with BMGD`) and E2's contains bold runs.
- **`text` is discarded.** It is the human title in every real sample, never a reliable filename (§3.3).

### 5b. Explicitly ignored (never an entry)

Frontmatter (YAML `---` fenced, at file start only); **all headings**; prose paragraphs; markdown tables;
Docusaurus admonitions `:::note` / `:::tip` and their bodies; HTML comments; fenced code blocks; blockquotes;
nested/indented list items; `---` horizontal rules; **any bullet with no markdown link** (E7 has 11); and — the
one that matters — **any bullet whose "link" is a bold key-colon pair** `- **Key:** value` (E10).

### 5c. Description handling

- Trim; collapse interior whitespace; strip inline markdown links → their text, and strip `*` / `` ` `` — i.e.
  **reuse `SiteGenerator.CollapseSummary` verbatim**, which already does exactly this and is already truncating
  ADR summaries to 160 chars on a grapheme-cluster boundary. Do not write a second collapser.
- **Do not enforce the 3-10 word bound.** E2 uses full sentences, E9 uses Chinese (word counting is wrong for
  CJK anyway). Truncate at `CollapseSummary`'s existing 160 and let the surface ellipsize.
- Empty after collapsing ⇒ treat as **absent**, never as an empty blurb (NFR8).

### 5d. Inconsistencies observed, with parse impact

| # | Observed in | Impact |
|---|---|---|
| I1 | separator em-dash (E2,E4-E7) vs ASCII (E9) vs absent (E8) | grammar must accept all three ⇒ a description containing ` — ` truncates at the wrong point |
| I2 | bold absent (E8,E9) | requiring `**` rejects the real downstream case |
| I3 | link text = human title, never filename (all 8) | no filename fallback when the href fails to resolve |
| I4 | zero descriptions (E8) | a valid, well-formed index yields **no blurbs at all** |
| I5 | 64% dangling hrefs (E4) | existence-resolution is mandatory, not defensive |
| I6 | `../` escapes (E4,E7), directory hrefs `./planning/epics/` and `../how-to/` (E7,E8), non-`.md` href `./implementation/sprint-status.yaml` (E8) | three separate non-page targets in real files |
| I7 | unlinked bullets (E7 ×11), bold-key-colon (E10) | false-positive entries from another skill's output |
| I8 | semantic headings everywhere (all 8) | cannot scope the scan by heading |
| I9 | two-level nesting (E14) | **`SKILL.md` has no grammar for it** — its OUTPUT FORMAT shows exactly one `### subfolder/` level; my run C had to invent `### briefs/brief-SpecScribe-2026-07-05/`. `####`, or flattening, would have been equally defensible |
| I10 | run-to-run description churn (E12 vs E13) | 0/25 reproducible |

### 5e. AC #1's explicit question: parse as-is, or request a stricter emission mode upstream?

**Neither.** Both options presuppose the file is worth depending on.

- *Parse as-is* is refuted by I1-I10, and decisively by §2: the filename does not identify the producer, so a
  parser cannot know whether it is reading `bmad-index-docs`, `bmad-document-project`, `bmad-shard-doc`, or a
  hand-written Docusaurus landing.
- *Request a stricter mode upstream* is the better of the two and worth **proposing as a courtesy**, but it
  cannot be depended on and must not gate SpecScribe: (a) `.claude/skills/` is installer-managed and hash-pinned
  (`files-manifest.csv:223`, verified E1) — SpecScribe cannot ship the fix, and an edit would be overwritten on
  the next BMad update; (b) every already-generated `index.md` in the world stays in the old shape forever, since
  the skill never re-runs itself; (c) a stricter *prompt* is still a prompt — E12/E13 show the same prompt
  producing 0/25 identical descriptions, so tightening wording does not buy determinism; (d) it would not fix
  §2, which is a filename-ownership collision between three skills, not a formatting problem.

**If a request is filed upstream, the highest-value ask is not format strictness — it is a machine-readable
producer marker** (e.g. a frontmatter `generator: bmad-index-docs` key). That fixes §2, which is the actual
blocker. Format strictness without it fixes nothing.

---

## 6. Path resolution and failure taxonomy (AC #1 + AC #2)

### 6a. The resolution chain, as the code actually is

1. **Parse** href from the entry; URL-decode; strip any `#fragment` / `?query`.
2. **Reject non-local** immediately: absolute `http(s):`, protocol-relative `//`, `mailto:`, and rooted paths.
   (E5 and E2 both carry external `https://` links in entry position.)
3. **Resolve** `{folder of index.md}/{href}` lexically, collapsing `.` and `..`.
4. **Reject escapes** with the existing helper — **`PathUtil.EscapesRepoRoot`** already implements exactly this
   check (leading-segment aware, so `..cache/notes.md` is not misread as an escape). Do not hand-roll it.
5. **Normalize to the `_docs` key space.** `_docs` is keyed on `SiteGenerator.ToSourceRelative` =
   `Path.GetRelativePath(SourceRoot, fullPath)` — which on Windows yields **backslashes** — under
   `StringComparer.OrdinalIgnoreCase`. ⚠️ **A parsed href is forward-slashed; looking it up verbatim misses on
   Windows.** Convert to the platform separator (or normalize both sides) before the lookup. This is a concrete
   trap, not a theoretical one.
6. **Resolve by existence, then by claim** (never by trusting the href — I5): dedicated surface first
   (`bundle.ConsumedSourceRelatives`, special routes `epics.md`→`epics.html`, requirements, ADR records), then
   `_docs`. Only then map to output via **`PathUtil.ToOutputRelative`** (literally `Path.ChangeExtension(…, ".html")`).

### 6b. **The finding that decides AC #2's seam: `docs/index.md` is invisible to SpecScribe**

- `ForgeOptions.SourceRoot` = `{repoRoot}/_bmad-output`
- `ForgeOptions.ResolveAdrSourceRoot` = `{repoRoot}/docs/adrs`
- `EnumerateSourceFiles` = `Directory.EnumerateFiles(SourceRoot, "*.md", AllDirectories)` minus `IsIgnoredSourceFile`

`docs/` itself is **neither** root. So `docs/index.md` — the path `bmad-document-project` writes to in this repo
(`project_knowledge: {project-root}/docs`), and the natural target of a `bmad-index-docs` run over `docs/` — is
**never enumerated, never rendered, and never seen**. My own sample generation demonstrated this: the most
obvious folder to index in this repo produces a file SpecScribe cannot read.

**There are exactly two places an `index.md` can be visible:**

| Location | Today's behavior |
|---|---|
| anywhere under `_bmad-output/**` | renders as an ordinary page via `GenerateOneInternal`; **listed nowhere** (§6c) |
| `docs/adrs/index.md` | renders to `adrs/index.html` and **occupies the ADR landing slot** — already handled, already tested |

### 6c. §5 of the story CONFIRMED — and strengthened

Generic docs are rendered, tracked in the change surface (`WriteStructure`, timeline), captured for SPA/webview,
and mapped for reveal-in-editor. They appear in **no** listing: not `SiteNav.Items`, not `QuickLinks`, not any
list page, not `ProjectOutline` (epics/stories only).

Two new pieces of evidence the story did not have:

- **`BuildIndexPage(IReadOnlyList<DocModel> docs, …)` never reads `docs`.** It is a **dead parameter**, threaded
  through `HtmlTemplater.RenderIndex` → `BuildIndexPage` and every call site. `DashboardViewBuilder.Build` does
  not take it.
- **`SiteGenerator`'s `GenerateReadmeInternal` doc comment still says the README is kept out of `_docs` "so it
  never doubles up as a document-grid card."** There is no document grid — `grep -rn "document-grid" src/`
  returns that one comment and nothing else.

Both are fossils of the deliberately-removed home-index bands. Recorded as cleanup candidates, **not fixed**
(spike; no `src/**` changes). Neither changes the conclusion: **AC #2's docs landing page is greenfield.**

### 6d. Failure taxonomy — every mode gets a behavior, a category, drafted wording, and an anchor root

Story 18.1's review failed precisely this clause. Every row below is complete. Anchor root is
**`DiagnosticAnchorRoot.Source`** throughout — an `index.md` consumable by SpecScribe is under `SourceRoot` by
construction (§6b), so the source-relative contract on `AdapterDiagnostic.RelativePath` holds and the webview
Problems channel resolves a real path. (`DiagnosticAnchorRoot.Repo` exists since 18.2 but must **not** be used
here; it is for `_bmad/{code}/` subjects.) The five-value vocabulary is closed — no sixth is proposed.

| # | Failure | Behavior | Category | Drafted message |
|---|---|---|---|---|
| F1 | No `index.md` in the folder | **Surface absent.** No diagnostic — absence is the norm, not an event (NFR8) | *(none)* | — |
| F2 | `index.md` present, unreadable (I/O, permissions) | Skip the file; generation continues (AD-4) | `Malformed` | `"Could not read '{path}' for document descriptions; descriptions fall back to document content."` |
| F3 | Present but yields **zero** parseable entries (all prose / another skill's output) | Skip silently; fall through to derived blurbs | `Unsupported` | `"'{path}' contains no recognizable document entries; document descriptions fall back to document content."` |
| F4 | Entry href resolves to a **file that does not exist** (the 64% case, I5) | **Drop the entry.** Never emit the link | `Skipped` | `"{n} entry(ies) in '{path}' reference files that do not exist and were skipped."` — **aggregated to one diagnostic per index**, not one per entry (E4 would emit 7) |
| F5 | Href targets a **non-`.md`** file (I6 — `sprint-status.yaml`) | Drop the entry (no page exists, ever) | `Skipped` | folded into F4's aggregate count |
| F6 | Href targets a **directory** (I6 — `./planning/epics/`) | Drop the entry | `Skipped` | folded into F4 |
| F7 | Href **escapes SourceRoot** via `../` or is rooted/absolute (I6) | Drop the entry | `Skipped` | folded into F4 |
| F8 | Target is `IsIgnoredSourceFile` (dot-prefixed, `.tmp`, `.crswap`, `~$`) | Drop the entry, no report — ignored files are neither rendered nor reported anywhere else (Story 4.1 rule) | *(none)* | — |
| F9 | Target was **consumed by a dedicated surface** (`ConsumedSourceRelatives`) or is **special-routed** (`epics.md`, requirements, ADR records) | **Re-point the blurb at the dedicated surface.** Never drop, never link the generic page | *(none)* | — |
| F10 | **Duplicate entries** for the same resolved target | First-wins in file order; report once | `Skipped` | `"{n} duplicate entry(ies) in '{path}' skipped in favor of the first occurrence."` — mirrors `SiteNav.Build`'s existing module-doc precedent verbatim |
| F11 | **Conflicting** `index.md` files claiming the same target (nested indexes) | Nearest index wins (fewest path segments between index and target); tie → first alphabetically | `Skipped` | `"'{path}' description for '{target}' skipped; a nearer index.md already describes it."` |
| F12 | Description present but empty after collapsing | Treat as absent; entry still resolves its link | *(none)* | — |
| F13 | Non-UTF8 / BOM / CRLF | BOM and CRLF are handled by `MarkdownConverter.ReadAllTextShared` already; undecodable ⇒ F2 | see F2 | — |
| F14 | `index.md` is **stale** (see 6e) | Undetectable in general; per-entry existence (F4) is the honest partial mitigation | see F4 | — |

### 6e. Staleness — the honest answer is "not reliably detectable"

`index.md` is generated on demand and **never regenerated automatically**. It goes stale on the first rename.

- **mtime comparison is not sound.** An index newer than every file it references can still be wrong (a file
  renamed *before* the index was last touched for an unrelated reason); an older index can still be correct.
  Worse, mtime is not preserved by `git clone` — every file gets checkout time — so on CI the signal is noise.
  This repo already learned this: the Story 25.1 golden fingerprint was checkout-and-date dependent.
- **Entry-count vs on-disk-count is not sound either.** E4 has *more* entries than files (phantoms); E2 has
  vastly *fewer* (2 of 12). Both directions occur, so neither inequality means "stale."
- **What IS sound:** per-entry existence (F4). It catches the renamed/deleted case exactly, at the only
  granularity where the answer is knowable.

⇒ **Do not add a staleness heuristic.** Resolve every entry by existence, drop what does not resolve, report
once in aggregate. A blurb whose target still exists is served; a blurb whose target moved is dropped. That is
the honest behavior, and it degrades to "no blurb," never to a broken link.

---

## 7. Seam ranking — D0 / D1 / D2 / D3 evaluated, one picked (AC #2)

Scored on the story's six criteria. **Reader value · diff size · a11y viability · staleness exposure · closes §5's
gap · NFR8 behavior.**

### 🏆 WINNER — **D0, "No dependency"** (with a narrowly-scoped D2 follow-on, §7a)

Derive blurbs **from each document's own content**, generalizing the shipped `SiteGenerator.ExtractAdrSummary`
cascade, and add `Frontmatter.Description` as the highest-precedence source. `index.md`, where visible, becomes
**optional enrichment that overrides the derived value** — never a dependency.

**Precedence cascade** (first non-empty wins):
1. `Frontmatter.Description` — self-describing, per-doc, zero staleness. **BMad's own `index.md` already sets
   `description:` in frontmatter** (E2), as do all four GDS samples — so this is the one field the ecosystem
   demonstrably and consistently populates. `Frontmatter` models 12 fields today and has **no `Description`**.
2. A parsed `index.md` entry, where one is visible and resolves (§5 grammar, §6d taxonomy).
3. Content-derived: generalized `ExtractAdrSummary` — first paragraph under a `## Context`-class heading, else
   the post-dash tail of the H1 title, via `CollapseSummary`.
4. None ⇒ **absent** (NFR8).

**Why it wins:** it is the only option whose value does not depend on a file that (a) 0/25 reproduces the field
we want, (b) does not identify its producer, (c) is 64%-dangling in BMad's own org, (d) carries zero
descriptions in the most representative real sample, and (e) is invisible at the path the tooling writes it to.
`ExtractAdrSummary` is *already shipped and already trusted* for the ADR landing — generalizing a proven
extractor is strictly less risk than adopting an unversioned external contract. Note (2) is deliberately kept:
it satisfies AC #1's "pin a parseable contract" framing, and gives 18.4 the grammar it was promised, **without**
letting the portal's correctness depend on it.

### Why each loser lost

- **D1 "Tooltip generalization"** — ❌ **Rejected on accessibility, independently of everything else.** The one
  place a doc description renders today is `data-tooltip` on a quick-link pill
  (`HtmlRenderAdapter.AppendKeyViewsBand`). That is **hover-only**: invisible on touch, and to most keyboard and
  screen-reader users. This project does not signal meaning by hover any more than by color. Generalizing the
  *hover-only* slot would scale a defect from 8 filenames to every doc. Smallest diff, worst outcome. It also
  does nothing for §5's gap — a tooltip on a link that is not in any list is a tooltip nobody reaches.
- **D3 "Blurb on the page + local-context band"** — ❌ Lost on reader value, as the story anticipated. A
  description next to the document it describes is the least useful placement: the reader is already there and
  can read the H1 and first paragraph. The sibling-entry half is real value, but it is a strictly smaller
  version of D2 and is subsumed by it.
- **D2 "Docs landing page"** — ⚠️ **Not rejected — deferred and re-scoped.** Highest reader value, and the only
  option that closes §5's "generic docs are listed nowhere" gap. But it is a *surface* decision that D0 must
  precede: a landing page with no blurb source is an empty list, and with an `index.md`-only source it would be
  empty on every repo that has never run the skill (i.e. all of them — §3b). **D0 first, D2 second, over the
  cascade.** See §7a.

### 7a. §6's collision, answered explicitly: page, data source, or both?

**An `index.md` is the PAGE. It is never the landing page's data source, and the two never coexist.**

Concretely:
- Under `SourceRoot`: an `index.md` renders as an ordinary page via `PathUtil.ToOutputRelative`. If a docs
  landing page is later synthesized for that folder, the **`index.md` wins the slot and synthesis is
  suppressed** — precisely the `landingPathAlreadyWritten` rule `RegenerateAdrs` already implements, set **only
  on a successful write** (a file that exists but fails to render must not suppress the fallback, or the nav
  links a 404).
- `docs/adrs/index.md` already behaves exactly this way, with a shipped regression:
  `SiteGeneratorWebviewTests.AdrLandingSynthesis_DoesNotClobberAnAdrFileThatAlreadyOccupiesTheLandingSlot`
  (it even writes `"Hand-authored index."` as the fixture body).
- The blurbs an `index.md` contributes go to the **cascade** (precedence 2), which feeds *other* surfaces — doc
  pages and, later, D2's list rows for *sibling* folders. **A folder's own `index.md` never both renders as
  prose and has its data re-rendered as a list beneath itself.** That is the duplicate surface the story warned
  about, and this rule forecloses it.

### 7b. NFR8 absence rule for the winner

**With no `index.md`, nothing changes and nothing is signalled.** The cascade simply falls through to
frontmatter or content-derived text. With no blurb from *any* source, the description element is **omitted
entirely** — not an empty string, not "No description available", not a placeholder row. For D2 when it lands: a
folder with no listable docs yields **no page and no nav entry**, matching how `SiteNav` already gates every
optional surface on non-emptiness.

---

## 8. Follow-on scope boundary (AC #2's final clause)

**Story 18.7 (proposed, not yet seated) — "Per-document descriptions from a content-derived cascade."**

> **[Review][Patch], 2026-07-28: renumbered from "Story 18.6" to "Story 18.7".** At this story's own cited
> HEAD (`32fd282`) no Story 18.6 existed yet; a later, unrelated story ("Module-Aware Artifact Coverage
> Families," seated by 18.5's owner decision D4) has since shipped under that number. This proposal was never
> wrong when written — it's renumbered here only so a reader acting on it today doesn't collide with the real
> 18.6. `18.7` was unclaimed in `epics.md` as of the fix.

**Lands:**
- `Frontmatter.Description` (13th field) + its parse, mirroring the existing `Title` handling.
- `DocModel.Description`, populated by the §7 cascade in the shared core (AD-1/AD-2 — decided in C#, handed to
  adapters; nothing composed in TypeScript).
- `ExtractAdrSummary` generalized from ADR-only to any `DocModel` — renamed, moved off the ADR path, and its
  `## Context` heading pattern widened to a small ordered set of first-prose-section headings. `CollapseSummary`
  and `IsDecorativeLine` are **reused verbatim**, not reimplemented.
- The `index.md` reader: §5's grammar + §6a's chain + §6d's taxonomy, as **one** parser in the core, behind the
  cascade at precedence 2. Reads only `_bmad-output/**` (§6b).
- Diagnostics F2/F3/F4/F10/F11 with exactly the wording drafted above, `DiagnosticAnchorRoot.Source`.

**Does NOT land:**
- Any new page, nav entry, or `QuickLinkFamily` membership — that is D2, sequenced after.
- Any change to `ModuleContext` / `ModuleDoc` / `BmadModule` / `DocsFor`. `index.md` is a `core` convention and
  module-independent (§1). Routing it through module identity would re-create the exact defect 18.2 just fixed.
  The 8 hardcoded `ModuleDoc.Description` literals stay as-is; the cascade does not replace them in this slice.
- Any edit to `.claude/skills/**` (installer-managed, hash-pinned — E1).
- Extending `KnownIndexGroups` / `IsWellKnownTopLevelFolder` (explicit do-not-extend warning).
- Any staleness heuristic (§6e).
- Reviving the home-index doc bands.

**What 18.4 (Forged Ideas List Page) reuses:** the cascade and `DocModel.Description` — **not** the `index.md`
parser. 18.4's blurb half was written expecting to consume "18.3's pinned contract"; it should consume
**precedence 3 (content-derived)** as its baseline, because `_bmad-output/forge/` will have no `index.md` on any
real repo. The grammar in §5 stays surface-agnostic and is available to it, but 18.4 must not be blocked on it.
18.4 also uses `ListRow` (Story 10.8) — as would D2, whose `ListRow.Render(sb, summaryHtml, badgeHtml, chipsHtml,
primaryLinkHtml, …)` signature already carries a `summaryHtml` slot a blurb drops straight into.

**Sequencing:** 18.7 (cascade + parser) → then D2 (docs landing page) as its own story. 18.4 may run in parallel
once 18.7's `DocModel.Description` exists.

---

## 9. Task 6 — architecture fork check: **YES, and an ADR is proposed**

The question — *"is an external, LLM-generated, unversioned file an allowed **input** to generation?"* — is a
genuine cross-cutting fork, and per this project's ADR-trigger discipline it is **proposed here, not buried**.

> **[Review][Decision], 2026-07-28: premise revised.** The original version of this section framed the fork as
> "human-authored vs. LLM-generated," and code review correctly flagged that this dichotomy does not hold —
> most of SpecScribe's own inputs, including this very document, are themselves produced by an LLM through the
> BMad workflow, and are read back reproducibly once committed. The revision below replaces authorship with
> **provenance discipline** as the actual fork, which is what the evidence in §2/§4/§6e supports.

**Why AD-4 does not already cover it.** AD-4 governs *optional insight providers* that "enrich but never own
baseline success." The naive "human-authored vs. LLM-generated" framing does not survive scrutiny — a BMad
story file is LLM-drafted too. The real fork is **provenance discipline**, not authorship. Every input
SpecScribe already trusts — human-edited or LLM-drafted — is a **committed document of record**: it has one
accountable producer per artifact, it is revised deliberately (a person re-runs `create-story`/`dev-story` and
commits the result), and re-reading it twice yields the same answer because nothing regenerates it silently in
between. `index.md` fails all three, and the failure is measured, not assumed: §2 shows **at least three
different BMad skills** write the same filename with no producer marker, so a reader cannot even tell which
convention it followed; §4 measures **0/25** field-level agreement across two runs of the *same* skill over the
*same* folder — instability at authorship time, not a hypothetical regeneration risk; and §6e shows there is no
trigger that ever re-syncs it against the tree it describes, so it silently goes stale. An LLM-generated
artifact that is checked in, owned, and deliberately re-run — like a BMad story file — is not this third class;
an unowned, producer-anonymous, silently-stale byproduct is, regardless of who or what wrote it. That
precedence question is exactly what §7's cascade answers, and it is an invariant future stories will need.

**Proposed ADR 0019 — "Unowned, Producer-Anonymous Artifacts Are Enrichment-Only Inputs, Never Authoritative
Ones."** Sketch of the decision: SpecScribe may read an artifact with no accountable producer and no
re-sync trigger, but (a) it may never be the *sole* source of any rendered fact — every field it supplies must
have a reproducible fallback from a committed, owned source; (b) it is always outranked by a document-of-record
source in the same document (frontmatter beats index entry); (c) every reference it makes is resolved against
the filesystem, never trusted; (d) its absence is silent (NFR8), never signalled. Consequence: the §7 cascade's
precedence order becomes an architectural invariant rather than one story's local choice, and 18.4/18.7/D2 all
inherit it.

⚠️ **Numbering note (added by code review, 2026-07-28):** `docs/adrs/README.md`'s ADR 0021 entry records that
ADR number **0019 is also claimed-but-unwritten by Story 22.3**, for an unrelated decision (reconciling ADR
0008 vs. ADR 0009 on IR-projection architecture). That collision was discovered by Story 18.4, which ran after
this story concluded, so it was not knowable at authorship time. Left as-authored rather than renumbered —
whoever ratifies this proposal should resolve the collision against 22.3's draft (if one exists) before writing
the ADR file.

**Not written in this story** — the spike proposes, the owner ratifies (the Story 18.1 → ADR 0015 pattern).

---

## 10. Corrections to the story's own premises

Recorded so a later reader is not misled by the create-story text:

1. **§2's "`docs/` (7 loose files…)"** — `docs/` holds **6** loose `.md` files (`Epic3UXFeedback`,
   `MissingFeatures`, `SonarCloudSetup`, `Story1_4_UX_Observations`, `UserJourneys`,
   `VSCodeIntegrationRecommendations`) plus `adrs/` and the gitignored `live/`.
2. **§"NOT gated by 18.2" cites `ModuleContext.cs:220,229`** with a `.Where(n => … "core" …)` predicate. 18.2
   replaced it; the live anchor is `ReservedModuleNames` / `IsReservedModuleName`. Conclusion unchanged.
3. **§3's "plausibly hand-authored"** is strengthened by directory-listing contradiction (§3a) — the dangling-link
   evidence rules out a *fresh* skill run over the folder, upgrading from unsupported hypothesis to
   evidence-backed conclusion. **[Review][Patch] softened, 2026-07-28** — was "now proven hand-authored…upgrade
   from hypothesis to fact"; the evidence rules out fresh generation, not a stale prior run (§3a).
4. **§1's "67 lines"** — `wc -l` reports 66 (no trailing newline); the file is 67 lines of content. Immaterial.
5. **Open Question 3 is resolved:** `sprint-status.yaml` now reads 18.2 = `review`, matching its story file. No
   drift remains.
6. **The story assumed the spike's own generated samples would be the primary evidence.** They turned out to be
   *confirmatory*; the load-bearing evidence is the 8 wild samples and the GitHub-wide null result (E11).

---

## 11. Non-goals honored

No parser, no `Frontmatter` field, no `DocModel` field, no page, no nav entry, no ADR file written, no
`.claude/skills/` edit, no `KnownIndexGroups` change, no module-identity touch, no revived home-index bands, no
new authoring schema. **Zero `src/**` / `tests/**` changes** — see Task 7 verification below.

### Task 7 verification — stated honestly

```
$ git diff --stat 86b35c2 HEAD -- src tests
16 files changed, 1171 insertions(+), 104 deletions(-)   ← ALL Story 18.2's, committed in 32fd282
$ git status --porcelain -- src tests
 M src/SpecScribe/Charts.cs
 M src/SpecScribe/DashboardViewBuilder.cs
 M src/SpecScribe/EpicsView.cs
 M src/SpecScribe/EpicsViewBuilder.cs
 M src/SpecScribe/HierarchyExplorer.cs
 M src/SpecScribe/HtmlRenderAdapter.Dashboard.cs
 M src/SpecScribe/HtmlRenderAdapter.Epics.cs
?? src/SpecScribe/HierarchyExplorer.Projectors.cs
```

⚠️ **Those 8 files are NOT mine.** The working tree was **clean** when this story opened (verified
`git status --porcelain` → empty at session start); they appeared *during* this session. The file set —
a new `HierarchyExplorer.Projectors.cs` plus `Charts`, `EpicsView`/`EpicsViewBuilder`, `DashboardViewBuilder`,
and the dashboard/epics render adapters — matches the **Epic 20 hierarchy rollout (Story 20.7
`site-wide-hierarchy-rollout`, possibly with 20.9 `colorized-hierarchies-code-map-and-ownership`; both are
`ready-for-dev` and I cannot attribute between them from the file set alone)**. Per CLAUDE.md's shared-`main`
rules I read nothing into them, changed none of them, and ran no `git reset` / `checkout --` / `clean`.
`_bmad-output/implementation-artifacts/5-6-…md` and `deferred-work.md` are also modified by that session, not by me.

**This story's own File List is 2 files, both `_bmad-output/` bookkeeping.**

### ⛔ The regression suite could NOT be run — the working tree does not compile, and it is not this story's doing

Step 9 requires the full suite. I ran it and must report the result honestly rather than claim a pass:

```
$ dotnet build
tests/SpecScribe.Tests/HierarchyExplorerTests.cs(504,67): error CS1739:
    The best overload for 'Render' does not have a parameter named 'fallbackHtml'
tests/SpecScribe.Tests/HierarchyExplorerTests.cs(580,67): error CS1739: (same)
    2 Error(s)
```

**Attribution, proven rather than asserted:**

- `git show HEAD:src/SpecScribe/HierarchyExplorer.cs | grep -c fallbackHtml` → **3**. At `HEAD` the parameter
  exists, so `HierarchyExplorerTests.cs` compiles there.
- The **working-tree** `HierarchyExplorer.cs` (modified, uncommitted, not mine) has removed it, and says so in a
  new doc comment: *"**The `fallbackHtml` slot is gone.** It was owner decision D1 of Story 20.5 made concrete…"*
- `git status --porcelain -- tests/` shows `HierarchyExplorerTests.cs` is **unmodified** — the caller has not yet
  been updated to match the new signature.

⇒ The break is created **solely** by the concurrent Epic 20 session's uncommitted mid-refactor. This story
changed **zero** code files, so it cannot have caused it and cannot honestly be validated against it. Per
CLAUDE.md I did **not** fix it, did not touch `HierarchyExplorer*`, and ran no `git reset` / `checkout --` /
`clean` / `stash`. I also did not build from a temporary worktree — CLAUDE.md records that parallel worktrees are
not available on the primary machine.

**Why this does not block the story's Definition of Done:** the deliverable is a research contract in prose. No
`src/**` or `tests/**` file is touched, so there is no code change for a suite to regress. A green suite would
have told us nothing about this story, and the red one tells us nothing either — it is a fact about someone
else's in-flight work. **Recommend the owner re-run the suite once the Epic 20 session lands its test update.**

*(The canonical File List is at the end of the Dev Agent Record, below the appendix.)*

---

## APPENDIX — verbatim sample bytes

Reproduced in full so every claim above is falsifiable without the scratchpad.

### E12 — local run A, `bmad-index-docs` over `docs/` (2026-07-27)

```markdown
# Directory Index

## Files

- **[Epic3UXFeedback.md](./Epic3UXFeedback.md)** - Page-by-page UX and consistency review of the portal
- **[MissingFeatures.md](./MissingFeatures.md)** - Capabilities that do not exist yet, per journey
- **[SonarCloudSetup.md](./SonarCloudSetup.md)** - Connecting the repository to SonarQube Cloud analysis
- **[Story1_4_UX_Observations.md](./Story1_4_UX_Observations.md)** - Candidate scope expansions for Story 1.4 polish
- **[UserJourneys.md](./UserJourneys.md)** - The seven questions the portal exists to answer
- **[VSCodeIntegrationRecommendations.md](./VSCodeIntegrationRecommendations.md)** - Native VS Code integration candidates for story seating

## Subdirectories

### adrs/

- **[0001-spec-driven-development-framework.md](./adrs/0001-spec-driven-development-framework.md)** - Adopting BMAD-METHOD as the spec-driven development framework
  … [0002 … 0018 elided — 18 entries, one per ADR, same shape] …
- **[README.md](./adrs/README.md)** - Purpose and conventions for architecture decision records

### live/

- **[action-items.html](./live/action-items.html)** - Generated portal page for open action items
- **[epics.html](./live/epics.html)** - Generated portal page listing epics
```
*(27 entry lines total. `README.md` sorted LAST. `live/` — gitignored, non-`.md` — INCLUDED.)*

### E13 — local run B, same folder, minutes later

```markdown
# Directory Index

## Files

- **[Epic3UXFeedback.md](./Epic3UXFeedback.md)** - Portal-wide UX and consistency feedback, 2026-07-09 snapshot
- **[MissingFeatures.md](./MissingFeatures.md)** - Absent capabilities that leave user journeys unfinished
- **[SonarCloudSetup.md](./SonarCloudSetup.md)** - How to wire the repo to SonarQube Cloud
- **[Story1_4_UX_Observations.md](./Story1_4_UX_Observations.md)** - Accessibility and polish observations from the generated site
- **[UserJourneys.md](./UserJourneys.md)** - Seven journeys the generated portal must serve
- **[VSCodeIntegrationRecommendations.md](./VSCodeIntegrationRecommendations.md)** - Recommendations for deeper VS Code native integration

## Subdirectories

### adrs/

Architecture decision records, numbered 0001-0018, plus the landing README.

- **[README.md](./adrs/README.md)** - What an ADR is and how these are written
- **[0001-spec-driven-development-framework.md](./adrs/0001-spec-driven-development-framework.md)** - Adopt BMAD-METHOD as the SDD framework
  … [0002 … 0018 elided — same shape, every description differing from run A] …
```
*(25 entry lines. `README.md` sorted FIRST. `live/` ABSENT. A prose paragraph inserted inside the entry block.
**0 of 25 shared descriptions byte-identical to run A; 25/25 hrefs identical.**)*

### E14 — local run C, over `_bmad-output/planning-artifacts/`

```markdown
# Directory Index

## Files

- **[epics.md](./epics.md)** - Epic and story breakdown with requirements mapping
- **[sprint-change-proposal-2026-07-08.md](./sprint-change-proposal-2026-07-08.md)** - Sprint change proposal dated 2026-07-08
  … [8 further sprint-change-proposal entries elided] …

## Subdirectories

### briefs/brief-SpecScribe-2026-07-05/

- **[brief.md](./briefs/brief-SpecScribe-2026-07-05/brief.md)** - Product brief for SpecScribe

### prds/prd-SpecScribe-2026-07-05/

- **[prd.md](./prds/prd-SpecScribe-2026-07-05/prd.md)** - Product requirements document with FRs and NFRs
- **[review-rubric.md](./prds/prd-SpecScribe-2026-07-05/review-rubric.md)** - Quality review rubric companion to the PRD

### research/

- **[market-git-activity-analysis-tools-file-level-insights-research-2026-07-22.md](./research/market-…-2026-07-22.md)** - Market research on git activity analysis tools

### ux-designs/ux-SpecScribe-2026-07-05/

- **[DESIGN.md](./ux-designs/ux-SpecScribe-2026-07-05/DESIGN.md)** - UX design system and visual language
- **[EXPERIENCE.md](./ux-designs/ux-SpecScribe-2026-07-05/EXPERIENCE.md)** - UX behavior, flows, and interaction patterns
```
*(I9: `SKILL.md` has no grammar for two-level nesting; the compound `### briefs/brief-SpecScribe-2026-07-05/`
heading was invented under pressure. `.memlog.md` files correctly skipped as dotfiles. Note that `prd.md`,
`brief.md`, `DESIGN.md`, `EXPERIENCE.md` are **exactly four of the five filenames `ModuleContext.BmadMethodDocs`
already hardcodes blurbs for** — an index here would duplicate the incumbent, not extend it.)*

### E8 — `cregis-dev/apex` `_bmad-output/index.md` — the most representative real-world sample

```markdown
# Apex Gateway BMAD Output

`_bmad-output/` 用于保存 BMAD agent 工作流产出的过程文档。

这些文档保留上下文、设计推演与实施记录价值，但默认不作为当前项目事实的单一来源。

## Stories

- [Web Dashboard](./stories/web-dashboard.md)

## Planning

- [BMAD Epics](./planning/epics.md)
- [Detailed Epic Files](./planning/epics/)
- [UX Design Specification](./planning/ux-design-specification.md)
  … [5 further planning entries elided] …

## Implementation

### Story Outputs

- [7-1 PII Masking Engine](./implementation/stories/7-1-pii-masking-engine.md)
  … [7 further story entries elided] …

### Tech Specs

- [Dashboard Heatmap](./implementation/tech-specs/dashboard-heatmap.md)
  … [4 further tech-spec entries elided] …

### Test Output

- [Test Summary](./implementation/tests/test-summary.md)
- [Sprint Status](./implementation/sprint-status.yaml)
```
*(No bold. **Zero descriptions on any of 22 entries.** A directory href `./planning/epics/`. A non-`.md` href
`./implementation/sprint-status.yaml`. Semantic `##`/`###` headings. Non-English prose.)*

### E4 — GDS `docs/how-to/index.md` — the dangling-link evidence

```markdown
---
title: "How-To Guides"
description: Practical guides for specific game development tasks
---

# How-To Guides

Step-by-step guides for completing specific game development tasks with BMGD.

---

## Engine Setup Guides

Get started with your preferred game engine:

- **[Set up a Unity project with BMGD](./setup-unity.md)** — Configure Unity for full production development
- **[Set up an Unreal project with BMGD](./setup-unreal.md)** — Configure Unreal for full production development
- **[Set up a Godot project with BMGD](./setup-godot.md)** — Configure Godot for full production development

---

## Quick Flow Guides

- **[Quick Flow: Rapid prototyping](./quick-prototype.md)** — Create a playable prototype in hours (coming soon)

---

## Production Workflows

- **[Run sprint planning](./sprint-planning.md)** — Plan and track development sprints
- **[Conduct code reviews](./code-review.md)** — Review code quality (coming soon)
- **[Course correction](./correct-course.md)** — Get back on track when implementation diverges (coming soon)

---

## Testing Guides

- **[Set up automated testing](./testing-setup.md)** — Initialize test frameworks (coming soon)
- **[Design game tests](./test-design.md)** — Create comprehensive test scenarios (coming soon)
- **[Plan playtesting sessions](./playtesting.md)** — Structure your playtesting (coming soon)
- **[Performance testing](./performance-testing.md)** — Design performance testing strategy (coming soon)

---

## Reference

- **[Workflows Reference](../reference/workflows.md)** — All BMGD workflows
- **[Agents Reference](../reference/agents.md)** — All 6 BMGD agents
- **[Game Types Reference](../reference/game-types.md)** — All 24 game type templates
```
*(Folder actually contains only `setup-unity.md`, `setup-unreal.md`, `setup-godot.md`, `sprint-planning.md`
besides `index.md`. **7 of 11 `./` entries dangle.** 3 `../` entries escape the folder. Em-dash separators
throughout, against `SKILL.md`'s documented ASCII hyphen.)*

### E2 — BMAD-METHOD `docs/index.md` (excerpt; full 3718 bytes at the pinned SHA)

```markdown
---
title: Welcome to the BMad Method
description: AI-driven development framework with specialized agents, guided workflows, and intelligent planning
---

The BMad Method (**B**uild **M**ore **A**rchitect **D**reams) is an AI-driven development framework module …

:::note[🚀 V6 is Here and We're Just Getting Started!]
Skills Architecture, BMad Builder v1, Dev Loop Automation, and so much more in the works. **[Check out the Roadmap →](/roadmap/)**
:::

## New Here? Start with a Tutorial

- **[Get Started with BMad](./tutorials/getting-started.md)** — Install and understand how BMad works
- **[Workflow Map](./reference/workflow-map.md)** — Visual overview of BMM phases, workflows, and context management

## How to Use These Docs

| Section           | Purpose                                                    |
| ----------------- | ---------------------------------------------------------- |
| **Tutorials**     | Learning-oriented. Step-by-step guides …                   |
…

## Join the Community

- **[Discord](https://discord.gg/gk8jAdXWmj)** — Chat with other BMad users, ask questions, share ideas
- **[GitHub](https://github.com/bmad-code-org/BMAD-METHOD)** — Source code, issues, and contributions
```
*(Upstream `docs/` contains `404.md`, `_STYLE_GUIDE.md`, `roadmap.mdx`, `index.md` and 8 subdirectories. This
file links **2** local files and lists none of the rest ⇒ **provably not a folder index**. Also: external
`https://` links in entry position, a markdown table, Docusaurus admonitions, and an absolute `/roadmap/` route.)*

### E10 — `rustrak/rustrak` `docs/index.md` — a *different* skill's output at the same path

```markdown
# Rustrak — Project Documentation Index

> Generated: 2026-03-10 | Scan level: deep | workflow_version: 1.2.0
>
> **Primary entry point for AI-assisted development.**

---

## Project Overview

- **Type:** Turborepo Monorepo — 6 parts
- **Primary Language:** Rust (server), TypeScript (UI, client, tools)
- **Architecture:** Decoupled server + optional dashboard, Sentry SDK compatible
- **Repository:** https://github.com/rustrak/rustrak

### Quick Reference by Part

| Part | Root | Type | Tech |
|------|------|------|------|
| **server** | `apps/server/` | backend | Rust, Actix-web 4, SQLx, Tokio |
…
```
*(`bmad-document-project` output — not `bmad-index-docs` — at the canonical `{project_knowledge}/index.md`
path. The `- **Key:** value` lines are the §2 false-positive hazard: a lenient entry regex reads
`- **Type:** Turborepo Monorepo — 6 parts` as an entry.)*

### File List

- `_bmad-output/implementation-artifacts/18-3-bmad-index-docs-contract-spike.md` (modified — Status, task checkboxes, Dev Agent Record, File List, Change Log)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — `18-3-…` status transitions)

**No `src/**` or `tests/**` files.** This is a research spike; the contract in the Completion Notes is the
deliverable. The 8 modified/untracked `src/**` files present in the working tree belong to a concurrent Epic 20
session — see §11's Task 7 verification.

## Change Log

| Date | Change |
|---|---|
| 2026-07-27 | dev-story 18.3 complete. Spike executed end to end; no production code. AC #1 and AC #2 both satisfied with a **negative-for-index.md** result: recommendation is **D0 (no dependency)** with `index.md` as optional enrichment at precedence 2 of a four-level cascade. Evidence: 8 wild samples (5 distinct shapes, **0 conforming** to `SKILL.md`), a GitHub-wide null result for the documented markers, a measured **0/25 description reproducibility** across two same-folder runs, and the new finding that **`index.md` is written by ≥3 different BMad skills** so the filename does not identify its producer. Entry grammar, path-resolution chain, and a 14-row failure taxonomy (each with category + drafted wording + anchor root) pinned surface-agnostically for 18.4 and the follow-on. **ADR 0019 proposed** (LLM-generated artifacts are enrichment-only inputs). Status → review. |
| 2026-07-28 | code review (`/bmad-code-review 18.3`). Core conclusion (D0, the 0/25 determinism finding, all spot-checked symbols) survived adversarial verification unchanged. 3 decision-needed items resolved by owner: ADR 0019's numbering collision with Story 22.3 annotated in place (not renumbered); the follow-on's proposed "Story 18.6" renamed to **Story 18.7** (the real 18.6 shipped, unrelated, after this story concluded); §9's ADR argument reframed from "human- vs. LLM-authored" to **provenance discipline** (accountable producer, deliberate re-run/commit, no silent regeneration), since most of SpecScribe's own inputs are themselves LLM-generated. 4 patch findings applied: a baseline citation correction, the §5a grammar's separator-optionality fixed to match its own prose, §3a/§3b's "proves"/"conclusively retires" language softened to what the evidence actually supports, and a `DiagnosticAnchorRoot` citation corrected to `DiagnosticsTemplater.cs`. 2 items deferred to the follow-on story (D2's missing concrete path; 13 unmapped edge cases in the entry grammar/failure taxonomy) — see `deferred-work.md`. Status → done. |

## Open Questions for the Owner

1. **May the spike write sample `index.md` files into the repo?** It has no samples to work from (§2). Default
   assumption if unanswered: generate into the **session scratchpad only**, and paste the verbatim bytes into
   Completion Notes. Say the word if you would rather have real `docs/index.md` /
   `_bmad-output/planning-artifacts/index.md` files committed — that changes the spike's output and gives
   18.4 and the follow-on story a live fixture, at the cost of two hand-maintained files that go stale.
2. **Do you have a preference among D0–D3 (§10)?** The spike is written to rank all four and justify the
   losers, so no answer is needed — but if you already know you want (say) the docs landing page, saying so now
   converts a research question into a design question and shortens the follow-on story's verify-and-iterate
   round.
3. **`sprint-status.yaml` says 18.2 is `ready-for-dev`; the 18.2 story file says `in-progress`.** Not this
   story's business, but the two artifacts disagree and someone should reconcile them.
