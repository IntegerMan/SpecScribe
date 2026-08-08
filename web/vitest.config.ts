/**
 * Vitest for `web/`. [Story 23.5 AC #8]
 *
 * Stood up by Story 23.5 because the packaging decision put `web/` into CI, and a Sonar scan that pulls in
 * ~1,800 lines of first-party `.vue`/`.mjs`/`.ts` at 0% coverage would red the quality gate Story 25.1 had
 * just turned green. `sonar.javascript.lcov.reportPaths` in `.github/workflows/build-test-analyze.yml`
 * points at the `lcov` reporter's output below, so the two must stay in step.
 *
 * ⚠️ `SPECSCRIBE_PACKAGE_BUILD=1` is set for the whole run, and it is load-bearing for testability, not a
 * convenience. `ir/adapter.ts` reads the IR manifest at MODULE SCOPE, so merely importing it to test a pure
 * function like `splitContentRegion` would otherwise require a generated portal to exist on disk — making
 * the unit tests depend on a 40-second `dotnet run … generate --spa`. The package-build flag (added by this
 * same story for the packaging artefact) stubs the manifest empty, so the module imports cleanly with no IR
 * present. Any test that needs real IR DATA must therefore build its own fixture rather than read one.
 */
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

process.env.SPECSCRIBE_PACKAGE_BUILD = '1'

export default defineConfig({
  // Required for the `.vue` files in `coverage.include` below to be TRANSFORMED at all. Without it v8 has
  // nothing to instrument and every component is silently dropped from the report rather than reported at
  // 0% — which is the shape of the defect owner decision D3 exists to close, so leaving the plugin out
  // would reintroduce it by another route. [Story 23.5 code review 2026-08-08]
  plugins: [vue()],
  test: {
    env: { SPECSCRIBE_PACKAGE_BUILD: '1' },
    include: ['test/**/*.test.{ts,mjs}'],
    coverage: {
      provider: 'v8',
      // `lcov` is what Sonar consumes; `text` keeps a local run readable without opening a file.
      reporter: ['text', 'lcov'],
      reportsDirectory: 'coverage',
      // ⚠️ `.vue`, not `.ts`, for components. This used to read `components/**/*.ts` — components are `.vue`
      // files, so that glob matched NOTHING and no component ever reached `lcov.info`. Sonar independently
      // excluded `web/**/*.vue`, so the "honest gap" both configs claimed to document was in neither
      // denominator: it contributed zero rather than 0%, adding component tests would have LOWERED the
      // reported number, and dropping the Sonar exclusion later would have revealed a cliff nobody
      // predicted. Both sides now count `.vue`. [Story 23.5 code review 2026-08-08, owner decision D3]
      include: [
        '../.github/scripts/release/next-preview-tag.mjs',
        'ir/**/*.ts',
        'scripts/*.mjs',
        'server/**/*.ts',
        'components/**/*.{ts,vue}',
        'pages/**/*.vue',
        'layouts/**/*.vue',
        'app.vue',
      ],
      // Harnesses that exist to DRIVE a built site rather than to be unit-tested: they spawn servers, walk
      // `.output/`, and assert on a full generate. Excluded from the coverage denominator deliberately and
      // named here so the exclusion is a decision on the record rather than a silent gap.
      exclude: [
        'scripts/check-links.mjs',
        'scripts/check-a11y.mjs',
        'scripts/measure-parity.mjs',
        'scripts/measure-payload.mjs',
        // Story 23.6's drift-gate drivers: they boot a Nitro artefact and drive routes through it. The pure
        // decision logic they share lives in `scripts/parity-lib.mjs`, which is deliberately NOT excluded and
        // is covered by `test/parity-lib.test.mjs`.
        'scripts/check-parity.mjs',
        'scripts/pin-parity.mjs',
        'scripts/render-lib.mjs',
        'scripts/experiment-two-ir.mjs',
        'scripts/build-package.mjs',
        'scripts/sync-runtime-assets.mjs',
        'scripts/extract-tokens.mjs',
        'scripts/extract-ir-content.mjs',
        'scripts/check-tokens.mjs',
        'scripts/check-ir-content.mjs',
        'scripts/ir-content-build.mjs',
      ],
    },
  },
})
