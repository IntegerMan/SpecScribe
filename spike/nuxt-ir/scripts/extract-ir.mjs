// Build-time step: pull the four spike surfaces out of the proxy IR into a small self-contained bundle the
// Nuxt app imports. Keeps the app from loading the full ~80 MB SpaDelivery output at prerender time, and keeps
// the SpaDelivery→neutral mapping confined to ir/adapter.mjs.
//
// Usage:  node scripts/extract-ir.mjs [pathToSpecScribeOutput]
// Default source: ../../SpecScribeOutput (the repo-root output `specscribe generate --spa` writes).

import { readFileSync, writeFileSync, mkdirSync, copyFileSync, statSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chunkFileFor, SURFACES, toIrPage, toIrSite } from '../ir/adapter.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const appRoot = resolve(here, '..');
const outputRoot = resolve(process.argv[2] ?? join(appRoot, '..', '..', 'SpecScribeOutput'));

const manifest = JSON.parse(readFileSync(join(outputRoot, 'spa', 'manifest.json'), 'utf8'));
const site = toIrSite(manifest);

const chunkCache = new Map();
function chunk(file) {
  if (!chunkCache.has(file)) {
    chunkCache.set(file, JSON.parse(readFileSync(join(outputRoot, file), 'utf8')));
  }
  return chunkCache.get(file);
}

function extract(irPath) {
  const entry = manifest.pages[irPath];
  if (!entry) throw new Error(`IR has no page '${irPath}' — regenerate with: specscribe generate --spa`);
  const html = chunk(chunkFileFor(entry))[irPath];
  if (typeof html !== 'string') throw new Error(`IR chunk ${chunkFileFor(entry)} has no content for '${irPath}'`);
  return toIrPage(irPath, entry, html);
}

const pages = {};
for (const surface of SURFACES) {
  pages[surface.route] = { ...extract(surface.irPath), probe: surface.probe };
}

// Scaling probe (AC #3 Node-in-pipeline cost): SPIKE_SCALE=N adds N pages of the real 914-page site as routes,
// so prerender throughput can be measured at a realistic route count instead of extrapolated from four.
const rawScale = process.env.SPIKE_SCALE;
const scale = rawScale === undefined || rawScale === '' ? 0 : Number(rawScale);
if (!Number.isInteger(scale) || scale < 0) {
  throw new Error(`SPIKE_SCALE must be a non-negative integer, got '${rawScale}'`);
}
if (scale > 0) {
  // Exclude the four named surfaces: they are already routes above, and re-adding them as /p/N prerendered
  // them TWICE, which is where the report's 918-vs-914 route-count discrepancy came from.
  const named = new Set(SURFACES.map((s) => s.irPath));
  const all = Object.keys(manifest.pages).sort().filter((p) => !named.has(p));
  if (scale > all.length) {
    console.warn(`SPIKE_SCALE=${scale} exceeds ${all.length} available pages — clamped to ${all.length}`);
  }
  // Stride-sample rather than slice(0, N): the keys are sorted, so a prefix slice draws an alphabetically
  // biased subset (all of `adrs/…`, none of `planning-artifacts/…`) whose per-route cost is unrepresentative.
  const take = Math.min(scale, all.length);
  const stride = all.length / take;
  for (let i = 0; i < take; i++) {
    pages[`/p/${i}`] = { ...extract(all[Math.floor(i * stride)]), probe: 'scale probe' };
  }
}

mkdirSync(join(appRoot, 'ir-data'), { recursive: true });
writeFileSync(join(appRoot, 'ir-data', 'ir.json'), JSON.stringify({ site, pages }), 'utf8');

// The token/style source is the SHIPPED stylesheet — imported verbatim, never re-typed, so the spike cannot
// drift from the --status-*/--motion-* values (AD-7). The scoped-SFC components layer on top of it.
mkdirSync(join(appRoot, 'public'), { recursive: true });
copyFileSync(join(outputRoot, 'specscribe.css'), join(appRoot, 'public', 'specscribe.css'));

// Cost-analysis inputs for AC #3: how big is the proxy IR we are consuming?
const spaDir = join(outputRoot, 'spa');
const irBytes = readdirSync(spaDir).reduce((n, f) => n + statSync(join(spaDir, f)).size, 0);
const bundleBytes = statSync(join(appRoot, 'ir-data', 'ir.json')).size;

console.log(`IR source        : ${outputRoot}`);
console.log(`IR total bytes   : ${irBytes.toLocaleString()} (${readdirSync(spaDir).length} files)`);
console.log(`extracted bundle : ${bundleBytes.toLocaleString()} bytes, ${Object.keys(pages).length} pages`);
for (const [route, p] of Object.entries(pages)) {
  console.log(`  ${route.padEnd(14)} ${String(Buffer.byteLength(p.contentHtml)).padStart(8)} B  ${p.probe}`);
}
