# ADR 0035: The SonarCloud quality gate is inherited deliberately, and rule-level exceptions have one home

- **Status:** Proposed
- **Date:** 2026-07-31
- **Deciders:** Owner (Matt Eland)
- **Context story:** [Story 25.2](../../_bmad-output/implementation-artifacts/25-2-quality-gate-and-findings-triage.md)
  (AC #1 decisions 1a–1e, AC #3's rule-level-decision home), amended by
  [Story 25.6](../../_bmad-output/implementation-artifacts/25-6-readme-coverage-and-quality-badges.md)'s
  2026-07-29 re-measurement, and by Story 23.5's in-flight supersession of 25.2's original coverage-exclusion
  answer. Raised at 25.2's own code review (2026-07-31) rather than at authoring time — the story's binding
  clarifications scoped a proposed-ADR requirement only to the (rejected) `.editorconfig` path, but the
  decisions below are cross-cutting CI/quality policy regardless of that narrower framing, and CLAUDE.md's
  decision-records section asks for one without being asked.
- **Numbering:** `docs/adrs/` ends at `0033` on disk. `0019` remains claimed-but-unwritten (Stories 18.3/22.3),
  and `0034` is claimed-but-unwritten by Story 26.2's spike (per `sprint-status.yaml`, verified by listing at
  authoring time). `0035` is the first uncontested slot.

## Context

Story 25.1 wired SonarCloud analysis into CI without deciding what the analysis *means*: no quality gate was
chosen, no stance was taken on the new-code period, and no home existed for "we know about this rule and we are
not going to fix it." SonarCloud does not wait for that decision — it applies its default `Sonar way` gate (id
9) to every project the moment analysis exists, and evaluates it whether or not anyone reads the result.

Story 25.2 found that default gate already **red**, on a project whose owner had made no gate decision at all,
and had to answer five sub-questions before the "quality gate" feature this project advertises meant anything:

1. **Keep the inherited gate, or mint a custom one?** The org already contained a cautionary tale: a second gate
   named `Customized` (id 4194), created at some point, applied to nothing, documented nowhere. A server-side
   gate object is invisible in a diff and drifts silently — `Customized` is that failure mode, already realized,
   sitting unexamined in the same org.
2. **What to do about `new_coverage` on a mixed C#/JS/Vue project** whose only coverage report was C#-only
   OpenCover, so JS/Vue new code counted as 100% uncovered by construction — a gate failing on a measurement gap,
   not a real quality problem.
3. **What "new code" means**, given the applied period is a sliding `days: 30` window. Measured behavior: new
   code went from 3,198 lines to 22,640 lines in a single day as the window absorbed unrelated epics landing on
   shared `main` — a new-code gate that has started behaving as a whole-project gate.
4. **Whether a failing gate blocks anything.** `sonar.qualitygate.wait` was unset by 25.1; nothing enforces the
   gate today regardless of its color.
5. **Where a "we do not follow this rule" decision is recorded**, so it survives the next analysis run instead of
   being silently re-triaged. SonarCloud's own UI (deactivating a rule in the quality profile) is the same
   invisible-server-side-object problem as the stray `Customized` gate.

These are not one story's implementation detail. Every future story that lands code, adds a dependency, or asks
"is the gate green" inherits whichever answer stands here, and the record needs to be findable without
re-deriving it — which is exactly what happened once already: Story 25.6 independently re-discovered and
corrected a stale claim about which rule drove `new_security_rating`, because the reasoning lived only in prose
scattered across three files (`docs/SonarCloudSetup.md`, `deferred-work.md`, `sprint-status.yaml`) with no single
authoritative record a later story could check itself against before restating a now-superseded number.

## Decision

**The `Sonar way` default gate is kept deliberately, not replaced — and every input that decision depends on
that *can* live in a diff, does.**

Concretely:

1. **Gate identity: `Sonar way` (id 9), not a custom gate.** Rejected both a bespoke project-specific gate and
   adopting the stray `Customized` gate. Where the gate's *inputs* can be expressed in-repo (coverage
   exclusions, rule suppressions, `qualitygate.wait`), they are, on the SonarScanner `begin` step in
   `.github/workflows/build-test-analyze.yml`, in a diff, matching the precedent Story 25.1 set for where this
   project's Sonar configuration lives. The gate's *conditions themselves* remain a server-side SonarCloud
   object — that break from "the truth lives in a diff" is accepted, not hidden, and mitigated by transcribing
   the six conditions verbatim into `docs/SonarCloudSetup.md` alongside the `curl` command that re-verifies them
   at any time.
2. **Coverage gap: fix the input, not the threshold.** A coverage exclusion for a scope with no report is a
   workaround for a *missing* report, not a permanent answer — supplying the report is preferred whenever it is
   affordable. (Story 23.5 proved this out days after 25.2 shipped the narrower workaround: it supplied real
   Vitest/lcov coverage for `web/` instead of leaving it excluded, and `new_coverage` improved from a workaround
   figure to a measured one. The exclusion tool stays available for scopes where a report genuinely cannot be
   produced — `extension/src/**` is deliberately *not* excluded today because its 0% is real information the
   project wants visible, at the accepted cost that its next change can turn the gate red on it.)
3. **New-code period: `days: 30` stays, with the defect named rather than hidden.** The alternatives
   (`previous_version`, a reference branch) cost more than they return before this project's first release —
   `previous_version` needs `sonar.projectVersion` wired to a build's informational version and is meaningless
   before a release exists; a reference branch is degenerate when the analyzed branch already is `main`. The
   documented **trigger to revisit**: adopt `previous_version` at the first release tag (Epic 16), filed as a
   standing owner action item, not left as prose someone has to rediscover.
4. **`sonar.qualitygate.wait` stays unset until three preconditions hold, and the preconditions are written
   down, not just believed.** Setting it before all three hold would turn every push to `main` red on code that
   is out of scope for whoever is landing at the time — an enforcement decision made before the gate is
   trustworthy is worse than no enforcement. The three preconditions (a passing `new_coverage`, `A`
   `new_reliability_rating`, `A` `new_security_rating`) are transcribed into `docs/SonarCloudSetup.md` and
   tracked as an open `sprint-status.yaml` action item with a named owner, specifically so the "gate everything
   silently" failure mode this ADR otherwise argues against does not reappear here in a different shape.
5. **Rule-level "we do not follow this" decisions have exactly one home: `docs/SonarCloudSetup.md` § Rule-level
   decisions, enforced (when a rule reaches that point) via `sonar.issue.ignore.multicriteria` on the
   SonarScanner `begin` step.** A quality-profile change in the SonarCloud UI is rejected as a home for the same
   reason `Customized` is a cautionary tale: invisible in a diff, drifts silently, no reviewer ever sees it.
   `.editorconfig` was considered and rejected — it can reach only the `external_roslyn:` band, not
   `csharpsquid:`/`css:`/`javascript:` rules, so it can never be the *single* home this decision requires, and it
   would additionally change local and CI build-warning behavior for `src/`/`tests/` as a side effect nobody
   asked for. The mechanism is deliberately applied to **zero rules today** — every rule currently in the
   baseline is either routed to a named Epic 17 story (where suppressing it would hide scheduled work from the
   dashboard meant to prove it done) or is the INFO-band external-Roslyn import whose disposition depends on
   measurement the suppression would destroy. That is a decision with a stated reason, not an oversight.

## Consequences

- **A red project-level gate is expected and accepted for now.** The gate does not block anything
  (`qualitygate.wait` unset), and Story 25.6's quality-gate badge is correspondingly blocked from shipping until
  the three preconditions clear — a red badge on the README the moment it lands is worse than no badge, so it
  waits.
- **The gate's server-side half is a standing, accepted blind spot.** Anyone auditing this project's Sonar
  configuration from `git log` alone will not see the six gate conditions or the fact that `Sonar way` — not a
  custom gate — is what evaluates. `docs/SonarCloudSetup.md` is the compensating record; it must be kept current
  by hand, and this ADR is what says it must be.
- **The new-code window will keep behaving like a whole-project window until Epic 16's first release tag.**
  Any story landing before then should expect "new code" in Sonar's reporting to mean something closer to "all
  code the 30-day window has swallowed" than "code this diff added," and should not be surprised by gate
  conditions that look disproportionate to a small change.
- **Rule suppressions, once any are actually applied, must land in the workflow file's `begin` step and be
  cross-referenced in `docs/SonarCloudSetup.md` — never only toggled in the SonarCloud UI.** A future session
  that deactivates a rule in the quality profile instead of following this path has silently reintroduced the
  `Customized`-gate failure mode this ADR exists to close off.
- **This record is now the thing a later story checks before restating a Sonar-driven fact, instead of
  re-deriving or re-copying prose that can go stale.** Story 25.2's own artifacts (`deferred-work.md`,
  `sprint-status.yaml`) still contain at least one uncorrected stale claim about which rule drives
  `new_security_rating`, found at this ADR's originating code review — a concrete instance of the problem this
  ADR's single-home requirement is meant to prevent from recurring.

## Alternatives considered

- **Adopt a custom quality gate**, either from scratch or by applying the org's existing `Customized` gate.
  Rejected: both are server-side objects invisible in a diff, and `Customized` is a live demonstration in this
  same org of exactly the drift that creates — made, applied to nothing, documented nowhere, until this story
  found it.
- **Add JS/TS coverage collection immediately** to close the coverage gap that made 25.2's original exclusion
  necessary. Deferred rather than rejected at 25.2's authoring time (no Node test runner existed yet); Story 23.5
  did this days later and it is now the standing answer, folded into Decision 2 above.
- **Switch the new-code period to `previous_version` now.** Rejected as premature: the field it depends on
  (`sonar.projectVersion`) is not wired to anything meaningful before this project's first release, and a
  reference branch is degenerate when the analyzed branch already is `main`. Filed as a triggered action instead
  of a live decision.
- **Set `sonar.qualitygate.wait` immediately, to make the gate mean something sooner.** Rejected: at authoring
  time this would have turned every push to `main` red on `web/` bugs and `src/` vulnerabilities that Epic 25 is
  explicitly forbidden to touch, breaking CI for concurrent sessions mid-epic over conditions nobody landing
  code that day could fix.
- **Record rule-level exceptions per-issue in the SonarCloud UI ("Won't Fix").** Rejected on volume alone — it
  does not scale to bands of 100+ instances of one rule, and it is per-issue rather than per-rule, which is not
  the granularity AC #3 required.

## Ratified decisions

None yet — this ADR is **Proposed**. Ratification is the owner's.
