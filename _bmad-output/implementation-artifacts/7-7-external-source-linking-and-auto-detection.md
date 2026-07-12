---
baseline_commit: 5691d24b30be909f6690504525516c61329d0924
---

# Story 7.7: External Source Linking and Auto-Detection

Status: review

## Story

As a maintainer whose repository is hosted on a platform like GitHub or GitLab,
I want each in-portal code page to link out to the same file's hosted source, with the base URL detected automatically,
so that readers can reach the canonical, syntax-highlighted source without my having to hand-configure a URL.

## Acceptance Criteria

1.
**Given** a repository with a recognizable hosting remote, or a GitHub Pages / CI deployment context
**When** the site is generated without an explicit source-URL override
**Then** the external source base is derived automatically from the git remote or the deployment environment
**And** an explicit override always takes precedence, and an unrecognizable or absent remote degrades to in-portal-only with no error. [Source: epics.md#Story 7.7]

2.
**Given** an external source base is configured or detected
**When** code pages are generated
**Then** in-portal code pages are still generated and each gains an additive "view source online" link to the hosted file
**And** source citations continue to resolve to the in-portal pages — the external base is additive, never a replacement — and the setting is reachable from both the CLI and the interactive menu (NFR7). [Source: epics.md#Story 7.7]

---

## Developer Context

This story reframes the `--code-url` capability shipped by Stories 7.1/7.2. Those stories treated a set
external base URL as a **replacement**: with it set, in-portal code pages were skipped entirely and every
citation was rewritten straight to the hosted platform. Owner feedback (2026-07-12) is that the portal's
value is the in-portal browsing + relationship view, and the hosted source is a nice *additional* jump-off —
so the base URL becomes **additive**, and it should **auto-detect** rather than require hand-configuration.

**Scope boundary:** the relationship-first / graph redesign of the code page itself is Story 7.1 (reopened);
this story owns (a) flipping the gate to additive, (b) auto-detecting the base URL, (c) the per-page "view
source online" link, and (d) menu/CLI parity + diagnostics for the setting.

## Technical Requirements (Dev Agent Guardrails)

**DO**
- Keep the `#L{n}` line-anchor scheme untouched (shared cross-story convention).
- Route the git calls through the existing timeout-guarded, failure-tolerant `GitMetrics.RunGit` seam.
- Keep URL parsing pure and unit-testable, split from the git/env I/O.
- Keep detection **out of the default `ForgeOptions.Resolve` path** — gate it behind `autoDetectCodeUrl`, which
  only the CLI (`SiteSettings.Resolve`) sets true — so library/test callers (incl. the golden fingerprint) stay
  deterministic and never pick up the running machine's real remote.

**DON'T**
- Don't divert citations to the external base — they resolve in-portal now.
- Don't fail generation on a missing/odd remote; degrade to in-portal-only silently.
- Don't persist a detected (branch-specific) URL as if it were an explicit setting.

## File Structure Requirements

- New: `src/SpecScribe/CodeSourceUrlResolver.cs` — remote/CI parse → blob base (pure).
- `src/SpecScribe/GitMetrics.cs` — `TryGetRemoteUrl` / `TryGetCurrentBranch` via `RunGit`.
- `src/SpecScribe/ForgeOptions.cs` — `autoDetectCodeUrl` gate; `CodeSourceBaseUrl` doc updated to additive.
- `src/SpecScribe/SiteSettings.cs` — CLI opts into detection; `--code-url` help text updated.
- `src/SpecScribe/SiteGenerator.cs` — removed external-mode gates; per-page external URL; citations resolve in-portal.
- `src/SpecScribe/CodeFileTemplater.cs` — additive "view source online" link (host-aware label).
- `src/SpecScribe/Commands.cs` — `CodeUrl` in interactive `ConfigurePaths` (menu parity).
- `src/SpecScribe/DiagnosticsTemplater.cs` — "External source base" row on the config log.

## Testing Requirements

- Unit: remote parsing (GitHub/GitLab, HTTPS/SSH/`.git`, credentials, subgroups, `ssh://`), CI-env building
  (SHA over ref over main), CI-over-remote precedence (`CodeSourceUrlResolverTests`).
- Generation: external base is additive — in-portal pages still generated + carry the link; citations stay
  in-portal (`SiteGeneratorCodePagesTests`, `SiteGeneratorCodeCitationTests`).
- Templater: view-source link markup + host-aware label (`CodeFileTemplaterTests`).
- Golden fingerprint rebaselined for the new CSS.

## Dev Agent Record

Implemented 2026-07-12 alongside the Story 7.1 rework.

- `CodeSourceUrlResolver.TryDetect(repoRoot, env?)`: CI env first (GitHub Actions vars, pinning to the immutable
  `GITHUB_SHA` when present), else the git `origin` remote. `ParseRemote` handles HTTPS (incl. embedded creds),
  scp-like SSH, and `ssh://`/`git://` URIs; GitLab uses `/-/blob/` and keeps subgroups in the owner path.
- Gate flipped: `SiteGenerator.DiscoverCodeReferences`/`GenerateCodePagesInternal` always run; `BuildExternalSourceUrl`
  joins base + repo-relative path (no `#L`, whole-file link); the `CodeReferenceLinkifier` call passes a null base so
  citations always resolve to the in-portal pages.
- `CodeFileTemplater` renders `.code-external-link` with a host-derived label ("View on GitHub"/"View on GitLab"/…),
  `rel="noopener noreferrer"`.
- Detection verified end-to-end: a real `generate` on this repo auto-derived
  `https://github.com/IntegerMan/SpecScribe/blob/main/<path>` from the git remote and rendered the "View on GitHub"
  link on every code page while keeping in-portal browsing.
- Full suite green (881 tests, +22).

## Change Log

- 2026-07-12: Created and implemented. `--code-url` is now additive (in-portal pages always generate; each gains a
  hosted-source link) with git-remote + GitHub-Pages/CI auto-detection, menu/CLI parity, and a diagnostics row.
