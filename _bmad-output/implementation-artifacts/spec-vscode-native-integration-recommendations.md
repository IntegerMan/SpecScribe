---
title: 'VS Code Native-Integration Recommendations Document'
type: 'chore'
created: '2026-07-11'
status: 'done'
route: 'one-shot'
---

# VS Code Native-Integration Recommendations Document

## Intent

**Problem:** The Story 6.4/6.5 extension is deliberately a minimal shim (one command, one panel, one setting, two hardcoded watch globs), and there was no consolidated survey of how SpecScribe could integrate more natively into VS Code — discoverability, native surfaces, settings, UX, and file-change reactivity — across both the current implementation and future stories (Epics 5, 7, 8, 16).

**Approach:** Research the shipped extension, the `specscribe webview` CLI seam, ADRs 0003/0005/0006, and Epic 5/6/7/8/16 stories; then write `docs/VSCodeIntegrationRecommendations.md` — 25+ recommendations (R1–R8) with effort tags, constraint analysis (thin shim, read-only, directory-scoped settings), a sequencing table, and story-seating suggestions. No code changes.

## Suggested Review Order

1. [docs/VSCodeIntegrationRecommendations.md](../../docs/VSCodeIntegrationRecommendations.md) — the whole deliverable. Read §2 (constraints) first — it frames why every recommendation is shaped the way it is; then §4 (sequencing table) for the TL;DR; then §3 for the detail.
2. Load-bearing findings to sanity-check against the code:
   - **R6.1** — both the extension globs and [FileWatcherService.cs](../../src/SpecScribe/FileWatcherService.cs) filter to `*.md`, so `sprint-status.yaml` and `_bmad/config.toml` edits never live-refresh (shipped gap).
   - **R5.3** — `specscribe webview` (and `generate`/`watch`) never consult `SettingsStore`; the `.specscribe` file is honored only by the interactive menu (known gap, seats in Story 5.2).
   - **R5.4** — Workspace Trust declaration recommended before Marketplace publication (Story 16.5).
3. Review provenance: an adversarial (Blind Hunter) review ran on the first draft and returned 11 findings (1 HIGH — the original R5.3 claimed settings resolution worked; 4 MEDIUM; 6 LOW). All 11 were patched into the document; none deferred, none rejected.
