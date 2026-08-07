# SonarCloud Setup

How to connect this repository to [SonarQube Cloud](https://sonarcloud.io) (formerly SonarCloud) so that
every push to `main` and every pull request is analyzed for code quality.

The CI workflow that performs the analysis — [`.github/workflows/build-test-analyze.yml`](../.github/workflows/build-test-analyze.yml)
— is already committed and **already builds and tests without any of this setup**. What the steps below add is
the analysis upload. Until they are done, CI still runs green; the three scanner steps simply skip.

> **This document owns the *analysis* half of that workflow only.** The *gating* half — which check is a
> required status check on `main`, its exact context string (`build-test-analyze`), why
> `portability-probe (ubuntu, non-gating)` is deliberately **not** required, and the admin bypass — lives in
> [`CiGate.md`](CiGate.md). Keep the two in step: they describe one workflow from two angles, and the scanner
> steps' `continue-on-error` posture described below is precisely why a SonarCloud outage cannot fail the gate.

---

## The short answer

**The token is generated in SonarCloud and stored in GitHub — never in this repository.**

| | Where |
|---|---|
| Generate the token | SonarCloud → your avatar → **My Account → Security** → <https://sonarcloud.io/account/security> |
| Store the token | GitHub → repo → **Settings → Secrets and variables → Actions** → <https://github.com/IntegerMan/SpecScribe/settings/secrets/actions> |
| Secret name (exact) | **`SONAR_TOKEN`** |

The workflow reads it as a job-level environment variable and never interpolates it into a shell command:

```yaml
env:
  SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
```

---

## This project's values

These are the real values for this repository, confirmed against SonarCloud's API. The organization key is
**not** the display name, and guessing it produces an authentication failure that reads like a bad token.

| Setting | Value |
|---|---|
| Organization key (`/o:`) | **`integerman-github`** — *not* `integerman` |
| Project key (`/k:`) | **`IntegerMan_SpecScribe`** |
| Region | **EU / global** (`sonarcloud.io`) — so **no** `sonar.host.url` and **no** `/d:sonar.region` |
| Visibility | Public (free OSS tier) |
| Dashboard | <https://sonarcloud.io/project/overview?id=IntegerMan_SpecScribe> |

To re-confirm the organization key at any time, without logging in:

```bash
curl -s "https://sonarcloud.io/api/components/show?component=IntegerMan_SpecScribe"
```

---

## Step 1 — Turn OFF Automatic Analysis

**Do this first. It is a hard blocker, not a tidiness step.** SonarQube Cloud **rejects CI-based analysis
while Automatic Analysis is enabled**, so if you add the token without doing this, the first run fails at the
`SonarScanner end` step with a confusing error.

1. Open the project → **Administration → Analysis Method**
   (<https://sonarcloud.io/project/analysis_method?id=IntegerMan_SpecScribe>).
2. Turn **Automatic Analysis** off.
3. Leave **CI-based analysis** as the method.

> **Note:** older guidance says Automatic Analysis "doesn't cover C#". That is out of date — it does, and it
> has already analyzed this repository. That makes disabling it *more* important, not less: left on, it
> reports inflated findings over vendored and generated files that the CI analysis deliberately excludes.

## Step 2 — Generate a token

1. Go to <https://sonarcloud.io/account/security>.
2. Under **Generate Tokens**, enter a name (e.g. `specscribe-github-actions`).
3. Choose type **Project Analysis Token** scoped to `IntegerMan_SpecScribe` if offered, otherwise a
   **User Token**. Project-scoped is preferred — least privilege.
4. Click **Generate** and copy the value.

**You will not be shown the value again.** Do not paste it into a file, a commit message, a story record, an
issue, or a chat window. If you lose it, revoke it and generate a new one.

## Step 3 — Store it as a GitHub repository secret

### Option A — GitHub web UI

1. Go to <https://github.com/IntegerMan/SpecScribe/settings/secrets/actions>.
2. Click **New repository secret**.
3. **Name:** `SONAR_TOKEN` — exactly this, uppercase, no prefix. The workflow's `if: env.SONAR_TOKEN != ''`
   guards match on this name; any other name silently keeps analysis skipped.
4. **Secret:** paste the token.
5. Click **Add secret**.

### Option B — GitHub CLI

Use the interactive prompt so the value never lands in your shell history:

```bash
gh secret set SONAR_TOKEN --repo IntegerMan/SpecScribe
```

Paste the token at the prompt. Avoid `--body "<token>"` — that records it in history.

Confirm it exists (this prints only names, never values):

```bash
gh secret list --repo IntegerMan/SpecScribe
```

## Step 4 — Verify

Trigger a run and watch it:

```bash
gh workflow run build-test-analyze.yml --repo IntegerMan/SpecScribe --ref main
```

```bash
gh run watch --repo IntegerMan/SpecScribe
```

A correctly configured run shows **Install SonarQube Cloud scanner**, **SonarScanner begin**, and
**SonarScanner end** as *executed* rather than skipped. If those three show as skipped, the secret is missing
or misnamed — go back to Step 3.

Results then appear at
<https://sonarcloud.io/project/overview?id=IntegerMan_SpecScribe>.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Scanner steps show as **skipped**, build and test still pass | No `SONAR_TOKEN` visible to the job. Either the secret is missing/misnamed, or this is a **pull request from a fork** — GitHub does not give forks access to secrets. Skipping is the intended behaviour there. |
| Failure at `SonarScanner end` mentioning automatic analysis | Step 1 was not done. Turn Automatic Analysis off. |
| Authentication / "project not found" error | Wrong organization key. It is `integerman-github`, not `integerman`. Re-check with the `curl` command above. |
| A Java error at `SonarScanner begin` | SonarQube Cloud dropped scanner runtimes below **Java 21** on 2026-07-20 — analyses on Java 17 now fail rather than warn. The workflow pins Java 21 via `actions/setup-java`; check that step ran. |
| Analysis succeeds but reports thousands of issues in vendored files | The exclusion list on the `begin` step is not being applied. See *Where the configuration lives* below. |

### Rotating or revoking the token

Revoke at <https://sonarcloud.io/account/security>, generate a replacement, and repeat Step 3. Updating the
GitHub secret is enough — no workflow change is needed.

---

## Where the configuration lives

**Everything is in the workflow file**, on the `dotnet sonarscanner begin` step — not in a
`sonar-project.properties` file (the .NET scanner does not read one) and not in the SonarCloud UI. Keeping it
in the workflow means it is reviewable in a diff and versioned with the code.

That includes the analysis exclusions. Vendored and generated content is excluded so the findings list is
about code this project actually authors:

- `src/SpecScribe/assets/prism.js`, `prism.css`, `plotly-hierarchy.min.js` — vendored third-party
- `spike/**`, `tools/**` — throwaway and build tooling
- `extension/node_modules/**`, `extension/dist/**`, `extension/bin/**` — dependencies and build output
- `SpecScribeOutput/**`, `docs/live/**`, `artifacts/**`, `_bmad-output/**` — generated
- `_bmad/**`, `.claude/**`, `.agents/**` — installed BMad tooling and skill packs, not authored here
  (`.claude` and `.agents` hold the *same* packs, which is where a spurious 12.1% duplication figure came from)
- `chat.json` — a 4,861-line transcript at the repo root

`extension/src/**` is deliberately **in** scope — it is genuine first-party source.

> **If you change this list, verify it against measurements, not by reading it.** The original list looked
> complete and left ~26% of the analyzed lines on content this project does not author. After an analysis runs,
> check what is actually in scope, largest first:
>
> ```bash
> curl -s "https://sonarcloud.io/api/measures/component_tree?component=IntegerMan_SpecScribe&metricKeys=ncloc&qualifiers=DIR&s=metric&metricSort=ncloc&asc=false&ps=25"
> ```

### Coverage exclusions

Separately from `sonar.exclusions` (which removes files from analysis entirely), the `begin` step carries a
`sonar.coverage.exclusions` list that removes paths from the **coverage denominator only** — their bugs and
code smells still report normally.

**This setting has a short and instructive history. Read it before changing it.**

Story 25.2 set it to the whole of `web/**`, because the only report CI uploaded was C#-only OpenCover, which
structurally cannot reach a Nuxt/Node subtree: `web/**` contributed 918 new lines to cover, all 918
uncovered, and alone dragged `new_coverage` to 59.4% against an 80% threshold while the C# side sat at 94.9%.
That was a workaround for a **missing report**, and 25.2 recorded it as one.

Story 23.5 then supplied the missing report — Vitest under `web/` plus
`sonar.javascript.lcov.reportPaths` — and correctly **narrowed** the exclusion to only what genuinely cannot
be unit-tested (`web/scripts/**` harnesses, `web/server/plugins/**` Nitro plugins, and `web/**/*.vue` until
component tests exist). The list deliberately mirrors `web/vitest.config.ts`'s own coverage `exclude` so the
two cannot drift.

**The lesson worth keeping:** a coverage exclusion is a statement that a path *cannot* be measured, never that
it need not be. When the report arrives, the exclusion must shrink. Do not widen it back to `web/**` — measured
`web/` coverage is about 51% statements, which *will* show `new_coverage` red, and that is the correct signal.

`extension/src/**` is **not** excluded, deliberately, even though no report reaches it either (508 lines to
cover, 0% covered). It is shipped first-party product code and its 0% is a finding this project wants visible.
The accepted consequence: **the next change to `extension/src` will turn the gate red on `new_coverage`, and
that too is correct.**

### Known gap: `specscribe.js` is not analyzed (JavaScript generally *is*)

`src/SpecScribe/assets/specscribe.js` is registered by the scanner but produces **no `ncloc`** and zero
issues — SonarJS reports `Some of the project files were automatically excluded because they looked like
generated code` without naming them. It is not the usual minified-bundle heuristic (longest line is 191
characters, and Node.js was available).

**This gap is file-specific, not language-wide.** As of 2026-07-27 SonarJS analyzes `web/**` normally and
reports real findings there (`javascript:*` and `typescript:*` rules across `web/scripts/`, `web/ir/`, and the
`.vue` components), and TypeScript under `extension/src` has always analyzed. So:

- "No findings in `web/`" would mean clean.
- "No findings in `specscribe.js`" means **not analyzed**.

Re-check the gap with:

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=IntegerMan_SpecScribe%3Asrc%2FSpecScribe%2Fassets%2Fspecscribe.js&metricKeys=ncloc,lines,violations"
```

A response carrying `lines` but no `ncloc` means the file is still being skipped.

`tests/SpecScribe.Tests` is classified as test code automatically by SonarScanner for .NET, via its
`Microsoft.NET.Test.Sdk` / xunit references.

---

## Quality gate

**Decided by Story 25.2 on 2026-07-27. Earlier wording in this file said "no quality gate is enforced" — that
was true about `sonar.qualitygate.wait` and misleading about the gate, which has been evaluating since the
first analysis.**

### Which gate

SonarCloud applies its built-in **`Sonar way` (id 9)** to this project. Story 25.2 kept it rather than
minting a project-specific gate. The reason is 1e below: a custom gate is a *server-side* object that no diff
shows and no reviewer sees, and the org already contains a live demonstration of that failure mode — a
second, non-default gate named **`Customized` (id 4194)** with `new_coverage ≥ 30` and
`new_duplicated_lines_density ≤ 8`, which is **not applied to this project** and is documented nowhere.
Anyone who finds it should assume it is inert unless `get_by_project` says otherwise.

### The conditions, transcribed

Gate conditions cannot live in the workflow file — they are server-side. They are transcribed here so a
reviewer can see them without a SonarCloud login, and verified with the command below.

| Condition | Operator | Threshold | Enforcing or advisory |
|---|---|---|---|
| `new_reliability_rating` | worse than | A | Advisory (reports; does not block) |
| `new_security_rating` | worse than | A | Advisory |
| `new_maintainability_rating` | worse than | A | Advisory |
| `new_coverage` | less than | 80% | Advisory |
| `new_duplicated_lines_density` | greater than | 3% | Advisory |
| `new_security_hotspots_reviewed` | less than | 100% | Advisory |

**Every condition is advisory today**, because `sonar.qualitygate.wait` is not set. A failing gate therefore
blocks **nothing**: it reports on the SonarCloud dashboard and on pull requests, and CI stays green.

```bash
curl -s "https://sonarcloud.io/api/qualitygates/get_by_project?project=IntegerMan_SpecScribe&organization=integerman-github"
```

```bash
curl -s "https://sonarcloud.io/api/qualitygates/project_status?projectKey=IntegerMan_SpecScribe"
```

### The new-code period

Currently **`days: 30`** — a sliding window whose effective start is the first analysis
(`2026-07-25T20:54:41Z`). Story 25.2 kept it, with the defect named rather than hidden:

> On a repository whose first analysis is days old, a sliding 30-day window makes "new code" ≈ "all code".
> `new_lines` went **3,198 → 22,640 in a single day** as the window swallowed whole epics. The new-code
> conditions are currently behaving as whole-project conditions.

The alternatives were rejected as costing more than they are worth *today*, not as wrong:

- **`previous_version`** is the right long-term answer, but it needs `sonar.projectVersion` wired to the
  build's informational version, and "new since the last released version" is meaningless for a project that
  has not released. **Revisit at the first release tag (Epic 16).**
- **A reference branch** is degenerate when the analyzed branch *is* `main`.

### What would make the gate blocking

`sonar.qualitygate.wait` is **not** set, deliberately. Setting it today would turn every push to `main` red on
findings that live in `src/` and `web/` — code Epic 25 is explicitly forbidden to touch — and would break CI
for concurrent work mid-epic.

Set it once **all three** of these are true. **Re-measured by Story 25.6 on 2026-07-29 against the analysis of
`240afae`:**

1. ✅ `new_coverage` — **PASSES, at 90.2% against 80%.** This condition was the whole story on 2026-07-27
   morning (59.4%) and is now the least of the problem. Story 23.5's Vitest lcov report plus its *narrowed*
   coverage exclusions did it: excluding `web/scripts/**` (743 of the 918 uncovered lines) and
   `web/**/*.vue` left a denominator of genuinely testable code.
2. ❌ `new_reliability_rating` must be A — it is **D**, from **12 open bugs inside the new-code window**:
   1 CRITICAL, 9 MAJOR, 2 MINOR. **10 of the 12 are in `src/SpecScribe/`** (`SiteGenerator.cs` ×4,
   `assets/specscribe.css` ×3, `CapabilityStyler.cs`, `WorkGraph.cs`, `HtmlRenderAdapter.Dashboard.cs`),
   1 in `extension/src/extension.ts`, and 1 in `web/scripts/check-links.mjs`.
3. ❌ `new_security_rating` must be A — it is **C**, from **164 open vulnerabilities inside the new-code
   window**: 2 MAJOR, 162 MINOR. **161 of the 164 are in `src/`** — 160 `csharpsquid:S6444` plus one
   `csharpsquid:S4036` — and only 3 are in `web/scripts/`.

> ### ⚠ Correction (Story 25.6, 2026-07-29): the paragraph this replaces was wrong
>
> This section previously carried the following block-quote, measured at `b86fc27`:
>
> > **Both remaining blockers are in `web/scripts/**`** — dev-time harness scripts, coverage-excluded but
> > still fully analyzed, which is exactly the intended arrangement. Neither is C# and neither is Story
> > 17.2's: the `csharpsquid:S6444` band no longer drives the *new-code* security rating, though it still
> > drives the project-level one.
>
> **Every load-bearing claim in it is now false**, and the last one contradicted this same file's
> § *Rule-level decisions* → *Current decisions*, which said all along that `S6444` "drives security rating
> **C** and keeps `new_security_rating` at **B**". That half was right; this one was not.
>
> **Why it went stale:** the new-code period is a **sliding `days: 30` window**, and it has since swallowed
> the `S6444` band whole. § *The new-code period* above predicts exactly this failure mode — "new code ≈ all
> code" — and this is what it looks like when it happens. Nothing was fixed and nothing regressed; the window
> simply moved to cover code that was already there. Expect any *count* in this section to age the same way.
>
> **Ownership, corrected:**
>
> - The `csharpsquid:S6444` / `S4036` band — 161 of the 164 vulnerabilities — is **Story 17.2's**, per
>   § *Rule-level decisions*. It was never Epic 23's.
> - The 12-bug reliability sweep is **unowned**. 10 of the 12 are in `src/SpecScribe/`, which is outside
>   Epic 23's scope entirely; the single `check-links.mjs` bug is the only one Epic 23 could be said to own.
>   Naming it unowned is deliberate — it needs a home before the gate can be made blocking.

**Fixing the named `web/scripts/` files does not turn the gate green.** On Sonar's scale, `A` means *zero*
open issues of that class — not "none severe". Clearing `check-links.mjs:204` moves reliability D → C;
clearing `experiment-two-ir.mjs:95` moves security C → B. Both conditions still fail. The real precondition
is Story 17.2's 161-issue band **plus** the 12-bug sweep across `src/`.

Until then the actionable channel is **pull-request decoration** by the SonarQube Cloud GitHub App, not a red
CI job.

### Pull-request decoration

**Confirmed working**, first observed on [PR #3](https://github.com/IntegerMan/SpecScribe/pull/3) on
2026-07-27. SonarCloud contributes both:

- a check run named **`SonarCloud Code Analysis`** (GitHub App slug `sonarqubecloud`), and
- a summary comment from `sonarqubecloud[bot]`.

**No `permissions:` grant is required for this.** The workflow's `contents: read` is sufficient — the App uses
its own installation token, not `GITHUB_TOKEN`. The corollary is that decoration **cannot appear on pull
requests from forks**, which also get no `SONAR_TOKEN` and therefore no analysis at all.

> **A green PR check does not mean the project gate is green.** The two are different objects. The PR gate is
> evaluated against the pull request's own new code, and SonarCloud **drops conditions that do not apply** —
> on PR #3 only five of the six conditions were evaluated, because a documentation-and-YAML change contributes
> no new lines to cover, so `new_coverage` was absent. That PR passed while `main` was red on three conditions.
> Read the branch status for the project's real state:
>
> ```bash
> curl -s "https://sonarcloud.io/api/qualitygates/project_status?projectKey=IntegerMan_SpecScribe"
> ```

---

## Triaging findings

This is the repeatable pass. Run it top-to-bottom; it is designed so two sessions running it a month apart
produce comparable output. Story 25.2 performed the first baseline with it.

### Step 1 — Always pass `resolved=false`

`api/issues/search` **includes closed issues by default**, and the gap is large and growing: on 2026-07-27 the
unfiltered response reported **1,598** issues against a real unresolved count of **1,420** — a 178-issue
difference, all of it CLOSED/FIXED issues on paths the exclusion list removed from analysis. Triaging the
default response manufactures backlog items pointing at files Sonar no longer looks at.

### Step 2 — Take the shape of the set before any issue text

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false&ps=1&facets=rules,types,severities"
```

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=IntegerMan_SpecScribe&metricKeys=ncloc,files,coverage,duplicated_lines_density,security_rating,reliability_rating,sqale_rating,alert_status,sqale_index,new_lines"
```

### Step 3 — Triage by RULE, not by issue

This is the whole method. On 2026-07-27, 1,420 issues collapsed to ~40 rules and the **top three rules were
746 issues — 52.5% of everything**. A per-issue pass is a transcription, not a triage.

For each rule above the materiality bar record: rule id, name, count, severity, whether it is a SonarSource
rule (`csharpsquid:` / `css:` / `javascript:` / `typescript:` / `githubactions:`) or an **external Roslyn**
import (`external_roslyn:`), and one decision — **fixed**, **scheduled to a named story**, or **accepted with
rationale**.

**The materiality bar used by the 25.2 baseline**, restated so a future pass can match or deliberately change
it:

- **Every bug gets an individual decision.** No volume excuse. There were 14.
- **Every vulnerability rule gets a decision**, individually where the count is small.
- **Every rule with ≥ 20 unresolved issues** gets a decision, as a rule.
- **The INFO band is one decision.** All 771 INFO issues are `external_roslyn:` imports — .NET SDK analyzer
  output the scanner picks up from the build, not SonarSource rules.

Bugs and vulnerabilities enumerate cheaply:

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false&types=BUG&ps=100"
```

New-code issues — the ones actually driving the gate — need `inNewCodePeriod`:

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=IntegerMan_SpecScribe&resolved=false&inNewCodePeriod=true&types=BUG,VULNERABILITY&ps=100"
```

Rule names resolve with the **required** `organization` parameter — omitting it returns an error, not a rule:

```bash
curl -s "https://sonarcloud.io/api/rules/show?organization=integerman-github&key=csharpsquid:S6444"
```

### Step 4 — Check the existing decisions before deciding anything

Read **§ Rule-level decisions** below and the most recent
`## Deferred from: …-quality-gate-and-findings-triage` group in
[`_bmad-output/implementation-artifacts/deferred-work.md`](../_bmad-output/implementation-artifacts/deferred-work.md).
A rule already dispositioned there is **not re-triaged** — that is the entire point of recording it.

### Step 5 — Write the output where the project already reads it

Findings route into `deferred-work.md`, which is **parsed by `src/SpecScribe/DeferredWorkParser.cs` and
rendered on the portal's follow-up surface** (FR30 / Story 9.6). It is not a scratch file. The format is a
contract:

- Group heading: `## Deferred from: <label>` — the label must contain an `N-M-slug` with a letter in it for
  provenance to link back. A bare date will not match.
- Items are **column-0** list markers. Indented lines are continuations of the current item.
- Match the existing `- source_spec: / summary: / evidence:` shape.
- Resolution is `~~strikethrough~~` or a bracketed `[RESOLVED`. A bare word "RESOLVED" in prose does nothing.

**Budget: ≤ 15 items for a whole baseline pass.** Add `sprint-status.yaml` `action_items:` entries only for
things needing a *person* to act; findings scheduled into a story belong in `deferred-work.md` and must not be
duplicated in both.

**Verify by generating, not by re-reading the markdown:**

```bash
dotnet run --project src/SpecScribe -- generate --source _bmad-output --adrs docs/adrs
```

---

## Rule-level decisions

**This section is the single home for "Sonar reports X and this project deliberately does not follow it".**
Story 25.2 chose it over three alternatives:

| Rejected option | Why |
|---|---|
| Deactivate the rule in the SonarCloud **quality profile** | Server-side. Invisible in a diff, drifts silently, no reviewer ever sees it — the exact failure mode Story 25.1 rejected for exclusions, and the one the stray `Customized` gate demonstrates. |
| A new **`.editorconfig`** | Cannot reach `csharpsquid:` / `css:` / `javascript:` rules at all — only the `external_roslyn:` band — so it could never be the *single* home and would guarantee two places to look. It also changes local and CI **build** warning behaviour for `src/` and `tests/`, which Epic 25 must not touch. Rejected, so **no ADR is required**; neither `.editorconfig` nor `Directory.Build.props` exists in this repo. |
| Issue-level **"Won't Fix"** in the UI | Per-issue, does not scale to 156 or 326 instances, and server-side again. |

**Enforcement mechanism**, for rules dispositioned *accepted — will not fix*: add
`/d:sonar.issue.ignore.multicriteria` entries to the `begin` step in
[`build-test-analyze.yml`](../.github/workflows/build-test-analyze.yml), in a diff, with a comment naming this
section. For example:

```text
/d:sonar.issue.ignore.multicriteria="e1" `
/d:sonar.issue.ignore.multicriteria.e1.ruleKey="external_roslyn:CA1861" `
/d:sonar.issue.ignore.multicriteria.e1.resourceKey="**/*.cs" `
```

### Current decisions

**As of the 2026-07-27 baseline, the enforcement mechanism is deliberately applied to zero rules.** That is a
decision, not an omission, and the reason is worth keeping:

> Every rule in the current set is either **routed to a named Epic 17 story** — where suppressing it would
> hide scheduled work from the dashboard that is supposed to prove it done — or **INFO-band external Roslyn**,
> whose disposition is *accepted for now, measured at Story 17.3*, and Story 17.3's AC #1 requires a
> measurement before and after. Suppressing either band destroys the evidence the decision depends on.

| Rule(s) | Count (2026-07-27) | Decision |
|---|---|---|
| `csharpsquid:S6444` — regex without timeout | 156 | **Scheduled → Story 17.2.** Not noise despite MINOR severity: SpecScribe parses markdown from arbitrary third-party repositories, so catastrophic backtracking is a real input-driven surface. Drives project-level security rating **C**; its effect on `new_security_rating` moves with the sliding new-code window — see § *What would make the gate blocking* for the current measured value rather than trusting a fixed letter here. Not suppressed. |
| `csharpsquid:S4036` — OS command search in PATH | 1 | **Scheduled → Story 17.2**, with S6444. |
| `githubactions:S8233` / `S8264` | 3 | **Fixed by Story 25.2** — permissions moved to job level in `publish-docs-live-pages.yml`. `build-test-analyze.yml` (Story 25.1's own workflow) carries **zero** `githubactions:*` findings, which is worth keeping as evidence the gate is already clean on our own CI. |
| `external_roslyn:*` INFO band | 771 | **Accepted for now, not suppressed.** Revisit at Story 17.3 (the performance rules: `CA1861`, `CA1859`, `CA1822`) and as a bulk disposition for the rest. |
| Everything else above the bar | — | Routed to Stories 17.1 / 17.3 / 17.5 — see the `25-2-quality-gate-and-findings-triage` group in `deferred-work.md`. |

See [ADR 0035](adrs/0035-sonarcloud-quality-gate-and-rule-decision-policy.md) for the standing policy this table
implements — gate identity, the coverage/new-code-period/`qualitygate.wait` decisions, and why this table is the
one home for a rule-level exception.

## The agent-facing digest (Story 25.4)

The triage pass above is for a **human deciding project policy**. There is a second, narrower need: an agent
running `create-story` or `dev-story` wants to know what analysis already says about *the three files it is
about to touch* — and it wants that without reading 1,488 issues.

`tools/analysis-digest/` fetches the same public endpoints this document already uses and writes the findings
to `.specscribe/analysis/` as [ADR 0023](adrs/0023-agent-facing-analysis-observation-contract.md)
`AnalysisObservation` records: one index plus one shard per source file.

```bash
node tools/analysis-digest/index.mjs
```

- **No token.** It uses the same anonymous access as every `curl` in this document. It never reads, prompts
  for, or writes a credential.
- **Gitignored** (`.gitignore`'s `.specscribe` entry — [ADR 0014](adrs/0014-specscribe-settings-folder-format.md)),
  and it writes nothing into `SpecScribeOutput/`, so the golden fingerprint cannot move.
- **Opt-in by invocation.** No hook, no watcher, no `postinstall`, no MSBuild target. If you do not run it,
  nothing happens.
- **A failed fetch leaves the previous digest untouched and exits 0.** It never writes an empty digest,
  because an empty digest reads as *"this code is clean"*.
- Refresh it after a new analysis lands. The consumption rules — including the read-time staleness rule — are
  in `CLAUDE.md` § Analysis observations, which is auto-loaded into every session.

```bash
node tools/analysis-digest/index.mjs --check-staleness <revision>   # print the provenance block, write nothing
node tools/analysis-digest/index.mjs --help
```

### The Sonar MCP server is a complement, not the contract

SonarSource ships an official MCP server for SonarQube Cloud and documents Claude Code explicitly. For
**interactive** questions — *"what does Sonar think of this file right now?"* — it is strictly better than
anything this project would build, and it costs no code.

**It is not the contract, and it cannot replace the digest.** Named plainly so nobody swaps one for the other:

- It delivers **Sonar's** model, not the source-agnostic `AnalysisObservation` profile that Epic 26's product
  surfaces bind to.
- It **requires a token**; the digest is anonymous.
- It **dies offline**; the digest is a local file that keeps working.
- It **cannot see raw compiler output**, so the second proven source class is out of reach.
- It **cannot attach observations to planning entities**, which is the whole point of ADR 0023 Decision 5.
- It has **no provenance/staleness contract** — nothing tells you which revision an answer describes.

Use both, with those roles.

## Badges

Story 25.6 put two badges under the README's H1: **build status** and **coverage**. This section is the
disclosure record for them — what each URL reveals, and to whom. The README is a front door; this is the
record.

### What ships, and what deliberately does not

| | Build badge | Coverage badge |
|---|---|---|
| **Image URL** | `https://github.com/IntegerMan/SpecScribe/actions/workflows/build-test-analyze.yml/badge.svg?branch=main` | `https://sonarcloud.io/api/project_badges/measure?project=IntegerMan_SpecScribe&metric=coverage` |
| **Links to** | the workflow's run history | the SonarQube Cloud `coverage` measure page |
| **Served by** | GitHub | SonarSource |
| **Discloses** | repo owner and name, the workflow *filename*, the branch name `main`, and pass/fail of the latest run | the SonarCloud project key and the current `coverage` value |
| **Already public?** | Yes — the repo is public and the workflow file is in it | Yes — `IntegerMan_SpecScribe` and `integerman-github` are literals in `build-test-analyze.yml:185-186`, and the project is `visibility: public` |
| **Carries a token?** | **No** | **No** |

No quality-gate, reliability, or security badge ships. All three render red today, and a permanently-red
badge on the front page is worse than none. `sqale_rating` is green (A) but was left out as well — the README
carries two live badges, not a wall.

**Trigger for revisiting the gate badge.** No story is seated for it; Story 25.6 offered one and the owner
declined, on the grounds that seating a story against a precondition nobody has scheduled is bookkeeping
rather than work. The trigger lives here instead:

> When `alert_status` reads `OK` — check with the `project_status` command in § *The conditions, transcribed* —
> add the gate badge to the README's badge row, pointing at
> `https://sonarcloud.io/api/project_badges/measure?project=IntegerMan_SpecScribe&metric=alert_status`, and
> delete the "Why there is no quality-gate badge" paragraph in the README's § *Continuous integration*. That
> paragraph and this trigger are a matched pair; neither should outlive the red gate.

Reaching `OK` needs Story 17.2's `S6444`/`S4036` band cleared **and** the 12-bug reliability sweep across
`src/` — see § *What would make the gate blocking*. Neither is a README task.

### No token, ever, for this project

Neither URL carries a credential, and neither needs one: the project is `visibility: public`, so every badge
endpoint answers anonymously. SonarCloud's `api/project_badges/token` endpoint mints a badge token for
*private* projects, and **it must never be called for this project**. A token in a README image URL is a
credential published on the front page and in the NuGet listing — and rotating it silently breaks every
rendered copy.

NFR12's literal scope is generated output and committed directory-scoped settings files, so a README token
would sit just outside it. It plainly crosses NFR12's *intent*, and Story 25.4 already holds this project to
writing no token value anywhere. Public project, public metrics, no token needed: there is nothing to weigh
against it.

### The disclosure that is about the reader, not the project

Nothing in either URL reveals a private repository, a credential, a contributor identity, a source path, or a
finding. The rendered values are already published on a public SonarCloud dashboard and a public Actions tab.

The genuinely new disclosure is about **whoever views the README**:

- **On github.com**, README images are proxied through `camo.githubusercontent.com`. The viewer's IP reaches
  GitHub, not SonarSource.
- **Rendered anywhere else** — nuget.org, a docs mirror, an RSS reader, an IDE package pane — the image
  request goes **direct to `sonarcloud.io`**, disclosing the viewer's IP and User-Agent to SonarSource.

That is a normal and accepted cost of badges. The point is that it is written down rather than unnoticed.

### Constraint on any future badge change: the NuGet allow-list

`src/SpecScribe/SpecScribe.csproj:23,56` sets `PackageReadmeFile` and packs the repo-root `README.md` into the
package, so **this README is also the NuGet.org package listing**. NuGet.org renders README images only from
an allow-list of domains; anything else is dropped and raises a warning visible to package owners. Relative
image and link paths do not resolve there either — badge links must be absolute `https://` URLs.

Both current hosts are allow-listed: `sonarcloud.io`, and the
`github.com/<owner>/<repo>/actions/workflows/<file>/badge.svg` path shape. **Re-check the allow-list before
swapping in any other badge host** (shields.io, badgen, a self-hosted SVG). A self-generated coverage SVG is
additionally forbidden on different grounds: it would be a second, separately-computed coverage number, which
Story 25.6's AC #1 rules out. The badge must be SonarQube Cloud's own render of its own measure.

Whether the packaged README actually renders on the package listing is Epic 16's verification, not this
record's.

## Security notes

- The token is never committed. It exists only in SonarCloud and in GitHub's encrypted secret store.
- The workflow references it as `$env:SONAR_TOKEN` and never interpolates `${{ secrets.SONAR_TOKEN }}` into a
  `run:` script body, which would inline the value into the rendered command in the logs.
- Pull requests **from forks** get no secrets. Build and test still run there; only analysis is skipped. The
  workflow deliberately does not use `pull_request_target` to work around this — that would run untrusted code
  with write-scoped credentials.
- The workflow requests least privilege (`permissions: contents: read`). PR decoration is performed by the
  SonarQube Cloud GitHub App using its own installation token.
