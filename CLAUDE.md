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
- **Expect a content-drift gate to move under you.** It may shift because of a
  concurrent session's changes, not yours. Confirm a regenerated result is stable across
  two repeated runs before locking it in, and say in the story record whose changes the
  regeneration sat on top of.
- **Never regenerate a gate's baseline reflexively — establish causality first.**
  If a gate moved and you did **not** touch rendering, audit the harness *before*
  touching the baseline: a broken normalizer leaks a volatile token, and regenerating
  hides the defect behind a green test. Epic 5 found exactly that — the harness itself
  was leaking the commit SHA. Prove whose change moved it by bisecting into a throwaway
  tree (`git archive HEAD` into the scratchpad, then overwrite only your own files) —
  never by resetting the shared tree. Stories 18.2, 18.4 and 18.6 each did this and
  each proved the move was somebody else's.
- **Rebuild non-incrementally before trusting anything that involves an asset.**
  `specscribe.css`/`.js` are embedded resources; an incremental build reuses the cached
  assembly and never re-embeds a changed asset, so what you measure is stale. This bites
  the *rendered page* too, not just a hash: a `generate` after an incremental build
  serves the previous CSS, and the styles you are inspecting in the browser are not the
  ones you wrote.

### Which gate is which — `GoldenContentFingerprint` is retired

`GoldenContentFingerprint` no longer exists. ADR 0034 (Story 23.6) retired it with its
subject — the C# `.html` writer — and
`tests/SpecScribe.Tests/SiteGeneratorAdapterTests.cs` carries only its tombstone comment.
The live gates, and what each can actually see:

| gate | run from | subject | catches |
|---|---|---|---|
| `npm run check:parity` | `web/` | the RENDERER against a **frozen** corpus (`web/fixtures/parity-corpus/`) + pinned oracle | a change in how Nuxt renders a fixed IR |
| `npm run check:ir-content` | `web/` | `web/assets/ir-content.css` vs `src/SpecScribe/assets/specscribe.css` | a stylesheet edit not propagated to the scoped layer |
| `npm run check:tokens` / `check:assets` | `web/` | token block / runtime asset copies | the same class, for tokens and assets |

**`check:parity` cannot see a C#-side change.** Its corpus IR is frozen, so anything the
C# region composer emits differently — nav markup, a new dashboard panel, changed body
HTML — renders from the *pinned* input and the gate stays green. Verified 2026-08-01: a
change that removed an element from the shared nav on every page left all 24 routes
byte-identical. Do not read a green `check:parity` as "my rendering change is safe"; it
means "the renderer still behaves the same on the frozen fixture". Cover C#-side output
with unit tests over the region and with live-browser inspection.

Regenerate with `npm run pin:parity`, which produces a **reviewable diff** rather than a
hex-literal bump (ADR 0033). ADR 0033 also governs any NEW gate: it must localize failure
to a named artifact, be scoped so a sibling story elsewhere cannot turn it red, and be
proven deterministic across machines and CI operating systems before pinning.

### Changing `specscribe.css`? The regeneration order is load-bearing

`extract:ir-content` **prunes** any rule whose selector names a class or id it cannot find
in the IR. So a stylesheet edit that lands alongside NEW markup must be extracted from an
IR that already contains that markup, or the new rules are dropped — silently, with the
gate green, and the styles simply absent from the rendered page. Run:

```sh
dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental   # re-embed the asset
dotnet run --project src/SpecScribe -- generate                  # IR now has the new markup
cd web && npm run extract:ir-content && npm run check:ir-content # derive from THAT IR
cd web && npm run build:package                                  # renderer bundles the CSS
dotnet run --project src/SpecScribe -- generate                  # render with it
```

Two generates, deliberately. Skipping either one leaves you inspecting a page whose CSS
predates your edit — and the failure looks exactly like "my selector is wrong".

**The gate cannot catch a bug in its own derivation.** `check:ir-content` re-derives
through the same `harvest`/`selectorIsUsed` code the extractor uses, so a rule wrongly
dropped is dropped identically on both sides and the diff is empty. That is not
hypothetical: a dangling `else` in `harvest` meant **no id was ever collected**, so every
id-bearing selector was pruned and the Code Map's pure-CSS spec/test filter was absent
from the shipped site — for however long, with every gate green. Found 2026-08-01 only by
reading computed styles in a live browser. `web/test/ir-content-harvest.test.mjs` now
pins the derivation itself; extend it rather than trusting the round-trip gate.
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

**When siblings share the same files, File-List scoping is not enough — fall back to
attribution by hunk.** Scoping by file assumes one story owns each file. Once several
stories land in the same file, a symbol a *sibling* added is invisible to **both**
reviews: yours skips it as not-your-file-region, theirs skips it as not-in-their-File-List.
Story 18.2's review found `IsModulePresent` and `ForCode` sitting in 18.2's primary file
while their own doc comments self-attributed them to Story 18.5 — neither review would
have covered them if that one had not recorded the handoff. So:

- Attribute by **hunk**, not by file, whenever a file appears in more than one in-flight
  story's File List.
- A symbol whose doc comment attributes it to another story is that story's to review —
  **record the handoff explicitly** in your review record so it cannot fall between them.
- Say in the record which hunks you excluded and to whom, the same way you already state
  excluded sibling stories.

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

## Framework support: evidence before implementation

**A coverage map built from a framework's documentation is a hypothesis, not evidence.** Story 12.1 built the
GSD map from vendor docs, said so plainly in its own Debug Log, and **six of its eight derived claims failed**
against one real repository — costing Story 12.2 five mid-story decisions against its own task text. The AC
wording was the root cause: "representative repositories" never required them to be named, obtainable, or
plural. Story 4.10 owns the contract; `docs/framework-reference-corpus.md` is the manifest.

Before implementing support for a framework:

- **Build a reference corpus of three real adopting repositories.** A repository that *uses* the framework —
  the framework's own source repo (`github/spec-kit`, `bradygaster/squad`, `obra/superpowers`) is the **tool**,
  not a reference. Choose the three for **variance, not similarity**; one repo cannot show variance, and
  variance is the entire point.
- **Confirm the marker before searching for adopters.** Discovery is two-pass — you cannot search for a marker
  you have not confirmed. Scaffold the framework (`specify init`, `squad init`, …) or read its current docs
  first, *then* search public repos by the confirmed marker.
- **A shortfall is a finding; a silent shortfall is a defect.** Where fewer than three qualifying repos exist,
  record the query, its result count and the substitute used, and carry the reduced confidence forward as a
  declared limit on that framework's page (NFR8). Never pad the corpus with repos that do not qualify — a
  false-positive reference repo validates a detection heuristic against noise.
- **Corpus repos are dev-time references, never a test dependency.** CI has no clone. Every shape they reveal
  becomes a temp-directory fixture, the way `GsdCoreArtifactAdapterTests` derives from CORA without reading it.
- **Verify expected values, not just absence of errors.** The coverage story writes an expected-versus-actual
  record per corpus repo — page count, epic/story counts, status distribution, coverage tiers, diagnostics —
  and *explains* every difference rather than merely observing it.

### Seed `CONDITIONAL_CLASSES` for any cross-framework markup

**The documented CSS regeneration order above cannot save markup that only a non-BMad repo produces.**
`extract:ir-content` prunes any rule whose selector is absent from the IR, and the extraction corpus is **this
repository's own IR — and this repository is a BMad project**. So no harvest run here can ever see markup that
requires a different framework's shape.

Measured on Story 12.2 (§F1), not theorised: with the stylesheet edit in place and the regeneration order
followed exactly, **all five `.milestone-band*` rules were pruned and `check:ir-content` stayed GREEN** — the
bands would have shipped unstyled on a real GSD site with no gate able to see it. Seed
`web/scripts/ir-content-lib.mjs`'s `CONDITIONAL_CLASSES` (the existing seam, also used for the sunburst
black-fill and `owner-author-2` incidents) and pin it in `web/test/ir-content-harvest.test.mjs` — the
round-trip gate structurally cannot check its own derivation. **Every framework epic hits this.**

## Analysis observations — read the digest for files you are about to touch

`.specscribe/analysis/` holds this repository's current SonarCloud findings as
[ADR 0023](docs/adrs/0023-agent-facing-analysis-observation-contract.md) `AnalysisObservation`
records. It is gitignored, dev-time only, and refreshed by hand:

```sh
node tools/analysis-digest/index.mjs
```

**How to read it — do not read the whole thing.**

1. **Go straight to the shard for a file you are about to touch.** The path is derivable:
   `src/SpecScribe/Charts.cs` → `.specscribe/analysis/files/src/SpecScribe/Charts.cs.json`.
   No shard means no open observations on that file. A shard carries the full provenance
   block, so it is safe to read on its own.
2. **`index.json` is for the repo-wide view only** — totals, and which files have
   observations. It is ~31 KB; the median shard is ~4 KB. Reading three shards costs
   ~13 KB. Reading everything costs 1.34 MB. Read shards.
3. Project-level observations with no file live in `unlocated.json`.

**Absent means UNKNOWN, never clean.** No `.specscribe/analysis/` means the digest was never
generated or the fetch failed — the emitter deliberately leaves the old digest alone and
writes nothing rather than emitting an empty one, because an empty digest reads as "this code
is clean". A digest that exists with zero observations for a file *is* a real "clean" answer;
a missing digest is not.

**Staleness — check it before you trust a line number.** Every shard and the index carry a
`provenance` block:

- **The read-time rule, which overrides everything else:** if `git rev-parse HEAD` differs
  from `provenance.evaluatedAtRevision`, the digest is stale **regardless of what `isStale`
  says**. `isStale` was frozen when the digest was written and ages into a lie on the next
  commit. Re-run the emitter.
- `isStale` **fails closed** — it is `true` whenever staleness cannot be computed.
  `staleReasons` says which of `analysis-behind-working-tree`, `working-tree-dirty`,
  `commits-behind-not-computable`, or `analysis-revision-unknown` applies.
- `workingTreeDirty: true` is itself a staleness condition: line numbers are anchored to
  `analysisRevision`, and uncommitted edits move them. Given § Concurrent work above, expect
  this to be true most of the time — treat cited lines as approximate and confirm by symbol.
- Staleness is **revision-first**. `analysisDate` can read "an hour ago" while the revision
  is commits behind; only the revision is honest.

**What the digest does not tell you.** `attachment.basis` is `"unavailable"` on every record:
the code→planning join is not computed here (it needs `--deep-git` and its fan-out bounding
rule is Story 26.5's). The digest is file-keyed, which is enough — you already know your files.
Severity is SARIF's four-level scale, so Sonar's single `BLOCKER` is indistinguishable from
`HIGH` at `severity.normalized`; read `severity.provider` if that distinction matters.

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
