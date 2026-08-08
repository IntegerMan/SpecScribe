#!/usr/bin/env node
// Self-test for the release pipeline's assertions. [Story 16.4 AC #8, Task 10]
//
// 🚨 WHY THIS EXISTS, AND WHY IT RUNS ON EVERY RELEASE RATHER THAN ONCE.
//
// Every failure mode ADR 0040 routes to Story 16.4 is a GREEN PIPELINE THAT SHIPS SOMETHING WRONG — a build
// date stamped from the run instead of the commit, an archive missing its renderer, a version already consumed
// on nuget.org, a release body that silently lost its notes. Each is guarded by an assertion, and *an assertion
// nobody has watched go red is an assertion that has not been tested*.
//
// The story asked for three negative proofs, run deliberately, once. Running them on every release is strictly
// better and costs seconds: it means the guards cannot rot into no-ops between releases, which is exactly what
// happened to this project's `check:ir-content` derivation (CLAUDE.md § Changing specscribe.css — a dangling
// `else` meant no id was ever collected, every id-bearing selector was pruned, and every gate stayed green for
// an unknown length of time).
//
// It is also why the assertions are SCRIPTS rather than inline `run:` blocks. Shell embedded in YAML can only
// be exercised by triggering the pipeline; a script can be driven from fixtures, on a laptop, in a second.
//
// usage: node selftest.mjs        (exit 0 = every assertion behaves, red and green)

import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { crc32 } from 'node:zlib';

const HERE = dirname(fileURLToPath(import.meta.url));
const work = mkdtempSync(join(tmpdir(), 'specscribe-release-selftest-'));
const bash = process.platform === 'win32' && existsSync('C:\\Program Files\\Git\\bin\\bash.exe')
  ? 'C:\\Program Files\\Git\\bin\\bash.exe'
  : 'bash';

let passed = 0;
const failures = [];

function check(name, fn) {
  try {
    fn();
    passed++;
    process.stdout.write(`  ok   ${name}\n`);
  } catch (err) {
    failures.push(`${name}: ${err.message}`);
    process.stdout.write(`  FAIL ${name}\n         ${err.message}\n`);
  }
}

/** Runs a command, returning {code, stdout, stderr} instead of throwing on a non-zero exit.
 *  spawnSync rather than execFileSync deliberately: execFileSync RETURNS stdout and only carries stderr on the
 *  thrown error, so a script that succeeds while writing a `::warning::` to stderr — which is exactly what
 *  release-body.mjs must do — looks silent to the test. */
function run(cmd, args, { input, cwd } = {}) {
  const r = spawnSync(cmd, args, { input: input ?? '', encoding: 'utf8', cwd });
  return { code: r.status ?? -1, stdout: r.stdout ?? '', stderr: r.stderr ?? String(r.error ?? '') };
}

const node = (script, args, opts) => run(process.execPath, [join(HERE, script), ...args], opts);
const sh = (script, args) => run(bash, [join(HERE, script), ...args]);

function expectCode(result, want, what) {
  if (result.code !== want) {
    throw new Error(
      `${what}: expected exit ${want}, got ${result.code}\n         stdout: ${result.stdout.trim()}\n         stderr: ${result.stderr.trim()}`,
    );
  }
}
function expectContains(haystack, needle, what) {
  if (!haystack.includes(needle)) throw new Error(`${what}: expected to find ${JSON.stringify(needle)} in:\n${haystack}`);
}
function expectAbsent(haystack, needle, what) {
  if (haystack.includes(needle)) throw new Error(`${what}: did NOT expect ${JSON.stringify(needle)} in:\n${haystack}`);
}

// ══ 0. Stage A tag allocation (ADR 0040 §Decision 9) ══════════════════════════════════════════════════════
process.stdout.write('\nnext-preview-tag.mjs — allocate Stage A tags without a second version source\n');

check('bootstrap: no existing release tags starts at v0.1.0-preview.1', () => {
  const r = node('next-preview-tag.mjs', [], { input: '' });
  expectCode(r, 0, 'bootstrap tag allocation');
  if (r.stdout.trim() !== 'v0.1.0-preview.1') throw new Error(`expected bootstrap tag, got ${r.stdout.trim()}`);
});

check('increments the preview counter on the highest semantic base', () => {
  const r = node('next-preview-tag.mjs', [], {
    input: 'v0.1.0-preview.4\nv0.2.0-preview.1\nv0.1.1-preview.9\nnot-a-release-tag\n',
  });
  expectCode(r, 0, 'next preview tag allocation');
  if (r.stdout.trim() !== 'v0.2.1-preview.10') throw new Error(`expected next patch and global preview counter, got ${r.stdout.trim()}`);
});

check('a reviewed release-base file controls the next semantic target', () => {
  const baseFile = join(work, 'release-base');
  writeFileSync(baseFile, '1.0.0\n');
  const r = node('next-preview-tag.mjs', ['--base-file', baseFile], { input: 'v0.2.1-preview.10\n' });
  expectCode(r, 0, 'release-base override');
  if (r.stdout.trim() !== 'v1.0.0-preview.11') throw new Error(`expected reviewed target, got ${r.stdout.trim()}`);
});

check('a release-base file cannot move the semantic version backwards', () => {
  const baseFile = join(work, 'backwards-release-base');
  writeFileSync(baseFile, '0.1.0\n');
  const r = node('next-preview-tag.mjs', ['--base-file', baseFile], { input: 'v0.2.1-preview.10\n' });
  expectCode(r, 1, 'backwards release-base override');
  expectContains(r.stderr, 'below the latest release base', 'backwards override rejection');
});

// ══ 1. SOURCE_DATE_EPOCH (ADR 0040 §Decision 7) — negative proof (a) ═════════════════════════════════════════
process.stdout.write('\nassert-source-date-epoch.sh — the csproj falls back to TODAY, silently\n');

check('green: a real epoch passes', () => {
  const r = sh('assert-source-date-epoch.sh', ['1754611200']);
  expectCode(r, 0, 'a well-formed epoch');
  expectContains(r.stdout, 'OK', 'verdict');
});

check('RED (a): an empty value fails before anything is built', () => {
  const r = sh('assert-source-date-epoch.sh', ['']);
  expectCode(r, 1, 'empty');
  // The message must name the silent fallback, or a reader fixes the symptom and not the cause.
  expectContains(r.stderr, "today's date", 'names the csproj fallback');
});

check('RED (a): a non-numeric value fails', () => {
  expectCode(sh('assert-source-date-epoch.sh', ['not-an-epoch']), 1, 'alphabetic');
});

check('RED (a): 11 digits fails — the same bound the csproj regex uses', () => {
  // The guard must accept exactly what its subject accepts. A guard that is more permissive than the thing it
  // guards passes a value the csproj will then silently reject and replace with today's date.
  expectCode(sh('assert-source-date-epoch.sh', ['12345678901']), 1, '11 digits');
  expectCode(sh('assert-source-date-epoch.sh', ['1234567890']), 0, '10 digits');
});

check('RED (a): a value with surrounding whitespace fails', () => {
  // MSBuild does not trim, so ' 1754611200' hits the fallback path. A shell guard that trims would pass it.
  expectCode(sh('assert-source-date-epoch.sh', [' 1754611200']), 1, 'leading space');
});

check('RED (a): a negative value fails', () => {
  expectCode(sh('assert-source-date-epoch.sh', ['-1']), 1, 'negative');
});

// ══ 3. The archive assertion (ADR 0040 §Decision 2/5) — negative proof (b) ═══════════════════════════════════
process.stdout.write('\nassert-archive-renderer.sh — assert the PATH, not the file count\n');

/** Minimal STORED (uncompressed) zip writer. Deterministic, and needs no `zip` binary on any platform. */
function writeZip(target, entries) {
  const chunks = [];
  const central = [];
  let offset = 0;
  for (const [name, content] of entries) {
    const nameBuf = Buffer.from(name, 'utf8');
    const data = Buffer.from(content, 'utf8');
    const sum = crc32(data);
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(20, 4);
    local.writeUInt16LE(0, 8); // stored
    local.writeUInt32LE(sum, 14);
    local.writeUInt32LE(data.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBuf.length, 26);
    chunks.push(local, nameBuf, data);

    const cd = Buffer.alloc(46);
    cd.writeUInt32LE(0x02014b50, 0);
    cd.writeUInt16LE(20, 4);
    cd.writeUInt16LE(20, 6);
    cd.writeUInt16LE(0, 10);
    cd.writeUInt32LE(sum, 16);
    cd.writeUInt32LE(data.length, 20);
    cd.writeUInt32LE(data.length, 24);
    cd.writeUInt16LE(nameBuf.length, 28);
    cd.writeUInt32LE(offset, 42);
    central.push(cd, nameBuf);
    offset += local.length + nameBuf.length + data.length;
  }
  const centralBuf = Buffer.concat(central);
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(entries.length, 8);
  eocd.writeUInt16LE(entries.length, 10);
  eocd.writeUInt32LE(centralBuf.length, 12);
  eocd.writeUInt32LE(offset, 16);
  writeFileSync(target, Buffer.concat([...chunks, centralBuf, eocd]));
}

const ROOT = 'specscribe-0.1.0-preview.1-win-x64';
const goodZip = join(work, 'good.zip');
const badZip = join(work, 'bad.zip');

writeZip(goodZip, [
  [`${ROOT}/specscribe.exe`, 'MZ...'],
  [`${ROOT}/renderer/server/index.mjs`, 'export default {}'],
  [`${ROOT}/renderer/public/x.css`, 'a{}'],
]);
// THE MEASURED FALSE PASS, reproduced: the renderer payload is PRESENT and at the WRONG DEPTH. Same entry
// count, same byte total, no entry point at the archived path. This is what a size-or-count check calls a pass.
writeZip(badZip, [
  [`${ROOT}/specscribe.exe`, 'MZ...'],
  [`${ROOT}/renderer/renderer/server/index.mjs`, 'export default {}'],
  [`${ROOT}/renderer/public/x.css`, 'a{}'],
]);

check('green: an archive with the entry point at its archived path passes', () => {
  const r = sh('assert-archive-renderer.sh', [goodZip, ROOT]);
  expectCode(r, 0, 'a good zip');
  expectContains(r.stdout, 'OK', 'verdict');
});

check('RED (b): a doubled path — same entry count, same bytes — FAILS', () => {
  const r = sh('assert-archive-renderer.sh', [badZip, ROOT]);
  expectCode(r, 1, 'a wrong-path zip');
  expectContains(r.stderr, 'does NOT contain', 'verdict');
  // The diagnosis has to point at the archive's rooting, not at the publish step, or the reader debugs the
  // wrong half — this is precisely the cycle Story 16.3 paid for.
  expectContains(r.stderr, 'rooted wrongly', 'diagnosis');
});

check('RED (b): the same assertion over a .tar.gz', () => {
  const tarRoot = join(work, 'tarsrc');
  const dir = join(tarRoot, ROOT);
  mkdirSync(join(dir, 'renderer', 'server'), { recursive: true });
  writeFileSync(join(dir, 'specscribe'), '#!/bin/sh\n');

  // ⚠️ RELATIVE paths, with cwd — never an absolute Windows path. GNU tar reads a leading `C:` as a REMOTE HOST
  // spec (`host:path`, the rsh transport), so `tar -czf C:\…\x.tar.gz` tries to reach a machine called `C` and
  // writes nothing. It exits non-zero, which a test asserting exit 1 mistakes for the assertion firing —
  // the archive check then looks proven when it never ran. `--force-local` fixes it for GNU tar but is not a
  // bsdtar (macOS) flag, so the portable answer is to stay relative.
  const build = (name) => {
    const r = run('tar', ['-czf', name, ROOT], { cwd: tarRoot });
    if (r.code !== 0) throw new Error(`tar failed to build the fixture: ${r.stderr.trim()}`);
    return join(tarRoot, name);
  };

  expectCode(sh('assert-archive-renderer.sh', [build('missing.tar.gz'), ROOT]), 1, 'tar.gz without the entry point');

  writeFileSync(join(dir, 'renderer', 'server', 'index.mjs'), 'export default {}');
  expectCode(
    sh('assert-archive-renderer.sh', [build('complete.tar.gz'), ROOT]),
    0,
    'tar.gz with the entry point',
  );
});

check('RED: a BACKSLASH-separated zip fails, and is diagnosed as such rather than as "missing"', () => {
  // PowerShell 5.1 `Compress-Archive` writes backslashes, violating APPNOTE 4.4.17.1. The payload is present
  // and at the right depth, so the naive message would send the reader to debug the publish step. Found by
  // rehearsing the release job on the development machine, 2026-08-08.
  const backslashZip = join(work, 'backslash.zip');
  writeZip(backslashZip, [
    [`${ROOT}\\specscribe.exe`, 'MZ...'],
    [`${ROOT}\\renderer\\server\\index.mjs`, 'export default {}'],
  ]);
  const r = sh('assert-archive-renderer.sh', [backslashZip, ROOT]);
  expectCode(r, 1, 'a backslash-separated zip');
  expectContains(r.stderr, 'BACKSLASH', 'separator diagnosis');
  expectContains(r.stderr, 'Compress-Archive', 'names the tool that does this');
});

check('RED: a missing archive, and an unknown extension, both fail rather than pass unchecked', () => {
  expectCode(sh('assert-archive-renderer.sh', [join(work, 'nope.zip'), ROOT]), 1, 'missing archive');
  const odd = join(work, 'thing.7z');
  writeFileSync(odd, 'x');
  // "I could not check it" must never pass for "I checked it".
  expectCode(sh('assert-archive-renderer.sh', [odd, ROOT]), 1, 'unknown extension');
});

// ══ 4. The registry preflight (ADR 0040 §Decision 10) ════════════════════════════════════════════════════════
process.stdout.write('\nnuget-version-consumed.mjs — refuse a burned version in seconds, not at the push step\n');

check('green: a 404 (package never published) leaves every version free', () => {
  expectCode(node('nuget-version-consumed.mjs', ['0.1.0-preview.1'], { input: '' }), 0, 'no registration');
});

check('green: a version not in the index is free', () => {
  const input = JSON.stringify({ versions: ['0.1.0-preview.1', '0.1.0-preview.2'] });
  expectCode(node('nuget-version-consumed.mjs', ['0.1.0-preview.3'], { input }), 0, 'unpublished version');
});

check('RED: an already-published version fails, and names the forward recovery', () => {
  const input = JSON.stringify({ versions: ['0.1.0-preview.1'] });
  const r = node('nuget-version-consumed.mjs', ['0.1.0-preview.1'], { input });
  expectCode(r, 1, 'consumed version');
  expectContains(r.stdout, 'ALREADY PUBLISHED', 'verdict');
  expectContains(r.stdout, 'preview.N+1', 'forward recovery');
});

check('RED: version comparison is case-insensitive and ignores build metadata', () => {
  // nuget.org normalises both, so a raw string compare would let a duplicate through to a 409 at the push step.
  const input = JSON.stringify({ versions: ['0.1.0-Preview.1'] });
  expectCode(node('nuget-version-consumed.mjs', ['0.1.0-preview.1'], { input }), 1, 'case difference');
  expectCode(node('nuget-version-consumed.mjs', ['0.1.0-preview.1+abc123'], { input }), 1, 'build metadata');
});

check('fails CLOSED on an unreadable index', () => {
  expectCode(node('nuget-version-consumed.mjs', ['0.1.0-preview.1'], { input: '<html>502</html>' }), 1, 'garbage');
});

// ══ 5. The release body (ADR 0040 §Decision 6, §Decision 2/13) ═══════════════════════════════════════════════
process.stdout.write('\nrelease-body.mjs — it must NEVER hard-fail; the version is already burned by then\n');

const digestsFile = join(work, 'digests.txt');
writeFileSync(
  digestsFile,
  'a'.repeat(64) + '  specscribe-0.1.0-preview.1-win-x64.zip\n' +
  'b'.repeat(64) + '  specscribe-0.1.0-preview.1-linux-x64.tar.gz\n',
);

const FALLBACK = 'No user-visible changes in this release.';

check('AC #7: an ABSENT CHANGELOG.md yields the fallback line and exit 0', () => {
  const r = node('release-body.mjs', ['--version', '0.1.0-preview.1', '--changelog', join(work, 'nope.md')]);
  expectCode(r, 0, 'absent changelog');
  expectContains(r.stdout, FALLBACK, 'fallback line');
  expectContains(r.stderr, '::warning::', 'warns rather than fails');
});

check('AC #7: an ABSENT SECTION yields the fallback line and exit 0', () => {
  const cl = join(work, 'other-version.md');
  writeFileSync(cl, '# Changelog\n\n## [0.9.9] - 2026-01-01\n\n### Added\n- something else\n');
  const r = node('release-body.mjs', ['--version', '0.1.0-preview.1', '--changelog', cl]);
  expectCode(r, 0, 'absent section');
  expectContains(r.stdout, FALLBACK, 'fallback line');
  expectAbsent(r.stdout, 'something else', 'must not leak another version’s notes');
});

check('AC #7: an EMPTY SECTION yields the fallback line and exit 0', () => {
  const cl = join(work, 'empty-section.md');
  writeFileSync(cl, '# Changelog\n\n## [0.1.0-preview.1] - 2026-08-08\n\n## [0.0.9] - 2026-01-01\n\n- old\n');
  const r = node('release-body.mjs', ['--version', '0.1.0-preview.1', '--changelog', cl]);
  expectCode(r, 0, 'empty section');
  expectContains(r.stdout, FALLBACK, 'fallback line');
  expectAbsent(r.stdout, '- old', 'must stop at the next ## header');
});

check('green: a present section is copied verbatim and bounded by the next header', () => {
  const cl = join(work, 'good.md');
  writeFileSync(
    cl,
    '# Changelog\n\n## [0.1.0-preview.1] - 2026-08-08\n\n### Added\n- The renderer ships inside the package.\n\n' +
      '### Changed\n- **BREAKING:** SPECSCRIBE_RENDERER_DIR is no longer required.\n\n## [0.0.9] - 2026-01-01\n\n- old\n',
  );
  const r = node('release-body.mjs', ['--version', '0.1.0-preview.1', '--changelog', cl, '--digests', digestsFile]);
  expectCode(r, 0, 'present section');
  expectContains(r.stdout, 'The renderer ships inside the package.', 'copied notes');
  expectContains(r.stdout, '**BREAKING:**', 'the breaking-change marker survives');
  expectAbsent(r.stdout, '- old', 'bounded by the next header');
  expectAbsent(r.stdout, FALLBACK, 'no fallback when there are real notes');
});

check('AC #5: every supplied digest reaches the release body', () => {
  const r = node('release-body.mjs', ['--version', '0.1.0-preview.1', '--digests', digestsFile]);
  expectCode(r, 0, 'digests');
  expectContains(r.stdout, 'a'.repeat(64), 'win-x64 digest');
  expectContains(r.stdout, 'b'.repeat(64), 'linux-x64 digest');
  expectContains(r.stdout, 'SHA-256', 'integrity table');
  // §Decision 13 declined code signing; the body has to say so rather than leave a user guessing at SmartScreen.
  expectContains(r.stdout, 'not code-signed', 'the accepted consequence is stated');
});

check('a malformed digest line warns but never crashes the last step of a release', () => {
  const bad = join(work, 'bad-digests.txt');
  writeFileSync(bad, 'this is not a digest line\n' + 'c'.repeat(64) + '  ok.zip\n');
  const r = node('release-body.mjs', ['--version', '0.1.0-preview.1', '--digests', bad]);
  expectCode(r, 0, 'malformed digest');
  expectContains(r.stdout, 'c'.repeat(64), 'the good line still lands');
  expectContains(r.stderr, '::warning::', 'the bad line is reported');
});

check('a dry-run body is unmistakably marked', () => {
  const r = node('release-body.mjs', ['--version', '0.1.0-preview.1', '--dry-run']);
  expectCode(r, 0, 'dry run');
  expectContains(r.stdout, 'DRY RUN', 'marker');
});

check('Stage B prepends changelog notes without replacing Stage A digests', () => {
  const stageABody = join(work, 'stage-a-body.md');
  const changelog = join(work, 'stage-b-changelog.md');
  writeFileSync(stageABody, '## Verifying these downloads\n\n| asset | SHA-256 |\n|---|---|\n| `a.zip` | `' + 'd'.repeat(64) + '` |\n');
  writeFileSync(changelog, '# Changelog\n\n## [0.1.0-preview.1] - 2026-08-08\n\n### Fixed\n- Release notes survive promotion.\n');
  const r = node('release-body.mjs', [
    '--version', '0.1.0-preview.1', '--changelog', changelog, '--append-to', stageABody,
  ]);
  expectCode(r, 0, 'Stage B append');
  expectContains(r.stdout, 'Release notes survive promotion.', 'prepended notes');
  expectContains(r.stdout, 'd'.repeat(64), 'preserved Stage A digest');
  if (r.stdout.indexOf('Release notes survive promotion.') > r.stdout.indexOf('## Verifying these downloads')) {
    throw new Error('Stage B notes must appear above the Stage A digest block');
  }
});

// ══ Result ═══════════════════════════════════════════════════════════════════════════════════════════════════
rmSync(work, { recursive: true, force: true });

process.stdout.write(`\n${passed} passed, ${failures.length} failed\n`);
if (failures.length > 0) {
  process.stdout.write('\nThe release pipeline\'s own assertions are not behaving. Do NOT publish.\n');
  process.exit(1);
}
process.stdout.write('Every release assertion was proven to fire on its red path as well as its green one.\n');
