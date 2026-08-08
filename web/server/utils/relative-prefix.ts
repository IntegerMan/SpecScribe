/**
 * The `../`-prefix a page needs to reach the output root, computed from a ROUTE. [Story 23.5 AC #7]
 *
 * Lives in its own module rather than beside the plugin that uses it because a Nitro plugin file cannot be
 * imported outside Nitro — `defineNitroPlugin` is an auto-import that only exists in the Nitro build — so a
 * unit test could not reach the rule while it sat there. Nitro also auto-imports `server/utils/`, so the
 * plugin still gets it for free.
 *
 * ⚠️ This is the route-space sibling of `relativePrefix()` in `ir/adapter.ts`, itself the mirror of the C#
 * `PathUtil.RelativePrefix`. Three implementations of one rule is one too many, so they are pinned together
 * by `test/relative-prefix.test.ts`, which asserts these two agree on a GENERATED `.html` corpus spanning
 * depth 0–6 (plus dotted segments and the real route shapes), and which pins the one documented divergence
 * — extension-less routes — as an explicit expectation rather than leaving it as an untested assumption.
 * The adapter never sees an extension-less route (every IR route carries `.html` verbatim); this one must
 * handle them because the app's own routes (`/design-system`, `/measure/*`) do not carry the extension.
 * Note what that test can and cannot do: it pins AGREEMENT, not correctness — both could share a bug and
 * stay green. [breadth widened by the Story 23.5 code review 2026-08-08]
 *
 * The depth is that of the OUTPUT FILE, not of the route string, because the two differ:
 *
 *   `/index.html`                          → index.html                → depth 0 → ''
 *   `/epics/epic-3.html`                   → epics/epic-3.html         → depth 1 → '../'
 *   `/code/src/SpecScribe/Charts.cs.html`  → code/…/Charts.cs.html     → depth 3 → '../../../'
 *   `/`                                    → index.html                → depth 0 → ''
 *   `/design-system`                       → design-system/index.html  → depth 1 → '../'
 *   `/design-system/`                      → design-system/index.html  → depth 1 → '../'
 *
 * That second-to-last row is the trap: Nitro writes an EXTENSION-LESS route to `<route>/index.html`, so it
 * sits one directory deeper than its route string suggests.
 *
 * ⚠️ A TRAILING SLASH must be stripped before counting, and that is not cosmetic. Vue Router is non-strict
 * by default, so `GET /design-system/` and `GET /measure/async/` both resolve to a real 200 page. Counting
 * the empty final segment as a directory made the prefix one level too deep and 404'd every asset on that
 * response — the exact failure this module exists to prevent. A FRAGMENT is stripped for the same reason the
 * query string is: this takes a route STRING, and a caller that hands it one is not wrong to.
 * [Story 23.5 code review 2026-08-08]
 */
export function relativePrefixFor(routePath: string): string {
  const stripped = ((routePath.split('#')[0] ?? '').split('?')[0] ?? '')
    .replace(/^\/+/, '')
    .replace(/\/+$/, '')
  if (stripped === '') return ''
  const slashes = (stripped.match(/\//g) ?? []).length
  const depth = stripped.toLowerCase().endsWith('.html') ? slashes : slashes + 1
  return '../'.repeat(depth)
}
