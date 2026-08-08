# Story 17.5: Large-File Investigation (CSS and Kindred Hotspots)

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->
<!-- created 2026-08-08 (create-story 17.5) at baseline_commit e8a689d. Every measurement in this file was
     taken at that revision. Line numbers WILL drift on shared `main` — re-resolve by SYMBOL, never by `:NNN`.
     The *measurements* are reproducible: every one names the command that produced it. -->

**baseline_commit:** `e8a689d` (`Merge branch 'worktree-story-16-1-decisions'`)

## Story

As the SpecScribe maintainer preparing the codebase for outside contributors,
I want a deliberate investigation of oversized source files — especially `src/SpecScribe/assets/specscribe.css` and any C#/TS peers that repeatedly absorb every feature change —
So that we have a concrete, sequenced plan to split or modularize them before release hardening locks the shape in.

## Acceptance Criteria

Reproduced verbatim from `epics.md` § Epic 17 → Story 17.5. **Read the two ⚠ callouts before acting** — AC #2's
sequencing premise is broken, and a large part of AC #1's CSS analysis has already been done by ADR 0018.

1.
**Given** the current `specscribe.css` (and a shortlist of other large/hotspot files identified by size + change frequency)
**When** the investigation runs
**Then** it records measured size (lines / bytes), ownership hotspots (which features keep appending), coupling risks (regen/golden impact, webview theming bridge), and 2–3 viable modularization options (e.g. layer split by domain: base tokens / chrome / charts / code-pages / insights) with trade-offs
**And** it does **not** perform a big-bang rewrite in this story — findings + a recommended sequence are the deliverable (implementation may land here only for a thin, reversible first slice if the recommendation is unambiguous and tests stay green).

> ⚠ **A large part of this analysis already exists and must not be re-derived: [ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md) § Addendum.**
> Story 23.4 measured the whole stylesheet by *what blocks each rule from ceasing to exist*, and published the
> result as `npm run report:ir-content-residue` → `web/measurements/ir-content-residue.{json,txt}`. That is the
> domain decomposition AC #1 asks you to invent, already computed and already owner-visible. **Start from it.**
> Two cautions: (a) it is **stale** — it reports `totalCarriedRules: 1420` while `ir-content.manifest.json` at
> HEAD reports `carriedRules: 1475`, so re-run the report before quoting a bucket size; (b) it buckets the
> *extracted layer*, not the source file, so ~249 source rules that the extractor drops as unused are absent
> from it entirely (§ B.3).
>
> ⚠ **"the golden impact" in AC #1 names a gate that no longer exists.** `GoldenContentFingerprint` was retired
> by [ADR 0034](../../docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) (Story 23.6);
> `tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` carries only its tombstone. The live CSS gates are
> `check:ir-content` and `check:tokens`. Read § C for what each can and cannot see.

2.
**Given** Stories 17.1 (structural sweep) and 17.3 (performance) may overlap
**When** this investigation concludes
**Then** its recommendations are fed into 17.1/17.3 Dev Notes (or scheduled follow-on tasks) so the hardening epic does not rediscover the same debt
**And** any accepted "leave as-is for preview" decision is explicit with rationale (not silent).

> ⚠ **AC #2's sequencing premise is BROKEN AT HEAD: Story 17.1 has already been implemented.**
> `sprint-status.yaml` at `e8a689d`:
>
> | story | status at HEAD | can this story still feed it? |
> |---|---|---|
> | 17.1 Structural sweep | **`review`** — dev-story completed 2026-08-07 | ❌ **No.** The sweep already ran. |
> | 17.2 Security | `ready-for-dev` | ✅ yes (but AC #2 does not name it) |
> | 17.3 Performance | `ready-for-dev` | ✅ **yes** — the one AC #2 target still open |
> | 17.4 Burndown | `backlog` | ✅ yes — and it is the correct home for 17.1-shaped leftovers |
>
> Story 17.1's own create-story record already flagged this as an open owner question
> (*"17.5 has not run though its AC #2 says it should feed 17.1"*) and ran anyway. So AC #2's first clause is
> **half-dischargeable**: feed 17.3 as written, and route anything that would have been 17.1's into **17.4's
> burndown** (its ACs 2–3 exist precisely to seat unowned work), naming the redirect explicitly. Do **not**
> silently drop a finding because its named recipient closed.
>
> **This is the fourth consecutive Epic 17 story whose AC premises are stale** (17.1: 3/3 named examples closed;
> 17.2: 3/3 closed; 17.3: 3/4 closed; 17.5: the golden gate retired + the recipient story already run). That is
> a signal about `epics.md`, not a coincidence — see **Q1**.

## Scope

**In scope**

- Measured size + growth + churn for `specscribe.css` and a shortlist of kindred hotspots (§ A).
- The coupling map: every consumer of `specscribe.css` and what each one assumes (§ C).
- 2–3 named modularization options with the constraints that actually bind them, and a recommended sequence (§ D).
- Feeding 17.3 and 17.4 (AC #2), with explicit "leave as-is for preview" decisions where that is the answer.
- **At most one** thin, reversible first slice — and only if § D's recommendation is unambiguous. § E argues the
  obvious candidate is **not** safe, and names what to do instead.

**Out of scope — and who owns it instead**

| Not this story | Owner |
|---|---|
| Fixing the duplicate-selector / dead-rule findings in the stylesheet | **Story 17.1** (already implemented — leftovers go to 17.4) |
| ReDoS / `S4036` / CSP / dependency audit | **Story 17.2** |
| Perf measurement, the at-scale byte-bounding cluster, `SPECSCRIBE_PHASE_TIMING` | **Story 17.3** |
| Disposing the 13 clusters; seating candidates; the absent TS test harness; `check:ir-content` re-measurement | **Story 17.4** (ACs 2–4) |
| `.coverage-card` vocabulary collision (two components, one class name) | **Epic 27** — re-routed there by 17.1 |
| Retiring `HtmlRenderAdapter*.cs`; retiring the `ir-content` layer itself | **Story 23.6** / **Epic 22** |
| Any *visual* change to the rendered portal | nobody — this story must not move a pixel |

**Boundary discipline.** `specscribe.css`, `SiteGenerator.cs`, `Charts.cs` and `GitMetrics.cs` appear in more than
one in-flight story's File List. Per CLAUDE.md § *Scoping a code review*, **attribute by hunk, not by file**, and
say so in the record.

---

## § A. Measured: size, growth, and churn (at `e8a689d`)

### A.1 The shortlist — size

Tracked files only; `node_modules`, lockfiles and `chat.json` excluded.

| file | lines | bytes | area | generated? |
|---|---:|---:|---|---|
| `src/SpecScribe/assets/specscribe.css` | **7,881** | **334,629** | src | authored |
| `src/SpecScribe/SiteGenerator.cs` | **7,144** | **466,882** | src | authored |
| `web/assets/ir-content.css` | 6,360 | 186,428 | web | **generated** (from the row above) |
| `src/SpecScribe/Charts.cs` | 3,734 | 242,391 | src | authored |
| `src/SpecScribe/assets/specscribe.js` | **3,299** | 186,237 | src | authored |
| `extension/src/extension.ts` | 2,407 | 139,008 | extension | authored |
| `src/SpecScribe/GitMetrics.cs` | 1,519 | 96,056 | src | authored |
| `src/SpecScribe/HierarchyExplorer.cs` | 1,140 | 78,060 | src | authored |
| `src/SpecScribe/CodeFileTemplater.cs` | 1,102 | 69,330 | src | authored |
| `src/SpecScribe/Commands.cs` | 1,090 | 71,968 | src | authored |
| `src/SpecScribe/BmadCommands.cs` | 1,033 | 57,561 | src | authored |
| `src/SpecScribe/EpicsParser.cs` | 1,004 | 53,950 | src | authored |

Whole-repo totals: `src` 160 files / 65,752 lines; `tests` 143 / 59,088; `web` 95 / 52,004; `extension` 7 / 3,017.
**`specscribe.css` alone is 12 % of `src/`'s line count; `specscribe.css` + `SiteGenerator.cs` together are 23 %.**

### A.2 Growth — the "before more feature CSS accumulates" premise is three weeks late

The 2026-07-18 seating note in `epics.md` asked to *"propose a split path **before more feature CSS
accumulates**"*. It has accumulated. Sampled by `git show <rev>:<path>`:

| date | `specscribe.css` | `SiteGenerator.cs` | `specscribe.js` |
|---|---:|---:|---:|
| 2026-07-05/06 (first) | 1,245 | 565 | 134 |
| 2026-07-16 | 4,648 | 3,320 | 757 |
| 2026-08-01/02 | 7,756 | 7,110 | 3,262 |
| **2026-08-07 (HEAD)** | **7,881** | **7,144** | **3,299** |

Lifetime churn on `specscribe.css`: **+10,522 / −2,642 across 177 commits** — deletions are **25 %** of
additions. `SiteGenerator.cs`: +9,203 / −2,059 across 143 commits. **Both files are ~6× larger than they were
five weeks ago, and neither has ever had a shrinking phase.** Report this plainly: the story's own framing
assumed a pre-accumulation window that has closed.

### A.3 Churn — which files absorb every change

`git log --no-merges --name-only`, 447 commits all-time / 334 since 2026-07-11.

| path | commits (all-time) | since 2026-07-11 |
|---|---:|---:|
| `src/SpecScribe/assets/specscribe.css` | **177** | 133 |
| `src/SpecScribe/SiteGenerator.cs` | **143** | 114 |
| `src/SpecScribe/Charts.cs` | 113 | 84 |
| `src/SpecScribe/HtmlTemplater.cs` | 61 | 23 |
| `src/SpecScribe/assets/specscribe.js` | 58 | 50 |
| `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` | 54 | 52 |
| `src/SpecScribe/SiteNav.cs` / `EpicsTemplater.cs` | 40 / 40 | 24 / 18 |
| `extension/src/extension.ts` | 22 | 22 |

**Size and churn agree.** The two largest files are also the two most-touched, by a wide margin — that is the
"repeatedly absorbs every feature change" property AC #1 asks you to identify, and it is confirmed on both axes.
`HtmlTemplater.cs` is the notable *divergence*: high all-time churn (61) that has largely stopped (23 since
07-11), consistent with Epic 23 moving rendering out of C#.

### A.4 Static-analysis density — and two files where "zero findings" means "not analyzed"

From `.specscribe/analysis/` (ADR 0023 digest). **⚠ The digest is STALE** — `provenance.analysisRevision` is
`01acf5b1`, `evaluatedAtRevision` is `c73ebcb`, and HEAD is `e8a689d`. Per CLAUDE.md's read-time rule that is
stale regardless of `isStale`. **Re-run `node tools/analysis-digest/index.mjs` before quoting any line number.**
Counts are directionally usable; line numbers are not.

| file | observations | lines | per 100 lines |
|---|---:|---:|---:|
| `src/SpecScribe/SiteGenerator.cs` | 96 (19 error / 47 warn / 30 note) | 7,144 | 1.3 |
| `src/SpecScribe/EpicsParser.cs` | 51 | 1,004 | 5.1 |
| `src/SpecScribe/Charts.cs` | 49 | 3,734 | 1.3 |
| `src/SpecScribe/RenderParity.cs` | 44 | 474 | 9.3 |
| `extension/src/extension.ts` | 11 | 2,407 | 0.5 |
| `src/SpecScribe/assets/specscribe.css` | **9** | 7,881 | 0.1 |
| `src/SpecScribe/assets/specscribe.js` | **0 — no shard** | 3,299 | — |
| `web/assets/ir-content.css` | **0 — no shard** | 6,360 | — |

**Two corrections this table forces:**

1. **`specscribe.js`'s zero is an EXCLUSION, not cleanliness.** `docs/SonarCloudSetup.md` § *Known gap:
   `specscribe.js` is not analyzed* states it outright: the file *"is registered by the scanner but produces no
   `ncloc` and zero"* findings, and *"'No findings in `specscribe.js`' means **not analyzed**."* So the repo's
   **third-largest authored file has no static analysis at all** — while `web/assets/ir-content.css` is
   explicitly in `sonar.exclusions` (`.github/workflows/build-test-analyze.yml`) because it is generated.
   CLAUDE.md's "a digest that exists with zero observations *is* a real clean answer" does **not** apply to an
   excluded path. This is 17.4 AC #3's `specscribe.js`-invisible-to-static-analysis candidate, independently
   confirmed here — say so, and hand it back to 17.4 rather than re-seating it.
2. **`RenderParity.cs`'s billing as *"by a wide margin the densest file in the repository at roughly one finding
   every six lines"* (`deferred-work.md`, routed to this story) does not reproduce at HEAD.** At 44 obs over 474
   raw lines it is 9.3/100 — tied with `DeferralHeuristics.cs` and *behind* `Program.cs` (18.4), `RetroActionStyler.cs`
   (13.6) and `RetroParser.cs` (12.0). The gap is the denominator: the original used Sonar `ncloc` (309), this
   table uses raw lines. Both are defensible; the *superlative* is not. Also note **what the file is**: a
   semantic-parity harness (`SemanticFacts` / `FromPageView` / `Extract`) that verifies delivery surfaces —
   verification code, not rendered output. Correct the ledger entry rather than inheriting its framing.

---

## § B. `specscribe.css` anatomy

### B.1 It is already sectioned — 58 banner blocks, and they are a domain map

`grep -n '^/\* ={5,}'` finds **58 banner comments** (`/* ====== NAV ====== */` and kin) plus a **123-line
preamble** holding the `:root` token block. **A split does not need a new taxonomy; it needs to promote the one
that is already there.** The ten largest sections:

| lines | section |
|---:|---|
| 668 | `SPRINT STATUS (Story 2.3)` |
| 514 | `ACCESSIBILITY (skip link + focus ring)` — in practice also holds the Hierarchy Explorer component (Story 20.5) |
| 513 | *(unbannered block at `:2063`)* — follow-up/work-graph vocabulary |
| 417 | `ARTICLE BODY` |
| 406 | `STATUS LEGEND (Story 8.2)` |
| 367 | `PAGE HEADER` |
| 310 | `NAV` |
| 254 | `SUNBURST` |
| 226 | `GIT INSIGHTS HUB (Story 3.8)` |
| 224 | `CODE MAP (source-code treemap)` |

Note the shape: **no section exceeds 9 % of the file.** There is no single dominant blob to carve off — the
file is large because it has ~58 tenants, which is exactly the profile a domain split addresses well and a
"extract the big part" split addresses badly.

⚠ **Three banners have drifted from their contents.** `ACCESSIBILITY` holds the whole Hierarchy Explorer
component; three sections at `:1864`, `:2003`, `:2063` have banner rules with no title line. Any split keyed on
banner text must read the block, not the banner.

### B.2 Rule inventory

- **1,750 top-level rule blocks** + **23 top-level at-rules** (comment-aware brace scan).
- **26 `@media`** queries, **5 `@keyframes`**, **1 `:root` block** (merged from two by Story 17.1; `check:tokens`
  now pins *emitted block count == source block count*, so re-splitting `:root` turns that gate red).

### B.3 How much of it is load-bearing for the rendered site

`web/assets/ir-content.manifest.json` at HEAD lists **1,501 source rules**, of which **1,480 are carried** into
the scoped layer and **21 are re-homed unscoped** (15 runtime body classes → `runtime-body.css` per ADR 0039,
3 shared primitives → `shared-primitives.css` per ADR 0029, 3 root-level → `base.css`). **Nothing in the
manifest is "dropped".**

Against 1,750 counted top-level rules, that leaves **~249 source rules (~14 %) absent from the manifest** —
i.e. dropped by the extractor as unused. **⚠ Verify this figure before quoting it**: the gated manifest
deliberately omits the whole-corpus stats (`ir-content-build.mjs` — they move on any `specscribe.css` edit and
would redden CI on unrelated commits), so the authoritative numbers are `extract:ir-content`'s own console
`source rules` / `dropped — unused` lines. Run the full order in § C.4 and read them off.

The headline that matters: **~86 % of this stylesheet is still reachable by the shipped site.** ADR 0018's
"bounded layer" property is real but thin — it bounds by ~14 %, not by the 62 % the ADR's original Decision
section reported (that figure predates Story 23.4 widening extraction from 4 families to the whole site; the
Addendum says so).

### B.4 The residue buckets — the decomposition AC #1 asks for, already computed

From ADR 0018 § Addendum / `web/measurements/ir-content-residue.json` (**figures are Story 23.4's and now
stale — re-run `npm run report:ir-content-residue`**):

| bucket | rules | blocker to it ceasing to exist |
|---|---:|---|
| `card` | 459 | Epic 22 — the IR carries no per-family view models |
| `other` | 396 | Epic 22 — uncategorized injected vocabulary |
| `chart` | 284 | Epic 22 — the IR carries no structured chart data |
| `chrome` | 97 | **permanent by design** — ADR 0024 + owner decision D2 keep C# composing the region |
| `prose` | 93 | **none — authorable in `web/` today** |
| `status` | 91 | the token bridge — must stay in step with the six `--status-*` tokens |

**Read this as the answer to "which features keep appending":** 93.5 % of the carried layer is the portal's
bespoke visual vocabulary — **651 distinct classes emitted as rendered HTML by ~25 C# templaters**. The
stylesheet grows because every feature epic adds a templater and its vocabulary. A split that does not track
that emitter→vocabulary relationship will be re-sorted by the next epic.

---

## § C. The coupling map — six consumers, each with a different assumption

**This is the section AC #1 most needs and the one a naive split will violate.** Every one of these was
verified by reading the code at `e8a689d`.

### C.1 The consumers

| # | consumer | binding | what it assumes |
|---|---|---|---|
| 1 | `SiteGenerator.CopyEmbeddedAsset("SpecScribe.assets.specscribe.css", ForgeOptions.StylesheetName)` | one embedded resource → one output file | **exactly one file**, named by the `StylesheetName` const |
| 2 | `PathUtil.RenderHeadOpen` | emits one `<link rel="stylesheet" href="…?v={AssetVersion}">` | **one `<link>`**, one cache-bust token |
| 3 | `WebviewRenderAdapter` | `ReadEmbedded(...)` → `.Replace("__CSS__", …)` then `__THEME_CSS__` | **whole sheet inlined**, and the theme bridge (`specscribe-webview-theme.css`, 597 lines) must land **after** it — cascade order is load-bearing |
| 4 | `web/scripts/ir-content-lib.mjs` `SOURCE_CSS` | one path; hand-written comment/brace scanner | **one file**, parseable by a hand-rolled tokenizer with no CSS-parser dependency (ADR 0010) |
| 5 | `web/scripts/extract-tokens.mjs` / `tokens-lib.mjs` | the `:root` block | **one `:root` block** (gate asserts emitted count == source count) |
| 6 | tests | `StylesheetTests` (~70 `ReadStylesheet()` assertions), `HierarchyColorizeTests.Stylesheet()`, `WebviewThemingTests`, `ChangeSurfaceTests` | the resource name `SpecScribe.assets.specscribe.css` **and** the literal repo path `src/SpecScribe/assets/specscribe.css` |

Also relevant: `web/scripts/sync-runtime-assets.mjs` **deliberately does not carry `specscribe.css`** — its own
comment says *"Serving the whole monolith would reverse 23.2's central decision."* `check:assets` therefore has
no opinion here; do not expect it to catch anything.

### C.2 The trap that kills the obvious "move chrome to `web/`" idea

ADR 0018's Addendum says the `chrome` bucket *"needs not deletion but a change of **provenance** — an owned
sheet in `web/`"*. **Do not read that as license to move those rules out of `specscribe.css`.**
`WebviewRenderAdapter` renders the shared nav/breadcrumb chrome (`HtmlRenderAdapter.Shared.RenderNavMarkup`) and
**inlines `specscribe.css` to style it** — the webview never goes through Nuxt. Move the chrome rules to `web/`
and the VS Code panel loses its nav styling, silently, on a surface no CSS gate inspects. If chrome is
re-homed, it must be re-homed to a sheet the **webview** can also inline. Say this in the record; it is the
single most likely way this story's recommendation gets implemented wrongly later.

### C.3 The trap that kills "prune what nothing renders"

**A rule with no matching element in server markup can still be load-bearing.** `specscribe.js` builds a hidden
probe node — literally `svg.setAttribute("class", "sunburst ss-hierarchy-probe")`, appended off-screen and
`aria-hidden` under the page's `.ir-content` wrapper — applies the server-emitted `colorClass` verbatim to it,
and reads `fill`/`stroke` back out of the cascade: *"A hard-coded hex would survive a token change and quietly
lie about it (AD-7)"*. **The probe exists only at runtime, so no harvest of server-rendered markup can see it**,
and the rules it depends on look dead to every static signal.

`web/scripts/ir-content-lib.mjs` carries a **hand-maintained seed/allowlist** for exactly this, and its comments
record three real incidents:

- `sb-%s` / `sb-seg` / `sb-noplan` / `sb-followup-*` / `sb-unplanned` — *"[incident: sunburst rendered all-black]"*
- `owner-author-0..6` — *"[incident: `check:ir-content` failed in CI with `+.ir-content .ownership-legend-swatch.owner-author-2`, absent from a local harvest run one commit behind]"*
- `sprint-lane-empty` / `sprint-filter-empty` / `chart-empty` — data-conditional classes absent when the condition is false
- and a **structural** class: milestone-band markup only a **non-BMad** repo can produce, which *"no amount of regenerating fixes"*

**Consequence for this story:** any deadness claim about a CSS rule needs three independent confirmations (no
emitter in `src/`, absent from generated pages, and already pruned by `extract:ir-content` — the standard
Story 17.1 used), *plus* a check that no `getComputedStyle` reader depends on it. Two of those signals agreeing
is not enough.

### C.4 The regeneration order is load-bearing, and skipping a step reads as "my selector is wrong"

Per CLAUDE.md § *Changing `specscribe.css`?*. Any change to the stylesheet — including a pure file split —
must be measured through:

```sh
dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental   # re-embed the asset
dotnet run --project src/SpecScribe -- generate                  # IR now has the new markup
cd web && npm run extract:ir-content && npm run check:ir-content # derive from THAT IR
cd web && npm run build:package                                  # renderer bundles the CSS
dotnet run --project src/SpecScribe -- generate                  # render with it
```

Two `generate`s, deliberately. **An incremental build reuses the cached assembly and never re-embeds a changed
asset** — you would be inspecting a page whose CSS predates your edit.

⚠ **`check:ir-content` is expected RED in a fresh worktree** and that is environmental, not drift: with no
generated IR, the harvest finds nothing and ~everything is pruned. Do not regenerate the baseline to make it
green. (17.4 AC #4 owns establishing this gate's true state; § C.4's order is the same one it must use.)

### C.5 What each gate can actually see

| gate | subject | **cannot** see |
|---|---|---|
| `check:ir-content` | `web/assets/ir-content.css` vs `specscribe.css` | a bug in its own derivation — it re-derives through the same `harvest`/`selectorIsUsed` code, so a wrongly-dropped rule is dropped identically on both sides and the diff is empty. `web/test/ir-content-harvest.test.mjs` pins the derivation; extend **that** |
| `check:tokens` | the `:root` token block | anything outside `:root` |
| `check:parity` | the RENDERER over a **frozen** corpus | **any C#-side change** — proven 2026-08-01 when a nav element removed from every page left all 24 routes byte-identical |
| `check:assets` | runtime asset copies in `web/public/` | `specscribe.css` — not on its list, by design |
| `StylesheetTests` | substrings of the embedded sheet | whether a rule *reaches* an element |

**Nothing in this table proves rendered output is unchanged after a C#-side or file-layout change.** Per
CLAUDE.md § *Verification*, the evidence for "no pixel moved" is **live-browser computed styles**, not a green
`npm run check`. Budget for it.

---

## § D. The three modularization options (the AC #1 deliverable)

Each option is stated with the constraint that actually binds it. **Recommendation: Option A, then a bounded
slice of Option C. Option B is a trap.**

### Option A — authored partials, concatenated at build into one embedded `specscribe.css`

Split the source into `src/SpecScribe/assets/css/*.css` (one per promoted banner section, § B.1) and add an
MSBuild step that concatenates them **in a declared order** into the single `specscribe.css` that gets embedded.

- ✅ **All six consumers in § C.1 are untouched.** One resource, one `<link>`, one webview inline, one
  `SOURCE_CSS`, one `:root`, same resource name.
- ✅ **Byte-identical output is achievable and provable** — if concatenation order matches current source order,
  the embedded bytes are unchanged, so `check:ir-content` / `check:tokens` diffs are empty *for the right reason*.
  That makes it the only option with a cheap correctness proof.
- ✅ Reviewable diffs; a feature epic appends to its own partial instead of the shared bottom of a 7,881-line file.
- ⚠ **Costs and unknowns to measure, not assume:** (a) `ChangeSurfaceTests` asserts the literal path
  `src/SpecScribe/assets/specscribe.css` in the ADR 0007 change surface — decide whether the surface names the
  partials or the concatenation, and record it; (b) SpecScribe documents its own repo, so the partials each gain
  a `code/**.html` page and the concatenated artifact's page changes — a **page-count change on this repo's own
  site**, which is expected and must be stated, not discovered; (c) the build step must be deterministic across
  Windows and Ubuntu (ADR 0033 requires any new gate to be proven so) — **line endings are the obvious hazard**;
  (d) an incremental build must not serve a stale concatenation, which is the same hazard CLAUDE.md already
  documents for the embedded asset.

### Option B — ship multiple stylesheets (`@import`, or several `<link>`s)

- ❌ Touches **five of six** consumers: `RenderHeadOpen` (N links + N cache-busts), `CopyEmbeddedAsset` (N copies),
  `WebviewRenderAdapter` (N inline `<style>` blocks in a **cascade-order-sensitive** sequence, ahead of the theme
  bridge), `SOURCE_CSS` (the extractor must walk N files and keep its manifest's `line span` provenance coherent),
  and the tests' single-resource assumption.
- ❌ `@import` specifically adds a render-blocking serial fetch per sheet on a static site with no bundler, and an
  extra CSP surface on a site that currently has **no CSP at all** (Story 17.2's finding).
- ❌ Buys nothing Option A does not, because the problem is *authoring ergonomics*, not delivery.
- **Reject it explicitly with this reasoning** rather than leaving it as an unexamined "option".

### Option C — reduce the monolith by provenance, following ADR 0018's own amended retirement path

Rather than splitting, *shrink*: move rules out to sheets that legitimately own them.

- ✅ The `prose` bucket (**93 rules, blocker: none**) is authorable in `web/` today — ADR 0018 names it as the one
  bucket its owner decision D5 actually reaches.
- ⚠ The `chrome` bucket (**97 rules**) needs re-homing, **but see § C.2** — the webview inlines `specscribe.css`
  and would lose its nav styling. Its new home must be webview-reachable.
- ❌ The `card`/`chart`/`other` buckets (**1,139 rules, 80 %**) are blocked on Epic 22 view models. **Not this
  story's, not this epic's.**
- ⚠ Removing prose rules from `specscribe.css` *does* change rendered pages unless `web/` picks them up
  identically — this is the one option that can move a pixel. It needs live-browser verification, and it
  overlaps Epic 23's in-flight surface (23.2 `in-progress`, 23.4 `review`, 23.6 `in-progress`). **Coordinate or
  defer.**

### Recommended sequence (write this up; do not execute past step 1)

1. **Option A** — mechanical, provable, unblocks everything else. The only step this story may implement.
2. **Option C's `prose` slice** — after Epic 23 settles. Route to 17.4 for seating, not to 17.1 (closed).
3. **Option C's `chrome` re-homing** — needs an ADR (it changes who owns chrome styling across two hosts).
   Propose it; do not decide it here.
4. Everything else waits on Epic 22. **Record that as an explicit accepted limitation for preview**, per AC #2's
   second clause — the monolith ships at ~7,900 lines for the community preview, deliberately.

### The C# / TS peers — recommend, do not split

`SiteGenerator.cs` (7,144 lines, one `sealed class`, ~180 members, **not** `partial`) is the kindred hotspot.
`HtmlRenderAdapter` already demonstrates the pattern — it is `sealed partial` across `.cs`, `.Dashboard.cs`,
`.Epics.cs`. A `partial`-class split of `SiteGenerator` is behaviour-preserving by construction and needs no
gate. **But** it collides with three in-flight stories (17.1 `review`, 17.2/17.3 `ready-for-dev` all list it),
so recommending it is right and doing it here is not. `specscribe.js` (3,299 lines, **zero static analysis**) and
`extension.ts` (2,407 lines, **no TypeScript test harness at all** — 17.4's cluster) should be named as hotspots
whose split must wait on their coverage gap, not lead it.

**One forward-looking fact worth recording:** `SiteGenerator.MaxCodeFileBytes = 1_048_576` degrades an oversized
file's own code page to a placeholder. `SiteGenerator.cs` is at **466,882 bytes — 45 % of that cap** — and grew
+9,203 lines in five weeks. At the observed trajectory SpecScribe's largest file eventually stops rendering on
SpecScribe's own site. Not urgent; worth stating once.

---

## § E. The "thin, reversible first slice" — and why the obvious candidate is NOT it

`deferred-work.md` routes the inert `.sb-seg` hover-emphasis rules to *"Story 17.1's dead-code sweep **and**
Story 17.5's large-file investigation"*, noting they *"still ship … now accurately labelled as dead"*. Story
17.1 did **not** delete them (its record lists `.impact-shape-*`, dead `border`/`padding` resets and two
`word-break` keywords — no `.sb-seg`). They are still present at HEAD.

**Do not take this as the first slice.** The ledger's own caution — *"the comments distinguish rules that are
inert from `.sb-<status>` TOKEN rules that are still live — do not sweep the whole block"* — is stronger than it
looks:

- `HierarchyExplorer.PlanningSegClass = "sb-seg"` is **still emitted**, composed as `"sb-seg sb-<status>"`.
- It reaches the DOM only via `specscribe.js`'s hidden **probe node**, whose computed `fill`/`stroke` the chart
  reads back (§ C.3).
- `ir-content-lib.mjs` **seeds `sb-seg` explicitly** in `CONDITIONAL_CLASSES`, citing the *"sunburst rendered
  all-black"* incident.

So the block splits **per selector**, not per class:

- **Genuinely inert** — the *interaction* selectors: `.sunburst:hover .sb-seg`, `.sunburst .sb-seg:hover`, and the
  `.sunburst-panel:has(.sb-<status>-item:hover) .sb-seg:not(.sb-<status>)` emphasis set. Since Story 20.7 the only
  `svg.sunburst` in the document is the off-screen, `aria-hidden` probe — never hovered, and with no
  `.sunburst-panel` legend siblings — so these match nothing. `HierarchyExplorer.LegendHtml`'s own doc comment says
  so: *"Those rules match nothing once the SVG is gone … Recorded as a loss rather than routed around."*
- **Live** — the *token* rules `.sb-seg { stroke … }` and `.sb-<status>`, which are exactly what the probe is
  built to read.

**A correct disposition needs per-selector adjudication with live-browser confirmation** —
that is 17.1-shaped remediation work, not a reversible layout slice, and it belongs in **17.4's burndown**.

**If a first slice is taken, take Option A's step 1** — mechanical, byte-provable, and reversible by deleting
the build step.

---

## Tasks / Subtasks

- [ ] **Task 0 — Establish the measurement baseline (hard prerequisite).** (AC: #1)
  - [ ] Re-run `node tools/analysis-digest/index.mjs`; confirm `provenance.evaluatedAtRevision` equals `git rev-parse HEAD`. The digest at authoring time was 15+ commits behind. **Absent means UNKNOWN, never clean.**
  - [ ] Re-run `npm run report:ir-content-residue` (§ B.4 figures are Story 23.4's and stale — manifest says 1,475 carried, the residue file says 1,420).
  - [ ] Run the full § C.4 order once and record `extract:ir-content`'s console `source rules` / `dropped — unused` figures — these are the authoritative version of § B.3's ~249 estimate.
  - [ ] Re-verify § A's size/churn table at HEAD (it is measured at `e8a689d` and `main` moves).
- [ ] **Task 1 — Produce the measured findings.** (AC: #1)
  - [ ] Size + growth + churn for the § A.1 shortlist. Confirm or correct the "size and churn agree" conclusion.
  - [ ] Record the `specscribe.js` / `ir-content.css` analysis-exclusion finding (§ A.4) — **do not report those zeros as clean.**
  - [ ] Correct the `RenderParity.cs` "densest file by a wide margin" claim in `deferred-work.md`, naming the `ncloc`-vs-raw-lines denominator (§ A.4).
  - [ ] Ownership hotspots: derive from the residue buckets + the ~25 emitting templaters (§ B.4), not by re-reading the file.
- [ ] **Task 2 — Write the coupling map.** (AC: #1)
  - [ ] Verify all six consumers in § C.1 still bind as described; correct any that moved.
  - [ ] State the webview-chrome trap (§ C.2) and the probe/`getComputedStyle` trap (§ C.3) in the deliverable — these are the two ways a later implementer breaks this.
  - [ ] State plainly that AC #1's "golden impact" gate no longer exists and what replaced it (§ C.5).
- [ ] **Task 3 — Write up the three options + recommended sequence.** (AC: #1)
  - [ ] Option A / B / C per § D, each with its binding constraint. **Reject B explicitly, with reasons.**
  - [ ] Include the C#/TS peers as recommendations only, with their in-flight collisions named.
- [ ] **Task 4 — The first slice: decide, and justify either way.** (AC: #1)
  - [ ] If taken, it is Option A step 1 **only**, and it must be proven byte-identical: same embedded bytes ⇒ empty `check:ir-content` / `check:tokens` diffs, plus a live-browser spot-check of at least one page per family.
  - [ ] Record § E's finding that `.sb-seg` is **not** a safe slice, with the three-signal evidence.
  - [ ] **If the recommendation is not unambiguous, take no slice.** AC #1 permits implementation only in that case; a findings-only outcome is a full pass, not a shortfall.
- [ ] **Task 5 — Feed forward (AC #2), honestly.** (AC: #2)
  - [ ] Add recommendations to **17.3**'s Dev Notes (the one named recipient still open).
  - [ ] Route 17.1-shaped items to **17.4**'s burndown, stating in the record that 17.1 was already at `review` and could not receive them.
  - [ ] Record every "leave as-is for preview" decision **with rationale** — including the headline one: the monolith ships at ~7,900 lines for the community preview because 80 % of it is blocked on Epic 22.
  - [ ] Update the `deferred-work.md` entries routed here (`RenderParity.cs` density; the `.sb-seg` block) with their adjudications.

---

## Dev Notes

### Read these first, in this order

1. [ADR 0018](../../docs/adrs/0018-transitional-ir-content-style-layer.md), **especially § Addendum** — it is
   the prior art for most of AC #1's CSS half.
2. CLAUDE.md § *Which gate is which* and § *Changing `specscribe.css`? The regeneration order is load-bearing*.
3. `web/scripts/ir-content-lib.mjs` — the `CONDITIONAL_CLASSES` / seed block and its incident comments (§ C.3).
4. `docs/SonarCloudSetup.md` § *Known gap: `specscribe.js` is not analyzed*.

### Anti-patterns specific to this story

- **Do not re-derive the residue buckets.** They exist, they are gated, and re-deriving them by hand invites a
  second set of numbers that drifts from the generated one.
- **Do not treat a green `npm run check` as evidence that rendering is unchanged.** `check:parity` is
  structurally blind to C#-side changes; `check:ir-content` cannot see a bug in its own derivation. Live browser
  or it did not happen.
- **Do not regenerate any gate baseline to make it green.** If a gate moves and you did not touch rendering,
  audit the harness first — CLAUDE.md § *Never regenerate a gate's baseline reflexively*, and Epic 5's incident
  where the harness itself leaked the commit SHA.
- **Do not `git reset --hard` / `checkout --` / `clean`.** Concurrent sessions may have uncommitted work.
- **Verify after every edit.** Grep for what you just wrote before relying on it — a `Charts.cs` edit has
  silently vanished this way before.
- **Rebuild non-incrementally before trusting anything asset-related.**

### Existing patterns to reuse, not reinvent

| need | reuse |
|---|---|
| splitting a large C# type | `HtmlRenderAdapter` — `sealed partial` across three files |
| bounding an artifact by size | `SpaDelivery`'s `MaxChunkBytes` + `MaxPagesPerChunk`, with the oversized-page isolation rule |
| a hand-written CSS tokenizer | `web/scripts/ir-content-lib.mjs` — comment- and brace-aware, zero npm deps (ADR 0010). **Note its known gap**: it is not *string*-aware, so `content: "}"` would break it (a latent item already in `deferred-work.md`). Do not introduce such a string. |
| measuring generation cost | `SPECSCRIBE_PHASE_TIMING` (`SiteGenerator`) — 17.3's seam, already built |
| proving a rule is dead | Story 17.1's three-signal method: no emitter in `src/`, absent from generated pages, already pruned by `extract:ir-content` — **plus** § C.3's fourth check |

### Project Structure Notes

- **Generate to `SpecScribeOutput/` (the default). Never `--output docs/live`** — vestigial and gitignored.
- New authored CSS partials, if Option A's slice is taken, belong under `src/SpecScribe/assets/` (the embedded-asset
  home). `web/assets/` is for **generated** bridges (`ir-content.css`, `tokens.css`, `shared-primitives.css`,
  `runtime-body.css`) and hand-authored `web/`-owned sheets — do not put a C#-embedded partial there.
- `web/public/` is **a copy by contract** (`sync-runtime-assets.mjs`); never hand-edit it.
- The deliverable itself: follow the epic's precedent for investigation output — a report committed alongside the
  story (`16-1-spike-report.md`, `20-4-spike-report.md`, `22-1-spike-report.md`, `23-1-spike-report.md`,
  `25-3-spike-report.md` are the established shape). Propose `17-5-large-file-report.md` in
  `_bmad-output/implementation-artifacts/`.

### If a decision gets made here, it needs an ADR

Per CLAUDE.md § *Decision records*: **propose an ADR without being asked** for anything that changes shared
architecture or amends a prior ADR. Two candidates in this story's path:

- **Re-homing the `chrome` bucket** amends ADR 0018 § Addendum and touches ADR 0024's region seam — and must
  answer § C.2's webview question. ADR-shaped.
- **A build-time concatenation step** (Option A) changes how a shipped asset is produced. It brushes ADR 0026
  (generated layers derive from templates, not project data) and ADR 0022 (Node is a build toolchain) — read
  both before deciding whether it needs its own record or just a note.

And **read `docs/adrs/` before declaring you are crossing a project rule** — Story 21.3 announced it was
crossing the no-JS rule that ADR 0010 had already relaxed two days earlier.

---

## Previous Story Intelligence

**Story 17.3** (`ready-for-dev`, the immediate predecessor) established the Epic 17 house pattern this story
follows: reproduce the ACs verbatim, then a ⚠ table checking each named example against HEAD, then a scope
table naming who owns what is excluded. It also recorded the finding that **`epics.md`'s AC illustrations are a
2026-07 snapshot** across three consecutive stories — 17.5 is the fourth.

**Story 17.1** (`review`) is the most consequential predecessor, because it already touched this file:

- Merged `:root` to **one block** (45 tokens, unchanged — proven by `check:tokens`), merged
  `.now-next-card.active`, **deleted** the pre-rename `.impact-shape-*` toggle block as dead, removed dead
  `border: 0` / `padding: 0` resets, and replaced two deprecated `word-break: break-word` with `overflow-wrap`.
- **Did not** touch `.sb-seg` (§ E).
- Found `.coverage-card` is **not** a duplicate but two components colliding on one class name, **with the
  shipped layout depending on the leak** — re-routed to Epic 27. Do not "fix" it.
- Re-pointed `tokens-lib.test.mjs` at the invariant *"emitted block count EQUALS source block count"* after a
  legitimate source edit turned two tests red while the extractor was correct.
- Reported that `ir-content.manifest.json`'s `generatedBytes` was **12 bytes stale on `main`** before any edit.
  **Re-checked at `e8a689d`: it now matches** (186,428 = actual). That specific defect is closed; the class of
  defect — `check:ir-content` compares rules, not bytes, so it cannot see a stale byte count — is not.
- Adjudicated all seven C# reliability findings as false positives of one class (a collection mutated only
  inside a lambda/local function). **Do not re-open them.**

**Story 23.4** (`review`) produced the residue measurement this story leans on, and demonstrated the failure
mode this story must avoid: it discovered that extraction bounded to 4 families left **~58 % of the classes
other pages emit with no rule at all** — *"Nothing fails and nothing is logged; the element just renders bare."*

### Git intelligence

Recent commits are merges of per-story worktree branches (`worktree-story-16-1-decisions`,
`worktree-story-17-1-dev`, `worktree-code-review-23-3`, …) — **a single commit routinely carries several
stories' work**, because code review runs at epic end. Do not scope anything here by commit range; scope by File
List and, where files are shared, by hunk.

---

## Latest technical information

No new library or framework version is required — this is an investigation over existing code. Two version facts
that bound the options:

- **`web/` runs on `nuxt ^4.5.1` + `vitest ^4.1.10`, with a deliberate zero-dependency posture for tooling**
  (ADR 0010). ADR 0018 rejected a PostCSS/CSS-parser dependency for the extractor on exactly that ground, and
  said to revisit *"if the extractor's hand-written selector handling starts producing wrong output rather than
  merely conservative output"*. **That condition has been met once** — the `harvest` dangling-`else` bug meant
  no id was ever collected (CLAUDE.md § *The gate cannot catch a bug in its own derivation*). If Option A's
  concatenation makes the tokenizer's job harder, say so; do not add a parser dependency without an ADR.
- **`npm ci` in `web/` was broken** (EUSAGE, missing `@emnapi/runtime@1.11.3` from the lockfile), which blocks
  building the Nuxt renderer from a fresh checkout — Story 23.5 (`review`) owns the fix. **Verify whether it is
  still broken at HEAD before planning any task that needs a fresh `web/` install**, and do not run
  `npm audit fix` (it rewrites the same lockfile 23.5 is fixing).

---

## Owner Questions

None blocking. Defaults are stated; the dev may proceed on them.

1. **`epics.md`'s Epic 17 AC illustrations are stale in all four stories — amend them?** 17.1 (3/3 examples
   closed), 17.2 (3/3), 17.3 (3/4), 17.5 (the golden gate retired; the AC #2 recipient already implemented).
   Each story has carried a ⚠ correction table instead. *Default: leave `epics.md` alone, keep correcting in the
   story records.* A structural amendment would land in `epics.md` **and** `sprint-status.yaml` in the same
   change (CLAUDE.md § Decision records).
2. **AC #2 names 17.1, which is already at `review`. Confirm the redirect to 17.4?** *Default: yes — route
   17.1-shaped findings to 17.4's burndown and say so explicitly in the record.*
3. **Does the deliverable go in a separate `17-5-large-file-report.md`, or inline in this story file?** The epic
   precedent is a separate report for spikes. *Default: separate report, matching `16-1-spike-report.md` et al.*
4. **May this story take Option A's first slice, or is it findings-only?** Option A is byte-provable and
   reversible, but it adds a build step and changes this repo's own generated page set (§ D Option A, caveat b).
   *Default: findings-only — write the recommendation, take no slice.* AC #1 permits either.
5. **Epic 17's stated sequencing ("after Epics 1–15 and 18") is still unmet** — Epics 18, 20, 22, 23, 24, 25, 26
   and 27 are in flight, and `specscribe.css` gained 133 commits in the last four weeks. Any split lands on a
   moving file. *Default: proceed, and state in the report that the recommendation has a shelf life.*

---

## References

- `_bmad-output/planning-artifacts/epics.md` § Epic 17 → Story 17.5 (ACs; the 2026-07-18 seating note)
- [ADR 0018 — transitional ir-content style layer](../../docs/adrs/0018-transitional-ir-content-style-layer.md), **§ Addendum** (the residue measurement, the unreachable retirement condition, the permanent `chrome` floor)
- [ADR 0024 — SPA and webview are filtered projections of one region seam](../../docs/adrs/0024-spa-and-webview-are-filtered-projections-of-one-region-seam.md) (why `chrome` never empties)
- [ADR 0029 — unscoped shared primitive layer](../../docs/adrs/0029-unscoped-shared-primitive-layer.md) · [ADR 0039 — runtime-attached body-level classes](../../docs/adrs/0039-runtime-attached-body-level-classes.md) (the 21 re-homed rules)
- [ADR 0033 — content-drift gates are targeted and regenerable](../../docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md) (requirements on any NEW gate) · [ADR 0034](../../docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) (retired `GoldenContentFingerprint`)
- [ADR 0010 — client-side charting](../../docs/adrs/0010-client-side-charting-js-for-opt-in-analytics-surfaces.md) § zero-dependency posture · [ADR 0022 — Node is a build toolchain](../../docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) · [ADR 0026 — generated layers derive from templates](../../docs/adrs/0026-generated-layers-derive-from-templates-not-project-data.md)
- `ARCHITECTURE-SPINE.md` § AD-7 (presentation tokens are shared; host chrome is host-owned) — the reason `specscribe.js` reads colors from the cascade instead of hard-coding them
- `docs/SonarCloudSetup.md` § *Known gap: `specscribe.js` is not analyzed*; § *Coverage exclusions*
- `deferred-work.md` — the two entries routed to this story (`RenderParity.cs` density; the inert `.sb-seg` block, amended 2026-08-07) and the `specscribe.css` duplicate-selector entry flagged here for the file-scale question
- `web/scripts/ir-content-lib.mjs` (`SOURCE_CSS`, `CONDITIONAL_CLASSES`, the seed list and its three incidents) · `web/scripts/ir-content-build.mjs` (why whole-corpus stats stay out of the gated manifest) · `web/scripts/sync-runtime-assets.mjs` (why `specscribe.css` is not a runtime asset)
- `src/SpecScribe/WebviewRenderAdapter.cs` (`__CSS__` / `__THEME_CSS__` inline order) · `src/SpecScribe/PathUtil.cs` (`RenderHeadOpen`, `AssetVersion`) · `src/SpecScribe/ForgeOptions.cs` (`StylesheetName`) · `src/SpecScribe/SiteGenerator.cs` (`CopyEmbeddedAsset`, `MaxCodeFileBytes`)
- `src/SpecScribe/HierarchyExplorer.cs` (`PlanningSegClass`, `LegendHtml`'s "what does NOT survive" note) · `src/SpecScribe/assets/specscribe.js` (the probe node / `tokenFor()` cascade read)
- `tests/SpecScribe.Tests/StylesheetTests.cs`, `HierarchyColorizeTests.cs`, `WebviewThemingTests.cs`, `ChangeSurfaceTests.cs` (the four test-side bindings to the stylesheet)
- CLAUDE.md § *Concurrent work on shared `main`*, § *Which gate is which*, § *Changing `specscribe.css`?*, § *Analysis observations*, § *Verification*

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
