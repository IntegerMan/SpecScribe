// Story 24.6 — assemble the probe pages.
//
// The fixture is INLINED as a `<script type="application/json">` data island rather than fetched, because the
// shipped webview CSP is `default-src 'none'` with no `connect-src`: a fetch would be blocked, and production
// already uses the island idiom (SunburstExplorer.cs `SunburstExplorerDataId`). Inlining is therefore the faithful
// shape, not a convenience.
//
// Vendored assets are COPIED into probe/vendor/ rather than served from SpecScribeOutput/, because a concurrent
// session regenerating that directory mid-measurement is a documented hazard that bit Story 20.4.

import { mkdirSync, copyFileSync, readFileSync, writeFileSync, existsSync, readdirSync } from 'node:fs'
import { dirname, resolve, join, basename } from 'node:path'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')
const repoRoot = resolve(root, '..', '..')
const probeDir = join(root, 'probe')
const vendorDir = join(probeDir, 'vendor')
mkdirSync(vendorDir, { recursive: true })

const fixture = process.argv[2] ?? 'ego-top20'
const fixturePath = join(root, 'fixtures', `${fixture}.json`)
if (!existsSync(fixturePath)) {
  console.error(`no such fixture: ${fixturePath}`)
  console.error(`available: ${readdirSync(join(root, 'fixtures')).filter((f) => f.endsWith('.json')).join(', ')}`)
  process.exit(1)
}
const fixtureText = readFileSync(fixturePath, 'utf8')

// The GENERATED stylesheet, not the source asset — token values must resolve through the real cascade over what
// the tool actually emits (Story 20.4 §2). Falls back to the source asset with a loud note if no site is present.
const generatedCss = join(repoRoot, 'SpecScribeOutput', 'specscribe.css')
const sourceCss = join(repoRoot, 'src/SpecScribe/assets/specscribe.css')
const cssFrom = existsSync(generatedCss) ? generatedCss : sourceCss
copyFileSync(cssFrom, join(vendorDir, 'specscribe.css'))

// The SHIPPED, already-embedded Plotly bundle. Candidate (a)'s whole case is that this file already exists.
const plotly = join(repoRoot, 'src/SpecScribe/assets/plotly-hierarchy.min.js')
copyFileSync(plotly, join(vendorDir, 'plotly-hierarchy.min.js'))

for (const f of readdirSync(join(root, 'dist')).filter((f) => f.endsWith('.min.js'))) {
  copyFileSync(join(root, 'dist', f), join(vendorDir, f))
}

const templates = readdirSync(join(probeDir, 'templates')).filter((f) => f.endsWith('.html'))
const built = []
for (const t of templates) {
  const src = readFileSync(join(probeDir, 'templates', t), 'utf8')
  const out = src.replace('__FIXTURE__', fixtureText)
  writeFileSync(join(probeDir, t), out, 'utf8')
  built.push({ page: t, bytes: out.length })
}

writeFileSync(
  join(probeDir, 'index.html'),
  `<!doctype html><html lang="en"><head><meta charset="utf-8"><!--CSP-META-->
<title>Story 24.6 graph-engine probes</title><link rel="stylesheet" href="./vendor/specscribe.css"></head>
<body style="padding:1.5rem"><h1>Story 24.6 — graph-engine probes</h1>
<p>Fixture: <code>${fixture}</code> — ${JSON.parse(fixtureText).nodes.length} nodes, ${JSON.parse(fixtureText).edges.length} edges</p>
<ul>${built.map((b) => `<li><a href="./${b.page}">${b.page}</a> (${b.bytes.toLocaleString()} B)</li>`).join('')}</ul>
</body></html>`,
  'utf8',
)

console.log(`fixture:  ${basename(fixturePath)} (${fixtureText.length.toLocaleString()} B inlined)`)
console.log(`css from: ${cssFrom === generatedCss ? 'SpecScribeOutput (GENERATED)' : 'src asset (NO GENERATED SITE — note it)'}`)
console.log(`vendor:   ${readdirSync(vendorDir).join(', ')}`)
for (const b of built) console.log(`  built ${b.page.padEnd(24)} ${b.bytes.toLocaleString()} B`)
