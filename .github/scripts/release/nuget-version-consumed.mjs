#!/usr/bin/env node
// ADR 0040 §Decision 10 — has this version already been consumed on nuget.org? [Story 16.4 AC #2, Task 9]
//
// PURE: reads the flat-container index on stdin, talks to nothing. `assert-version-unpublished.sh` fetches.
//
// WHY THIS RUNS BEFORE ANYTHING IS BUILT. A version number is consumed on first publish to any channel and is
// never reused: nuget.org REJECTS a duplicate version and permits only unlisting, never deletion. So a re-run
// of an already-published tag cannot succeed — the only question is whether it fails in five seconds or after
// building three ~76 MiB RIDs and a Nuxt artefact, at the push step, with a GitHub Release already drafted.
// ADR 0040 §Decision 10 calls this being "idempotent by refusal", and it is the mechanism that makes AC #2's
// reworded second clause — safe to re-run ON A NEW TAG — true rather than aspirational.
//
// Recovery is FORWARD: bump `-preview.N` and cut a new tag. Per-channel resume is rejected by the ADR because
// it would require distinguishing "this version is on this channel because I put it there" from "…because
// someone else did", across three registries with three different conflict semantics.
//
// stdin  : the https://api.nuget.org/v3-flatcontainer/<id>/index.json body, or empty for a 404 (never published).
// argv[2]: the version to check.
// stdout : a human-readable verdict.
// exit   : 0 = not consumed, safe to publish · 1 = consumed, cut preview.N+1.

const EXIT_FREE = 0;
const EXIT_CONSUMED = 1;

const wanted = process.argv[2];
if (!wanted) {
  process.stderr.write('usage: nuget-version-consumed.mjs <version>   (index.json on stdin, empty for 404)\n');
  process.exit(EXIT_CONSUMED);
}

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) chunks.push(chunk);
  return Buffer.concat(chunks).toString('utf8');
}

const raw = (await readStdin()).trim();

// An empty body is the 404 case: nuget.org has no registration for this package id at all, so no version of it
// is consumed. That is the state this repository is in today — `SpecScribe` was verified unclaimed on
// 2026-08-07 (ADR 0040 §Decision 12) and reserving it is an owner action.
if (raw === '') {
  process.stdout.write(`nuget preflight OK — the package has no published versions; '${wanted}' is free.\n`);
  process.exit(EXIT_FREE);
}

let versions;
try {
  const parsed = JSON.parse(raw);
  versions = Array.isArray(parsed?.versions) ? parsed.versions : null;
} catch {
  versions = null;
}

if (versions === null) {
  // Fail CLOSED. An unreadable index must not read as "free" — that would hand a 409 to the push step after
  // everything has been built and the GitHub Release drafted, which is the exact ordering this check exists
  // to prevent.
  process.stdout.write(
    'FAIL — the nuget.org flat-container index could not be read as {"versions":[…]}.\n' +
      '       Treating that as "version is free" would defer the conflict to the push step, after the build\n' +
      '       and after the draft Release exists. Failing here instead. Re-run once nuget.org responds.\n',
  );
  process.exit(EXIT_CONSUMED);
}

// nuget.org normalises SemVer for comparison: version equality is case-insensitive, and build metadata (+sha)
// is not part of identity. Comparing the raw strings would let `0.1.0-Preview.1` past a `0.1.0-preview.1` that
// is already published, and the push would then 409 after everything was built.
const normalise = (v) => String(v).trim().toLowerCase().split('+')[0];
const target = normalise(wanted);
const hit = versions.find((v) => normalise(v) === target);

if (hit === undefined) {
  process.stdout.write(
    `nuget preflight OK — '${wanted}' is not among the ${versions.length} published version(s).\n`,
  );
  process.exit(EXIT_FREE);
}

process.stdout.write(
  `FAIL — version '${hit}' is ALREADY PUBLISHED on nuget.org, so this version number is consumed.\n` +
    '\n' +
    '       nuget.org rejects a duplicate version and permits only unlisting, never deletion, so this tag\n' +
    '       can never be published again. Recovery is FORWARD (ADR 0040 §Decision 10):\n' +
    '\n' +
    '         1. bump the pre-release counter — the next tag is -preview.N+1, not a retry of this one\n' +
    '         2. push the new tag; this pipeline runs again from a clean checkout\n' +
    '\n' +
    '       If this release was bad, withdraw it by UNLISTING on nuget.org (never delete — deletion breaks\n' +
    '       restore for anyone who already resolved it) and deleting its GitHub Release. See docs/Releasing.md.\n',
);
process.exit(EXIT_CONSUMED);
