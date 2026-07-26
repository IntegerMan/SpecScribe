# SonarCloud Setup

How to connect this repository to [SonarQube Cloud](https://sonarcloud.io) (formerly SonarCloud) so that
every push to `main` and every pull request is analyzed for code quality.

The CI workflow that performs the analysis — [`.github/workflows/build-test-analyze.yml`](../.github/workflows/build-test-analyze.yml)
— is already committed and **already builds and tests without any of this setup**. What the steps below add is
the analysis upload. Until they are done, CI still runs green; the three scanner steps simply skip.

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

`extension/src/**` is deliberately **in** scope — it is genuine first-party source.

`tests/SpecScribe.Tests` is classified as test code automatically by SonarScanner for .NET, via its
`Microsoft.NET.Test.Sdk` / xunit references.

### Quality gate

No quality gate is enforced by this workflow: `sonar.qualitygate.wait` is deliberately **not** set, so a
failing gate does not currently fail the build. Analysis results are reported, not gated.

## Security notes

- The token is never committed. It exists only in SonarCloud and in GitHub's encrypted secret store.
- The workflow references it as `$env:SONAR_TOKEN` and never interpolates `${{ secrets.SONAR_TOKEN }}` into a
  `run:` script body, which would inline the value into the rendered command in the logs.
- Pull requests **from forks** get no secrets. Build and test still run there; only analysis is skipped. The
  workflow deliberately does not use `pull_request_target` to work around this — that would run untrusted code
  with write-scoped credentials.
- The workflow requests least privilege (`permissions: contents: read`). PR decoration is performed by the
  SonarQube Cloud GitHub App using its own installation token.
