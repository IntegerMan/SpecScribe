# Epic 16 Context: Release Engineering & Community Preview Launch
<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Put a dependable, explicitly preview-stage SpecScribe release in community hands. Epic 16 covers a reproducible clean-checkout CI gate, CLI distribution, release preparation and promotion, release-facing documentation, and launch readiness. It covers FR32 (release engineering), FR33 (read-only VS Code Marketplace distribution), FR34 (install/upgrade documentation, changelog, and version policy), and FR18 (OSS onboarding), under NFR9: CI must build, test, and package releases from a clean checkout before any distribution publish.

The release sequence is Epic 5 operational completion, Epic 17 hardening sign-off, then Epic 16 publication. The product remains CLI-first, local-first, and read-only; the extension is a follow-on read-only surface sharing the core projection/rendering model.

## Stories

- **16.1 - Release & Distribution Packaging Spike** (ADR 0040)
- **16.2 - Continuous Integration Build & Test Gate**
- **16.3 - CLI Packaging and Publication**
- **16.4 - Tag-Triggered Release Pipeline**
- **16.5 - VS Code Extension Packaging and Marketplace Publication**
- **16.6 - OSS Onboarding, Release-Facing Documentation, Changelog, and Versioning Policy**
- **16.7 - Preview Launch Readiness and Cut**
- **16.8 - npx Distribution via npm-Wrapped Native Binary**
- **16.9 - Composite GitHub Action for External-Project CI/CD Consumption**
- **16.10 - Release-Branch Coverage (post-preview)**

## Requirements & Constraints

- Release builds are reproducible in the NFR9 sense: CI builds from a clean checkout with a passing build and test run; byte-identical rebuilds are not promised. Tag history is required for MinVer.
- The first preview cut is ordered: NuGet dotnet global tool, npm/npx, then self-contained binaries for `win-x64`, `linux-x64`, and `osx-arm64`. The VSIX/Marketplace path is explicitly out of this first cut. The dotnet tool remains the actionable fallback for deferred binary RIDs.
- Packaged CLI consumers must receive the required renderer without requiring a repository checkout or `SPECSCRIBE_RENDERER_DIR`. The CLI and renderer are one released unit; npm uses an exact renderer dependency, and binary archives must contain the matching renderer.
- Consumer documentation must use real commands and disclose prerequisites: Node within the supported range and .NET 10 for the dotnet tool. `--help` and `--version` must agree with published documentation. The unsigned binary experience and its SmartScreen/Gatekeeper consequences must be documented.
- Publishing uses NuGet Trusted Publishing, npm Trusted Publishing, and the per-run GitHub token. `release.yml` is policy-bound for NuGet, declares no environment, and requires the `NUGET_USER` repository variable. Do not add an API-key path without an ADR amendment.
- Primary package identities are `SpecScribe`, `specscribe`, and `specscribe-renderer`; unavailable or unowned primary IDs require owner escalation, not a silent fallback.

## Technical Decisions

- ADR 0040 is accepted and binding. Versioning is MinVer-derived SemVer `0.MINOR.PATCH-preview.N`. MINOR covers new user-visible features and breaking changes; PATCH covers fixes and internal/docs work. Promotable releases retain a preview label. The extension mirrors CLI MINOR and has its own monotonic plain SemVer PATCH counter.
- Releasing is continuous on `main` in two stages. Stage A follows successful `build-test-analyze`, assigns the next preview tag, builds assets, and creates a prerelease GitHub Release with binary archives and SHA-256 digests. Stage B is manual dispatch for a Stage A tag and Release; it performs credential exchange, registry preflight, ordered publication, and release-body completion.
- Registry publication is non-transactional. First publication consumes a version permanently. Failed or partial promotions are withdrawn on affected channels and recovered with a new preview number, never by retrying the same version. Unpromoted Stage A tags and Releases may be deleted.
- The changelog follows Keep a Changelog 1.1.0. User-visible changes use independently authored `changelog.d/<story-key>.md` fragments; promotion deterministically assembles `CHANGELOG.md` in a PR. Stage B appends its section above the digest block. Missing or empty release notes warn and continue, but the first public preview requires non-empty backfilled notes before tagging.
- The preview does not code-sign binaries. SHA-256 digests on the GitHub Release are the integrity control for direct-download archives.

## Cross-Story Dependencies

- Epic 16 follows Epic 5 and the Epic 17 release-readiness pass. Story 16.7 is blocked by Story 17.4's hardening sign-off and Story 23.7's thin-repository empty-state fix.
- Story 16.5 depends on the Epic 6 extension and its workspace-trust prerequisite; Marketplace release remains deferred from the first preview.
- Story 16.4 consumes the CI foundation from Story 16.2 and packaging from Story 16.3; it must honor ADR 0040 rather than older human-tagged pipeline descriptions.
- Stories 16.8 and 16.9 depend on the packaged CLI/renderer unit from Story 16.3. Story 16.6 supplies content used by the launch-readiness cut.
- Story 16.10 is intentionally post-preview. Preview support is forward-fix only from `main`; release branches and hotfix flow are not introduced early.