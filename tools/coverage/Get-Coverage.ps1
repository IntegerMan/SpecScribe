#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Produces a browsable local HTML coverage report for the C# side of SpecScribe.

.DESCRIPTION
    This is a RENDERER, not a second coverage mechanism. The collection path is unchanged:

        coverlet.collector 6.0.4  ->  coverage.opencover.xml  -+->  SonarScanner   (CI, untouched)
           (already referenced)        (already emitted)        +->  ReportGenerator (here, local only)

    ReportGenerator reads the OpenCover file `dotnet test` already emits. It never instruments,
    never runs tests, and never computes a coverage number of its own.

    WHY THE RAW DIRECTORY IS DELETED FIRST (step 1, non-negotiable):
    every `dotnet test --collect:...` run writes a NEW GUID-named directory. A glob over
    `**/coverage.opencover.xml` would otherwise merge every historical run - including runs from a
    different commit or a partially-failing run - into one plausible-looking, wrong number.

    RUNTIME: the tool package ships net8.0/net9.0/net10.0 assets and the manifest pins
    `rollForward: false`, so it launches on a matching installed runtime. If it ever fails to start
    on a leaner machine, set DOTNET_ROLL_FORWARD=Major - the same mitigation
    `.github/workflows/build-test-analyze.yml:56` already applies to the SonarScanner.

    SCOPE: C# only. `web/` has its own collector (@vitest/coverage-v8 -> web/coverage/lcov.info) and
    is deliberately NOT merged here; see README.md in this directory for the priced alternative and
    for why the local figure differs from the SonarCloud badge.

.PARAMETER Open
    Opt-in: open the generated report in the default browser when the run finishes. Off by default -
    an auto-launching browser is hostile inside an agent loop and in CI.

.EXAMPLE
    pwsh tools/coverage/Get-Coverage.ps1

.EXAMPLE
    pwsh tools/coverage/Get-Coverage.ps1 -Open
#>
[CmdletBinding()]
param(
    [switch] $Open
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# PowerShell 7.4+ defaults $PSNativeCommandUseErrorActionPreference to $true, which turns a non-zero
# native exit code into a terminating error. That would abort the run the moment `dotnet test` goes
# red - and a red run is EXACTLY the case where we still want the report rendered so the caller can
# see what failed. Exit codes are checked explicitly below instead.
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$outRoot  = Join-Path $repoRoot 'artifacts/coverage'
$rawDir   = Join-Path $outRoot  'raw'
$htmlDir  = Join-Path $outRoot  'html'

Push-Location $repoRoot
try {
    $started = [System.Diagnostics.Stopwatch]::StartNew()

    # 1. Clean. See the block comment above - this is what keeps the number honest.
    if (Test-Path $outRoot) {
        Write-Host "Removing stale $outRoot ..." -ForegroundColor DarkGray
        Remove-Item $outRoot -Recurse -Force
    }

    # 2. Restore the pinned renderer from .config/dotnet-tools.json.
    Write-Host 'Restoring local dotnet tools ...' -ForegroundColor DarkGray
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed with exit code $LASTEXITCODE." }

    # 3. Collect. Same collector, same format, same solution as
    #    .github/workflows/build-test-analyze.yml:208. No `--no-build`: unlike CI, this command has
    #    no preceding build step and must work from a clean tree.
    #    The collect string is passed via a variable so PowerShell keeps its `;` inside one argument.
    $collect = 'XPlat Code Coverage;Format=opencover'
    Write-Host 'Running tests with coverage collection ...' -ForegroundColor DarkGray
    $testTimer = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet test SpecScribe.slnx --collect:$collect --results-directory $rawDir
    $testExit = $LASTEXITCODE
    $testTimer.Stop()

    if ($testExit -ne 0) {
        Write-Host ''
        Write-Warning @'
THE TEST RUN WAS RED. coverlet writes the OpenCover file regardless of test outcome, so the report
below IS still generated - but its percentage is LOWER and plausible-looking. Do not cite a number
taken from a failing run. (The local suite is known-flaky: GitMetrics.cs:259 puts a 3 s timeout on
every git subprocess and a cold deep-git read has been measured at 6,496 ms.)
'@
        Write-Host ''
    }

    $reportGlob = Join-Path $rawDir '**/coverage.opencover.xml'
    if (-not (Get-ChildItem -Path $rawDir -Filter 'coverage.opencover.xml' -Recurse -ErrorAction SilentlyContinue)) {
        throw "No coverage.opencover.xml was produced under $rawDir. Nothing to render."
    }

    # 4. Render. `-reporttypes` MUST stay quoted / variable-passed: a bare `;` is a PowerShell
    #    statement separator and would silently render only Html, leaving no TextSummary to read.
    #    NO `-assemblyfilters` - and that is MEASURED, not precautionary. The first run of this
    #    script (2026-07-28) produced `Assemblies: 1` / `specscribe` in Summary.txt, with zero
    #    `SpecScribe.Tests` entries, confirming coverlet's default test-assembly exclusion holds
    #    here. If a test assembly ever does appear, add
    #    `"-assemblyfilters:+SpecScribe;-SpecScribe.Tests"` - quoted for the same `;` reason.
    $reportTypes = 'Html;TextSummary'
    Write-Host 'Rendering HTML report ...' -ForegroundColor DarkGray
    $renderTimer = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet tool run reportgenerator -- "-reports:$reportGlob" "-targetdir:$htmlDir" "-reporttypes:$reportTypes"
    if ($LASTEXITCODE -ne 0) { throw "reportgenerator failed with exit code $LASTEXITCODE." }
    $renderTimer.Stop()

    $started.Stop()

    # 5. Print the summary and the path. TextSummary is the machine-readable source of the
    #    line/branch figures the story's reconciliation uses - it is not decoration.
    $summaryFile = Join-Path $htmlDir 'Summary.txt'
    if (Test-Path $summaryFile) {
        Write-Host ''
        Get-Content $summaryFile | Write-Host
    }

    $indexPath = (Resolve-Path (Join-Path $htmlDir 'index.html')).Path
    Write-Host ''
    Write-Host "Report:  $indexPath" -ForegroundColor Green
    # Phases are timed separately on purpose. Total wall clock on a shared dev machine is dominated
    # by build/test contention and is not a stable number; the render phase is, so report both.
    Write-Host ("Elapsed: {0:n1} s total  (build+test {1:n1} s, render {2:n1} s)" -f `
        $started.Elapsed.TotalSeconds, $testTimer.Elapsed.TotalSeconds, $renderTimer.Elapsed.TotalSeconds) `
        -ForegroundColor DarkGray

    if ($Open) { Start-Process $indexPath }

    # Surface the test outcome to the caller. A red suite is a red command, even though the report
    # rendered - otherwise a script consuming this would treat a fictional percentage as fact.
    exit $testExit
}
finally {
    Pop-Location
}
