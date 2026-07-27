/**
 * Rewrites Nuxt's root-absolute `/_nuxt/…` asset URLs to page-relative ones. [Story 23.5 AC #7]
 *
 * ── Why this is required, not a preference ─────────────────────────────────────────────────────────────
 *
 * Nuxt emits `<link rel="stylesheet" href="/_nuxt/entry.<hash>.css">` because `app.baseURL` is unset. A
 * leading slash means "the server root", and the SpecScribe portal is not served from a server root — it is
 * a RELATIVE FILE TREE. ADR 0012 §Decision 1 requires the generated portal to keep working offline and from
 * `file://`; EXPERIENCE.md:270 has the owner copying the output folder to a USB drive for an offline demo.
 * From `file://`, `/_nuxt/entry.css` resolves to the FILESYSTEM ROOT (`file:///_nuxt/…`) and 404s, so every
 * page loses its stylesheet. The same breakage occurs whenever the portal is served from a subdirectory
 * (GitHub Pages project sites, any reverse-proxied sub-path).
 *
 * The C# portal has always got this right: `PathUtil.RenderHeadOpen` emits depth-aware relative hrefs
 * (`../specscribe.js`, `../../specscribe.css`), which is why those survive the trip untouched and only the
 * Nuxt-injected ones were broken. This plugin makes the Nuxt half match the convention the portal already
 * has, rather than teaching the portal a new one.
 *
 * ── Why not `app.baseURL: './'` ────────────────────────────────────────────────────────────────────────
 *
 * Because it is wrong for every page that is not at the root. `baseURL` is a single global string, but the
 * correct prefix DEPENDS ON THE PAGE'S DEPTH: `./_nuxt/…` resolves against the page's own directory, so on
 * `epics/epic-3.html` it asks for `epics/_nuxt/…`, and on `code/src/SpecScribe/Charts.cs.html` it asks for
 * `code/src/SpecScribe/_nuxt/…`. Both 404. A per-page prefix cannot be expressed as a baseURL, which is why
 * this is a render-time rewrite rather than a config line.
 *
 * ── Scope ─────────────────────────────────────────────────────────────────────────────────────────────
 *
 * Only `/_nuxt/` is rewritten, and only in `href="…"`/`src="…"` attribute position. Everything else in the
 * page is the IR's own markup, which already carries correct relative hrefs and must not be touched — the
 * whole reason routes mirror IR paths verbatim is that no IR href is ever rewritten (Story 23.3).
 *
 * The depth rule itself lives in `server/utils/relative-prefix.ts` so it can be unit-tested; a Nitro plugin
 * module cannot be imported outside Nitro.
 */
export default defineNitroPlugin((nitroApp) => {
  nitroApp.hooks.hook('render:html', (html, { event }) => {
    const prefix = relativePrefixFor(event.path)
    // Depth 0 still needs the rewrite: `/_nuxt/…` must become `_nuxt/…`, or the leading slash survives and
    // the root page breaks from `file://` exactly like the nested ones.
    const rewrite = (s: string) => s.replace(/(\s(?:href|src)=")\/_nuxt\//g, `$1${prefix}_nuxt/`)

    for (const key of ['head', 'bodyPrepend', 'body', 'bodyAppend'] as const) {
      const part = html[key]
      if (Array.isArray(part)) {
        for (let i = 0; i < part.length; i += 1) part[i] = rewrite(part[i]!)
      }
    }
  })
})
