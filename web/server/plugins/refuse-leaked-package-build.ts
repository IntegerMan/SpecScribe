/**
 * Refuses to SERVE when `SPECSCRIBE_PACKAGE_BUILD` has leaked into the server's environment.
 * [Story 23.5 code review 2026-08-08, owner decision D4]
 *
 * ── The failure this prevents ──────────────────────────────────────────────────────────────────────────
 *
 * `ir/adapter.ts` reads the flag at module scope and, when set, returns `EMPTY_MANIFEST` — deliberately
 * schema-current so it slips past the adapter's own FATAL version check. That is exactly right during a
 * package build, where the point is to bake in no project. It is catastrophic in a serving process: with an
 * empty manifest `site.paths` is `[]`, `site.title` is `''`, and `hasPage()` is false for every route, so
 * the server boots happily and hands out an empty shell — at HTTP 200. A wrong answer with a success status
 * is the failure mode this whole flag was introduced to engineer against, and it was the one shape of it
 * left unguarded.
 *
 * The leak is not hypothetical. `build:package` sets the flag, this project's own docs instruct operators to
 * export it, CI sets it on two steps, and `vitest.config.ts` sets it for the entire test environment. Any
 * launcher that forwards `process.env` — a shell that exported it, a container inheriting a build stage —
 * carries it into `node .output/server/index.mjs`. `src/SpecScribe/NuxtPrerender.cs` already blanks it for
 * the one caller it controls, calling the leak "catastrophic for SERVING"; that guard cannot help any other
 * caller, which is why this one lives in the artefact itself.
 *
 * ── Why the env var and not the adapter's `PACKAGE_BUILD` constant ─────────────────────────────────────
 *
 * They are the same read, but going through `process.env` here keeps this file independent of the adapter's
 * module graph, so the guard cannot be defeated by an import-order change. The flag provably survives into
 * the bundle as a RUNTIME read rather than being inlined: the artefact Story 23.5 built with the flag set
 * went on to render 1,056 real routes from a real IR, which is only possible if `loadManifest()` re-read the
 * environment at run time.
 *
 * ── Why `import.meta.prerender` is excluded ────────────────────────────────────────────────────────────
 *
 * Nitro boots this same server to prerender during a build. Under a package build the route table is empty
 * so nothing is prerendered, but the guard must not be able to break the build it is shipped by, so the
 * prerender phase is exempt explicitly rather than by luck.
 */
export default defineNitroPlugin(() => {
  if (process.env.SPECSCRIBE_PACKAGE_BUILD !== '1') return
  if (import.meta.prerender) return

  throw new Error(
    'Refusing to start: SPECSCRIBE_PACKAGE_BUILD=1 is set in this server process.\n' +
      '  That flag stubs the IR manifest EMPTY. Serving with it set would answer every route with an\n' +
      '  empty page at HTTP 200 — a wrong answer with a success status — instead of rendering the IR at\n' +
      '  SPECSCRIBE_IR_DIR.\n' +
      '  It is a BUILD-time flag only. Unset it before starting the server; `build:package` sets it for\n' +
      '  the build alone and it must not survive into the serving environment.',
  )
})
