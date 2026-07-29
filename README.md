# SpecScribe

**SpecScribe turns spec-driven-development artifacts into a human-readable website.**

Frameworks like [BMad](https://github.com/bmad-code-org/BMAD-METHOD) (including its GDS game-development
submodule) produce a wealth of markdown artifacts — PRDs, GDDs, epics, stories, requirements inventories,
and architecture decision records. Those files are written for AI agents and power users, not for humans
skimming project status. SpecScribe watches those artifacts and renders them into a styled, navigable,
cross-linked static HTML site: epic and story dashboards with progress gauges, requirements traceability
pages, rendered mermaid diagrams, and ADR indexes — regenerated live on every save.

## Supported frameworks

SpecScribe renders artifacts from the spec-driven-development frameworks below. Support for additional
frameworks is planned — see the [Roadmap](#roadmap) for feature-level plans.

| Framework | Version | Status |
|-----------|---------|--------|
| [BMad Method](https://github.com/bmad-code-org/BMAD-METHOD) | 6.10.0 | ✅ Supported |
| BMad GDS (Game Dev Studio) | 0.6.0 | ✅ Supported |
| [GitHub Spec Kit](https://github.com/github/spec-kit) | — | 🧭 Planned |
| GSD | — | 🧭 Planned |
| GSD-Pi | — | 🧭 Planned |
| Superpowers | — | 🧭 Planned |

### BMad modules

BMad is not one thing — it installs as a set of modules under `_bmad/{code}/`, and BMad Builder can mint
new ones with arbitrary codes. SpecScribe identifies the installed module(s) from that directory name and
tells you honestly how deeply it understands each one. Installing a second module never degrades the one
you already had; a repo running BMM alongside Test Architect keeps its full BMM surface.

| Module | Code | What SpecScribe does |
|--------|------|----------------------|
| BMad Method | `bmm` | **Full projection** — epics, stories, requirements, sprint, retros, planning docs, next-step commands, and the BMM glossary |
| Game Dev Studio | `gds` | **Full projection** — the same families, with GDD / narrative planning docs and GDS commands |
| Test Architect | `tea` | **Named, with its test artifacts interpreted** — they get their own page and a dashboard *Module Coverage* panel carrying the quality-gate verdict and coverage figures (see below) |
| Creative Intelligence Suite | `cis` | **Named** — its real label and its parsed command catalog; its markdown renders through the generic document pass |
| BMad Builder | `bmb` | **Named** — as above |
| Anything BMad Builder mints | any | **Named** — module identity is open-world, so a custom module is named rather than misidentified |

"Named" is a deliberate, stated boundary rather than a gap. For every module below the top two — **Test
Architect included** — SpecScribe publishes no glossary and no planning-doc set, and the dashboard's
artifact-coverage panel is omitted rather than reporting eight BMad Method artifact families the module
never produces. The run records that omission as a non-fatal note on the Diagnostics page, which also
names the module actually detected.

Two BMad **core** skills — present in every install regardless of module — get first-class surfaces:

- **`bmad-forge-idea`** → an **Ideas** page listing every forged idea workspace grouped by outcome
  (hardened / in progress / killed), each with a detail page, the original `forge-report.html` carried
  through verbatim, and forward links to the brief, PRD, or epic an idea produced where that link is
  evidenced on disk.
- **`bmad-testarch-*` (Test Architect)** → a **Test Artifacts** page, plus the first non-markdown sources
  SpecScribe reads: `gate-decision.json` and `e2e-trace-summary.json` are read by exact filename so the
  `PASS`/`CONCERNS`/`FAIL`/`WAIVED` gate verdict is visible instead of silently invisible to the `*.md` scan.

Every discovered module artifact carries one of three **coverage tiers**, so the interpretation boundary is
stated rather than guessed at:

| Tier | Meaning |
|------|---------|
| **Rendered** | The document has its own page; SpecScribe reads its prose but interprets none of its structure |
| **Summarized** | SpecScribe extracts a structured headline — verdict, coverage figures — and surfaces it alongside the artifact, while the file itself is not fully modelled |
| **Unsupported** | Discovered and named, nothing interpreted. Not an error — an honest statement of the boundary |

Optional surfaces are omitted entirely when their artifacts don't exist: no forge workspaces means no Ideas
page and no nav entry; no Test Architect artifacts means no Test Artifacts page and no Module Coverage panel.

## Install

SpecScribe is a [.NET global tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) targeting .NET 10.

From a clone of this repository:

```
dotnet pack src/SpecScribe -c Release -o artifacts
dotnet tool install --global SpecScribe --add-source ./artifacts
```

That puts `specscribe` on your PATH (`%USERPROFILE%\.dotnet\tools`), so you can run it from any
project directory. To pick up a newer build later: bump the `<Version>` in
`src/SpecScribe/SpecScribe.csproj`, re-pack, then `dotnet tool update --global SpecScribe --add-source ./artifacts`.

## Usage

```
specscribe                  # interactive menu (generate / watch / configure paths)
specscribe generate         # generate the site once and exit
specscribe watch            # generate, then regenerate on every file save (Ctrl+C to stop)
specscribe --help           # full CLI help
```

Run with no arguments (or with unrecognized arguments) and SpecScribe drops into an interactive
menu where you can generate, watch, or adjust paths before running.

### Options

Both `generate` and `watch` accept:

| Option | Default |
|--------|---------|
| `--source <DIR>` | Walks up from the current directory to find `_bmad-output/` |
| `--adrs <DIR>` | `<repo root>/docs/adrs` |
| `--output <DIR>` | `<repo root>/SpecScribeOutput` |
| `--project-name <NAME>` | `project_name` from `_bmad/config.toml`, else "BMad Live Docs" |
| `--deep-git` | Off — opt-in deeper git analytics (file hotspots + change coupling) as a distinct dashboard panel; leaving it off keeps baseline generation unaffected |

With no options, SpecScribe auto-discovers a BMad project from wherever you run it — so inside a
BMad repo, plain `specscribe generate` just works.

## Publishing to GitHub Pages

You can publish SpecScribe output for any repository, not just this one.

### Option A: Build and deploy with GitHub Actions (recommended)

Create `.github/workflows/publish-specscribe-pages.yml`:

```yaml
name: Publish SpecScribe Docs

on:
  push:
    branches: ["main"]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: pages
  cancel-in-progress: false

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          # Full history so git metrics (commit count, activity heatmap) and the
          # opt-in deep analytics (--deep-git) reflect real history, not just the tip.
          fetch-depth: 0
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Generate static site
        run: |
          dotnet tool restore
          specscribe generate \
            --source _bmad-output \
            --adrs docs/adrs \
            --output SpecScribeOutput \
            --project-name "My Project" \
            --deep-git

      - name: Upload pages artifact
        uses: actions/upload-pages-artifact@v4
        with:
          path: SpecScribeOutput

  deploy:
    needs: build
    runs-on: ubuntu-latest
    timeout-minutes: 10
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url || steps.deployment_retry.outputs.page_url }}
    steps:
      # GitHub's Pages backend intermittently returns "Deployment failed, try
      # again later." The first attempt may fail without failing the job; the
      # guarded retry re-invokes the deploy only when that happens.
      - id: deployment
        continue-on-error: true
        uses: actions/deploy-pages@v5

      - if: steps.deployment.outcome == 'failure'
        run: sleep 30

      - id: deployment_retry
        if: steps.deployment.outcome == 'failure'
        uses: actions/deploy-pages@v5
```

Notes:
- Replace paths and project name for your project layout.
- If you are not using a local tool manifest, install SpecScribe in the workflow before running `specscribe`.
- The deploy step is retried once because GitHub's Pages backend occasionally reports a transient `Deployment failed, try again later.` error; the retry avoids a full rebuild.
- Full repository example workflow: https://github.com/IntegerMan/SpecScribe/blob/main/.github/workflows/publish-docs-live-pages.yml

### Option B: Commit generated output and publish from that folder

If you commit generated site files, you can keep output in a single top-level folder like `SpecScribeOutput`
and configure GitHub Pages to serve that published content from version control.

For this mode:
- Run SpecScribe with `--output SpecScribeOutput`.
- Commit and push the generated `SpecScribeOutput` files.
- Configure GitHub Pages in repository settings to publish from the branch/folder setup that serves that directory.

This is useful if you prefer static output tracked in git instead of artifact-based deployment.

## What it renders

- **Dashboard** — project-wide progress, epic/story completion gauges, git activity stats
- **Epics & stories** — parsed from BMad `epics.md` structure, grouped and cross-linked, with status pills
- **Requirements traceability** — FR/NFR inventory with epic coverage maps; requirement IDs in any
  document become anchor links
- **ADRs** — hand-authored architecture decision records rendered with rewritten cross-links
- **Ideas** — forged idea workspaces grouped by outcome, with the original forge report and forward links
  to whatever the idea became (omitted when no ideas exist)
- **Test artifacts** — Test Architect output with its quality-gate verdict, coverage figures, and a
  per-artifact coverage tier, plus a Module Coverage panel on the dashboard (omitted when absent)
- **Mermaid diagrams** — fenced ` ```mermaid ` blocks render client-side
- **Task lists** — GitHub-style checkboxes render as progress

Source files are always read with shared access; the watcher never holds a write lock on anything
it observes.

## Roadmap

Planned framework support (Spec Kit, GSD, GSD-Pi, Superpowers) is tracked in the
[Supported frameworks](#supported-frameworks) table above. Feature-level plans:

- **Git insights** — richer history-derived views (velocity, file heatmaps) beyond the current commit stats
- **Directory-structure insights** — project-layout overviews generated from the tree itself

## Development

```
dotnet build            # build everything
dotnet test             # run the unit tests
dotnet run --project src/SpecScribe -- generate    # run without installing
pwsh tools/coverage/Get-Coverage.ps1               # browsable coverage report (add -Open to launch it)
```

The solution is `SpecScribe.slnx`; the tool lives in `src/SpecScribe`, tests in `tests/SpecScribe.Tests`.

The coverage report is written to the gitignored `artifacts/coverage/html/`. It renders the OpenCover file
`dotnet test` already produces — it is not a second coverage mechanism — and it covers **C# only**, so its
percentage is deliberately not the same number as the SonarCloud figure, which uses a different formula and
also counts `extension/` and `web/`. [`tools/coverage/README.md`](tools/coverage/README.md) explains the
difference and shows which figures to compare.

### Continuous integration

Every push to `main` and every pull request builds the solution and runs the full test suite via
[`.github/workflows/build-test-analyze.yml`](.github/workflows/build-test-analyze.yml), which also submits the
build to SonarQube Cloud for code-quality analysis.

Build and test need no configuration. The analysis half requires a one-time SonarCloud setup — generating a
token and storing it as the `SONAR_TOKEN` repository secret — described in
**[SonarCloud Setup](docs/SonarCloudSetup.md)**. Until that is done CI still runs green; the scanner steps
simply skip, which is also what happens on pull requests from forks, since GitHub does not share secrets with
them.

## License

[MIT](LICENSE) — Copyright (c) 2026 Matt Eland
