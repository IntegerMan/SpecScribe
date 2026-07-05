using System.Text;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace SpecScribe;

/// <summary>Renders a <c>```mermaid</c> fenced code block as <c>&lt;pre class="mermaid"&gt;</c> — the shape the
/// client-side mermaid.js renderer picks up (see <see cref="Mermaid"/>) — instead of Markdig's default
/// <c>&lt;pre&gt;&lt;code class="language-mermaid"&gt;</c>. Every other code block falls through to the default
/// renderer this one wraps, so ordinary fenced code is untouched.</summary>
public sealed class MermaidCodeBlockRenderer : HtmlObjectRenderer<CodeBlock>
{
    public const string MermaidInfo = "mermaid";

    private readonly CodeBlockRenderer _fallback;

    public MermaidCodeBlockRenderer(CodeBlockRenderer fallback) => _fallback = fallback;

    /// <summary>True if the parsed document contains at least one mermaid fenced block — used to decide whether
    /// a page needs the mermaid init script injected.</summary>
    public static bool HasMermaid(MarkdownDocument document) =>
        document.Descendants<FencedCodeBlock>().Any(IsMermaid);

    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (obj is FencedCodeBlock fenced && IsMermaid(fenced))
        {
            renderer.Write(Mermaid.Block(ExtractSource(fenced)));
            return;
        }

        _fallback.Write(renderer, obj);
    }

    private static bool IsMermaid(FencedCodeBlock fenced) =>
        string.Equals(fenced.Info, MermaidInfo, StringComparison.OrdinalIgnoreCase);

    private static string ExtractSource(LeafBlock block)
    {
        var sb = new StringBuilder();
        var lines = block.Lines.Lines;
        for (var i = 0; i < block.Lines.Count; i++)
        {
            sb.Append(lines[i].Slice.ToString());
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd('\n');
    }
}
