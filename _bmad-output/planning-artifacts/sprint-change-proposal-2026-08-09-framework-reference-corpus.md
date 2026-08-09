# Sprint Change Proposal — Framework Reference Corpus

**Date:** 2026-08-09
**Author:** correct-course workflow (owner-directed)
**Trigger story:** 12.2 — GSD Core Baseline Adapter Coverage
**Mode:** Batch
**Scope classification:** **Moderate** — backlog reorganization + AC amendments across six epics; no PRD goal changes, no architecture rework.

---

## Section 1 — Issue Summary

### Problem statement

**Framework support is scoped from documentation, and the variance only surfaces during implementation.**
Every framework epic (11–15) is spike-led precisely so that coverage work starts from a known map. But the
spike ACs say only *"representative … repositories"* — they never require the repositories to be **named,
obtainable, or plural**. In practice the spikes were run against vendor docs and the tool's own source repo,
and the map was wrong in ways that only a real adopting project could reveal.

### How it was discovered

Story 12.2 (GSD Core) is the first story in the project to make a non-BMad repository generate at all. Its
create-story stage obtained one real GSD Core repository (`C:/dev/CORA`) and checked Story 12.1's coverage map
against it:

| Evidence | Count |
|---|---|
| Story 12.1 claims re-checked against a real repo | 8 |
| Claims that **failed** | **6** |
| Further load-bearing findings with no 12.1 counterpart | 2 |
| Decisions the dev agent had to take **against its own story text** (§D1–§D5) | 5 |
| Post-hoc findings a reviewer would otherwise rediscover (§F1–§F7) | 7 |

Story 12.1 flagged this itself, in its own Debug Log:

> *"Every layout claim below therefore rests on current vendor documentation, not on a directory listing…
> Story 12.2 must re-confirm exact filenames against a generated repo before writing discovery globs."*

The spike knew its evidence was thin and said so. Nothing in the process required it to be otherwise.

### What the missing evidence actually cost

Each of these is a documented rework loop in 12.2 that a real-repo corpus would have caught at spike time:

1. **`EpicInfo.Number` is `int`; real phase numbers are decimal** (`02.1`, `04.5`, `999.1`). Forced owner
   decision D2 (synthetic ordinals) mid-story. 2 of 8 shipped phases and the entire backlog were
   unrepresentable.
2. **12.1 ruled task mapping "not a compromise at all for Core."** It is false: across all 58 `PLAN.md` files
   there are **0 checked boxes**, so `TaskListParser` returns 0/0 for every plan.
3. **No `Status:` line exists anywhere** in those 58 files, so every finished plan would have rendered as a
   drafted story with no task plan — the exact defect class `BmadArtifactAdapter`'s own doc comment warns about.
4. **Requirement ids are open-ended project prefixes** (`CONV-01`, `RAG-03` — twelve distinct prefixes in one
   repo), not `REQ-001`. Forced owner decision D3.
5. **`phase:` frontmatter takes eight different encodings in one repo**, making the filename the only stable key.
6. **Three completion signals disagree** (ROADMAP 58/58, `STATE.md` 42/50, 42 SUMMARY files on disk).

### The finding that generalizes beyond 12.2

**§F1 is the one that makes this urgent rather than merely tidy.** `extract:ir-content` prunes any CSS rule
whose selector is absent from the IR, and the extraction corpus is *this repository's own IR — and this
repository is a BMad project*. Milestone bands only ever render for a framework that has a milestone level, so
no harvest run here can see them. Measured, not theorised: with the documented regeneration order followed
exactly, **all five `.milestone-band*` rules were pruned and `check:ir-content` stayed GREEN.** The bands would
have shipped unstyled on a real GSD site with no gate able to see it.

12.2 fixed it for itself by seeding `CONDITIONAL_CLASSES`, and wrote in the code comment that **every remaining
framework epic (11, 12.3, 13, 14, 15) will hit this.** That is a standing, cross-cutting hazard that only
exists because we render for frameworks whose repos we do not hold. It is a corpus problem.

### Why the moment is right

Everything downstream is still unstarted. This lands **ahead of** every remaining framework:

| Story | Status |
|---|---|
| 11.1 Spec Kit spike | `ready-for-dev` (drafted 2026-07-20, never run) |
| 13.1 SpecFlow spike | `ready-for-dev` |
| 14.1 Squad spike | `ready-for-dev` |
| 15.1 Superpowers spike | `ready-for-dev` |
| 11.2 / 12.3 / 13.2 / 14.2 / 15.2 coverage | all `backlog`, never created |

Five spikes and five coverage stories — ten stories — would each repeat 12.2's discovery loop. The cost of
fixing the standard now is four story-file amendments; the cost of not fixing it is ten reruns of a loop we
have already measured.

### Feasibility probe run for this proposal (2026-08-09)

The obvious objection to "require 3 public repos" is that they may not exist. Probed live via GitHub code
search, counting **files** matching each framework's marker path:

| Framework | Probe | Hits | Verdict |
|---|---|---|---|
| Spec Kit | `path:.specify/memory filename:constitution.md` | **6,248** | abundant |
| GSD Core | `path:.planning filename:ROADMAP.md "## Phases"` | **1,932** | abundant |
| GSD Core | `path:.planning/phases filename:PLAN.md` | **2,547** | abundant |
| Squad | `path:.squad/agents filename:charter.md` | **5,144** | abundant |
| Squad | `path:.squad filename:routing.md` | **700** | abundant |
| GSD Pi | `path:.gsd filename:STATE.md` | **281** | sufficient |
| SpecFlow | `filename:.specflow-version` | **0** | **marker unconfirmed** |
| SpecFlow | `filename:.specflow-config.json` | **0** | **marker unconfirmed** |
| Superpowers | `path:docs/superpowers/plans` | noisy/unusable | **marker unconfirmed** |

**Two consequences, both of which shape the proposed ACs:**

1. For **Spec Kit, GSD Core, GSD Pi and Squad** a three-repo corpus is trivially achievable. A hard requirement
   is fair.
2. For **SpecFlow and Superpowers** the marker itself is a hypothesis from thin docs. Story 15.1 already records
   that Superpowers *is never installed into the target repo at all* — it is an agent plugin, and its only
   on-disk trace is a user-overridable plan-path convention. **You cannot search for adopters until you know
   what to search for.** So corpus discovery is inherently **two-pass** (confirm marker → search by marker), and
   the AC needs a documented-shortfall path rather than an unconditional "3 or it fails".

**A third distinction the ACs must carry explicitly:** the framework's **own source repository is not a
reference repo.** `github/spec-kit`, `bradygaster/squad` and `obra/superpowers` are the *tools*; a reference
repo is a project that *uses* the tool. Story 15.1 already flagged that its fetched material "documents the
tool's own repository, not a downstream project's use of it." Both 14.1 and 15.1 currently point their dev
agent at the tool repo as the thing to inspect.

---

## Section 2 — Impact Analysis

### Epic impact

| Epic | Impact | Can it complete as planned? |
|---|---|---|
| **Epic 4** — Framework-Agnostic Adapter Foundation | **New Story 4.10** (Framework Reference Corpus Contract). Same append-only, post-retrospective amendment pattern as Stories 4.9, 7.9 and 8.9. Epic 4 is already `in-progress`; its retrospective stays `done`. | Yes, reopened-scope |
| **Epic 11** — Spec Kit | 11.1 gains AC #3 + tasks; 11.2 gains AC #3. Story file 11.1 amended in place (`ready-for-dev`, never run). | Yes |
| **Epic 12** — GSD / GSD-Pi | **12.2 reopened** `review` → `in-progress` with new AC #6 (owner decision). 12.3 gains AC #5. | Yes |
| **Epic 13** — SpecFlow | 13.1 gains AC #3 + tasks + the marker-first two-pass rule; 13.2 gains AC #3. | Yes |
| **Epic 14** — Squad | 14.1 gains AC #3 + tasks + the tool-repo-is-not-a-reference-repo correction; 14.2 gains AC #3. | Yes |
| **Epic 15** — Superpowers | 15.1 gains AC #3 + tasks + both corrections; 15.2 gains AC #3. | Yes |

No epic is invalidated, removed, or resequenced. **One sequencing constraint is added:** Story 4.10 should
complete before the next framework spike is run, because it defines the contract the amended ACs cite. If the
owner wants a framework spike to run first, 4.10's contract is small enough to be authored inside that spike
and lifted out afterward — but the default order is 4.10 first.

### Story impact

**Amended in place (drafted, `ready-for-dev`, never run — safe to edit):**
`11-1-spec-kit-integration-spike.md`, `13-1-specflow-integration-spike.md`,
`14-1-squad-integration-spike.md`, `15-1-superpowers-integration-spike.md`

**Reopened:** `12-2-gsd-core-baseline-adapter-coverage.md` (`review` → `in-progress`, new AC #6)

**New:** Story 4.10 (`backlog`, create-story when scheduled)

**AC text amended in `epics.md` only** (story files do not exist yet, so the AC is inherited at create-story):
11.2, 12.3, 13.2, 14.2, 15.2

### Artifact conflicts

| Artifact | Change |
|---|---|
| `_bmad-output/planning-artifacts/epics.md` | Story 4.10 added to Epic 4; AC additions to 11.1, 11.2, 12.2, 12.3, 13.1, 13.2, 14.1, 14.2, 15.1, 15.2; amendment comments per the decision-records rule |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | `4-10-…` key added (`backlog`); `12-2-…` → `in-progress`; `last_updated` note. **Co-landed in the same change** per CLAUDE.md § Decision records |
| Four spike story files | New AC + new Tasks + Dev Notes corrections |
| `CLAUDE.md` | New § "Framework support: evidence before implementation" |
| `docs/adrs/0044-*.md` | New ADR (authored by Story 4.10, not by this proposal) |
| `docs/framework-reference-corpus.md` | New manifest (authored by Story 4.10) |
| **PRD** | **No change.** FR3/FR4/FR17 are unaffected — this raises the evidence standard for satisfying them, not the requirements themselves |
| **ARCHITECTURE-SPINE.md** | **No change.** No AD is amended; the adapter contract, projection path and watch guarantees are untouched |
| **UX designs** | **No change.** No user-facing surface changes |

### Technical impact

- **No production code changes** in this proposal. Story 4.10 is documentation + one manifest + one ADR; the
  optional `CONDITIONAL_CLASSES` seeding convention is a comment-and-test change in `web/`.
- **No CI impact.** Corpus repos are dev-time only and never a test dependency — the same rule 12.2 applied to
  CORA (*"CI has no such path"*). No clone in CI, no new network dependency, no new drift gate (ADR 0033
  prefers none).
- **No new dependencies.** Corpus discovery uses the already-authenticated `gh` CLI.
- **Repo size unaffected.** Corpus repos are cloned outside the tree and pinned by SHA in a manifest, not
  vendored.

---

## Section 3 — Recommended Approach

### Selected path: **Direct Adjustment (Option 1), with one reopen**

Modify stories and ACs within the existing epic structure. Add one story to Epic 4. Reopen Story 12.2.

**Rejected — Option 2, Rollback.** Nothing in 12.2 is wrong. It shipped a working GSD Core adapter, both shared
prerequisites, ADR 0038, 34 new tests and live-browser verification. Its evidence base is *narrow*, not
*incorrect*. Rolling back would discard correct work to fix a process gap.

**Rejected — Option 3, MVP review.** The MVP is unaffected. FR3/FR4/FR17 stand exactly as written; this changes
what counts as adequate evidence for satisfying them.

### Rationale

- **It is cheap and it lands early.** Four story-file amendments and ten AC additions, against ten downstream
  stories that would each otherwise repeat a measured rework loop.
- **It matches the project's own established pattern.** 12.2 solved its shared prerequisites once, in one
  story, with one ADR that Epics 11/12.3/13/14/15 inherit (decision D4). 4.10 does the same thing for the
  evidence standard. Restating the rule in nine AC blocks without a shared contract is exactly the drift
  CLAUDE.md § Decision records warns about.
- **It is provably feasible.** The probe above shows four of six frameworks have abundant public adopters. The
  two that do not are the two whose *markers are still hypotheses* — which is itself the finding, and the AC
  handles it explicitly rather than pretending otherwise.
- **It closes a hazard that has already fired once.** §F1 is not a prediction; it was measured with a green
  gate. Every remaining framework epic will hit it.

### Effort, risk, timeline

| | |
|---|---|
| **Effort** | **Low–Medium.** Story 4.10 ≈ one spike-sized story (manifest + ADR + CLAUDE.md + a repeatable discovery recipe). AC/story amendments ≈ one sitting. Story 12.2's new AC #6 is **Medium** — it re-runs generation against 2 new repos and fixes whatever variance surfaces, which is genuinely unknown until run. |
| **Risk** | **Low** for the standard itself. **Medium** for 12.2's reopen: a second GSD Core repo could surface a shape the adapter mishandles, which is the entire point but does mean 12.2's close date is not predictable. That is the trade the owner has accepted. |
| **Timeline** | Adds one story to Epic 4 and re-opens one story in Epic 12. Saves an unquantified but repeatedly-measured amount from ten downstream stories. |

---

## Section 4 — Detailed Change Proposals

### 4A — New Story 4.10 (`epics.md`, Epic 4)

**Insert after Story 4.9, with the amendment comment above it.**

```markdown
<!-- Story 4.10 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed) — an append-only
     post-retrospective amendment to Epic 4, not a renumber: 4.1/4.2/4.8/4.9 are untouched (same pattern as
     Stories 4.9, 7.9 and 8.9). Provoked by Story 12.2: Story 12.1 built its GSD coverage map from vendor
     documentation, said so in its own Debug Log, and SIX of its eight derived claims failed on contact with
     one real repository — costing 12.2 five mid-story owner decisions against its own task text. The
     generalizing finding is 12.2 §F1: `extract:ir-content` prunes any rule whose selector is absent from the
     IR, and the extraction corpus is THIS repository's own IR, which is a BMad project — so markup only a
     non-BMad repo produces is pruned WITH THE GATE GREEN (measured: all five `.milestone-band*` rules).
     Every remaining framework epic hits this. Belongs in Epic 4 because the evidence standard is a property
     of the shared adapter contract, not of any one framework. -->
### Story 4.10: Framework Reference Corpus Contract

As a maintainer adding support for a new spec-driven framework,
I want a defined, obtainable set of real adopting repositories to research and verify against before
implementation begins,
So that a framework's coverage map is built from how the framework is actually used rather than from its
documentation, and rendered values are checked against known-correct projects.

**Acceptance Criteria:**

1.
**Given** framework support has repeatedly been scoped from vendor documentation and corrected during
implementation
**When** the reference-corpus contract is written
**Then** it defines what qualifies as a reference repository — a project that USES the framework, explicitly
not the framework's own source repository — and sets the target at three per framework, chosen for VARIANCE
rather than for similarity
**And** it states the recorded-shortfall rule: when fewer than three qualifying public repositories can be
found, the search evidence, the query used, and the substitute (a self-scaffolded `init` repository) are
recorded, and the reduced confidence is declared on that framework's page rather than left silent.

2.
**Given** a corpus repository is a moving target and CI has no access to it
**When** a repository is admitted to the corpus
**Then** a committed manifest records its URL, the exact commit SHA inspected, its licence, its approximate
size, and the specific variance it was chosen to contribute
**And** the contract states that corpus repositories are dev-time references only, never a test dependency,
with every shape they reveal carried into temp-directory fixtures instead.

3.
**Given** a framework's marker directory or file is itself a hypothesis until confirmed
**When** corpus discovery runs
**Then** the contract prescribes the two-pass order — confirm the marker against the framework's own
documentation and a scaffolded `init`, then search public repositories BY that confirmed marker — and records
a repeatable discovery recipe
**And** the recipe is proven by running it for at least one framework and recording the resulting counts.

4.
**Given** `extract:ir-content` prunes any rule whose selector is absent from this repository's own BMad IR, so
markup that only a non-BMad repository produces is silently dropped with every gate green
**When** the contract is written
**Then** it names `CONDITIONAL_CLASSES` seeding as the required step for any cross-framework markup, and states
that `web/test/ir-content-harvest.test.mjs` — not the round-trip gate — is the layer that pins it
**And** the hazard is stated once, in a place the remaining framework epics inherit, rather than rediscovered
per epic.

5.
**Given** this changes the evidence basis on which the shared adapter contract is extended
**When** the contract concludes
**Then** it lands as one ADR that Epics 11–15 inherit, related to ADR 0038 (adapter selection) and ADR 0041
(multi-framework coexistence) without superseding either
**And** the working convention is additionally recorded in `CLAUDE.md` so an agent that reads no ADR still
meets it.
```

**Rationale:** one shared contract, authored once, inherited by ten downstream stories — the same shape as
12.2's decision D4 (one registry, one ADR, five epics inherit). AC #3 exists because the feasibility probe
found two frameworks whose marker is still unknown. AC #4 exists because §F1 was measured, not predicted.

---

### 4B — Spike AC additions (`epics.md` — Stories 11.1, 13.1, 14.1, 15.1)

Add as **AC #3** to each of the four framework integration spikes. Framework name substituted per story.

**OLD:** *(spikes carry two ACs — coverage map, and framework-extra/non-goals)*

**NEW — appended AC #3:**

```markdown
3.
**Given** a coverage map built from documentation is a hypothesis, not evidence
**When** the spike surveys the framework
**Then** a reference corpus of three real adopting repositories is selected and pinned per the Story 4.10
contract — each named with its commit SHA, its licence, and the variance it contributes — and every claim in
the coverage map is marked as confirmed-against-corpus, contradicted, or unobservable
**And** where fewer than three qualifying public repositories exist, the search query, its result count, and
the substitute used are recorded, and the reduced confidence is carried forward as a declared limit into the
coverage story.
```

**Rationale:** the ACs already say "representative repositories"; this makes *representative* mean named,
plural, obtainable and pinned. The confirmed/contradicted/unobservable marking is what would have made Story
12.1's six wrong claims visible at spike time instead of at dev time.

---

### 4C — Coverage-story AC additions (`epics.md` — Stories 11.2, 12.3, 13.2, 14.2, 15.2)

Add as the next free AC number in each story (11.2 → #3, 12.3 → #5, 13.2 → #3, 14.2 → #3, 15.2 → #3).

```markdown
N.
**Given** the framework's reference corpus selected by its integration spike
**When** generation runs against every corpus repository
**Then** each one generates without fatal errors, and an expected-versus-actual record is written for each —
covering page count, epic and story counts, the status distribution, the coverage tiers assigned, and the
diagnostics emitted — with every difference from expectation explained rather than merely observed
**And** each awkward shape the corpus revealed is carried into a temp-directory test fixture, so the corpus
repositories are never read by a test.
```

**Rationale:** this is the "verify that we render expected values for those sample projects" half of the
request. 12.2 did generate against CORA and recorded live-browser evidence, which is why it is a good model;
what it lacked was a *plural* corpus and a written expected-vs-actual table. The fixture-derivation clause is
12.2's own successful practice promoted to a requirement.

---

### 4D — Story 12.2: new AC #6 and reopen (`epics.md` + story file + `sprint-status.yaml`)

**Owner decision: 12.2 is reopened rather than superseded by a new story.**

```markdown
<!-- AC #6 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed). Story 12.2 shipped correctly but
     on a SINGLE, PRIVATE reference repository (`C:/dev/CORA`). Owner decision at correct-course: reopen the
     story (review -> in-progress) and widen its evidence rather than seat a follow-up, so the evidence stays
     with the story that owns the adapter. The public-repo probe run 2026-08-09 found ~1,932 files matching
     `path:.planning filename:ROADMAP.md "## Phases"` and ~2,547 matching `path:.planning/phases
     filename:PLAN.md`, so a three-repo GSD Core corpus is readily available. -->

6.
**Given** GSD Core support was verified against exactly one repository, which is private and unavailable to CI
or to any other contributor
**When** the reference corpus is widened to three repositories per the Story 4.10 contract — `C:/dev/CORA` plus
at least two PUBLIC adopting repositories, pinned by commit SHA
**Then** generation runs cleanly against all three, an expected-versus-actual record is written for each, and
any shape the adapter mishandles is either fixed or recorded as a declared boundary on the GSD framework page
**And** each newly-revealed shape is carried into `GsdCoreArtifactAdapterTests` as a temp-directory fixture,
with the corpus repositories themselves never read by a test.
```

**Story-file edits for `12-2-gsd-core-baseline-adapter-coverage.md`:**

- `Status: review` → `Status: in-progress`
- New AC #6 appended to **Acceptance Criteria**
- New **Task 12** appended to Tasks/Subtasks:

```markdown
- [ ] **Task 12 — Widen the GSD Core reference corpus to three repositories (AC: #6)**
  - [ ] Select 2+ PUBLIC GSD Core adopting repositories per the Story 4.10 contract, chosen for variance
    against CORA — prefer one with non-decimal phase numbering, one with a differently-shaped `STATE.md`, and
    one without a `## Backlog` section, if such exist. Pin each by commit SHA in the corpus manifest.
  - [ ] Generate against each into `SpecScribeOutput/`. Record page count, phase/plan counts, status
    distribution, coverage tiers and diagnostics as an expected-versus-actual table in Completion Notes.
  - [ ] Verify the milestone bands in a LIVE BROWSER over HTTP for at least one non-CORA repo. Note §F2:
    Chromium refuses `crossorigin` stylesheets over `file://`, so a `file://` check reports false failures.
  - [ ] Carry every newly-revealed shape into `GsdCoreArtifactAdapterTests` as a temp-dir fixture. Corpus repos
    are never read by a test.
  - [ ] Where a shape is mishandled and NOT fixed here, state it as a declared boundary on the GSD framework
    page (NFR8: absent, not misleadingly empty) rather than leaving it silent.
```

- **Change Log** entry appended recording the reopen and its provenance.

**Rationale for reopening rather than seating 12.4:** the adapter and its evidence stay in one place, and the
story's own §D1–§D5 decisions get re-tested against repos that did not inform them. The cost is that 12.2's
close date becomes unpredictable — accepted by the owner at this correct-course.

**Note for the eventual code review:** 12.2 has *not* been code-reviewed yet (Epic 12's review runs at epic
end). Reopening now is cheaper than reopening after review. Per CLAUDE.md § Scoping a code review, Task 12's
work must be attributed **by hunk** where it touches files a sibling story may also hold.

---

### 4E — Spike story-file amendments (four files, all `ready-for-dev`, never run)

Each of `11-1-spec-kit-integration-spike.md`, `13-1-specflow-integration-spike.md`,
`14-1-squad-integration-spike.md`, `15-1-superpowers-integration-spike.md` gets:

**1. AC #3** — the text from §4B, framework-substituted.

**2. A replacement task** — each file currently has a task of the form *"Fetch/inspect ⟨the tool's repo⟩ … to
confirm the exact layout."* Replace with:

```markdown
- [ ] **Task 2 — Build and pin the reference corpus (AC: #1, #2, #3)**
  - [ ] **Pass 1 — confirm the marker.** Scaffold the framework into a scratch directory (`⟨init command⟩`)
    and/or read its current docs. Do NOT treat the framework's own source repository as a reference repo — it
    is the tool, not a project that uses it.
  - [ ] **Pass 2 — find adopters BY the confirmed marker.** Search public repositories for the marker path
    (recipe in the Story 4.10 contract). Record the query and its result count.
  - [ ] Select THREE adopting repositories chosen for variance, not similarity. Pin each in the corpus manifest
    with URL, commit SHA, licence, approximate size, and the variance it contributes.
  - [ ] If fewer than three qualify, record the query, the count, and the substitute used, and carry the
    reduced confidence forward as a declared limit — do not silently proceed on one repo.
  - [ ] Mark every row of this story's hypothesis table **confirmed / contradicted / unobservable** against the
    corpus. A contradicted row is a finding, and it belongs in Completion Notes where the coverage story reads it.
```

**3. Two Dev Notes corrections, per file:**

- **All four:** *"The framework's own repository is the tool, not a reference repo."* (15.1 already half-says
  this; 14.1 and 15.1 currently point the dev agent at `bradygaster/squad` and `obra/superpowers` as the thing
  to inspect.)
- **13.1 and 15.1 additionally:** the marker is unconfirmed and the 2026-08-09 probe found **zero** public hits
  for `.specflow-version` / `.specflow-config.json`, and no usable query for Superpowers' plan-path convention.
  Pass 1 is therefore load-bearing for these two, and the shortfall rule is likely to fire. **This is expected
  and must be recorded, not worked around.**

**4. A Dev Notes pointer** to the §F1 `CONDITIONAL_CLASSES` hazard, so the coverage story that follows inherits
it.

**Rationale:** these four files are drafted but never run, so amending them costs nothing and is the only way
the requirement reaches an agent that reads only its own story.

---

### 4F — `sprint-status.yaml`

Co-landed with `epics.md` in the same change, per CLAUDE.md § Decision records.

```yaml
  # Epic 4 — Story 4.10 added 2026-08-09 (SCP 2026-08-09, correct-course, owner-directed): append-only
  # post-retro amendment, same pattern as 4.9. Epic 4 stays in-progress; epic-4-retrospective stays done.
  4-10-framework-reference-corpus-contract: backlog

  12-2-gsd-core-baseline-adapter-coverage: in-progress   # was: review
```

Plus a `last_updated` note recording the SCP, the reopen of 12.2, and the AC amendments to 11.1/11.2/12.3/13.1/
13.2/14.1/14.2/15.1/15.2.

---

### 4G — `CLAUDE.md`

New section, so an agent that reads no ADR still meets the standard:

```markdown
## Framework support: evidence before implementation

**A coverage map built from a framework's documentation is a hypothesis.** Story 12.1 built the GSD map from
vendor docs, said so in its own Debug Log, and six of its eight derived claims failed against one real
repository — costing Story 12.2 five mid-story decisions against its own task text.

Before implementing support for a framework:

- **Build a reference corpus of three real adopting repositories**, per Story 4.10's contract and
  `docs/framework-reference-corpus.md`. A repository that USES the framework — the framework's own source repo
  is the tool, not a reference. Choose for variance, not similarity.
- **Corpus repos are dev-time references, never a test dependency.** CI has no clone. Every shape they reveal
  becomes a temp-directory fixture, the way `GsdCoreArtifactAdapterTests` derives from CORA.
- **Confirm the marker before searching for adopters.** You cannot search for a marker you have not confirmed.
  Where fewer than three qualifying repos exist, record the query, the count and the substitute — a documented
  shortfall is a finding; a silent one is a defect.
- **Seed `CONDITIONAL_CLASSES` for any cross-framework markup.** `extract:ir-content` prunes any rule whose
  selector is absent from the IR, and the extraction corpus is THIS repo's IR — a BMad project. Markup that
  only a non-BMad repo produces is pruned **with every gate green**: measured on Story 12.2, all five
  `.milestone-band*` rules. `web/test/ir-content-harvest.test.mjs` is the layer that pins it; the round-trip
  gate structurally cannot.
```

---

## Section 5 — Implementation Handoff

**Scope classification: Moderate** — backlog reorganization across six epics plus one reopen. Routes to
**Product Owner / Developer**.

### Sequence

| # | Action | Owner | Blocking? |
|---|---|---|---|
| 1 | Land §4A–§4C, §4F, §4G — `epics.md` + `sprint-status.yaml` **in the same change**, plus `CLAUDE.md` | PO/Dev | — |
| 2 | Land §4E — four spike story-file amendments | PO/Dev | — |
| 3 | Land §4D — reopen 12.2, add AC #6 and Task 12 | PO/Dev | — |
| 4 | `create-story 4.10`, then `dev-story 4.10` — manifest, ADR 0044, discovery recipe | Dev | blocks 5, 6 |
| 5 | `dev-story 12.2` Task 12 — widen the GSD corpus | Dev | after 4 |
| 6 | Framework spikes 11.1 / 13.1 / 14.1 / 15.1 under the amended ACs | Dev | after 4 |

**Next free numbers, verified 2026-08-09:** Epic 4 story → **4.10**. ADR → **0044** (0043 is the highest on
disk; 0019 remains claimed-but-unwritten — do not take it).

### Success criteria

- `epics.md` and `sprint-status.yaml` agree on Story 4.10 and on 12.2's status — no drift between them.
- Each amended spike story names the corpus requirement in its own AC text, so an agent reading only that file
  meets it.
- `docs/framework-reference-corpus.md` exists and lists ≥3 pinned repos for GSD Core before 12.2 closes.
- ADR 0044 is indexed in `docs/adrs/README.md`.
- No corpus repository path appears in any test.

### Known hazards for whoever executes this

- **Concurrent edits.** `epics.md` and `sprint-status.yaml` are the two most contended files in the repo.
  Verify each edit landed by grepping for the inserted symbol, per CLAUDE.md § Concurrent work.
- **`epics.md` line anchors in this proposal** were read at `main`/`25d8c60` on 2026-08-09 and may have moved.
  Locate by heading, not by line number.
- **Pre-existing, out of scope, not this change's to fix:** ADR 0037 is missing from `docs/adrs/README.md`
  (12.2 §F5); `crossorigin` makes the portal render unstyled from `file://` in Chromium (12.2 §F2, NFR-3
  relevant); `FileWatcherServiceTests` flakes under parallel load (12.2 §F7).

---

## Appendix — Checklist record

| § | Item | Status |
|---|---|---|
| 1.1 | Triggering story identified — 12.2 | [x] Done |
| 1.2 | Core problem defined — *failed approach / process gap: framework scope derived from documentation, corrected during implementation* | [x] Done |
| 1.3 | Evidence gathered — 6/8 claims failed, §D1–D5, §F1–F7, 2026-08-09 feasibility probe | [x] Done |
| 2.1 | Current epic (12) completable as planned | [x] Done — yes, with 12.2 reopened |
| 2.2 | Epic-level changes — Epic 4 gains Story 4.10; no epic added, removed or redefined | [x] Done |
| 2.3 | Remaining epics reviewed — 11, 13, 14, 15 all AC-amended; all still unstarted | [x] Done |
| 2.4 | No epic invalidated; one story added | [x] Done |
| 2.5 | Epic order unchanged; one intra-epic sequencing constraint added (4.10 before the next framework spike) | [x] Done |
| 3.1 | PRD conflicts | [N/A] — FR3/FR4/FR17 unaffected; MVP unchanged |
| 3.2 | Architecture conflicts | [N/A] — no AD amended; ADR 0044 is additive to 0038/0041 |
| 3.3 | UI/UX conflicts | [N/A] — no surface change |
| 3.4 | Other artifacts — CLAUDE.md, corpus manifest, ADR index, testing strategy (fixture derivation) | [!] Action-needed — listed in §4 |
| 4.1 | Option 1 Direct Adjustment | [x] **Viable — SELECTED.** Effort Low–Medium, Risk Low (Medium for the 12.2 reopen) |
| 4.2 | Option 2 Rollback | [ ] Not viable — 12.2's work is correct; only its evidence is narrow |
| 4.3 | Option 3 MVP review | [ ] Not viable — MVP and FRs unaffected |
| 4.4 | Path selected — Option 1 + reopen, per owner decision | [x] Done |
| 5.1–5.5 | Proposal components | [x] Done — §§1–5 above |
| 6.4 | `sprint-status.yaml` update | [!] Action-needed — §4F, on approval |
