# Story 17.2: Security and Privacy Hardening for Public and Private Repos

Status: ready-for-dev

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

- [ ] **Task 0 — Baseline before touching anything (AC: #1, #2)**
  - [ ] `git rev-parse HEAD`, and record it as this story's real baseline (this file says `c73ebcb`).
  - [ ] Refresh the analysis digest: `node tools/analysis-digest/index.mjs`. **At authoring time the digest's `analysisRevision` was 15 commits behind HEAD** (`isStale: true`, `analysis-behind-working-tree`) — every line number in § C is anchored to `01acf5b1` and *will* have moved. Re-resolve by symbol.
  - [ ] Re-count `S6444`/`S4036` from the refreshed digest. The band grew 156 → 174 in 11 days; assume it moved again.
  - [ ] Do **not** run `npm run check:ir-content` as a health signal yet — it is red in a fresh worktree for environmental reasons (no IR ⇒ nearly everything pruned). If you need it, run the full load-bearing order from CLAUDE.md first. Its true state is **Story 17.4's** to establish, not yours.

- [ ] **Task 1 — Prove, then close, the `v-html` injection channel (AC: #1)**
  - [ ] **Measure first.** Add a `.md` fixture containing `<img src=x onerror="…">`, `<svg onload="…">`, and a `javascript:` link. Generate. Confirm whether the handler survives into the shipped `.html`. **Keep the artifact.** If it does not reproduce, record that and stop — items A/Q3/Q4 collapse.
  - [ ] Confirm the same fixture is inert in the **webview** (expected: nonce-locked CSP blocks it, per ADR 0032). A differing answer between the two surfaces is itself the finding.
  - [ ] Propose the policy decision as an **ADR** (Q4) before implementing — this changes a cross-cutting contract and amends ADR 0021's asymmetry. Options to weigh, with the trade-off stated: strip handlers/`javascript:` at render; escape raw HTML blocks entirely (breaks legitimate `<details>`/`<kbd>`/`<br>` already used in this repo's own `epics.md` — verified present, so this option has a real cost); or gate-and-diagnose in `IdeaDiscovery`'s style.
  - [ ] Reuse `IdeaDiscovery.UnsafeReportPattern` rather than authoring a second pattern. If it must be shared, lift it to one place — a second copy is precisely the SSOT defect 17.1 is sweeping up.
  - [ ] Pin with a regression test asserting the hostile fixture renders inert.

- [ ] **Task 2 — Close the tool-resolution surface (AC: #1)**
  - [ ] **Measure first, on Windows.** Put a harmless marker `git.exe` (or `node.exe`) at a scratch repo root, `cd` into it, run `generate`, and observe whether it is invoked. Record the result — this settles whether the `CreateProcess` search order reaches the repo directory in practice.
  - [ ] If it reproduces: resolve `git`/`node` to absolute paths, modelled on `extension.ts`'s `resolveTool()` 3-tier pattern. Do not invent a second resolution scheme.
  - [ ] Address `web/scripts/build-package.mjs:55` (`javascript:S4036`) in the same pass.
  - [ ] Pin with a regression test that a repo-local executable is not preferred.
  - [ ] If it does **not** reproduce, still close the Sonar finding (absolute paths are cheap and correct) but say plainly in the record that the exploit did not reproduce — do not overclaim a fix.

- [ ] **Task 3 — ReDoS band (AC: #1)**
  - [ ] Classify all `S6444` sites by **input provenance**: third-party repo content vs first-party/harness/build. `RenderParity.cs` (16) and `build-package.mjs` are the clear second category.
  - [ ] Harden the first category. Prefer a **construction seam + an enforcing test** over 174 individual edits — the band grew +18 in 11 days and will grow again.
  - [ ] Expect `NonBacktracking` to be unusable on patterns with lookarounds/backreferences; fall back to an explicit `matchTimeout` there. A mixed answer is the correct answer.
  - [ ] Batch or defer the second category **with a recorded rationale** (ADR 0035 §5: no blanket suppression).
  - [ ] Pin at least one catastrophic-backtracking case with a timing-bounded test.

- [ ] **Task 4 — CSP verification and ratification (AC: #1)**
  - [ ] Re-verify the webview policy string at HEAD and re-run ADR 0032's whole-site assertions (0 executable scripts in-region; islands inert). Match on real `<script>` **tags**, never a substring — this portal renders its own source and `code/**` pages *mention* these tokens.
  - [ ] Decide and propose the **static-site CSP** question as an ADR (Q3), coupled to Task 1's outcome.
  - [ ] Propose ratification of ADR 0032 and ADR 0016 (both still `Proposed`).

- [ ] **Task 5 — Privacy / NFR3 (AC: #2)**
  - [ ] **Measure the prerender server's bind address** (`netstat`/`ss` while a `generate` runs, or read Nitro's resolved config). If not loopback-only, set `NITRO_HOST=127.0.0.1` beside the existing `NITRO_PORT` and pin it.
  - [ ] Re-confirm NFR3 across paths added since last verification — `NuxtPrerender.cs` is the only new network code; record the enumeration so the next audit starts from a list.
  - [ ] Confirm generated output exposes nothing beyond the source artifacts; state the git author-name/email position explicitly rather than leaving it implicit.
  - [ ] Verify Epic 4 de-personalization end to end on a differently-organized repo.

- [ ] **Task 6 — Workspace Trust effectiveness (AC: #1)**
  - [ ] Pin `restrictedConfigurations` coverage with a test that fails if a new execution-bearing setting is contributed without being restricted. That is the durable form of "present and effective".

- [ ] **Task 7 — Dependencies (AC: #2)**
  - [ ] Re-run all three audits at implementation time (they move).
  - [ ] Fix `web/`'s `brace-expansion` — **after** resolving the Story 23.5 lockfile collision (Q5). Verify `npm ci` succeeds before and after.
  - [ ] Record the C# and `extension/` clean results, and record that `extension/` has zero runtime dependencies.

- [ ] **Task 8 — CI supply chain (AC: #2)**
  - [ ] Pin `dotnet-sonarscanner` to a version **and** add that version to the `actions/cache` key.
  - [ ] Decide SHA-pinning for `actions/*`; record the decision either way.
  - [ ] Review and record SonarCloud's GitHub App repository scopes.
  - [ ] Record the already-correct posture (`contents: read`, `pull_request` not `pull_request_target`, token via `env`) so a future change that regresses it is visible as a regression.

- [ ] **Task 9 — Record and hand off (AC: #1, #2)**
  - [ ] Close resolved `deferred-work.md` items in the same pass (Epic 3 retro rule).
  - [ ] Record Epic 26's clause as *precondition unmet*, handed to Story 17.4.
  - [ ] State hunk-level attribution against Story 17.1 for every shared file.

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

### Debug Log References

### Completion Notes List

### File List
