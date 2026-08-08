# Packaging SpecScribe

How a SpecScribe package is produced, how to verify one, and the ways a packaging change can produce a green
build that ships a package which cannot render.

**Authority:** [ADR 0040](adrs/0040-release-channels-and-versioning-policy.md) decides the channels, the
packaging shape, the versioning mechanism and the RID matrix. This document is the operational companion —
it records *how*, not *whether*. Written by Story 16.3; Story 16.4's Stage A builds the release assets on it.

**Nothing here publishes anything.** Every command below produces a package locally and installs it from a
local feed. Pushing to nuget.org / npm is Story 16.4's, and it is gated on owner actions besides.

---

## Why the renderer is the whole problem

Since [ADR 0034](adrs/0034-the-ir-is-the-product-and-the-site-is-rendered-from-it.md) **no C# code path writes
content HTML.** `generate` emits the JSON IR, then boots a prebuilt Nitro artefact and issues one request per
route in the manifest it just wrote. So a package that does not carry that artefact installs cleanly, puts
`specscribe` on `PATH`, and then fails every `generate`.

`NuxtPrerender.ResolveArtefactDirectory` looks in three places, in order:

| # | location | who populates it |
|---|---|---|
| 1 | `SPECSCRIBE_RENDERER_DIR` | the operator, explicitly. **Hard-fails if it does not resolve** — it never falls through to another artefact, because rendering with a different artefact than the one you named is a wrong answer with a success status. |
| 2 | `renderer/` beside the executing assembly | **packaging — this document** |
| 3 | `web/.output/` under the repo root | the developer path, in this checkout only |

Candidate 3 is why a packaging change **cannot be verified from inside this repository**: it rescues a broken
package and reports a false pass. Every verification below runs from a foreign repository.

---

## Build order (the order is load-bearing)

```sh
cd web
SPECSCRIBE_PACKAGE_BUILD=1 npm ci   # PowerShell: $env:SPECSCRIBE_PACKAGE_BUILD='1'; npm ci
npm run sync:assets
npm run build:package               # NEVER `npm run build`
cd ..
dotnet pack src/SpecScribe/SpecScribe.csproj -c Release -o artifacts
```

Three things about that, each of which has cost someone a diagnosis cycle:

- **`SPECSCRIBE_PACKAGE_BUILD=1` is not optional on a fresh checkout.** `postinstall: nuxt prepare` loads
  `web/nuxt.config.ts`, which calls `loadManifest()` and hard-fails when no IR exists — and on a fresh clone
  there is none. The flag stubs the manifest empty, which is exactly what it is for. Without it `npm ci` exits
  1 with `IR not found at …/SpecScribeOutput/spa/manifest.json`.
- **`build:package`, never `build`.** A plain `nuxt build` bakes *this* project's pages into `.output/public`,
  and Nitro serves `public/` ahead of the SSR route — so the artefact returns SpecScribe's own pages for your
  project, at HTTP 200. ([ADR 0022](adrs/0022-node-is-a-build-toolchain-and-a-generate-time-runtime.md) § 2.)
- **`web/.output` is gitignored and absent on every fresh checkout.** It is not in the repository; it must be
  built. This is the precondition the guards below exist to enforce.

---

## The packaging shape

One `Content` item in `src/SpecScribe/SpecScribe.csproj` serves **both** channels:

```xml
<Content Include="..\..\web\.output\**\*" Pack="false"
         Link="renderer\%(RecursiveDir)%(Filename)%(Extension)"
         CopyToOutputDirectory="Never" CopyToPublishDirectory="PreserveNewest" />
```

| channel | where the payload lands | proven |
|---|---|---|
| `dotnet` global tool | `tools/<tfm>/any/renderer/**` inside the nupkg — and therefore beside the executing assembly in the tool store | Story 16.3, foreign repo, `errors=0` |
| self-contained binary | a sibling `renderer/` directory beside the executable | Story 16.3, `win-x64`, foreign repo, `errors=0` |

- `CopyToOutputDirectory="Never"` keeps a local `dotnet build` free of ~180 copied files. The inner loop stays
  fast, and developers are served by candidate 3 anyway.
- `Pack="false"` is **required and is not "do not package this"**. `Content` defaults to packable, and a
  packable `Content` item lands in `contentFiles/` — a second full copy that nothing reads. The `tools/` copy
  arrives via *publish*, not via this flag.
- `PublishSingleFile` does **not** move `AppContext.BaseDirectory` away from the sibling `renderer/`. Measured
  by Story 16.1 at `errors=0`.

### One item, not two — and why that is the correction, not a shortcut

Story 16.3 was briefed to add *two* items: a `None` with `Pack="true"` + `PackagePath` for the nupkg, and a
publish-time copy for the binary, on the reasoning that pack and publish are separate pipelines. Half of that
is right — the publish copy is invisible to a plain `dotnet pack` of a **library**. But **`PackAsTool` builds
`tools/<tfm>/any/` from the publish output**, so here the publish copy populates the nupkg too.

Measured four ways, each a full pack plus `unzip -l`:

| configuration | files | entry point at `tools/net10.0/any/renderer/server/index.mjs` |
|---|---|---|
| both items | 203 | present |
| `Content` only | 203 (byte-identical) | present |
| `None` only | 203 | present |
| `None` with a **broken** `PackagePath` + `Content` | 203 | present, **no doubled tree** |

That last row is the finding: **with the `Content` item present the `None` item contributes nothing — not even
its own damage.** A broken `PackagePath` on it is invisible. Shipping both would mean shipping one item that
looks load-bearing, is not, and silently absorbs its own defects, while the guard below reports green. The
`None` item was therefore removed.

---

## Versioning

The version comes from the nearest reachable git tag via [MinVer](https://github.com/adamralph/minver).
`<Version>` is **deleted** from the csproj, not replaced by a second literal (ADR 0040 § 5).

```
untagged build           →  0.1.0-preview.0.<commits-since>
build on tag v0.1.0-preview.1  →  0.1.0-preview.1   (exactly, no height)
```

Three properties keep untagged builds inside ADR 0040 § 5's scheme; without them MinVer's tagless default is
`0.0.0-alpha.0`, which is neither `0.x-preview` nor something the About page renders correctly:

```xml
<MinVerTagPrefix>v</MinVerTagPrefix>
<MinVerMinimumMajorMinor>0.1</MinVerMinimumMajorMinor>
<MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>
```

**Consequences to plan for, not discover:**

- 🚨 **`fetch-depth: 0` is mandatory on any CI checkout that packs.** MinVer resolves from tag reachability, and
  a shallow clone has no tags — which produces a **wrong version rather than an error**. This repository's
  `build-test-analyze.yml` already sets it (for SonarCloud's blame data); the consumer recipe in `README.md`
  now sets it too.
- **The version changes on every commit** (the height moves). Nothing downstream should break —
  `normalizeVolatile` in `web/scripts/harness-lib.mjs` folds `SpecScribe v…` to a stable token, which is why
  the parity gate tolerates it.
- **Never hard-code `--version` in an install recipe.** Read it off the produced `.nupkg` filename.
- **The pre-release label is a product surface, not cosmetics.** `AboutTemplater` renders the About page's
  `Preview` badge from `ProductMetadata.IsPrerelease`, which needs a non-empty trailing label. ADR 0040 § 5:
  *"the first release without the label is by definition no longer a preview."*
- **MinVer also sets `AssemblyVersion` to `{Major}.0.0.0`**, so on a `0.x` version it is `0.0.0.0` (it was
  `0.1.0.0`). Nothing reads it; `AssemblyFileVersion` still tracks the real version.

---

## Two traps that produce a green build and a broken package

### Trap 1 — a glob that matches nothing is not an MSBuild error

`web/.output` is gitignored and absent on a fresh checkout, so the *default* outcome of `dotnet pack` on a
clean tree is a ~2.5 MB nupkg that installs cleanly and fails at generate time. It ships silently.

**Guarded by `AssertRendererAvailableForPublish`** (`BeforeTargets="PrepareForPublish"`), which fails naming
the artefact and the command that builds it. Because `PackAsTool` packs *via* publish, this guard fires during
`dotnet pack` too — and fires *before* a broken nupkg is produced at all.

### Trap 2 — the payload is present, and at the wrong path

This is the dangerous one, because **every cheap check passes it.** A destination-shape mistake (a doubled
`%(RecursiveDir)`, a mis-rooted `Link`) buries the entry point while leaving the file count and the byte total
untouched. Measured, deliberately, during Story 16.3:

| | good package | package with a doubled `Link` |
|---|---|---|
| files | 203 | **203** |
| total bytes | 10,675,619 | **10,675,619** |
| `tools/net10.0/any/renderer/server/index.mjs` | present | **absent** |

A size-or-count check calls that a pass. **So the assertion is made against the packaged path, inside the
produced nupkg** — `AssertRendererPacked` (`AfterTargets="Pack"`) unzips the package MSBuild just produced and
errors if the entry point is not at exactly `tools/$(TargetFramework)/any/renderer/server/index.mjs`. It uses
MSBuild's built-in `Unzip` task, so it needs no new dependency.

`$(TargetFramework)`, never a literal `net10.0`: `PackAsTool` places the tool at `tools/<tfm>/any/`, and a
hard-coded TFM silently detaches the payload from its assembly the day the TFM moves.

**Both guards are scoped to pack/publish only.** `dotnet build` and `dotnet test` are unaffected — verified
with `web/.output` renamed away: build succeeded, tests passed. Breaking the inner loop to protect the release
loop is not a trade worth making.

---

## Verifying a package

A size check is not a verification (see Trap 2). Verify the **packaged path**, then verify it **renders from a
foreign repository**.

```sh
# 1. The packaged path. `dotnet pack` already asserts this and fails the build if it is wrong;
#    this is how you check a package you were handed.
unzip -l artifacts/SpecScribe.*.nupkg | grep 'renderer/server/index.mjs'
#    → tools/net10.0/any/renderer/server/index.mjs

# 2. Install from a LOCAL feed. Read the version off the nupkg; never type one from memory.
VERSION=$(ls artifacts/SpecScribe.*.nupkg | sed 's|.*/SpecScribe\.||; s|\.nupkg$||')
dotnet tool install SpecScribe --version "$VERSION" --tool-path ./probe-tools --add-source ./artifacts

# 3. Run it from a DIFFERENT repository, with SPECSCRIBE_RENDERER_DIR UNSET.
#    Assert both preconditions rather than assuming them.
cd /some/other/repo && test ! -d web && test -z "$SPECSCRIBE_RENDERER_DIR"
/path/to/probe-tools/specscribe generate --project-name "Probe"     # must report errors=0

# 4. THE NEGATIVE CASE — this is what proves step 3 was genuinely foreign.
#    Rename the packaged renderer/ away inside the tool store and re-run.
#    It MUST fail. If it still succeeds, candidate 3 rescued it and step 3 was a false pass.
```

Step 4 is not optional and is not pedantry: running the probe inside this repository lets `web/.output`
satisfy candidate 3, and the run reports success while proving nothing at all.

### Reference results (Story 16.3, `0.1.0-preview.0.410`)

Derive these figures rather than quoting them — the payload has moved twice already.

| | value |
|---|---|
| nupkg without the renderer | 2,520,789 B · 25 files |
| nupkg with the renderer | 3,739,917 B · 203 files |
| delta | **+1,219,128 B (+48.4%)** for a 178-file payload |
| self-contained `win-x64` publish | sibling `renderer/` · 178 files |

---

## What this does *not* cover

- **Byte-identical rebuilds.** Explicitly out of scope — ADR 0040 § 7 claims the weaker reading of NFR9
  ("built from a clean checkout by CI"). All three of its named preview gaps are now closed — `npm ci` (16.2),
  version-from-tag (16.3) and `SOURCE_DATE_EPOCH` (16.4) — so the weak reading holds. `Deterministic` /
  `ContinuousIntegrationBuild` / SourceLink are deferred past the preview to Story 17.4.
- **The npm and VS Marketplace channels.** Stories 16.8 and 16.5.
- **Re-verifying from the *published* artifact** on a clean environment. Story 16.7.

### Closed by Story 16.4 — see [docs/Releasing.md](Releasing.md)

These three were listed here as gaps and are now covered by
[`.github/workflows/build-test-analyze.yml`](../.github/workflows/build-test-analyze.yml):

- **Stage A assets.** The main-push workflow builds the nupkg and three self-contained archives, then creates
  the prerelease GitHub Release. Manual Stage B promotion in `release.yml` pushes the durable nupkg asset.
- **Cross-RID execution.** `win-x64`, `linux-x64` and `osx-arm64` are each **produced on, and executed on,
  their own operating system** in the release matrix — including a run from a path containing a space, from a
  foreign repository, with `SPECSCRIBE_RENDERER_DIR` unset. 16.3's host-RID-only proof is superseded.
- **`SOURCE_DATE_EPOCH`.** Set from the **tagged commit's committer timestamp** and asserted before the first
  build step, because the csproj's response to a malformed value is to silently stamp today's date.

One thing this document's guards still do **not** cover, and the release pipeline adds: `AssertRendererPacked`
inspects the produced **nupkg** and `AssertRendererAvailableForPublish` inspects the **source** directory.
Neither can see inside a produced `.zip` / `.tar.gz`, so the archives carry their own path assertion
(`assert-archive-renderer.sh`) — asserting the **archived path**, never the entry count or byte total, for
exactly the reason § Trap 2 measured.

## Known limitation: the README on nuget.org

`README.md` is the packaged README (`PackageReadmeFile`), so on NuGet it *is* the package listing. Its badge
hosts (`github.com/…/badge.svg`, `sonarcloud.io`) are on nuget.org's allow-list and there are no relative-path
images (which silently do not render). Its **relative links** — `LICENSE`, `docs/adrs/…`, `docs/Packaging.md` —
will not resolve on nuget.org. Recorded here as a known listing limitation; the rewrite decision belongs to
Story 16.6 rather than being half-done here.
