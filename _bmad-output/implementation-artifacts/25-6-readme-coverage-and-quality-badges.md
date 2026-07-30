---
baseline_commit: 8a2fb8352f882debb2e81c7369f52366f6a24c53 # `8a2fb83` — HEAD at authoring time (2026-07-28)
epic: 25
nfr: [NFR10, NFR11, NFR12]
frs: []
depends_on: [25-1, 25-2] # 25.1 stood up the analysis + project key; 25.2 settled what the gate asserts
blocked_by_for_gate_badge: [17-2] # the quality-gate badge cannot be green until 17.2's S6444 band is cleared — see § Read first
ships_product_code: false # documentation only. The golden fingerprint MUST NOT move.
adrs: [] # considered and declined — a README badge is not a cross-cutting contract. See § Project Structure Notes.
touches:
  - "README.md" # badge row under the H1 + a Project health table in § Continuous integration
  - "docs/SonarCloudSetup.md" # NEW § Badges (disclosure record) + correction to § What would make the gate blocking
  - "_bmad-output/implementation-artifacts/sprint-status.yaml"
# NOT src/**, NOT tests/**, NOT web/**, NOT extension/**, NOT .github/workflows/**, NOT epics.md
---

# Story 25.6: Coverage and Quality Badges on the README

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor evaluating SpecScribe,
I want the README to show current build, coverage, and quality-gate status at a glance,
So that the project's health is visible before I read a line of code.

## ⛔ Read first — five live facts, two of which invert what the upstream docs say

All figures below were measured **anonymously** (the project is `visibility: public`) against
SonarCloud's analysis of `8a2fb83` — the current `HEAD` — dated **2026-07-28T23:55:45Z**.
**Re-measure before writing any number into the README.** See § Re-measure first.

### 1. ⛔ The quality gate is RED right now. The gate badge cannot ship.

`alert_status` = **`ERROR`**. The `alert_status` badge SVG literally renders the word **`failed`**.

AC #1 forbids landing it: *"a permanently-red badge on the front page is worse than none."*
AC #2 supplies the escape hatch, and **this story exercises that hatch as written** —
build and coverage badges ship alone, and the story says so explicitly. This is the AC being
satisfied, not waived. **No `epics.md` amendment is needed; all three ACs are carried verbatim.**

### 2. ⚠ `docs/SonarCloudSetup.md` contradicts itself, and the wrong half is the operative one

The file states the precondition for a green gate in two places that now disagree:

| Location | Claim | True? |
|---|---|---|
| `docs/SonarCloudSetup.md` § *What would make the gate blocking* (~L294-303) | Only 3 issues block, both remaining ones in `web/scripts/**`, "the `csharpsquid:S6444` band **no longer drives** the *new-code* security rating" | ❌ **FALSE as of 2026-07-28** |
| `docs/SonarCloudSetup.md` § *Rule-level decisions* → *Current decisions* (~L456) | `S6444` "Drives security rating **C** and keeps `new_security_rating` at **B**" — **Scheduled → Story 17.2** | ✅ **Correct** |

The sliding `days: 30` new-code window has since swallowed the `S6444` band — exactly the failure
mode Story 25.2 named ("new code ≈ all code"). **160 `csharpsquid:S6444` issues are inside the
new-code period today.** Correcting this contradiction is **Task 3 of this story**.

### 3. ⛔ Sonar's rating scale means "fix the 3 named files" does NOT turn the gate green

Both failing conditions require rating **A**, and on Sonar's scale **A = zero issues of that class** —
not "no critical ones". Measured OPEN (`resolved=false`) issues inside the new-code period:

| Gate condition | Threshold | Actual | Open new-code issues blocking A |
|---|---|---|---|
| `new_reliability_rating` | worse than A → ERROR | **D (4)** | **12 bugs** — 1 CRITICAL, 9 MAJOR, 2 MINOR |
| `new_security_rating` | worse than A → ERROR | **C (3)** | **164 vulnerabilities** — 2 MAJOR, 162 MINOR |
| `new_maintainability_rating` | worse than A | A (1) | OK |
| `new_coverage` | < 80% | **90.5%** | OK |
| `new_duplicated_lines_density` | > 3% | 0.8% | OK |
| `new_security_hotspots_reviewed` | < 100% | 100.0% | OK |

Where they live — **the two `web/scripts/` files the setup doc names are 3 of 176**:

- **9 of the 12 bugs are in `src/SpecScribe/`** (`SiteGenerator.cs:1509/2558/2565`, `WorkGraph.cs:403`,
  `CapabilityStyler.cs:57`, `assets/specscribe.css:1623/1625/1991`, `HtmlRenderAdapter.Dashboard.cs:253`,
  `SiteGenerator.cs:2056`), 1 in `extension/src/extension.ts:1268`, 1 in `web/scripts/check-links.mjs:204`.
- **160 of the 164 vulnerabilities are `csharpsquid:S6444` in `src/`** (regex without timeout) —
  **Story 17.2's band**, per the triage table.

Fixing `check-links.mjs:204` alone moves reliability **D → C**. Fixing `experiment-two-ir.mjs:95`
alone moves security **C → B**. **The gate stays red either way.** Do not attempt it in this story.

> **The real precondition for the gate badge is Story 17.2 (the 161-issue `S6444`/`S4036` band) plus a
> 12-bug reliability sweep across `src/`.** That is a hardening epic, not a README story.
> Epic 25 is on record as forbidden to touch `src/` and `web/`.

### 4. ✅ Build and coverage badges are green today, and the coverage figure is already the CI figure

Verified by fetching the SVGs anonymously:

| Badge | Endpoint | Renders |
|---|---|---|
| Build | `github.com/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/badge.svg?branch=main` | `<title>Build, Test &amp; Analyze - passing</title>` ✅ |
| Coverage | `sonarcloud.io/api/project_badges/measure?project=IntegerMan_SpecScribe&metric=coverage` | text node `89.8%` ✅ |

AC #1's *"shows the same figure the CI analysis reports, not a separately-computed one"* is satisfied
**by construction**: the badge is rendered by SonarCloud from the same `coverage` measure the CI
analysis publishes. Nothing is recomputed anywhere.

⚠ **But do not label it "line coverage."** Per Story 25.5's finding, Sonar's `coverage` is a *blend*:
`(covered_lines + covered_conditions) / (lines_to_cover + conditions_to_cover)`. Line coverage is
**91.9%** and branch coverage is **85.5%** — both different numbers. The badge's own label is the
lowercase word `coverage`; the README must not re-caption it as anything more specific.

### 5. ⚠ This README **is** the NuGet package README — badge domains are constrained

`src/SpecScribe/SpecScribe.csproj:23` sets `<PackageReadmeFile>README.md</PackageReadmeFile>` and
`:56` packs the repo-root `README.md` into the package. NuGet.org renders it on the package listing
and **restricts images to an allow-list**; anything else "will not be rendered and will produce a
warning ... visible to the package owners."

**Both planned badge hosts are on NuGet.org's allow-list** — verified against Microsoft Learn,
*Package readme on NuGet.org* § *Allowed domains for images and badges*:

- `sonarcloud.io` ✅
- `github.com/.../workflows/.../badge.svg` ✅ (this exact path shape)

Pipe **tables are supported** (Markdig/CommonMark), so the Project health table renders there too.

**Two hard constraints this places on the implementation:**

1. **Badge link targets MUST be absolute `https://` URLs.** Relative paths do not resolve on
   nuget.org. (The README's existing relative links are pre-existing and out of scope.)
2. **Do not swap in a different badge host** (shields.io, badgen, a self-generated SVG) without
   re-checking the allow-list. Do not use a `raw.githubusercontent.com`-hosted static SVG — it would
   be a second, separately-computed coverage number, which AC #1 forbids.

`epics.md:2864` (Epic 16 packaging) already requires *"the packaged README/license render on the
package listing"* — that verification is Epic 16's, not this story's. Note the dependency and move on.

## Acceptance Criteria

> Carried **verbatim** from `epics.md` § *Story 25.6*. No amendment.

1.
**Given** SonarCloud publishes badge endpoints for this project
**When** badges are added to `README.md`
**Then** they render **green at the moment they land** — a permanently-red badge on the front page is worse than none — and each badge links to the surface that explains it
**And** the coverage badge shows the same figure the CI analysis reports, not a separately-computed one.

2.
**Given** the quality gate is Story 25.2's decision, not this story's
**When** a quality-gate badge is added
**Then** it is added **after** 25.2 has settled what the gate asserts, so the badge cannot advertise a gate that does not yet mean anything
**And** if 25.2 has not landed, the coverage and build badges may ship alone and the story says so explicitly.

3.
**Given** badges are external image requests
**When** they are added
**Then** the story records what each badge URL discloses about the project, confirming nothing private is implied by a public badge (NFR10's disclosure discipline).

## Owner decisions locked at create-story (2026-07-28)

- **D1 — Scope.** Ship **build + coverage badges only**, exercising AC #2's escape hatch explicitly.
  **Additionally correct `docs/SonarCloudSetup.md`**: resolve the § *What would make the gate blocking*
  vs § *Current decisions* contradiction (fact 2) and restate the real precondition with the measured
  counts (fact 3). No `src/`, `web/`, or `extension/` code is touched.
  *Superseded at elicitation:* an initial "fix the 3 named files and ship all badges" direction was
  reversed once the 176-issue measurement showed the gate would stay red regardless.
- **D2 — Presentation.** A **compact badge row directly under the H1**, plus a fuller
  **Project health table in § Continuous integration** carrying every measure, its current value,
  and what it means.
- **D3 — Disclosure record (AC #3).** Lands in **`docs/SonarCloudSetup.md`** as a new § *Badges*.
  The story file cites it. Rationale: that file already owns every other Sonar fact (project key,
  gate transcription, exclusions), and Story 25.5 fixed the rule that **"the README is a front door,
  not a record."**
- **D4 — No new story seated.** A follow-up story for the gate badge was offered and **declined**.
  The trigger is recorded in prose in `docs/SonarCloudSetup.md` instead. **Do not add a story to
  `epics.md` or `sprint-status.yaml`.**

## Tasks / Subtasks

- [x] **Task 1 — Re-measure before writing any number** (AC: #1)
  - [x] Run the four commands in § *Re-measure first* and record the output in the Dev Agent Record.
  - [x] If `alert_status` has become `OK`, **STOP and ask the owner** — D1's premise has changed and the
        gate badge may now be shippable. Do not silently add it; AC #2 makes it an owner-visible decision.
        → **Still `ERROR`. Premise holds; no STOP triggered.**
  - [x] If `coverage` is no longer `89.8`, use the new figure everywhere. Never carry a stale number forward.
        → **It moved to `89.9`. Every figure in this story's output is the re-measured one.**
  - [x] Confirm the build badge still reads `passing`. If CI is red, **STOP** — AC #1 is not satisfiable today.
        → **`passing`.**

- [x] **Task 2 — Badge row under the H1** (AC: #1)
  - [x] Insert **two** badges as a single line immediately after `# SpecScribe` (`README.md:1`) and
        **before** the bold tagline on line 3. Blank line either side.
  - [x] Build badge — image
        `https://github.com/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/badge.svg?branch=main`,
        linking to
        `https://github.com/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml`.
        Keep `?branch=main` so a PR run cannot flip the front page.
  - [x] Coverage badge — image
        `https://sonarcloud.io/api/project_badges/measure?project=IntegerMan_SpecScribe&metric=coverage`,
        linking to `https://sonarcloud.io/summary/overall/component_measures?id=IntegerMan_SpecScribe&metric=coverage`.
  - [x] Alt text must carry the meaning in **words**, not rely on the image ("Build status", "Coverage").
        → *"Build status for the main branch"* / *"Coverage, measured by SonarQube Cloud"*. **No measured
        value is baked into the alt text** — see Deviation D-2.
  - [x] **Absolute `https://` link targets only** (fact 5). No relative paths.
  - [x] **Do NOT add** `alert_status`, `reliability_rating`, or `security_rating` badges — all three
        render red today. `sqale_rating` is green (A) but is not in D2's set; leave it out.

- [x] **Task 3 — Project health table + the gate-badge absence, in § Continuous integration** (AC: #1, #2)
  - [x] Anchor by the **heading text** `### Continuous integration`, **not by line number** — a concurrent
        session is holding ~47 uncommitted README lines right now (see § *Concurrent work*).
  - [x] Add a `#### Project health` table after the existing two paragraphs. Columns:
        *Measure* · *Value* · *What it means*. Rows: Build, Coverage, Line coverage, Branch coverage,
        Maintainability, Reliability, Security, Quality gate. → **All 8 rows present, verified in the DOM.**
  - [x] **Every row states its status in words.** No cell may signal state by color or by a bare emoji alone.
        → No emoji used at all; ratings are written as *"A — best rating"*, *"D — third-worst of five"*, etc.
  - [x] Carry a provenance line under the table in the shape ADR 0023 uses for the analysis digest:
        *"Measured `<date>` against SonarCloud's analysis of `<short-sha>`."* Plus the one-line
        re-measure command so the next reader can refresh it rather than trust it.
  - [x] Add one short paragraph stating **why there is no quality-gate badge**: the gate is
        `Sonar way`, every condition is advisory (`sonar.qualitygate.wait` unset), it currently reports
        failing, and a red front-page badge is worse than none. Link to `docs/SonarCloudSetup.md`.
        **This paragraph is how AC #2's "the story says so explicitly" reaches a reader.**

- [x] **Task 4 — Correct `docs/SonarCloudSetup.md` § What would make the gate blocking** (AC: #2)
  - [x] Replace the 3-item checklist's items 2 and 3 and the `> Both remaining blockers are in
        web/scripts/**` block-quote with the measured truth from fact 3: **12 open new-code bugs and
        164 open new-code vulnerabilities**, 9 bugs and 160 vulns of which are in `src/`.
        → **Measured truth is 10 bugs in `src/`, not 9, and 161 vulns, not 160. See Deviation D-1.**
  - [x] Delete the sentence *"the `csharpsquid:S6444` band no longer drives the new-code security
        rating"* — it is false and contradicts the same file's § *Current decisions* row.
        → Removed from the operative text; preserved inside the Correction note as the quoted prior wording.
  - [x] State the correct ownership: **Story 17.2** owns the `S6444`/`S4036` band; the reliability
        sweep across `src/` is unowned and should be named as such rather than implied to be Epic 23's.
  - [x] Explain **why** the claim went stale — the sliding `days: 30` window absorbed the band — and
        cross-reference § *The new-code period*, which already predicts exactly this.
  - [x] Follow the file's own precedent (§ *Quality gate* opening) of **preserving the prior wording and
        saying it was wrong**, rather than silently rewriting history.
  - [x] Do **not** touch § *Rule-level decisions* — that half was right. → Untouched.

- [x] **Task 5 — Badge disclosure record, `docs/SonarCloudSetup.md` § Badges** (AC: #3)
  - [x] New section stating, per badge: the exact URL, what it discloses, and who serves it.
        Draft content is in § *Disclosure analysis (AC #3)* below — verify each claim, do not paste blind.
        → Every claim re-verified against the live tree before writing; see Debug Log.
  - [x] State explicitly that **neither URL carries a token**, that the SonarCloud project is
        `visibility: public` so every badge endpoint answers anonymously, and that
        **`api/project_badges/token` must never be called** for this project.
  - [x] Record the NuGet.org allow-list dependency (fact 5) so a future badge swap re-checks it.
  - [x] **D4's declined-story trigger** recorded in prose in the same section (added beyond the subtask
        list because D4 requires it and no other subtask carried it).

- [x] **Task 6 — Verify rendering** (AC: #1)
  - [x] `curl -sI` each badge URL: expect `200` and `content-type: image/svg+xml`.
        → Build badge `200 image/svg+xml`. **Coverage badge returns `405` to HEAD — the endpoint is
        GET-only.** GET gives `200 image/svg+xml`. See Deviation D-3.
  - [x] Grep each fetched SVG for its rendered text (`passing`, `89.8%`) to confirm the values match
        Task 1's measurements. → `passing` and `89.9%`, both matching Task 1.
  - [x] Render the README in a live browser (CLAUDE.md § Verification) and confirm the badge row sits
        under the H1, both images resolve, both links land on the right surface, and the health table
        does not overflow. → **Both images genuinely loaded** (`naturalWidth` 194×20 and 131×20 — not
        merely present in the DOM). Table `scrollWidth == clientWidth`, no page overflow. Both link
        targets return `200`.
  - [x] Confirm `git diff --stat` shows **only** `README.md`, `docs/SonarCloudSetup.md`, and
        `sprint-status.yaml` **from this story**. Sibling sessions will have other files staged.
        → Confirmed; `src/SpecScribe/SiteGenerator.cs` and an untracked test file are a sibling
        session's and were left untouched.

- [x] **Task 7 — Confirm the fingerprint is unmoved** (AC: n/a — scope guard)
  - [x] Prove it **by scope**, not by quoting a hash: this story changes no file under `src/`, so
        `GoldenContentFingerprint` cannot move. → **Proven by scope. No hash quoted, no generation run.**

## Dev Notes

### Absolute scope boundaries

| Allowed | Forbidden |
|---|---|
| `README.md` | anything under `src/`, `tests/`, `web/`, `extension/` |
| `docs/SonarCloudSetup.md` | `.github/workflows/**` — the analysis config is 25.1/25.2's |
| `sprint-status.yaml` | `epics.md` — no AC amendment, no new story (D4) |
| | SonarCloud **server-side** settings — the gate, the new-code period, exclusions |

This story ships **documentation only**. No build, no test, no generated output changes.

### Re-measure first

The project is public; every endpoint answers anonymously with **no token**.

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=IntegerMan_SpecScribe&metricKeys=alert_status,coverage,line_coverage,branch_coverage,ncloc,sqale_rating,reliability_rating,security_rating,quality_gate_details"
```

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe&inNewCodePeriod=true&types=BUG,VULNERABILITY&resolved=false&ps=1"
```

```bash
curl -s "https://sonarcloud.io/api/project_badges/measure?project=IntegerMan_SpecScribe&metric=coverage" | grep -o '>[0-9.]*%<'
```

```bash
curl -s "https://github.com/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/badge.svg?branch=main" | grep -o '<title>[^<]*</title>'
```

⚠ **Always pass `resolved=false`.** `docs/SonarCloudSetup.md` § *Triaging findings* § *Step 1* makes this
a standing rule, and it matters acutely here: the unfiltered new-code query returns **210** issues, of
which **34 are already FIXED**. Quoting 210 would overstate the blocker by a third.

### Ratings → issues, the mapping that makes fact 3 true

Sonar derives `reliability_rating` from the **worst-severity open bug** and `security_rating` from the
**worst-severity open vulnerability**: `A` = none · `B` = MINOR · `C` = MAJOR · `D` = CRITICAL ·
`E` = BLOCKER. The gate demands `A` on both **for new code**. A single surviving MINOR issue holds the
rating at `B` and the gate at `ERROR`. This is why "fix the CRITICAL" is not a plan.

### Measured baseline (2026-07-28, analysis of `8a2fb83` at 23:55:45Z)

| Measure | Value |
|---|---|
| `alert_status` | `ERROR` (badge renders `failed`) |
| `coverage` | 89.8% |
| `line_coverage` | 91.9% |
| `branch_coverage` | 85.5% |
| `new_coverage` | 90.5% (passes the 80% condition) |
| `ncloc` | 43,132 |
| `sqale_rating` (maintainability) | 1.0 → **A** |
| `reliability_rating` | 4.0 → **D** |
| `security_rating` | 3.0 → **C** |
| Open new-code bugs | 12 |
| Open new-code vulnerabilities | 164 |
| New-code period baseline | `2026-07-25T20:54:41Z` (`days: 30`, sliding) |
| Project visibility | `public` |

These moved measurably between 2026-07-27 and 2026-07-28. **Assume they have moved again.**

### Disclosure analysis (AC #3) — verify, then write into § Badges

| | Build badge | Coverage badge |
|---|---|---|
| **URL** | `github.com/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/badge.svg?branch=main` | `sonarcloud.io/api/project_badges/measure?project=IntegerMan_SpecScribe&metric=coverage` |
| **Served by** | GitHub | SonarSource |
| **Discloses** | repo owner/name, the workflow **filename**, the branch name `main`, and pass/fail of the latest run | the SonarCloud **project key** and the current `coverage` value |
| **Already public?** | Yes — the repo is public and the workflow file is in it | Yes — the key `IntegerMan_SpecScribe` and org `integerman-github` are literals in `build-test-analyze.yml:186-187`; the project is `visibility: public` |
| **Token?** | None | **None.** Sonar's `&token=` form is for private projects only |

**The genuinely new disclosure is not about the project — it is about the reader.** On github.com,
README images are proxied through `camo.githubusercontent.com`, so a visitor's IP reaches GitHub
rather than SonarSource. **Rendered anywhere else** — nuget.org, a docs mirror, an RSS reader — the
image request goes **direct to `sonarcloud.io`**, disclosing the viewer's IP and User-Agent to
SonarSource. That is the honest statement AC #3 is asking for. It is a normal, accepted cost of
badges; the point is to have written it down rather than to have not noticed.

Nothing in either URL reveals a private repository, a credential, a contributor identity, a file
path, or a finding. The measured values (89.8%, `passing`) are already published on a public
SonarCloud dashboard and a public Actions tab.

### Concurrent work — this WILL bite

`README.md` currently holds **~47 uncommitted lines** from another session (an Epic 18 *BMad modules*
section inserted after § *Supported frameworks*, plus two bullets in § *What it renders*).
`src/SpecScribe/AboutSddTemplater.cs` is also dirty.

Consequences:

- **Every README line number in this story is approximate.** Anchor edits by heading text.
- **Story 25.5 is `in-progress`** (its file says so even though `sprint-status.yaml` still reads
  `ready-for-dev`) and it edits **§ Development** — the fenced command block. This story edits
  **§ Continuous integration**, the section immediately after. Adjacent, not overlapping, but
  **re-read the section immediately before editing it**; do not trust a snapshot from ten minutes ago.
- **Never `git checkout --`, `git reset --hard`, or `git clean`** to tidy up. CLAUDE.md is explicit,
  and it has already destroyed real work on this repo.
- After editing, **grep the README for the badge line** to confirm it actually landed. A silently
  vanished write is a documented failure mode here.

### Anti-patterns this story is specifically at risk of

1. **Adding the gate badge because it "will be green soon."** It will not. See fact 3.
2. **Computing a second coverage number.** Any locally-generated coverage SVG, or a shields.io
   endpoint that recomputes, violates AC #1's *"not a separately-computed one"*. The badge must be
   SonarCloud's own render of its own measure. Story 25.5's local report is a **different artifact
   for a different purpose** — it must not feed the badge.
3. **Fixing `check-links.mjs:204` "while we're here."** It is a one-line `localeCompare`, it is
   tempting, and it changes nothing about the badge. It is `web/`, which this story may not touch.
4. **Captioning the coverage badge as line coverage.** It is a line+branch blend (fact 4).
5. **Writing measured numbers into the README without a date stamp.** A bare "89.8%" in prose is a
   lie the moment CI runs again. Every hand-written figure needs the provenance line from Task 3.
6. **Silently rewriting the setup doc's wrong claim.** That file's own § *Quality gate* opening sets
   the precedent: preserve the prior wording, say it was wrong, say why.
7. **Treating a `200` from a badge URL as proof it renders correctly.** Grep the SVG text.

### Testing standards

No automated tests. This story ships no code — `dotnet test` is unaffected and need not run.
Verification is the live-browser render plus the `curl` checks in Task 6. Per CLAUDE.md
§ *Verification*, **visual work is verified in a live browser**, and a remote-image badge row is
precisely the kind of thing a markdown preview renders convincingly and wrongly.

### Project Structure Notes

- **No ADR.** Considered per CLAUDE.md § *Decision records* and declined: adding two badges changes no
  shared architecture and amends no prior ADR. The **correction** in Task 4 restates an existing
  documented fact rather than deciding a new one. Recorded here so a reviewer sees it was weighed.
- **No `epics.md` change.** All three ACs are carried verbatim and AC #2's escape hatch is exercised
  as written. There is no structural scope change, so CLAUDE.md's
  "`epics.md` and `sprint-status.yaml` in the same change" rule is not triggered.
- `docs/SonarCloudSetup.md` is the established home for Sonar facts; this story extends it rather than
  starting a parallel record.

### References

- `_bmad-output/planning-artifacts/epics.md` § *Story 25.6* — the three ACs, verbatim (~L4802-4825).
- `_bmad-output/planning-artifacts/epics.md` § *Story 25.5* / Epic 16 packaging (~L2864) — the
  "packaged README renders on the package listing" requirement this story's badge hosts must survive.
- `docs/SonarCloudSetup.md` § *Quality gate* → *Which gate* / *The conditions, transcribed* /
  *The new-code period* / *What would make the gate blocking* — the gate 25.2 settled, and the
  stale claim Task 4 corrects.
- `docs/SonarCloudSetup.md` § *Triaging findings* → *Step 1* — the standing `resolved=false` rule.
- `docs/SonarCloudSetup.md` § *Rule-level decisions* → *Current decisions* — the `S6444` row that
  assigns the band to Story 17.2 and is the half of the file that is right.
- `25-1-sonarcloud-onboarding-and-ci-analysis.md` Task 7 (~L218-222, L781-782) — badge deliberately
  **not** added; the reason then was "should follow 25.2's gate decision rather than precede it".
  25.2 has since landed, and this story records that the reason has changed again.
- `25-5-local-coverage-report.md` § *Read first* facts 1-2 — why `coverage` ≠ line coverage, and why
  the local report is not the badge's source. Also its "README is a front door, not a record" rule.
- `.github/workflows/build-test-analyze.yml:186-187` — `/o:"integerman-github"`,
  `/k:"IntegerMan_SpecScribe"`; `:176` — `sonar.qualitygate.wait` deliberately unset.
- `src/SpecScribe/SpecScribe.csproj:23,56` — `PackageReadmeFile` / the `Pack="true"` include that
  makes this README the NuGet listing.
- Microsoft Learn, *Package readme on NuGet.org* § *Allowed domains for images and badges* —
  `sonarcloud.io` and `github.com/.../workflows/.../badge.svg` are both allow-listed; unlisted
  domains and relative paths do not render.
- `README.md:1-3` (badge row insertion point), `### Continuous integration` (health table anchor).
- CLAUDE.md § *Concurrent work on shared `main`*, § *Verification*, § *Decision records*.

## Dev Agent Record

### Agent Model Used

`claude-opus-5` (Claude Opus 5) via `/bmad-dev-story 25.6`, 2026-07-29.

### Debug Log References

**Execution baseline.** Story `baseline_commit` is `8a2fb83` (preserved, not overwritten). Actual `HEAD`
at execution was **`94b8e56`** — the concurrent session's ~47 uncommitted README lines the story warned
about had since been committed, so the tree was clean at start apart from a sibling's `src/` work.

#### Task 1 — re-measurement (2026-07-29, SonarCloud analysis `240afae` @ 2026-07-30T01:49:15Z)

The story's create-time snapshot was `8a2fb83` @ 2026-07-28T23:55:45Z. **Several figures moved.**

| Measure | Story (2026-07-28) | Measured (2026-07-29) | Moved? |
|---|---|---|---|
| `alert_status` | `ERROR` | **`ERROR`** | no — D1's premise holds |
| `coverage` | 89.8% | **89.9%** | ✅ moved |
| `line_coverage` | 91.9% | 91.9% | no |
| `branch_coverage` | 85.5% | **85.7%** | ✅ moved |
| `new_coverage` | 90.5% | **90.19%** | ✅ moved |
| `ncloc` | 43,132 | **44,268** | ✅ moved |
| `sqale_rating` | 1.0 (A) | 1.0 (A) | no |
| `reliability_rating` | 4.0 (D) | 4.0 (D) | no |
| `security_rating` | 3.0 (C) | 3.0 (C) | no |
| `new_maintainability_rating` | A | A | no |
| `new_duplicated_lines_density` | 0.8% | **0.62%** | ✅ moved |
| `new_security_hotspots_reviewed` | 100% | 100% | no |
| Open new-code bugs | 12 | 12 | no |
| Open new-code vulnerabilities | 164 | 164 | no |
| Project visibility | `public` | `public` | no |

Badge SVGs, fetched anonymously: build renders `Build, Test &amp; Analyze - passing`; coverage renders the
text nodes `coverage` and `89.9%`.

#### Deviation D-1 — the story's own bug count was wrong, and I used the measured one

Fact 3 and Task 4 both say **"9 of the 12 bugs are in `src/`"**, but fact 3's own file list enumerates
**ten** `src/` paths. Counting the issues array precisely (`ps=100`, `resolved=false`, deduplicating against
the response's trailing `components[]` array, which double-counts if grepped naively):

| Location | Open new-code bugs |
|---|---|
| `src/SpecScribe/SiteGenerator.cs` | 4 |
| `src/SpecScribe/assets/specscribe.css` | 3 |
| `src/SpecScribe/CapabilityStyler.cs` | 1 |
| `src/SpecScribe/WorkGraph.cs` | 1 |
| `src/SpecScribe/HtmlRenderAdapter.Dashboard.cs` | 1 |
| **`src/` subtotal** | **10** |
| `extension/src/extension.ts` | 1 |
| `web/scripts/check-links.mjs` | 1 |
| **Total** | **12** |

Severities: 1 CRITICAL, 9 MAJOR, 2 MINOR.

Vulnerabilities likewise: the story says "160 of the 164 … in `src/`", but 160 is the `S6444` count, not the
`src/` count. `src/` also holds one `csharpsquid:S4036`, so **161 of 164 are in `src/`** and only **3** are in
`web/scripts/` (`javascript:S4036`, `jssecurity:S8707`, `jssecurity:S8705`). Severities: 2 MAJOR, 162 MINOR.

**The corrected numbers are what went into `docs/SonarCloudSetup.md`.** Writing 9 and 160 would have made a
correction section itself inaccurate — the exact failure this story exists to fix. This does not change any
conclusion: the gate is still red, the band is still Story 17.2's, and the sweep is still unowned.

#### Deviation D-2 — no measured value in badge alt text

First draft used alt text `"Build status: passing"`. That bakes a live value into a static string and becomes
a lie the moment CI goes red — the same defect as anti-pattern 5, but *unfixable by a date stamp* because alt
text cannot carry provenance. Replaced with `"Build status for the main branch"`, matching the story's own
value-free examples (*"Build status"*, *"Coverage"*).

#### Deviation D-3 — `curl -sI` on the coverage badge returns 405, and that is correct

Task 6 asks for `curl -sI` (HEAD) expecting `200`. SonarCloud's badge endpoint is **GET-only** and answers
HEAD with `405 application/json`. This is a method rejection, not a broken badge: the same URL under GET
returns `200 image/svg+xml` and renders `89.9%`. Recorded so a future reader does not read the 405 as a
regression. The build badge answers HEAD normally with `200 image/svg+xml`.

#### Task 6 — live-browser verification

`README.md` and `docs/SonarCloudSetup.md` were rendered to HTML with `marked` (GFM) — installed into the
**session scratchpad**, `--no-save`, never into the repo — and written to the gitignored
`artifacts/readme-check/`, then loaded in the browser pane over `file://`. Rendering the real markdown rather
than hand-writing equivalent HTML was deliberate: a hand-built stand-in cannot catch a markdown syntax error.

Evidence gathered from the live DOM, not from a preview:

- **Both badge images actually loaded.** `naturalWidth × naturalHeight` = **194×20** (build) and **131×20**
  (coverage). A non-zero `naturalWidth` is the proof; presence of the `<img>` tag alone is not.
- Badge row sits **between the H1 and the tagline paragraph**; `&metric=` query params survived markdown
  parsing intact; both `<a href>` targets are the intended absolute URLs.
- Health table: 8 rows in the required order; `scrollWidth == clientWidth == 900`; no document-level
  horizontal overflow.
- Both badge **link targets** return `200` (checked with `curl`, not by clicking away from the page).
- `docs/SonarCloudSetup.md`: the Correction note renders as a blockquote carrying its `h3` heading, with the
  preserved prior wording rendering as a **nested** blockquote inside it, plus 2 ownership list items. The new
  `## Badges` section renders with its comparison table.

**Limitation, stated rather than glossed:** the narrow-viewport check was **inconclusive**. The browser pane
serves out-of-project files as a static snapshot and did not honour the `mobile` resize — the viewport stayed
at 980px. The table's own geometry showed no overflow, and GitHub/NuGet supply their own table styling, but
this story did not positively verify a 375px render.

#### Concurrent work encountered

`src/SpecScribe/SiteGenerator.cs` (modified) and `tests/SpecScribe.Tests/RegionCompositionParityTests.cs`
(untracked) were dirty in the tree throughout and belong to a **sibling session**. Left entirely untouched;
no `git checkout --`, `reset`, or `clean` was run at any point. Both README edits were grep-verified after
writing, per CLAUDE.md § *Concurrent work*.

### Completion Notes List

**All three ACs satisfied. Documentation only — no product code, no test changes, no generated output.**

- **AC #1 — badges render green at the moment they land, link to explaining surfaces, and the coverage
  figure is CI's own.** Build (`passing`) and coverage (`89.9%`) both verified green *and verified as
  actually loading* in a live browser. Coverage is SonarCloud's own render of its own `coverage` measure, so
  "not a separately-computed one" holds **by construction** — nothing is recomputed anywhere, and Story
  25.5's local report was deliberately not used. Per fact 4, the README never captions it "line coverage";
  the health table distinguishes the blend (89.9%) from line (91.9%) and branch (85.7%) explicitly.
- **AC #2 — the gate badge is withheld, and the story says so explicitly.** `alert_status` is still `ERROR`,
  so AC #2's escape hatch was exercised **as written, not waived**. The "Why there is no quality-gate badge"
  paragraph in the README's § *Continuous integration* is how that reaches a reader. Task 4's correction to
  `docs/SonarCloudSetup.md` resolves the file's self-contradiction, preserving the wrong wording verbatim
  inside a Correction note per that file's own precedent, and naming the sliding `days: 30` window as the
  reason it went stale.
- **AC #3 — disclosure recorded.** New `## Badges` section in `docs/SonarCloudSetup.md`: per-badge URL,
  server, and what it discloses; explicit no-token statement plus the standing prohibition on
  `api/project_badges/token`; the NuGet allow-list constraint on any future badge swap; and the honest
  point that the genuinely new disclosure is **about the reader** — `camo.githubusercontent.com` proxies on
  github.com, but a nuget.org or mirror render sends the viewer's IP and User-Agent straight to SonarSource.

**Owner decisions honoured.** D1 (build + coverage only, plus the setup-doc correction) · D2 (badge row under
the H1 + Project health table) · D3 (disclosure record lands in `docs/SonarCloudSetup.md`, not the README) ·
D4 (**no new story seated**; the gate-badge trigger is recorded in prose in the § *Badges* section, paired
with an instruction to delete the README's "why there is no badge" paragraph when the gate turns green).

**Scope guards held.** No file under `src/`, `tests/`, `web/`, `extension/`, `.github/workflows/`, or
`epics.md` was modified. `GoldenContentFingerprint` is proven unmoved **by scope**, with no hash quoted and no
generation run — per CLAUDE.md, quoting a hash here would be meaningless while a sibling session edits `src/`.
No ADR was raised; the create-story rationale for declining one was re-checked and stands.

**Three anti-patterns actively avoided:** the gate badge was not added on "it'll be green soon"; no second
coverage number was computed; and `web/scripts/check-links.mjs:204` was left alone despite being a
one-line fix.

**Two things a reviewer should look at first:** (1) Deviation **D-1** — I wrote measured counts that differ
from the story's own prose (10 not 9, 161 not 160); if the story's numbers were authoritative for some reason
I did not find, the setup-doc correction needs revisiting. (2) The narrow-viewport render was **not**
positively verified (see Task 6 limitation).

**No tests were run and none were added.** This story ships no code; `dotnet test` is unaffected by a README
and a docs file, and the suite is documented as flaky while a preview server runs. Per § *Testing standards*,
verification here is the `curl` checks plus the live-browser render, both completed above.

### File List

| File | Change |
|---|---|
| `README.md` | Badge row (build + coverage) inserted between the H1 and the tagline; `#### Project health` table, dated provenance line, refresh command, and the "why there is no quality-gate badge" paragraph added to § *Continuous integration*. |
| `docs/SonarCloudSetup.md` | § *What would make the gate blocking* — checklist items 2 and 3 rewritten with measured counts; the stale block-quote replaced by a Correction note that preserves it verbatim and explains why it went stale; ownership corrected to Story 17.2 + an unowned sweep. **New** `## Badges` section (AC #3 disclosure record + D4's gate-badge trigger). § *Rule-level decisions* untouched. |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | `25-6-…: ready-for-dev` → `review`; `last_updated` header entry prepended. |
| `_bmad-output/implementation-artifacts/25-6-readme-coverage-and-quality-badges.md` | This story file — tasks checked, Dev Agent Record, File List, Change Log, Status. |

Not modified, and deliberately so: `src/**`, `tests/**`, `web/**`, `extension/**`, `.github/workflows/**`,
`_bmad-output/planning-artifacts/epics.md`.

## Change Log

| Date | Change |
|---|---|
| 2026-07-29 | Story 25.6 implemented. Build and coverage badges added to `README.md` with a Project health table; quality-gate badge deliberately withheld (gate still `ERROR`), exercising AC #2's escape hatch as written. `docs/SonarCloudSetup.md` § *What would make the gate blocking* corrected and a new § *Badges* disclosure record added. Re-measurement moved `coverage` 89.8 → 89.9 and corrected the story's own `src/` bug count from 9 to 10. Documentation only; golden fingerprint unmoved by scope. Status → review. |
