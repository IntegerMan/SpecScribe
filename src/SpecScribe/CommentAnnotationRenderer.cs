using System.Text;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SpecScribe;

/// <summary>Renders an <c>HtmlBlock</c> of type <see cref="HtmlBlockType.Comment"/> (a <c>&lt;!-- ... --&gt;</c>
/// that starts its own line) as a visible, de-emphasized <c>&lt;aside class="md-comment"&gt;</c> instead of
/// Markdig's default raw (browser-invisible) passthrough. Every other <see cref="HtmlBlockType"/> falls through
/// to the default renderer this one wraps, following the same wrap/fallback shape as
/// <see cref="MermaidCodeBlockRenderer"/>.</summary>
public sealed class HtmlBlockCommentRenderer : HtmlObjectRenderer<HtmlBlock>
{
    private readonly HtmlBlockRenderer _fallback;

    public HtmlBlockCommentRenderer(HtmlBlockRenderer fallback) => _fallback = fallback;

    protected override void Write(HtmlRenderer renderer, HtmlBlock obj)
    {
        if (!renderer.EnableHtmlForBlock || obj.Type != HtmlBlockType.Comment)
        {
            _fallback.Write(renderer, obj);
            return;
        }

        var text = StripCommentMarkers(ExtractSource(obj));
        var encoded = PathUtil.Html(text).Replace("\n", "<br>");
        renderer.Write("<aside class=\"md-comment\">").Write(encoded).WriteLine("</aside>");
    }

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

    private static string StripCommentMarkers(string text)
    {
        text = text.Trim();
        if (text.StartsWith("<!--", StringComparison.Ordinal) && text.EndsWith("-->", StringComparison.Ordinal))
        {
            // Overlapping/short malformed comments (e.g. "<!-->", "<!--->") have no room between the
            // markers for content — slicing on the mutated string here (rather than stripping the leading
            // marker first, then separately checking the trailing one) avoids leaving a stray character
            // behind when the two markers overlap.
            var innerStart = 4;
            var innerEnd = text.Length - 3;
            text = innerEnd > innerStart ? text[innerStart..innerEnd] : string.Empty;
        }
        return text.Trim();
    }
}

/// <summary>Renders an <c>HtmlInline</c> whose tag is a mid-line <c>&lt;!-- ... --&gt;</c> comment as a visible
/// <c>&lt;span class="md-comment-inline"&gt;</c> instead of Markdig's default raw passthrough. Every other
/// inline HTML tag falls through to the wrapped default renderer (which itself only writes when
/// <c>renderer.EnableHtmlForInline</c> — preserved by delegating rather than reimplementing it).</summary>
public sealed class HtmlInlineCommentRenderer : HtmlObjectRenderer<HtmlInline>
{
    private readonly HtmlInlineRenderer _fallback;

    public HtmlInlineCommentRenderer(HtmlInlineRenderer fallback) => _fallback = fallback;

    protected override void Write(HtmlRenderer renderer, HtmlInline obj)
    {
        var tag = obj.Tag;
        if (renderer.EnableHtmlForInline && tag is not null
            && tag.StartsWith("<!--", StringComparison.Ordinal) && tag.EndsWith("-->", StringComparison.Ordinal))
        {
            // Overlapping/short malformed tags (e.g. "<!-->", length 5) satisfy both checks above but leave
            // no room between the markers for content — guard against the negative-length slice that would
            // otherwise throw ArgumentOutOfRangeException.
            var innerStart = 4;
            var innerEnd = tag.Length - 3;
            var text = (innerEnd > innerStart ? tag[innerStart..innerEnd] : string.Empty).Trim();
            var encoded = PathUtil.Html(text);
            renderer.Write("<span class=\"md-comment-inline\">").Write(encoded).Write("</span>");
            return;
        }

        _fallback.Write(renderer, obj);
    }
}
