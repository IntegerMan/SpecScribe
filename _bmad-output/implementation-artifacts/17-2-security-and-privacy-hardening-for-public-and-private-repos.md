---
baseline_commit: e8a689dca4f84ac03339c44584f155549f8497b4
---

# Story 17.2: Security and Privacy Hardening for Public and Private Repos

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->
<!-- created 2026-08-07 (create-story 17.2) at baseline_commit c73ebcb. Every line number in this file was
     resolved at that revision and WILL drift — see § "Citations drift" before trusting any `:NNN`. -->

**baseline_commit:** `c73ebcb` (`Merge branch 'worktree-story-16-2-dev'`)

## Story

As the SpecScribe maintainer,
I want the tool audited and hardened so it is safe to run on both public and private codebases,
So that neither a hostile public repo nor a sensitive private one can produce an unsafe or leaky result.

## Acceptance Criteria

Reproduced verbatim from `epics.md` § Epic 17 → Story 17.2. **Read the ⚠ notes before acting on either AC's
examples** — AC #1's three named holes are all already closed, and AC #2's third clause has an unmet
precondition.

1.
**Given** SpecScribe renders untrusted repository content into HTML and a VS Code webview
**When** the security review runs
**Then** output-injection surfaces are closed — HTML-escaping is complete and consistent (for example the unescaped detail-page `<h1>` titles, `StatusStyles.Badge`'s un-escaped `cssClass`, and the `RequirementLinkifier` attribute-injection exposure recorded in deferred-work), the webview CSP/nonce posture is verified, and the untrusted-workspace / `toolPath` tool-resolution attack surface is closed (Story 6.8's Workspace-Trust posture is present and effective)
**And** each closed hole is pinned by a regression test.

> ⚠ **All three examples AC #1 names were closed in July 2026. Do not chase them.**
> Verified by reading the code at `c73ebcb`, not by trusting the ledger:
>
> | AC #1 says | Actual state at HEAD |
> |---|---|
> | "the unescaped detail-page `<h1>` titles" | **Closed.** `deferred-work.md:677` — resolved 2026-07-18. Superseded by Story 6.2: titles are opaque `TitleHtml` from `MarkdownConverter.RenderInline` and are emitted verbatim *on purpose*; wrapping them in `PathUtil.Html` would double-escape markdown. Pinned by `RenderEpicBody_EmitsTitleHtmlInH1_WithoutPathUtilDoubleEscape`. **Re-escaping this would be a regression, not a fix.** |
> | "`StatusStyles.Badge`'s un-escaped `cssClass`" | **Closed.** `StatusStyles.cs:447-452` — the 3-arg overload now does `var cls = PathUtil.Html(cssClass);` and `var tip = PathUtil.Html(StageMeaning(iconClass));`. The 2-arg overload delegates to it. `deferred-work.md:712` records the fix (2026-07-18) with a hostile-class unit test. |
> | "the `RequirementLinkifier` attribute-injection exposure" | **Closed as misdiagnosed.** `deferred-work.md:776` — resolved 2026-07-19. `RequirementLinkifier`'s and `StoryEpicLinkifier`'s `ProtectedSplit` regexes are byte-for-byte identical, including the `<[^>]*>` catch-all-tag alternative the item asked for; attribute-corruption regression tests already exist for both in `LinkifierTests.cs`. |
>
> The AC's *intent* — "output-injection surfaces are closed" — is live, and there is real work. But the
> surface **moved** when Epic 23 changed who writes the HTML. The C# escaping bugs of Epic 1–2 are gone;
> what replaced them is a `v-html` injection channel and a process-spawn channel, neither of which existed
> when this AC was written. See § *The actual work-list*. Raised as **Q1**.

2.
**Given** SpecScribe may run on a private codebase
**When** the privacy review runs
**Then** generated output is confirmed to leak no secrets or unintended private content beyond what the source artifacts already expose, no personal-structure assumptions remain that would misrender or drop a differently-organized repo (Epic 4 de-personalization verified end to end), and third-party dependencies (C# and the extension's npm tree) are audited for known vulnerabilities
**And** local-first / no-remote-telemetry operation (NFR3) is re-confirmed for every code path added since it was last verified
**And** the audit scope explicitly includes the CI supply chain introduced by Epic 25 (the SonarScanner and any CI actions, plus the third-party service's access to the repository) and, if Epic 26 shipped, its external-service integration — verifying that no credential value reaches generated output or a committed settings file (NFR12), that the integration is off by default, and that the NFR3 re-confirmation accounts for the outbound network path Story 26.2's ADR authorized.

> ⚠ **Two scope corrections, both verified in `sprint-status.yaml` at HEAD:**
>
> - **Epic 26 has NOT shipped, so its whole clause is inapplicable.** `26-2-ingestion-posture-and-credential-spike` is `ready-for-dev` (the credential *spike* has not even run); `26-3` through `26-7` are `backlog`. There is no external-service integration, no credential, and no ADR-authorized outbound path to audit. Record this as *condition unmet* and hand the clause forward to Story 17.4 — **do not** invent a credential audit for code that does not exist.
> - **Epic 25 HAS shipped and its clause is live.** `25-1` … `25-6` are all `review`, and `.github/workflows/build-test-analyze.yml` exists and is a required check on `main` (Story 16.2, commit `a2eee2a`). The SonarScanner, the CI actions, and SonarCloud's repository access are all real and in scope.
>
> Also note AC #2 says "the extension's npm tree" — written before `web/` existed. `web/`'s tree is now a
> **shipped** supply-chain surface (the prebuilt Nuxt renderer is part of the product per ADR 0022), even
> though its packages sit under `devDependencies`. It is in scope. Raised as **Q2**.

## Scope

**In scope**

- The `v-html` / raw-markdown-HTML injection channel (C# `MarkdownConverter` → IR → `web/` renderer).
- Static-site CSP posture (there is none today) and verification of the webview's (there is a strict one).
- Process-spawn / tool-resolution hardening: the 3 `S4036` sites plus the extension's `resolveTool`.
- The ReDoS band: `csharpsquid:S6444` (**174 at HEAD**) — routed here by name in `docs/SonarCloudSetup.md:490`.
- Workspace Trust effectiveness (Story 6.8 posture).
- Dependency audit: C#, `extension/`, `web/`.
- NFR3 re-confirmation over code paths added since last verified — principally `NuxtPrerender.cs`.
- CI supply chain: action pinning, scanner pinning, token handling, third-party repo access.

**Out of scope — and who owns it instead**

| Not this story | Owner |
|---|---|
| The ~300-issue maintainability band (`S1192`/`S3776`/`S3358`/`S107`/`S125`…) | **Story 17.1** (`deferred-work.md:1262` routes it there by name) |
| Performance/at-scale bounding, unbounded git-log payloads, byte-blind emitters | **Story 17.3** |
| Cluster disposition, story-candidate seating, the TypeScript test harness, `check:ir-content` / ADR 0033 §4 re-measurement | **Story 17.4** (its ACs 2–4) |
| `specscribe.css` / `SiteGenerator.cs` file-scale split | **Story 17.5** |
| Epic 26's credential and outbound-integration audit | **Deferred — precondition unmet** (see AC #2 ⚠) |

**Boundary with 17.1.** 17.1 and 17.2 will both touch `src/SpecScribe/*.cs`, and several files (`EpicsParser.cs`,
`SiteGenerator.cs`, `CodeReferenceLinkifier.cs`, `IdeaDiscovery.cs`) appear in both work-lists. Per CLAUDE.md
§ *Scoping a code review*, **attribute by hunk, not by file**, and say so in the record. A regex you change for
a timeout is yours; a duplicated string literal in the same method is 17.1's.

## The actual work-list (verified at `c73ebcb`)

### A. The injection surface moved: raw markdown HTML → `v-html`, on a site with no CSP

This is the headline finding and the largest single item in the story.

**The channel, end to end — each hop verified in code:**

1. `MarkdownConverter.BuildPipeline()` (`MarkdownConverter.cs:25-31`) builds `new MarkdownPipelineBuilder().UseAdvancedExtensions()` with **no `DisableHtml()`**. Raw HTML in a source `.md` therefore passes through.
2. `CommentAnnotationRenderer.cs:12` confirms this in the repo's own words: the comment renderer special-cases `HtmlBlockType.Comment` and "**Every other `HtmlBlockType` falls through** to Markdig's default raw (browser-invisible) passthrough."
3. **ADR 0016** makes that rendered prose HTML the IR's payload, carried **verbatim** as strings.
4. `web/components/surfaces/IrSurface.vue` injects it with `v-html`. ADR 0016 §Consequences: `v-html` of an IR string is "measurably byte-faithful" — which is exactly the property that makes it an injection sink.
5. `PathUtil.RenderHeadOpen` (`PathUtil.cs:138-146`) emits `charset`, `viewport`, `description`, `og:*` — and **no `Content-Security-Policy`**. Grep confirms no CSP anywhere in `web/`.

**Why the existing defenses do not cover it.** `IrSurface.vue:34` reasons: "`v-html` never executes injected
`<script>` tags." That is true and it is not sufficient. `innerHTML` does not run `<script>`, but it *does* run
`<img src=x onerror=…>`, `<svg onload=…>`, and `javascript:` URLs. **ADR 0032**'s measured invariant
(0 executable scripts in-region, 163 inert JSON islands) and `IrSurface.vue`'s build-time throw both key on
**script islands only** — neither one looks at event-handler attributes.

**The asymmetry that makes this a clear defect rather than a judgement call.** **ADR 0021** §Decision already
writes the exact policy, for *foreign* carried HTML:

> **No script.** Not a `<script>` tag, not an inline event handler, not a `javascript:` URL, and not an
> embedding element (`<iframe>`/`<object>`/`<embed>` — an `iframe srcdoc` executes).

…and `IdeaDiscovery.UnsafeReportPattern` (`IdeaDiscovery.cs:69-71`) implements it:

```csharp
@"<\s*(?:script|iframe|object|embed)\b|\son[a-z]+\s*=|javascript\s*:"
```

So a *foreign* `forge-report.html` is refused unless it is script-free — while the repo's **own** `.md` files,
which reach the same output through `v-html`, are not checked at all. Same threat, opposite treatment, policy
language already written and already shipped.

**Threat, stated plainly.** A hostile public repo commits a `.md` containing `<img src=x onerror="…">`.
SpecScribe renders it into the portal. Anyone opening the generated site executes it. This repo's own
`publish-docs-live-pages.yml` publishes to GitHub Pages, so the realistic impact is stored XSS on a
`*.github.io` origin. The **webview is not affected** (nonce-locked `script-src` blocks inline handlers —
ADR 0032); the **static site is**, because it has no CSP at all.

> **Verify before you fix.** Reason 1→5 is documentary, not measured. Task 1 requires you to *demonstrate* the
> vector end to end and keep the artifact. If it does not reproduce, say so and stop — do not fix a
> hypothetical. CLAUDE.md is explicit that a green harness has hidden exactly this class of thing before.

Two decisions here change a cross-cutting contract, so per CLAUDE.md § *Decision records* they are **ADR
candidates, not story notes** — see Q3 and Q4.

### B. Tool resolution: `git` and `node` are spawned by bare name (the 3 × `S4036`)

Sonar flags exactly three, and all three are in AC #1's "tool-resolution attack surface":

| site | code at HEAD |
|---|---|
| `GitMetrics.cs` `RunGit` (~`:1480-1496`, Sonar says `:1487`) | `FileName = "git"`, `WorkingDirectory = workingDirectory` (**the repo being analyzed**), `UseShellExecute = false` |
| `NuxtPrerender.cs` `VerifyNodeAvailable` (~`:147-160`, Sonar says `:151`) | `new ProcessStartInfo("node", "--version")`, no `WorkingDirectory` → inherits SpecScribe's own cwd |
| `web/scripts/build-package.mjs:55` (`javascript:S4036`) | "Make sure the `PATH` variable only contains fixed, unwriteable directories" |

**The mechanism, stated precisely — and the subtlety that decides whether it is real.** On Windows,
`Process.Start` with `UseShellExecute=false` and a bare file name reaches `CreateProcessW`, whose documented
search order includes **the current directory of the *calling* process** ahead of `PATH`. It is *not* the
child's `lpCurrentDirectory` (the `WorkingDirectory` property) that is searched. So the vector requires
SpecScribe's **own** cwd to sit inside the hostile repo — which is precisely the normal invocation
(`cd some-cloned-repo && specscribe generate`). A `git.exe` or `node.exe` committed at a repo root would then
be preferred over the real one.

**Do not take that on my word — it is the one claim in this story most likely to be subtly wrong.** Verify
empirically (Task 2) on Windows before hardening, and record the result either way. Note also that .NET may
set `NoDefaultCurrentDirectoryInExePath` in some hosting configurations; that is exactly why this needs
measuring rather than reasoning.

**Reuse, do not reinvent.** `extension/src/extension.ts`'s `resolveTool()` already implements 3-tier
resolution (setting → bundled binary → PATH) and `deferred-work.md:789` records it as the hardened
production answer. The C# side has no equivalent. Model the fix on it.

### C. ReDoS: 174 × `csharpsquid:S6444` — and the band is growing

Routed here by name: `docs/SonarCloudSetup.md:490-491`, `deferred-work.md:1246`. The rationale is sound and
worth repeating: *SpecScribe parses markdown, epics, and sprint files from arbitrary third-party repositories*,
so catastrophic backtracking is an input-driven surface, not a theoretical one. It is the sole driver of the
project's security rating **C**.

**Current counts, read from the digest at HEAD** (see § *Analysis digest* for its staleness):

| rule | ledger said (2026-07-27) | digest says now | delta |
|---|---|---|---|
| `csharpsquid:S6444` | 156 | **174** | +18 |
| `csharpsquid:S4036` | 1 | **2** | +1 |
| `javascript:S4036` | — | **1** | new |

Top files: `EpicsParser.cs` 21 · `RenderParity.cs` 16 · `GsdCoreArtifactAdapter.cs` 15 · `SiteGenerator.cs` 13 ·
`FollowUpRefs.cs` 9 · `DeferredWorkParser.cs` / `RequirementsParser.cs` / `RetroParser.cs` / `SpaDelivery.cs` 7
each. 48 files in total.

**Two things follow from `+18 in 11 days`, and they should shape the fix:**

1. **A one-time sweep of 174 call sites will re-rot.** Prefer a construction seam — a shared helper, or
   `[GeneratedRegex]`/`RegexOptions.NonBacktracking` as the house default — plus a test that fails on a bare
   `new Regex(` without a timeout or `NonBacktracking`. That converts a recurring 174-item chore into an
   invariant. This mirrors what 17.1 is doing for SSOT drift.
2. **Not all 174 are equal, and the story should not pretend they are.** `RenderParity.cs` (16) is
   parity-harness code and `web/scripts/build-package.mjs` is a build script — neither consumes hostile repo
   content. Adjudicate per-file by *input provenance*: regexes that read third-party repo content get hardened
   first; the rest may be batched or explicitly deferred with a recorded rationale. **ADR 0035 §Decision 5
   rules out a blanket rule suppression as the route** — same constraint 17.1 operates under.

> ⚠ `RegexOptions.NonBacktracking` is not a drop-in for every pattern: it rejects lookarounds, backreferences,
> and atomic groups. Several patterns here use them (`IdeaDiscovery.UnsafeReportPattern`'s `\son[a-z]+\s*=`
> is fine; `ProtectedSplit` and the linkifiers should be checked). Where it will not compile, use an explicit
> `matchTimeout`. Expect a mix, and do not force one answer across the band.

### D. CSP posture: the webview is verified and strict; the static site has none

**ADR 0032** did this work already, at whole-site scale over 1,469 IR pages, and its result is *stronger* than
expected: **no relaxation** — `script-src` stays nonce-locked with no `'unsafe-inline'` and no
`'strict-dynamic'`. Confirmed unchanged at HEAD in `WebviewRenderAdapter.cs:63-64`:

```
default-src 'none'; base-uri 'none'; form-action 'none'; img-src __CSP_SOURCE__ data: https:;
style-src 'unsafe-inline' __CSP_SOURCE__; script-src 'nonce-__NONCE__'; font-src __CSP_SOURCE__ data:;
```

Nonce forgery is already closed: `composeEntryHtml()` extracts rendered content behind a random per-call
sentinel before substituting `__NONCE__`/`__CSP_SOURCE__`, so region content cannot forge a shell token
(`extension.ts:1960-1981`, `deferred-work.md:794`).

**So AC #1's "webview CSP/nonce posture is verified" is largely already satisfied — by an ADR that is still
`Status: Proposed`.** ADR 0032 and ADR 0016 both read "Proposed (… ratification is the owner's)". Verifying a
posture whose governing record is unratified is half a job. Re-run the verification, then propose ratification
(**Q3**).

The gap ADR 0032 does **not** cover is the static site, which has no CSP at all — the other half of item A.

### E. Workspace Trust (Story 6.8): present, and coverage is complete

`extension/package.json:18-25`:

```json
"capabilities": { "untrustedWorkspaces": { "supported": "limited",
  "restrictedConfigurations": ["specscribe.toolPath"] } }
```

The extension contributes exactly **two** settings — `specscribe.toolPath` and `specscribe.openLocation`.
The execution-bearing one is restricted; the other selects where to open a file. **Coverage is therefore
complete, not partial** — I checked for a second execution-bearing setting (an args/prefix option) and there
is none.

There is no `workspace.isTrusted` check in the extension source, and that is fine: `restrictedConfigurations`
is the declarative mechanism, enforced by VS Code, and it is the correct one. What is missing is **evidence**.
AC #1 asks for "present *and effective*", and nothing pins it — a future contribution of a third,
execution-bearing setting would silently ship unrestricted. That is the test to write.

### F. Privacy / NFR3: one new network path, and one unmeasured binding

**Every network call in the product, enumerated:** `NuxtPrerender.cs` only. `HttpClient` with
`BaseAddress = new Uri($"http://127.0.0.1:{port}/")` (`:276`, `:358`). The extension TypeScript has **zero**
outbound calls (no `fetch`, no `http.request`, no `axios`). So NFR3 holds by construction — this is loopback
to a locally-spawned renderer, not telemetry.

**But `NuxtPrerender` is exactly the "code path added since NFR3 was last verified" the AC targets, and it has
an unmeasured privacy edge.** The spawn sets `PORT` and `NITRO_PORT` (`:262-263`) and **does not set `HOST` or
`NITRO_HOST`**. `FreePort()` (`:244-251`) binds `IPAddress.Loopback` only to *pick* a free port and immediately
releases it — it does not constrain what the Node server then binds. If Nitro's node-server preset defaults to
all interfaces when `HOST` is unset, then for the duration of every `generate`, a server rendering **the whole
private repository's portal** is reachable from the LAN on an ephemeral port.

> **This is UNVERIFIED and must be measured, not assumed.** `web/node_modules` is not installed in a fresh
> worktree (the known-broken `npm ci`, Story 23.5), so I could not read Nitro's default. Measure it (Task 5);
> if it binds loopback-only, record that and close the item. If it binds `0.0.0.0`/`::`, the fix is one line:
> set `NITRO_HOST=127.0.0.1` beside the existing `NITRO_PORT`.

**Also in this bucket:** confirm generated output leaks nothing beyond what the source exposes, and that Epic 4
de-personalization holds end to end (Epic 4 is `in-progress`; `4-9-multi-framework-coexistence-strategy-spike`
is `review`). `Charts.cs`/git-insights surfaces render author names and emails from `git log` — that is
*already-exposed* repository data, so it is in-bounds, but the audit should say so explicitly rather than leave
it unexamined.

### G. Dependencies — measured at HEAD, today

| tree | result | note |
|---|---|---|
| C# (`SpecScribe.csproj`, incl. transitive) | **0 vulnerable packages** | `dotnet list package --vulnerable --include-transitive` against nuget.org, 2026-08-07. Direct deps: Markdig 1.3.2, Spectre.Console 0.57.2, Spectre.Console.Cli 0.55.0, YamlDotNet 18.1.0 |
| `extension/` | **0 vulnerabilities** | `npm audit`. Notable: **zero runtime dependencies** — only `esbuild`/`typescript`/`@types`. Nothing from npm ships in the VSIX. Excellent posture; record it |
| `web/` | **1 high** | `brace-expansion` — GHSA-mh99-v99m-4gvg + GHSA-rgw5-rvv9-x895 (DoS via unbounded expansion → OOM). Reached via `archiver-utils`, `readdir-glob`, and directly. `npm audit fix` available |

> ⚠ **Sequencing trap on the `web/` fix.** `npm ci` in `web/` is **already broken** at HEAD (EUSAGE, missing
> `@emnapi/runtime@1.11.3` from the lock file) and fixing the lockfile is **Story 23.5's** — which is `review`,
> not merged. `npm audit fix` rewrites that same lockfile. Do not land an audit fix that collides with 23.5.
> Coordinate, or fix the lockfile first and verify `npm ci` succeeds before and after. Raised as **Q5**.

Practical read on severity: `brace-expansion` here is a build-time DoS in a glob dependency, not a
product-runtime vulnerability. Fix it, but do not let it outrank items A–C.

### H. CI supply chain (Epic 25) — mostly sound, two real gaps

**Already correct — verify and record, do not "fix":**

- `permissions: contents: read` — least privilege, with a written rationale (SonarCloud PR decoration uses the GitHub App's own installation token, not `GITHUB_TOKEN`).
- Trigger is `pull_request`, **not** `pull_request_target` — so a fork PR never gets secrets. This is the single most important thing to get right and it is right.
- `SONAR_TOKEN` comes from `secrets.SONAR_TOKEN` into `env:` and is referenced as `$env:SONAR_TOKEN`, never interpolated into a script body.
- Sonar steps are `if: env.SONAR_TOKEN != ''`, so a fork PR degrades rather than failing.

**The two gaps:**

1. **The scanner is unpinned, and its cache never refreshes.** `dotnet tool update dotnet-sonarscanner --tool-path .\.sonar\scanner` installs *latest* on a cache miss, and the `actions/cache@v4` key `${{ runner.os }}-sonar-scanner` has **no version component** — so once populated, the cached binary is pinned indefinitely with no refresh path. Worst of both: unpinned on first write, frozen forever after. This is **already routed here by name** (`deferred-work.md:1215`: *"pinning it is a legitimate 17.2 call… flagged here rather than decided unilaterally"*). Fix both halves together — pin a version *and* put it in the cache key.
2. **Every action is on a floating major tag** (`actions/checkout@v4`, `setup-dotnet@v4`, `setup-node@v4`, `setup-java@v4`, `cache@v4`, `upload-pages-artifact@v4`, `deploy-pages@v5`). All are first-party `actions/*`, so risk is lower than a third-party action — but GitHub's own hardening guidance is SHA pinning. Decide deliberately and record the decision either way; a documented "floating tags accepted for first-party actions" is a valid outcome.

**Third-party repository access** — the SonarQube Cloud GitHub App has installation-level access to the repo.
That is a configuration review, not a code one: check the app's granted scopes and record them, alongside
`docs/SonarCloudSetup.md` § *Security notes* (`:625`).

## Tasks / Subtasks

**Sequencing is load-bearing.** Task 0 first. Tasks 1 and 2 both *measure before fixing* and their measurements
decide the shape of the work — do not skip to remediation.

- [x] **Task 0 — Baseline before touching anything (AC: #1, #2)**
  - [x] `git rev-parse HEAD`, and record it as this story's real baseline (this file says `c73ebcb`). **Real dev baseline: `e8a689d`** — 6 merges after the create-story baseline. Recorded in this file's YAML frontmatter.
  - [x] Refresh the analysis digest: `node tools/analysis-digest/index.mjs`. **At authoring time the digest's `analysisRevision` was 15 commits behind HEAD** (`isStale: true`, `analysis-behind-working-tree`) — every line number in § C is anchored to `01acf5b1` and *will* have moved. Re-resolve by symbol. **Refreshed at `e8a689d`: 1753 observations (143 error / 1180 warning / 430 note) across 236 shards, `commitsBehind: 0`, `isStale: false`.**
  - [x] Re-count `S6444`/`S4036` from the refreshed digest. The band grew 156 → 174 in 11 days; assume it moved again. **It did: `csharpsquid:S6444` = 175 (156 → 174 → 175), `csharpsquid:S4036` = 2, `javascript:S4036` = 1. 46 files carry S6444 (story said 48). Top files unchanged in rank: `EpicsParser.cs` 21 · `RenderParity.cs` 16 · `GsdCoreArtifactAdapter.cs` 15 · `SiteGenerator.cs` 13 · `FollowUpRefs.cs` 9.**
  - [x] Do **not** run `npm run check:ir-content` as a health signal yet — it is red in a fresh worktree for environmental reasons (no IR ⇒ nearly everything pruned). If you need it, run the full load-bearing order from CLAUDE.md first. Its true state is **Story 17.4's** to establish, not yours.

- [x] **Task 1 — Prove, then close, the `v-html` injection channel (AC: #1)**
  - [x] **Measure first.** Add a `.md` fixture containing `<img src=x onerror="…">`, `<svg onload="…">`, and a `javascript:` link. Generate. Confirm whether the handler survives into the shipped `.html`. **Keep the artifact.** If it does not reproduce, record that and stop — items A/Q3/Q4 collapse. **IT REPRODUCED, and then EXECUTED.** Every vector survived verbatim into the shipped HTML, and a live-browser (CDP/Edge) load of the generated page set **three** markers — `__SPECSCRIBE_XSS_IMG`, `__SPECSCRIBE_XSS_SVG`, `__SPECSCRIBE_XSS_FRAME` (the `iframe srcdoc` executed against the *parent* window). Fixture preserved as `tests/SpecScribe.Tests/HtmlSafetyTests.cs`; the rendered before/after is quoted in ADR 0042.
  - [x] Confirm the same fixture is inert in the **webview** (expected: nonce-locked CSP blocks it, per ADR 0032). A differing answer between the two surfaces is itself the finding. **Confirmed, and the differing answer WAS the finding:** the webview policy string at `WebviewRenderAdapter.cs:63-64` is unchanged at HEAD and `script-src 'nonce-…'` with no `'unsafe-inline'` blocks inline handlers; the static site has no CSP at all. Pinned by a new test (Task 4).
  - [x] Propose the policy decision as an **ADR** (Q4) before implementing — **ADR 0042** (markdown HTML policy) and **ADR 0043** (static-site CSP), both `Proposed`. — this changes a cross-cutting contract and amends ADR 0021's asymmetry. Options to weigh, with the trade-off stated: strip handlers/`javascript:` at render; escape raw HTML blocks entirely (breaks legitimate `<details>`/`<kbd>`/`<br>` already used in this repo's own `epics.md` — verified present, so this option has a real cost); or gate-and-diagnose in `IdeaDiscovery`'s style.
  - [x] Reuse `IdeaDiscovery.UnsafeReportPattern` rather than authoring a second pattern. If it must be shared, lift it to one place — a second copy is precisely the SSOT defect 17.1 is sweeping up. **Lifted to `HtmlSafety.ContainsExecutableMarkup`; `IdeaDiscovery` keeps the *decision* to reject and now calls it. No second pattern authored.**
  - [x] Pin with a regression test asserting the hostile fixture renders inert. **`HtmlSafetyTests`, 33 tests** — every measured vector individually, plus the obfuscation variants (`java\tscript:`, leading whitespace, mixed case), plus the *inverse* assertions that `<details>`/`<summary>`/`<kbd>`/`<br>`/`<span>`/`<sub>`/`<abbr>` and ordinary links survive byte-identically.

  **Three findings the story did not predict:**
  1. **The cheapest vector needs no raw HTML at all** — ordinary `[text](javascript:alert(1))` markdown parses to a `LinkInline` and Markdig writes the `Url` straight into an `href`. A raw-HTML-only fix would have missed it. Closed by `MarkdownConverter.NeutralizeDangerousLinks`.
  2. **A literal `<script>` was already a denial-of-service, not an injection.** It reached the IR, tripped `IrSurface`'s executable-island throw and the page returned **HTTP 500** (`errors=1`) — so hostile markdown could *delete a page* from the portal. Escaping closes that too.
  3. **`<base>`/`<meta http-equiv>` carry neither a handler nor a `javascript:` URL**, so handler-stripping alone would not catch them — and `<base>` silently re-points every relative URL on the page.

  **The trap that shaped the design:** the sanitizer operates on Markdig's raw-HTML passthrough nodes **only**, never on rendered output. This portal renders its own source, so `onerror=` appears legitimately (escaped) in code spans on the Code Map and on this story's own page — a regex pass over finished HTML would have corrupted shipped documentation with every gate green. Pinned by `EscapedProseAboutHandlersIsNotCorrupted` and `FencedCodeBlocksAreNotCorrupted`.

- [x] **Task 2 — Close the tool-resolution surface (AC: #1)**
  - [x] **Measure first, on Windows.** Put a harmless marker `git.exe` (or `node.exe`) at a scratch repo root, `cd` into it, run `generate`, and observe whether it is invoked. Record the result — this settles whether the `CreateProcess` search order reaches the repo directory in practice. **IT REPRODUCES. The story's claim is correct.** Two-arm controlled measurement with a marker binary planted as `git.exe`, cwd set to the hostile root, `WorkingDirectory` deliberately pointing elsewhere (mirroring `GitMetrics`):

    | arm | result |
    |---|---|
    | `NoDefaultCurrentDirectoryInExePath=1` | real `git version 2.55.0.windows.3` |
    | variable **unset** (the default end-user shell) | **the planted binary executed** |

    Proof it was the plant and not a silent failure: the child's own stderr read `The application to execute does not exist: 'C:\…\hostilerepo\marker.dll'` — the planted apphost looking for its sidecar in the hostile directory. Identical result for `node`. **`WorkingDirectory` is not and never was the protection.** ⚠ The guard variable *was* set inside this project's Git Bash session, which is exactly the confounder the story predicted — measuring in one shell only would have produced the wrong answer.
  - [x] If it reproduces: resolve `git`/`node` to absolute paths, modelled on `extension.ts`'s `resolveTool()` 3-tier pattern. Do not invent a second resolution scheme. **New `ToolResolver` — PATH-only search, absolute result, relative PATH entries (`.`) skipped, `PATHEXT` honored, cached.** Deliberate deviation recorded in its doc comment: `resolveTool()`'s setting→bundled→PATH cascade is right for locating *SpecScribe itself*, but `git`/`node` have no setting to honor and nothing bundled, so two of its three tiers could never fire. The reused principle — resolve to absolute before spawning, never hand a bare name to the OS loader — is what carries over.
  - [x] Address `web/scripts/build-package.mjs:55` (`javascript:S4036`) in the same pass. **Was `spawnSync('nuxt', ['build'], { shell: true })`; now resolves nuxt's own entry via `createRequire(...).resolve('nuxt/bin/nuxt.mjs')` and runs it under `process.execPath`. Removes the PATH search *and* the shell.**
  - [x] Pin with a regression test that a repo-local executable is not preferred. **`ToolResolverTests`, 6 tests** — including `SpawnSitesResolveAbsolutePaths`, which reads the shipped source, because no unit test over the resolver could see someone reverting a call site to a bare `"git"`.
  - [x] If it does **not** reproduce, still close the Sonar finding … — **not applicable; it reproduced.** End-to-end confirmation after the fix: `git.exe` planted at the root of a **real git repository**, cwd there, guard variable unset, full `generate` — 512 pages, `errors=0`, **marker never invoked**, real git used throughout.

- [x] **Task 3 — ReDoS band (AC: #1)**
  - [x] Classify all `S6444` sites by **input provenance**: third-party repo content vs first-party/harness/build. `RenderParity.cs` (16) and `build-package.mjs` are the clear second category. **Classified — all 175 sites are in `src/SpecScribe/` product code across 46 files. Second category confirmed as `RenderParity.cs` (16, parity harness over a frozen corpus) plus the `web/scripts` build script. But see the next line: the classification did not end up gating the fix.**
  - [x] Harden the first category. Prefer a **construction seam + an enforcing test** over 174 individual edits — the band grew +18 in 11 days and will grow again. **Done, and it covers BOTH categories.** New `TimedRegex.New(pattern, options)` is now the single construction point; **163 sites across 46 files** were routed through it mechanically. Because the seam is uniform, the provenance split stopped being a *deferral* decision — there was no cheaper answer for the second category than the one the first category already got. **Nothing is deferred and nothing is suppressed** (ADR 0035 §5 satisfied: the Regex objects genuinely carry a timeout; Sonar's finding disappears from 174 sites because there is no longer a Regex constructor at them).
  - [x] Expect `NonBacktracking` to be unusable on patterns with lookarounds/backreferences; fall back to an explicit `matchTimeout` there. A mixed answer is the correct answer. **Measured rather than assumed, and the answer is NOT mixed — it is uniformly "timeout".** Across all 163 sites: **33 of the 46 regex-bearing files use a lookaround**, 2 use a backreference (`RetroActionStyler`, `Toc`), 0 use atomic groups. `NonBacktracking` rejects all three at *construction* time, so as a house default it would throw at type-initialization across most of the codebase. Individual patterns can still opt in through `options`; the two compose.
  - [x] Batch or defer the second category **with a recorded rationale** (ADR 0035 §5: no blanket suppression). **Not needed — see above. Recorded as "no deferral" rather than left silent.**
  - [x] Pin at least one catastrophic-backtracking case with a timing-bounded test. **`CatastrophicBacktrackingIsBoundedRatherThanHanging`** — `^(a+)+$` against 40 `a`s plus a non-match, the textbook exponential case, asserted to raise `RegexMatchTimeoutException` inside a bounded wall clock instead of hanging.

  **The enforcing half is the durable part:** `EveryRegexIsConstructedThroughTheFactory` and `RegexFieldsDoNotUseTargetTypedNew` scan `src/SpecScribe/**/*.cs` and fail on a bare `new Regex(` or a target-typed `Regex X = new(`. Two checks are needed because 162 of the 163 sites used the target-typed form, which contains no `new Regex(` token at all.

- [x] **Task 4 — CSP verification and ratification (AC: #1)**
  - [x] Re-verify the webview policy string at HEAD and re-run ADR 0032's whole-site assertions (0 executable scripts in-region; islands inert). Match on real `<script>` **tags**, never a substring — this portal renders its own source and `code/**` pages *mention* these tokens. **Re-measured on both sides of the seam, tag-matched:**

    | | IR side | rendered site |
    |---|---|---|
    | units scanned | 1,268 region strings | 1,262 pages |
    | **executable `<script>` in-region** | **0** | **0** (inside `<main id="main-content">`) |
    | inert `type="application/json"` islands | 348 | 343 |
    | pages flagged `hasExecutableIsland` | **0** | — |

    The island **count** moved since ADR 0032 was written (163 over 1,469 pages → 348 over 1,268) because the site changed underneath it; **the invariant it actually asserts — zero executable in-region — is unchanged.** Policy string at `WebviewRenderAdapter.cs` is byte-identical to ADR 0032's, and `WebviewRenderAdapterTests` already pins its three security-critical clauses.
  - [x] Decide and propose the **static-site CSP** question as an ADR (Q3), coupled to Task 1's outcome. **ADR 0043**, and the decision is **deliberately referred to the owner with a concrete recommendation rather than shipped**. Grounded in measurement, not preference: 1,054 inline `<script>` blocks on 531 of 1,262 pages (14 of them `type=module` importing Mermaid **from a CDN**) and 2,105 inline `style=""` attributes. Three findings decide the shape — (a) a nonce is *worthless* on a static file, because the constant is baked into the published HTML an attacker is already reading; (b) `'unsafe-inline'` would permit exactly the inline handlers this story found, making the policy pointless for its own threat; (c) hashes are tractable (~4–6 distinct scripts) but need an ADR 0033-compliant drift gate. With ADR 0042 closing the channel at source, the CSP is now defense-in-depth over a shut door — and ADR 0032's own precedent is that a half-applied CSP blanked the page (148 SVGs → 0).
  - [x] Propose ratification of ADR 0032 and ADR 0016 (both still `Proposed`). **Both requested in-file, with the re-verification recorded alongside. Status left `Proposed` in both — flipping it is the owner's call, not this story's.**

- [x] **Task 5 — Privacy / NFR3 (AC: #2)**
  - [x] **Measure the prerender server's bind address.** If not loopback-only, set `NITRO_HOST=127.0.0.1` beside the existing `NITRO_PORT` and pin it. **⚠ THE DEFECT IS REAL AND WAS DEMONSTRATED, NOT INFERRED.** With the shipped env (`PORT`/`NITRO_PORT` only), Nitro logged `Listening on http://[::]:39117` — the IPv6 **wildcard** — and the listening socket's `LocalAddress` was `::`. The fully rendered portal was then **fetched over both of this machine's real LAN addresses** (`172.28.160.1`, `192.168.50.25`), each answering **HTTP 200 with 1,305,409 bytes**. So for the duration of every `generate`, a **private** repository's entire portal was readable by anyone on the same network. Fixed by setting `HOST` **and** `NITRO_HOST` (the preset reads `NITRO_HOST` first and falls back to `HOST`; setting one alone would depend on which preset the artefact was built with). After: `Listening on http://127.0.0.1:39118`, `LocalAddress 127.0.0.1`, loopback still HTTP 200 with the **identical** 1,305,409 bytes, both LAN addresses refused. Full `generate` after the fix: 512 pages, `errors=0`.

    Why it was invisible from the C# side: `FreePort()` binds `IPAddress.Loopback` only to *pick* a port and releases it immediately — it never constrained what Node then bound. And why no existing test caught it: **every test fetches over loopback, and loopback succeeds identically whether the server bound `127.0.0.1` or `::`.**
  - [x] Re-confirm NFR3 across paths added since last verification — record the enumeration so the next audit starts from a list. **Enumerated and now pinned as a gate, not a paragraph** (`NetworkPostureTests`): the only `HttpClient` in the product is `NuxtPrerender`'s loopback client; `extension/src/**` has **zero** outbound calls (no `fetch`, no `http.request`, no `axios`, no `XMLHttpRequest`). `TheOnlyOutboundHttpIsLoopback` fails if any `BaseAddress` is ever non-loopback, so a future crossing has to be deliberate and visible in review.
  - [x] Confirm generated output exposes nothing beyond the source artifacts; state the git author-name/email position explicitly rather than leaving it implicit. **Measured over all 1,262 generated pages:** author **email — 0 occurrences**. Author *name* (161 in 64 files) and the GitHub owner (1,982 in 296) appear only where the source artifacts already carry them (ADR `**Deciders:**` lines, doc URLs). Absolute **local paths** (`C:\Users\MattE\…`, 5 occurrences in 4 pages) traced to **4 source `.md` files that already contain them** — SpecScribe renders them, it does not invent them. All in-bounds per AC #2's "beyond what the source artifacts already expose", stated rather than left implicit. Note for the owner: agent-authored story files routinely embed absolute local paths, so a **publicly** published portal discloses a username and directory layout — a property of the artifacts, not a SpecScribe defect.
  - [x] Verify Epic 4 de-personalization end to end on a differently-organized repo. **Verified decisively.** This repository's *real* `epics.md` and all 269 implementation artifacts were relocated to entirely non-standard paths (`some/deep/planning/epics.md`, `other/place/implementation-artifacts/`) and generated: **120 pages, `errors=0`**, with `epics.html`, `requirements.html` and `traceability.html` all rendering. A separate minimal fixture failed identically in **both** the non-standard and the canonical layout, which is what proves the failure was the fixture and not a structure assumption — the control is why this conclusion is trustworthy.

  **⚠ NEW FINDING the story did not name — a viewer-side outbound path.** `Mermaid.cs:151` emits `import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/…'` on the 10 pages carrying diagrams. This is **not** a tool-side NFR3 violation (SpecScribe makes no such call), but it means **a reader of a private portal fetches script from a third-party CDN**, disclosing their IP and referrer, and the CDN can serve arbitrary JS into the page. An ESM `import` URL cannot carry SRI, and the static site has no CSP (ADR 0043), so there is no second line of defence. Not fixed here — vendoring mermaid is a packaging decision (ADR 0022 territory) with real size consequences. Recorded in `deferred-work.md` with that recommendation rather than shipped unilaterally.

- [x] **Task 6 — Workspace Trust effectiveness (AC: #1)**
  - [x] Pin `restrictedConfigurations` coverage with a test that fails if a new execution-bearing setting is contributed without being restricted. That is the durable form of "present and effective". **`WorkspaceTrustTests`, 4 tests.** The story's finding is confirmed unchanged at HEAD: two contributed settings, the execution-bearing one (`specscribe.toolPath`) restricted, coverage complete.

    **Designed as a gate, not a snapshot.** It deliberately does *not* assert "there are two settings" — that is a change-detector the next contributor edits without thinking. It asserts every contributed setting is **explicitly classified**: either in `restrictedConfigurations`, or in a named `SafeInUntrustedWorkspaces` list carrying the reason it cannot lead to execution. A new setting fails until someone makes that decision. The inverse drift is covered too (`EveryRestrictedConfigurationIsActuallyContributed` — a restriction naming a setting that no longer exists reads as protection while protecting nothing), and `ContributedSettingKeys` handles `contributes.configuration` being either an object *or* an array of categories, because reading only the object form would return nothing the day someone groups the settings and turn every assertion green for the wrong reason.

    **Proven able to fail**, not just observed passing: injecting an unrestricted `specscribe.extraArgs` turned `EveryContributedSettingIsClassifiedForUntrustedWorkspaces` red (1 failed / 3 passed); `package.json` was then restored and re-verified clean (`git diff` empty).

    **Route recorded honestly:** this lives in the C# suite because `extension/` still has no TypeScript harness — that is Story 17.4's cluster. This is the second of the three routes the story named, chosen over shipping unpinned.

- [x] **Task 7 — Dependencies (AC: #2)**
  - [x] Re-run all three audits at implementation time (they move). **They moved.** C#: **0 vulnerable** (incl. transitive, vs nuget.org). `extension/`: **0 vulnerabilities**. `web/`: **2 high** — `brace-expansion` as the story recorded, **plus `nanoid` (GHSA-2v37-7h3g-55p8), which is new since the story was written.** Re-running rather than trusting the recorded figure is what found it.
  - [x] Fix `web/`'s `brace-expansion` — **after** resolving the Story 23.5 lockfile collision (Q5). Verify `npm ci` succeeds before and after. **Q5's precondition is RESOLVED, so the trap no longer applies as written:** Story 23.5's lockfile fix has landed in the tree (`@emnapi/runtime` present, 12 entries) and `npm ci` **succeeds** — the EUSAGE failure the story recorded is gone. (23.5 is still `review`, but its code is in.) Verified in the required order: `npm ci` **before** ✅ → `npm audit fix` → **0 vulnerabilities** → `npm ci` **after** ✅ (639 packages, exit 0) → `npm run build:package` ✅ → full `generate` **512 pages, `errors=0`** → `npm test` **196/196** → gates ✅. Lockfile diff is surgical: **12 lines**, `brace-expansion` 2.1.2→2.1.4 / 5.0.8→5.0.9 and `nanoid` 3.3.16→3.3.18, no transitive churn.
  - [x] Record the C# and `extension/` clean results, and record that `extension/` has zero runtime dependencies. **Recorded: `extension/` has `"dependencies": {}` — only `@types/node`, `@types/vscode`, `esbuild`, `typescript` as devDependencies, so nothing from npm ships in the VSIX.**

  **⚠ A defect in this story's OWN Task 2 fix, found because the real build was run.** The first form of the `build-package.mjs` change used `createRequire(...).resolve('nuxt/bin/nuxt.mjs')`, which **fails with `ERR_PACKAGE_PATH_NOT_EXPORTED`** — `require.resolve` honours the package's `exports` map and nuxt does not export its bin path. It was only caught because Task 7 re-ran `npm run build:package` for real; nothing else in the suite exercises that script. Corrected to resolve `nuxt/package.json` (which *is* exported) and read `bin.nuxt` relative to its directory. The failure mode and why the obvious form does not work are recorded in the file so it is not re-introduced.

- [x] **Task 8 — CI supply chain (AC: #2)**
  - [x] Pin `dotnet-sonarscanner` to a version **and** add that version to the `actions/cache` key. **Both halves, from ONE definition** — a new job-level `SONAR_SCANNER_VERSION: "11.2.1"` feeds both `--version $env:SONAR_SCANNER_VERSION` and `key: ${{ runner.os }}-sonar-scanner-${{ env.SONAR_SCANNER_VERSION }}`, so they cannot drift apart. Either half alone would reproduce the original defect: a pinned install with an unversioned key still serves the stale cached binary, and a versioned key with an unpinned install still fetches "latest" on a miss. The bare `restore-keys` prefix was also removed — a partial restore would seed the directory with a *different* version's binary. YAML re-parsed to confirm validity.
  - [x] Decide SHA-pinning for `actions/*`; record the decision either way. **Decided and ENFORCED, not just recorded: first-party `actions/*` may float on a major tag; any third-party action must be pinned to a full 40-char commit SHA.** Rationale: trusting `actions/*` is the same trust already extended to GitHub by running on their runners, and pinning them buys little against constant bump churn — whereas a third-party tag is mutable and the publisher is not GitHub. Measured: this repo uses **only** first-party actions (7 distinct, zero third-party). `CiSupplyChainTests.ThirdPartyActionsMustBeShaPinned` enforces it, because the next contributor adding a third-party action is exactly the person who will not read the note.
  - [x] Review and record SonarCloud's GitHub App repository scopes. **Attempted programmatically and HONESTLY RECORDED AS NOT VERIFIED.** `/repos/{owner}/{repo}/installation` requires a GitHub App JWT (HTTP 401 with a user token) and `/user/installations` requires an App-authorized token (HTTP 403) — so this is genuinely an owner UI review, exactly as the story anticipated. `docs/SonarCloudSetup.md` now carries the **procedure** (Settings → GitHub Apps → SonarQube Cloud → Configure), the **expected** scopes (read on code/metadata/PRs, write on checks/PR comments for decoration) and the **red flags** (any write access to code, actions, or secrets). Recorded as unverified rather than assumed benign.
  - [x] Record the already-correct posture … so a future change that regresses it is visible as a regression. **Recorded as TESTS, not prose** — prose in a story file is archaeology, not a regression signal. `CiSupplyChainTests` (5 tests) pins: no `pull_request_target` anywhere; no secret interpolated into a `run:` body; every workflow declares its own `permissions:`; the scanner pin + cache key; and the third-party SHA rule.

- [x] **Task 9 — Record and hand off (AC: #1, #2)**
  - [x] Close resolved `deferred-work.md` items in the same pass (Epic 3 retro rule). **Two closed in place, both with the correction they earned:**
    - `:1214` (scanner cache key, routed here by name) — struck, both halves fixed from one definition.
    - `:1245` (the ReDoS band) — struck, **and its proposed fix recorded as partly wrong**: `NonBacktracking` is not usable as the house default (33 of 46 regex-bearing files use a lookaround, 2 use a backreference — all rejected at construction time), and its "approximately 40 construction sites" understated the real figure by 4× (163). Its own warning to re-measure rather than trust the count was correct: 156 → 175.
    - **Three NEW items recorded** rather than left in this story's prose: the Mermaid CDN import, the static-site CSP referral, and the unverified SonarCloud app scopes.
  - [x] Record Epic 26's clause as *precondition unmet*, handed to Story 17.4. **Re-verified at HEAD, not copied:** `26-2-ingestion-posture-and-credential-spike` is still `ready-for-dev` (the credential *spike* has not run) and `26-3`…`26-7` are still `backlog`. There is no external-service integration, no credential, and no ADR-authorized outbound path to audit. **AC #2's third clause is therefore formally UNMET-BY-PRECONDITION, not skipped** — no credential audit was invented for code that does not exist. Handed to Story 17.4.
  - [x] State hunk-level attribution against Story 17.1 for every shared file. **Stated below in § Attribution.**

## Attribution (per CLAUDE.md § Scoping a code review)

**17.1 has LANDED since this story was written.** Its create-story record said "17.1 is `ready-for-dev` and not yet implemented, so expect no code from it" — that is now **stale**: `sprint-status.yaml` has `17-1-…: review` at this baseline. So the shared files carry real 17.1 code, and attribution by hunk is mandatory rather than precautionary.

**This story's hunks, in files 17.1 also touched:**

| file | 17.2's hunks (mine) | NOT mine |
|---|---|---|
| `EpicsParser.cs` | 21 `TimedRegex.New(` call-site rewrites, nothing else | `NumberIndex.ByFirst` and the epic-number convergence — 17.1 |
| `SiteGenerator.cs` | 11 `TimedRegex.New(` rewrites | everything else in a ~5,900-line file |
| `CodeReferenceLinkifier.cs` | 3 `TimedRegex.New(` rewrites | — |
| `IdeaDiscovery.cs` | `UnsafeReportPattern` → `HtmlSafety.ContainsExecutableMarkup`; 2 `TimedRegex.New(` rewrites | the `ExternalSubresourcePattern` policy itself — Story 18.4 |
| `MarkdownConverter.cs` | `NeutralizeDangerousLinks` + its call; 1 `TimedRegex.New(` rewrite | the Markdig pipeline/`EmphasisExtras` decision — Epic 2 |
| `AbbreviationExpander.cs` | the one dynamic-pattern rewrite at the local `var pattern` | `ProtectedSplit`'s content — the linkifier lineage |

**The 163 regex rewrites are mechanically identical and touch one token per site.** They are wide but shallow; a reviewer should read `TimedRegex.cs` and the two enforcing tests, then spot-check the call sites rather than read 46 diffs. The substantive new code is five files: `HtmlSafety.cs`, `ToolResolver.cs`, `TimedRegex.cs`, and the changes in `CommentAnnotationRenderer.cs` / `NuxtPrerender.cs`.

**Not mine, deliberately left alone:** `specscribe.css` and `web/assets/ir-content.css` (untouched — verified via `git status`), the `.coverage-card` deferral (17.1 → Epic 27), and the maintainability band (17.1).

## Dev Notes

### Architecture constraints you must not violate

- **ADR 0016** — the IR carries Markdig-rendered prose HTML **verbatim**. Any sanitisation must happen at a defined seam and must not break byte-faithfulness for benign content, or Epic 23's central finding (no Vue reimplementation of ~889 LOC of custom renderers) is forfeited.
- **ADR 0032** — `script-src` stays nonce-locked. **Do not relax the webview CSP.** The measured conclusion was "no relaxation needed"; a story that loosens it is reversing a decision, which needs its own ADR.
- **ADR 0021** — foreign artifacts are carried *verbatim or not at all*; **sanitising-by-transformation is explicitly a rejected alternative** for carried artifacts ("that produces a document the author did not write while still presenting it as the original"). Note this constrains *carried* artifacts specifically — the repo's own markdown is already transformed by Markdig, so the rejection does not automatically extend to it. Argue the distinction in the ADR rather than assuming it either way.
- **ADR 0035 §Decision 5** — a Sonar rule suppression is not an acceptable route to closing a finding.
- **ADR 0013 / NFR5** — the no-JS text-twin contract. Do not close an injection hole by making a surface JS-dependent.

### Traps specific to this repository

- **`check:parity` cannot see a C#-side change.** Its corpus IR is frozen. A change to the C# region composer renders from *pinned* input and the gate stays green. Verified 2026-08-01: removing an element from the shared nav on every page left all 24 routes byte-identical. Most of this story's work is C#-side — **a green `check:parity` is not evidence that your change is safe.** Cover with unit tests over the region plus live-browser inspection.
- **Rebuild non-incrementally before trusting anything involving an asset.** `specscribe.css`/`.js` are embedded resources; an incremental build reuses the cached assembly and never re-embeds a changed asset.
- **If you touch `specscribe.css`, the regeneration order in CLAUDE.md is load-bearing** (two `generate`s, deliberately). Unlikely for this story, but Task 1's fix could touch rendered markup.
- **Never regenerate a gate baseline reflexively.** If a gate moves and you did not touch rendering, audit the harness first. Bisect in a throwaway tree (`git archive HEAD` into the scratchpad) — never by resetting the shared tree.
- **Assume another agent is editing these files right now.** Verify after every edit by grepping for the symbol you just added. `check:ir-content` red in a fresh worktree is environmental, not drift.
- **`specscribe generate` in a worktree**: the renderer-path defect was **fixed by Story 16.3** — do *not* set `SPECSCRIBE_RENDERER_DIR` any more.

### Analysis digest

`.specscribe/analysis/` is gitignored and lives only in the **main checkout**, not in a fresh worktree.
At authoring time: `evaluatedAtRevision` = `c73ebcb` (= HEAD, so the read-time rule passes), but
`analysisRevision` = `01acf5b1`, `commitsBehind: 15`, `isStale: true`, `workingTreeDirty: false`. Totals:
1,755 observations across 231 files (142 error / 1,179 warning / 434 note). **Line numbers are anchored to the
analysis revision — re-resolve by symbol.** Read shards, never the whole tree (index ~31 KB; everything ~1.34 MB).

### Testing

- xUnit, `tests/SpecScribe.Tests/`. `Xunit.SkippableFact` is available and is the established pattern for tests needing elevation (the symlink tests use it) — useful if a Task 2 test needs to plant an executable.
- `web/` uses Vitest. **`extension/` has no TypeScript test harness at all** — no test script, no runner, no test files. That harness is **Story 17.4's** cluster. If a Task 6 fix needs a TS test, you cannot pin it today: either assert `package.json`'s shape from the C# suite, or record the gap honestly. Do not silently ship unpinned.
- Hostile-input test precedents already exist to model on: `GitInsightsTemplaterTests.cs:417` (`src/</script><img src=x onerror=alert(1)>/<!--x.cs`) and `IdeasTests.cs:384` (`<div onerror=alert(1)>x</div>`).

### Previous story intelligence (17.1)

17.1 is `ready-for-dev` and **not yet implemented**, so expect no code from it — but its create-story record is
directly reusable:

- It hit **the identical pattern this story hits**: all three of *its* AC #1 examples were also already closed. Two consecutive Epic 17 stories with stale AC illustrations is a signal about `epics.md`, not a coincidence — hence Q1.
- It records the `GoldenContentFingerprint` retirement (ADR 0034) and the `check:parity` blindness that follows. Do not look for a golden byte-parity gate; it does not exist.
- Its Task 0 (digest + measure-before-touching) is the pattern Task 0 here follows.
- It flags that `S2583` findings are **not all real** — `CapabilityStyler.cs` and `WorkGraph.cs` were read and both are dataflow blind spots. Expect the same for some `S6444` sites; adjudicate rather than bulk-apply.

### Git intelligence

Recent commits are merges of per-story worktree branches (`c73ebcb` ← `worktree-story-16-2-dev`;
`a2eee2a` made `build-test-analyze` a **required** check on `main`). Two consequences:

- **CI is now blocking.** A red build-test-analyze blocks merges. The `FileWatcherServiceTests.BurstOfSaves`
  flake is a known load-sensitive test in that suite — if you hit it, it is the known flake
  (Story 17.4 AC #3 treats it as time-critical), not your change.
- Commits routinely **bundle sibling stories** because review runs at epic end. Scope by File List *and* by
  hunk.

## Project Structure Notes

Primary files, all existing (`UPDATE`, none new): `src/SpecScribe/MarkdownConverter.cs`,
`src/SpecScribe/GitMetrics.cs`, `src/SpecScribe/NuxtPrerender.cs`, `src/SpecScribe/IdeaDiscovery.cs`, the ~48
regex-bearing files in § C, `web/components/surfaces/IrSurface.vue`, `web/scripts/build-package.mjs`,
`extension/package.json`, `.github/workflows/build-test-analyze.yml`, `web/package-lock.json`.

Expect new: regression tests in `tests/SpecScribe.Tests/`, a hostile-markdown fixture, and (likely) two ADRs.

## References

- [Source: `_bmad-output/planning-artifacts/epics.md` § Epic 17 → Story 17.2] — ACs, verbatim above
- [Source: `docs/adrs/0016-ir-carries-rendered-prose-html.md`] — the IR carries prose HTML verbatim (`Proposed`)
- [Source: `docs/adrs/0021-carrying-foreign-artifacts-verbatim-into-the-portal.md` §Decision] — the no-script policy language, already written
- [Source: `docs/adrs/0032-csp-posture-after-the-projection-layer.md`] — measured CSP posture; no relaxation (`Proposed`)
- [Source: `docs/adrs/0035-sonarcloud-quality-gate-and-rule-decision-policy.md` §Decision 5] — suppression is not a route
- [Source: `docs/SonarCloudSetup.md:490-491, :579, :625`] — `S6444`/`S4036` routed to 17.2; security notes
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:677, :712, :776`] — the three closed AC #1 examples
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:1215, :1246`] — scanner pinning and the ReDoS band, both routed here
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:491`] — filename-derived hrefs escaped but never percent-encoded (open; adjacent, low priority)
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:1159`] — inline JSON island escaping unspecified (open; incidentally safe via `System.Text.Json` defaults, and `UnsafeRelaxedJsonEscaping` *is* used at `Commands.cs:91` and `HierarchyExplorer.cs:769` — worth one check that the two do not meet)
- [Source: `CLAUDE.md`] — concurrent-work rules, gate semantics, digest reading, verification-in-a-live-browser

## Questions for the owner

All non-blocking; each has a stated default so implementation is not gated on an answer.

1. **`epics.md` AC #1's three examples are all stale (closed July 2026), and this is the second consecutive Epic 17 story where that is true.** Amend the epic text to name the live surfaces instead? *Default: leave `epics.md` alone, proceed on the ⚠ table in this file.*
2. **AC #2 says "the extension's npm tree" — written before `web/` existed.** I have treated `web/` as in scope because its build output ships. *Default: `web/` in scope.*
3. **Should the generated static site carry a CSP?** It has none today; the webview has a strict one. This is a cross-cutting contract change → **ADR candidate**, coupled to Q4. *Default: propose an ADR, do not ship a CSP unilaterally.*
4. **What is the policy for raw HTML in the repository's own markdown?** ADR 0021 already gates *foreign* HTML on exactly this; the repo's own `.md` is ungated. → **ADR candidate**. Note this repo's own `epics.md` uses `<details>` legitimately, so a blanket escape has a real cost. *Default: propose the ADR before implementing.*
5. **`web/`'s `brace-expansion` fix rewrites the same lockfile Story 23.5 is fixing** (23.5 is `review`, `npm ci` currently broken). Sequence behind 23.5, or fix both together here? *Default: coordinate with 23.5; do not land a colliding lockfile change.*
6. **ADR 0032 and ADR 0016 are both still `Proposed`.** AC #1 asks for a *verified* CSP posture; verifying against an unratified record is half a job. Ratify as part of this story? *Default: re-verify, then propose ratification.*
7. **Epic 17's stated sequencing ("after Epics 1–15/18 and Epic 5") is still unmet** — 15 epics remain `in-progress`. This hardening pass runs on a moving codebase, so a surface hardened today can regrow tomorrow (the `S6444` band grew +18 in 11 days is the proof). *Default: proceed, and prefer invariant-shaped fixes over one-time sweeps.* (Same question 17.1 raised.)

## Dev Agent Record

### Agent Model Used

Claude Opus 5 (1M context) — `claude-opus-5[1m]`, dev-story workflow, 2026-08-08, worktree `worktree-story-17-2-dev` cut from `e8a689d`.

### Debug Log References

Measurement harnesses, all under the job scratchpad (throwaway, not committed):

- `cdp-check.mjs` — CDP/Edge live-browser probe. Used for the before/after XSS execution proof and the final portal health check (charts, console errors, DOM geometry).
- `spawnprobe/` — standalone .NET console app reproducing `Process.Start` current-directory resolution, run in both `NoDefaultCurrentDirectoryInExePath` arms.
- `marker/` — harmless marker binary planted as `git.exe`/`node.exe`.
- `xss-repo/`, `diffrepo/`, `ctrlrepo/`, `realmoved/` — hostile-markdown fixture, differently-organized repo, its canonical-layout control, and this repo's real artifacts relocated to non-standard paths.
- `rewrite-regex.py` — the mechanical `TimedRegex.New` rewrite (163 sites/46 files), with its own leftover report.

### Completion Notes List

**Every AC claim in this story is backed by a measurement, and three of the story's own premises were corrected against the code.**

1. **The `v-html` injection channel was real, and it EXECUTED.** Not "the handler survives into the HTML" — a live browser load of the generated page set three markers (`__SPECSCRIBE_XSS_IMG`, `_SVG`, `_FRAME`; the `iframe srcdoc` executed against the *parent* window). Closed at the Markdig seam by `HtmlSafety`, so the IR never carries the construct and **both** surfaces benefit while ADR 0016's verbatim carriage survives for benign content. After: 0 markers, 0 handlers, 0 `javascript:` hrefs, and `<details>`/`<kbd>`/`<img>`/`<svg>` counts byte-identical.

2. **Three vectors the story did not predict.** (a) `[text](javascript:…)` needs **no raw HTML at all** — a raw-HTML-only fix would have missed the cheapest vector; (b) a literal `<script>` was already a **denial of service** (page 500s via `IrSurface`'s island throw), so hostile markdown could delete a page; (c) `<base>`/`<meta http-equiv>` carry neither a handler nor a `javascript:` URL and would survive handler-stripping alone.

3. **The design trap that shaped the fix.** The sanitizer touches raw-HTML passthrough nodes **only**. This portal renders its own source, so `onerror=` appears legitimately — escaped — in code spans on the Code Map and on this very story's page; a regex pass over finished HTML would have corrupted shipped documentation with every gate green. Pinned by two tests.

4. **The Windows tool-resolution hijack reproduced**, in a controlled two-arm measurement, and the confounder the story warned about was live: `NoDefaultCurrentDirectoryInExePath` **was** set in this project's Git Bash session (real git ran) and unset in a default shell (**planted binary ran**). Measuring in one shell only would have produced the wrong answer.

5. **The ReDoS band was closed as an invariant, not a sweep** — 175 findings, growing (156 → 174 → 175). One factory + two enforcing tests. **The deferred-work record's proposed fix was partly wrong**: `NonBacktracking` is unusable as a default (33/46 files use lookarounds), and "≈40 construction sites" understated the truth by 4× (163).

6. **A LAN-exposure privacy defect was found, demonstrated, and fixed.** The prerender server bound the IPv6 wildcard; a private repository's entire rendered portal answered **HTTP 200 with 1,305,409 bytes over two real LAN addresses** for the duration of every `generate`. Now loopback-only, rendering unaffected. **No existing test could have caught it** — every test fetches over loopback, which succeeds identically either way.

7. **A defect in this story's own work, caught by running the real build.** The first `build-package.mjs` fix used `require.resolve('nuxt/bin/nuxt.mjs')`, which fails with `ERR_PACKAGE_PATH_NOT_EXPORTED` because `require.resolve` honours `exports`. Nothing in the suite exercises that script; only Task 7's real `npm run build:package` found it.

8. **`check:ir-content` measured GREEN, and its redness diagnosed without touching a baseline.** A plain `generate` omits the deep-git-gated code-insight/history/relationships surfaces, so the extractor prunes **185** rules and the round-trip looks like drift (the 17.4 triage saw `-187` — same signature). With `generate --deep-git` the gate reports **1475 rules in sync**. Per CLAUDE.md, causality was established first; no baseline was regenerated. **This answers Story 17.4 AC #4's open question, which recorded the gate as believed-red but unmeasured.**

9. **Two AC clauses are formally unmet, by precondition, and say so.** Epic 26's credential/outbound-integration clause (26-2 still `ready-for-dev`, 26-3…26-7 `backlog` — no credential exists to audit), and SonarCloud's GitHub App scopes (both API routes refused: HTTP 401 needs an App JWT, HTTP 403 needs an App-authorized token). Neither was invented or assumed benign.

10. **Three findings deliberately not fixed**, each because the fix is a decision: the Mermaid CDN import (packaging, ADR 0022), the static-site CSP (ADR 0043, referred to the owner with a costed recommendation), and the app scopes. All recorded in `deferred-work.md`.

**Verification:** 3,058 tests passed / 0 failed / 3 skipped (3,000 → 3,058, +58 new). `web/` vitest 196/196. All four gates green — `check:parity`, `check:ir-content`, `check:tokens`, `check:assets`. Full `generate` 512 pages `errors=0`; `generate --deep-git` 819 pages `errors=0`. Live-browser verified per CLAUDE.md: Plotly loaded, 421/47 SVGs, 0 inline handlers site-wide, **0 console errors**.

**Owner questions Q1–Q7:** all seven proceeded on their stated defaults. Q1 (stale `epics.md` examples) — `epics.md` left alone, this file's ⚠ table used. Q2 — `web/` treated as in scope. Q3/Q4 — both ADRs proposed before implementing. Q5 — **precondition changed**: 23.5's lockfile fix has landed and `npm ci` works, so the collision the question guarded against no longer exists; verified before *and* after. Q6 — re-verified, ratification requested, status left `Proposed`. Q7 — proceeded, and every fix is invariant-shaped rather than a one-time sweep, exactly as the question recommended.

### File List

**New — product (3):**
- `src/SpecScribe/HtmlSafety.cs`
- `src/SpecScribe/ToolResolver.cs`
- `src/SpecScribe/TimedRegex.cs`

**New — tests (6):**
- `tests/SpecScribe.Tests/HtmlSafetyTests.cs`
- `tests/SpecScribe.Tests/ToolResolverTests.cs`
- `tests/SpecScribe.Tests/TimedRegexTests.cs`
- `tests/SpecScribe.Tests/NetworkPostureTests.cs`
- `tests/SpecScribe.Tests/WorkspaceTrustTests.cs`
- `tests/SpecScribe.Tests/CiSupplyChainTests.cs`

**New — decision records (2):**
- `docs/adrs/0042-raw-html-in-the-repositorys-own-markdown-is-neutralized.md`
- `docs/adrs/0043-the-generated-static-site-carries-no-csp.md`

**Modified — substantive (5):**
- `src/SpecScribe/CommentAnnotationRenderer.cs` — raw HTML block/inline sanitization
- `src/SpecScribe/MarkdownConverter.cs` — `NeutralizeDangerousLinks` + a `TimedRegex` rewrite
- `src/SpecScribe/NuxtPrerender.cs` — `HOST`/`NITRO_HOST` loopback pin, absolute node path, 2 `TimedRegex` rewrites
- `src/SpecScribe/GitMetrics.cs` — absolute git path
- `src/SpecScribe/IdeaDiscovery.cs` — pattern lifted to `HtmlSafety`, 2 `TimedRegex` rewrites

**Modified — mechanical `TimedRegex.New` rewrites only (42):**
`AbbreviationExpander.cs` · `ActionItemsTemplater.cs` · `AdrLinkRewriter.cs` · `ArtifactCoverage.cs` · `BmadArtifactAdapter.cs` · `CapabilityStyler.cs` · `ChangeSurface.cs` · `ChangeSurfaceFileResolver.cs` · `CodeReferenceLinkifier.cs` · `CodeReferenceScanner.cs` · `CodeSourceUrlResolver.cs` · `ColorSwatchRewriter.cs` · `CommitDetailTemplater.cs` · `DeferralHeuristics.cs` · `DeferredWorkParser.cs` · `EpicsParser.cs` · `FileListLinkifier.cs` · `FollowUpRefs.cs` · `FollowUpRow.cs` · `FollowUpSlug.cs` · `ForgeOptions.cs` · `GherkinStyler.cs` · `GsdCoreArtifactAdapter.cs` · `Memlog.cs` · `ModuleContext.cs` · `PathUtil.cs` · `PlanningCodeImpact.cs` · `ReferenceChipRenderer.cs` · `RenderParity.cs` · `RequirementLinkifier.cs` · `RequirementsParser.cs` · `RetroActionStyler.cs` · `RetroParser.cs` · `SiteGenerator.cs` · `SourceLinkifier.cs` · `SpaDelivery.cs` · `SprintStatusParser.cs` · `StoryEpicLinkifier.cs` · `TaskListParser.cs` · `Toc.cs` · `UnplannedWorkGeometry.cs` · `WorkGraph.cs` (all under `src/SpecScribe/`)

**Modified — build, CI, docs, records (8):**
- `web/scripts/build-package.mjs` — nuxt resolved via its own `bin` entry, no shell, no PATH search
- `web/package-lock.json` — `npm audit fix` (12 lines: brace-expansion, nanoid)
- `.github/workflows/build-test-analyze.yml` — `SONAR_SCANNER_VERSION` pin + versioned cache key
- `docs/SonarCloudSetup.md` — scanner pin, action-pinning policy, GitHub App scope-review procedure
- `docs/adrs/0016-ir-carries-rendered-prose-html.md` — ratification request
- `docs/adrs/0032-csp-posture-after-the-projection-layer.md` — re-verification + ratification request
- `_bmad-output/implementation-artifacts/deferred-work.md` — 2 items closed, 3 recorded
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status transitions

**Not modified (stated because they are the usual suspects):** `src/SpecScribe/assets/specscribe.css` and `web/assets/ir-content.css` — untouched, verified via `git status`. No gate baseline was regenerated.

## Change Log

| Date | Change |
|---|---|
| 2026-08-08 | Story 17.2 implemented (dev-story, baseline `e8a689d`). Closed a reproduced-and-executed stored-XSS channel in rendered markdown (ADR 0042); closed a reproduced Windows tool-resolution hijack (`ToolResolver`); converted the 175-finding `S6444` ReDoS band into an enforced invariant (`TimedRegex`); fixed a demonstrated LAN-exposure privacy defect in the prerender server; pinned the CI scanner and recorded an enforced action-pinning policy; resolved 2 `web/` high-severity advisories; added 58 regression tests and 2 ADRs. Status → review. |
