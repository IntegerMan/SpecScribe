// Story 24.6 AC #3 — determinism, tested by REPETITION ACROSS PROCESSES, not by assertion.
//
// The C# probe's own `--runs 3` only proves in-process stability, which cannot see a per-process source of
// variation: string-hash randomisation, a seeded-by-startup PRNG, dictionary ordering that depends on allocation
// addresses, or tiered-JIT changing floating-point contraction between runs. Each run here is a FRESH PROCESS,
// and every emitted fixture is hashed byte-for-byte.

import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { readFileSync, readdirSync, mkdirSync, writeFileSync, rmSync, existsSync } from 'node:fs'
import { dirname, resolve, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')
const repoRoot = resolve(root, '..', '..')
const proj = join(root, 'layout', 'GraphEngineSpike.csproj')
const RUNS = Number(process.argv[2] ?? 3)

const hashes = []
for (let run = 1; run <= RUNS; run++) {
  const out = join(root, '.determinism', `run-${run}`)
  if (existsSync(out)) rmSync(out, { recursive: true, force: true })
  mkdirSync(out, { recursive: true })
  execFileSync(
    'dotnet',
    ['run', '--project', proj, '--no-build', '--', '--repo', repoRoot, '--out', out, '--runs', '1'],
    { stdio: 'pipe', cwd: repoRoot },
  )
  const perFile = {}
  for (const f of readdirSync(out).filter((f) => f.endsWith('.json')).sort()) {
    // scale.json carries wall-clock SOLVE TIMINGS, which are legitimately non-deterministic. Excluding it is a
    // stated exclusion, not a convenience: the LAYOUT fixtures are what AC #3 requires to be byte-stable.
    if (f === 'scale.json') continue
    perFile[f] = createHash('sha256').update(readFileSync(join(out, f))).digest('hex').slice(0, 16)
  }
  hashes.push({ run, perFile })
  console.log(`run ${run}: ${Object.keys(perFile).length} fixtures hashed`)
}

const files = Object.keys(hashes[0].perFile)
const report = files.map((f) => {
  const set = [...new Set(hashes.map((h) => h.perFile[f]))]
  return { fixture: f, stable: set.length === 1, distinctHashes: set.length, hash: set[0], all: set }
})
const allStable = report.every((r) => r.stable)

writeFileSync(
  join(root, 'measurements', 'determinism.json'),
  JSON.stringify({ runs: RUNS, processes: 'separate per run', excluded: ['scale.json (wall-clock timings)'],
                   allStable, report }, null, 2),
  'utf8',
)

console.log(`\nfixture                          stable  sha256/16`)
for (const r of report) {
  console.log(`${r.fixture.padEnd(32)} ${(r.stable ? 'YES' : 'NO ').padEnd(7)} ${r.hash}${r.stable ? '' : '  ALL: ' + r.all.join(' ')}`)
}
console.log(`\n${allStable ? `PASS — ${RUNS} separate processes, every fixture byte-identical.` : 'FAIL — drift detected.'}`)
process.exit(allStable ? 0 : 1)
