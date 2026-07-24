# SpecScribe — Working Conventions for Agents

Project-level guidance for any agent working in this repository. These are working
conventions, not architecture: architecture lives in `docs/adrs/` and
`_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`.

## Concurrent work on shared `main`

**Assume another agent may be editing the same files right now.** The primary machine
cannot run parallel git worktrees, so isolation is not available and is not the fix.
This is an accepted working condition, not a defect to engineer away.

Consequences you must plan for:

- **Verify after every edit.** Do not trust that a write landed just because the tool
  returned success and the build passed. Grep for the symbol you just added before
  relying on it. A `Charts.cs` edit has silently vanished this way before.
- **Never `git reset --hard`, `git checkout --`, or `git clean`** to tidy up. Another
  session's uncommitted work may be in the tree. This has already destroyed real work
  mid-story.
- **Expect the golden fingerprint to move under you.** `GoldenContentFingerprint` may
  shift because of a concurrent session's changes, not yours. Confirm a regenerated
  hash is stable across two repeated runs before locking it in, and say in the story
  record whose changes the regeneration sat on top of.
- **Expect commits to bundle sibling stories.** Because code review runs at epic end
  (see below), a single commit routinely carries several stories' work.

## Story lifecycle (the owner's actual workflow)

1. `create-story` — seed the story with context and, for any visual surface, elicit
   named design directions from the owner up front.
2. `dev-story` — implement.
3. **Owner verifies the rendered behavior** and gives extensive commentary: things that
   are wrong, not standardized across surfaces, or simply behaving badly. The story
   iterates. This is a designed stage, not rework — but its size is driven by how deep
   the create-story elicitation went. Detail that lands at create-story
   (colors, units, density, empty states, controls) is detail this stage does not have
   to spend a round on.
4. **Code review runs at epic end**, once every story in the epic is complete and the
   owner is satisfied — not per-story on merge.

### Scoping a code review

Because reviews run at epic end over bundled commits, **scope by the story's own
`File List` and its declared symbols — never by a commit range.** State the exclusion
explicitly in the review record ("sibling stories X/Y excluded from the same commit
range"). Verify a story's claimed symbols actually exist before trusting its File List.

## Decision records

- **Propose an ADR without being asked** for any decision that changes shared
  architecture, a cross-cutting contract, or amends a prior ADR. Do not bury such a
  decision as an owner-locked note in a story file or `sprint-status.yaml` prose.
- **Read `docs/adrs/` before declaring you are crossing a project rule.** Story 21.3
  described its interactive treemap as "a deliberate crossing of the pure-SVG, no-JS
  rule," citing a memory — when ADR 0010, ratified two days earlier, already permitted
  exactly that for opt-in deep-analytics surfaces. Project memory can be stale; a
  ratified ADR is the authority.
- **Structural scope changes land in `epics.md` and `sprint-status.yaml` in the same
  change.** A renumber, spike insertion, or story add/remove recorded in only one
  artifact is a drift bug.

## Verification

- Generate to `SpecScribeOutput/` (the default). Never `--output docs/live` — that path
  is vestigial and gitignored.
- **Verify visual and layout work in a live browser.** The test suite is large and
  valuable, but it structurally cannot see CSS containment leaks, sub-pixel layout
  collapse, or DOM corruption from markup splicing — all three shipped and were caught
  only by looking at the rendered page. Inspect real computed styles and real
  scroll/DOM geometry; bisect live rather than guessing.
- Every chart needs an accessible text equivalent, and no state may be signalled by
  color alone.
