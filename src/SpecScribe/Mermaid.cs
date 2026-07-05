using System.Text;

namespace SpecScribe;

/// <summary>Builds the mermaid.js epic-roadmap diagram. Rendering happens client-side via a CDN-loaded
/// module script (see <see cref="InitScript"/>) — the offline SVG/CSS charts in <see cref="Charts"/> are
/// the backbone; this is the one internet-dependent extra.</summary>
public static class Mermaid
{
    public static string RoadmapDiagram(EpicsModel model)
    {
        var vs = model.Epics.Where(e => e.Section == EpicSection.VerticalSlice).OrderBy(e => e.Number).ToList();
        var fd = model.Epics.Where(e => e.Section == EpicSection.FurtherDevelopment).OrderBy(e => e.Number).ToList();

        var sb = new StringBuilder();
        sb.Append("flowchart LR\n");

        AppendSubgraph(sb, "VS", "Vertical Slice", vs);
        AppendSubgraph(sb, "FD", "Further Development", fd);

        if (vs.Count > 0 && fd.Count > 0)
        {
            sb.Append("  VS --> FD\n");
        }

        sb.Append("  classDef done fill:#e8f0e4,stroke:#6b8f62,color:#2a1f0e,stroke-width:1px;\n");
        sb.Append("  classDef pending fill:#e8d5b0,stroke:#c4a882,color:#5a4535,stroke-width:1px;\n");

        var doneIds = model.Epics.Where(e => e.Status == EpicStatus.Drafted).Select(e => $"E{e.Number}").ToList();
        var pendingIds = model.Epics.Where(e => e.Status == EpicStatus.Pending).Select(e => $"E{e.Number}").ToList();
        if (doneIds.Count > 0) sb.Append($"  class {string.Join(',', doneIds)} done\n");
        if (pendingIds.Count > 0) sb.Append($"  class {string.Join(',', pendingIds)} pending\n");

        return sb.ToString();
    }

    private static void AppendSubgraph(StringBuilder sb, string id, string title, List<EpicInfo> epics)
    {
        if (epics.Count == 0) return;

        sb.Append($"  subgraph {id}[\"{EscapeLabel(title)}\"]\n");
        sb.Append("    direction LR\n");
        sb.Append("    " + string.Join(" --> ", epics.Select(e =>
            $"E{e.Number}[\"{EscapeLabel($"{e.Number:00} {PathUtil.StripHtmlTags(e.Title)}")}\"]")) + "\n");
        sb.Append("  end\n");
    }

    private static string EscapeLabel(string text) => text.Replace('"', '\'').Replace('\n', ' ').Trim();

    /// <summary>Wraps raw mermaid source for the client-side renderer to pick up. HTML-encoding here is
    /// correct, not redundant: mermaid.js reads the element's textContent, which the browser decodes back
    /// to plain text — this just prevents the raw source from being parsed as markup in the meantime.</summary>
    public static string Block(string mermaidSource) => $"<pre class=\"mermaid\">\n{PathUtil.Html(mermaidSource)}\n</pre>\n\n";

    public static string InitScript() => """
        <script type="module">
          import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs';
          mermaid.initialize({
            startOnLoad: true,
            theme: 'base',
            flowchart: { useMaxWidth: false },
            themeVariables: {
              background: '#faf7f2',
              primaryColor: '#f4ead5',
              primaryTextColor: '#2a1f0e',
              primaryBorderColor: '#d4c4a8',
              lineColor: '#8b3a1a',
              secondaryColor: '#e8d5b0',
              tertiaryColor: '#faf7f2',
              fontFamily: 'Georgia, "Times New Roman", serif'
            }
          });
        </script>

        """;
}
