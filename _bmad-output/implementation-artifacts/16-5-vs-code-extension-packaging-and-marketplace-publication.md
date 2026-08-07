---
baseline_commit: 07bdb79 # local `main` HEAD AND `origin/main` at authoring time (2026-08-07) — they are equal,
                         # unlike 16.2's authoring state. Tree clean. Every repo fact below was read at 07bdb79.
epic: 16
frs: [FR33] # "VS Code extension packaging and Marketplace publication (depends on Epic 6)" (epics.md:246)
nfrs: [NFR9] # reproducible-from-clean-checkout, publish gated on a passing build+test run
depends_on:
  - 16-1 # ADR 0040 §4 (publisher/credential posture) + §5 (versioning) + §1 (renderer rides inside the package)
  - 16-2 # the required `build-test-analyze` gate ADR 0040 §9 makes a release tag inherit
  - 16-3 # produces the self-contained per-RID binary this story would bundle, and wires MinVer version-from-tag
  - 16-4 # produces the release pipeline AC #2 says this story "extends"; it DOES NOT EXIST YET — see R1
  - 6-8  # R5.4 Workspace-Trust posture, the spike's stated prerequisite — ALREADY SHIPPED, see R9
blocks: [16-7] # the preview-cut readiness pass verifies "the extension install if Epic 6 shipped" (epics.md)
informs: [16-6] # Marketplace listing copy + the Node prerequisite on a consumer-facing surface (ADR 0040 §8)
amends: null # No structural scope change is planned. ⚠️ ONE CONDITIONAL: if R2 resolves the way the evidence
             # points, ADR 0040 §4 must be AMENDED by a new ADR. That is a decision record, not a scope edit.
ships_product_code: true # Edits extension/** (manifest, assets, walkthrough) and .github/workflows/**.
                         # Does NOT edit src/**, web/, or tests/**.
decides: conditional # See R2. A new ADR is required ONLY IF `vsce publish --oidc` proves viable, because that
                     # amends ADR 0040 §4's organization-owned-publisher decision. CLAUDE.md § Decision records
                     # requires proposing it rather than burying it as a story note.
owner_decisions_required: 3 # publisher ownership (R2), fat-vs-thin VSIX (R6), publisher-ID reservation (R8).
                            # NONE were pre-locked at create-story — unlike 16.2, these need the owner.
deliverables:
  - "extension/package.json (Marketplace-ready manifest: publisher, icon, categories, keywords, repository, walkthrough)"
  - "extension/media/*.png (a RASTER icon — vsce rejects SVG icons by name, see R4)"
  - "extension/LICENSE (vsce looks for one beside the manifest; the repo root's is not seen)"
  - ".github/workflows/** (the Marketplace publish job — new file or an extension of 16.4's release pipeline)"
  - "docs/adrs/00NN-*.md (CONDITIONAL — only if R2 unmakes ADR 0040 §4)"
---

# Story 16.5: VS Code Extension Packaging and Marketplace Publication

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a VS Code user,
I want the read-only SpecScribe extension available from the Marketplace,
So that I can install it without building from source.

| | |
|---|---|
| **Epic** | 16 — Release Engineering & Community Preview Launch |
| **Covers** | FR33 · NFR9 |
| **Governed by** | [ADR 0040](../../docs/adrs/0040-release-channels-and-versioning-policy.md) §1, §2, §4, §5, §9 · [ADR 0005](../../docs/adrs/0005-vs-code-integration-architecture.md) · [ADR 0006](../../docs/adrs/0006-delivery-architecture-and-distribution.md) |
| **Owns recommendations** | R1.4 (walkthrough) · R1.6 (Marketplace metadata) · **R8.1** (platform-specific VSIX targets — *not* R8.2, see R10) |

---

## ⛔ Read first — eleven reconciliations against the live repository and the live publishing toolchain

Every claim below was verified at `07bdb79` on 2026-08-07, against the working tree, against `vsce`'s own
source, and against the current VS Code publishing documentation. Where a prior artifact is wrong, the
correction is stated and attributed. **Read all eleven before writing a line.**

### R1 — The Epic-6 gate (AC #3) is SATISFIED. The *ordering* gate is not, and it is the reason not to start yet.

AC #3 keeps this story blocked "until the extension surface exists". It exists:

- `sprint-status.yaml` — **`6-1` through `6-12` are all `done`**, and `epic-6-retrospective: done` (2026-07-12).
- `extension/` carries a real runtime: `src/extension.ts` is **2,398 lines**, with a webview host, an outline
  tree view, diagnostics, and twelve contributed commands.

⚠️ **The `epic-6:` key itself still reads `in-progress`** (`sprint-status.yaml:143`) while all twelve of its
stories and its retrospective are `done`. That is **stale drift, not an open gate** — do not read it as a
block, and do not silently "fix" it either; it is outside this story's scope guard (AC #7).

**What is genuinely not ready** — this is the part AC #3 does not cover:

| precondition | state at `07bdb79` |
|---|---|
| A release pipeline for AC #2 to extend | ❌ **Does not exist.** `.github/workflows/` contains exactly `build-test-analyze.yml` and `publish-docs-live-pages.yml`. Story 16.4 creates it and is `backlog`. |
| A self-contained binary to bundle (if R6 goes fat) | ❌ Story 16.3 produces it; `backlog`. |
| Version-from-tag (MinVer) | ❌ `SpecScribe.csproj:19` still carries a literal `<Version>0.1.0-preview</Version>`. Story 16.3 deletes it. |
| A green, required CI gate | ❌ Story 16.2 is `ready-for-dev`, not done. |
| ADR 0040 ratified | ⚠️ Still **`Proposed`**. Story 16.1 AC #4 asked the owner to ratify. |
| ADR 0040 §2's preview cut | 🚩 Puts **"VSIX / VS Marketplace is OUT of the first preview"**. |

**So:** this story file is ready; **`dev-story` is not**. Work the § Preflight checklist first. Nothing here
is wasted — the R2 credential question should be answered *early* precisely because it is irreversible.

### R2 — 🚨 `vsce publish --oidc` exists, and it may unmake ADR 0040 §4's explicitly irreversible decision. VERIFY BEFORE YOU WIRE ANYTHING.

**This is the single highest-value item in the story. Do it first.**

ADR 0040 §4 decided: *"when Story 16.5 runs, it targets an **organization-owned** publisher using Microsoft
Entra workload identity federation"*, and justified rejecting personal ownership up front because
`microsoft/vscode-vsce#1023` reports federated service principals failing on a **personally-owned** publisher,
and *"publisher ownership is effectively irreversible once extensions are published under it."*

**That decision weighed exactly two mechanisms: a PAT (closed) and `--azure-credential` (Entra SP).** Current
`vsce` documents a **third**, which the 16.1 spike never evaluated:

```yaml
permissions:
  contents: read
  id-token: write
steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-node@v4
    with:
      node-version: 22
  - run: npm ci
  - run: npx @vscode/vsce publish --oidc
```

Per `vsce`'s own README: *"OIDC publishing requests a GitHub Actions token for the
`marketplace.visualstudio.com` audience and exchanges it for a short-lived Marketplace credential"*, after you
*"configure a trusted publishing policy for the repository and workflow on the Visual Studio Marketplace"*. It
*"does not fall back to a PAT when token acquisition or exchange fails"* — so a misconfiguration fails loudly
rather than silently reaching for a secret.

**Why this matters so much here:**

1. It is the **exact structural analogue** of the Trusted Publishing that ADR 0040 §3 already chose for
   nuget.org and npm. If it works, AC #2's *"no secret value is committed"* becomes **structural on all three
   channels**, not two — which is a materially better outcome than the ADR currently claims.
2. **Publisher ownership is not documented as a constraint for OIDC.** The #1023 failure is specific to
   `--azure-credential`, where a *service principal* must be added as a **member of the publisher** — a
   publisher-membership problem. A trusted-publishing policy is keyed to a **repository + workflow**, not to a
   directory principal, so the membership problem plausibly does not arise. **Plausibly is not verified.**
3. Setting up an Azure DevOps organization + Entra tenant is a real, non-trivial, and largely irreversible
   cost to impose on the owner. Do not impose it before checking whether it is necessary.

**Required action, in this order — and note the asymmetry: the *cheap* check gates the *expensive* commitment.**

1. Confirm on the live Marketplace publisher-management UI whether trusted publishing policies are offered,
   and whether the option is available for a **personally-owned** publisher.
2. **If yes → do not create an organization.** Use `--oidc` with a personal publisher, and **propose an ADR
   amending ADR 0040 §4.** CLAUDE.md § Decision records is explicit: *"Propose an ADR without being asked for
   any decision that changes shared architecture, a cross-cutting contract, **or amends a prior ADR**. Do not
   bury such a decision as an owner-locked note in a story file."*
3. **If no →** ADR 0040 §4 stands as written; proceed to the organization-owned publisher + Entra federation,
   and record the evidence that closed the question so the next reader does not re-open it.

**Do not treat ADR 0040 §4 as unchallengeable.** CLAUDE.md's rule is that *a ratified ADR outranks stale
memory* — but ADR 0040 is `Proposed`, and this is new evidence about the very mechanism it chose between.
Equally: do not just assume `--oidc` wins. **Verify, then record.**

### R3 — The Marketplace hard-rejects this project's own versioning scheme. ADR 0040 already anticipated it — do not "fix" what looks broken.

ADR 0040 §5 sets `0.MINOR.PATCH-preview.N` (SemVer 2.0) for the project. The VS Code publishing documentation
states flatly: **"We only support `major.minor.patch` for extension versions"** and **"semver pre-release tags
are not supported"**. So `0.1.0-preview` is *rejected by the Marketplace*.

ADR 0040 §5 already carved this out and it is **correct** — it is *"the documented exception"*: the manifest
keeps a plain `0.1.0`, and pre-release status is carried by the Marketplace's own Preview flag plus
`vsce publish --pre-release`. The spike went out of its way to record this so that *"nobody 'fixes' it into
`0.1.0-preview` and breaks the Marketplace parse."* **Heed that. `extension/package.json:5` stays `0.1.0`.**

**Two things ADR 0040 did *not* decide, which this story must:**

- **The odd/even convention.** VS Code recommends `major.EVEN.patch` for releases and `major.ODD.patch` for
  pre-releases, because VS Code auto-updates to the highest available version — without the split, a
  pre-release user gets silently pulled onto a release build. `0.1.x` is already **odd**, so a preview-only
  VSIX is consistent today. Adopt the convention explicitly and write it down, so `0.2.0` is not casually used
  for the next preview.
- **The tag → VSIX version mapping.** The release tag will read `v0.1.0-preview.N` (MinVer). The VSIX version
  must be `0.1.N` (plain). State the transform in the workflow; do not leave it implicit.

**One thing that is NOT affected, so do not chase it:** the About page's `Preview` badge
(`AboutTemplater.cs:133-135`) renders from the **CLI's** informational version, not the extension manifest's.
Dropping `-preview` from the VSIX does not remove that badge.

### R4 — AC #1's icon cannot be satisfied with any asset that exists. `vsce` rejects SVG icons *by name*.

`vsce`'s `validateManifestForPackaging()` tests `/\.svg$/i` against `manifest.icon` and fails with:

```
SVGs can't be used as icons: <path>
```

Current state, verified at `07bdb79`:

- `extension/media/` contains **only** `specscribe.svg` and `specscribe-outline.svg`.
- **There is no `.png` anywhere in the repository** (`find . -name "*.png"` excluding `node_modules`/`.git`
  returns nothing).
- `extension/package.json` has **no `icon` field at all** today.

So AC #1 requires **producing a raster icon** (128×128 PNG is the conventional size).

⚠️ **This lands directly on debt already routed to this story, and doing it carelessly makes that debt worse.**
`deferred-work.md` routes three items to *"Story 16.5's asset pipeline"*:

- the Scribe's Nib geometry kept in **three hand-maintained renditions**, plus a fourth (the favicon)
  duplicating the brand palette, with **no sync guard**;
- *"nothing mechanical pins the extension assets to the [C#] const — tests run against the compiled assembly,
  not repo-relative asset files, and no build step spans C#/SVG"*, asking for a repo-relative asset test **or a
  generation step** when 16.5 builds its asset pipeline;
- the social-card item notes the project *"ships self-contained with no image dependency/pipeline"*, so a
  committed raster is **new manual-sync debt** by construction.

**A hand-drawn PNG would be a fifth unpinned rendition.** Either derive it from the existing SVG in a build
step, or add the repo-relative sync guard alongside it. Do not add rendition five with no guard.

### R5 — `"private": true` is the 16.1 spike's open item #7. The answer is *"not a blocker"* — but spend 30 seconds confirming it rather than trusting this paragraph.

The spike deliberately left this untested and assigned it here: *"Confirm whether `"private": true` blocks
`vsce package` — **not confirmed by this spike**, it is 16.5's to check on a manifest it owns."*

Evidence gathered 2026-08-07:

- `vsce`'s `src/package.ts` contains **no reference to `manifest.private`** whatsoever.
- `microsoft/vscode-vsce#597` reports that *both* `vsce package` and `vsce publish` **ignore** the setting and
  always produce a public extension (the resulting `extension.vsixmanifest` carries a `Publish` node
  regardless).

**Conclusion: it does not block packaging.** It should still be **removed** from the manifest, because it now
asserts something false about an extension that is about to be published. Confirm with one real
`vsce package` run before relying on this — the check is nearly free and the source read above covers one
file, not the whole tool.

### R6 — What the VSIX actually *ships* is undecided, and it is this story's biggest fork. The seam is already built and empty.

`extension/src/extension.ts:1993-1999`:

```ts
function resolveTool(context: vscode.ExtensionContext): ResolvedTool {
  const configured = vscode.workspace.getConfiguration('specscribe').get<string>('toolPath')?.trim();
  const bundled = path.join(context.extensionPath, 'bin', process.platform === 'win32' ? 'specscribe.exe' : 'specscribe');
  const tool = configured || (fs.existsSync(bundled) ? bundled : 'specscribe');
  ...
```

Its doc comment (`:1985-1987`) reads: *"explicit setting → binary bundled with the extension **(populated by
Story 16.5's packaging)** → `specscribe` on PATH."* And `extension/.gitignore` already ignores `bin/` and
`*.vsix`. **The seam exists, the destination is pre-ignored, and nothing populates it** — precisely the class
of gap ADR 0040 was written to close for `renderer/`.

🚨 **The binary alone is not sufficient, and this is easy to get wrong.** `extension.ts:405-406` registers:

```ts
register('specscribe.generateSite', () => stageTerminalCommand(context, 'generate'));
register('specscribe.watch',        () => stageTerminalCommand(context, 'watch'));
```

Both route through the **same `resolveTool`**. ADR 0040 §1 measured that `generate` requires a sibling
`renderer/` directory beside the executable. **So a bundled binary must ship `bin/renderer/**` as well** —
otherwise Generate/Watch resolve the bundled binary and then fail on a missing renderer, which is exactly the
failure ADR 0040 exists to prevent (*"a published tool that resolves no renderer and tells its user to build a
Nuxt artefact"*). Budget from 16.1's measurements: **~76 MiB binary + 3.96 MB renderer, per platform target.**

**The fork:**

| | **(a) Thin VSIX — RECOMMENDED default** | **(b) Fat, platform-targeted VSIX (R8.1)** |
|---|---|---|
| ships | shim only (~tens of KB) | shim + `bin/specscribe[.exe]` + `bin/renderer/**` |
| tool resolution | `toolPath` setting → `specscribe` on PATH | bundled binary wins automatically |
| user must | install the CLI (`dotnet tool`/npx — **the channels the preview ships first**) | nothing |
| `--target` matrix | none | `win32-x64`, `linux-x64`, `darwin-arm64` (mirrors ADR 0040 §2's three RIDs — **note the rename**) |

⚠️ **`vsce --target` does not speak .NET RIDs.** ADR 0040 §2's matrix is `win-x64`, `linux-x64`, `osx-arm64`;
the Marketplace's platform identifiers are `win32-x64`, `linux-x64`, **`darwin-arm64`**. `osx-arm64` and
`win-x64` are **not valid `--target` values**. The full supported set is `win32-x64`, `win32-arm64`,
`linux-x64`, `linux-arm64`, `linux-armhf`, `alpine-x64`, `alpine-arm64`, `darwin-x64`, `darwin-arm64`, `web`.
| depends on | nothing further | **Story 16.3** must exist first |
| risk | user hits "is the tool installed?" | ~80 MB × 3, unverified size ceiling (R7) |

**Recommendation: (a) thin for the preview**, with (b) recorded as the follow-on. Rationale: ADR 0040 §2 ships
`dotnet tool` and npx *first*, so by the time the VSIX publishes the CLI is a one-line install; the extension
already degrades gracefully with an actionable error and a "Set `specscribe.toolPath`" affordance
(`extension.ts:1946-1951`); and (b) hard-blocks on 16.3 which has not started. **This is an owner decision —
see § Owner actions. Do not silently pick one.**

### R7 — The Marketplace size ceiling is *undocumented*. If R6 goes fat, measure it; do not assume it.

`microsoft/vsmarketplace#1541` ("What is the maximum allowed VSIX / extension size?") is **open with no
official answer**, and the publishing documentation states no limit. A ~80 MB platform-targeted VSIX is
therefore **unverified, not known-good**. If option (b) is chosen, produce a real VSIX, record its measured
byte size in the story record, and prove one actually uploads before committing to the three-target matrix.
Consistent with this project's standing rule: measure, don't infer.

### R8 — Manifest gaps `vsce` will name, and the publisher ID is still free.

Verified against `extension/package.json` at `07bdb79`:

| field | today | required by |
|---|---|---|
| `private: true` | present | remove (R5) |
| `icon` | **absent** | AC #1 — must be PNG (R4) |
| `repository` | **absent** | AC #1. `vsce` warns: *"A 'repository' field is missing… Use `--allow-missing-repository` to bypass"* — **do not bypass it** |
| `categories` | `["Other"]` | R1.6 → `["Visualization", "Other"]` |
| `keywords` | **absent** | R1.6 → spec-driven development, BMAD, dashboard. **Max 30** (`"You exceeded the number of allowed tags of 30"`) |
| `LICENSE` file | **absent from `extension/`** (repo root has one) | `vsce` looks beside the manifest; `license: "MIT"` is already declared |
| `publisher` | `"specscribe"` | `marketplace.visualstudio.com/publishers/specscribe` → **HTTP 404 on 2026-08-07** = unclaimed, consistent with the nuget.org/npm IDs 16.1 verified. **Reserving it is an owner action.** |
| `displayName` / `description` / `engines.vscode ^1.90.0` | present and adequate | — |

Also required by AC #1 and R1.4: **`contributes.walkthroughs`** — a 4-step first run, specified in
`docs/VSCodeIntegrationRecommendations.md:45`: detect/open a spec-driven repo → open the status panel → what
"read-only companion" means → where full-site generation lives (`specscribe generate` / watch). Walkthroughs
surface automatically on install; R1.4 calls this *"the single best onboarding lever for the Story 16.5
Marketplace launch."*

⚠️ Marketplace asset rule: **the icon may not be an SVG**, and badges/images in the README must be HTTPS and
non-SVG unless from a trusted provider. `extension/README.md` (16.5 KB) is developer-facing today — it opens
with an F5 dev-host workflow. It needs a consumer-facing rewrite for the listing; **coordinate with Story 16.6**,
which owns release-facing documentation and, per ADR 0040 §8, owns surfacing the **Node prerequisite** on the
Marketplace listing.

### R9 — The stated prerequisite (R5.4, Workspace Trust) is ALREADY SHIPPED. Verify it; do not re-engineer it.

The 16.1 spike made *"Story 6.8's Workspace-Trust posture"* a prerequisite of this publish, and R5.4 calls it
*"required reading for the 16.5 Marketplace review bar"*. It is already in the manifest, matching R5.4's
recommendation exactly:

```json
"capabilities": {
  "untrustedWorkspaces": {
    "supported": "limited",
    "restrictedConfigurations": ["specscribe.toolPath"]
  }
}
```

Confirm it survives your manifest edits. That is the whole task. This is the 16.2 pattern repeating: a
precondition asserted as open that is in fact closed — check before you build.

### R10 — `epics.md` mis-cites the platform-target recommendation. It is R8.1, not R8.2.

The Story 16.5 comment in `epics.md` credits *"R8.2 (platform-specific VSIX targets)"*. That is wrong:

- **R8.1** (`docs/VSCodeIntegrationRecommendations.md:100`) *is* platform-specific VSIX targets, and says
  *"(Story 16.5)"* in its own title.
- **R8.2** (`:101`) is *"Story 6.7 (JSON+SPA) as a webview accelerant"* — a different subject entirely.
- The roadmap table (`:111`) routes **"R1.4 + R1.6 + R8.1 → 16.5"**, confirming R8.1.

Cite **R8.1**. Correcting the `epics.md` comment is a one-line editorial fix; if you make it, say so in the
File List. R8.1 also records a detail worth keeping: a framework-dependent **"thin" variant as the documented
opt-in for .NET-runtime holders** — which is R6 option (a) under another name.

### R11 — Pin `vsce`. The current package script floats `@latest` inside a release path.

`extension/package.json` `scripts.package`:

```
node esbuild.js --production && npx --yes @vscode/vsce@latest package --no-dependencies
```

`@latest` resolves differently on every run, which contradicts NFR9 even in the weak sense ADR 0040 §7 claims
(*"built from a clean checkout by CI"*). **Pin an exact `@vscode/vsce` version**, ideally as a `devDependency`
so `extension/package-lock.json` records it. Note `vsce` requires **Node ≥ 22**; CI pins Node 24.11.1, which
satisfies it.

---

## ✅ Preflight — do not run `dev-story` until these are true

1. [ ] Story 16.2 done — the `build-test-analyze` gate is green and required (ADR 0040 §9).
2. [ ] Story 16.3 done — version-from-tag (MinVer) wired; **and, only if R6 chooses fat**, a self-contained
       per-RID binary is produced.
3. [ ] Story 16.4 done — a release pipeline exists for AC #2 to extend. *Or* the owner accepts a standalone
       parallel workflow (AC #2 permits *"or a parallel job"*).
4. [ ] **ADR 0040 ratified** (`Proposed` → `Accepted`), and ADR 0040 §2's "VSIX is out of the first preview"
       consciously revisited — this story publishes the thing that decision deferred.
5. [ ] The three § Owner actions below are answered.

**R2's verification is the exception: do it early, out of order.** It is cheap, it is irreversible if gotten
wrong, and its answer determines whether the owner must stand up an Azure DevOps organization at all.

---

## Acceptance Criteria

**AC #1, #2 and #3 are `epics.md` verbatim.** #4–#7 are added by create-story and are traceable to the
reconciliations above.

1.
**Given** the Epic 6 extension exists
**When** the extension is packaged
**Then** a valid VSIX is produced reproducibly with a Marketplace-ready manifest (publisher, display name,
description, icon, categories, repository link) and versioning aligned to Story 16.1's policy.

2.
**Given** the VSIX and a configured publisher
**When** a release publishes the extension
**Then** it appears on the VS Code Marketplace as a read-only preview and installs cleanly
**And** publication is automatable (extends the Story 16.4 pipeline or a parallel job) rather than a manual
one-off.

3.
**Given** Epic 6 is not yet complete
**When** this story is scheduled
**Then** it remains blocked/backlog and is not started until the extension surface exists.

> **Status of AC #3: SATISFIED at create-story.** Epic 6's stories 6.1–6.12 and its retrospective are all
> `done`; `extension/src/extension.ts` is a 2,398-line runtime. Record this in the completion notes with the
> evidence, and note the stale `epic-6: in-progress` key (R1) without editing it.

4. **(Runtime resolution — the empty seam)**
**Given** `resolveTool` already probes `<extensionPath>/bin/specscribe[.exe]` and its doc comment attributes
populating it to this story (R6)
**When** the packaging shape is chosen
**Then** the choice between a thin VSIX and a bundled-runtime VSIX is **recorded with its rationale**
**And if** a runtime is bundled, the VSIX ships `bin/renderer/**` beside the binary and a **measured** proof is
recorded: install the built VSIX into a clean VS Code, open a repository that is **not** this one, and run
Generate to completion with `SPECSCRIBE_RENDERER_DIR` unset and no `specscribe` on `PATH`
**And if** it is thin, the "tool not found" path is verified to surface the actionable
`Set specscribe.toolPath` affordance rather than a bare failure.

> Running the proof inside this repository is a **false pass** — `NuxtPrerender`'s third candidate
> (`web/.output` at the repo root) rescues it. This is the identical trap 16.1 documented and avoided.

5. **(Credential posture — no stored secret, and no irreversible choice made blind)**
**Given** ADR 0040 §4 chose an organization-owned publisher without having evaluated `vsce publish --oidc` (R2)
**When** this story sets up publishing
**Then** trusted publishing via `--oidc` is **verified or refuted against the live Marketplace first**, and the
result is recorded with evidence
**And** publisher ownership (personal vs organization) is chosen only after that answer
**And if** `--oidc` proves viable, an ADR amending ADR 0040 §4 is **proposed** (CLAUDE.md § Decision records)
**And** no secret value is committed to the repository, and `id-token: write` is scoped to the publish job only.

6. **(Marketplace launch surface — R1.4 + R1.6)**
**Given** walkthroughs surface automatically on install
**When** the extension is published
**Then** `contributes.walkthroughs` provides the 4-step first run specified in R1.4
**And** the manifest carries real `categories` (`Visualization`, `Other`), `keywords` (≤30), an icon, and a
`repository` link — with `--allow-missing-repository` **not** used
**And** `capabilities.untrustedWorkspaces` (R5.4, shipped by Story 6.8) is confirmed intact
**And** the icon is a raster image that does not add an unpinned rendition of the Scribe's Nib (R4).

7. **(Scope guard)**
**Given** commits in this repository routinely bundle sibling stories (CLAUDE.md § Concurrent work)
**When** this story is implemented
**Then** changes are confined to `extension/**`, `.github/workflows/**`, `docs/adrs/**` (only under AC #5), and
`docs/VSCodeIntegrationRecommendations.md` / `epics.md` for the R10 citation fix
**And** `src/**`, `tests/**` and `web/**` are untouched — verified with `git status --porcelain`, not assumed.

---

## Tasks / Subtasks

- [ ] **Task 0 — Preflight and the irreversible question** (AC: #3, #5)
  - [ ] Confirm the § Preflight checklist; if any item is false, **stop and report** rather than working around it.
  - [ ] Record AC #3's satisfaction evidence (Epic 6 story statuses + `extension.ts` line count).
  - [ ] **R2:** check the live Marketplace publisher UI for trusted-publishing policy support, and whether it is
        offered for a **personally-owned** publisher. Record the finding verbatim with the date.
  - [ ] Put the publisher-ownership recommendation to the owner **with** that evidence. Do not proceed past
        this point on an assumption.

- [ ] **Task 1 — Manifest: Marketplace-ready** (AC: #1, #6)
  - [ ] Remove `"private": true` (R5); confirm empirically with one `vsce package` run.
  - [ ] Add `repository`, real `categories`, `keywords` (≤30); keep `version` at plain `0.1.0` (**R3 — do not
        add `-preview`**); confirm `capabilities.untrustedWorkspaces` is intact (R9).
  - [ ] Add `extension/LICENSE` (MIT, matching the declared `license` and the repo-root LICENSE).
  - [ ] Pin `@vscode/vsce` to an exact version as a devDependency; drop `@latest` from `scripts.package` (R11).

- [ ] **Task 2 — The icon, without adding unpinned debt** (AC: #1, #6)
  - [ ] Produce a raster PNG icon (128×128). **SVG is rejected by name** (R4).
  - [ ] Derive it from the existing SVG in a build step, **or** add the repo-relative asset sync guard that
        `deferred-work.md` asks this story's asset pipeline to provide. Do not ship rendition five unpinned.
  - [ ] Note in the story record which of the two you did, and what remains deferred.

- [ ] **Task 3 — Walkthrough (R1.4)** (AC: #6)
  - [ ] `contributes.walkthroughs`, 4 steps per `VSCodeIntegrationRecommendations.md:45`.
  - [ ] Verify each step's completion event actually fires in a dev host — a walkthrough step that never
        completes is worse than none.

- [ ] **Task 4 — Packaging shape** (AC: #4)
  - [ ] Implement the owner's R6 choice.
  - [ ] **If fat:** ship `bin/renderer/**`; add `--target` for `win32-x64`, `linux-x64`, `darwin-arm64`
        (mirroring ADR 0040 §2's RIDs); **measure and record the VSIX byte size** (R7) and prove an upload.
  - [ ] **If thin:** verify the not-found path surfaces the `Set specscribe.toolPath` affordance.
  - [ ] Confirm `.vscodeignore` excludes sources but **not** `bin/` if bundling. Current contents: `src/**`,
        `node_modules/**`, `esbuild.js`, `tsconfig.json`, `.gitignore`, `**/*.map`.
  - [ ] Run the AC #4 foreign-repository proof. **Not inside this repo** — that is a false pass.

- [ ] **Task 5 — Publish automation** (AC: #2, #5)
  - [ ] Add the publish job (extending 16.4's pipeline, or a parallel workflow if the owner accepts).
  - [ ] Wire the R2 outcome: `--oidc` with `permissions: {contents: read, id-token: write}`, **or**
        `--azure-credential`. Scope `id-token: write` to the publish job only.
  - [ ] Define, document and implement the tag → VSIX version transform, and pass `--pre-release`. The tag is
        `v0.1.0-preview.N`; the VSIX must be plain `major.minor.patch`. **The mapping is a decision, not a
        given** — e.g. `0.1.0-preview.N` → `0.1.N` keeps the preview counter monotonic in the patch field, but
        whatever you choose must be monotonically increasing across releases (the Marketplace rejects a
        re-used or lower version) and must keep the minor **odd** per R3's convention. Write it down.
  - [ ] Gate publish on the tagged commit already being green on `main` (ADR 0040 §9) — **do not** re-run
        build+test inside the release job.
  - [ ] Verify re-runnability: a failed publish must leave no partial state.

- [ ] **Task 6 — ADR, if R2 requires one** (AC: #5)
  - [ ] If `--oidc` is viable on a personal publisher, author an ADR amending **ADR 0040 §4**, add its
        `docs/adrs/README.md` index line, and verify the next ADR number is genuinely free — **0039 was not
        free when 16.1 assumed it**, and `0019` remains claimed-but-unwritten by Story 18.3.

- [ ] **Task 7 — Verify and close** (AC: #7)
  - [ ] `git status --porcelain src/ tests/ web/` → **empty**.
  - [ ] Fix the R10 R8.2→R8.1 citation in `epics.md`; list it in the File List.
  - [ ] Regression floor: `dotnet test` plus `npm run check` from `web/` (incl. `check:parity`). **Note:**
        `check:parity` **cannot see** a C#-side change (CLAUDE.md); this story touches neither, so a green run
        here means "unchanged", which is the intended claim.
  - [ ] **Re-grep every symbol you added before claiming it landed** (CLAUDE.md § Concurrent work).

---

## Dev Notes

### 👤 Owner actions — this story cannot complete without them

1. **Publisher ownership: personal or organization.** Blocked on R2's evidence. **Effectively irreversible.**
   Bring the finding, then the recommendation — not the reverse.
2. **Reserve the `specscribe` Marketplace publisher.** 404 on 2026-08-07 = unclaimed. Creating a publisher is
   an owner account action, mirroring 16.1's nuget.org/npm reservations.
3. **Fat vs thin VSIX (R6).** Recommendation: **thin for the preview**, platform-targeted fat as the follow-on.
4. **Create the trusted-publishing policy** (repo + workflow) on the Marketplace, or the Entra managed identity
   + publisher membership — whichever R2 selects.
5. **Ratify ADR 0040** — still `Proposed`; Story 16.1 AC #4 asked for this and it remains open.

### Files being modified — current state, what changes, what must be preserved

**`extension/package.json`** (9,880 B) — *UPDATE.*
Today: a complete, working contribution surface — 12 commands, an activity-bar container, 2 views,
`viewsWelcome`, 6 custom status colors, 5 menu contributions, 2 configuration properties, and the R5.4
Workspace-Trust block. **Changes:** remove `private`; add `icon`, `repository`, `keywords`, real `categories`,
`walkthroughs`; pin `vsce`. **Preserve:** every `contributes.*` block, `capabilities.untrustedWorkspaces`,
`activationEvents: ["onStartupFinished"]`, `engines.vscode`, and **`version: "0.1.0"` exactly** (R3).

**`extension/src/extension.ts`** (2,398 lines) — *ideally UNCHANGED.*
`resolveTool` (`:1993`) already implements the three-tier order this story populates. **Do not rewrite it.** If
bundling, satisfy the path it already probes — `<extensionPath>/bin/specscribe[.exe]` — rather than changing
the contract. Preserve the read-only invariant (ADR 0037: the core is the only writer) and the CSP/nonce
handling (`:1597`, `:1799`, `:2380`).

**`extension/.vscodeignore`** — *UPDATE only if bundling*, to ensure `bin/` is included.

**`.github/workflows/**`** — *NEW, or an extension of 16.4's pipeline.* `build-test-analyze.yml` must not be
disturbed; ADR 0040 §9 and epics.md § 16.2 (AMENDED 2026-07-25) forbid a second build+test workflow. A publish
job that does not build or test is not that.

**`extension/README.md`** (16,557 B) — *coordinate with 16.6.* Developer-facing today (opens with an F5 dev-host
walkthrough). The Marketplace renders it as the listing. 16.6 owns release-facing docs and the Node prerequisite
on consumer surfaces (ADR 0040 §8) — agree the split rather than both editing it.

### Testing standards

- `dotnet test` (`tests/SpecScribe.Tests`) is the C# regression floor. This story should not move it; if it
  does, something is out of scope. Note the known `FileWatcherServiceTests` timing flake that Story 16.2 is
  root-causing — if you see it, it is not yours.
- `web/` gates: `npm run check` (`check:parity`, `check:ir-content`, `check:tokens`, `check:assets`).
  **Never regenerate a gate baseline reflexively** — establish causality first (CLAUDE.md).
- The extension has **no automated test suite**; `npm run typecheck` (`tsc --noEmit`) is the static floor.
  Manifest correctness is verified by `vsce package` itself plus a **dev-host / installed-VSIX** check.
- **Verify in a live VS Code**, not by reading the manifest. CLAUDE.md § Verification: the suite structurally
  cannot see this class of defect. A walkthrough that renders wrong, an icon the Marketplace rejects, or a
  bundled binary that resolves no renderer are all only visible by looking.
- ⚠️ **If you run `dev-story` inside a git worktree, `generate` will mislead you.** Two known traps compound:
  `NuxtPrerender.FindRepoRoot` tests `Directory.Exists(".git")`, but in a worktree `.git` is a **file**, so it
  walks past the worktree root and can resolve the *main* checkout's `web/.output` (16.1 finding #2, still
  open); and the prerender can silently skip unless `SPECSCRIBE_RENDERER_DIR` is set. Both produce a *green*
  result that proves nothing about packaging. **Run AC #4's proof from a real, non-worktree foreign
  repository**, or the measurement is worthless.
- **Rebuild non-incrementally before trusting anything asset-related** (CLAUDE.md): `dotnet build … --no-incremental`.

### Project structure notes

- `extension/` is a self-contained npm project with its own `package-lock.json`; it is **not** part of the
  `web/` workspace, so 16.2's `web/package-lock.json` repair does not touch it.
- `extension/.gitignore` already ignores `node_modules/`, `dist/`, **`bin/`**, and `*.vsix` — the bundled-binary
  destination and the built artifact are pre-ignored. Do not commit either.
- The build is esbuild → `dist/extension.js`, CJS, `platform: node`, `target: node18`, `external: ['vscode']`,
  minified in `--production`. Do not switch bundlers; Story 6.3's spike settled this.

### Scope guard — what this story does not do

Open VSX is an **explicit non-goal** in ADR 0040 §2, alongside code signing and byte-identical reproducible
builds. Do not add a second registry "while we're here". Do not fix the stale `epic-6:` key (R1). Do not touch
`src/**`, `tests/**` or `web/**`.

### Concurrent-work discipline (CLAUDE.md)

Another agent may be editing these files right now. **Verify after every edit** — grep for the symbol you added
before relying on it. **Never** `git reset --hard`, `git checkout --`, or `git clean`. Expect commits to bundle
sibling stories, and scope any later review by this story's File List and by **hunk** where a file is shared.

### References

- [Source: `_bmad-output/planning-artifacts/epics.md#Story 16.5`] — AC #1–#3 verbatim; the R8.2 citation error (R10)
- [Source: `docs/adrs/0040-release-channels-and-versioning-policy.md`] — §1 renderer-inside-the-package; §2 preview cut + non-goals; §4 publisher/credential; §5 versioning + the Marketplace exception; §9 tag/gate relationship
- [Source: `_bmad-output/implementation-artifacts/16-1-spike-report.md`] — §5.3 the PAT finding; §6.4 the four version numbers; open item #7 (`private: true`)
- [Source: `docs/VSCodeIntegrationRecommendations.md:45,47,81,100,111`] — R1.4, R1.6, R5.4, **R8.1**, the routing table
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:566,572,576,852`] — the asset-pipeline debt routed here
- [Source: `extension/src/extension.ts:405-406,1985-1999,1946-1951`] — the empty `bin/` seam, Generate/Watch routing, the not-found affordance
- [Source: `extension/package.json`] — the manifest gaps in R8
- [VS Code, *Publishing Extensions*](https://code.visualstudio.com/api/working-with-extensions/publishing-extension) — `major.minor.patch` only, no semver pre-release tags; odd/even convention; `--target` platform list; SVG icons rejected; 30-keyword cap; Entra/PAT status
- [`microsoft/vscode-vsce` README](https://github.com/microsoft/vscode-vsce) — `--oidc` trusted publishing, the workflow YAML, no-PAT-fallback, Node ≥ 22
- [`microsoft/vscode-vsce#597`](https://github.com/microsoft/vscode-vsce/issues/597) — `private` is ignored by package/publish
- [`microsoft/vsmarketplace#1541`](https://github.com/microsoft/vsmarketplace/issues/1541) — max VSIX size: open, unanswered
- [Source: `CLAUDE.md`] — concurrent work, gate discipline, ADR proposal duty, live-browser verification

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List
