# Sprint Change Proposal — Release Engineering & Community Preview Launch

- **Date:** 2026-07-10
- **Author:** Matthew-Hope Eland (with Dev agent)
- **Workflow:** correct-course
- **Scope classification:** Moderate (backlog reorganization — new append-only epic + light PRD distribution note; no rollback, no MVP redefinition)
- **Change mode:** Batch

---

## Section 1 — Issue Summary

**Problem statement.** SpecScribe's plan thoroughly covers *what the tool does* (Epics 1–15: portal experience, insights, adapter foundation, code/git exploration, per-framework coverage) but has **no coverage of how the tool ships to the community**. To release a public preview we need release engineering that does not exist in the plan today.

**How discovered.** Owner-initiated: preparing to share SpecScribe with the community, even in preview.

**Supporting evidence (from the repo, not assumptions):**

- **CLI packaging is half-wired.** [`src/SpecScribe/SpecScribe.csproj`](../../src/SpecScribe/SpecScribe.csproj) already declares `PackAsTool=true`, `ToolCommandName=specscribe`, `PackageId=SpecScribe`, `Version=0.1.0`, MIT license, and a packed README. But **nothing publishes the package** — there is no NuGet push, no versioning/changelog discipline, and no release artifacts.
- **CI covers only the demo portal.** The single workflow, [`.github/workflows/publish-docs-live-pages.yml`](../../.github/workflows/publish-docs-live-pages.yml), builds the sample site and deploys it to GitHub Pages. There is **no build/test gate on pull requests** and **no release pipeline**.
- **No VS Code extension exists yet.** Epic 6 builds the read-only webview (Story 6.4 is the runtime, greenfield/new tech stack). "Publish a VSIX to the Marketplace" therefore has a hard dependency on Epic 6.
- **Docs are content-shaped, not release-shaped.** Epic 5 / Story 5.4 (*OSS Onboarding and Reference Documentation*, backlog) covers getting-started, CLI reference, and contribution guidance. It does **not** cover release comms: install-from-NuGet instructions, a `CHANGELOG.md`, a versioning/pre-release policy, or a Marketplace listing. (No `CHANGELOG` file exists in the repo.)
- **PRD gap.** PRD §4.4 *Distribution Surfaces* names CLI-first and the extension-as-follow-on, but says nothing about distribution mechanics (packaging, publishing, release pipeline) or a preview launch.

---

## Section 2 — Impact Analysis

### Epic Impact

- **No existing epic is invalidated.** This is additive scope.
- **New epic required:** Epic 16 — *Release Engineering & Community Preview Launch* (append-only, no renumber — consistent with the Epics 11–15 and Story 4.8 / 6.4 precedent).
- **Cross-epic dependencies:**
  - Epic 16's VS Code Marketplace story **depends on Epic 6** (the extension must exist before it can be packaged/published).
  - Epic 16's release-docs story **coordinates with Epic 5 / Story 5.4** — 5.4 owns onboarding/reference *content*; Epic 16 owns distribution-facing docs, changelog, and versioning policy. The seam is called out to prevent overlap.

### Artifact Conflicts

| Artifact | Impact |
|---|---|
| **PRD** | Non-conflicting gap. Add a short *Release & Distribution* note under §4.4 and append FRs to the inventory. MVP scope unaffected. |
| **Epics** | Add Epic 16 with Stories 16.1–16.7; append FR32–FR34 and NFR9 to the Requirements Inventory + FR Coverage Map. |
| **Architecture** | No architectural change. Release engineering is build/CI/packaging, orthogonal to the shared-core/adapter design. |
| **UX Design** | No UX change (Marketplace listing copy is release-docs, not product UX). |
| **CI/CD** | Primary target of the change: new PR build/test gate + new tag-triggered release pipeline, alongside the existing Pages workflow. |
| **sprint-status.yaml** | Add `epic-16` and its seven stories at `backlog`. |

### Technical Impact

- New GitHub Actions workflows (PR CI gate; release pipeline). Existing Pages workflow untouched.
- Repository secrets required (NuGet API key; VS Marketplace PAT/publisher) — inventoried by the spike, never committed.
- `dotnet pack`/publish already feasible from the current csproj; the release pipeline formalizes and automates it.

---

## Section 3 — Recommended Approach

**Selected path: Option 1 — Direct Adjustment (add a new append-only epic).**

| Option | Verdict | Rationale |
|---|---|---|
| **1. Direct Adjustment** | **Selected** | Purely additive; no existing work changes. Matches the project's append-only/no-renumber convention. A spike-led first story matches the Epics 11–15 pattern and resolves the open "which CLI channel" question before committing downstream stories. |
| 2. Rollback | Not viable | Nothing to roll back — this is new scope. |
| 3. MVP Review | Not applicable | MVP is not over-scoped; it is silent on release mechanics. We add scope, not reduce it. |

- **Effort:** Medium. **Risk:** Low (isolated to build/CI/packaging; product code untouched).
- **Timeline impact:** Runs in parallel with feature epics. The CI build/test gate (16.2) is worth landing early as release-readiness hygiene; the Marketplace story (16.5) is gated on Epic 6.

---

## Section 4 — Detailed Change Proposals

### 4A. New Epic

> ### Epic 16: Release Engineering & Community Preview Launch
>
> Everything needed to put a preview build of SpecScribe in the community's hands and keep shipping updates reliably: a reproducible build/test gate, packaged and published CLI distribution, a tag-triggered release pipeline, VS Code Marketplace publication of the read-only extension, release-facing documentation with a changelog and versioning policy, and a preview-launch readiness cut.
>
> **FRs covered:** FR32, FR33, FR34 · **NFRs:** NFR9
> **Depends on:** Epic 6 (for Story 16.5 — the extension must exist to be published).

### 4B. New Stories

**Story 16.1 — Release & Distribution Packaging Spike** *(spike-led first story, per Epics 11–15 pattern)*

> As a maintainer preparing a community preview,
> I want the distribution channels, versioning policy, and publishing prerequisites decided and written down before release stories begin,
> So that packaging work starts with an agreed scope and no surprise blockers.
>
> **AC1. Given** the CLI can ship via multiple channels, **when** the spike evaluates them, **then** a written decision records the chosen CLI channel(s) — NuGet `dotnet` global tool (already wired) and/or self-contained per-OS binaries — with rationale and explicit non-goals.
> **AC2. Given** publishing requires accounts and secrets, **when** the spike documents prerequisites, **then** it inventories every required secret/credential (NuGet API key, VS Marketplace publisher + PAT), where each is stored as a repo/environment secret, and any signing decision — **and** no secret value is committed.
> **AC3. Given** a preview release differs from a stable one, **when** the spike defines policy, **then** it records the versioning + pre-release scheme (e.g. `0.x` / `-preview` tags), the changelog format, and what "preview" promises and does not promise to consumers.

**Story 16.2 — Continuous Integration Build & Test Gate**

> As a maintainer,
> I want every pull request and push to build and run the test suite in CI,
> So that release builds start from a known-green baseline and regressions are caught before merge.
>
> **AC1. Given** a PR or push to a release-relevant branch, **when** CI runs, **then** it restores, builds, and executes the [`tests/SpecScribe.Tests`](../../tests/SpecScribe.Tests) suite on a clean checkout, and the job fails on any build or test failure.
> **AC2. Given** the gate is green, **when** a maintainer reviews the PR, **then** the build/test status is visible as a required signal, **and** the workflow is independent of (and does not disturb) the existing Pages publish workflow.

**Story 16.3 — CLI Packaging and Publication**

> As a prospective user,
> I want SpecScribe published to its chosen distribution channel,
> So that I can install and run it with a documented one-line command.
>
> **AC1. Given** Story 16.1's channel decision, **when** packaging runs, **then** the CLI is produced as the chosen artifact(s) — a NuGet global-tool package and/or self-contained per-OS executables — reproducibly from the repo with the version derived from the release tag (not a hard-coded csproj value).
> **AC2. Given** a produced package, **when** a user follows the documented install path (e.g. `dotnet tool install -g SpecScribe`), **then** the `specscribe` command runs and `--version`/`--help` report correctly, **and** the packaged README/license render on the package listing.

**Story 16.4 — Tag-Triggered Release Pipeline**

> As a maintainer cutting a release,
> I want pushing a release tag to build, verify, package, and publish automatically,
> So that releases are one action and never depend on a local machine's state.
>
> **AC1. Given** a release/pre-release tag is pushed, **when** the release pipeline runs, **then** it builds and tests on a clean checkout, packages per Story 16.3, publishes to the chosen channel(s), and attaches the release artifacts to the corresponding GitHub Release — **and** publishing is gated on the build+test step passing (NFR9).
> **AC2. Given** a `-preview`/pre-release tag, **when** the pipeline publishes, **then** the release is marked as a pre-release / preview channel per Story 16.1's policy, **and** a failed publish leaves no partially-released state (safe to re-run).

**Story 16.5 — VS Code Extension Packaging and Marketplace Publication** *(depends on Epic 6)*

> As a VS Code user,
> I want the read-only SpecScribe extension available from the Marketplace,
> So that I can install it without building from source.
>
> **AC1. Given** the Epic 6 extension exists, **when** the extension is packaged, **then** a valid VSIX is produced reproducibly with a Marketplace-ready manifest (publisher, display name, description, icon, categories, repository link) and versioning aligned to Story 16.1's policy.
> **AC2. Given** the VSIX and a configured publisher, **when** a release publishes the extension, **then** it appears on the VS Code Marketplace as a read-only preview and installs cleanly, **and** publication is automatable (extends the Story 16.4 pipeline or a parallel job) rather than a manual one-off.
> **AC3. Given** Epic 6 is not yet complete, **when** this story is scheduled, **then** it remains blocked/backlog and is not started until the extension surface exists (dependency made explicit).

**Story 16.6 — Release-Facing Documentation, Changelog, and Versioning Policy**

> As a community adopter,
> I want install/upgrade instructions, a changelog, and a stated versioning policy,
> So that I can adopt the preview confidently and track what changes between releases.
>
> **AC1. Given** the chosen distribution channels, **when** the release docs are produced, **then** the README (and Marketplace listing, if applicable) carry accurate install, upgrade, and quick-start instructions using real commands, **and** a `CHANGELOG.md` following the Story 16.1 format exists and is updated per release.
> **AC2. Given** Story 5.4 owns onboarding/reference *content*, **when** these release docs are written, **then** they cover distribution-facing concerns (install/upgrade, changelog, versioning/pre-release policy, Marketplace listing copy) and cross-link to — rather than duplicate — Story 5.4's material, **and** `--help`/`--version` output is audited to match the docs.

**Story 16.7 — Preview Launch Readiness and Cut**

> As a maintainer,
> I want a final readiness pass before announcing the preview,
> So that the first public impression is a working install, not a broken link.
>
> **AC1. Given** the pipeline and docs are in place, **when** the readiness checklist runs, **then** the CLI install path is verified end-to-end from the published artifact on a clean environment (and the extension install if Epic 6 shipped), the LICENSE and contribution/onboarding links resolve, and the preview version/tag is set per Story 16.1's policy.
> **AC2. Given** readiness passes, **when** the preview is cut, **then** release notes are published for the tag and the announcement points at working install instructions, **and** any items intentionally excluded from the preview are recorded as known limitations rather than silent gaps.

### 4C. Requirements Inventory additions (epics.md; sync back to PRD)

- **FR32:** Release engineering — reproducible packaging of the CLI to its chosen distribution channel(s), driven by a tag-triggered release pipeline that attaches release artifacts and supports preview/pre-release channels. *(Epic 16)*
- **FR33:** VS Code extension packaging and Marketplace publication of the read-only extension as a preview, dependent on the Epic 6 extension surface. *(Epic 16)*
- **FR34:** Release-facing documentation — install/upgrade instructions, a changelog, and a stated versioning/pre-release policy for community consumption. *(Epic 16)*
- **NFR9:** Release builds are reproducible and produced by CI from a clean checkout; publishing to any distribution channel is gated on a passing build + test run. *(Epic 16)*

### 4D. PRD note (§4.4 Distribution Surfaces)

Add a short *Release & Distribution* subsection stating: the CLI ships via a published distribution channel (decided in Story 16.1), the extension follows via the VS Code Marketplace after Epic 6, releases are produced by a tag-triggered CI pipeline gated on tests, and the initial public release is an explicitly-labeled preview. MVP scope is unchanged.

---

## Section 5 — Implementation Handoff

- **Scope:** Moderate → **Product Owner + Developer** coordination.
- **Sequencing:**
  1. **16.1 (spike)** first — unblocks channel/versioning/secret decisions.
  2. **16.2 (CI gate)** early — release-readiness hygiene, independent of the spike outcome.
  3. **16.3 → 16.4** — packaging then the automated pipeline.
  4. **16.6** — release docs alongside/after 16.3.
  5. **16.5** — only after **Epic 6** ships the extension.
  6. **16.7** — capstone launch cut.
- **Success criteria:** A community member can install the preview CLI from its published channel with a documented command and run it; releases are produced by CI on a tag with tests gating publish; a changelog and versioning policy are published; (once Epic 6 lands) the extension is installable from the Marketplace.
- **Deliverables on approval:** append Epic 16 + Stories 16.1–16.7 to `epics.md`; append FR32–FR34 / NFR9 + coverage-map lines; add `epic-16` and its stories (`backlog`) to `sprint-status.yaml`; add the PRD §4.4 note. Detail each story via `create-story` when scheduled (run `create-story` for 16.1 first).

---

## Approval

- [x] **Approved for implementation** — Matthew-Hope Eland, 2026-07-10.
- [ ] Revise (feedback: ______)

**Applied on approval (2026-07-10):**
- `epics.md` — Epic 16 (Stories 16.1–16.7) added to the Epic List + full detail section; FR32–FR34 and NFR9 appended to the Requirements Inventory; FR Coverage Map lines added.
- `sprint-status.yaml` — `epic-16` + seven stories seeded at `backlog` (16.5 flagged blocked on Epic 6).
- `prd.md` — §4.4 *Release & Distribution (preview)* subsection added.
