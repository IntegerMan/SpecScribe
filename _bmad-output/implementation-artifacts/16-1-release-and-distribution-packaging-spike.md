---
baseline_commit: 7ff3b13 # HEAD at authoring time (2026-08-06). Verify before citing a line number — shared main.
epic: 16
frs: [FR32, FR33, FR34] # release engineering · VSIX/Marketplace publication · release-facing docs + versioning
nfrs: [NFR9] # "Release builds are reproducible and produced by CI from a clean checkout; publishing to any
             # distribution channel is gated on a passing build + test run." (epics.md:138)
decides: docs/adrs/00NN-release-channels-and-versioning-policy.md # NEW ADR — this spike DECIDES and must RATIFY.
                                                                 # `docs/adrs/` ends at 0038 on disk; 0019 is
                                                                 # claimed-but-unwritten by Story 18.3, so 0039
                                                                 # is the likely next free number. VERIFY at
                                                                 # authoring time — claims move on shared main.
depends_on: [] # No gates. ADR 0006 (Accepted) and ADR 0022 (Proposed) supply the inputs and already exist.
blocks: [16-2, 16-3, 16-4, 16-5, 16-6, 16-7, 16-8, 16-9] # every other story in the epic
informs: [17-4] # release-readiness sign-off inherits this spike's preview promises + known-limitations frame
ships_product_code: false # THROWAWAY / DECISION-ONLY spike. No `src/`, no `tests/`, no `web/`, no `extension/`.
                          # `npm run check:parity` and the C# suite MUST NOT move.
timebox: ~2 days
deliverables:
  - "_bmad-output/implementation-artifacts/16-1-spike-report.md"
  - "docs/adrs/00NN-release-channels-and-versioning-policy.md (RATIFIED by the owner, not merely drafted — AC #4)"
  - "one line in docs/adrs/README.md"
  - "spike/release/** (OPTIONAL, only if AC #5 needs a disposable pack/install probe; quarantined per spike/README.md)"
---

# Story 16.1: SPIKE — Release & Distribution Packaging

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer preparing a community preview,
I want the distribution channels, versioning policy, and publishing prerequisites decided and written down before release stories begin,
So that packaging work starts with an agreed scope and no surprise blockers.

| | |
|---|---|
| **This spike does** | Decide **which channels ship in the preview cut** and in what order. Decide **how the renderer artefact rides inside a package** — the one unanswered mechanical question the rest of the epic is built on, verified by a real pack + install, not by reasoning. Inventory every credential/account/reservation with its **current** 2026 mechanism. Decide the versioning + pre-release scheme, the changelog format, and what "preview" promises. Author a **ratified** ADR. |
| **This spike does NOT** | Ship product code. Build a release workflow (16.4). Pack or publish anything to a real feed (16.3/16.8). Create accounts, reserve package IDs, or store secrets — those are **owner actions this spike inventories** (see § Owner actions). Touch `src/**`, `tests/**`, `web/**`, `extension/**`. |

**Discipline:** decision-first, timeboxed, throwaway — same as Stories 6.3, 6.6, 20.1, 20.4, 22.1, 23.1, 24.6, 25.3.
Suggested timebox **2 days**. If one axis eats the box, finish that axis and report the rest as *undecided*
rather than half-deciding all of them. **AC #5 is the axis to protect** — it is the only one where an answer
cannot be reasoned to and every downstream story depends on it.

---

## ⛔ Read first — eleven reconciliations against shipped code, live registries, and prior ADRs

Each one changes what you would otherwise decide, measure, or write. Every code reference was verified at
`7ff3b13` on 2026-08-06; every registry fact was read live the same day.

### R1 — Most of AC #1 is **already decided**. Do not re-litigate it; record what is settled and spend the box on what is not.

`sprint-status.yaml:301-304` says this outright: *"16.1's channel decision is now largely pre-answered by ADR
0006; 16.1 executes the packaging mechanics on that basis."* Two ADRs supply the inputs.

| Question | Status | Where |
|---|---|---|
| Does the CLI ship as a `dotnet` global tool? | **Settled — yes.** Already wired: `PackAsTool`/`ToolCommandName`/`PackageId` | `SpecScribe.csproj:14-16` |
| Does the CLI ship as self-contained per-OS binaries? | **Settled — yes**, re-affirmed from ADR 0005; ~73 MiB / RID, 34 MB gzipped | ADR 0006 § Decision, § Comparison |
| Is npx a channel? | **Settled — yes, first-class**, proven end-to-end (1,558-byte wrapper, 196 files, no .NET present) | ADR 0006 § Decision 2; Story 16.8 |
| Is the VSIX a channel? | **Settled — yes**, FR33, blocked on Epic 6 | epics.md § Story 16.5 |
| Does Node ship inside any package? | **Settled — no.** Node is a build-time toolchain and a generate-time *prerequisite* | ADR 0022 § Decision 1-2 |
| Does the standalone binary require Node? | **Settled — yes, documented prerequisite** (owner decision 2026-07-27); does not bundle a JS runtime, does not silently degrade | ADR 0022 § Decision 5 |
| **Which channels are in the *preview cut*, and in what order?** | **OPEN — yours** | — |
| **How does the renderer artefact get inside a package?** | **OPEN — yours, and it is the load-bearing one** | R2 |
| **Non-goals** | **OPEN — yours.** AC #1 demands them explicitly | — |

**Write the settled column as inherited fact with citations, not as fresh analysis.** A spike report that
re-derives ADR 0006's comparison table has spent its box proving something already ratified.

### R2 — The renderer must ship *inside* the package, and **nothing has decided the shape**. This is the spike's centre of gravity.

`NuxtPrerender.ResolveArtefactDirectory` (`src/SpecScribe/NuxtPrerender.cs:73`) probes three locations in order,
and its doc comment names the second one for you:

```
/// <item><c>renderer/</c> beside the executing assembly — the Epic 16 packaging shape.</item>
```
— `src/SpecScribe/NuxtPrerender.cs:68`

**The resolution logic exists. Nothing populates it.** `SpecScribe.csproj:55-76` packs `README.md` and embeds
seven assets as `EmbeddedResource`; it packs **no `renderer/` payload at all**. Note that the embedding trick
does not transfer — the artefact is 185 files that Node must `import` from disk, so it has to be *packed as
content*, not embedded in the assembly. Every consequence downstream flows from that gap:

- `README.md:132-141` tells any external user to set `SPECSCRIBE_RENDERER_DIR` by hand and points at
  *SpecScribe's own clone* for the artefact.
- Story 16.9's epics.md entry states the dependency precisely: *"DEPENDS ON STORY 16.3, AND ON ONE SPECIFIC
  THING WITHIN IT: the renderer artefact being IN the published package … Until it does, this Action can only
  build from source and inherits the whole toolchain; after it does, the Action collapses to install-and-run."*
- Story 23.5 § 10 open item 4 assigns *"`npm run build:package` stage in the release pipeline"* to **Stories
  16.1 / 16.4**. This is that.

**The three channels need three answers, and they are not the same answer.**

| channel | what "beside the executable" resolves to | the question |
|---|---|---|
| `dotnet tool` (NuGet) | the shim's real assembly under `~/.dotnet/tools/.store/…/tools/<tfm>/any/` | does a `renderer/**` payload packed under `tools/<tfm>/any/` land there, and does `AppContext.BaseDirectory` point at it? |
| self-contained binary | the publish directory beside the single-file exe | does `PublishSingleFile` extraction change `AppContext.BaseDirectory` such that a sibling `renderer/` is still found? |
| npm wrapper (16.8) | wherever the platform package unpacked the binary | does the per-RID platform package carry `renderer/` too, or is it shared? |

**Hypothesis to verify, not to assert.** A `dotnet tool` nupkg unpacks to
`~/.dotnet/tools/.store/<id>/<version>/<id>/<version>/tools/<tfm>/<rid>/` with a PATH shim; `AppContext.BaseDirectory`
should therefore point inside that store path, so a `renderer/` folder packed alongside the assembly *should*
resolve. **Prove it with a real pack + install**, because the failure mode if it does not is a tool that resolves
nothing and tells the user to build a Nuxt artefact:

```sh
cd web && npm run build:package && cd ..
# add a temporary <None Include="..\..\web\.output\**" Pack="true" PackagePath="tools\net10.0\any\renderer\" />
dotnet pack src/SpecScribe -c Release -o artifacts
dotnet tool install SpecScribe --version <v> --tool-path ./probe-tools --add-source ./artifacts
# then, from a DIFFERENT repository with no web/ directory and SPECSCRIBE_RENDERER_DIR unset:
./probe-tools/specscribe generate --output probe-out
```

Success is `errors=0` and a populated `probe-out`. **Run it from a different repository** — running it inside
this one lets the third candidate (`web/.output/` at the repo root) succeed and reports a false pass, the same
class of wrong-answer-with-a-success-status that Story 23.5 hit when Nitro served a baked project's pages.

Record the **measured package size** too: the artefact is ~3.78 MB (185 files) per Story 23.5, and a `dotnet tool`
nupkg that grows by that much is a fact the channel decision needs. Any temporary csproj edit made for this probe
is **reverted before the story closes** — `ships_product_code: false`.

### R3 — AC #2's answer has changed. **Trusted Publishing means the NuGet and npm channels need no stored secret at all.**

The seeded AC says *"inventories every required secret/credential (NuGet API key, VS Marketplace publisher +
PAT), where each is stored as a repository/environment secret."* That framing is a long-lived-token framing and
it is out of date for two of the three channels. Verified live 2026-08-06:

| channel | 2026 mechanism | what is actually stored |
|---|---|---|
| **nuget.org** | **Trusted Publishing.** `NuGet/login@v1` exchanges a GitHub OIDC token for a **short-lived API key (1 hour, single-use exchange)**. Needs `permissions: id-token: write` and a trusted-publishing policy configured on nuget.org. | **no secret.** A policy on nuget.org, not a repo secret. |
| **npm** (Story 16.8) | **Trusted Publishing**, GA 2025-07-31. Requires **npm CLI ≥ 11.5.1** and `id-token: write`. Publishes **provenance attestations by default** (`--provenance` no longer needed). **Do NOT set `NODE_AUTH_TOKEN`** — npm falls back to the legacy token path if you do. Policies created after **2026-05-20** must explicitly select at least one allowed action. | **no secret.** |
| **VS Marketplace** (Story 16.5) | ⚠️ **Dated.** Azure DevOps retires global PATs on **2026-12-01** — roughly four months from today. `@vscode/vsce` ≥ 3.9.2 supports `--azure-credential` (Entra app registration + GitHub federated credential + the identity added as a publisher member), but `microsoft/vscode-vsce#1023` reports federated service principals failing publish with *"You need to be logged in with your corporate credentials"* on a **personally-owned publisher**, closed as *not planned*. | PAT today; **the migration is a live, dated risk** |

**What this changes about the deliverable.** The inventory is no longer a list of secret names — it is a list of
**one-time owner configurations** plus **one genuinely open credential question**. Say plainly whether the
VS Marketplace path is (a) PAT now with a dated migration item seated against Story 16.5, (b) an
organization-owned publisher from the start to sidestep the personal-publisher mismatch, or (c) the VSIX drops
out of the preview cut. **All three are defensible; picking none is not.** AC #2's "no secret value is committed"
clause is satisfied trivially by trusted publishing and must still be stated.

**Also decide, because ADR 0022 § Decision 5 leaves it open** (23.5 § 10 open item 5, owner-assigned): whether the
npx channel checks the Node prerequisite at install time.

### R4 — Both package IDs are **unclaimed as of 2026-08-06** — verified, and that is a risk, not a comfort.

- `https://api.nuget.org/v3/registration5-gz-semver2/specscribe/index.json` → **HTTP 404**
- `https://registry.npmjs.org/specscribe` → **HTTP 404**

ADR 0022 § Decision 5 already noted `nuget.org/packages/SpecScribe` → 404 on 2026-07-27; it is still true.
An unreserved ID on a public project with a public roadmap is squattable, and the entire channel decision names
those two strings. **Reserving them is an owner action** (see § Owner actions) — the dev agent must not create
accounts or push placeholder packages. Record it as a **dated, prioritized prerequisite**, and record the
fallback ID for each registry if either is taken before the reservation happens.

Note also that the npm ID interacts with 16.8's `optionalDependencies` shape: the wrapper needs the base name
**plus** a per-RID platform-package name per registry (the esbuild/Biome pattern). Enumerate the full name set,
not just the base name.

### R5 — Node detection **already exists**. 23.5's open item 3 is *partly* stale — and the code says exactly which part.

23.5 § 10 open item 3 assigns *"Node detection + actionable error in the standalone binary"* to Story 16.3. The
mechanism shipped with Story 23.6:

- `NuxtPrerender.SupportedNodeRange = "^22.19.0 || ^24.11.0 || >=26.0.0"` — `src/SpecScribe/NuxtPrerender.cs:41`
- version assertion + actionable message — `src/SpecScribe/NuxtPrerender.cs:139-216`
- a missing/invalid `SPECSCRIBE_RENDERER_DIR` is a **hard error, never a fallback** — `NuxtPrerender.cs:80-98`

**Do not rewrite that mechanism.** But read its own doc comment before you call the item closed
(`NuxtPrerender.cs:142-144`):

> *"ADR 0022 §Decision 5 assigned Node DETECTION to Story 16.3, which has not been built — every `16-*` key is
> still backlog. Until it is, this is the check…"*

So two things genuinely remain, and both are yours to route rather than to build:

1. **Placement.** ADR 0022 § Decision 5 words it as *"The binary detects Node **at startup**"*. Today the check
   runs at **prerender time**, inside `NuxtPrerender`. That is a real difference for the standalone-binary
   channel — a user gets the actionable message after ingest rather than immediately. Decide whether the ADR's
   wording or the shipped placement is the one that stands, and say so.
2. **Surfaces.** The prerequisite is stated in `README.md:92-98` and nowhere a *packaged* consumer sees it — not
   the NuGet listing, not an npm README, not the Marketplace listing. That is Story 16.6's, not 16.3's.

### R6 — NFR9 says "reproducible". **Today it is not, in three specific ways.** Scope it; do not assume it.

Story 23.5 § 10 open item 6 recorded this as *"unowned; named so it is inherited knowingly"* — **this spike is
where it gets an owner.** The three gaps:

1. **`<Version>0.1.0-preview` is a hand-edited literal** (`SpecScribe.csproj:19`). Story 16.3 AC #1 requires the
   version to derive **from the release tag**. Decide the mechanism (MinVer / Nerdbank.GitVersioning / plain
   `-p:Version=` from the tag) — that choice is a versioning-policy decision, which is AC #3's, not 16.3's.
2. **No workflow sets `SOURCE_DATE_EPOCH`.** The csproj already honors it for the `BuildDate` stamp
   (`SpecScribe.csproj:33-42`) — a correctly-built escape hatch with nothing calling it.
3. **No `<Deterministic>` / `ContinuousIntegrationBuild` property** anywhere in the project.

State honestly which of the three the preview actually needs. "Reproducible" can mean *built from a clean
checkout by CI* (which `build-test-analyze.yml` already achieves) or *byte-identical rebuilds of the same commit*
(which needs all three). **NFR9's wording supports the weaker reading; say which one you are claiming** rather
than letting a reader assume the stronger one.

### R7 — Four version numbers already exist and **can drift**. The policy must cover all four, not just the CLI.

| where | value | note |
|---|---|---|
| `src/SpecScribe/SpecScribe.csproj:19` | `0.1.0-preview` | hand-edited; drives the About page's Preview badge |
| `extension/package.json:5` | `0.1.0` | **no pre-release label** — VS Marketplace has no semver pre-release concept, only a "Preview" flag |
| `README.md:260` | `0.1.0-preview` | a **hard-coded literal** inside the published CI recipe |
| npm wrapper + per-RID platform packages (16.8) | does not exist yet | `optionalDependencies` pins must match exactly |

`AboutTemplater.ParseInformationalVersion` (`src/SpecScribe/AboutTemplater.cs:90`) splits the informational
version into semver + commit hash and `AboutTemplater.cs:133-135` renders a `Preview` badge **whenever the
version is a pre-release**. So the pre-release label is not cosmetic — it is a rendered, user-visible product
surface, and a policy that drops `-preview` silently removes that badge.

Story 16.9 AC #2 raises the stakes: *"it pins both halves together as one released unit, so a consumer cannot
combine a CLI and a renderer from different revisions … since a portal that renders from a mismatched pair fails
as wrong output rather than as an error."* **The versioning policy must state how the CLI and its renderer are
pinned as one unit** — that is a versioning decision, and 16.9 inherits whatever this spike says.

### R8 — The CI gate **already exists**. 16.2 extends it. Get the required-check string right.

`.github/workflows/build-test-analyze.yml` is the repository's first build+test gate (Story 25.1, NFR11). Its
header comment gives Story 16.2 exactly what it needs, verbatim:

- required-check string = the **job name**: `build-test-analyze`
- `portability-probe (ubuntu, non-gating)` carries `continue-on-error` at the **job** level and **must NOT** be
  made required
- the two SonarScanner steps carry `continue-on-error` deliberately; **Build and Test do not**
- epics.md § Story 16.2 (AMENDED 2026-07-25): *"Do NOT create a second build+test workflow"*

NFR9's *"publishing … is gated on a passing build + test run"* is a **release-pipeline** property (16.4), and the
release pipeline is tag-triggered while this workflow is push/PR-triggered. Say how the gate applies to a tag
push — re-run build+test in the release job, or require the tagged commit to already be green on `main`. Those
are different guarantees and different failure modes.

### R9 — `extension/package.json` is not Marketplace-ready. Inventory it; do not fix it here.

`"private": true` (line 6), `"categories": ["Other"]` (lines 12-14), `"publisher": "specscribe"` (line 7 —
unverified, the publisher ID may or may not be registered), and `extension/README.md:190-192` states outright
*"Packaging/publish is Story 16.5's job … nothing here publishes"*. Story 16.5's epics.md entry additionally owns R1.4 (walkthroughs),
R1.6 (Marketplace metadata polish) and R8.2 (platform-specific VSIX targets), and names a **prerequisite**: the
Workspace-Trust posture (R5.4) in Story 6.8 must be in place first — it is a Marketplace review-bar item.

Confirm whether `"private": true` interferes with `vsce package`/`vsce publish` and record the answer; do not
edit the manifest in this story.

### R10 — `.specscribe/analysis/` does not exist in this tree. That means **UNKNOWN, never clean**.

Per CLAUDE.md, a missing digest means the emitter never ran or the fetch failed — not that the files are clean.
This spike is documentation-only, so no shard is required; if you end up touching a `.cs` or `.csproj` file,
run `node tools/analysis-digest/index.mjs` first and read the shard for that path.

### R11 — Shared `main`, and `sprint-status.yaml` is the highest-traffic file in the repository.

Per CLAUDE.md § Concurrent work: verify after every edit, never `git reset --hard` / `git checkout --` /
`git clean`, and expect a gate to move under you. This story writes **no code**, so the drift gates
(`check:parity`, `check:ir-content`, `check:tokens`, `check:assets`) must be **unchanged** at the end — that is
AC #6, and a moved gate here means somebody else's change, not yours. Line numbers cited above were read at
`7ff3b13`; **re-read before citing them in the report.**

---

## Acceptance Criteria

**AC #1-#3 are verbatim from `epics.md` § Story 16.1. AC #4-#6 are additions made at create-story time**, each
with its reason stated — #4 because CLAUDE.md requires an ADR for cross-cutting decisions and a spike whose
output lives only in a story file buries it; #5 because R2's question is the epic's actual blocker and AC #1's
"chosen channel(s) … with rationale" cannot be answered honestly without it; #6 because this is a declared
no-product-code spike and that needs a checkable assertion.

1.
**Given** the CLI can ship via multiple channels
**When** the spike evaluates them
**Then** a written decision records the chosen CLI channel(s) — NuGet `dotnet` global tool (already wired in SpecScribe.csproj) and/or self-contained per-OS binaries — with rationale and explicit non-goals.

2.
**Given** publishing requires accounts and secrets
**When** the spike documents prerequisites
**Then** it inventories every required secret/credential (NuGet API key, VS Marketplace publisher + PAT), where each is stored as a repository/environment secret, and any code-signing decision
**And** no secret value is committed to the repository.

3.
**Given** a preview release differs from a stable one
**When** the spike defines policy
**Then** it records the versioning + pre-release scheme (for example `0.x` / `-preview` tags), the changelog format, and what "preview" promises and does not promise to consumers.

4. *(added)*
**Given** channel selection, credential posture and versioning are cross-cutting contracts that Stories 16.2-16.9 and 17.4 all build on
**When** this spike concludes
**Then** the decision is recorded as an **ADR under `docs/adrs/`**, indexed in `docs/adrs/README.md`, and **ratified by the owner** — not left as `Proposed` and not buried as prose in this story file
**And** where it amends ADR 0006 or ADR 0022 it says so explicitly, in the shape ADR 0022 § "Relationship to ADR 0006 — this AMENDS it" already models.

5. *(added)*
**Given** `NuxtPrerender.ResolveArtefactDirectory` probes `renderer/` beside the executable and calls it "the Epic 16 packaging shape" while nothing populates it
**When** the spike decides the packaging shape
**Then** it is **verified empirically** — a real `dotnet pack` + `dotnet tool install --tool-path` + a `generate` run **from a repository other than this one**, with `SPECSCRIBE_RENDERER_DIR` unset — and the report states the measured package size delta and the observed `AppContext.BaseDirectory`
**And** the answer is given **per channel** (global tool / self-contained binary / npm platform package), since "beside the executable" resolves differently in each
**And** any temporary `.csproj` edit made for the probe is reverted, leaving the working tree free of product-code changes.

6. *(added)*
**Given** this is a decision-only spike
**When** it closes
**Then** no file under `src/`, `tests/`, `web/` or `extension/` appears in the File List
**And** `dotnet test SpecScribe.slnx` and `cd web && npm run check` are both green and **unchanged** from the pre-story baseline, with any movement attributed to a concurrent session by name rather than absorbed.

---

## Tasks / Subtasks

- [ ] **Task 1 — Inherit before deciding (AC: #1)**
  - [ ] Read ADR 0006 § Decision + § Comparison and ADR 0022 § Decision 1-7 and § Relationship to ADR 0006. Build the settled-vs-open table from R1 **with citations**; do not re-derive the measurements.
  - [ ] Read Story 23.5's report (`23-5-packaging-strategy-report.md`) § 4 (strategy comparison), § 4 "Per channel", and § 10 (open items 3-6). Four of its six open items are this epic's.
  - [ ] Confirm each cited line number still resolves at your HEAD (shared `main`; R11).

- [ ] **Task 2 — Decide the renderer packaging shape, empirically (AC: #1, #5)** ← *protect this one*
  - [ ] Build the artefact: `cd web && SPECSCRIBE_PACKAGE_BUILD=1 npm ci && npm run sync:assets && npm run build:package`. Use `build:package`, **never** `build` (`README.md:125-127` says why: a plain `nuxt build` bakes this project's pages into `.output/public` and Nitro serves them ahead of the SSR route — HTTP 200, wrong project).
  - [ ] Add a **temporary** `<None Include="..\..\web\.output\**" Pack="true" PackagePath="tools\net10.0\any\renderer\" />` to `SpecScribe.csproj`, `dotnet pack -c Release -o artifacts`, and inspect the nupkg layout.
  - [ ] `dotnet tool install SpecScribe --tool-path ./probe-tools --add-source ./artifacts`, then run `generate` **from a different repository** with `SPECSCRIBE_RENDERER_DIR` unset. Record `errors=`, the page count, and the resolved artefact path.
  - [ ] Record the nupkg size before and after the payload (~3.78 MB / 185 files expected, per 23.5).
  - [ ] Answer the self-contained-binary case: does `PublishSingleFile` extraction move `AppContext.BaseDirectory` away from a sibling `renderer/`? Measure or, if the box is tight, state it as **unmeasured** and seat it against 16.3.
  - [ ] Answer the npm case: does each per-RID platform package carry its own `renderer/` (multiplying ~3.78 MB across RIDs) or is it shared? This is 16.8's cost driver.
  - [ ] **Revert the csproj edit.** Confirm with `git status` that no file under `src/` is modified by you.

- [ ] **Task 3 — Choose the preview cut and the non-goals (AC: #1)**
  - [ ] Decide which channels ship in the **first preview** and in what order, given: 16.5 is blocked on Epic 6, 16.9 is blocked on 16.3's renderer payload, and Epic 17's sign-off (Story 17.4) gates the cut.
  - [ ] Write the **explicit non-goals** AC #1 demands. Candidates to rule in or out by name: stable/1.0, Homebrew/winget/Chocolatey/Scoop, a container image (recorded but deliberately unseated in epics.md § Story 16.9), Open VSX alongside the VS Marketplace, and per-RID matrix breadth.
  - [ ] State the **RID matrix** for the self-contained binaries — each RID is ~73 MiB / 34 MB gzipped (ADR 0006), so the matrix is a real cost decision and 16.8's `optionalDependencies` shape follows from it.

- [ ] **Task 4 — Credential and prerequisite inventory (AC: #2)**
  - [ ] Re-verify R3's three mechanisms live before writing them down — this area moved twice in the last year.
  - [ ] Record, per channel: the account/identity needed, the mechanism (trusted publishing vs. stored token), what is stored **where** (repository secret, environment secret, or *nothing*), and who can rotate it.
  - [ ] Decide the VS Marketplace credential path against the **2026-12-01** PAT retirement (R3). Name the option chosen and seat the migration explicitly against Story 16.5 if it is deferred.
  - [ ] Decide the **npx install-time Node check** (ADR 0022 § Decision 5 / 23.5 open item 5, owner-assigned).
  - [ ] Record the **code-signing decision** AC #2 requires — Authenticode for Windows binaries, notarization for macOS, or explicitly neither for the preview. An unsigned single-file exe has real SmartScreen/AV consequences; ADR 0022 § Alternatives already flags the dropper-heuristic issue for a related shape. Say what preview users will see.
  - [ ] Record the package-ID reservations as **owner actions** with today's verified 404s as evidence (R4), plus the fallback ID per registry.
  - [ ] Assert AC #2's "no secret value is committed" — and note that trusted publishing makes it structural for two of three channels rather than a matter of discipline.

- [ ] **Task 5 — Versioning, changelog, and preview promises (AC: #3)**
  - [ ] Decide the scheme: `0.x` + `-preview` (today's shape) or an alternative, and how a pre-release tag maps to each channel's version string — including the VS Marketplace, which has **no semver pre-release concept**, only a Preview flag (R7).
  - [ ] Decide how `<Version>` derives from the tag (MinVer / Nerdbank.GitVersioning / `-p:Version=`), which is 16.3 AC #1's requirement and this AC's decision.
  - [ ] State how **the CLI and its renderer are pinned as one released unit** (Story 16.9 AC #2 depends on this).
  - [ ] Decide the changelog format (Keep a Changelog vs. generated release notes), where `CHANGELOG.md` lives — **it does not exist yet** — and who updates it. Story 16.6 AC #1 requires "a `CHANGELOG.md` following the Story 16.1 format".
  - [ ] Write what "preview" **promises and does not promise**: breaking-change policy inside `0.x`, support expectations, data/output-format stability (the IR is versioned — ADR 0008/ADR 0034), and the Node prerequisite as a stated consumer-facing condition.
  - [ ] Scope **NFR9 reproducibility** per R6: name which of the three gaps the preview closes, which it defers, and to which story. Say which reading of "reproducible" you are claiming.
  - [ ] Name the four existing version numbers (R7) and state what happens to each.

- [ ] **Task 6 — Author and ratify the ADR (AC: #4)**
  - [ ] Verify the next free ADR number at authoring time (`docs/adrs/` ends at 0038; 0019 is claimed-but-unwritten by Story 18.3, so 0039 is likely — **confirm, do not assume**).
  - [ ] Author `docs/adrs/00NN-release-channels-and-versioning-policy.md` in the house shape: Status / Date / Deciders / Amends, Context, Options, Decision, Consequences (positive **and** negative), References.
  - [ ] State explicitly what it amends. ADR 0022 § Decision 5's channel table and ADR 0006 § Decision's channel list are both in scope.
  - [ ] Add one line to `docs/adrs/README.md` in the existing format.
  - [ ] **Take it to the owner and ratify it.** `Proposed` does not satisfy AC #4.

- [ ] **Task 7 — Write the spike report (AC: #1-#5)**
  - [ ] `_bmad-output/implementation-artifacts/16-1-spike-report.md`, following the shape of `23-5-packaging-strategy-report.md`: a **Verdict** up front, a **method-and-provenance table** marking each figure as harness-derived / session-measured / read-from-registry, then the axes.
  - [ ] Record anything that came out **wrong first** and what it taught — 23.5 § 2 does this and it is the most useful section in that report.
  - [ ] Close with an **open-items table with named owners**, in 23.5 § 10's shape. Anything you could not measure goes here as *unmeasured*, not as an estimate.

- [ ] **Task 8 — Sequence the rest of the epic and record it (AC: #1, #4)**
  - [ ] Note per story what this spike unblocks or changes: 16.2 (R8's required-check string + how the gate applies to a tag), 16.3 (packaging shape + version-from-tag; **not** Node detection — R5), 16.4 (build:package stage; 23.5 open item 4), 16.5 (credential path + the 2026-12-01 deadline + the Story 6.8 prerequisite), 16.6 (docs surfaces for the Node prerequisite + changelog format), 16.8 (RID matrix, platform-package names, install-time check), 16.9 (renderer-in-package + the CLI/renderer pinning rule).
  - [ ] Per CLAUDE.md: **a structural scope change lands in `epics.md` AND `sprint-status.yaml` in the same change.** If your decision adds, removes, or re-sequences a story, edit both. If it only refines ACs within existing stories, say so and edit neither.
  - [ ] Update `sprint-status.yaml`: `epic-16` → `in-progress`, `16-1-…` → `review` when done, `last_updated`.

- [ ] **Task 9 — Scope guard (AC: #6)**
  - [ ] `git status` shows no modification under `src/`, `tests/`, `web/`, `extension/` **attributable to you**. Concurrent sessions may show their own — name them rather than reverting them (CLAUDE.md forbids `git checkout --`).
  - [ ] `dotnet test SpecScribe.slnx` green.
  - [ ] `cd web && npm run check` green (`check:tokens`, `check:ir-content`, `check:assets`, `check:parity`).
  - [ ] If a gate moved: **establish causality before touching any baseline.** You changed no rendering code, so a moved gate is somebody else's. Bisect in a throwaway tree (`git archive HEAD` into the scratchpad), never by resetting the shared tree. Stories 18.2, 18.4 and 18.6 each did this and each proved the move was someone else's.

---

## Owner actions (not the dev agent's)

These are **outward-facing and irreversible-ish**. The spike **inventories and recommends** them; it does not
perform them. Surface them to the owner as a numbered list at the end of the report.

1. Reserve `SpecScribe` on nuget.org and `specscribe` (+ per-RID platform names) on npmjs.com — both verified
   unclaimed 2026-08-06 (R4).
2. Configure the nuget.org trusted-publishing policy (repository + workflow + optional environment).
3. Configure the npm trusted-publishing policy, explicitly selecting allowed actions (required post-2026-05-20).
4. Register / verify the VS Marketplace publisher, and decide personal vs. organization ownership **before**
   `--azure-credential` is wired (R3).
5. Ratify the ADR (AC #4).
6. Any code-signing certificate acquisition, if Task 4 decides signing is in scope for the preview.

---

## Dev Notes

### What this story touches

**All-new files:**

- `_bmad-output/implementation-artifacts/16-1-spike-report.md`
- `docs/adrs/00NN-release-channels-and-versioning-policy.md`
- `spike/release/**` — *only if* Task 2 needs a disposable probe script. Quarantined per `spike/README.md`: no
  `.slnx` reference, not part of `dotnet pack`, contributes no rendering path. Delete-able.

**Updated files:**

- `docs/adrs/README.md` — one index line
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — `epic-16` → `in-progress`, story status,
  `last_updated`
- `_bmad-output/planning-artifacts/epics.md` — **only** if a structural scope change is decided (Task 8)

**Read-only, do not modify:** `src/SpecScribe/SpecScribe.csproj` (except the reverted probe edit),
`src/SpecScribe/NuxtPrerender.cs`, `extension/package.json`, `.github/workflows/**`, `README.md`.

`README.md` is deliberately excluded: its install and CI-recipe sections were repaired on 2026-08-06 and are
Story 16.6's to own thereafter. If this spike's decisions make README text wrong, **record it as a Story 16.6
item**, do not fix it here — the same discipline 23.5 applied when it raised `DashboardSurface.vue` against
Story 23.3 rather than patching it.

### The current state of the packaging surface, so you do not rediscover it

| fact | where |
|---|---|
| `PackAsTool` / `ToolCommandName specscribe` / `PackageId SpecScribe` already wired | `SpecScribe.csproj:14-16` |
| `<Version>0.1.0-preview` — hand-edited literal | `SpecScribe.csproj:19` |
| `README.md` is packed into the nupkg (`PackageReadmeFile`) — so the listing already renders it | `SpecScribe.csproj:23,56` |
| MIT via `PackageLicenseExpression`; `LICENSE` at repo root | `SpecScribe.csproj:22` |
| `SOURCE_DATE_EPOCH`-aware `BuildDate` stamp, with a digit-count guard — **built and unused** | `SpecScribe.csproj:35-41` |
| Seven embedded assets (specscribe.css/.js, prism.js/.css, plotly-hierarchy, spa, webview-theme) — they survive global-tool packaging by being *embedded*, not copied. The renderer artefact **cannot** use that trick: it is 185 files that Node must `import` from disk | `SpecScribe.csproj:55-76` |
| `RollForward Major`, `net10.0` | `SpecScribe.csproj:5,10` |
| Renderer resolution order: env override → `renderer/` beside exe → `web/.output` at repo root | `NuxtPrerender.cs:66-127` |
| An explicit-but-wrong `SPECSCRIBE_RENDERER_DIR` is a **hard error**, never a fallback | `NuxtPrerender.cs:80-98` |
| Node range + assertion + actionable message | `NuxtPrerender.cs:41,141-216` |
| Preview badge is driven by the version being a pre-release | `AboutTemplater.cs:90,133-135` |
| Extension: `version 0.1.0`, `private: true`, `publisher: specscribe`, `categories: ["Other"]` | `extension/package.json:5-14` |
| Extension has no CI wiring; packaging is 16.5's | `extension/README.md:190-192` |

### Testing standards

There is nothing to unit-test — this spike ships no code. The evidence standard is Story 23.5's, and it is
higher than "I ran it once":

- **Say where every number came from.** 23.5 § 1 has a provenance table because Story 23.1's review found its
  "every number is reproducible" claim to be false. Mark each figure harness-derived, session-measured, or
  read-from-registry, and name the machine/OS for session-measured ones.
- **Prove the negative case.** AC #5's install probe must be run from a repository *without* a `web/` directory,
  or the third resolution candidate makes it a false pass.
- **Record the false starts.** 23.5 § 2 "Three things the experiment got wrong first" is the highest-value
  section in that report precisely because each wrong result looked plausible.
- **Regression floor:** `dotnet test SpecScribe.slnx` and `cd web && npm run check` green and unchanged (AC #6).
  Note that `GoldenContentFingerprint` **no longer exists** — ADR 0034 / Story 23.6 retired it with the C#
  `.html` writer; `check:parity` is its replacement. Do not cite it, and do not look for it.

### Project structure notes

- ADRs: `docs/adrs/NNNN-kebab-slug.md`, indexed in `docs/adrs/README.md`, `**Status:**` line required.
- Spike reports: `_bmad-output/implementation-artifacts/<epic>-<story>-spike-report.md` — the convention set by
  `20-4-spike-report.md`, `22-1-spike-report.md`, `23-1-spike-report.md`, `24-6-spike-report.md`,
  `25-3-spike-report.md`. (`23-5-packaging-strategy-report.md` used a descriptive name; either is acceptable,
  but the `-spike-report` form is the majority convention.)
- Throwaway code: `spike/<topic>/`, per `spike/README.md`.
- Generate to `SpecScribeOutput/` (the default) if you generate at all. **Never** `--output docs/live`.

### Anti-patterns this story exists to prevent

- **Re-deriving ADR 0006's measurements.** They are ratified. Cite them (R1).
- **Re-seating Node detection.** It shipped in 23.6 (R5).
- **Writing the credential inventory from 2024 knowledge.** Trusted publishing changed the answer for two of
  three channels, and the third has a dated deadline (R3).
- **Reasoning about `AppContext.BaseDirectory` instead of measuring it.** AC #5 exists because the whole epic
  hangs off that one behavior (R2).
- **Creating a second build+test workflow**, or requiring `portability-probe` (R8).
- **Leaving the ADR at `Proposed`.** AC #4 says ratified. ADR 0022 sat Proposed from 2026-07-27 through today
  with Story 23.6 proposing its ratification — that is exactly the drift AC #4 is written against.
- **Burying the decision in this story file** instead of an ADR (CLAUDE.md § Decision records).
- **Regenerating a drift-gate baseline** because it went red. You changed no rendering code; establish
  causality first (R11).

### Previous-story intelligence

Story 16.1 is the first story in Epic 16, so there is no `16-0`. The substantive predecessors are **Story 23.5**
(packaging reconciliation — reads as this spike's prequel and hands it four of its six open items) and **Story
23.6** (retired the C# HTML writer, making the renderer artefact mandatory rather than optional). Read both.

From 23.5's own concurrency notes, two habits worth copying: it moved its experiment onto IRs in a scratch
directory after a concurrent session wiped `SpecScribeOutput/spa/` twice mid-measurement, and it re-read every
seeded line number before use because two files had moved since seeding.

### Recent git context

`7ff3b13` (merge) ← `38507ce` *"Fix the external-project CI recipe and seat Story 16.9"* — the immediate
predecessor and the reason this spike is being run now. That commit repaired a README recipe that had been
broken since Story 23.6 and that nobody noticed, and seated 16.9 with an explicit dependency on 16.3's renderer
payload. Prior work on `main` is Epic 12 (`bafa488`, GSD Core adapter, ADR 0038) and Epic 25 flake-recording —
neither touches this surface. Worktree branches are in active use on this repository (`git worktree list` shows
two locked worktrees), so **expect sibling work in flight**.

### Latest technical information (verified 2026-08-06)

| item | fact | why it matters here |
|---|---|---|
| nuget.org Trusted Publishing | `NuGet/login@v1` exchanges a GitHub OIDC token for a **1-hour, single-use** API key; needs `permissions: id-token: write` + a policy on nuget.org | removes the stored NuGet API key entirely (AC #2) |
| npm Trusted Publishing | GA 2025-07-31; **npm CLI ≥ 11.5.1**; provenance published **by default**; must **not** set `NODE_AUTH_TOKEN`; policies created after 2026-05-20 must explicitly select allowed actions | removes the stored npm token (16.8) |
| Azure DevOps PAT retirement | global PATs retire **2026-12-01** | ~4 months of runway for the Marketplace path (16.5) |
| `@vscode/vsce` | ≥ 3.9.2 supports `--azure-credential`; `microsoft/vscode-vsce#1023` reports federated SPs failing on **personally-owned** publishers, closed *not planned* | the personal-vs-org publisher choice must be made **before** wiring (AC #2) |
| `nuget.org/packages/SpecScribe` | **404** | ID unclaimed and squattable |
| `registry.npmjs.org/specscribe` | **404** | same |
| Node support range (this project's own pin) | `^22.19.0 \|\| ^24.11.0 \|\| >=26.0.0`, pinned in `web/.nvmrc` (24.11.1) and `engines` | the consumer-facing prerequisite the policy must state |
| Nuxt | project runs Nuxt 4.5.1; Nuxt 3 EOL 2026-07-31 | toolchain is current; not a release blocker |

Sources: [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) ·
[.NET Blog — Trusted Publishing on NuGet.org](https://devblogs.microsoft.com/dotnet/enhanced-security-is-here-with-the-new-trust-publishing-on-nuget-org/) ·
[npm trusted publishers](https://docs.npmjs.com/trusted-publishers/) ·
[GitHub Changelog — npm trusted publishing GA](https://github.blog/changelog/2025-07-31-npm-trusted-publishing-with-oidc-is-generally-available/) ·
[VS Code — Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension) ·
[microsoft/vscode-vsce#1023](https://github.com/microsoft/vscode-vsce/issues/1023)

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Epic 16` — lines 2889-3161, all nine stories]
- [Source: `_bmad-output/planning-artifacts/epics.md:80-82,138` — FR32/FR33/FR34, NFR9]
- [Source: `docs/adrs/0006-delivery-architecture-and-distribution.md#Decision` — channels; § Consequences names "Distribution now maintains two channels … and a per-RID native-package matrix"]
- [Source: `docs/adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md#Decision` — §1-7, esp. §5 per-channel Node posture and § "Relationship to ADR 0006 — this AMENDS it"]
- [Source: `docs/adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md` — why no C# path writes HTML, which is why the renderer must ship]
- [Source: `docs/adrs/0033-content-drift-gates-are-targeted-and-regenerable.md` — governs any new gate; §4 cross-OS determinism proof]
- [Source: `_bmad-output/implementation-artifacts/23-5-packaging-strategy-report.md` — §1 provenance table, §2 experiment + false starts, §4 strategy comparison + per-channel table, §10 open items 3-6]
- [Source: `_bmad-output/implementation-artifacts/23-5-packaging-reconciliation-node-build-step.md` — the story behind that report]
- [Source: `src/SpecScribe/NuxtPrerender.cs:41,66-127,139-216` — Node range, artefact resolution, assertions]
- [Source: `src/SpecScribe/SpecScribe.csproj:5-76` — tool packaging, version, SOURCE_DATE_EPOCH stamp, embedded assets]
- [Source: `src/SpecScribe/AboutTemplater.cs:61,90,133-135` — informational-version parsing and the Preview badge]
- [Source: `.github/workflows/build-test-analyze.yml:1-13,41-42` — the gate, the required-check job name, 16.2's instructions]
- [Source: `.github/workflows/build-test-analyze.yml` § portability-probe — non-gating, must not be required]
- [Source: `README.md:87-141` — install, prerequisites, the renderer/CLI two-halves problem, `SPECSCRIBE_RENDERER_DIR`]
- [Source: `README.md:184-290` — the external-project CI recipe Story 16.9 replaces; `0.1.0-preview` literal at :260]
- [Source: `extension/package.json:6-13` and `extension/README.md:189-190` — Marketplace-readiness gaps]
- [Source: `_bmad-output/implementation-artifacts/sprint-status.yaml:298-326` — Epic 16 block and its seeded notes]
- [Source: `CLAUDE.md` § Concurrent work, § Decision records, § Verification — shared-main rules, ADR trigger, gate discipline]
- [Source: `spike/README.md` — quarantine rules for throwaway code]

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
