# Settings and Signals

This companion defines settings persistence and extension relevance signals in operational terms.

## Directory-Scoped Settings

Settings are organized as a file associated with the active source directory so behavior can vary by project.

| Area | Required Controls | Access Surface |
|---|---|---|
| Core generation | source/output paths, include/exclude classes, watch mode behavior | interactive options + CLI parameters |
| Git insights | baseline pulse on/off, depth tier, time window, hotspots/coupling toggles | interactive options + CLI parameters |
| ADR coverage | ADR discovery path, include/exclude statuses, rendering detail tier | interactive options + CLI parameters |
| Framework adapters | enabled frameworks/modules, coverage tiers, unsupported handling policy | interactive options + CLI parameters |
| Output/documentation | portal sections, README table emission mode, description verbosity | interactive options + CLI parameters |

## Settings File Behavior

- Settings file lives in or is selected for the active source directory.
- CLI parameters can override settings-file values for a run.
- Interactive changes can be persisted back to the directory-scoped settings file.
- Effective configuration is shown to users before generation/watch starts.

## Concrete Relevance Signals Explained

"Concrete relevance signals" are measurable triggers that tell you extension work is worth doing now rather than later.

| Signal | How to Measure | Why It Matters |
|---|---|---|
| Active usage pull | repeated requests from maintainers/users for in-IDE read-only status views | proves real demand |
| Workflow friction | repeated context switches to browser or external viewers, plus frequent watch-mode setup overhead | indicates in-IDE status surfaces and helper prompts can remove friction |
| Reliability readiness | no unresolved critical parser/generation defects in current CLI usage | avoids shipping UI over unstable core |
| Delivery capacity | maintainer time exists for extension support and bug triage | prevents extension drag on core roadmap |
| Editor-share fit | VS Code-family editor usage is high enough to benefit from extension work | keeps timing aligned to real workflow |

## Minimal Decision Rule

Start extension increments when either demand or friction is clearly present, reliability plus capacity gates are satisfied, and editor-share fit indicates the extension will materially reduce current workflow friction.

## Architectural Planning Note

- Build one shared projection and rendering core.
- Feed both static HTML generation and VS Code webview adapters from that core.
- Keep adapter-specific code limited to delivery concerns (routing, host APIs, packaging), not parser/projection logic.
