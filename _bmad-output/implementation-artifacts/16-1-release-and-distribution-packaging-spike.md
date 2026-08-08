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

Status: review

<!-- Code review 2026-08-07: returned from `review` to `in-progress`. 30 patch findings applied, 4 deferred
     to `deferred-work.md`, and 9 owner decisions left open.
     Dev-story 2026-08-07 (second pass, worktree-story-16-1-decisions at 15336f4): returned to `review`.
     EIGHT of the nine decisions are RESOLVED into ADR 0040 — which now carries no `OPEN` marker — and one
     structural correction landed with them (Story 23.3 now gates Story 16.7, seated in epics.md AND
     sprint-status.yaml per CLAUDE.md, correcting Task 8's original "no structural scope change").
     **ONE ITEM REMAINS: ADR 0040's ratification.** It is AC #4, it is an act rather than a decision, and an
     agent cannot perform it on the owner's behalf. -->

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

- [x] **Task 1 — Inherit before deciding (AC: #1)**
  - [x] Read ADR 0006 § Decision + § Comparison and ADR 0022 § Decision 1-7 and § Relationship to ADR 0006. Build the settled-vs-open table from R1 **with citations**; do not re-derive the measurements. → report § 3
  - [x] Read Story 23.5's report (`23-5-packaging-strategy-report.md`) § 4 (strategy comparison), § 4 "Per channel", and § 10 (open items 3-6). Four of its six open items are this epic's. → items 4, 5 and 6 taken by ADR 0040; item 3 confirmed already-shipped (R5)
  - [x] Confirm each cited line number still resolves at your HEAD (shared `main`; R11). → all verified at `838d591`: `NuxtPrerender.cs:41,68,73`, `SpecScribe.csproj:14-16,19,23`, `AboutTemplater.cs:90,133-135`, `extension/package.json:5-14`. `<Deterministic>` confirmed absent.

- [x] **Task 2 — Decide the renderer packaging shape, empirically (AC: #1, #5)** ← *protect this one*
  - [x] Build the artefact. Used `build:package`, never `build`; artefact verified to carry **0** prerendered HTML files in `public/`. `npm ci` had to be replaced with `npm install --no-save --no-package-lock` because `npm ci` **fails at HEAD** — recorded as a finding, report § 6.1.
  - [x] Add a **temporary** `<None Include=... />` to `SpecScribe.csproj`, `dotnet pack -c Release -o artifacts`, and inspect the nupkg layout. → **`%(RecursiveDir)` double-applies**; correct form omits it (report § 2.7 finding 1).
  - [x] `dotnet tool install ... --tool-path ./probe-tools`, then run `generate` **from a different repository** with `SPECSCRIBE_RENDERER_DIR` unset. → **`errors=0`**, 373 routes @ 4.9 ms/route. Resolved artefact path proven by negative case (report § 2.3). ⚠️ **Corrected 2026-08-07:** this line previously read "18 pages". It should not — `generated=18` is `GenerationSummary.Count`, which tallies something other than prerendered pages; the prerendered page count is **373**, and `NuxtPrerender`'s own doc comment notes that *"successful routes are not individually evented"*.
  - [x] Record the nupkg size before and after the payload. → baseline **2,515,650 B / 25 entries** → **3,757,359 B / 212 entries**; delta **+1,241,709 B (+49.4%)**. Artefact re-measured at **3.96 MB / 187 files** (23.5 recorded 3.78 MB / 185).
  - [x] Answer the self-contained-binary case. → **MEASURED, not deferred:** `PublishSingleFile` does **not** move `AppContext.BaseDirectory`; sibling `renderer/` resolves; `errors=0`, 373 routes @ 5.0 ms/route. Exe = **79,742,177 B (76.0 MiB)**.
  - [x] Answer the npm case. → **one shared `specscribe-renderer` package, not per-RID.** Decided from a measured property (0 native bindings, 0 platform binaries in the artefact). Marked **unmeasured** for the end-to-end npx install, seated against 16.8 (report § 10 item 5).
  - [x] **Revert the csproj edit.** → reverted; `git status --porcelain src/ tests/ web/ extension/` is **empty**.

- [x] **Task 3 — Choose the preview cut and the non-goals (AC: #1)**
  - [x] Decide which channels ship in the **first preview** and in what order. → dotnet tool → npx → self-contained binaries; **VSIX OUT** (report § 3.1, ADR 0040 § Decision 2)
  - [x] Write the **explicit non-goals** AC #1 demands. → **ten** named in report § 3.2's "Out, by name" list (stable/1.0, Homebrew, winget, Chocolatey, Scoop, container image, Open VSX, code-signing, byte-identical reproducible builds, non-GitHub-Actions CI); the eleventh — `linux-arm64`/`osx-x64` — is stated in the following RID paragraph and folded into the single non-goals list in ADR 0040 § Decision 2. ⚠️ *Corrected 2026-08-07: this line previously claimed "eleven named, report § 3.2", which § 3.2's own list does not support.*
  - [x] State the **RID matrix**. → **three**: `win-x64`, `linux-x64`, `osx-arm64`; `linux-arm64`/`osx-x64` named and deferred. Deferral is cheap *because* the renderer is shared.

- [x] **Task 4 — Credential and prerequisite inventory (AC: #2)**
  - [x] Re-verify R3's three mechanisms live before writing them down. → all three re-verified 2026-08-07; **one changed the decision** (§ 5.3)
  - [x] Record, per channel: identity, mechanism, what is stored **where**, and who rotates it. → report § 5.1. Two channels store **nothing**.
  - [x] Decide the VS Marketplace credential path. → **the PAT path is already CLOSED**, not merely dated: global-PAT creation/regeneration was blocked **2026-03-15**, and `vsce` requires exactly that "All accessible organizations" shape. Decision: **organization-owned publisher + Entra federation**, VSIX out of the preview cut. Seated against 16.5.
  - [x] Decide the **npx install-time Node check**. → **no install-time check**; `engines.node` only, postinstall script explicitly rejected (report § 5.6). Closes ADR 0022 open question 1.
  - [x] Record the **code-signing decision**. → **neither Authenticode nor notarization for the preview**, with SmartScreen/Gatekeeper consequences stated (report § 5.5). The two lead channels install via package managers and dodge both.
  - [x] Record the package-ID reservations as **owner actions** with today's verified 404s, plus fallbacks. → four endpoints re-checked 2026-08-07, all **404**; full five-name set and per-registry fallbacks in report § 5.4.
  - [x] Assert AC #2's "no secret value is committed" — and note trusted publishing makes it structural. → asserted, report § 5.1.

- [x] **Task 5 — Versioning, changelog, and preview promises (AC: #3)**
  - [x] Decide the scheme and how a pre-release tag maps to each channel. → `0.MINOR.PATCH-preview.N`; Marketplace exception documented (no SemVer pre-release; Preview flag + `--pre-release`)
  - [x] Decide how `<Version>` derives from the tag. → **MinVer**, with the rejection reasons for Nerdbank.GitVersioning and `-p:Version=` recorded (report § 6.2)
  - [x] State how **the CLI and its renderer are pinned as one released unit**. → structural on two channels; **exact pin (`=X.Y.Z`, never `^`)** on npm (report § 6.3)
  - [x] Decide the changelog format, location and owner. → **Keep a Changelog 1.1.0**, repo root, hand-authored per story (generated notes rejected because commits bundle stories here)
  - [x] Write what "preview" **promises and does not promise**. → report § 6.6, including the Node prerequisite as a consumer-facing condition
  - [x] Scope **NFR9 reproducibility** per R6. → **weaker reading claimed explicitly**; three gaps triaged, and a **fourth found** (`npm ci` fails at HEAD) that breaks even the weak reading
  - [x] Name the four existing version numbers and state what happens to each. → report § 6.4

- [~] **Task 6 — Author and ratify the ADR (AC: #4)** — authored, and **extended on 2026-08-07 with the eight decisions the code review left open**; **ratification is the owner's act and remains open**
  - [x] Verify the next free ADR number at authoring time. → **0039 was NOT free.** Next free is **0040**. (The story said "confirm, do not assume"; this is why.) ⚠️ **Corrected by code review 2026-08-07:** 0039 was taken by **ADR 0039 — A Second Bounded Unscoped Layer, for Runtime-Attached Body-Level Classes**, authored from the owner's sunburst verify round. Story 4.9 had merely *reserved* 0039 in its story file and ultimately landed as **0041**. The original note ("Story 4.9 claimed it") misattributed the owner's own ADR.
  - [x] Author `docs/adrs/0040-release-channels-and-versioning-policy.md` in the house shape.
  - [x] State explicitly what it amends. → § Relationship to ADR 0006 and § Relationship to ADR 0022, both in the shape ADR 0022 models.
  - [x] Add one line to `docs/adrs/README.md` in the existing format.
  - [x] *(added 2026-08-07)* **Resolve the eight technical decisions the code review left open, into the ADR.** → all eight written into ADR 0040 §§ Decision 2, 5, 6, 9, 10, 11, 12 and the Status header; the record now carries **no `OPEN` marker**. See § Review Findings for each resolution.
  - [ ] **Take it to the owner and ratify it.** `Proposed` does not satisfy AC #4. → **BLOCKED — owner action, and now the story's ONLY open item.** The dev agent cannot ratify on the owner's behalf; the story's own § Owner actions item 5 assigns this to the owner. The pressure has grown since the review: **Stories 16.2 and 16.3 have both merged** against this record, 16.3 implementing § Decision 1 and § Decision 5 directly into `SpecScribe.csproj`. Surfaced at hand-off; see Completion Notes 10 and 12.

- [x] **Task 7 — Write the spike report (AC: #1-#5)**
  - [x] `_bmad-output/implementation-artifacts/16-1-spike-report.md` with a **Verdict** up front and a **method-and-provenance table**.
  - [x] Record anything that came out **wrong first**. → **four**, report § 2.7; three produced a green-looking or self-consistent wrong answer.
  - [x] Close with an **open-items table with named owners**. → ten items, report § 10; three marked *unmeasured*/*unverified* rather than estimated.

- [x] **Task 8 — Sequence the rest of the epic and record it (AC: #1, #4)**
  - [x] Note per story what this spike unblocks or changes. → report § 9, all ten stories incl. 23.3 and 17.4
  - [x] Per CLAUDE.md: a structural scope change lands in `epics.md` AND `sprint-status.yaml`. → ⚠️ **CORRECTED 2026-08-07 — this box originally certified "No structural change … Neither file structurally edited", and that was wrong on one point.** No story was added, removed or re-sequenced, which is what the original reasoning was about. But the spike **created a new cross-epic blocking edge** — Story 23.3 now gates Story 16.7 (§ 4.1, ADR 0040 § Decision 11) — and **an edge is structure**. It has now landed in `epics.md` § Story 16.7, `epics.md` § Story 23.3 (reciprocal seat) and `sprint-status.yaml`, in this change. Everything else remains AC refinement within existing stories, and *that* absence is still a recorded decision rather than an omission.
  - [x] Update `sprint-status.yaml`. → `epic-16` already `in-progress`; story → `in-progress` → `review`; `last_updated` set.

- [x] **Task 9 — Scope guard (AC: #6)**
  - [x] `git status` shows no modification under `src/`, `tests/`, `web/`, `extension/`. → **empty.** Ran in a dedicated worktree, so no concurrent session's work was in the tree to attribute or disturb.
  - [x] `dotnet test SpecScribe.slnx` green. → ✅ **RE-RUN 2026-08-07 on the decisions pass at `15336f4`: 2,991 passed / 0 failed / 3 skipped. Literally green — no flake, no re-run needed.** The three skips are the symlink tests that skip by design on this host.

    **Baseline, which the review correctly said was missing.** This pass changed **no code at all** — its File List is `.md` and `.yaml` only — so the suite is **unchanged by construction**, and this figure *is* the recorded baseline for anything downstream. Movement against the previously recorded floor of **2,978** is attributed **by name** to work merged into `main` between the two runs, not absorbed: `8faa08c` (**Story 16.3** dev — CLI packaging, the largest contributor), plus the code-review merges `69c4fe7` (**25.3**), `4571a2e` (**24.2**) and `15336f4` (**23.2** fourth pass).

    ⚠️ *Downgraded from `[x]` to `[~]` by code review 2026-08-07, because the original run was 2,962/**1**/3 while the box said "green", the attribution was to a flake class rather than to a named session, and no baseline figure existed. All three shortfalls are now closed on evidence rather than on argument — the earlier disposition was reasonable, but the certification was stronger than what backed it.*
  - [~] `cd web && npm run check` green. → **RE-RUN 2026-08-07 at `15336f4`: three of four green — `check:tokens` OK, `check:assets` OK, `check:parity` OK (24 routes / 14 families byte-identical). `check:ir-content` is RED, and it is red on `main` itself, in CI, for a cause this story did not create and deliberately did not patch.** See Completion Note 17 and report § 4.3. Causality was established before anything was touched, as CLAUDE.md requires, and **no baseline was regenerated**: the committed `web/assets/ir-content.css` is **186,504 bytes** while the committed manifest beside it claims `generatedBytes: 186492` — a 12-byte lie provable from the repository alone, with no environment involved. Attributed by name to **`3b085e7`** (the Story 24.2 code review, whose own sprint-status note records *"extraction reverted in favour of a surgical edit — RE-VERIFY ON MAIN"*). Raised and routed rather than fixed, because AC #6 forbids this story touching `web/` and because this story has already twice routed defects rather than patching them.
  - [x] If a gate moved: **establish causality before touching any baseline.** → `check:ir-content` went red **twice** and **no baseline was regenerated**. Cause 1: fresh worktree had no IR. Cause 2: a plain `generate` omits `--deep-git`, giving `+4 / -185` — the signature `build-test-analyze.yml:281-290` documents in advance as `+4 / -182`. Running `extract:ir-content` at that point would have **deleted 185 rules** and turned the gate green over a real regression.

### Review Findings

Code review 2026-08-07 (`bmad-code-review`), three parallel layers — Blind Hunter (adversarial),
Edge Case Hunter, Acceptance Auditor — over commit `9837e67`. **Scope:** that commit alone; its five-file
diff matches this story's File List 1:1, so **no sibling story is bundled** and no hunk attribution was
needed (unusual for this repository — recorded per CLAUDE.md § Scoping a code review). 56 raw findings →
44 after dedup, 1 dismissed. Every claim below was re-verified at the reviewer's `HEAD` (`c73ebcb`) **by
symbol**, not by the line numbers in the report.

**Two positives worth recording, since a review record that only lists faults misreads the work:** all
three routed defects genuinely landed in their target story files (23.3 / 16.3 / 16.2) rather than being
merely claimed, and the `npm ci` finding proved real and has since been repaired by `0b1f561`. All
arithmetic across the five documents reconciles with no numeric contradiction.

#### Decision needed

**Eight of the nine are resolved (2026-08-07 dev-story pass); the ninth is ratification, which is an act
rather than a decision and remains the owner's.** Every resolution landed in **ADR 0040**, because that is
the governing record — CLAUDE.md § Decision records forbids burying a cross-cutting decision in a story file.

- [ ] [Review][Decision] **ADR 0040 stands at `Proposed`; AC #4 requires ratified** — this is the one AC the
  implementation cannot close by itself, and it is disclosed honestly (Completion Note 10, ADR lines 4-6).
  But AC #4 and § Owner actions item 5 contradict each other, and only you can resolve it. Note the
  pressure is real: Story 16.2 has **already merged** against § Decision 9 while the record is unratified.
  → ⛔ **STILL OPEN — the story's only remaining gap.** It has *grown*: **Story 16.3 has also merged
  since**, implementing § Decision 1's pack item and § Decision 5's MinVer derivation directly into
  `SpecScribe.csproj`. Two shipped stories now depend on an unratified record.
- [x] [Review][Decision] **MinVer is unshippable exactly as specified** — the repository has **0 git tags**
  (verified), § Decision 5 **deletes** `<Version>` from `SpecScribe.csproj` (still present at `:19`), no
  bootstrap tag is named, and `MinVerTagPrefix` appears in neither document — so the report's own worked
  example `v0.1.0-preview.1` does **not** match MinVer's default empty prefix. Both failure modes are
  silent (`dotnet pack` exits 0) and land as `0.0.0-alpha.0.N`, which also breaks `README.md:260`'s
  published install recipe. Decide the tag prefix and the first tag.
  → ✅ **CLOSED, and largely by implementation rather than decision — the finding was overtaken by Story
  16.3, which has merged since the review.** Verified in the tree today: `SpecScribe.csproj` now sets
  `MinVerTagPrefix=v` (so the `v0.1.0-preview.1` example matches), `MinVerMinimumMajorMinor=0.1` and
  `MinVerDefaultPreReleaseIdentifiers=preview.0` — so an **untagged** build emits `0.1.0-preview.0.<height>`,
  inside the scheme and still carrying a pre-release label, and MinVer's `0.0.0-alpha.0.N` is unreachable.
  `README.md`'s literal is gone too: the recipe now reads the version off the produced `.nupkg`. The
  remaining act — the first tag `v0.1.0-preview.1` — is a **release-time owner action seated at 16.4**, not
  a 16.3 precondition. ADR 0040 § Decision 5 records all of this.
- [x] [Review][Decision] **No re-publish, rollback, yank or version-burn policy** — `retag`, `rollback`,
  `yank`, `unlist`, `idempotent` and `409` are absent from both documents, yet Story 16.4 AC #2 requires
  *"a failed publish leaves no partially-released state (the pipeline is safe to re-run)"*. nuget.org and
  npm both reject republishing a version. As written that AC is unachievable.
  → ✅ **DECIDED — ADR 0040 § Decision 10.** A version is **consumed on first publish to any channel and
  never reused**; recovery is forward (bump `-preview.N`, re-tag). Per-channel resume is **rejected** — it
  would need the pipeline to tell "already there because I put it there" from "…because someone else did",
  across three registries with three conflict semantics. A **registry preflight** fails fast on a consumed
  version, and the GitHub Release is created as a **draft** that brackets the irreversible registry
  publishes, so a mid-run failure leaves something deletable rather than an announced release pointing at
  nothing. Withdrawal = **unlist** (nuget) + **`npm deprecate`** + delete the Release, never delete/unpublish.
  **16.4 AC #2 becomes achievable** under the precise reading *"safe to re-run **on a new tag**"*.
- [x] [Review][Decision] **§ Decision 9 ("the tagged commit already passed on `main`") names no mechanism
  and no failure branch** — and `build-test-analyze.yml` triggers only on `main` push/PR (verified), so no
  release-branch or hotfix commit is ever built by the gating workflow. A hotfix to a released version is
  structurally impossible without first merging to `main`.
  → ✅ **DECIDED — ADR 0040 § Decision 9.** The lookup rule is now normative: query check-runs for the
  **tagged SHA**, require name `build-test-analyze` and `conclusion == success`, poll 30 s up to 15 min while
  in progress, treat the **most recent completed run as authoritative** (a later red supersedes an earlier
  green, never the reverse), and fail with an actionable message when no run exists. The hotfix branch is
  answered **by scope rather than by mechanism**: the preview is **forward-fix only**, all tags are cut from
  `main`, and that is now an explicit non-goal in § Decision 2 — which is what makes the rule total. If a
  hotfix branch is ever needed, the prerequisite is seated: 16.2 must extend the workflow's `push` trigger
  first.
- [x] [Review][Decision] **A new cross-epic blocking gate exists only as prose in a spike report** — § 4.1/§ 9
  make the `EpicsIndexSurface` fix a precondition for Story 16.7, but ADR 0040 never mentions it, and
  Task 8 simultaneously certifies *"No structural scope change … Neither file structurally edited."* Per
  CLAUDE.md a new cross-epic gate belongs in `epics.md` **and** `sprint-status.yaml`. Compounding it,
  **Story 23.3 was already at `review`** when the work was routed to it (verified in the sprint status at
  `838d591` and still today). Decide where the gate lands and which story implements it.
  → ✅ **DECIDED and LANDED.** The review was right on both halves. **(a) Task 8's certification was wrong**
  on this one point — no story was added, removed or renumbered, but a new **cross-epic blocking edge is
  structure**, so it now lives in `epics.md` § Story 16.7, `epics.md` § Story 23.3 (reciprocal seat, so the
  edge is visible from either end) and `sprint-status.yaml`, all in this change. Task 8 is corrected below
  rather than left standing. **(b) Story 23.3 keeps the fix** despite being at `review`: in this project's
  lifecycle `review` is an *iterating* state (CLAUDE.md § Story lifecycle puts owner verification and
  iteration there), 23.3 owns the surface, and it already fixed the identical defect class one component
  over — `DashboardSurface.vue` handles its own empty case gracefully **in the same run**. Opening a new
  story would fragment it; moving a Vue fix into 16.7, a launch-readiness story, would hide it.
- [x] [Review][Decision] **The recorded fallback package IDs silently change the product's documented
  command** — § 5.4 offers `specscribe-cli` as a drop-in, but `npx specscribe` is printed in ADR 0006,
  `epics.md` § 16.8 and the README. Taking the fallback invalidates all three with no escalation rule.
  → ✅ **DECIDED — ADR 0040 § Decision 12.** An implementer **may not substitute a fallback**; they stop and
  escalate. The two registries are no longer treated as symmetric, which was the buried error: losing the
  **NuGet** ID is cheap (`ToolCommandName` keeps the invocation `specscribe`, only the install line moves),
  while losing the **npm** ID is **not recoverable by any rename**, because `npx` resolves the *package*
  name — `npx specscribe` would run someone else's package. The owner then chooses between adopting
  `npx specscribe-cli` with all three documents amended in the same act, or **dropping npx from the preview
  cut**, which is a real option since `dotnet tool` leads it. This is why reservation is owner action #1.
- [x] [Review][Decision] **A single hand-edited root `CHANGELOG.md` becomes the highest-contention file in
  the repo** — generation was rejected because commits bundle stories, which is sound, but the alternative's
  known local failure mode (CLAUDE.md: *"A `Charts.cs` edit has silently vanished this way before"*) is not
  addressed. No fragment directory or verify-after-edit rule is specified.
  → ✅ **DECIDED — ADR 0040 § Decision 6.** Stories write **fragments, not the file**: one new
  `changelog.d/<story-key>.md` per user-visible change, holding Keep a Changelog sections and bullets with no
  version header. Story 16.4's release job assembles them by section into `CHANGELOG.md`, copies the released
  section into the Release body, and deletes the consumed fragments in the release commit. **Each story
  creates a distinct new file, so two concurrent stories cannot conflict and neither can overwrite the
  other** — the failure mode becomes a *missing file*, visible in `git status` and in review, rather than a
  vanished line inside a shared one. The "generated notes are rejected" rationale is untouched: assembly is
  mechanical, not generative. 16.6 owns format + assembler; 16.4 invokes it.
- [x] [Review][Decision] **The VS Marketplace exception permits exactly one VSIX publish ever** — the
  extension is frozen at a plain `0.1.0` with the Marketplace Preview flag carrying prerelease status, but
  the Marketplace requires each publish to be strictly greater. No CLI↔extension version correspondence
  rule exists.
  → ✅ **DECIDED — ADR 0040 § Decision 5.** *"The extension's MINOR mirrors the CLI's MINOR. The extension's
  PATCH is its own monotonic counter, incremented on every VSIX publish."* So CLI `v0.2.0-preview.3` publishes
  as extension `0.2.0`, and a second cut against the same CLI MINOR publishes `0.2.1` — strictly-greater is
  always satisfiable, and the correspondence reads in both directions. The extension's PATCH deliberately
  does **not** track the CLI's: the two ship on different cadences (the VSIX is out of the first preview
  entirely), and forcing a match would reintroduce the same frozen-version problem one component down.
- [x] [Review][Decision] **Only one change class is mapped to a version component** — "minor = breaking"
  inside `0.x`. PATCH has no stated meaning, a non-breaking feature has no assigned component, and there is
  no `0.x` exit criterion. Every tag decision after the first is a judgement call.
  → ✅ **DECIDED — ADR 0040 § Decision 5**, as a table. **MINOR** = a breaking change *or* a new user-visible
  feature; **PATCH** = fixes, performance, docs, internal refactors; **`-preview.N`** = a re-cut of the same
  target version after a failed or withdrawn release. MINOR deliberately carries **two** meanings — that is
  SemVer's own `0.x` rule (§4) and the policy does not pretend otherwise, which is exactly **why the
  `**BREAKING:**` changelog prefix is the load-bearing signal rather than the digits**: a consumer reads the
  changelog, not the version. Plus a three-part **`0.x` → `1.0.0` exit criterion** (IR schema frozen under
  ADR 0008; every channel in the cut has published once; the *does not promise* list no longer contains
  output/API/IR stability), so "preview forever" is not the default outcome and **Story 17.4 has something
  checkable to test**.

#### Patch

- [x] [Review][Patch] The "0039 was taken by Story 4.9" claim is **false** — ADR 0039 is the runtime-attached
  body-level-classes record authored from the owner's sunburst verify round; Story 4.9 took **0041**.
  Repeated in 5 places incl. the permanent index and commit history [`docs/adrs/README.md`, story Task 6 +
  Completion Note 4, `16-1-spike-report.md` § 11]
- [x] [Review][Patch] "`gh` is not installed on this machine" is **false** — `C:\Program Files\GitHub CLI\gh.exe`
  verified present, and project memory already records it as installed-but-not-on-PATH. It is the sole
  stated reason the NFR9-breaking item shipped **unverified-on-CI** [`16-1-spike-report.md` § 6.1, open item 3]
- [x] [Review][Patch] ADR 0040 contradicts itself on ADR 0022's owner questions — the `Amends:` header and
  § Relationship lead say **both** closed; the trailing clause says Question 2 *"remains open"*. The README
  index propagates the wrong half [`docs/adrs/0040-…md:9,202-204`; `docs/adrs/README.md`]
- [x] [Review][Patch] ADR 0040 carries no **`Deciders`** field; both sibling ADRs from the same week (0039,
  0041) do. For a record whose only open item is who ratifies it, that is the wrong field to drop
  [`docs/adrs/0040-…md:1-11`]
- [x] [Review][Patch] The self-contained-binary / GitHub Releases channel has **no credential row at all** —
  a channel in the shipping cut with an uninventoried publish credential (AC #2). Zero hits for
  `GITHUB_TOKEN` or `contents: write` in either document [`16-1-spike-report.md` § 5.1]
- [x] [Review][Patch] "Where stored" is unanswered on both fallback paths — the NuGet API-key fallback has no
  secret name, scope, owner or rotation rule, and the Marketplace federation's stored material is unstated.
  The headline *"two of three channels need no stored secret"* drops the caveat [§ 5.1, § 5.2, § 5.3]
- [x] [Review][Patch] The ADR records the pack path as a glob (`tools/<tfm>/any/renderer/**`) while the
  proven-correct exact `PackagePath` survives only in the report, and `net10.0` is hard-coded with no
  TFM-derivation rule — a TFM bump silently relocates the assembly away from the payload [ADR § Decision 1]
- [x] [Review][Patch] Nothing requires the packed renderer to be **verified complete**, though § 2.7 measured
  exactly that false pass (*"187 entries, right count, right total bytes, exit 0 — and `renderer/server/index.mjs`
  did not exist"*). Combined with the discarded renderer error text, 16.4 can publish a broken package green
- [x] [Review][Patch] The renderer is spawned via the single-string `ProcessStartInfo` overload, not
  `ArgumentList`, and § Decision 1 moves that path to a consumer-chosen install directory — a username or
  install path containing a space breaks the lead channel's first run [`src/SpecScribe/NuxtPrerender.cs:251`]
- [x] [Review][Patch] npm publish **ordering** is unspecified, so the exact `=X.Y.Z` pin has an
  install-breaking window if the wrapper lands before the renderer package [ADR § Decision 5]
- [x] [Review][Patch] The 1-hour single-use NuGet key has no stated placement in the job and no re-exchange
  rule; a retried publish has no credential [§ 5.1]
- [x] [Review][Patch] The **.NET 10 prerequisite appears nowhere** in the preview promises or in § Decision 8's
  listing surfaces, while Node does — the likeliest install blocker for the channel leading the cut [§ 6.6]
- [x] [Review][Patch] No supported-platform matrix in the promises, and no required message for the
  unmatched-platform case in the `optionalDependencies` wrapper (`linux-arm64` / `osx-x64` are deferred) [§ 6.6, § 3.2]
- [x] [Review][Patch] Keep a Changelog has no **Breaking** section, yet "breaking changes are recorded in
  `CHANGELOG.md`" is a stated preview promise — breaking and non-breaking become indistinguishable [§ 6.5, § 6.6]
- [x] [Review][Patch] `SOURCE_DATE_EPOCH` is marked **CLOSED** with no value or derivation specified, and the
  csproj **silently stamps today's date** on an unset or malformed value rather than failing
  [ADR § Decision 7; `src/SpecScribe/SpecScribe.csproj:36-38`]
- [x] [Review][Patch] The binary channel's "structural" CLI↔renderer pin is **two filesystem objects** with no
  version stamp — reproducing exactly the mismatch Story 16.9 AC #2 exists to prevent [ADR § Decision 5 vs § Decision 1]
- [x] [Review][Patch] The only unsigned channel also has **no published digest or attestation**, and 16.4's
  release-asset naming/archive format is unspecified [§ 5.5]
- [x] [Review][Patch] Decisions a downstream story must implement live only in the report, not the ADR —
  § 6.6 preview promises (17.4's sign-off checklist), § 5.4 fallback IDs + RID naming (16.8), and the
  code-signing consequences AC #2 demanded. CLAUDE.md § Decision records names this pattern by name
- [x] [Review][Patch] The Node-check amendment promotes a **self-described interim stand-in** to permanent
  without engaging the code's own doc comment (*"Until it is, this is the check"*)
  [`src/SpecScribe/NuxtPrerender.cs:143-145`; ADR § Decision 8]
- [x] [Review][Patch] "Amends ADR 0006" is hollow where loud and silent where real — adding the packaging
  shape is an extension, while the genuine departure (this ADR ships `dotnet tool` **first**; ADR 0006 calls
  npx *primary*) is framed as **not** an amendment [`docs/adrs/0006-…md:202`]
- [x] [Review][Patch] The `npm ci` root cause handed to 16.2 points at the wrong half of the lockfile — the
  peer dependencies **were** declared at `838d591`; the missing item was the top-level
  `node_modules/@emnapi/runtime` tree entry, which is what `0b1f561` added [§ 6.1]
- [x] [Review][Patch] The probe repository's composition is documented nowhere, and the two probe runs are
  irreconcilable on the report's face — § 2.2 reports **373 routes**, § 2.7/§ 4.1 report a first probe at
  **21**. The composition is load-bearing for AC #5's central claim [§ 2.1]
- [x] [Review][Patch] No pre-story baseline was recorded, so AC #6's *"unchanged from the pre-story baseline"*
  is unprovable; the suite was **1 failing** and the task box asserts "green"; attribution is to a flake
  class, not *"to a concurrent session by name"* as AC #6 words it [§ 7.2, Task 9]
- [x] [Review][Patch] Channel version **parity** is unstated — a version resolvable on NuGet may not exist on
  npm, leaving 16.9's Action with no authoritative channel for "a released version" [ADR § Decision 2]
- [x] [Review][Patch] Provenance table points to § 3 for commands that are in § 2.1 — a broken pointer in the
  one table whose purpose is auditability [§ 1]
- [x] [Review][Patch] The probe item called "reproduced **verbatim**" is recorded two different ways —
  § 2.1 carries `CopyToOutputDirectory="Never"`, the Debug Log does not [§ 2.1, § 11]
- [x] [Review][Patch] "**three of the four** produced a green-looking or self-consistent wrong answer" —
  only one did; (2) threw, (3) reported `errors=1`, (4) turned the gate red [§ 2.7]
- [x] [Review][Patch] `generated=18` is mislabelled "**18 pages**" — the prerendered page count is 373;
  `GenerationSummary.Count` tallies something else [story Task 2]
- [x] [Review][Patch] A transient defect repaired the same day (`npm ci`, fixed by `0b1f561`) is written into
  the **permanent ADR index**, where it will read as a standing defect indefinitely [`docs/adrs/README.md`]
- [x] [Review][Patch] Non-goal count mismatch — Task 3 claims *"eleven named"*; § 3.2 enumerates **ten**
  [story Task 3 vs § 3.2]

#### Deferred

- [x] [Review][Defer] Packaging shape measured on **Windows / `win-x64` only** but asserted for three RIDs and
  both packing hosts — deferred, verification belongs to 16.3/16.4 on Linux/macOS runners
- [x] [Review][Defer] The regression-floor gates were run from a worktree the report itself proves resolved
  the **main checkout's** artefact (§ 4.2), and the two facts are never reconciled — deferred, pre-existing;
  the underlying `FindRepoRoot` defect has since been fixed by Story 16.3
- [x] [Review][Defer] The `-185` vs documented `-182` gate delta is dispositioned as "corpus growth" **by
  assertion**, with the exact `+4` match on the other side left unexamined — deferred, pre-existing
- [x] [Review][Defer] First-ever-publish ID claiming vs trusted-publishing policy creation ordering is
  unaddressed, so § 8's owner-action order may be unexecutable — deferred, resolves at 16.4 wiring time

#### Dismissed (1)

The npm channel's per-channel AC #5 answer being reasoned-from-a-measured-property rather than run
end-to-end: honestly self-labelled **unmeasured** in open item 5 and seated against Story 16.8. Handled
elsewhere, not a defect.

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

Claude Opus 5 (`claude-opus-5[1m]`) — dev-story workflow, 2026-08-07.

### Debug Log References

Run in a dedicated git worktree, `.claude/worktrees/story-16-1-dev` on branch `worktree-story-16-1-dev`,
branched from `838d591`. The story frontmatter's `baseline_commit: 7ff3b13` was **preserved, not overwritten**
— `main` had advanced by one merge (`838d591`) between create-story and dev-story.

Key commands, in order:

```sh
cd web && npm install --no-save --no-package-lock   # npm ci FAILS at 838d591 — see Completion Note 6
npm run sync:assets && npm run build:package        # 187 files / 4,154,964 B; 0 prerendered HTML in public/
dotnet pack src/SpecScribe/SpecScribe.csproj -c Release -o artifacts-baseline   # 2,515,650 B / 25 entries
# + temporary <None Include="..\..\web\.output\**\*" Pack="true" PackagePath="tools\net10.0\any\renderer" CopyToOutputDirectory="Never" />
#   (corrected 2026-08-07: CopyToOutputDirectory="Never" was missing here but present in report § 2.1.
#    The exact string is load-bearing — § 2.7 finding 1 shows a wrong form packs 187 entries at exit 0
#    with no entry point — so the two records must not disagree. ADR 0040 § Decision 1 now carries the
#    normative form, with $(TargetFramework) in place of the literal net10.0.)
dotnet pack src/SpecScribe/SpecScribe.csproj -c Release -o artifacts            # 3,757,359 B / 212 entries
dotnet tool install SpecScribe --version 0.1.0-preview --tool-path ./probe-tools --add-source ./artifacts
# from C:\Users\MattE\.claude\jobs\eac9eab5\tmp\probe-project (own git repo, NO web/, env unset):
specscribe generate --output probe-out3        # 373 routes @ 4.9 ms/route, errors=0
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o probe-singlefile
specscribe generate --output probe-out-sf      # 373 routes @ 5.0 ms/route, errors=0
# revert csproj; then the regression floor:
dotnet test SpecScribe.slnx                                                     # 2962 / 1 / 3
dotnet run --project src/SpecScribe --no-build -- generate --deep-git           # --deep-git is REQUIRED
cd web && npm run check                                                          # all four gates OK
```

Probe build outputs (`probe-tools/`, `probe-singlefile/`, `artifacts/`, `artifacts-baseline/`) were deleted
before the final gate run so they could not pollute the Code Map corpus the IR gates derive from.

**Second pass (2026-08-07)** — worktree `.claude/worktrees/story-16-1-decisions` on branch
`worktree-story-16-1-decisions`, cut at **`15336f4`**. `main` had advanced **five merges** past the code
review's `c73ebcb`, including `8faa08c` (**Story 16.3 dev**), which closed most of the MinVer finding before
this pass could — re-verified in the tree rather than taken from 16.3's story file. Commands:

```sh
dotnet test SpecScribe.slnx                       # 2,991 passed / 0 failed / 3 skipped — literally green
cd web && npm ci && npm run sync:assets && npm run build:package
dotnet build src/SpecScribe/SpecScribe.csproj --no-incremental   # re-embed assets (CLAUDE.md)
dotnet run --project src/SpecScribe --no-build -- generate --deep-git   # errors=0, generated=809
cd web && npm run check                           # tokens OK; ir-content RED; chain halted there
cd web && npm run check:assets && npm run check:parity   # both OK — run separately, since && short-circuits
```

**Diagnosing the red gate without regenerating a baseline** (CLAUDE.md § Concurrent work). The originals were
copied to the job scratchpad first, the extractor run **purely as a diagnostic**, the delta read, and the file
**restored** — the extraction was never committed:

```sh
cp web/assets/ir-content.manifest.json  <scratch>/manifest.orig.json
cp web/assets/ir-content.css            <scratch>/ir-content.orig.css
cd web && npm run extract:ir-content              # DIAGNOSTIC ONLY
diff <scratch>/manifest.orig.json web/assets/ir-content.manifest.json
#   17c17: "generatedBytes": 186492  ->  186504        (the ONLY field that moves)
git diff --stat web/                              # only the manifest; all three CSS sheets byte-identical
node -e "console.log(require('fs').statSync('web/assets/ir-content.css').size)"   # 186504 — committed file
cp <scratch>/manifest.orig.json web/assets/ir-content.manifest.json   # RESTORED
"C:/Program Files/GitHub CLI/gh.exe" run view 31234945903 --log-failed   # same failure in CI, at 15336f4
```

`gh` was called **by full path** — project memory records it as installed but not on `PATH`, and the first
pass's false *"`gh` is not installed"* claim was one of the two findings the code review overturned.

### Completion Notes List

1. **AC #5 is satisfied on two channels, measured from a foreign repository.** A `renderer/**` payload packed
   at `tools/net10.0/any/renderer/` lands beside the executing assembly in the `dotnet tool` store;
   `AppContext.BaseDirectory` resolves there; `generate` runs to **`errors=0`** with `SPECSCRIBE_RENDERER_DIR`
   unset. `PublishSingleFile` **does not** move `AppContext.BaseDirectory` away from a sibling `renderer/` —
   the case R2 allowed to be left unmeasured is measured. The observed `BaseDirectory` was obtained by a
   **negative case** (renaming the artefact away and reading the tool's own error), not from documentation,
   and that same negative case is the proof the probe repository was genuinely foreign: candidate 3
   (`web/.output` in the repo) did **not** rescue it.

2. **Cost: +1,241,709 bytes (+49.4%) on the nupkg for a 3.96 MB / 187-file payload.** The artefact compresses
   ~3.4× because it is pure JavaScript. Story 23.5 measured 3.78 MB / 185 files eleven days ago; this is a
   **refresh, not a correction**, and the figure has now moved twice — Story 16.3 should derive it, not quote it.

3. **The VS Marketplace finding changed a decision.** R3 framed the PAT as "dated" with a 2026-12-01 deadline.
   Live verification found the operative date is **2026-03-15**, when Azure DevOps blocked creation *and
   regeneration* of global PATs — and `vsce` requires exactly that shape ("All accessible organizations" +
   Marketplace (Manage)). For a publisher not already holding a pre-March token, **there is no PAT path at
   all**. Decision: organization-owned publisher + Entra federation, and the **VSIX drops out of the preview
   cut**. `microsoft/vscode-vsce#1023` (federated SPs failing on *personally*-owned publishers, closed
   `not_planned`) is why the org-vs-personal call is made now rather than at 16.5 time.

4. **`0039` was not free.** The ADR landed as **0040**. The story file said "confirm, do not assume", and this
   is the case that justified it.

   ⚠️ **Corrected by code review 2026-08-07 — this note was wrong as originally written.** It said *"Story 4.9
   claimed it on 2026-08-06."* It did not. **0039 is `0039-runtime-attached-body-level-classes.md`**, authored
   from **the owner's verify round on the sunburst surfaces** (Deciders: Matthew-Hope Eland), landed in
   `76e5e42`/`6a7bc71`. Story 4.9 had *reserved* 0039 in its own story file and ultimately took **0041**, whose
   header records: *"Took 0041 because 0039 **and** 0040 were both claimed after the story's baseline."* The
   misattribution was repeated in five places — this note, Task 6, spike report § 11, the `docs/adrs/README.md`
   index entry, and the commit message of `9837e67` — and Story 4.9's own review had already flagged it and
   assigned the correction back to this story. All except the immutable commit message are now fixed.

5. **A real product defect was found and raised, not patched — same class as 23.5's `DashboardSurface.vue`.**
   `EpicsIndexSurface.vue` hard-throws when the epics index has no child pages, so a project SpecScribe cannot
   extract epics from generates with `errors=1` and no `epics.html`. Reproduced twice. Practical weight for
   Epic 16 is high: it is what a thin or non-BMad external adopter sees first. **Routed to Story 23.3, gating
   Story 16.7.** The dashboard already handles its own empty case gracefully *in the same run*, so the correct
   behaviour is modelled one component over.

6. **`npm ci` fails on a clean checkout at `838d591`** (`Missing: @emnapi/runtime@1.11.3 from lock file`), and
   three CI steps depend on it. This breaks even the *weaker* reading of NFR9 that ADR 0040 claims. Recorded
   as **unverified-on-CI**, not as "CI is broken". Routed to Story 16.2. The session worked around it with
   `npm install --no-save --no-package-lock`, which left `web/package-lock.json` untouched.

   ⚠️ **Corrected by code review 2026-08-07 — the stated reason for leaving this unverified was false.** The
   note originally read *"CI's actual status could not be checked — `gh` is not installed on this machine."*
   `gh` **is** installed, at `C:\Program Files\GitHub CLI\gh.exe` (verified); it is simply not on `PATH`, and
   project memory already records exactly that, with the instruction to call it by full path. A checkable
   fact was declared uncheckable, and it was the single item this ADR says *"breaks even the weak reading of
   NFR9"* — so the one release-blocking question shipped open on a false premise. The Node-version caveat
   (CI pins 24.11.1 via `web/.nvmrc`; this session ran 24.18.1) was legitimate and stands.

   ✅ **Since resolved.** Commit `0b1f561` ("CI fix: repair the lockfile and regenerate the two stale drift
   gates") repaired it. The finding was real and valuable — only the excuse for not verifying it was not.

7. **Second defect: `NuxtPrerender.FindRepoRoot` does not recognise a git worktree.** It tests
   `Directory.Exists(".git")`, but in a worktree `.git` is a *file* (56 bytes, measured), so the walk runs past
   the worktree root to the main checkout. Observed live: a generate from this worktree resolved
   `C:\Dev\SpecScribe\web\.output`. Developer-path only, but newly reachable — CLAUDE.md still says worktrees
   are unavailable on this machine while `git worktree list` shows five and the last four commits on `main`
   are worktree merges. Routed to Story 16.3; the CLAUDE.md statement is stale.

8. **`check:ir-content` went red twice and no baseline was regenerated.** First red was an environmental
   precondition (a fresh worktree has no IR). Second was `+4 / -185` from a `generate` without `--deep-git` —
   the exact signature `build-test-analyze.yml:281-290` documents in advance as `+4 / -182`. Following the
   gate's own suggested fix (`npm run extract:ir-content`) would have **deleted 185 deep-analytics rules from
   the shipped stylesheet layer and turned the gate green**. This is the CLAUDE.md trap, met in the wild.

9. **AC #6 holds.** No file under `src/`, `tests/`, `web/` or `extension/` is modified; the temporary csproj
   probe edit was reverted and the revert *verified by `git status`*, not assumed. Suite 2,962 passed / 1
   failed / 3 skipped, the one failure a known-class `FileWatcherService` timing flake proven by an isolated
   re-run at 11/11. All four web gates green.

10. **⚠️ AC #4 IS NOT FULLY SATISFIED, and this is a genuine hand-off, not an oversight.** ADR 0040 is
    authored, indexed, and complete — but it stands at **`Proposed`**. AC #4 requires it *ratified*, while the
    story's own § Owner actions item 5 assigns ratification to the owner. The dev agent cannot ratify on the
    owner's behalf, so the two clauses can only be reconciled by the owner acting at review time. **This is
    the one acceptance criterion the implementation cannot close by itself.** Everything else it asks for —
    ADR under `docs/adrs/`, indexed in `README.md`, explicit amendment statements — is done.

11. **~~No structural scope change~~ — CORRECTED 2026-08-07.** The original claim was that neither
    `epics.md` nor `sprint-status.yaml` needed a structural edit, reasoning that no story was added, removed
    or renumbered. That reasoning was sound but incomplete: this spike **created a new cross-epic blocking
    edge** (Story 23.3 → Story 16.7), and *an edge is structure*. It now lands in `epics.md` § Story 16.7,
    `epics.md` § Story 23.3 (reciprocal, so the dependency is visible from either end) and
    `sprint-status.yaml`. The rest of § 9 remains AC refinement within existing stories, and *that* absence
    is still a recorded decision rather than an omission.

---

### Second pass — 2026-08-07, closing the code review's nine open items

Worktree `.claude/worktrees/story-16-1-decisions` on branch `worktree-story-16-1-decisions`, cut at
**`15336f4`**. Note `main` had advanced **five merges** past the code review's `c73ebcb` before this pass
started — including `8faa08c`, **Story 16.3's dev merge** — which turned out to matter (note 13).

12. **Eight of the nine open items are resolved; the ninth is ratification and is genuinely the owner's.**
    Every resolution went into **ADR 0040**, not into this story file — CLAUDE.md § Decision records is
    explicit that a cross-cutting decision buried in a story file is the anti-pattern, and three of these
    (release atomicity, the gate lookup rule, changelog fragments) are implemented by stories that will read
    the ADR and never open this file. ADR 0040 now carries **no `OPEN` marker anywhere**. Per-item detail is
    in § Review Findings; the short form: release atomicity + withdrawal (§ D10), the CI-gate lookup rule and
    a forward-fix-only preview scope (§ D9), MinVer bootstrap (§ D5), version-component semantics and a
    `0.x`→`1.0` exit criterion (§ D5), extension versioning (§ D5), `changelog.d/` fragments (§ D6), the
    package-ID escalation rule (§ D12), and the `EpicsIndexSurface` gate's ownership (§ D11).

13. **One finding was overtaken by implementation rather than answered by decision, and that is worth
    recording as its own note.** The review's MinVer item was the most alarming of the nine — 0 git tags, no
    tag prefix, both failing silently at exit 0. **Story 16.3 merged in the interval and closed most of it:**
    `SpecScribe.csproj` now carries `MinVerTagPrefix=v`, `MinVerMinimumMajorMinor=0.1` and
    `MinVerDefaultPreReleaseIdentifiers=preview.0`, so an untagged build emits `0.1.0-preview.0.<height>` —
    inside the scheme, still pre-release, so the About page's Preview badge survives — and MinVer's
    `0.0.0-alpha.0.N` is now unreachable. `README.md`'s hard-coded `--version 0.1.0-preview` is gone too; the
    recipe reads the version off the produced `.nupkg`. **Verified in the tree, not assumed from 16.3's story
    file.** What remains is one act, not a defect: the first tag `v0.1.0-preview.1`, seated as an owner
    action at **16.4** release time. The general lesson is CLAUDE.md's own: a review finding on shared `main`
    can age between the review and the fix, so re-verify against the tree before implementing the remedy.

14. **The structural-scope correction was the review's sharpest catch, and it changed an artifact, not just
    a sentence.** Task 8 had certified "no structural scope change" while § 4.1 simultaneously created a
    blocking precondition on Story 16.7 — the two could not both be true. Resolved by landing the edge in
    `epics.md` **and** `sprint-status.yaml` (CLAUDE.md's requirement), with a reciprocal seat on § Story 23.3.
    On the "which story implements it" half: **23.3 keeps it.** `review` is an *iterating* state in this
    project's lifecycle, not a closed one; 23.3 owns the surface; and it already fixed the identical defect
    class one component over, since `DashboardSurface.vue` handles its own empty case gracefully **in the
    same run**. A new story would fragment the work and 16.7 would hide a Vue fix inside a launch-readiness
    story.

15. **Regression floor, with the baseline the review said was missing.** `dotnet test SpecScribe.slnx` →
    **2,991 passed / 0 failed / 3 skipped** at `15336f4` — literally green, no flake, no isolated re-run
    needed. This pass changed **no code whatsoever** (File List is `.md` + `.yaml` only), so the suite is
    unchanged *by construction* and this figure is the recorded baseline. The delta from the previously
    recorded floor of 2,978 is attributed **by name** to merges landed in between — `8faa08c` (Story 16.3),
    `69c4fe7` (25.3), `4571a2e` (24.2), `15336f4` (23.2) — rather than absorbed into a hand-wave.

17. **🔴 A third defect found and raised, not patched: `main`'s CI is RED right now, on a REQUIRED check, and
    the cause is a 12-byte stale number in a committed file.** This is the most operationally urgent thing
    this pass found, so it is stated plainly rather than buried in a gate note.

    **The evidence, in the order it was established** — CLAUDE.md is explicit that causality comes before any
    baseline is touched, and this is the trap it warns about, met a second time:
    - `git status --porcelain src/ tests/ web/ extension/` in this worktree: **empty**. This pass changed no
      code, so the red is not mine.
    - Every generated sheet is **byte-identical** to its committed version — `ir-content.css`,
      `shared-primitives.css`, `runtime-body.css` all diff to zero. Only the manifest moves.
    - The manifest moves in **exactly one field**: `generatedBytes` 186492 → **186504**.
    - The proof needs no environment at all: the **committed** `web/assets/ir-content.css` measures
      **186,504 bytes** on disk, while the **committed** manifest beside it claims **186,492**. The committed
      artifact contradicts itself. My regeneration produces the *correct* number.
    - **It reproduces in CI, not just locally**, which disproves the fresh-worktree/pruning explanation that
      would otherwise be the obvious suspect (and which project memory records as the usual cause). Run
      `31234945903` at `15336f4` fails on `check:ir-content` with the identical sub-line —
      *"ir-content.manifest.json: out of sync with the sheet it documents."* `main` has been red since
      `c73ebcb`; it was last green at `07bdb790`.
    - Attributed **by name** to **`3b085e7`** (Story 24.2's code review). Its own `sprint-status.yaml` note
      records the mechanism in advance: *"extraction reverted in favour of a surgical edit — **RE-VERIFY ON
      MAIN**."* A surgical edit changed `ir-content.css` by 12 bytes without recomputing the manifest field
      that describes it. The re-verify never happened.

    **Why it was raised rather than fixed.** AC #6 forbids this story from putting any `web/` file in its
    File List, and this story has twice already established the discipline of routing defects instead of
    patching them (`EpicsIndexSurface.vue` → 23.3, `FindRepoRoot` → 16.3, `npm ci` → 16.2). Patching here to
    make my own gate run green would be the exact move CLAUDE.md names as the anti-pattern — and would break
    the AC the code review most recently penalised this story for over-claiming.

    **Why it matters beyond a red badge, and why it lands in *this* story's report.** Story 16.2 made
    `build-test-analyze` a **required** check on `main`, so a red `main` blocks every PR merge. Worse for
    Epic 16 specifically: **ADR 0040 § Decision 9 — written in this very pass — makes "the tagged commit
    already passed on `main`" the release preflight.** While `main` is red, that preflight can never pass and
    **no release can be cut at all**. A one-field staleness has become a release blocker, which is precisely
    the kind of coupling the § Decision 9 lookup rule exists to make visible rather than mysterious.

    **The fix is one command, and it is an owner action** (report § 8 action 8): `cd web && npm run
    extract:ir-content`, then commit. It is provably safe — every CSS sheet is byte-identical, so the shipped
    stylesheet does not change; only the manifest stops lying about it.

18. **AC #4 is still not satisfied, and it is now the *only* thing outstanding *within this story*.** Reported plainly rather
    than softened: everything else the story asks for is done and evidenced. The one gap has grown since the
    review — **two shipped stories (16.2 and 16.3) now depend on an unratified record**, with 16.3 having
    implemented § Decision 1's pack item and § Decision 5's MinVer derivation directly into the product. The
    ADR is complete and internally consistent; it needs the owner to move `Status: Proposed` → `Accepted`.

### File List

**Added**

- `_bmad-output/implementation-artifacts/16-1-spike-report.md`
- `docs/adrs/0040-release-channels-and-versioning-policy.md`

**Modified**

- `docs/adrs/README.md` — the ADR 0040 index entry (added first pass; its "9 open decisions" line rewritten
  in the second pass to record the eight resolutions)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — story status + `last_updated`; second pass
  also seated the 23.3 → 16.7 gate on both the `16-7` and `23-3` keys
- `_bmad-output/implementation-artifacts/16-1-release-and-distribution-packaging-spike.md` — this story file
  (tasks, Dev Agent Record, File List, Change Log, Status)

**Modified — second pass only (2026-08-07, closing the code review's nine open items)**

- `docs/adrs/0040-release-channels-and-versioning-policy.md` — the eight resolutions (§§ Decision 2, 5, 6, 9,
  10, 11, 12 and the Status header); no `OPEN` marker remains anywhere in the record
- `_bmad-output/implementation-artifacts/16-1-spike-report.md` — new § 4.3 (`main` CI red, raised not
  patched), § 8 owner actions 6–9, § 9 sequencing rows for 16.4/16.5/16.6/16.7/16.8/23.3, § 10 items 11–14
  and 17–20, plus the Verdict's review banner
- `_bmad-output/planning-artifacts/epics.md` — the structural change: § Story 16.7 gains a blocking
  dependency on Story 23.3 and a `**Depends on:**` line; § Story 23.3 gains the reciprocal seat

**AC #6 re-verified on the second pass:** `git status --porcelain src/ tests/ web/ extension/` is **empty**.
No product code, no tests, no `web/`, no `extension/` — this pass is `.md` and `.yaml` only. The one `web/`
file that *would* have been touched (`ir-content.manifest.json`) was deliberately **restored rather than
committed**, and the defect routed instead (Completion Note 17).

**Deliberately NOT created**

- `spike/release/**` — the probe needed no committed throwaway code: six shell commands and one reverted
  `.csproj` item, reproduced verbatim in report § 2.1.

**Touched during the probe and reverted / removed (no net change)**

- `src/SpecScribe/SpecScribe.csproj` — temporary `<None Include=... />` pack item, **reverted**; verified
  absent from `git status`.
- `probe-tools/`, `probe-singlefile/`, `artifacts/`, `artifacts-baseline/`, `SpecScribeOutput/` — untracked
  build outputs; the first four deleted before the final gate run.

## Change Log

| date | change |
|---|---|
| 2026-08-07 | **Second dev-story pass — closed the code review's nine open items (8 resolved, 1 handed back).** All eight technical decisions landed in **ADR 0040**, which now carries no `OPEN` marker: release atomicity + withdrawal (a version is consumed on first publish and never reused; forward-only re-cuts, a registry preflight, a draft Release bracketing the irreversible publishes — making **16.4 AC #2 achievable** as *"safe to re-run on a new tag"*); the **CI-gate lookup rule** (check-runs on the tagged SHA, poll/authority/failure branches named) with the hotfix branch answered **by scope** — the preview is **forward-fix only**; **MinVer bootstrap** (closed largely by Story 16.3, which merged in the interval and shipped `MinVerTagPrefix=v` + a `0.1` floor + `preview.0` identifiers — verified in the tree, not assumed); **version-component semantics** + a checkable `0.x`→`1.0` exit criterion; **extension versioning** (MINOR mirrors the CLI, PATCH is its own monotonic counter — a frozen `0.1.0` allowed exactly one Marketplace publish ever); **`changelog.d/` fragments** replacing a contended root file; the **package-ID escalation rule** (losing the npm ID is *not* recoverable by rename, because `npx` resolves the package name); and the **`EpicsIndexSurface` gate's ownership** (23.3 implements, 16.7 blocked). That last one came with a **structural correction**: Task 8's "no structural scope change" was wrong — a new cross-epic blocking **edge is structure** — so it now lands in `epics.md` (§ 16.7 and § 23.3) **and** `sprint-status.yaml`, per CLAUDE.md. Regression floor re-run with the baseline the review said was missing: **2,991 passed / 0 failed / 3 skipped**, literally green, movement attributed by name to `8faa08c`/`69c4fe7`/`4571a2e`/`15336f4`. 🔴 **Third defect found and raised, not patched: `main`'s CI is RED on the required `build-test-analyze` check** — the committed `ir-content.manifest.json` says `generatedBytes: 186492` while the committed `ir-content.css` beside it is **186,504 bytes**; proven from the repository alone, reproduced **in CI** (so not the usual worktree-pruning cause), attributed to **`3b085e7`**, and **blocking § Decision 9's release preflight**. No baseline regenerated; one-command fix seated as owner action 8. No product code touched (AC #6 re-verified empty). **ADR ratification still open — owner action (AC #4).** |
| 2026-08-07 | Story implemented. Renderer packaging shape decided **empirically** on two channels (`errors=0` from a foreign repository, `AppContext.BaseDirectory` proven by negative case); nupkg delta measured at +1,241,709 B (+49.4%). Preview cut, non-goals and a three-RID matrix fixed. Credential inventory re-verified live — two channels store **no secret**, and the VS Marketplace PAT path found **already closed** since 2026-03-15, moving the VSIX out of the preview. Versioning (MinVer, `0.x`/`-preview`, exact CLI↔renderer pin), Keep a Changelog, and preview promises recorded. **ADR 0040** authored (0039 was taken by Story 4.9) and indexed, amending ADR 0006 §Decision and ADR 0022 §Decision 5. Two product defects found and routed rather than patched (`EpicsIndexSurface.vue` empty-epics throw → 23.3; `FindRepoRoot` worktree blindness → 16.3), plus a live `npm ci` failure → 16.2. No product code changed; AC #6 verified. **ADR ratification remains open — owner action (AC #4).** |
