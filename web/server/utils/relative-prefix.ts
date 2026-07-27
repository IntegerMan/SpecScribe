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
 * by `test/relative-prefix.test.ts`, which asserts these two agree across the whole `.html` route space.
 * They differ ONLY on extension-less routes, which the adapter never sees (every IR route carries `.html`
 * verbatim) and which this one must handle because the app's own routes (`/design-system`, `/measure/*`) do
 * not.
 *
 * The depth is that of the OUTPUT FILE, not of the route string, because the two differ:
 *
 *   `/index.html`                          → index.html                → depth 0 → ''
 *   `/epics/epic-3.html`                   → epics/epic-3.html         → depth 1 → '../'
 *   `/code/src/SpecScribe/Charts.cs.html`  → code/…/Charts.cs.html     → depth 3 → '../../../'
 *   `/`                                    → index.html                → depth 0 → ''
 *   `/design-system`                       → design-system/index.html  → depth 1 → '../'
 *
 * That last row is the trap: Nitro writes an EXTENSION-LESS route to `<route>/index.html`, so it sits one
 * directory deeper than its route string suggests.
 */
export function relativePrefixFor(routePath: string): string {
  const stripped = (routePath.split('?')[0] ?? '').replace(/^\/+/, '')
  if (stripped === '') return ''
  const slashes = (stripped.match(/\//g) ?? []).length
  const depth = stripped.toLowerCase().endsWith('.html') ? slashes : slashes + 1
  return '../'.repeat(depth)
}
