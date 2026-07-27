---
baseline_commit: 32fd28237d42f9a558b716d46bb2ffd7b5dbf6a4
---

# Story 18.4: Forged Ideas List Page

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a team using bmad-forge-idea to pressure-test ideas before they become product briefs,
I want forged idea artifacts (hardened or killed) rendered as a list page in the portal,
so that idea-stage lineage and rationale are visible alongside requirements/epics rather than lost in standalone files.

> **Citation policy (adopted from ADR 0015, per [[cite-adrs-by-symbol-not-line-number]]).** Code is cited by
> **symbol** first; line numbers appear only where a symbol cannot identify the site and are marked
> *(as of 2026-07-27)*. `SiteGenerator.cs` is ~5,100 lines and under concurrent editing — its line numbers drift
> within days. `baseline_commit` above pins the tree these citations were taken against. At that commit the tree
> was already dirty from a concurrent session (`Charts.cs`, `HierarchyExplorer.cs` — Epic 20 rollout work); those
> files are not this story's.

## Why this story exists (read first)

`bmad-forge-idea` writes its session workspace to `{output_folder}/forge/{slug}/`
[`.claude/skills/bmad-forge-idea/customize.toml` §`forge_output_path`, §`run_folder_pattern`]. `{output_folder}`
resolves to `_bmad-output` — **which is SpecScribe's `SourceRoot`** (`ForgeOptions.SourceDirName = "_bmad-output"`).

So a repo that has run the forge is, today, already in this state and nobody designed it:

| File the forge writes | What SpecScribe does with it today |
|---|---|
| `forge/{slug}/forged-idea.md` | Renders as an **orphan generic page**, linked from nowhere. Written only on a *hardened* outcome. |
| `forge/{slug}/.memlog.md` | **Never a page** — `PathUtil.IsIgnoredSourceFile` drops every dotfile. But it *is* read: `SiteGenerator.BuildMemlogMap` globs `.memlog.md` across the whole SourceRoot for the coverage panel's freshness enrichment. |
| `forge/{slug}/forge-report.html` | **Invisible.** `EnumerateSourceFiles` globs `"*.md"` only. Nothing else in the product reads a non-`.md` source file except `sprint-status.yaml` / `_bmad/config.toml` (`SiteGenerator.IsDataSource`). |
| the `forge/` folder itself | Emits an **"unrecognized structure" notice** — `SiteGenerator.UnrecognizedTopLevelFolders` → `DashboardViewBuilder.IsWellKnownTopLevelFolder`, which knows only `planning-artifacts` / `specs` / `implementation-artifacts`. |

This story turns that accidental state into a designed surface. It is the first SpecScribe surface whose
richest content is **not markdown**, and the first to read a `.memlog.md` as *content* rather than as a date.

**The one-line test for "is this in scope?":** if the change *discovers* forge workspaces, *derives* an idea's
title/verdict/date, *renders* the Ideas list or an idea detail page, *carries* the original report into the
output, or *links* an idea forward to a downstream artifact → in. If it changes module identity, the adapter
registry, `index.md` parsing, or any other framework's ingest → out.

## Acceptance Criteria

The three ACs below are epics.md's verbatim text. Four owner decisions (§"Owner decisions", elicited
2026-07-27) bind how they are satisfied; where a decision **extends** an AC it is called out, and the dev-story
run records the extension in `epics.md` in the same change (CLAUDE.md structural-scope rule).

1.
**Given** bmad-forge-idea's output artifacts (or a defined contract for identifying them) in a repository
**When** generation runs
**Then** a new Ideas list page renders each discovered idea with its title, verdict (hardened/killed/in-progress), and a link through to the persona-objections/rationale content, using the existing ListRow primitive per Story 10.8's list-page grammar.

2.
**Given** an idea that later produced a product brief, PRD, or epic
**When** the list page renders
**Then** it links forward to that downstream artifact where discoverable, so the idea's fate is traceable without manual cross-referencing.

3.
**Given** no forge-idea artifacts exist in a repository
**When** generation runs
**Then** the Ideas page/nav entry is omitted entirely rather than showing an empty page, matching existing optional-surface conventions elsewhere in the portal.

[Source: `_bmad-output/planning-artifacts/epics.md` §"Story 18.4: Forged Ideas List Page"]

### AC extensions from the owner decisions (record these in epics.md)

4. **(extends AC #1)** Each idea also gets a **synthesized detail page** in the portal, built from `.memlog.md`
   (the chronology of decisions, assumptions, cracks, kills and locks) plus `forged-idea.md` when present —
   and the forge's own `forge-report.html` is **carried into the output verbatim** and linked from that detail
   page as "the original report". A report that fails the safety gate (AC #6) is skipped, not rewritten.

5. **(extends AC #1)** The list is **grouped by verdict** — a section per verdict, each with a heading and a
   count — rather than one flat list.

6. **(new, safety)** A carried-over `forge-report.html` is emitted **only** if it is self-contained and
   script-free. A report containing a `<script` tag or an external-origin subresource reference is not written;
   the idea's detail page renders without the report link and one `Skipped` diagnostic is reported.

## Owner decisions (elicited 2026-07-27 — these are locked)

**D1 — Link target: synthesized detail page *and* the original report carried alongside.**
Chosen over "link to `forged-idea.md` only" (which leaves killed / clarified / in-progress ideas with no target
at all, since `forged-idea.md` exists only on a hardened outcome) and over "copy the report only" (an un-styled,
chrome-less foreign page as the sole destination). Cost accepted: this is the largest of the four options and it
establishes the non-markdown-source precedent. §4 shows the precedent is cheaper than it looks — `WriteOutput`
already takes a `string`, so "carry the report" is a read-then-`WriteOutput`, not a new file-copy mechanism.

**D2 — Three verdicts; `Clarified` folds into `in-progress`.**
epics.md AC #1 names *hardened / killed / in-progress*. The skill actually has **three terminal exits** —
`Hardened`, `Killed`, **`Clarified`** — plus "not yet complete" [`SKILL.md` §Exits]. The owner chose to keep
epics.md's three-value vocabulary and route a *clarified* session into the `in-progress` bucket.

> ⚠️ **Concern recorded, decision affirmed.** A clarified session is *complete*; bucketing it as "in-progress"
> reports a finished session as unfinished. Implement the decision as stated — three verdict buckets on the list.
> The honest mitigation that does **not** add a fourth bucket: the idea's **detail page** states the true exit
> word (`Clarified`) from the same derivation, so the list's bucketing is a grouping choice and never the only
> record. See Open Question 1 for the one thing left to the owner (the bucket's visible label).

**D3 — Grouped by verdict.** Sections with headings + counts, each section a `ListRow` list. Chosen over the
flat "ADR-landing twin" and over the JS sort/filter variant. Section order: **Hardened → In progress → Killed**
(strongest outcome first; killed last, as history). A verdict with zero ideas emits **no section at all** —
never an empty heading (NFR8).

**D4 — Forward links: best-effort, honest absence.** Derive AC #2's forward link only from evidence that
actually exists on disk. **No slug/title fuzzy matching** — the owner explicitly rejected it, and Story 21.1's
code review already caught this exact defect class (a "phantom-covered" requirement counted as covered and drawn
blank). No evidence ⇒ **no forward-link element at all**, not "none found".

## Context & Scope

### 1. The forge's on-disk contract, read from the emitter

Read these two files in full before writing anything — they are the whole specification:
`.claude/skills/bmad-forge-idea/SKILL.md` (108 lines) and `_bmad/scripts/memlog.py` (225 lines).

A session workspace `{output_folder}/forge/{slug}/` can hold three files:

- **`.memlog.md` — always present, created first.** `memlog.py init` writes frontmatter then an append-only
  body. The forge inits it as
  `memlog.py init --workspace {workspace} --field idea="<idea>" --field goal="<goal>"` [`SKILL.md` §Set up the
  session], so its frontmatter is `idea:`, `goal:`, `updated:` — and, once the session ends,
  `status: complete` (`memlog.py set --key status --value complete` [`SKILL.md` §Exits]).
  Body entries are one line each: `- (type) text`, where the forge's vocabulary is
  `decision | assumption | crack | kill | direction | lock | note` [`SKILL.md` §The forge].
- **`forged-idea.md` — only on a *hardened* exit.** Deliberately terse: *"only the decisions, rejected options,
  and reasons that matter downstream … If it reads like a document, it is too long."* [`SKILL.md` §Exits].
- **`forge-report.html` — always rendered, on every exit.** *"Always render `{workspace}/forge-report.html` as a
  self-contained HTML file the user can open, with inline CSS and an inline-SVG seal or stamp."* It credits the
  personas by name/icon/voice, lists what was rejected and why, names the weak points that survived, and stamps
  the outcome `HARDENED` / `KILLED` / `CLARIFIED`. **This is the "persona-objections/rationale content" AC #1
  names** — and it is the one file the product cannot currently see.

⚠️ **`memlog.py`'s own docstring contradicts the forge on `status`.** Invariant 3 reads: *"No lifecycle status.
A memory log has no 'complete' flag … never as frontmatter the log would have to mutate."* The forge sets one
anyway, via the generic `set` subcommand (which does not enforce a key vocabulary). Operationally the field
**is** there and is the only in-progress signal — but it exists against the tool's stated design, so treat it as
an observed convention, not a guarantee: an absent `status` must mean "in progress", never an error.

### 2. `.memlog.md` is NOT forge-specific — this is the discovery trap

**This repo already contains four `.memlog.md` files, and none of them is a forge session:**

```
_bmad-output/planning-artifacts/briefs/brief-SpecScribe-2026-07-05/.memlog.md
_bmad-output/planning-artifacts/prds/prd-SpecScribe-2026-07-05/.memlog.md
_bmad-output/planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/.memlog.md
_bmad-output/specs/spec-specscribe/.memlog.md
```

They are product-brief / PRD / UX / spec sessions. `memlog.py` is a **shared core tool** used by at least five
skills. So *"a directory containing `.memlog.md` is a forged idea"* is **wrong**, and a naive implementation
would list this repo's own PRD as an idea.

Two further discriminators, verified against the files above:

- Their frontmatter key is **`topic:`** (matching `memlog.py`'s own docstring example). The forge writes
  **`idea:`**. Confirm this on the real files — `head -4` each of the four paths above.
- None of them has a sibling `forge-report.html`.

**The discovery contract to implement (a cascade, not a single rule):**

1. **Path (authoritative for the default install):** any directory under `{SourceRoot}/forge/` that contains a
   `.memlog.md` is a forge workspace. This is the only rule that catches an **in-progress** session, which has
   *only* `.memlog.md` and no other marker.
2. **Marker (covers an overridden output path):** anywhere else under `SourceRoot`, a directory containing
   `.memlog.md` **plus** a sibling `forge-report.html` is a forge workspace.
3. **Frontmatter corroboration:** an `idea:` key in the memlog frontmatter. Use it to *reject* a false positive
   under rule 1 (a `forge/` folder someone hand-made), never as the sole positive signal.

⚠️ **State the limitation honestly in Completion Notes:** `forge_output_path` and `run_folder_pattern` are both
overridable via `_bmad/custom/bmad-forge-idea.toml`, and **SpecScribe reads no BMad skill/module TOML or
`config.yaml` at all today** (the same gap Story 18.5 records for TEA's `test_artifacts` key). So under an
overridden path, an *in-progress* session is undiscoverable until it completes and writes its report. Do not
build a TOML reader to close this — that is a separate, cross-cutting decision.

The run folder may be **nested** (`run_folder_pattern` is documented as overridable to add `{date}` or other
components, which is exactly why `SKILL.md` §5's resume glob is recursive: `{forge_output_path}/**/.memlog.md`).
Enumerate recursively; the `{slug}` is the workspace directory's own name, not necessarily a direct child.

### 3. Deriving the four fields

**Title** — cascade, first non-empty wins: memlog frontmatter `idea:` → `forged-idea.md`'s first H1
(`MarkdownConverter.ExtractFirstH1`) → the workspace directory name de-kebabed. The `idea:` value is free user
text that `memlog.py.render` has already newline-collapsed, so it is safe as a single line but may be long —
apply the same `CollapseSummary` treatment `ExtractAdrSummary` uses rather than inventing a second truncator.

**Verdict** — cascade:

| Signal | Verdict |
|---|---|
| frontmatter has no `status: complete` | **in-progress** |
| complete **and** `forged-idea.md` exists | **hardened** |
| complete, no `forged-idea.md`, memlog body has a `- (kill)` entry | **killed** |
| complete, no `forged-idea.md`, no kill entry | *clarified* → **in-progress** bucket per D2 |

The `forge-report.html` stamp word (`HARDENED` / `KILLED` / `CLARIFIED`) is the most *authoritative* record but
the least *parseable* — it is LLM-rendered prose HTML with no fixed markup. Use it only as **corroboration** in
the detail page, never as the primary derivation, and never string-match it to decide the bucket.

**Date** — the memlog `updated:` field. **Reuse `SiteGenerator.MemlogUpdatedPattern`** — it already parses
exactly this field for the coverage panel. Do not add a second regex for the same line (one-classifier/one-seam
discipline; the same rule that keeps `ArtifactCoverage` keyed off `ModuleContext.WellKnownDocs`).

**Summary / blurb** — the memlog frontmatter `goal:` value. Fallback: the first `- (lock)` entry, then the first
`- (decision)` entry. No summary ⇒ the row is the bare title (`ListRow` already handles a summary-only row).

> **On the stated 18.3 dependency.** epics.md's seating comment says 18.4 *"depends on 18.3's pinned contract for
> its blurb-metadata half."* **It does not.** 18.3 is about `bmad-index-docs`' `index.md`; a forge workspace
> carries its own `goal:` in its own frontmatter, so the blurb half is satisfiable with no external contract and
> **18.4 is not blocked by 18.3** (which is still `ready-for-dev`). Say this in Completion Notes and correct the
> seating comment in `epics.md` in the same change. If the index.md follow-on ever lands, ideas may adopt it as
> optional enrichment.

### 4. Carrying `forge-report.html` — cheaper than it looks, but it needs a safety gate

`SiteGenerator.WriteOutput(string outputRelativePath, string html)` takes a **string**, creates the directory,
writes the file, **and** populates `_spaCapture`. So "carry the report" is:

```
var raw = MarkdownConverter.ReadAllTextShared(reportFullPath);   // shared-read helper already used by BuildMemlogMap
if (IsSafeCarriedReport(raw)) WriteOutput($"ideas/{slug}-report.html", raw);
```

No new copy mechanism, no new asset pipeline, and SPA/webview capture comes free. That is the whole precedent —
say so plainly rather than framing it as a new subsystem.

Three things it still needs:

- **AC #6's safety gate.** The report is LLM-authored HTML landing verbatim inside the portal's own output
  directory. `SKILL.md` contracts it as *self-contained … with inline CSS and an inline-SVG seal* — it says
  nothing about scripts, and nothing enforces the contract. Reject a report containing `<script` (case-insensitive)
  or an external-origin subresource (`src=`/`href=` with `http://`, `https://`, or `//`) and report it. This also
  keeps the site inside ADR 0013 / NFR-5's JS-optional posture: a carried page that only works with JS would be a
  surface with no text twin.
- **A size bound.** `_spaCapture` feeds the SPA bundle, whose chunker is **byte-blind** (a known perf defect —
  [[story-6-6-deferred-cleanup-done-spa-at-scale-perf]]). Cap the carried report (suggest 512 KB; a self-contained
  page with an inline SVG seal is far under it) and skip-with-diagnostic above the cap rather than letting one
  report inflate every SPA chunk.
- **It carries no portal chrome.** The report has its own `<html>` and its own inline CSS; it is a **leaf, not a
  portal page**. Do not wrap it in `HtmlTemplater.RenderPage` (that would nest documents — the exact defect class
  Story 23.3 hit when the IR's two region shapes nested `<main>`/`<footer>` on 187 pages while every harness
  passed). Link it as a clearly-labelled "original report" and let it be a dead-end.

### 5. Page routing, and why the ADR landing's collision does not arise here

Precedent to model from: `SiteGenerator.RegenerateAdrs`' synthesized landing block — a `ListRow` list built into
a `DocModel` and pushed through `HtmlTemplater.RenderPage`, with `AdrEntry(Title, OutputRelativePath,
SourceRelativePath, Status, Number, Date, Summary)` as the row model. Copy that shape.

**Output paths:**

| Page | Path | Gate |
|---|---|---|
| Ideas list | `ideas.html` | at least one discovered idea |
| Idea detail | `ideas/{slug}.html` | per idea |
| Carried report | `ideas/{slug}-report.html` | report present **and** passes AC #6 |

Use a **top-level `ideas.html`** (like `traceability.html` / `cadence.html` / `work-graph.html`), *not*
`ideas/index.html`. Deliberate: it sidesteps the landing-slot collision entirely — the one `RegenerateAdrs`
guards with `landingPathAlreadyWritten` because a stray `index.md` can occupy the ADR landing path
(regression test at `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs:702-706` *(as of 2026-07-27)*). No
`ideas/index.html` ⇒ nothing to collide with.

⚠️ **`{slug}` must be sanitized before it becomes a path segment.** It is derived by an LLM from free user text
[`SKILL.md` §Set up the session] and is only *conventionally* kebab-case. Slugify it, and de-duplicate collisions
deterministically (ordinal order, first-wins + a `Skipped` diagnostic) — the same first-wins-plus-diagnostic rule
`SiteNav.Build`'s module-doc loop already uses, and the same class of bug
[[story-artifact-prefix-collision-fixed]] recorded for `{epic}-{story}-` filenames.

### 6. Registration footprint — the full checklist a new gated page needs

Model on **Work Graph** (Story 19.2), the most recent addition, and touch every one of these:

- `SiteNav.IdeasOutputPath` const + `HasIdeas => Items.Any(i => i.Label == "Ideas")`.
- `SiteNav.Build(… bool hasIdeas = false …)` → add to the **Project** group and to `quickLinks` with
  `Group = "Project"`. (Ideas sit *before* requirements in the lifecycle, alongside Readme / PRD / Brief / ADRs /
  Spec kernels. `Delivery` and `Insights` are both wrong: an idea is neither tracked work nor a derived metric.
  The owner may move it in the verify round.)
- Both `SiteNav.Build` call sites in `SiteGenerator` (`GenerateAll`'s and `BuildNav`'s) must pass the same gate —
  Story 18.2 found `BuildNav` silently disagreeing with `GenerateAll` on module detection, with 4 of its 5 call
  sites passing an empty source list. **Grep for every `SiteNav.Build(` call and prove they agree.**
- The gate is a **data signal read before the page renders**, exactly like `hasWorkGraph`. `SiteNav.Build`'s
  own `<remarks>` documents and accepts this tradeoff; do **not** attempt a post-render nav rebuild.
- **Do NOT add `ideas.html` to `EpicsFamilyPages`.** Ideas are not epics-derived; `ClearEpicsFamilyOutputs`
  must not delete them when `epics.md` disappears.
- `DashboardViewBuilder.KnownIndexGroups` — add `("Ideas", "forge")` so `forge/` stops raising an
  "unrecognized structure" notice. That array carries an explicit **do-not-extend** warning; address it head-on
  in a code comment. The warning is about `adrs` / `retros`, which are *not* `SourceRoot` tops (that was a
  misdiagnosed Epic 4 debt). `forge` **is** a `SourceRoot` top, written there by a core BMad skill — the
  warning's stated rationale does not cover this case. Say exactly that.
- **Suppress the duplicate generic page.** `forged-idea.md` currently renders as its own generic page. With a
  detail page at `ideas/{slug}.html` there would be two pages for one idea. Add the workspace's `.md` files to
  the `consumedArtifacts` set (`SiteGenerator` builds it from `bundle.ConsumedSourceRelatives` — see the
  "Files the adapter consumed into dedicated surfaces" block) so the generic pages loop skips them. If the
  Ideas discovery does not run through the adapter, extend the same `HashSet` at the generator level and say why.

### 7. Two live pre-existing behaviors this story must not break

**(a) The `hasScopedMemlog` regression — subtle and real.** `SiteGenerator.SelectMemlogUpdatedByFamily` treats a
root-level memlog (`Dir.Length == 0`) as every family's fallback **only when it is the only memlog in the tree**:

```
var hasScopedMemlog = memlogs.Any(ml => ml.Dir.Length > 0);
```

A forge workspace's memlog is *scoped* (`forge/{slug}`). So in a repo whose only decision journal is
`_bmad-output/.memlog.md`, **running the forge once silently strips the memlog date from every coverage card** —
without this story changing a line of that method. Write a regression test for it. Resolving it (exclude forge
memlogs from `BuildMemlogMap`'s input, or keep them and accept the behavior) is a decision to make explicitly
and record, not to discover later.

**(b) `BuildMemlogMap` will now see forge memlogs.** It globs
`Directory.EnumerateFiles(SourceRoot, ".memlog.md", AllDirectories)`. A forge memlog can never *win* a family
(no family's `SourcePath` starts with `forge/{slug}/`), so it adds no wrong date — but it does flip the flag in
(a). Verify both claims by test rather than by reading.

### 8. Watch mode is out of scope — but say so, don't leave it silent

The file watcher routes `.md` changes plus the two named non-`.md` data sources (`SiteGenerator.IsDataSource` →
`sprint-status.yaml`, `_bmad/config.toml`). A forge session's `.memlog.md` is dotfile-**ignored** and its
`forge-report.html` is not `.md`, so **watch mode will not react to forge activity at all**; a full
`GenerateAll` picks everything up (it wipes and rebuilds `OutputRoot`). Declare this a non-goal, name
`IsDataSource` as the mechanism a follow-on would extend, and do not build it here.

### 9. AC #2's forward links — what "discoverable" is allowed to mean

Per **D4**, only real on-disk evidence counts. Two admissible sources, in order:

1. **A markdown link inside `forged-idea.md`** that resolves to a source file which has a page. Resolve it the
   same way §7 of Story 18.3 specifies for `index.md` hrefs: `./`-relative → `ToSourceRelative` key space →
   `PathUtil.ToOutputRelative`, honouring special routing (`epics.md` → `epics.html`, requirements → the curated
   FR/NFR page) and dropping any target that has no page rather than emitting a dead link.
2. **A downstream doc that names the forge workspace path or slug** (e.g. a brief whose frontmatter `Sources`
   lists `forge/{slug}/forged-idea.md`). `Frontmatter` already models `Sources` — check it before inventing a
   scan.

Anything else — slug-vs-title similarity, date proximity, folder-name resemblance — is **forbidden**. No
evidence ⇒ the row/detail renders with **no forward-link element at all** (NFR8: absent, not "none found").

### 10. Diagnostics — category **and** drafted wording **and** anchor root, for every fallback

`AdapterDiagnosticCategory` is a closed five-value set: `Unsupported`, `Malformed`, `Skipped`, `Error`,
`Informational` [`AdapterDiagnostic.cs`]. Story 18.1's code review failed exactly this AC clause by listing
fallbacks with *no category and no wording*; 18.3 was written to avoid repeating it. Do not repeat it here.

Every forge workspace path is under `SourceRoot`, so every diagnostic below anchors to
**`DiagnosticAnchorRoot.Source`** — contrast Story 18.2, which had to add `DiagnosticAnchorRoot.Repo` for
repo-relative module notices. Drafted set (extend with the same rigor if you find more):

| Condition | Category | Drafted message | Anchor |
|---|---|---|---|
| `.memlog.md` present but frontmatter unparseable / unterminated | `Malformed` | `Forge session memlog could not be parsed; the idea is listed with its folder name and no summary.` | Source |
| Two workspaces slugify to the same page path | `Skipped` | `Duplicate idea slug '{slug}'; the first workspace in path order is listed and {n} other(s) skipped.` | Source |
| `forge-report.html` contains a script or external subresource | `Skipped` | `Forge report for '{slug}' was not carried into the portal: it is not self-contained (script or external resource).` | Source |
| `forge-report.html` exceeds the carry size cap | `Skipped` | `Forge report for '{slug}' was not carried into the portal: {size} exceeds the {cap} limit.` | Source |
| A `forged-idea.md` forward link resolves to no page | `Skipped` | `Forward link '{href}' from idea '{slug}' has no generated page; the link is omitted.` | Source |
| Workspace holds `.memlog.md` but the directory is unreadable mid-run | `Error` | `Forge session '{slug}' could not be read; it is omitted from the Ideas page.` | Source |

### 11. Naming — `Forge*` is already taken, and it is not the forge

⚠️ **`ForgeOptions` is SpecScribe's own generation-options record** (`SourceRoot`, `OutputRoot`, `AdrOutputSubdir`,
`StylesheetName`, …), referenced from ~20 files. `SiteGenerator` "forges" the site; it has nothing to do with
`bmad-forge-idea`. **Do not introduce `ForgeModel`, `ForgeEntry`, `ForgeTemplater`, `IsForgeFile`, or anything
else `Forge*`-prefixed.** Name everything after the *domain*, not the tool:

`IdeaEntry` · `IdeaVerdict` · `IdeasModel` · `IdeasTemplater` · `SiteNav.IdeasOutputPath` · `HasIdeas` ·
`IdeaDiscovery` (the workspace scan).

This is the same trap [[coverage-epics-seeded-25-5-25-6-epic-27]] recorded for Epic 27, where `ArtifactCoverage`
already owned the word "coverage" — caught before it shipped there; catch it here too.

### 12. Verdict badges — word first, colour second

Route verdict badges through `StatusStyles`, never a hand-rolled span. `StatusStyles.FreeTextBadge` already does
the right thing: a known lifecycle word routes to its canonical badge, anything else degrades to a slugged
`.pill.status-*` that **still carries the word** — *"never color-only"* (UX-DR17, and CLAUDE.md's
"no state may be signalled by color alone"). For the section accent bars use the `ListRow`
`extraRowClass: "list-row-accent-{token}"` slot, mapping **hardened → `done`, in-progress → `pending`,
killed → `deferred`** — the same three-way shape `StatusStyles.AdrAccentToken` already uses for
accepted/proposed/superseded. Consider adding an `IdeaAccentToken` beside it rather than overloading
`AdrAccentToken`.

Reuse `<ul class="list-rows-list js-listable">` for each section's list — `js-listable` is the opt-in seam
Story 10.9's client sort/filter enhances, and it is inert with JS off.

### Deliberate non-goals (seed list — extend with rationale)

- **Reading any BMad skill/module TOML or `config.yaml`** to resolve an overridden `forge_output_path`.
  SpecScribe reads none today; §2 states the resulting limitation honestly instead.
- **Watch-mode reactivity to forge activity** (§8).
- **Editing `.claude/skills/bmad-forge-idea/**`** — installer-managed and hash-pinned in
  `_bmad/_config/files-manifest.csv`; edits are overwritten on the next BMad update.
- **Rewriting, restyling, sanitizing-by-transformation, or wrapping `forge-report.html`.** It is carried
  verbatim or not at all (§4).
- **Fuzzy / heuristic forward-link matching** (D4, §9).
- **A fourth verdict bucket on the list** (D2). The detail page may state the true exit word; the list has three
  sections.
- **Module identity, `BmadModule`, `ModuleContext`, ADR 0015's surface area.** `bmad-forge-idea` ships in
  **`core`** (`_bmad/core/module-help.csv:14`, `_bmad/_config/skill-manifest.csv:7`) — the same finding Story
  18.3 records for `bmad-index-docs`, and `core` is the one module `ModuleContext.Detect` deliberately excludes.
  Ideas must **not** route through `ModuleContext` / `ModuleDoc` / `BmadModule`.
- **`index.md` parsing** — 18.3's domain (§3).
- **Reviving the removed home-index doc bands.** Adding `forge` to `KnownIndexGroups` gates the *diagnostic*
  only; the bands stay removed (§6).

## Tasks / Subtasks

- [ ] **Task 1 — Read the emitter and the consumption sites in full (AC: #1, #3)**
  - [ ] Read `.claude/skills/bmad-forge-idea/SKILL.md` and `_bmad/scripts/memlog.py` in full.
  - [ ] `head -6` each of the four existing `.memlog.md` files (§2) and confirm they use `topic:` not `idea:` and have no sibling `forge-report.html`. This is the false-positive set your discovery must reject.
  - [ ] Read `SiteGenerator.RegenerateAdrs`' synthesized-landing block, `AdrEntry`, `ListRow`, `StatusStyles.FreeTextBadge` / `AdrAccentToken` / `CanonicalRank`, `SiteNav.Build` (+ `HasWorkGraph`/`hasWorkGraph` as the gating template), `DashboardViewBuilder.KnownIndexGroups` / `IsWellKnownTopLevelFolder`, `SiteGenerator.UnrecognizedTopLevelFolders`, `BuildMemlogMap` / `SelectMemlogUpdatedByFamily` / `MemlogUpdatedPattern`, `PathUtil.IsIgnoredSourceFile`, `SiteGenerator.WriteOutput`, `EnumerateSourceFiles`, `AdapterDiagnostic`, `DiagnosticAnchorRoot`, `Frontmatter`, `MarkdownConverter.ExtractFirstH1` / `ReadAllTextShared`, `ForgeOptions` (§11).
  - [ ] Grep every `SiteNav.Build(` call site and list them — the gate must be passed identically at each (§6).

- [ ] **Task 2 — Create a real fixture, and confirm today's behavior before changing it (AC: #1, #3)**
  - [ ] Build a scratch repo (session scratchpad, **not** this repo's `main`) with `_bmad-output/forge/` workspaces covering all four states: in-progress (memlog only, no `status`), hardened (+`forged-idea.md`+report), killed (report + a `- (kill)` memlog entry), clarified (report, no kill entry, no `forged-idea.md`).
  - [ ] Generate against it **before** any code change and record verbatim: the orphan `forged-idea` page, the absent report, and the `forge` unrecognized-structure notice. This is the baseline the ACs are measured against — 18.1's review named unrecorded/unreproducible evidence as fatal.
  - [ ] Add the same shapes as test fixtures under `tests/`, pinned in-repo so the evidence survives the session.

- [ ] **Task 3 — Idea discovery and the domain model (AC: #1, #3)**
  - [ ] Implement the §2 cascade: `{SourceRoot}/forge/**` path rule → `.memlog.md`+`forge-report.html` marker rule → `idea:` frontmatter corroboration. Recursive (nested `run_folder_pattern`).
  - [ ] Reject every one of §2's four existing non-forge memlogs — assert this by test using the real frontmatter shapes.
  - [ ] Derive title / verdict / date / summary per §3. Reuse `MemlogUpdatedPattern`; do not add a second `updated:` regex.
  - [ ] Slugify + de-duplicate the workspace name into a page path (§5), first-wins in ordinal order with a `Skipped` diagnostic.
  - [ ] Model it as a pure `Build` over already-gathered inputs with an `Empty` singleton and an `IsEmpty` flag, mirroring `ArtifactCoverage` / `WorkInventory` / `WorkGraphModel`. Never throws — any failure degrades to `Empty` so the surface omits and generation still succeeds (AD-4 / NFR2).

- [ ] **Task 4 — The Ideas list page, grouped by verdict (AC: #1, #3; D3)**
  - [ ] `IdeasTemplater` renders `ideas.html`: a section per verdict in the order **Hardened → In progress → Killed**, each with a heading, a count, and a `<ul class="list-rows-list js-listable">` of `ListRow.Render` rows.
  - [ ] A verdict with zero ideas emits **no section** (NFR8) — never an empty heading, never "0 ideas".
  - [ ] Row anatomy: `<strong>{title}</strong> — {summary}` · verdict badge via `StatusStyles.FreeTextBadge` · date chip via `ListRow.Chip(PortalDates.Day(...))` · `ListRow.PrimaryLink(detailHref, "View idea")` · `extraRowClass` accent per §12 · `sortName`/`sortDate`/`sortStatus` populated.
  - [ ] Gate: no discovered ideas ⇒ **no page written and no nav entry** (AC #3). Verify the nav gate and the write gate read the same signal.

- [ ] **Task 5 — Idea detail pages + the carried report (AC: #1 ext. #4, #6; D1)**
  - [ ] `ideas/{slug}.html`: the memlog chronology rendered as typed entries (decision / assumption / crack / kill / direction / lock / note), `forged-idea.md`'s content when present, and the **true exit word** including `Clarified` (§D2 mitigation).
  - [ ] Carry `forge-report.html` to `ideas/{slug}-report.html` via `WriteOutput` (§4) and link it from the detail page as "the original report" — clearly labelled as a leaf artifact outside the portal chrome.
  - [ ] Implement AC #6's gate: skip a report with `<script` or an external-origin `src=`/`href=`, and skip one over the size cap, each with the §10 `Skipped` diagnostic.
  - [ ] **Never** wrap the report in `HtmlTemplater.RenderPage` — it has its own `<html>` (§4, nested-document defect class).
  - [ ] Suppress the now-duplicate generic `forged-idea.md` page via the `consumedArtifacts` set (§6).

- [ ] **Task 6 — Forward links (AC: #2; D4)**
  - [ ] Resolve markdown links inside `forged-idea.md` through the source-relative → output-relative chain, honouring special routing and dropping no-page targets with the §10 diagnostic.
  - [ ] Check `Frontmatter.Sources` on downstream docs for a reference to the workspace path or `forged-idea.md`.
  - [ ] **No** fuzzy matching. No evidence ⇒ no forward-link element at all. Assert the absence by test.

- [ ] **Task 7 — Registration, gating, and the two pre-existing behaviors (AC: #1, #3)**
  - [ ] Work through §6's checklist in full: `SiteNav` const + `HasIdeas` + `Build` parameter + Project group + quick link + every call site + **not** in `EpicsFamilyPages`.
  - [ ] Add `("Ideas", "forge")` to `KnownIndexGroups` with a code comment that names the do-not-extend warning and states why `forge` is categorically different (§6).
  - [ ] Write the §7(a) regression test: a repo with a root `_bmad-output/.memlog.md` keeps its coverage-card dates after a forge workspace appears — or, if you decide to exclude forge memlogs from `BuildMemlogMap`, test that decision instead and **record which you chose and why**.
  - [ ] Confirm §7(b) by test: a forge memlog never becomes a family's `MemlogUpdated`.

- [ ] **Task 8 — Verify live, in a browser (CLAUDE.md verification rule)**
  - [ ] Generate the Task 2 scratch repo to `SpecScribeOutput/` (**never** `--output docs/live`) and open `ideas.html`, one detail page, and one carried report in a real browser.
  - [ ] Check: section grouping and counts, verdict badges legible **without colour**, accent bars, the report link, and that the carried report does not inherit or corrupt portal chrome.
  - [ ] Check the **JS-off** path (CSP `script-src 'none'`, per [[story-20-6-text-twin-audit-done]]): the grouped list is plain HTML and must be fully readable — `js-listable` is enhancement only.
  - [ ] Check the empty case: a repo with no forge workspace has **no** Ideas nav entry, **no** `ideas.html`, and **no** `forge` structure notice regression.

- [ ] **Task 9 — Suite, golden gate, and artifact reconciliation**
  - [ ] Run the full suite. Expect the known rotating deep-git contention flakes ([[gitmetrics-3s-timeout-silent-deep-git-loss]]) — confirm any failure passes in isolation before calling it a regression.
  - [ ] `GoldenContentFingerprint`: this story adds production code but the golden fixture has **no** forge workspace, so the fingerprint should **not** move. If it does, that is a signal — investigate before re-baselining, and byte-compare against a clean checkout first ([[story-18-2-module-identity-done]]'s gotcha: `git status` did not report a file already changed on disk).
  - [ ] Amend `epics.md` **and** `sprint-status.yaml` in the same change (CLAUDE.md): seat AC #4/#5/#6, correct the "depends on 18.3's pinned contract" seating comment (§3), and record the FR resolution from Open Question 2.

- [ ] **Task 10 — ADR check (CLAUDE.md ADR-trigger discipline)**
  - [ ] Decide whether *"a foreign, externally-generated HTML artifact may be carried verbatim into the portal output"* is a cross-cutting architectural decision. Arguments in favour: every page in the site is C#-composed today; this adds a second, un-composed class of output; it interacts with AD-1/AD-2 (one shared rendering core, host-neutral view models) and with ADR 0013's text-twin contract. Argument against: AD-4 may already cover it as one more optional, additive provider.
  - [ ] If it is a fork, **propose the ADR** rather than burying the decision here or in `sprint-status.yaml` prose ([[adr-creation-trigger-gap-epic-10-retro]]). If not, say so in one sentence and move on.
  - [ ] Read `docs/adrs/` first — do not declare a rule-crossing without checking whether a ratified ADR already permits it ([[adr-consultation-gap-three-arc-renderers]]).

## Dev Notes

### Architecture compliance

- **AD-1** [ARCHITECTURE-SPINE.md §AD-1] — one shared projection/rendering core. Idea discovery and verdict
  derivation happen once, in C#, and produce a host-neutral model. No surface re-scans the forge folder.
- **AD-2** [§AD-2] — host-neutral view models are the core↔adapter contract. The Ideas view model is decided in
  C# and handed to adapters; nothing is composed in TypeScript.
- **AD-4** [§AD-4] — optional providers enrich but never own baseline success. A missing, malformed, or
  unreadable forge workspace must never fail or block generation.
- **NFR8** [`epics.md:137`] — *surfaces degrade gracefully — absent, not broken or misleadingly empty.* This
  governs AC #3 (no ideas ⇒ no page, no nav entry), the empty verdict section, and the absent forward link.
- **ADR 0013** — the text twin is the no-JS contract. The Ideas list is plain HTML; `js-listable` is
  enhancement only. The carried report is exempt from *composition* but not from *safety* (AC #6).
- **NFR-5 / "no state by colour alone"** — every verdict carries its word (§12).

### Anti-patterns to prevent

- Treating `.memlog.md` as a forge marker. It is a **shared core tool**; this repo already has four non-forge
  memlogs, and a naive rule lists SpecScribe's own PRD as a forged idea (§2).
- Naming anything `Forge*`. `ForgeOptions` is SpecScribe's own options record (§11).
- Deriving the verdict by string-matching the LLM-rendered report stamp (§3).
- Wrapping the carried report in `RenderPage`, producing nested `<html>`/`<main>` — Story 23.3's defect class,
  where every harness passed while 187 pages were structurally corrupt.
- Adding a second `updated:` regex beside `MemlogUpdatedPattern` (§3).
- Emitting an empty verdict section, or a "no downstream artifact found" placeholder (NFR8).
- Fuzzy-matching an idea to a brief/PRD by title similarity — a false provenance chain is worse than none, and
  Story 21.1's review already caught this class ([[story-21-1-code-review-done]]).
- Adding `ideas.html` to `EpicsFamilyPages` (it would be deleted whenever `epics.md` disappears).
- Extending `KnownIndexGroups` without naming and answering its do-not-extend warning (§6).
- Listing a fallback with no `AdapterDiagnosticCategory` and no drafted wording — Story 18.1's review failed
  exactly this clause (§10).
- Trusting that an edit landed. Grep for every new symbol before relying on it, and confirm with
  `git diff HEAD` — a zero-grep can be a transient mid-write read
  ([[shared-main-concurrent-edit-loss-verify-after-edit]]).

### Testing requirements

- One test project: `tests/SpecScribe.Tests` — **xUnit 2.9.3** on **net10.0**. Run with `dotnet test`. The
  suite is ~2,470 tests; a full run is slow, so iterate with `--filter`.
- **Split IO from logic the way this codebase already does.** `ArtifactCoverage.Build`, `WorkInventory`, and
  `ProgressCalculator` are all pure `Build` methods over already-gathered inputs, with the disk read in the
  caller — that is why their rules are unit-testable without a repo. Mirror it: workspace discovery does the
  IO; verdict/title/summary derivation is a pure function over `(frontmatter, bodyLines, hasForgedIdea)` and
  gets direct unit tests for all four states plus the malformed cases.
- Required tests, at minimum: the four verdict derivations; §2's four real non-forge memlogs all rejected;
  the nested `run_folder_pattern` case; slug collision → first-wins + `Skipped`; the AC #6 script/external
  and size rejections; AC #3's full omission (no page, no nav item, no quick link); an empty verdict section
  never rendering; a forward link with no page dropped, and no-evidence → **no element**; §7(a)'s
  `hasScopedMemlog` regression and §7(b)'s no-family-attribution claim.
- Known flake class: the deep-git family fails a **rotating** subset under load and passes in isolation
  ([[gitmetrics-3s-timeout-silent-deep-git-loss]]). Confirm in isolation before calling anything a regression.

### Project Structure Notes

- Story file: `_bmad-output/implementation-artifacts/18-4-forged-ideas-list-page.md`
- Sprint key: `18-4-forged-ideas-list-page`
- Epic 18 story map: 18.1 `done` · 18.2 `review` (module identity) · 18.3 `ready-for-dev` (index.md spike) ·
  **18.4 (this story)** · 18.5 `backlog` (gated by 18.2).
- **18.4 is gated by nothing.** Not by 18.2 (`bmad-forge-idea` is a `core` skill — same finding as 18.3) and
  not by 18.3 (§3 — the blurb half comes from the memlog's own `goal:`).
- Expect concurrent edits. At `baseline_commit` the tree was already dirty with another session's
  `Charts.cs` / `HierarchyExplorer.cs` work (Epic 20 rollout). Never `git reset --hard`, `git checkout --`, or
  `git clean` (CLAUDE.md).
- New files expected: `IdeasModel.cs` (or `IdeaEntry.cs`), `IdeasTemplater.cs`, tests. Modified: `SiteNav.cs`,
  `SiteGenerator.cs`, `DashboardViewBuilder.cs`, `StatusStyles.cs` (accent token), `specscribe.css` (section
  chrome, if the existing list grammar does not already cover it).
- Generate to `SpecScribeOutput/` only ([[generate-output-dir-is-specscribeoutput]]).

### References

- [Source: `_bmad-output/planning-artifacts/epics.md` §"Story 18.4: Forged Ideas List Page", §"Epic 18: BMad Module & Expansion Coverage Exploration", and the "Stories 18.3–18.4 added 2026-07-19" seating comment] — the verbatim ACs, and the 18.3-dependency claim §3 corrects.
- [Source: `_bmad-output/planning-artifacts/epics.md:87`, `:249`] — FR36's wording and its epic mapping; see Open Question 2.
- [Source: `_bmad-output/planning-artifacts/epics.md:137`] — NFR8 exact wording.
- [Source: `.claude/skills/bmad-forge-idea/SKILL.md`] — the whole emitter contract: §Set up the session (memlog init fields, `{workspace}` binding), §The forge (entry-type vocabulary), §Exits (the three terminal states, `forged-idea.md`, the always-rendered `forge-report.html`, the `status: complete` flip).
- [Source: `.claude/skills/bmad-forge-idea/customize.toml` §`forge_output_path`, §`run_folder_pattern`] — `{output_folder}/forge/{slug}`, both overridable.
- [Source: `_bmad/scripts/memlog.py`] — the `.memlog.md` file shape, the frontmatter `split`/`render` contract, and invariant 3 ("no lifecycle status") that the forge's `status: complete` contradicts.
- [Source: `_bmad/core/module-help.csv:14`, `_bmad/_config/skill-manifest.csv:7`] — `bmad-forge-idea` ships in **`core`**; its declared output is `{output_folder}/forge` and its artifact is a "refined-idea brief (optional)".
- [Source: `src/SpecScribe/ForgeOptions.cs` — `SourceDirName`, `OutputDirName`, `Resolve`, `AdrOutputSubdir`] — `SourceRoot` **is** `_bmad-output`, so forge workspaces land inside it; also the `ForgeOptions` naming collision (§11).
- [Source: `src/SpecScribe/PathUtil.cs` — `IsIgnoredSourceFile`] — dotfiles (naming `.memlog.md` explicitly) are neither rendered nor reported.
- [Source: `src/SpecScribe/SiteGenerator.cs` — `EnumerateSourceFiles`, `IsIgnored`, `IsDataSource`] — only non-ignored `*.md` under `SourceRoot` becomes a page; the two named non-`.md` data sources the watcher routes (§8).
- [Source: `src/SpecScribe/SiteGenerator.cs` — `BuildMemlogMap`, `SelectMemlogUpdatedByFamily`, `MemlogUpdatedPattern`] — the existing `.memlog.md` reader, the date regex to reuse, and the `hasScopedMemlog` regression (§7a).
- [Source: `src/SpecScribe/SiteGenerator.cs` — `RegenerateAdrs`, the synthesized-landing block and `landingPathAlreadyWritten`] — the list-page precedent, and the landing collision §5 avoids.
- [Source: `src/SpecScribe/SiteGenerator.cs` — `WriteOutput`] — takes a `string` and populates `_spaCapture`; the whole "carry the report" mechanism (§4).
- [Source: `src/SpecScribe/SiteGenerator.cs` — `UnrecognizedTopLevelFolders`, `EpicsFamilyPages`, `ClearEpicsFamilyOutputs`, the `consumedArtifacts` set] — the structure notice, the epics-family cleanup Ideas must stay out of, and the duplicate-page suppression seam (§6).
- [Source: `src/SpecScribe/DashboardViewBuilder.cs` — `KnownIndexGroups`, `IsWellKnownTopLevelFolder`] — the folder-classification gate and its explicit do-not-extend warning (§6).
- [Source: `src/SpecScribe/SiteNav.cs` — the output-path constants, `HasWorkGraph`, `Build`'s gate parameters and its `<remarks>` on the data-signal tradeoff, the module-doc loop's first-wins-plus-`Skipped` precedent] — the registration template (§6) and the duplicate-resolution discipline (§5).
- [Source: `src/SpecScribe/ListRow.cs`] — Story 10.8's shared row anatomy: `Render`, `Chip`, `PrimaryLink`, `EmptyState`, the `data-sort-*` attributes and the `list-row-accent-*` slot.
- [Source: `src/SpecScribe/StatusStyles.cs` — `FreeTextBadge`, `AdrAccentToken`, `ForSprint`, `CanonicalRank`] — the never-colour-only badge rule and the three-way accent mapping to mirror (§12).
- [Source: `src/SpecScribe/AdrModel.cs` — `AdrEntry`] — the row-model shape to mirror for `IdeaEntry`.
- [Source: `src/SpecScribe/ArtifactCoverage.cs`] — the pure-`Build` + `Empty` + `IsEmpty` model shape, and the never-throws/degrade-to-empty contract (AD-4 / NFR2).
- [Source: `src/SpecScribe/AdapterDiagnostic.cs`, `src/SpecScribe/DiagnosticsTemplater.cs` — `DiagnosticAnchorRoot`] — the closed five-value category set and the anchor-root contract (§10).
- [Source: `src/SpecScribe/Frontmatter.cs`] — the modelled fields, including `Sources` (§9).
- [Source: `src/SpecScribe/MarkdownConverter.cs` — `ExtractFirstH1`, `ReadAllTextShared`] — title fallback and the shared-read helper `BuildMemlogMap` already uses.
- [Source: `tests/SpecScribe.Tests/SiteGeneratorWebviewTests.cs:702-706` *(as of 2026-07-27)*] — the existing regression for a file literally named `index.md` in a landing slot; §5 explains why it does not apply here.
- [Source: `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` — `GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens`] — the golden gate (Task 9).
- [Source: `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md` §AD-1, §AD-2, §AD-4] — shared core, host-neutral view models, additive/non-blocking optional providers.
- [Source: `docs/adrs/0013-text-twin-is-the-no-js-contract.md`] — the JS-off contract Task 8 verifies against.
- [Source: `_bmad-output/implementation-artifacts/18-3-bmad-index-docs-contract-spike.md` §§1–10] — the sibling story's `core`-not-BMM finding, its path-resolution chain (reused by §9), its diagnostics discipline, and its `ListRow` reuse rule.
- [Source: `_bmad-output/implementation-artifacts/18-1-bmad-module-landscape-and-coverage-spike.md` §Review Findings] — the review failures this story is written to avoid: unrecorded external evidence, fallbacks with no category/wording, falsified `git status` claims, drifting line citations.
- [Source: `CLAUDE.md`] — shared-`main` conditions, live-browser verification, the ADR-proposal trigger, and the "structural scope changes land in `epics.md` **and** `sprint-status.yaml` in the same change" rule.
- **Memory:** [[create-story-elicit-visual-intent]] (the four owner decisions above), [[owner-verify-iterate-then-epic-end-review-workflow]] (the verify round is a designed stage), [[coverage-epics-seeded-25-5-25-6-epic-27]] (the vocabulary-collision precedent → §11), [[story-21-1-code-review-done]] (phantom coverage → D4), [[story-23-3-baseline-surfaces-done]] (nested-document defect → §4), [[story-6-6-deferred-cleanup-done-spa-at-scale-perf]] (byte-blind SPA chunker → §4), [[story-20-6-text-twin-audit-done]] (how to prove JS-off), [[gitmetrics-3s-timeout-silent-deep-git-loss]] (the rotating deep-git flakes), [[golden-diff-normalization-gotchas]] + [[story-18-2-module-identity-done]] (golden-gate traps), [[shared-main-concurrent-edit-loss-verify-after-edit]], [[story-artifact-prefix-collision-fixed]] (slug collisions), [[generate-output-dir-is-specscribeoutput]], [[cite-adrs-by-symbol-not-line-number]].

### Git intelligence summary

No forge workspace has ever existed in this repo: `_bmad-output/forge/` does not exist, and
`git log --all --diff-filter=A -- '*forged-idea.md' '*forge-report.html'` returns nothing. The only
`.memlog.md` additions in history are the four planning-session logs (§2), all landed in the three initial
product commits `08972ea` / `4f8b24b` / `365e1c4`. `src/` contains **no** reference to the forge skill —
every `Forge*` hit is SpecScribe's own `ForgeOptions` (§11), and every `memlog` hit is the coverage-panel
freshness path (§7). So this story starts from a genuinely clean slate on the artifact side and must create
its own fixtures (Task 2).

Recent commits (`32fd282` `86b35c2` `aed74c0` `261b300`) are Epic 20 / 22 / 23 / 25 work plus 18.1's code
review and ADR 0015's ratification — none touch the doc-rendering, nav-gating, or memlog paths this story
extends. Operationally relevant: `Charts.cs` and `HierarchyExplorer.cs` were **already modified in the working
tree** at `baseline_commit` by a concurrent Epic 20 session. Neither is this story's; read, do not tidy.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

## Open Questions for the Owner

1. **The `in-progress` bucket's visible label (from D2).** With `Clarified` folded in, that section will hold
   both genuinely-unfinished sessions and completed-but-not-hardened ones. "In progress" is then literally
   wrong for half of them. The implementation ships the decision as stated; if you would rather the *heading*
   read something like "Open / clarified" while the verdict token stays `in-progress`, say so and it is a
   one-line change in the verify round. No answer needed to start.

2. **FR coverage — FR36 does not obviously cover this story.** FR36 is *"Explore and provide baseline coverage
   for BMad's own **module and expansion ecosystem** beyond the BMM core … mapping each module's distinctive
   artifacts to the shared adapter contract"* [epics.md:87]. But `bmad-forge-idea` ships in **`core`**, not a
   module — the same finding Story 18.3 records for `bmad-index-docs`. So Epic 18's declared FR is a stretch for
   both 18.3 and 18.4. Three options: (a) widen FR36's wording to cover core-skill artifact surfaces; (b) mint a
   new FR (highest today is FR42, added with Epic 27); (c) accept the stretch and note it. Whichever you pick,
   Task 9 records it in `epics.md`. Defaulting to **(c)** if unanswered, since it blocks nothing.

3. **Should the Ideas nav entry live in `Project` or somewhere else?** §6 argues Project (alongside Readme,
   PRD, Brief, ADRs, Spec kernels) because an idea is neither tracked work nor a derived metric. Easy to move in
   the verify round; no answer needed to start.
