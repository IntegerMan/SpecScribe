// Boot a prebuilt Nitro artefact against an IR and drive routes through it. [Story 23.6 AC #3]
//
// This is the Node-side twin of the C# driver Story 23.6 Task 3 ships (`NuxtPrerender.cs`): same transport,
// same contract, different caller. `pin-parity.mjs` and `check-parity.mjs` both need to render a corpus, and
// duplicating a server boot in each of them is how the two would silently diverge.
//
// ⚠️ Read `experiment-two-ir.mjs` before changing anything here — Story 23.5 solved these traps and recorded
// them, and every one of them cost a debugging session:
//
//   · **The artefact must be a `build:package` build.** A `npm run build` artefact carries the building
//     project's prerendered pages in `.output/public`, and NITRO SERVES `public/` AHEAD OF THE SSR ROUTE.
//     Pointed at a different IR it returns the baked project's page with HTTP 200 — a wrong answer with a
//     success status, which is the failure mode worth engineering against. Asserted below, not assumed.
//   · **`IR_DIR` resolves at MODULE SCOPE**, so one server process sees exactly one IR. Rendering two IRs
//     means two processes; this module boots one server per call and shuts it down.
//   · **Readiness is polled, never slept.** ANY HTTP response means the server is listening — including a
//     500. Waiting for a healthy status hangs for the whole timeout on a project whose entry page genuinely
//     fails, and then reports "server did not listen", which is the wrong diagnosis entirely.
//   · **The child is killed in a `finally`.** A failed render must not leak a server holding a port.
//
// Zero npm dependencies (ADR 0010).

import { spawn } from 'node:child_process'
import { existsSync } from 'node:fs'
import { join } from 'node:path'
import { walk } from './harness-lib.mjs'

/**
 * Asserts a directory really is a servable, project-INDEPENDENT artefact.
 *
 * Returns the server entry path. Throws with an actionable message otherwise — never a silent skip, because
 * a missing artefact would otherwise read as "nothing to check".
 */
export function assertPackageArtefact(outputDir) {
  const entry = join(outputDir, 'server', 'index.mjs')
  if (!existsSync(entry)) {
    throw new Error(
      `No Nitro server at ${entry}.\n` +
        `  Build the project-independent artefact first:  npm run build:package`,
    )
  }
  const publicDir = join(outputDir, 'public')
  if (existsSync(publicDir)) {
    const stray = walk(publicDir).filter((f) => f.toLowerCase().endsWith('.html'))
    if (stray.length > 0) {
      throw new Error(
        `${publicDir} carries ${stray.length} prerendered HTML page(s) — e.g. ${stray.slice(0, 3).join(', ')}.\n` +
          `  Nitro serves public/ AHEAD of the SSR route, so those would shadow the renderer and this run\n` +
          `  would measure baked output rather than rendering — a wrong answer with a 200.\n` +
          `  Rebuild the project-independent artefact:  npm run build:package`,
      )
    }
  }
  return entry
}

/**
 * Boots the artefact against `irDir`, calls `drive(fetchRoute)`, and shuts the server down.
 *
 * `fetchRoute(path)` resolves `{ status, html }`. The server is killed in a `finally`, so a throw inside
 * `drive` cannot leak the process.
 */
export async function withRenderer({ outputDir, irDir, port = 3311, bootTimeoutMs = 60_000 }, drive) {
  const entry = assertPackageArtefact(outputDir)

  const proc = spawn(process.execPath, [entry], {
    cwd: outputDir,
    env: {
      ...process.env,
      SPECSCRIBE_IR_DIR: irDir,
      // Must NOT leak into the server: it stubs the manifest empty, which is correct for BUILDING the
      // artefact and catastrophic for SERVING with it — every route would render an empty shell.
      SPECSCRIBE_PACKAGE_BUILD: '',
      PORT: String(port),
      NITRO_PORT: String(port),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  const log = []
  proc.stdout.on('data', (d) => log.push(String(d)))
  proc.stderr.on('data', (d) => log.push(String(d)))

  const base = `http://127.0.0.1:${port}`
  try {
    const deadline = Date.now() + bootTimeoutMs
    for (;;) {
      if (proc.exitCode !== null) {
        throw new Error(`the renderer exited before listening (code ${proc.exitCode}):\n${log.join('').slice(-4000)}`)
      }
      try {
        await fetch(`${base}/`, { signal: AbortSignal.timeout(2000) })
        break
      } catch {
        /* not listening yet */
      }
      if (Date.now() > deadline) {
        throw new Error(
          `the renderer did not listen within ${bootTimeoutMs} ms:\n${log.join('').slice(-4000)}`,
        )
      }
      await new Promise((r) => setTimeout(r, 150))
    }

    const fetchRoute = async (path) => {
      const res = await fetch(`${base}/${path}`, { signal: AbortSignal.timeout(60_000) })
      return { status: res.status, html: await res.text() }
    }
    return await drive(fetchRoute)
  } finally {
    proc.kill()
  }
}
