// Vendoring script: produces src/SpecScribe/assets/plotly-hierarchy.min.js — the plotly.js custom bundle that
// drives the Hierarchy Explorer (ADR 0012). NOT part of the app build; run by hand (`node build.mjs` in this
// folder) only when the pinned version changes. The produced .min.js is COMMITTED; plotly-src/ is throwaway
// (gitignored), exactly like tools/prism-vendor/node_modules.
//
// Why this is a clone and not an `npm i plotly.js` (Story 20.4 spike, Finding D): the published npm package ships
// lib/, src/, dist/ and esbuild-config.js but NOT tasks/ — and esbuild-config.js requires ./tasks/util/constants.js.
// `npm run custom-bundle` therefore cannot run from the package at all. The tag clone is the only route, which is
// also why this file cannot be shaped like tools/prism-vendor/build.js.
//
//   node build.mjs              clone (if absent) + npm i (if absent) + bundle + copy
//   node build.mjs --no-fetch   skip clone/install; reuse an existing plotly-src/ tree
//
// Rebuild the .NET project afterwards so the embedded resource picks up the new file, then re-baseline the golden
// fingerprint deliberately (a vendored-asset change is expected to move it).

import { execFileSync } from 'node:child_process'
import { copyFileSync, existsSync, mkdirSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const repoRoot = resolve(here, '..', '..')

// --- The pinned facts. A version bump invalidates every measurement in the 20.4 spike report and must be its own
// --- decision (ADR 0012 + addendum), so it lives here as one named constant rather than a floating range.
const VERSION = '3.7.0'
const REPO = 'https://github.com/plotly/plotly.js.git'
// `heatmap` rides along because the portal's calendar/heat surfaces are on the same Epic 20 rollout. The RESOLVED
// module list is larger than these three: `scatter` lives in lib/core.js and cannot be excluded from any bundle,
// and `calendars` is pulled in as a component — expect `heatmap, scatter, sunburst, treemap`.
const TRACES = 'sunburst,treemap,heatmap'
const BUNDLE_NAME = 'specscribe-hierarchy'
// The standard bundle, NOT --strict: measured at 7 bytes LARGER with a byte-identical CSP-construct profile,
// because the Function-constructor paths --strict exists to remove live in the gl/regl traces this build already
// excludes. [20.4 spike §3.2]
const EXPECTED_BYTES = 1223515

const OUT_NAME = 'plotly-hierarchy.min.js'
const OUT_DIR = join(repoRoot, 'src', 'SpecScribe', 'assets')

const src = join(here, 'plotly-src')
const noFetch = process.argv.includes('--no-fetch')

function run(cmd, args, cwd, shell = false) {
  console.log(`> ${cmd} ${args.join(' ')}`)
  execFileSync(cmd, args, { cwd, stdio: 'inherit', shell })
}

// `npm` is a .cmd shim on Windows, and since the CVE-2024-27980 mitigation Node refuses to spawn a .cmd without a
// shell (EINVAL). shell:true is therefore the only route here; every argv below is a literal constant in this file,
// never user input, so the unescaped-concatenation hazard the deprecation warns about does not apply.
const npm = (args, cwd) => run('npm', args, cwd, process.platform === 'win32')

if (!existsSync(src)) {
  if (noFetch) {
    console.error(`build: ${src} is missing and --no-fetch was passed.`)
    process.exit(1)
  }
  run('git', ['clone', '--branch', `v${VERSION}`, '--depth', '1', REPO, 'plotly-src'], here)
}

if (!noFetch && !existsSync(join(src, 'node_modules'))) {
  // --ignore-scripts: the dev tree's postinstall steps build artifacts this bundle does not consume, and skipping
  // them keeps an untrusted-code surface off the box. The custom-bundle task itself does not need them.
  npm(['i', '--ignore-scripts'], src)
}

npm(['run', 'custom-bundle', '--', '--traces', TRACES, '--out', BUNDLE_NAME], src)

const built = join(src, 'dist', `plotly-${BUNDLE_NAME}.min.js`)
if (!existsSync(built)) {
  console.error(`build: expected bundle at ${built} — custom-bundle did not produce it.`)
  process.exit(1)
}

mkdirSync(OUT_DIR, { recursive: true })
copyFileSync(built, join(OUT_DIR, OUT_NAME))

const bytes = statSync(join(OUT_DIR, OUT_NAME)).size
console.log(`\nplotly.js v${VERSION} traces=${TRACES} -> src/SpecScribe/assets/${OUT_NAME}`)
console.log(`bytes: ${bytes}`)
if (bytes !== EXPECTED_BYTES) {
  // Not fatal — a legitimate version bump moves this — but loud, because a materially different number on the
  // PINNED version means something other than the intended bundle was built.
  console.warn(`WARNING: expected ${EXPECTED_BYTES} B for v${VERSION}. Confirm the trace list and the tag before committing.`)
}
