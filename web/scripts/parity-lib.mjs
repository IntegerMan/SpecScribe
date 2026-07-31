// Pure decision logic for the pinned content-drift oracle. [Story 23.6 AC #3]
//
// `pin-parity.mjs` WRITES the oracle and `check-parity.mjs` READS it back. Both are I/O drivers — they boot a
// Nitro artefact and walk a generated site — so neither is a unit-test subject. The part that decides whether
// a page has DRIFTED is pure and lives here, the same split `harness-lib.mjs`, `tokens-lib.mjs` and
// `ir-content-lib.mjs` already use.
//
// ── Why the corpus is PINNED, and why the oracle carries TWO digests ────────────────────────────────────
//
// Owner decision, 2026-07-31, taken during Story 23.6 after the shape D2 assumed was measured and found
// vacuous. Two findings drove it, both verified against the committed `measurements/parity.json`:
//
// 1. **`goldenSha === irSha === nuxtSha` on all 1,469 rows.** So "does the rendered page still hash to the
//    committed golden value?" and "does the rendered page match the IR it was rendered from?" are the SAME
//    question. A gate reading the committed value back over the LIVE site therefore asserts nothing the live
//    run does not already assert — it would carry the authority of a check while performing none, which is
//    the failure ADR 0033 § Context names.
//
//    This is structural, not an oversight: the IR *is* the renderer's input and the region passes through
//    VERBATIM by contract, so input-digest and output-digest are one quantity. Over a corpus that changes,
//    they cannot be separated, and "the content moved" is indistinguishable from "the renderer moved".
//
//    ⇒ The corpus is PINNED. A committed IR subset under `web/fixtures/parity-corpus/` is the renderer's
//      input, frozen. Its content cannot change, so ANY digest move is a RENDERING change, by construction.
//      A sibling story editing a doc can never turn this gate red (ADR 0033 §Decision 2).
//
// 2. **The old oracle hashes `<main>` only — it is blind to the chrome this story deletes.** `mainRegion()`
//    slices the landmark, so `<title>`, `<meta name="description">`, the favicon, the footer, `<script src>`
//    tags, the nav toggle, the Mermaid init and the Hierarchy/Graph anti-flash handshakes are all OUTSIDE
//    every digest ever recorded. Those are precisely what `HtmlRenderAdapter.Render` emitted and what Task 6
//    deletes — the highest-risk surface in the story had no gate at all.
//
//    ⇒ The oracle records TWO digests per route:
//        · `mainSha`  — the normalized `<main>` region. This is the C# LINEAGE: Story 23.4 proved the
//                       composed region byte-equal to C#'s own render across 1,469 pages, so this value is
//                       what C# produced, and it must survive the writer's deletion unchanged.
//        · `pageSha`  — the normalized WHOLE PAGE. Pinned from the RENDERER, not from C#: the two agree on
//                       `<main>` (proven) but were never claimed to agree on chrome, so pinning a C# whole-
//                       page digest here would record a difference that has always existed and call it drift.
//                       This is a renderer snapshot, and it guards the chrome from this point forward.
//
// Zero npm dependencies (ADR 0010).

import { createHash } from 'node:crypto'

/**
 * The oracle's digest: a 16-char sha256 slice over the NORMALIZED text.
 *
 * A byte LENGTH would not do — the failure this has to survive is a rewrite that preserves length while
 * changing content, which is exactly what a markup or escaping change looks like. Keeping the definition here
 * rather than in each driver is load-bearing: if the writer and the reader ever hashed differently, EVERY
 * route would read as drifted and the oracle would be regenerated to make it green, turning a real check into
 * a ritual.
 */
export function parityDigest(s) {
  return createHash('sha256').update(s, 'utf8').digest('hex').slice(0, 16)
}

/** The IR's own `<main>` region, rebuilt from the split — `<main …>` + body + `</main>`. */
export function composeIrMain(region, normalize) {
  return normalize(`<main id="main-content"${region.mainAttributes}>${region.mainInnerHtml}</main>`)
}

/**
 * Folds BUILD-derived asset digests out of a whole-page string, on top of `normalizeVolatile`.
 *
 * ⚠️ Only needed for `pageSha`, never for `mainSha`: the `<main>` region carries no `_nuxt/` reference at all.
 *
 * Nuxt content-hashes its emitted chunks — `_nuxt/PageShell.Ys9LGDmo.css`, `_nuxt/entry.DRLacYXT.css`. Those
 * digests are a property of the BUILD, not of the page, and they are exactly the same class of token
 * `normalizeVolatile` already folds for `?v=<ModuleVersionId>` and `SpecScribe v<VERSION>`. Leaving them in
 * would make the gate report CHROME DRIFT on every route whenever the artefact is rebuilt on a different
 * machine — a failure unrelated to the change under test, which is precisely what ADR 0033 §Decision 2
 * forbids, and which would have been discovered on CI-Ubuntu rather than here.
 *
 * The fold is deliberately narrow. The asset's DIRECTORY, STEM and EXTENSION all survive, so the gate still
 * catches a chunk being renamed, added, dropped, or moved between `<link>` and `<script>` — only the opaque
 * digest is neutralized. `[A-Za-z0-9_-]{8}` matches Vite/Rolldown's fixed-width base64url hash and will not
 * swallow a multi-segment stem.
 */
export function foldBuildAssets(s) {
  return s.replace(/(_nuxt\/[^"'\s/]+?)\.[A-Za-z0-9_-]{8}\.(css|js|mjs)/g, '$1.<HASH>.$2')
}

/** Thrown when the oracle cannot be trusted at all. Loud, never a silent skip (ADR 0033 §Decision 5). */
export class ParityOracleError extends Error {}

/**
 * Loudness gate A — the oracle itself, before any result is trusted.
 *
 * Returns `{ routes, families }`. Throws `ParityOracleError` when the basis is absent, unparsed, empty, or
 * carries a route without both digests. An empty basis reports "no drift" while measuring nothing, which is
 * the failure ADR 0033 §Decision 5 forbids — and the one the OLD `measure:parity` falls into the moment the
 * C# writer is deleted (its `goldenRoot` empties, every row takes the `NO GOLDEN` branch, and it exits 0).
 */
export function validateOracle(oracle, source = 'the committed oracle') {
  if (oracle === null || typeof oracle !== 'object') {
    throw new ParityOracleError(`${source} is not an object — it carries no comparison basis at all.`)
  }
  if (!Array.isArray(oracle.routes)) {
    throw new ParityOracleError(`${source} has no \`routes\` array — it carries no comparison basis at all.`)
  }
  if (oracle.routes.length === 0) {
    throw new ParityOracleError(
      `${source} carries ZERO routes. An empty basis reports "no drift" while measuring nothing.`,
    )
  }
  for (const r of oracle.routes) {
    if (!r || !r.path || !r.mainSha || !r.pageSha) {
      throw new ParityOracleError(
        `${source} route "${r?.path ?? '(unnamed)'}" is missing path/mainSha/pageSha. A route without both ` +
          `digests cannot be checked and must not be silently skipped.`,
      )
    }
  }
  const families = [...new Set(oracle.routes.map((r) => r.family).filter(Boolean))].sort()
  return { routes: oracle.routes, families }
}

/**
 * The verdict for one pinned route against its live render.
 *
 * `live` is either `{ unmeasurable: '<why>' }` or `{ mainSha, pageSha }`.
 *
 * `mainSha` is checked FIRST and reported alone when it moves: a region change also moves the whole-page
 * digest, and reporting both would name the same defect twice and bury which layer produced it.
 */
export function classifyRoute(route, live) {
  const base = { path: route.path, family: route.family }
  if (live?.unmeasurable) return { ...base, kind: 'unmeasurable', why: live.unmeasurable }

  if (live.mainSha !== route.mainSha) {
    return { ...base, kind: 'main-drift', expected: route.mainSha, actual: live.mainSha }
  }
  if (live.pageSha !== route.pageSha) {
    return { ...base, kind: 'chrome-drift', expected: route.pageSha, actual: live.pageSha }
  }
  return { ...base, kind: 'ok' }
}

/**
 * Loudness gates B and C, plus the counts the report prints.
 *
 * B — every pinned route must be MEASURABLE. A route that cannot be rendered is a hard failure, never a
 *     quietly smaller basis. This is the assertion `RegionCompositionCorpusProof` makes about the deep-git
 *     surfaces before trusting a delta count, which ADR 0033 §Decision 5 names as the reference.
 * C — every family the oracle claims to cover must still have at least one measured route. A corpus that has
 *     silently lost a whole family still reports "0 drift" for the ones that remain; that is the partial-run
 *     failure mode wearing a green tick.
 */
export function assessRun(verdicts, expectedFamilies = []) {
  const by = (kind) => verdicts.filter((v) => v.kind === kind)
  const unmeasurable = by('unmeasurable')
  const mainDrift = by('main-drift')
  const chromeDrift = by('chrome-drift')
  const measured = verdicts.length - unmeasurable.length

  const covered = new Set(verdicts.filter((v) => v.kind !== 'unmeasurable').map((v) => v.family))
  const missingFamilies = expectedFamilies.filter((f) => !covered.has(f))

  return {
    pinned: verdicts.length,
    measured,
    unmeasurable,
    mainDrift,
    chromeDrift,
    missingFamilies,
    ok:
      verdicts.length > 0 &&
      unmeasurable.length === 0 &&
      mainDrift.length === 0 &&
      chromeDrift.length === 0 &&
      missingFamilies.length === 0,
  }
}
