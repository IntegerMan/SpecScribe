#!/usr/bin/env node
// ADR 0040 §Decision 9 — decide whether a commit is green, from check-run JSON. [Story 16.4 AC #1, Task 2]
//
// PURE: reads JSON on stdin, talks to nothing. `require-green-gate.sh` is the half that calls the network and
// polls; this is the half that DECIDES, split out precisely so the decision can be driven from fixtures. An
// assertion nobody has watched go red is an assertion that has not been tested — see `selftest.mjs`.
//
// ⚠️ WHY CHECK-RUNS AND NOT WORKFLOW RUNS. `portability-probe` carries job-level `continue-on-error`, so a
// workflow RUN can conclude `success` while a job inside it is red. docs/CiGate.md documents that trap and
// works around it with a second `.../runs/<id>/jobs` call. The check-runs API is already per-job, and ADR 0040
// §Decision 9 fixes the required string as the job name verbatim — `build-test-analyze` — so filtering this
// response by name cannot express the trap at all. One call, and the wrong answer is unreachable rather than
// merely avoided.
//
// Node rather than jq: jq is present on GitHub runners but not on this project's development machine, and an
// assertion that can only be exercised in CI is one nobody will exercise before pushing. `tools/analysis-digest`
// already sets the .mjs precedent, and the release workflow sets Node up anyway.
//
// stdin  : the `gh api repos/{owner}/{repo}/commits/{sha}/check-runs` response, or a bare array of check runs.
// argv[2]: the check name to require.
// stdout : a human-readable verdict block.
// exit   : 0 = green · 75 = still running (EX_TEMPFAIL; the caller polls) · 1 = red, stop the release.

const EXIT_PASS = 0;
const EXIT_FAIL = 1;
const EXIT_PENDING = 75;

const checkName = process.argv[2];
if (!checkName) {
  process.stderr.write('usage: gate-verdict.mjs <check-name>   (check-run JSON on stdin)\n');
  process.exit(EXIT_FAIL);
}

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) chunks.push(chunk);
  return Buffer.concat(chunks).toString('utf8');
}

const raw = (await readStdin()).trim();

let parsed;
try {
  parsed = JSON.parse(raw === '' ? 'null' : raw);
} catch (err) {
  // Fail closed and LOUDLY. An unparseable gate response must never read as "no runs found" (which is also a
  // failure, but a differently-actionable one) and must certainly never read as green.
  process.stdout.write(`FAIL — the check-run response could not be parsed as JSON: ${err.message}\n`);
  process.exit(EXIT_FAIL);
}

// Accept the API envelope or a bare array, so a fixture does not have to mimic the wrapper.
const all = Array.isArray(parsed) ? parsed : Array.isArray(parsed?.check_runs) ? parsed.check_runs : [];
const runs = all.filter((r) => r && r.name === checkName);

if (runs.length === 0) {
  // ADR 0040 §Decision 9 names this branch and dictates its message: it is the one the Story 16.1 code review
  // found had no defined action. `build-test-analyze.yml:20-23` triggers on push/pull_request to `main` ONLY,
  // so a tag on any other ref has no run to point at. That is answered by SCOPE — the preview is forward-fix
  // only and every tag is cut from `main` — never by relaxing the gate.
  process.stdout.write(
    `FAIL — no '${checkName}' check run exists for this commit.\n` +
      "       Tag a commit that has been merged to 'main'; only 'main' is built by build-test-analyze.\n",
  );
  process.exit(EXIT_FAIL);
}

const pending = runs.filter((r) => r.status !== 'completed');
if (pending.length > 0) {
  // A tag pushed straight after a merge RACES the gate, so waiting is the normal path rather than a courtesy.
  process.stdout.write(
    `PENDING — ${pending.length} of ${runs.length} '${checkName}' check run(s) are queued or in progress.\n`,
  );
  process.exit(EXIT_PENDING);
}

// "The MOST RECENT completed run for that SHA is authoritative — a re-run that went red supersedes an earlier
// green, never the reverse" (ADR 0040 §Decision 9). completed_at orders them; id breaks the tie when two runs
// report the same second, so the rule stays total rather than depending on the API's array order.
const latest = runs
  .slice()
  .sort((a, b) => {
    const at = String(a.completed_at ?? '');
    const bt = String(b.completed_at ?? '');
    if (at !== bt) return at < bt ? -1 : 1;
    return Number(a.id ?? 0) - Number(b.id ?? 0);
  })
  .at(-1);

const conclusion = latest.conclusion ?? 'unknown';
const url = latest.html_url ?? '(no url)';

if (conclusion === 'success') {
  process.stdout.write(
    `PASS — the latest completed '${checkName}' for this commit concluded 'success'.\n       ${url}\n`,
  );
  process.exit(EXIT_PASS);
}

process.stdout.write(
  `FAIL — the latest completed '${checkName}' for this commit concluded '${conclusion}'.\n` +
    `       ${url}\n` +
    '       Publishing is gated on a passing build+test run (NFR9). Fix it on `main` and cut a NEW tag —\n' +
    '       this version number is not re-runnable (ADR 0040 §Decision 10).\n',
);
process.exit(EXIT_FAIL);
