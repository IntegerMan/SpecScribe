# ADR 0040 — Release Channels, Packaging Shape, Credential Posture and Versioning Policy

- **Status:** Proposed — 2026-08-07
  - ⏫ **Ratification to `Accepted` requested of the owner.** Story 16.1 AC #4 requires this ADR to land
    *ratified*, not `Proposed`. Stories 16.2–16.9 and 17.4 all build on it. The ratification is the owner's
    act; this line is the request, not the act.
- **Authored by:** [Story 16.1](../../_bmad-output/implementation-artifacts/16-1-release-and-distribution-packaging-spike.md) (release & distribution packaging spike)
- **Evidence:** [16-1-spike-report.md](../../_bmad-output/implementation-artifacts/16-1-spike-report.md)
- **Amends:** [ADR 0006](0006-delivery-architecture-and-distribution.md) §Decision (channel list — adds the packaging shape and an ordered preview cut) and [ADR 0022](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) §Decision 5 (Node-check *placement*; and closes its two open owner questions)
- **Governs:** Stories 16.2, 16.3, 16.4, 16.5, 16.6, 16.7, 16.8, 16.9, 17.4

## Context

ADR 0006 chose the distribution channels. ADR 0022 decided that the prebuilt Nuxt `.output/` artefact is what
ships and that Node is a generate-time runtime. Neither said **how the artefact physically gets inside a
package**, and nothing populated it: `NuxtPrerender.ResolveArtefactDirectory` probes `renderer/` beside the
executing assembly and its own doc comment calls that *"the Epic 16 packaging shape"*
(`src/SpecScribe/NuxtPrerender.cs:68`), while `SpecScribe.csproj` packs no such payload. Story 16.9's epics.md
entry states the consequence precisely: until the renderer ships inside the package, an external consumer must
vendor this entire repository.

Two further things had drifted since the epic was planned. The credential posture for two of three channels no
longer involves a stored secret at all (Trusted Publishing). And the third channel's credential path — an
Azure DevOps global PAT — **stopped being issuable on 2026-03-15**, which is not a future deadline but a past
one.

Story 16.1 was run as a decision-first, timeboxed spike. Its load-bearing question was answered
**empirically**, because the failure mode of getting it wrong is a published tool that resolves no renderer
and tells its user to build a Nuxt artefact.

## Options considered — how the renderer rides inside a package

| | option | verdict |
|---|---|---|
| **A** | **Packed as content at `tools/<tfm>/any/renderer/`; a sibling `renderer/` beside the binary; one shared npm package** | **CHOSEN** — measured working on two channels (spike report § 2) |
| **B** | Embed the artefact as `<EmbeddedResource>`, as the seven existing assets are | **Impossible.** The artefact is 187 files Node must `import` from disk. Embedding solves a different problem (single files the C# code writes out); it cannot serve an ESM module graph. |
| **C** | Download the artefact on first run from GitHub Releases | **Rejected.** Introduces a network dependency into a tool whose codebase makes zero outbound calls, breaks air-gapped and offline use, and creates a supply-chain surface for a 1.18 MB saving. |
| **D** | Keep `SPECSCRIBE_RENDERER_DIR` as the only path; document it | **Rejected.** This is the status quo, and it is the defect: `README.md:132-141` currently tells external users to point at *SpecScribe's own clone*. |
| **E** | Publish the renderer as a separate package on every channel and resolve it at runtime | **Rejected for NuGet/binary** (re-creates the version-mismatch class Story 16.9 AC #2 exists to prevent), **adopted for npm only**, where the packaging model already separates platform payloads and the artefact is platform-neutral. |

## Decision

### 1. The renderer artefact ships **inside** the package, as content

- **NuGet `dotnet` global tool:** packed at `tools/<tfm>/any/renderer/**`. **Measured:** it lands beside the
  real assembly in the tool store, `AppContext.BaseDirectory` resolves to that directory, and a `generate`
  run from a foreign repository with `SPECSCRIBE_RENDERER_DIR` unset completes at **`errors=0`**, 373 routes
  at 4.9 ms/route. Cost: **+1,241,709 bytes (+49.4%)** on the nupkg for a 3.96 MB / 187-file payload.
- **Self-contained binary:** a sibling `renderer/` directory beside the executable. **Measured:**
  `PublishSingleFile` does **not** move `AppContext.BaseDirectory` into an extraction directory, so the
  sibling resolves. `errors=0`, 373 routes at 5.0 ms/route.
- **npm / npx:** the artefact is **one shared, platform-neutral `specscribe-renderer` package** — never
  duplicated per RID. It contains **zero native bindings**, so per-RID duplication would multiply 1.10 MB
  (gzipped) across the matrix for no benefit.

**No `SPECSCRIBE_RENDERER_DIR` is required by any packaged consumer.** The variable remains the explicit
override and keeps its hard-fail-on-miss semantics (`NuxtPrerender.cs:80-98`).

### 2. The preview cut, in order

1. NuGet `dotnet` global tool
2. npx / npm wrapper
3. Self-contained per-OS binaries — RID matrix **`win-x64`, `linux-x64`, `osx-arm64`**
4. **VSIX / VS Marketplace is OUT of the first preview** (§ Decision 4)

**Explicit non-goals:** stable/1.0 · Homebrew · winget · Chocolatey · Scoop · a container image · Open VSX ·
code signing · byte-identical reproducible builds · publishing from any CI other than GitHub Actions ·
`linux-arm64` and `osx-x64` (named and deferred, cheap to add later because the renderer is shared).

### 3. Credential posture — two channels store nothing

| channel | mechanism | stored in the repository |
|---|---|---|
| nuget.org | Trusted Publishing — `NuGet/login@v1`, `id-token: write`, 1-hour single-use key | **nothing** |
| npm | Trusted Publishing — npm CLI ≥ 11.5.1 **and** Node ≥ 22.14.0, `id-token: write`, provenance by default, `NODE_AUTH_TOKEN` must **not** be set | **nothing** |

Story 16.1 AC #2's *"no secret value is committed"* is therefore **structural** for both shipping channels,
not a matter of discipline. **Caveat carried deliberately:** nuget.org's Trusted Publishing is still a gradual
rollout; if it is unavailable on the owner's account, the NuGet channel falls back to a stored API key and
this clause weakens for that channel. That must be confirmed before Story 16.4 begins.

### 4. VS Marketplace: organization-owned publisher + Entra federation, and out of the preview

The PAT path is **closed, not merely dated**. Azure DevOps blocked creation *and regeneration* of global PATs
on **2026-03-15**; `vsce` requires a PAT scoped to *"All accessible organizations"* with Marketplace (Manage),
which **is** the global shape; all remaining global PATs die **2026-12-01**. The VS Code documentation now
directs publishers to Microsoft Entra ID instead.

**Decision:** when Story 16.5 runs, it targets an **organization-owned** publisher using Microsoft Entra
workload identity federation. Personal ownership is rejected up front because `microsoft/vscode-vsce#1023`
reports federated service principals failing publish on a **personally-owned** publisher (closed
`not_planned`), and publisher ownership is effectively irreversible once extensions are published under it.

### 5. Versioning

- **Scheme:** SemVer 2.0, `0.MINOR.PATCH-preview.N`, remaining in `0.x` for the whole preview.
- **Derivation: MinVer**, from the nearest reachable git tag. `<Version>` is **deleted** from
  `SpecScribe.csproj` rather than replaced by a second literal. Chosen over Nerdbank.GitVersioning (needs a
  committed `version.json` — a second home for the version) and over `-p:Version=` from the tag (works only
  in CI; a local `dotnet pack` would then produce `1.0.0` and silently drop the Preview badge).
- **Every preview release carries a SemVer pre-release label.** This is not cosmetic:
  `AboutTemplater.cs:133-135` renders the About page's `Preview` badge from `meta.IsPrerelease`. The first
  release without the label is by definition no longer a preview.
- **The VS Marketplace is the documented exception.** It has no SemVer pre-release concept, so
  `extension/package.json` keeps a plain `0.1.0` and pre-release status is carried by the Marketplace's own
  Preview flag plus `vsce publish --pre-release`.
- **CLI and renderer are pinned as one released unit.** For the NuGet and binary channels this is structural
  — there is only one artefact. For npm, where § Decision 1 makes the renderer a separate package, the
  wrapper depends on `specscribe-renderer` with an **exact** version pin (`=X.Y.Z`, never `^`), published from
  the same tag in the same pipeline run. Story 16.9 AC #2 inherits this rule: a caret range is a licence to
  produce the mismatched pair that *"fails as wrong output rather than as an error"*.

### 6. Changelog

**Keep a Changelog 1.1.0**, `CHANGELOG.md` at the repository root, **hand-authored in the story that makes
the change**. Generated release notes are rejected because this repository's commits routinely bundle several
stories (CLAUDE.md § Concurrent work) — the commit is not the unit of change here, the story is. The release
pipeline copies the released version's section into the GitHub Release body; it does not author it.

### 7. NFR9 reproducibility — the weaker reading is claimed, explicitly

**"Reproducible" means _built from a clean checkout by CI_, not byte-identical rebuilds.** NFR9's own wording
supports this and it is stated so no reader assumes the stronger guarantee.

The preview closes: version-from-tag (16.3), `SOURCE_DATE_EPOCH` set by the release workflow (16.4 — the
csproj already honours it at `SpecScribe.csproj:28,36-37`), and a working `npm ci` (16.2).
It defers: `<Deterministic>` / `ContinuousIntegrationBuild` / SourceLink, and deterministic Nuxt builds.

**The weak reading is not currently satisfied.** `npm ci` fails at `838d591` on a clean checkout with
npm 11.16.0 (`Missing: @emnapi/runtime@1.11.3 from lock file`), and three CI steps depend on it. CI pins
Node 24.11.1 via `web/.nvmrc` and may therefore still be green — unverified. Story 16.2 owns closing this
before 16.4 builds a release pipeline on top of it.

### 8. Node prerequisite — placement, and the npx install-time check

- **The Node check stays where it shipped: at prerender time, in `NuxtPrerender`** (`NuxtPrerender.cs:141-216`).
  **This amends ADR 0022 §Decision 5's "detects Node at startup" wording to match the implementation.**
  Rationale: "at startup" moves a subprocess spawn into every invocation — including `--help` and `--version`
  — to warn about a dependency only the prerender path needs. The status quo pays that cost once, for a user
  about to hit the error anyway.
- **No install-time Node check for npx** (closing ADR 0022's open owner question 1). npm invokes npx, so Node
  is present by construction; the real risk is *version*, already covered by `SupportedNodeRange`. The npm
  wrapper declares `engines.node` so npm warns without executing anything. **A postinstall script is
  rejected** — it runs arbitrary code on install, is skipped by `--ignore-scripts`, and would surface a
  failure at install time for a tool that may never be run.
- The Node prerequisite is a **consumer-facing condition of use** and must appear where a packaged consumer
  sees it — the NuGet listing, the npm README and the Marketplace listing (Story 16.6). Today it is stated
  only in `README.md:92-98`.

### 9. The CI gate applies to a tag by requiring the tagged commit to be green on `main`

The release pipeline is tag-triggered; `build-test-analyze.yml` is push/PR-triggered. NFR9's *"publishing …
gated on a passing build + test run"* is satisfied by **requiring that the tagged commit already passed on
`main`**, not by re-running build+test inside the release job. Re-running invites a different result from the
same source and doubles the wall-clock of every release.

The required-check string is the **job name verbatim: `build-test-analyze`**.
`portability-probe (ubuntu, non-gating)` carries `continue-on-error` at the job level and **must not** be
made required. Per epics.md § Story 16.2 (AMENDED 2026-07-25), **do not create a second build+test workflow**.

## Consequences

**Positive**
- The renderer/CLI two-halves problem disappears for every packaged consumer. Story 16.9's composite Action
  collapses to install-and-run, and `README.md`'s external CI recipe loses its `SPECSCRIBE_RENDERER_DIR` step.
- The version-mismatch failure class Story 16.9 AC #2 fears is **structurally impossible** on two of three
  channels and exact-pinned on the third.
- No long-lived publishing credential exists to leak or rotate for either shipping channel.
- The 1.18 MB package cost is small enough that no channel is priced out by it.
- The two channels leading the preview cut install through package managers, so neither triggers SmartScreen
  nor Gatekeeper — which makes the no-code-signing decision materially cheaper than it first appears.

**Negative / trade-offs**
- **The VSIX is not in the first preview.** FR33 is deferred, not dropped, and the Marketplace path now
  carries an identity-federation setup cost that a PAT would not have.
- The unsigned direct-download binary is the roughest preview experience: SmartScreen on Windows, Gatekeeper
  on macOS. Accepted for the preview and documented rather than hidden (Story 16.6).
- The npm channel is the one place a CLI/renderer mismatch remains *expressible*; it is prevented by an exact
  pin, i.e. by policy plus a pipeline invariant, not by construction.
- Adding a RID later remains a real cost (~76 MiB / ~34 MB gzipped each), even though the renderer no longer
  multiplies with it.
- MinVer makes every build depend on git tag reachability; a shallow clone without tags produces a wrong
  version rather than an error. Story 16.3 must set `fetch-depth: 0`.
- "Reproducible" is claimed in its weaker sense only, and is not yet true even in that sense.

## Relationship to ADR 0006 — this AMENDS it

ADR 0006 §Decision names the channels and calls npx *"the primary CLI channel"* with `dotnet tool` secondary.
**That ordering is preserved as a statement of audience, but the preview *ships* `dotnet tool` first**
(§ Decision 2) — not because it matters more, but because it is already wired, needs no RID matrix, and was
the channel proven end-to-end by this spike. ADR 0006's channel *list* is unchanged; what is added is the
packaging shape it never specified and an ordered cut it never had.

ADR 0006 §Consequences already anticipated the cost this ADR now bounds: *"Distribution now maintains two
channels … and a per-RID native-package matrix."* § Decision 2 fixes that matrix at three RIDs for the preview.

## Relationship to ADR 0022 — this AMENDS it

- **§Decision 5's "The binary detects Node at startup"** is amended to *at prerender time*, matching the
  shipped implementation (§ Decision 8). The ADR led its implementation on this point; the implementation is
  the better answer and the ADR moves to it.
- **Its two "left to the owner" questions are closed here.** Question 1 (install-time Node check for npx):
  **no** — § Decision 8. Question 2 (`web/` coverage warranting a component-test story) is **not** closed by
  this ADR; it is a testing-scope question, unrelated to packaging, and remains open.
- **§Consequences' "Story 16.1/16.4 must add the `npm run build:package` stage"** is discharged as a
  *decision* here and assigned to Story 16.4 as *implementation*.
- Everything else in ADR 0022 stands unchanged: Node is never a shipped toolchain, the artefact carries no
  prerendered pages, the IR resolves at server runtime, and asset paths stay page-relative.

## References

- **This spike:** [Story 16.1](../../_bmad-output/implementation-artifacts/16-1-release-and-distribution-packaging-spike.md) ·
  [16-1-spike-report.md](../../_bmad-output/implementation-artifacts/16-1-spike-report.md) (measurements, provenance, false starts)
- **Predecessor:** [23-5-packaging-strategy-report.md](../../_bmad-output/implementation-artifacts/23-5-packaging-strategy-report.md) — open items 3–6 are Epic 16's; this ADR takes 4, 5 and 6.
- [ADR 0006 — Delivery Architecture & Distribution](0006-delivery-architecture-and-distribution.md)
- [ADR 0022 — Node Is a Build-Time Toolchain and a Generate-Time Runtime](0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md)
- [ADR 0034 — The IR Is the Product and the Site Is Rendered From It](0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) — why the renderer is mandatory, not optional
- [ADR 0008 — JSON IR as Canonical Representation](0008-json-ir-canonical-and-incremental-generation.md) — the versioned output format the preview declines to freeze
- `src/SpecScribe/NuxtPrerender.cs:41,66-127,141-216` — artefact resolution, Node range, assertions
- `src/SpecScribe/SpecScribe.csproj:14-16,19,28,36-37` — tool packaging, version literal, `SOURCE_DATE_EPOCH` stamp
- `src/SpecScribe/AboutTemplater.cs:90,133-135` — informational-version parsing and the Preview badge
- `.github/workflows/build-test-analyze.yml:1-13,246,281-290,416` — the gate, `npm ci`, the `--deep-git` requirement
- [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) ·
  [npm trusted publishers](https://docs.npmjs.com/trusted-publishers/) ·
  [Retirement of Global PATs in Azure DevOps](https://devblogs.microsoft.com/devops/retirement-of-global-personal-access-tokens-in-azure-devops/) ·
  [VS Code — Publishing Extensions](https://code.visualstudio.com/api/working-with-extensions/publishing-extension) ·
  [microsoft/vscode-vsce#1023](https://github.com/microsoft/vscode-vsce/issues/1023) ·
  [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/) ·
  [MinVer](https://github.com/adamralph/minver)
