---
title: 'Sprint Change Proposal — Roadmap Seating: VS Code Native Integration, Pre-Publication Hardening, BMad Module Exploration'
date: '2026-07-11'
workflow: 'bmad-correct-course'
mode: 'incremental'
status: 'applied'
scope_classification: 'moderate'
---

# Sprint Change Proposal — 2026-07-11

## 1. Issue Summary

Three roadmap gaps were identified by the owner and routed through `correct-course`:

1. **VS Code native-integration ideas were at risk of being lost.** The research deliverable [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md) (25+ recommendations R1–R8, seated from the one-shot spec [spec-vscode-native-integration-recommendations.md](../implementation-artifacts/spec-vscode-native-integration-recommendations.md)) proposed a substantial growth of the extension's host-integration surface (discoverability, native tree view / status bar, editor↔artifact bridges, reactivity hardening) plus one **shipped live-data defect** (non-`.md` sources never trigger refresh) and one **security prerequisite** (Workspace-Trust posture before Marketplace). None of this was reflected in the binding roadmap — it lived only in a recommendations doc that any future planning pass could overlook.

2. **No dedicated pre-publication hardening pass existed.** The roadmap went from feature epics straight into release engineering (Epic 16). There was no epic to deliberately remediate structural weaknesses, inconsistencies, inefficiencies, and security/privacy gaps, or to burn down the substantial `deferred-work.md` backlog, before putting the tool in front of public **and** private codebases.

3. **BMad's own module ecosystem was unscoped.** Epics 11–15 cover *third-party* frameworks (Spec Kit, GSD/GSD-Pi, SpecFlow, Squad, Superpowers). There was no epic to explore BMad's **native** modules and expansions (BMad Builder, Creative Intelligence, game-dev / GDS-style expansions) beyond the BMM core already supported.

The owner also directed an **end-of-roadmap resequencing**: the final delivery order should be **CLI → hardening → publication**, and the CLI epic (Epic 5) should shed its OSS-documentation story since a release epic now owns that content.

**Discovery context:** raised directly by the owner via `/bmad-correct-course` on 2026-07-11, immediately after the VS Code recommendations doc was completed and adversarially reviewed (11 findings, all patched).

## 2. Impact Analysis

- **Epic impact:** Two new epics (17 Hardening, 18 BMad Modules). Epic 5 retitled and narrowed. Epic 16 broadened (absorbs OSS docs). Epic 6 expanded with five native-integration stories. No existing epic's delivered work is altered.
- **Story impact:** New Stories 6.8–6.12, 17.1–17.4, 18.1–18.2. Story 5.4 **removed** (folded into 16.6). Story 16.6 rewritten to own onboarding/reference + release docs. Breadcrumb annotations added to Stories 5.2, 7.1, 7.2, 8.4, 16.5, 6.7 (recommendations they own). No in-flight `ready-for-dev`/`in-progress` story changed except the retirement of 5.4 (which was `backlog`).
- **Requirements impact:** +FR35 (native VS Code surfaces), +FR36 (BMad modules), +NFR10 (public/private hardening). FR18 coverage moved Epic 5 → Epic 16. Coverage map updated. (PRD sync deferred, consistent with the existing FR20–FR34 provenance-comment convention.)
- **Architecture impact:** None to the ADRs. All new work respects the Epic 6 invariants (rendering stays in C#; extension stays read-only — ADR 0005 AD-1/AD-2, ADR 0003 AD-6) and the golden byte-parity gate. The native surfaces use the ADR 0005 §1 "JSON export for a non-webview consumer" clause explicitly.
- **Sequencing impact:** Numbers stay stable (append-only / no-renumber). Delivery run order is now carried in the Overview note and `sprint-status.yaml`: features (1–4, 6–15, 18) → **Epic 5 → Epic 17 → Epic 16**.
- **Technical/debt impact:** Positive — the recommendations doc's shipped defect (R6.1) and several latent security items already in `deferred-work.md` (unescaped `<h1>`/`cssClass`, `RequirementLinkifier` attribute injection, `toolPath` RCE surface) now have explicit owning stories (6.11, 17.2).

## 3. Recommended Approach

**Direct Adjustment** — additive epics/stories within the existing plan, no rollback, no MVP reduction. Chosen because all three asks are net-new scope that attaches cleanly to the current architecture without reworking delivered code. Sequencing expressed via the delivery-order note rather than physical renumbering, to preserve the append-only convention and avoid churning the live Epic 5 story IDs (5.1–5.3 `ready-for-dev`), `sprint-status.yaml` keys, worktree references, and memory.

- **Effort:** planning-only in this change; implementation effort spread across the new stories (6.8–6.12 range S–L per the recommendations doc's wave tags; Epic 17 is a focused review-and-remediate epic; Epic 18 is spike-led).
- **Risk:** low. Nothing changes rendered output or delivered behavior; the golden fingerprint is untouched.
- **Timeline:** Epic 17 adds a gating step before the community preview; Epic 18 is exploratory and off the release-blocking path.

## 4. Detailed Change Proposals

All edits applied to [epics.md](_bmad-output/planning-artifacts/epics.md) and [sprint-status.yaml](_bmad-output/implementation-artifacts/sprint-status.yaml).

### Requirements (epics.md · Requirements Inventory)
- **FR35** (new): native VS Code host-integration surfaces beyond the webview panel — read-only, core-emitted data.
- **FR36** (new): BMad module/expansion coverage exploration via the shared adapter contract.
- **NFR10** (new): pre-publication hardening for public + private codebase readiness.
- **FR18**: coverage moved Epic 5 → Epic 16.

### Delivery sequencing (epics.md · Overview)
- Rewrote the phase description; added a **"Delivery sequencing (numbers are stable IDs, not run order)"** note fixing the end-of-roadmap order as **Epic 5 → Epic 17 → Epic 16**, with Epic 18 exploratory alongside Epics 11–15 and Epic 6's 6.8–6.12 completing before hardening.

### Epic 5 — Reliable CLI Operations and Configuration
- Retitled (was "Reliable Operations, Configuration, and OSS Documentation"); OSS-docs clause dropped; FRs `FR8, FR12, FR18` → `FR8, FR12`.
- **Story 5.4 removed** (folded into 16.6); slot intentionally vacant.

### Epic 16 — Release Engineering & Community Preview Launch
- FRs gain FR18; now depends on Epic 17's hardening sign-off for its cut.
- **Story 16.6** retitled and expanded to own onboarding/reference docs (former 5.4 ACs carried as AC #2/#3), dropping the "Story 5.4 owns it, cross-link" language.

### Epic 6 — new native-integration stories (seat R1–R8)
- **6.8** Extension Discoverability, Workspace Trust, and Command Surface (R5.4, R1.1–R1.3, R2.1–R2.4, R3.3, R5.2, R7.1–R7.3).
- **6.9** Native Project Outline — Tree View and Status Bar (R3.1, R3.2, R1.5).
- **6.10** Editor ↔ Artifact Bridges (Reveal-Source) (R4.1; seam for R4.2/R4.3).
- **6.11** File-Change Reactivity Hardening (R6.1 shipped defect, R6.2, R6.3).
- **6.12** Native Diagnostics — Problems Panel Integration (R8.3, rides Story 4.8).
- Epic 6 FRs gain FR35.

### Annotations on existing stories (recommendations they own)
- **5.2** ← R5.3 (webview spawn ignores directory-scoped settings — parity gap).
- **7.1 / 7.2** ← R4.2 (structured `data-code-path`/`data-line` so the host can re-target code citations).
- **8.4** ← R4.3 (webview "Open in Terminal" staged-command handoff, read-only).
- **16.5** ← R1.4 (walkthrough), R1.6 (Marketplace metadata), R8.1 (platform-specific VSIX targets); prerequisite R5.4 from 6.8.
- **6.7** ← R8.2 (keep payload shape compatible for instant-first-paint JSON consumption).

### Epic 17 — Code Hardening & Release-Readiness Review (new; NFR10)
- **17.1** Structural and Consistency Remediation Sweep.
- **17.2** Security and Privacy Hardening for Public and Private Repos.
- **17.3** Performance and Efficiency Pass.
- **17.4** Deferred-Work Burndown and Release-Readiness Sign-off (gates Epic 16 publish/cut).

### Epic 18 — BMad Module & Expansion Coverage Exploration (new; FR36)
- **18.1** BMad Module Landscape and Coverage Spike.
- **18.2** Priority BMad Module Baseline Coverage.

### sprint-status.yaml
- Retired `5-4-...` with a breadcrumb; registered `6-8`…`6-12`, `epic-17` + `17-1`…`17-4`, `epic-18` + `18-1`/`18-2` (all `backlog`); refreshed `last_updated`.

## 5. Implementation Handoff

**Scope classification: Moderate** — additive backlog reorganization; no fundamental replan, no architecture change.

- **Routing:** Product Owner / Developer. The roadmap and tracking edits are applied. Next operational step is `create-story` per new story when each is scheduled — spike-first where applicable (**18.1** before 18.2; **17.4** sign-off last in Epic 17). Story **6.8** carries the Workspace-Trust item (R5.4) that is a **prerequisite for the Story 16.5 Marketplace publish** and should be prioritized within the Epic 6 native wave.
- **Success criteria:** each new story reaches `ready-for-dev` via `create-story` with ACs intact; Epic 17's sign-off (17.4) precedes any Epic 16 publish/cut; the golden byte-parity gate and test suite stay green throughout (no hardening/efficiency change alters rendered output unless intentionally re-baselined).
- **Follow-ups recorded, not done here:** PRD traceability sync for FR35/FR36/NFR10 (consistent with the existing deferred FR20–FR34 sync note); the recommendations doc's "with their owning stories" bindings are now annotated in place so the seams get designed in rather than retrofitted.

---
*Generated by the `bmad-correct-course` workflow (incremental mode). Backing research: [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md).*
