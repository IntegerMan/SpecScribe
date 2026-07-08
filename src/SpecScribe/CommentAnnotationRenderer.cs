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
        if (obj.Type != HtmlBlockType.Comment)
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
        if (text.StartsWith("<!--", StringComparison.Ordinal))
        {
            text = text[4..];
        }
        if (text.EndsWith("-->", StringComparison.Ordinal))
        {
            text = text[..^3];
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
        if (tag is not null && tag.StartsWith("<!--", StringComparison.Ordinal) && tag.EndsWith("-->", StringComparison.Ordinal))
        {
            var text = tag[4..^3].Trim();
            var encoded = PathUtil.Html(text);
            renderer.Write("<span class=\"md-comment-inline\">").Write(encoded).Write("</span>");
            return;
        }

        _fallback.Write(renderer, obj);
    }
}
