// AC #3 probe: can Nuxt's hydration scripts carry the webview's per-render nonce?
//
// Nuxt 3 core has no `nonce` option (that lives in the third-party nuxt-security module), but Nitro's
// `render:html` hook exposes the emitted head/body script arrays before serialization. This plugin stamps a
// nonce PLACEHOLDER onto every <script> Nuxt emits — `__NONCE__`, the exact token
// src/SpecScribe/WebviewRenderAdapter.cs already string-replaces per panel render. In a prerendered (static)
// build the nonce cannot be per-render by construction, so placeholder + host substitution is the only shape
// that can work; this proves Nuxt will emit it.
export default defineNitroPlugin((nitro) => {
  nitro.hooks.hook('render:html', (html) => {
    // NOTE: `html.head` / `html.bodyAppend` entries are HTML CHUNKS, not one-tag-per-entry — the entry-module
    // <script> arrives inside the same string as the <link rel=modulepreload> block. So this rewrites every
    // <script> occurrence within each chunk, not just chunks that start with one.
    // Match only real tag starts (`<script` followed by whitespace or `>`), so the literal text `<script` inside
    // an inline script body or an HTML comment is left alone, and require a genuine `nonce` ATTRIBUTE in the
    // negative lookahead (`\snonce=`) rather than the bare substring — `data-nonce=` must not count as nonced.
    const stamp = (chunk: string) =>
      chunk.replace(/<script(?=[\s>])(?![^>]*\snonce=)/g, '<script nonce="__NONCE__"')

    // `html.body` is mapped too: anything Nuxt emits there (server components / <NuxtIsland>) would otherwise be
    // silently missed, and the report's "all Nuxt scripts carry a nonce" claim would be bounded by this plugin's
    // own coverage rather than measured. `?? []` guards Nitro versions where a bucket is absent.
    html.head = (html.head ?? []).map(stamp)
    html.bodyPrepend = (html.bodyPrepend ?? []).map(stamp)
    html.body = (html.body ?? []).map(stamp)
    html.bodyAppend = (html.bodyAppend ?? []).map(stamp)
  })
})
