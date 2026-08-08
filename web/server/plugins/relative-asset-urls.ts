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
 * ── Scope: `body` is DELIBERATELY not rewritten ────────────────────────────────────────────────────────
 *
 * Only `/_nuxt/` is rewritten, and only in `head`, `bodyPrepend` and `bodyAppend` — the three regions Nuxt
 * injects asset tags into. `html.body` is the rendered app, and on an IR route that is the IR's own markup
 * spliced in through `v-html`. It already carries correct relative hrefs and must not be touched: the whole
 * reason routes mirror IR paths verbatim is that no IR href is ever rewritten (Story 23.3).
 *
 * This scoping used to be a CLAIM in this comment while the loop covered `body` too, which made it a
 * mention-vs-mechanism bug of the same family the codebase has now hit four times (`data-hierarchy`,
 * `_payload.json`, `data-relgraph`, and this). It did not fire only because `PathUtil.Html` is
 * `WebUtility.HtmlEncode`, so a `"` in rendered source arrives as `&quot;` and misses the pattern — an
 * accidental guard owned by someone else's escaper. This portal renders its OWN source as code pages, and
 * line 6 above is the single place in the repository where the literal ` href="/_nuxt/` occurs, so the page
 * that would have been corrupted first is this file's own. A raw-HTML block passed through by the markdown
 * renderer is not escaped and would have been corrupted for real. [Story 23.5 code review 2026-08-08]
 *
 * Verified before narrowing: no component, page or layout references a bundled asset (no `<img>`, no
 * `srcset`, no `~/assets` import anywhere under `web/`), so nothing legitimate lives in `body`. The
 * `assertNoStrandedRefs` guard below exists so that the day one appears, it is loud instead of a silent 404.
 *
 * ── Known limit: a bundled stylesheet's own `url()` refs are out of reach ──────────────────────────────
 *
 * Vite emits `url(/_nuxt/…)` for fonts and background images INSIDE the generated `.css` files, and a `.css`
 * file never passes through `render:html`. So AC #7's `file://` guarantee holds for markup, and for CSS only
 * so long as no bundled stylesheet references a bundled asset. Today none does (the only `url()` in
 * `web/assets/*.css` is a `data:` URI). This cannot be fixed in this hook — it needs a post-build pass over
 * `.output/public/_nuxt/*.css` — and it is recorded here rather than left to be rediscovered.
 *
 * The depth rule itself lives in `server/utils/relative-prefix.ts` so it can be unit-tested; a Nitro plugin
 * module cannot be imported outside Nitro.
 */

/** Attribute position, both quote styles. `content` covers `<meta property="og:image">`. */
const ASSET_ATTR = /((?:href|src|srcset|imagesrcset|poster|content)\s*=\s*["'])\/_nuxt\//gi
/** Second and later entries of a `srcset` list, which the attribute pattern above cannot reach. */
const SRCSET_ENTRY = /(,\s*)\/_nuxt\//g
/** `url(/_nuxt/…)` inside an inline `<style>`; reachable only if `features.inlineStyles` is turned back on. */
const CSS_URL = /(url\(\s*["']?)\/_nuxt\//gi

export default defineNitroPlugin((nitroApp) => {
  nitroApp.hooks.hook('render:html', (html, { event }) => {
    const prefix = relativePrefixFor(event.path)
    // Depth 0 still needs the rewrite: `/_nuxt/…` must become `_nuxt/…`, or the leading slash survives and
    // the root page breaks from `file://` exactly like the nested ones.
    const rewrite = (s: string) =>
      s
        .replace(ASSET_ATTR, `$1${prefix}_nuxt/`)
        .replace(SRCSET_ENTRY, `$1${prefix}_nuxt/`)
        .replace(CSS_URL, `$1${prefix}_nuxt/`)

    for (const key of ['head', 'bodyPrepend', 'bodyAppend'] as const) {
      const part = html[key]
      if (Array.isArray(part)) {
        for (let i = 0; i < part.length; i += 1) part[i] = rewrite(part[i]!)
      }
    }

    // `body` is not rewritten (see § Scope). If a root-absolute asset ref ever appears there it would 404
    // from `file://` with no other symptom, so say so rather than shipping a silently broken page. Note the
    // escaped form (`&quot;`) cannot match, so rendered source pages quoting a `/_nuxt/` URL stay quiet.
    if (Array.isArray(html.body)) {
      for (const chunk of html.body) {
        if (typeof chunk === 'string' && ASSET_ATTR.test(chunk)) {
          ASSET_ATTR.lastIndex = 0
          console.warn(
            `[relative-asset-urls] ${event.path}: root-absolute /_nuxt/ reference in body, which is not ` +
              `rewritten. It will 404 from file:// and from any sub-path deployment. If a component now ` +
              `references a bundled asset, this plugin's scoping needs revisiting.`,
          )
          break
        }
        ASSET_ATTR.lastIndex = 0
      }
    }
  })
})
