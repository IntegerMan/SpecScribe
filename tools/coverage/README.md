# Local coverage report — the C# side, rendered from what CI already collects

`dotnet test` already emits an OpenCover coverage file (Story 25.1 chose that format because
SonarScanner for .NET reads `sonar.cs.opencover.reportsPaths` and does not document Cobertura support
for C#). CI uploads that file to SonarCloud. Locally, nothing read it — so finding an untested method
meant pushing a commit and waiting for an analysis.

This directory closes that loop with **one command**:

```sh
pwsh tools/coverage/Get-Coverage.ps1
```

Add `-Open` to launch the report in a browser when it finishes. It does not open one by default: an
auto-launching browser is hostile inside an agent loop and in CI.

## This is a renderer, not a second coverage mechanism

The collection path is untouched end to end:

```
coverlet.collector 6.0.4  ->  coverage.opencover.xml  -+->  SonarScanner    (CI, unchanged)
   (already referenced in                                |
    SpecScribe.Tests.csproj)   (already emitted)         +->  ReportGenerator (local only, here)
```

[ReportGenerator](https://github.com/danielpalme/ReportGenerator) *reads* that artifact. It never
instruments, never runs tests, and never computes a coverage number of its own. No coverage package
was added — `coverlet.collector` 6.0.4 was already there.

The renderer is pinned at **5.5.11** in [`.config/dotnet-tools.json`](../../.config/dotnet-tools.json),
the repo's first dotnet local tool manifest. It is committed deliberately: a local manifest is
version-pinned and restorable, so the report is reproducible. A `dotnet tool install -g` would be
none of those, and an MSBuild `PackageReference` would put a dev-only package on every restore for
everyone.

## Output

Everything lands in `artifacts/coverage/`, which is already gitignored by `.gitignore:66` (`artifacts/`) —
verified with `git check-ignore -v`, not assumed. Note that a root `coverage/` would **not** be
ignored: the existing `coverage*.xml` / `*.json` / `*.info` rules are file globs and do not cover a
directory.

| Path | What |
|---|---|
| `artifacts/coverage/raw/<guid>/coverage.opencover.xml` | the collector's output, untouched |
| `artifacts/coverage/html/index.html` | the browsable report |
| `artifacts/coverage/html/Summary.txt` | the `TextSummary`, the machine-readable line/branch figures |

The report is roughly **53 MB across 299 files** (~60 MB including the raw XML). That is a real cost
for a per-change habit, and it is why the directory is cleaned rather than accumulated.

## The raw directory is deleted on every run — deliberately

Every `dotnet test --collect:...` run writes a **new GUID-named directory**. A glob over
`**/coverage.opencover.xml` would otherwise merge every historical run — including runs from a
different commit, or a partially-failing run — into a single merged number that is wrong and looks
entirely plausible. Step 1 of the script deletes `artifacts/coverage/` for that reason. Do not
"optimize" it away.

## Why the local number differs from the SonarCloud badge

There are three separate reasons, and none of them is a disagreement:

1. **Different formula.** SonarCloud's headline `coverage` metric blends lines *and* branches:
   `(covered_lines + covered_conditions) / (lines_to_cover + conditions_to_cover)`. ReportGenerator
   reports line coverage and branch coverage as **two separate figures** and does not compute that
   blend. Comparing a line-coverage percentage against Sonar's `coverage` percentage compares two
   different formulas.
2. **Different denominator.** Sonar's project-wide figure also carries `extension/src` (~508 lines
   to cover, 0%) and `web/` (~45%). This report is C#-only, so it cannot and should not show them.
   The directory-scoped Sonar figures for `src/SpecScribe` are the ones to compare against.
3. **Different tree.** Sonar analyzes a pushed commit; this runs against your working tree,
   uncommitted edits included.

Compare **line coverage against Sonar's `line_coverage`** and **branch coverage against Sonar's
`branch_coverage`**, both scoped to `src/SpecScribe`. The full worked reconciliation lives in the
story record, `_bmad-output/implementation-artifacts/25-5-local-coverage-report.md`, and the
exclusion rules behind the denominator gap are in
[`docs/SonarCloudSetup.md`](../../docs/SonarCloudSetup.md) § *Coverage exclusions*.

## `web/` is not merged in

`web/` has its own collector (`@vitest/coverage-v8`, emitting lcov to the gitignored `web/coverage/`).
That is not a second mechanism measuring the same code — it covers a language the OpenCover path
structurally cannot reach.

ReportGenerator *does* ingest lcov, so
`-reports:"artifacts/coverage/raw/**/coverage.opencover.xml;web/coverage/lcov.info"` would produce a
single report spanning both stacks and land closer to Sonar's blended project figure. It was
declined for now, not overlooked: it requires the command to also run `npm run test:coverage`,
pulling Node into a .NET dev loop and roughly doubling the runtime, for a number the arithmetic above
already explains. Recorded here as a known, priced option.

## When the command refuses to render

The script **throws rather than rendering** if no `coverage.opencover.xml` was produced. Both of
these were hit while developing it on a shared machine, and both are real:

- **`CoverletDataCollectorException: Failed to instrument modules`** — coverlet could not restore an
  original module because another process held the file. No coverage is emitted at all.
- **`MSB3027 / MSB3021 … file is locked by "testhost"`** — the build itself fails against a leftover
  or concurrent test host, so no tests run.

In both cases the correct behaviour is to fail loudly. Re-run once the other process is done.

Separately, a **red test run still emits coverage**: the script renders the report but prints a loud
warning and exits with the test run's exit code. A failing run produces a *lower, plausible-looking*
percentage — do not cite one.

## Untested by design

There is no test project for `tools/`, and this script does not add one — `tools/plotly-vendor` and
`tools/prism-vendor` ship the same way, and `tools/**` is inside `sonar.exclusions`
(`.github/workflows/build-test-analyze.yml:191`), so nothing here appears in the findings list Epic 25
exists to triage.
