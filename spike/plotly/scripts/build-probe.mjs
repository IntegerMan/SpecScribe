// Story 20.4 — assembles the probe page from REAL shipped sources. Drift-free by construction:
// nothing here re-types a token value, a CSP string, or a payload node.
//
//   node scripts/build-probe.mjs [pathToSpecScribeOutput]
//
// Emits spike/plotly/probe/:
//   plotly.min.js         copied verbatim from the custom bundle built in Task 2
//   specscribe.css        copied verbatim from the GENERATED portal (the shipped stylesheet)
//   explorer.js           copied verbatim from probe-src/
//   index.html            control — no CSP at all
//   webview-meta.html     the byte-verbatim shipped webview policy delivered as <meta http-equiv>, with a fixed
//                         nonce substituted exactly as the extension shim does per render
//   webview-partial.html  the "half-applied relaxation" state R7 #3 warns about: script-src nonce present but the
//                         nonce on the bundle tag is WRONG. Under ADR 0013 there is no SVG beneath, so this is the
//                         blank-chart-region case, and it must be looked at rather than reasoned about.
//   nojs.html             every <script> that is not the data island removed — what a JS-off visitor gets

import { readFileSync, writeFileSync, mkdirSync, copyFileSync, existsSync, statSync } from 'node:fs'
import { join, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const spikeRoot = join(here, '..')
const repoRoot = resolve(spikeRoot, '..', '..')
const outRoot = resolve(process.argv[2] ?? join(repoRoot, 'SpecScribeOutput'))
const probe = join(spikeRoot, 'probe')
mkdirSync(probe, { recursive: true })

const bundle = join(spikeRoot, 'plotly-src', 'dist', 'plotly-specscribe-hierarchy.min.js')
const css = join(outRoot, 'specscribe.css')
const dashboard = join(outRoot, 'index.html')
for (const p of [bundle, css, dashboard]) {
  if (!existsSync(p)) { console.error(`build-probe: missing required input ${p}`); process.exit(1) }
}

copyFileSync(bundle, join(probe, 'plotly.min.js'))
copyFileSync(css, join(probe, 'specscribe.css'))
copyFileSync(join(spikeRoot, 'probe-src', 'explorer.js'), join(probe, 'explorer.js'))
copyFileSync(join(spikeRoot, 'probe-src', 'survival.js'), join(probe, 'survival.js'))

// The REAL Story 20.2 island, lifted verbatim out of the generated dashboard. Not a synthetic fixture (R6).
const dash = readFileSync(dashboard, 'utf8')
const islandMatch = dash.match(/<script type="application\/json" id="sunburst-explorer-data"[^>]*>[\s\S]*?<\/script>/)
if (!islandMatch) { console.error('build-probe: sunburst-explorer-data island not found in the generated dashboard'); process.exit(1) }
const island = islandMatch[0]

// The webview CSP, byte-verbatim from src/SpecScribe/WebviewRenderAdapter.cs (DocumentTemplate). Read from the
// SOURCE FILE at build time rather than pasted, so a policy change upstream cannot silently invalidate this probe.
const adapter = readFileSync(join(repoRoot, 'src', 'SpecScribe', 'WebviewRenderAdapter.cs'), 'utf8')
const cspMatch = adapter.match(/<meta http-equiv="Content-Security-Policy" content="([^"]+)"/)
if (!cspMatch) { console.error('build-probe: could not read the CSP string out of WebviewRenderAdapter.cs'); process.exit(1) }
const SHIPPED_CSP = cspMatch[1]
console.log(`shipped webview CSP (verbatim from WebviewRenderAdapter.cs):\n  ${SHIPPED_CSP}\n`)

const NONCE = 'ss20p4NONCEfixedForProbe'

function page({ cspMeta, scriptNonce, includeScripts = true, title }) {
  const nonceAttr = scriptNonce ? ` nonce="${scriptNonce}"` : ''
  const scripts = includeScripts
    ? `<script${nonceAttr} src="plotly.min.js"></script>\n<script${nonceAttr} src="explorer.js"></script>\n<script${nonceAttr} src="survival.js"></script>`
    : '<!-- scripts removed: JS-off / CSP-blocked simulation -->'
  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
${cspMeta ? `<meta http-equiv="Content-Security-Policy" content="${cspMeta}" />\n` : ''}<meta name="viewport" content="width=device-width, initial-scale=1.0" />
<title>${title}</title>
<link rel="stylesheet" href="specscribe.css" />
<style>
  body { padding: 1rem 1.5rem; }
  #probe-chart { width: 640px; height: 640px; max-width: 100%; }
  #probe-status { font-family: 'Courier New', monospace; font-size: 0.78rem; white-space: pre-wrap; }
  .probe-controls { display: flex; gap: 0.5rem; margin: 0.5rem 0; }
  .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
  /* AC #3 — the one thing Plotly's config surface CANNOT fix. Its marker.pattern hatch <path> is emitted with a
     stroke but NO fill, so SVG's initial value (black) is painted under every hatch at fill-opacity 1. Measured:
     rgb(0,0,0) present 21x in the pattern defs. There is no Plotly attribute for it; this CSS rule is the fix,
     and with it the foreign-color count goes 1 -> 0. The Story 20.5 component must ship this rule. */
  #probe-chart defs pattern > path { fill: none; }
</style>
</head>
<body>
<h1>Story 20.4 Plotly probe — ${title}</h1>
<p id="probe-status">(no JavaScript ran — this text is the server-rendered placeholder)</p>
<div class="probe-controls">
  <button type="button" data-shape="sunburst">Sunburst</button>
  <button type="button" data-shape="treemap">Treemap</button>
</div>
<div id="probe-chart"></div>
<p id="probe-live" class="sr-only" role="status" aria-live="polite"></p>
<h2>Text twin (ADR 0013 — the no-JS contract)</h2>
<p>This probe deliberately ships <strong>no</strong> server-rendered chart SVG, exactly as ADR 0013 specifies.
Whatever the chart region shows when scripts do not run is the honest answer to "what does a blocked visitor see".</p>
${island}
${scripts}
</body>
</html>
`
}

writeFileSync(join(probe, 'index.html'), page({ cspMeta: null, scriptNonce: null, title: 'control (no CSP)' }))
// Nonced but with NO meta policy: this is the page csp-probe.mjs serves when it delivers the policy as an HTTP
// HEADER. Keeping meta and header delivery on separate files stops the two policies stacking, which would make
// every header variant look blocked for the wrong reason.
writeFileSync(join(probe, 'nonced.html'), page({ cspMeta: null, scriptNonce: NONCE, title: 'nonced (policy comes from the HTTP header)' }))
writeFileSync(join(probe, 'webview-meta.html'), page({
  cspMeta: SHIPPED_CSP.replace(/__CSP_SOURCE__/g, "'self'").replace(/__NONCE__/g, NONCE),
  scriptNonce: NONCE,
  title: 'shipped webview CSP via <meta>',
}))
writeFileSync(join(probe, 'webview-partial.html'), page({
  cspMeta: SHIPPED_CSP.replace(/__CSP_SOURCE__/g, "'self'").replace(/__NONCE__/g, NONCE),
  scriptNonce: 'WRONG-NONCE-half-applied-relaxation',
  title: 'PARTIAL relaxation (wrong nonce) — the blank-region case',
}))
writeFileSync(join(probe, 'nojs.html'), page({ cspMeta: null, scriptNonce: null, includeScripts: false, title: 'JS off' }))

const sizes = ['plotly.min.js', 'specscribe.css', 'explorer.js', 'index.html', 'webview-meta.html', 'webview-partial.html', 'nojs.html']
  .map((f) => `  ${f.padEnd(22)} ${statSync(join(probe, f)).size.toLocaleString()} B`)
console.log('probe/ built:\n' + sizes.join('\n'))
console.log(`\nisland lifted from ${dashboard} — ${Buffer.byteLength(island).toLocaleString()} B, ${JSON.parse(island.replace(/^<script[^>]*>/, '').replace(/<\/script>$/, '')).nodes.length} nodes`)
