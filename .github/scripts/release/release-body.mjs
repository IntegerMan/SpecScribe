#!/usr/bin/env node
// ADR 0040 §Decision 6 + §Decision 2/§Decision 13 — compose the GitHub Release body.
// [Story 16.4 AC #5, AC #7, Task 8]
//
// 🚨 THIS SCRIPT MUST NOT THROW. EVER.
//
// It runs at the LAST step of a release, after the packages have already been pushed. By then the version is
// permanently consumed (ADR 0040 §Decision 10 — nuget.org rejects a duplicate version and permits only
// unlisting), so a crash here does not "fail the release": it burns a version number over a formatting problem
// and leaves an announced release with no notes. Every read is guarded and every failure degrades to a stated
// fallback line. AC #7 is explicit that an absent file, an absent section and an empty section are all the SAME
// non-fatal path.
//
// WHY THE CHANGELOG IS COPIED RATHER THAN GENERATED. ADR 0040 §Decision 6 rejects GitHub's generated release
// notes for a reason specific to this repository: commits routinely bundle several stories (CLAUDE.md
// § Concurrent work), so the commit is not the unit of change here — the story is. Do not reintroduce
// generated notes as a convenience.
//
// ⚠️ THE `changelog.d/` ASSEMBLER SEAM. ADR 0040 §Decision 6 (as revised by the Story 16.1 decisions pass) has
// stories write `changelog.d/<story-key>.md` fragments which the release job concatenates by section into
// CHANGELOG.md. It assigns THE FORMAT AND THE ASSEMBLER to Story 16.6 and only the INVOCATION to this story.
// Neither `CHANGELOG.md` nor `changelog.d/` exists at 16.4's baseline, so there is nothing to invoke yet and
// this story deliberately does not author one — writing the assembler here would take 16.6's format decisions
// with it. The seam is `readChangelogSection` below: when 16.6 lands the assembler, the release job runs it
// BEFORE this script and this script keeps reading the assembled CHANGELOG.md unchanged.
//
// usage: release-body.mjs --version <v> [--changelog <path>] [--digests <path>] [--repo <owner/repo>] [--dry-run]
// stdout: the release body, as Markdown.

import { readFileSync } from 'node:fs';

const FALLBACK = 'No user-visible changes in this release.';

function arg(name, fallback = null) {
  const i = process.argv.indexOf(`--${name}`);
  return i !== -1 && i + 1 < process.argv.length ? process.argv[i + 1] : fallback;
}
const flag = (name) => process.argv.includes(`--${name}`);

const version = arg('version');
if (!version) {
  process.stderr.write('usage: release-body.mjs --version <v> [--changelog <p>] [--digests <p>] [--repo <r>]\n');
  process.exit(2); // A missing --version is a WORKFLOW bug, caught before any publish, not a release-time fault.
}

const changelogPath = arg('changelog', 'CHANGELOG.md');
const digestsPath = arg('digests');
const repo = arg('repo', '');
const appendToPath = arg('append-to');
const dryRun = flag('dry-run');

const warnings = [];

/**
 * Extracts the released version's section from a Keep a Changelog 1.1.0 file.
 * Absent file / absent section / empty section are all the same non-fatal answer: null.
 */
function readChangelogSection(path, wantedVersion) {
  let text;
  try {
    text = readFileSync(path, 'utf8');
  } catch (err) {
    // ENOENT is the EXPECTED state until Story 16.6 authors the file — a warning, never an error.
    warnings.push(
      err.code === 'ENOENT'
        ? `no ${path} at the released commit (Story 16.6 owns authoring it)`
        : `could not read ${path}: ${err.message}`,
    );
    return null;
  }

  const lines = text.split(/\r?\n/);
  // Keep a Changelog headers are `## [0.1.0-preview.1] - 2026-08-08`. Match on the bracketed version only, so
  // the date separator (hyphen, en dash, em dash) and any trailing marker such as `— WITHDRAWN` cannot break it.
  const isVersionHeader = (line, v) =>
    new RegExp(`^##\\s+\\[\\s*v?${v.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\s*\\]`, 'i').test(line);

  const start = lines.findIndex((l) => isVersionHeader(l, wantedVersion));
  if (start === -1) {
    warnings.push(`${path} has no section for ${wantedVersion}`);
    return null;
  }

  const rest = lines.slice(start + 1);
  const end = rest.findIndex((l) => /^##\s/.test(l));
  const body = (end === -1 ? rest : rest.slice(0, end)).join('\n').trim();

  if (body === '') {
    // ADR 0040 §Decision 6: "An empty release … is not an error." A re-cut after a failed publish, a CI-only
    // fix or a dependency bump may legitimately carry no user-visible change.
    warnings.push(`${path}'s section for ${wantedVersion} is empty`);
    return null;
  }
  return body;
}

/** Reads `sha256sum`-format lines into {file, digest} pairs. Never throws. */
function readDigests(path) {
  if (!path) return [];
  let text;
  try {
    text = readFileSync(path, 'utf8');
  } catch (err) {
    warnings.push(`could not read digests from ${path}: ${err.message}`);
    return [];
  }
  return text
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean)
    .map((line) => {
      // `sha256sum` writes "<hex>  <name>"; BSD/macOS `shasum` writes the same. A leading `*` marks binary mode.
      const m = /^([0-9a-fA-F]{64})\s+\*?(.+)$/.exec(line);
      if (!m) {
        warnings.push(`unparseable digest line: ${line}`);
        return null;
      }
      return { digest: m[1].toLowerCase(), file: m[2].trim() };
    })
    .filter(Boolean)
    .sort((a, b) => a.file.localeCompare(b.file));
}

const out = [];

if (appendToPath) {
  let existingBody;
  try {
    existingBody = readFileSync(appendToPath, 'utf8').trim();
  } catch (err) {
    process.stderr.write(`::error::release body: could not read Stage A body ${appendToPath}: ${err.message}\n`);
    process.exit(1);
  }

  if (existingBody === '') {
    process.stderr.write(`::error::release body: Stage A body ${appendToPath} is empty; refusing to replace it.\n`);
    process.exit(1);
  }

  const section = readChangelogSection(changelogPath, version);
  out.push(section ?? FALLBACK);
  out.push('');
  out.push(existingBody);
  process.stdout.write(out.join('\n').trimEnd() + '\n');
  for (const w of warnings) process.stderr.write(`::warning::release body: ${w}\n`);
  process.exit(0);
}

if (dryRun) {
  out.push('> ⚠️ **DRY RUN** — this body was composed by a `workflow_dispatch` rehearsal.');
  out.push('> Nothing was published to nuget.org and no GitHub Release was created.');
  out.push('');
}

const section = readChangelogSection(changelogPath, version);
out.push(section ?? FALLBACK);
out.push('');

const digests = readDigests(digestsPath);
if (digests.length > 0) {
  // ADR 0040 §Decision 13 declines code signing for the preview, and §Decision 2 makes the digest the
  // compensating control: this is the ONLY channel without integrity by construction — npm publishes provenance
  // attestations by default and NuGet packages carry the registry's own guarantees. A consumer clicking through
  // SmartScreen (Windows) or Gatekeeper (macOS) must have something to verify against.
  out.push('## Verifying these downloads');
  out.push('');
  out.push('These binaries are **not code-signed** for the preview, so Windows SmartScreen will warn and macOS');
  out.push('Gatekeeper will block until you clear it explicitly. Check the SHA-256 of what you downloaded');
  out.push('against this table before running it:');
  out.push('');
  out.push('```sh');
  out.push('sha256sum <file>          # Linux');
  out.push('shasum -a 256 <file>      # macOS');
  out.push('Get-FileHash <file>       # Windows PowerShell');
  out.push('```');
  out.push('');
  out.push('| asset | SHA-256 |');
  out.push('|---|---|');
  for (const { file, digest } of digests) out.push(`| \`${file}\` | \`${digest}\` |`);
  out.push('');
} else {
  warnings.push('no digests were supplied, so the release body carries no integrity table');
}

out.push('## Installing');
out.push('');
out.push('```sh');
out.push(`dotnet tool install --global SpecScribe --version ${version}`);
out.push('```');
out.push('');
out.push('Each `specscribe-<version>-<rid>` archive contains the executable **and its `renderer/` directory**.');
out.push('Extract the whole archive and keep the two together — the CLI cannot render a single page without');
out.push('its sibling renderer, and mixing halves from two releases fails as *wrong output*, not as an error.');
out.push('');
out.push('Requires **Node** (`^22.19.0 || ^24.11.0 || >=26.0.0`). The `dotnet tool` channel also requires');
out.push('**.NET 10**; the self-contained archives do not.');

if (repo) {
  out.push('');
  out.push(`Built from a clean checkout by CI. See [docs/Releasing.md](https://github.com/${repo}/blob/main/docs/Releasing.md).`);
}

process.stdout.write(out.join('\n').trimEnd() + '\n');

// Warnings go to STDERR so they annotate the job log without ever contaminating the release body.
for (const w of warnings) process.stderr.write(`::warning::release body: ${w}\n`);
