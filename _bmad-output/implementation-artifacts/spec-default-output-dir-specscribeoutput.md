---
title: 'Default output directory to SpecScribeOutput'
type: 'chore'
created: '2026-07-06T00:00:00Z'
status: 'done'
route: 'one-shot'
---

# Default output directory to SpecScribeOutput

> Retroactive record for work implemented out of band. Captured to match the quick-dev
> record-keeping convention (see `spec-github-pages-publish-docs-live.md`).

## Intent

**Problem:** The generated site was still defaulting to `docs/live`, while the README and the
GitHub Pages workflow had already standardized on a single top-level `SpecScribeOutput` folder.
The stale default meant a plain `generate` (and the VS Code tasks) wrote to the wrong place —
a misconfiguration.

**Approach:** Make `SpecScribeOutput` the one output convention everywhere: the code default,
the local dev config, and the docs. `docs/adrs` stays put — it is the hand-authored ADR *source*,
not generated output.

## Boundaries & Constraints

**Always:** Keep output as a single top-level folder (not nested under `docs/`, where ADR source
lives). The output folder stays gitignored — this repo regenerates rather than commits it.

**Never:** Do not move or rename `docs/adrs` (that is source, not output). Do not commit generated
output into version control for this repo.

## Code Map

- `src/SpecScribe/ForgeOptions.cs` -- new `OutputDirName = "SpecScribeOutput"` constant; default `OutputRoot` resolves to it.
- `src/SpecScribe/SiteSettings.cs` -- `--output` help text default.
- `tests/SpecScribe.Tests/ForgeOptionsTests.cs` -- default-output assertions.
- `.vscode/tasks.json`, `.vscode/launch.json`, `.claude/launch.json`, `.gitignore`, `README.md` -- local dev config + docs aligned to `SpecScribeOutput`.

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/ForgeOptions.cs` -- add `OutputDirName` and use it as the default output root.
- [x] `src/SpecScribe/SiteSettings.cs` -- update `--output` default in the help text.
- [x] `tests/SpecScribe.Tests/ForgeOptionsTests.cs` -- assert the default resolves to `SpecScribeOutput`.
- [x] `.vscode/tasks.json` / `.vscode/launch.json` / `.claude/launch.json` -- point generate/watch/preview at `SpecScribeOutput`.
- [x] `.gitignore` -- ignore `SpecScribeOutput/` instead of `docs/live/`; removed the stale `docs/live` directory.
- [x] `README.md` (+ `AdrLinkRewriter.cs` / `SiteGenerator.cs` doc-comments) -- fix the documented default and illustrative paths.

**Acceptance Criteria:**
- Given no `--output` flag, when paths resolve, then `OutputRoot` is `<repo root>/SpecScribeOutput`.
- Given an explicit `--output`, when paths resolve, then the explicit value still wins (unchanged behavior).
- Given a fresh clone, when the repo is searched, then no config, task, or doc points generated output at `docs/live`.

## Spec Change Log

_None — implemented directly; no review loopback._

## Verification

**Commands:**
- `dotnet test tests/SpecScribe.Tests/SpecScribe.Tests.csproj` -- expected: all pass (ForgeOptionsTests assert the `SpecScribeOutput` default).
- `dotnet run --project src/SpecScribe -- generate` -- expected: writes to `SpecScribeOutput/`, not `docs/live/`. (Blocked at time of writing by an unrelated in-flight `RenderStoryPlaceholder` build error; unit-level default is covered by the tests.)

## Suggested Review Order

**Code default**

- Entry point — the `OutputDirName` constant and the default `OutputRoot` resolution.
  [`ForgeOptions.cs:43`](../../src/SpecScribe/ForgeOptions.cs#L43)

- The `--output` help text now documents the new default.
  [`SiteSettings.cs:18`](../../src/SpecScribe/SiteSettings.cs#L18)

**Tests**

- Default-output assertions pinned to `SpecScribeOutput`.
  [`ForgeOptionsTests.cs:27`](../../tests/SpecScribe.Tests/ForgeOptionsTests.cs#L27)

**Local dev config & docs**

- Generate/watch tasks output to `SpecScribeOutput`.
  [`tasks.json:35`](../../.vscode/tasks.json#L35)

- Ignore the new output folder (was `docs/live/`).
  [`.gitignore:490`](../../.gitignore#L490)

- Documented default in the options table.
  [`README.md:61`](../../README.md#L61)
