#!/usr/bin/env node
// `npm run build:package` — produce the project-INDEPENDENT renderer artefact. [Story 23.5 AC #1/#4/#5]
//
// The difference from `npm run build` is one environment variable, and it is the whole packaging decision:
//
//   npm run build            binds the artefact to ONE project — `nuxt.config.ts` reads that project's IR
//                            manifest at config-load time, bakes its 1,056 routes into
//                            `nitro.prerender.routes`, and `nuxt build` prerenders them into
//                            `.output/public`. Requires an IR to exist. This is the DEVELOPMENT/parity
//                            path, and every gate in `web/` measures against it.
//
//   npm run build:package    binds the artefact to NO project. `SPECSCRIBE_PACKAGE_BUILD=1` stubs the
//                            manifest empty (`ir/adapter.ts`), so the route table is empty, nothing is
//                            prerendered, and NO IR NEEDS TO EXIST. The result renders any project's IR at
//                            server runtime from `SPECSCRIBE_IR_DIR`.
//
// Why the second one has to exist at all — both reasons were measured, not assumed:
//
//   1. Without it the artefact cannot be built without an IR, so a release pipeline would have to generate
//      somebody's portal first just to produce a project-independent renderer.
//   2. Nitro serves `public/` static files AHEAD of the SSR route. An artefact carrying project A's baked
//      `/index.html` returned A's dashboard when pointed at project B — HTTP 200, wrong project. A wrong
//      answer with a success status is worse than a failure, and `scripts/experiment-two-ir.mjs` now
//      refuses to run against an artefact that still contains prerendered pages.
//
// Set SPECSCRIBE_IR_DIR here if you want; it is deliberately IGNORED for the route table and honoured only
// at server runtime.

import { spawnSync } from 'node:child_process'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const here = dirname(fileURLToPath(import.meta.url))

// The token drift gate. npm's lifecycle prefix matches the script NAME, so `prebuild` fires for
// `npm run build` and NOT for `npm run build:package` — meaning the one build that produces the artefact
// users actually get was the one build with no token check. CI still covers it via `npm run check`, so this
// closes a local/packaging gap rather than an unguarded pipeline, but the artefact is the wrong thing to
// leave to CI. Run it first: a drifted token is cheaper to catch before a full prerender than after.
// [Story 23.2 review 2026-08-07]
/**
 * Exit on a failed step, saying WHY.
 *
 * Branching on `.status` alone loses the whole spawn-failure class: when the spawn itself fails (`ENOENT` —
 * e.g. this script run as `node scripts/build-package.mjs` rather than through `npm run`, so
 * `node_modules/.bin` is off PATH) `status` is `null`, `error` is populated and never read, and
 * `stdio: 'inherit'` prints nothing. The process exited 1 with no output whatsoever, which is indis-
 * tinguishable from a gate that ran and failed. [Story 23.5 code review 2026-08-08]
 */
function exitIfFailed(step, result) {
  if (result.error) {
    console.error(`\n✗ ${step} could not be started: ${result.error.message}`)
    if (result.error.code === 'ENOENT') {
      console.error(`  The executable was not found. Run this through \`npm run build:package\` so npm puts`)
      console.error(`  node_modules/.bin on PATH, or run \`npm ci\` first.`)
    }
    process.exit(1)
  }
  if (result.status !== 0) {
    console.error(`\n✗ ${step} failed (exit ${result.status ?? 'signal ' + result.signal}).`)
    process.exit(result.status ?? 1)
  }
}

const tokens = spawnSync(process.execPath, [join(here, 'check-tokens.mjs')], {
  cwd: join(here, '..'),
  stdio: 'inherit',
})
exitIfFailed('check-tokens', tokens)

// Static assets still come from the C# source of truth — the artefact ships them, so they must be current.
const sync = spawnSync(process.execPath, [join(here, 'sync-runtime-assets.mjs')], {
  cwd: join(here, '..'),
  stdio: 'inherit',
})
exitIfFailed('sync-runtime-assets', sync)

// Resolve nuxt's own entry point and run it under THIS node, rather than spawning the bare name `nuxt`
// through a shell. [Story 17.2 Task 2, javascript:S4036]
//
// The previous form was `spawnSync('nuxt', ['build'], { shell: true })`, which asked the shell to find `nuxt`
// on PATH — the surface Sonar flags ("make sure the PATH variable only contains fixed, unwriteable
// directories"), and the same class of defect the C# side had at `GitMetrics`/`NuxtPrerender`. Resolving the
// module removes the PATH search AND the shell from the path entirely; `process.execPath` is already the
// trusted interpreter the two spawns above use.
//
// Resolved via nuxt's OWN `bin` field rather than a hard-coded `node_modules/nuxt/bin/nuxt.mjs`, so it keeps
// working if nuxt relocates the file.
//
// ⚠️ `require.resolve('nuxt/bin/nuxt.mjs')` does NOT work and was tried first: `require.resolve` honours the
// package's `exports` map, and nuxt does not export its bin path — it fails with ERR_PACKAGE_PATH_NOT_EXPORTED.
// `package.json` IS exported, so resolving that and reading `bin.nuxt` relative to its directory is the form
// that respects both the `exports` map and nuxt's own declaration of where its entry point lives.
const require_ = createRequire(import.meta.url)
const nuxtPkgPath = require_.resolve('nuxt/package.json')
const nuxtBin = join(dirname(nuxtPkgPath), require_(nuxtPkgPath).bin.nuxt)
const build = spawnSync(process.execPath, [nuxtBin, 'build'], {
  cwd: join(here, '..'),
  stdio: 'inherit',
  env: { ...process.env, SPECSCRIBE_PACKAGE_BUILD: '1' },
})
exitIfFailed('nuxt build', build)
