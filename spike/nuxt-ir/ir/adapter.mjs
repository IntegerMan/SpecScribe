// The ONE place this spike knows the proxy IR's shape.
//
// Epic 22's canonical IR (Story 22.2) does not exist yet, so this spike consumes the already-shipped
// SpaDelivery form (`spa/manifest.json` + `spa/pages-*.json`) as a PROXY IR — exactly as Story 22.1 did.
// Everything downstream of `toIrPage()` speaks the neutral `IrPage` shape below, so when 22.2 lands its real
// schema, THIS FILE is the only thing that changes. Do not let SpaDelivery field names leak into components.
//
// Neutral shape (what the Vue side is allowed to know about):
//   IrSite  { title, entry, nav: [{ label, path }], pages: Map<path, IrPage> }
//   IrPage  { path, title, contentHtml, breadcrumb: [{ label, path|null }], parent, children: [path] }

/** SpaDelivery manifest → neutral site header (title/entry/nav). */
export function toIrSite(manifest) {
  return {
    title: manifest.siteTitle,
    entry: manifest.entry,
    nav: (manifest.nav ?? []).map((n) => ({ label: n.label, path: n.outputRelativePath })),
  };
}

/** SpaDelivery manifest entry + its chunk's HTML → neutral page. */
export function toIrPage(path, manifestEntry, contentHtml) {
  return {
    path,
    title: manifestEntry.title,
    contentHtml,
    breadcrumb: (manifestEntry.breadcrumb ?? []).map((c) => ({
      label: c.label,
      path: c.outputRelativePath ?? null,
    })),
    parent: manifestEntry.parent ?? null,
    children: manifestEntry.children ?? [],
  };
}

/** Which chunk file holds a page's content. The only other SpaDelivery-specific fact. */
export function chunkFileFor(manifestEntry) {
  return manifestEntry.chunk; // e.g. "spa/pages-root.json"
}

/**
 * The surfaces this spike renders. Chosen (see the story's "Which surface" Dev Note) so that between them they
 * exercise all three AC #2 sub-risks: token-driven scoped CSS + chart-SVG injection (dashboard) and the custom
 * Markdig renderers (the three prose pages).
 */
export const SURFACES = [
  { route: '/dashboard', irPath: 'index.html', probe: 'chart-svg + tokens' },
  { route: '/adr-0006', irPath: 'adrs/0006-delivery-architecture-and-distribution.html', probe: 'mermaid + gherkin/capability stylers' },
  { route: '/story-22-1', irPath: 'implementation-artifacts/22-1-spike-incremental-recompute-and-ir-delta-transport.html', probe: 'reference chips + comment annotations' },
  { route: '/ux-design', irPath: 'planning-artifacts/ux-designs/ux-SpecScribe-2026-07-05/DESIGN.html', probe: 'color swatches + tables' },
];
