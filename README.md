# SpecScribe

**SpecScribe turns spec-driven-development artifacts into a human-readable website.**

Frameworks like [BMad](https://github.com/bmad-code-org/BMAD-METHOD) (including its GDS game-development
submodule) produce a wealth of markdown artifacts — PRDs, GDDs, epics, stories, requirements inventories,
and architecture decision records. Those files are written for AI agents and power users, not for humans
skimming project status. SpecScribe watches those artifacts and renders them into a styled, navigable,
cross-linked static HTML site: epic and story dashboards with progress gauges, requirements traceability
pages, rendered mermaid diagrams, and ADR indexes — regenerated live on every save.

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
| `--output <DIR>` | `<repo root>/docs/live` |
| `--project-name <NAME>` | `project_name` from `_bmad/config.toml`, else "BMad Live Docs" |

With no options, SpecScribe auto-discovers a BMad project from wherever you run it — so inside a
BMad repo, plain `specscribe generate` just works.

## What it renders

- **Dashboard** — project-wide progress, epic/story completion gauges, git activity stats
- **Epics & stories** — parsed from BMad `epics.md` structure, grouped and cross-linked, with status pills
- **Requirements traceability** — FR/NFR inventory with epic coverage maps; requirement IDs in any
  document become anchor links
- **ADRs** — hand-authored architecture decision records rendered with rewritten cross-links
- **Mermaid diagrams** — fenced ` ```mermaid ` blocks render client-side
- **Task lists** — GitHub-style checkboxes render as progress

Source files are always read with shared access; the watcher never holds a write lock on anything
it observes.

## Roadmap

- **[Spec Kit](https://github.com/github/spec-kit)** support — render `specify` constitutions, specs, plans, and task lists
- **GSD** support
- **Git insights** — richer history-derived views (velocity, file heatmaps) beyond the current commit stats
- **Directory-structure insights** — project-layout overviews generated from the tree itself

## Development

```
dotnet build            # build everything
dotnet test             # run the unit tests
dotnet run --project src/SpecScribe -- generate    # run without installing
```

The solution is `SpecScribe.sln`; the tool lives in `src/SpecScribe`, tests in `tests/SpecScribe.Tests`.

## License

[MIT](LICENSE) — Copyright (c) 2026 Matt Eland
