# ADR 0033: Content-drift gates are targeted and regenerable, not whole-tree fingerprints

- **Status:** Proposed
  - ⏫ **Ratification to `Accepted` PROPOSED by [Story 23.6](../../_bmad-output/implementation-artifacts/23-6-retire-the-c-sharp-html-writer.md) (2026-07-31)**, which is its first
    implementation: `npm run check:parity` satisfies §Decision 1 (a failure names the page), §Decision 2 (the
    corpus is FROZEN, so a sibling story elsewhere cannot turn it red), §Decision 3 (`npm run pin:parity` is a
    command producing a reviewable per-route diff), §Decision 4 (determinism proven across 3 local runs, with the
    Ubuntu half wired into `portability-probe`), and §Decision 5 (three loudness gates, all negative-tested).
    ⚠️ **One amendment is requested with it**: §Decision's "reference implementation" names
    `web/measurements/parity.json`, and Story 23.6 measured that oracle's shape to be **vacuous** — `goldenSha`,
    `irSha` and `nuxtSha` are IDENTICAL on all 1,469 rows, so reading the committed value back asks the same
    question the live run already answers. The reference implementation should be re-pointed at the pinned
    corpus. Ratification is the owner's; this line is the proposal, not the act.
- **Date:** 2026-07-30
- **Deciders:** Owner (Matt Eland)
- **Context story:** [Story 23.4](../../_bmad-output/implementation-artifacts/23-4-migrate-remaining-surfaces-retire-c-sharp-html-adapter.md)
  (owner decision D7), which surfaced the question by losing its own gate to it.

## Context

SpecScribe has used a **whole-tree fingerprint** as its content-drift gate since early in the project:
`GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` hashes every output file's
normalized content into one SHA-256 constant. Story 23.4 landed a second one over the IR,
`GenerateAll_GoldenIrFingerprint_IsStableAfterNormalizingVolatileTokens`, to keep a gate in place once the C#
page writer is deleted.

That second gate was **removed** on 2026-07-30 (commit `70b72ab`). It produced three different hashes across the
local development machine, CI-Windows and CI-Ubuntu for one identical commit. One cause was found and fixed
(`FallbackCodeWalk`'s unsorted directory walk, commit `7510a70`, which stays fixed). A second was never
identified after investigating unordered `HashSet` enumeration, `Parallel` usage and `Environment.NewLine`
leaks. The owner ended the investigation rather than keep spending CI round-trips on it.

That was not an isolated misfortune. The pattern is recorded repeatedly in this repository:

- **It moves for reasons unrelated to the change under test.** CLAUDE.md § Concurrent work states plainly that
  the constant "may shift because of a concurrent session's changes, not yours," and instructs agents to prove
  causality by bisecting into a throwaway tree before touching it. Stories 18.2, 18.4 and 18.6 each did that and
  each proved the move was somebody else's.
- **Its recorded value is stale almost immediately.** `deferred-work.md:1273` records four consecutive stories
  (22.2, 22.3, 22.4, 22.5) citing a constant that was already wrong when written. Story 23.4 made it five,
  twice in one session, once by reading the regeneration log comment instead of the assertion.
- **It fails late and coarsely.** A moved hash says "some byte, somewhere in the tree, changed." Localizing it
  costs a full generate plus a manual diff.
- **It invites the wrong repair.** CLAUDE.md has to carry an explicit rule against regenerating it reflexively,
  because Epic 5 found the harness itself leaking the commit SHA — a defect that a reflexive regeneration would
  have hidden behind a green test.

Stated as the owner did: the gate was *"unreliable and exceptionally brittle to how I work with multiple parallel
feature development … nothing owning regenerating that before CI"*, and the standing requirement is *"tests that
catch issues, but not overly-sensitive ones or things that agents just never run and fail on all the time."*

A gate that is red for reasons unrelated to the change under test is not a strict gate. It is a gate that gets
regenerated without reading, which is strictly worse than no gate — it carries the *authority* of a check while
performing none.

## Decision

**A content-drift gate must be targeted and regenerable. A single hash over a whole tree is not an acceptable
shape for a new gate.**

Concretely, a content-drift gate added from this point:

1. **Localizes its failure.** A failure names the artifact that moved — a page, a route, a file — not "the
   output." A gate whose entire diagnostic output is one changed hex string does not satisfy this.
2. **Is scoped to what the change under test can plausibly affect**, so a sibling story working elsewhere in the
   tree does not turn it red.
3. **Has an owner-runnable regeneration path that is a command, not a constant-bump.** Regeneration must be a
   deliberate, reviewable act producing a reviewable diff — `npm run measure:parity` rewriting
   `web/measurements/parity.json` is the shape; editing a hex literal in an assertion is not.
4. **Is deterministic across machines and CI before it is pinned.** Confirmed stable across at least two
   consecutive runs, and — where the gate will run in CI — on the CI operating systems too. Story 23.4's IR
   fingerprint passed the two-run rule locally and still differed on three platforms; two local runs is a floor,
   not a proof of portability.
5. **Fails loudly rather than vacuously when its oracle disappears.** A gate whose comparison basis can silently
   become empty must assert its basis is non-empty first. `RegionCompositionCorpusProof` already does this — it
   asserts the deep-git surfaces exist *before* trusting a delta count, precisely so a partial run cannot report
   a vacuous "0 deltas."

**The reference implementation of this shape already exists and is committed:** `web/measurements/parity.json`,
Story 23.4's per-page sha256 oracle over 1,469 pages. A failure names the page; regeneration is an explicit
`npm run`; the diff is reviewable per page.

**`GenerateAll_GoldenContentFingerprint_IsStableAfterNormalizingVolatileTokens` is grandfathered, not blessed.**
It stays for now because it currently covers real output that nothing else covers. This ADR does not schedule its
removal; it declines to accept more gates of its shape, and it directs that whatever replaces it when the C# page
writer is retired ([Story 23.6](../../_bmad-output/implementation-artifacts/23-6-retire-the-c-sharp-html-writer.md))
takes the targeted shape rather than being re-pointed at the IR as another whole-tree hash.

## Consequences

- **There is currently no content-drift gate over the IR.** `GoldenIrFingerprint` is gone and nothing replaced
  it. `GoldenContentFingerprint` covers the rendered `.html` only, and it is voided the moment the C# writer is
  deleted. Story 23.6 inherits this hole explicitly; `deferred-work.md` carries it as the standing action.
- **The per-page oracle costs more to store** than one constant — `parity.json` holds 1,469 hashes. That is the
  price of localization and it is accepted.
- **Gates get slower to author.** Requirement 4 means a new gate cannot be pinned from a single local run. This
  is deliberate: Story 23.4's IR fingerprint was pinned after three local runs and still failed on CI.
- **Requirement 2 admits blind spots.** A scoped gate cannot catch drift outside its scope, where a whole-tree
  hash nominally could. The judgment recorded here is that a coarse gate which is regenerated unread catches less
  in practice than a narrow gate that is trusted — and that the honest response to a blind spot is another
  targeted gate, not a return to hashing everything.
- **CLAUDE.md's rules about the golden fingerprint remain in force** for the grandfathered gate: establish
  causality before regenerating, rebuild non-incrementally before trusting a hash that involves an embedded
  asset, and confirm across two repeated runs.

## Alternatives considered

- **Fix the IR fingerprint's remaining nondeterminism and keep it.** Rejected by the owner after one confirmed
  cause was fixed and a second resisted identification across three environments. The cost was open-ended CI
  round-trips, and the resulting gate would still have had every brittleness property above.
- **Keep whole-tree fingerprints but regenerate them automatically in CI.** Rejected: a gate that regenerates
  itself asserts nothing. This is the failure mode Epic 5 found, where the harness leaked the commit SHA and a
  regeneration would have made it green.
- **Drop content-drift gating entirely** and rely on the unit suite. Rejected: the defects this class of gate
  actually catches are the ones unit tests structurally cannot see — Story 23.4's finding 4 (the same content
  dropped by three independent layers) and Story 23.3's double-wrapped `<main>` on 187 pages both passed every
  unit test.
- **Make the per-page oracle a hard CI gate immediately.** Deferred, not rejected. `measure:parity` needs a
  generate to run against; wiring it into CI is Story 23.6's to scope, alongside the writer deletion that
  changes what it compares.

## Ratified decisions

None yet — this ADR is **Proposed**. Ratification is the owner's.
