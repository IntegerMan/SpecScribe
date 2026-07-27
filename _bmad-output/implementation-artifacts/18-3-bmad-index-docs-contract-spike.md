---
baseline_commit: 86b35c267241c15b05c64e3aaa3e13cce58198b2
---

# Story 18.3: BMad Index-Docs Contract Spike

Status: ready-for-dev

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

- [ ] **Task 1 — Read the emitter and the consumption sites in full (AC: #1, #2)**
  - [ ] Read `.claude/skills/bmad-index-docs/SKILL.md` in full (67 lines) — it is the entire specification.
  - [ ] Confirm the `core`-module ownership claim: `_bmad/core/module-help.csv:6`, `_bmad/_config/skill-manifest.csv:9`, `_bmad/_config/files-manifest.csv:223`. Confirm `_bmad/core/` holds no materialized skill body.
  - [ ] Confirm `ModuleContext.Detect` still excludes `core`, and confirm the "18.3 is not gated by 18.2" conclusion against the current state of 18.2 (its story file reads `in-progress`; `sprint-status.yaml` reads `ready-for-dev` — a live drift worth noting, and a signal that a concurrent session may be editing `ModuleContext.cs`).
  - [ ] Read `ModuleContext.ModuleDoc` / `BmadMethodDocs` / `GameDevStudioDocs`, `SiteNav.Build`'s module-doc loop, `HtmlRenderAdapter.AppendKeyViewsBand`, `SiteGenerator.ExtractAdrSummary` + `CollapseSummary` + `IsDecorativeLine`, `Frontmatter`, `MarkdownConverter.Convert` / `ExtractFirstH1`, `DocModel`, `ListRow`, `AdapterDiagnostic`, and `RegenerateAdrs`' synthesized-landing block.
  - [ ] Verify §5's claim that no surface lists generic docs — grep `SiteNav`, `DashboardViewBuilder`, `ProjectOutline`, and the templaters. If a listing surface exists, it supersedes §5 and probably answers AC #2.

- [ ] **Task 2 — Obtain real samples (AC: #1)**
  - [ ] Resolve the Open Question on where generated samples may be written **before** generating anything.
  - [ ] Run `bmad-index-docs` against `docs/` and `_bmad-output/planning-artifacts/` — two structurally different folders (loose files + subdirs; one with a gitignored subdir, one with four content subdirs).
  - [ ] Run it **twice over the same folder** and diff. Run-to-run determinism is the single most decision-relevant property; record the diff verbatim.
  - [ ] Find at least one `index.md` in the wild. Start with `bmad-code-org/BMAD-METHOD`'s `docs/index.md` (§3) and reconfirm the fetched shape at today's HEAD. **Record the URL, the retrieval date, and the upstream commit SHA** — 18.1's review named the absence of exactly these as fatal.
  - [ ] For every sample, classify it **skill-generated vs hand-authored** and state the evidence. If no confirmed skill-generated sample can be found in the wild, say so — it is a finding, and it drives AC #1's as-is-vs-stricter-mode recommendation.
  - [ ] Paste the raw bytes of every sample (or a faithful excerpt with the elision marked) into Completion Notes.

- [ ] **Task 3 — Pin the entry grammar (AC: #1)**
  - [ ] Write the exact grammar SpecScribe should parse: the entry line shape, which parts are required vs optional, the accepted separators (ASCII `-` vs `–` vs `—` — BMad's own file uses an em-dash where `SKILL.md` documents a hyphen), bold-wrapped vs bare links, and how heading structure (`## Files` / `### subfolder/` vs arbitrary semantic headings) is or is not used.
  - [ ] Specify what is **ignored**: prose paragraphs, tables, `:::note`/`:::tip` admonitions, frontmatter, HTML comments, nested lists.
  - [ ] Specify description handling: length bound (the skill says 3-10 words; BMad's own file uses full sentences), truncation rule, and whether inline markdown in a description is stripped, escaped, or rendered.
  - [ ] Enumerate the repo-to-repo inconsistencies actually observed, each with the sample it came from, and state the parse impact of each.
  - [ ] **Recommend: parse as-is, or request a stricter emission mode upstream** — with the reasoning, and noting the §"non-goals" constraint that `.claude/skills/` is installer-managed.

- [ ] **Task 4 — Path resolution and failure taxonomy (AC: #1, #2)**
  - [ ] Specify the `./`-relative → source-root-relative → output-relative resolution chain (§7), including `../` segments, casing (`OrdinalIgnoreCase` matches the rest of the codebase), and slash normalization.
  - [ ] Enumerate every failure mode in §7 and assign each a named behavior: resolve-to-dedicated-surface, drop-silently, drop-with-diagnostic, or keep-blurb-without-link.
  - [ ] For each behavior that reports, name the `AdapterDiagnosticCategory`, **draft the message string**, and state its anchor root. (18.1's review failed this exact clause — do not repeat it.)
  - [ ] Specify staleness detection: is a stale `index.md` detectable at all (mtime vs referenced files? entry count vs on-disk count?), and what the honest behavior is when it is not.

- [ ] **Task 5 — Rank the candidate seams and pick one (AC: #2)**
  - [ ] Evaluate **D0 / D1 / D2 / D3** (§10) against: reader value, diff size, hover/a11y viability, staleness exposure, whether it closes §5's "generic docs are listed nowhere" gap, and NFR8 absence behavior.
  - [ ] Answer §6's collision explicitly: with an `index.md` present, is it the landing page, the data source, or both — and what suppresses what.
  - [ ] Recommend **one** and justify why each loser lost. Do not recommend "all of them."
  - [ ] State the NFR8 absence rule for the winner: with no `index.md`, the surface is absent — never an empty list or a "no description available" placeholder.
  - [ ] Write the follow-on story's scope boundary (AC #2's final clause): what it lands, what it does not, and which parts 18.4 reuses.

- [ ] **Task 6 — Architecture fork check (AC: #2)**
  - [ ] Decide whether "an external, LLM-generated, unversioned file is an allowed *input* to generation" is a cross-cutting decision warranting an ADR. Arguments in favor: it is a new class of input (every current input is either authored-by-a-human or derived-from-git); it interacts with AD-4's additive/non-blocking rule and with NFR8. Argument against: it may just be one more optional source under AD-4, requiring no new invariant.
  - [ ] If it is a fork, **propose** the ADR (per this project's ADR-trigger discipline) rather than burying the decision in this story's notes. If it is not, say so in one sentence and move on.

- [ ] **Task 7 — Record findings; no production code (AC: #1, #2)**
  - [ ] Write the contract (grammar + inconsistency inventory + as-is-vs-stricter recommendation + resolution chain + failure taxonomy with categories and drafted wording + seam ranking + winner + NFR8 rule + follow-on scope boundary) into this story's **Completion Notes**, mirroring Story 18.1's Completion-Notes-as-deliverable convention.
  - [ ] Do **not** land `src/**` / `tests/**` changes. Confirm with `git diff --stat {baseline_commit} HEAD -- src tests` and report the result honestly — 18.1's review caught a `git status` claim its own File List falsified. If the tree is dirty from a concurrent session, say whose changes and do not touch them.
  - [ ] Every external claim carries its source URL/path + retrieval date + commit SHA.

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
- [Source: `src/SpecScribe/AdapterDiagnostic.cs`] — the closed five-value category vocabulary and the source-root anchoring contract.
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

### Debug Log References

### Completion Notes List

### File List

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
