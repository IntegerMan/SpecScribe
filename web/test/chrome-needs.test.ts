import { describe, expect, it } from 'vitest'
import { chromeNeeds } from '../ir/adapter'

/**
 * The chrome-script derivation. [Story 23.6]
 *
 * ⚠️ **Three of these four probes did not exist before Story 23.6, and their absence was shipping.** The C#
 * `HtmlRenderAdapter.Render` was the only emitter of the mermaid init module, the relationship graph's
 * anti-flash boot marker, and the TOC active-section tracker. Once Story 23.6 Task 3 made Nuxt the writer of
 * every `.html`, those three simply stopped appearing on the portal — verified on the generated site before
 * the fix: a `data-relgraph` page carried ZERO occurrences of `data-ss-relgraph-boot` and zero of
 * `plotly-hierarchy.min.js`, and every mermaid diagram was an inert `<pre>` block.
 *
 * These tests exist so that cannot recur silently. They are the renderer-side half of the split recorded in
 * `tests/SpecScribe.Tests/RegionAssert.cs`: the C# suite asserts the REGION carries the mount point, and this
 * asserts the mount point turns into the right head tags.
 */
describe('chromeNeeds', () => {
  it('finds each mount point on the markup that actually carries it', () => {
    expect(chromeNeeds('<div data-hierarchy="sunburst"></div>').needsHierarchyEngine).toBe(true)
    expect(chromeNeeds('<div data-relgraph="stories"></div>').needsGraphEngine).toBe(true)
    expect(chromeNeeds('<pre class="mermaid">graph TD;</pre>').needsMermaid).toBe(true)
    expect(chromeNeeds('<nav class="toc-sidebar" aria-label="On this page"></nav>').needsToc).toBe(true)
  })

  it('reports nothing for a page with none of them', () => {
    expect(chromeNeeds('<section class="panels"><p>Plain prose.</p></section>')).toEqual({
      needsHierarchyEngine: false,
      needsGraphEngine: false,
      needsMermaid: false,
      needsToc: false,
    })
  })

  /**
   * The regression that motivated the attribute-not-substring rule. This portal renders its OWN source, so a
   * `code/**` page shows every one of these markers as entity-escaped prose. A substring probe loaded 1.22 MB
   * of charting engine onto four such pages for charts that do not exist.
   */
  it('is not fooled by a page that renders these markers as escaped PROSE', () => {
    const asProse =
      '<pre><code>&lt;div data-hierarchy=&quot;sunburst&quot;&gt;&lt;/div&gt;\n'
      + '&lt;div data-relgraph=&quot;x&quot;&gt;&lt;/div&gt;\n'
      + '&lt;pre class=&quot;mermaid&quot;&gt;graph TD;&lt;/pre&gt;\n'
      + '&lt;nav class=&quot;toc-sidebar&quot;&gt;&lt;/nav&gt;</code></pre>'
    expect(chromeNeeds(asProse)).toEqual({
      needsHierarchyEngine: false,
      needsGraphEngine: false,
      needsMermaid: false,
      needsToc: false,
    })
  })

  it('does not confuse the two chart families with each other', () => {
    const hierarchyOnly = chromeNeeds('<div data-hierarchy="sunburst"></div>')
    expect(hierarchyOnly.needsGraphEngine).toBe(false)
    const graphOnly = chromeNeeds('<div data-relgraph="stories"></div>')
    expect(graphOnly.needsHierarchyEngine).toBe(false)
  })

  /**
   * `data-hierarchy-label` must NOT light the engine: the lookahead exists so a longer attribute that merely
   * starts with the marker's name is not mistaken for it.
   */
  it('matches the whole attribute name, not a prefix of a longer one', () => {
    expect(chromeNeeds('<div data-hierarchy-label="Epics"></div>').needsHierarchyEngine).toBe(false)
    expect(chromeNeeds('<div data-relgraph-caption="x"></div>').needsGraphEngine).toBe(false)
  })

  it('finds a mount point that is not the first attribute on its tag', () => {
    // The real markup carries id/class first on most surfaces, so a probe anchored to `<div data-…` would
    // have missed nearly every genuine host.
    expect(chromeNeeds('<div id="roadmap" class="chart" data-hierarchy="sunburst"></div>').needsHierarchyEngine)
      .toBe(true)
    expect(chromeNeeds('<div id="g" class="ss-relgraph" data-relgraph="stories"></div>').needsGraphEngine)
      .toBe(true)
  })
})
