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
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const here = dirname(fileURLToPath(import.meta.url))

// Static assets still come from the C# source of truth — the artefact ships them, so they must be current.
const sync = spawnSync(process.execPath, [join(here, 'sync-runtime-assets.mjs')], {
  cwd: join(here, '..'),
  stdio: 'inherit',
})
if (sync.status !== 0) process.exit(sync.status ?? 1)

// `shell: true` so the `nuxt` bin resolves through npm's PATH shim on Windows as well as POSIX.
const build = spawnSync('nuxt', ['build'], {
  cwd: join(here, '..'),
  stdio: 'inherit',
  shell: true,
  env: { ...process.env, SPECSCRIBE_PACKAGE_BUILD: '1' },
})
process.exit(build.status ?? 1)
