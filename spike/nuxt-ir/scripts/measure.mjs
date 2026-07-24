// Produces every number in the Story 23.1 spike report from the actual artifacts. Re-runnable: `npm run measure`.
//
//   node scripts/measure.mjs [pathToSpecScribeOutput]
//
// Axes:
//   1. NFR6      — is the prerendered HTML fully rendered, and does it survive with every <script> removed?
//   2. Parity    — does the IR's content survive v-html byte-for-byte, and does it match the golden static page?
//   3. CSP       — inventory of every <script> Nuxt emits (inline vs external, nonced vs not).
//   4. Node cost — build wall-clock inputs, emitted asset weight, hydration payload weight.

import { readFileSync, existsSync, readdirSync, statSync, mkdirSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { SURFACES } from '../ir/adapter.mjs'

const here = dirname(fileURLToPath(import.meta.url))
const appRoot = resolve(here, '..')
const outputRoot = resolve(process.argv[2] ?? join(appRoot, '..', '..', 'SpecScribeOutput'))
const pub = join(appRoot, '.output', 'public')

const ir = JSON.parse(readFileSync(join(appRoot, 'ir-data', 'ir.json'), 'utf8'))
const manifest = JSON.parse(readFileSync(join(outputRoot, 'spa', 'manifest.json'), 'utf8'))

const B = (n) => n.toLocaleString()
const rows = []
const notes = []

function dirBytes(d) {
  if (!existsSync(d)) return 0
  return readdirSync(d, { withFileTypes: true }).reduce(
    (n, e) => n + (e.isDirectory() ? dirBytes(join(d, e.name)) : statSync(join(d, e.name)).size),
    0,
  )
}

// Strip every <script>…</script>. Approximates "JavaScript disabled" for the purpose of asking whether the
// document still carries its content and its links.
const stripScripts = (html) => html.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, '')

console.log('='.repeat(96))
console.log('AXIS 1+2 — NFR6 baseline and parity, per surface')
console.log('='.repeat(96))
console.log(
  ['route', 'prerender', 'js-off', 'payload', 'IR bytes', 'verbatim?', 'golden?', 'svg', 'a[href]']
    .map((h, i) => h.padEnd([14, 10, 10, 10, 10, 10, 8, 6, 8][i]))
    .join(''),
)

for (const surface of SURFACES) {
  const route = surface.route
  const htmlPath = join(pub, route.slice(1), 'index.html')
  if (!existsSync(htmlPath)) {
    console.log(`${route.padEnd(14)}MISSING — run \`npm run generate\` first`)
    continue
  }
  const html = readFileSync(htmlPath, 'utf8')
  const irHtml = ir.pages[route].contentHtml

  // Parity 1: does the IR's content string survive Vue's v-html byte-for-byte into the emitted HTML?
  const verbatimInNuxt = html.includes(irHtml)

  // Parity 2: does that same string appear byte-for-byte in the SHIPPED static page (the golden oracle)?
  // If both hold, the Nuxt content region is transitively byte-identical to the golden content region.
  const goldenPath = join(outputRoot, surface.irPath)
  const golden = existsSync(goldenPath) ? readFileSync(goldenPath, 'utf8') : ''
  const verbatimInGolden = golden ? golden.includes(irHtml) : null

  const jsOff = stripScripts(html)
  const payloadPath = join(pub, route.slice(1), '_payload.json')
  const payload = existsSync(payloadPath) ? statSync(payloadPath).size : 0

  rows.push({
    route,
    prerender: Buffer.byteLength(html),
    jsOff: Buffer.byteLength(jsOff),
    payload,
    irBytes: Buffer.byteLength(irHtml),
    verbatimInNuxt,
    verbatimInGolden,
    golden: golden ? Buffer.byteLength(golden) : 0,
    svg: (jsOff.match(/<svg\b/g) || []).length,
    links: (jsOff.match(/<a\s[^>]*href=/g) || []).length,
    probe: surface.probe,
  })

  const r = rows.at(-1)
  console.log(
    [
      route.padEnd(14),
      B(r.prerender).padEnd(10),
      B(r.jsOff).padEnd(10),
      B(r.payload).padEnd(10),
      B(r.irBytes).padEnd(10),
      String(r.verbatimInNuxt).padEnd(10),
      String(r.verbatimInGolden).padEnd(8),
      String(r.svg).padEnd(6),
      String(r.links).padEnd(8),
    ].join(''),
  )
}

// The spike index page is the JS-off NAVIGATION probe: its links must be real <a href> in the emitted HTML,
// and each target must exist as its own prerendered file on disk (no client router required).
const indexHtml = readFileSync(join(pub, 'index.html'), 'utf8')
const indexJsOff = stripScripts(indexHtml)
const navTargets = SURFACES.map((s) => ({
  route: s.route,
  anchorInHtml: indexJsOff.includes(`href="${s.route}"`),
  fileOnDisk: existsSync(join(pub, s.route.slice(1), 'index.html')),
}))
console.log('\nJS-off navigation (spike index → each surface):')
for (const t of navTargets) {
  console.log(`  ${t.route.padEnd(14)} <a href> present: ${t.anchorInHtml}   prerendered file on disk: ${t.fileOnDisk}`)
}

console.log('\n' + '='.repeat(96))
console.log('AXIS 3 — script inventory (webview CSP surface)')
console.log('='.repeat(96))
const dash = readFileSync(join(pub, 'dashboard', 'index.html'), 'utf8')
const scriptRe = /<script([^>]*)>([\s\S]*?)<\/script>/g
let m
let i = 0
while ((m = scriptRe.exec(dash))) {
  const attrs = m[1].trim()
  const inline = !/\ssrc=/.test(attrs)
  const nonced = /nonce=/.test(attrs)
  const type = (attrs.match(/type="([^"]+)"/) || [, 'text/javascript'])[1]
  const origin = attrs.includes('id="sunburst-explorer-data"') ? 'IR content (SpecScribe island)' : 'Nuxt'
  console.log(
    `  ${String(++i).padStart(2)}. ${inline ? 'inline  ' : 'external'} nonce=${String(nonced).padEnd(5)} ` +
      `type=${type.padEnd(18)} bytes=${String(Buffer.byteLength(m[2])).padStart(7)}  ${origin}`,
  )
}

console.log('\n' + '='.repeat(96))
console.log('AXIS 4 — Node-in-pipeline weight')
console.log('='.repeat(96))
const spaDir = join(outputRoot, 'spa')
const spaBytes = readdirSync(spaDir).reduce((n, f) => n + statSync(join(spaDir, f)).size, 0)
const nuxtAssets = dirBytes(join(pub, '_nuxt'))
const nodeModules = dirBytes(join(appRoot, 'node_modules'))
const totalPrerender = rows.reduce((n, r) => n + r.prerender, 0)
const totalPayload = rows.reduce((n, r) => n + r.payload, 0)
const totalGolden = rows.reduce((n, r) => n + r.golden, 0)

console.log(`  proxy IR (spa/, ${readdirSync(spaDir).length} files)   : ${B(spaBytes)} bytes`)
console.log(`  extracted IR bundle (4 surfaces)  : ${B(statSync(join(appRoot, 'ir-data', 'ir.json')).size)} bytes`)
console.log(`  Nuxt client assets (_nuxt/)       : ${B(nuxtAssets)} bytes`)
console.log(`  node_modules (build-time only)    : ${B(nodeModules)} bytes`)
console.log(`  manifest page count (route table) : ${B(Object.keys(manifest.pages).length)} pages`)
console.log('')
console.log(`  4 surfaces, C# static HTML        : ${B(totalGolden)} bytes`)
console.log(`  4 surfaces, Nuxt prerendered HTML : ${B(totalPrerender)} bytes`)
console.log(`  4 surfaces, hydration payloads    : ${B(totalPayload)} bytes  (fetched ON TOP of the HTML)`)
console.log(
  `  wire cost to hydrate vs C# static : ${((totalPrerender + totalPayload) / totalGolden).toFixed(2)}x`,
)

mkdirSync(join(appRoot, 'measurements'), { recursive: true })
writeFileSync(
  join(appRoot, 'measurements', 'measurements.json'),
  JSON.stringify(
    { rows, navTargets, spaBytes, nuxtAssets, nodeModules, totalPrerender, totalPayload, totalGolden, notes },
    null,
    2,
  ),
)
console.log(`\nwrote measurements/measurements.json`)
