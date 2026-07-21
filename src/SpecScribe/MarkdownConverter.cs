using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpecScribe;

/// <summary>Converts a single BMad markdown file (frontmatter + body) into a <see cref="DocModel"/>. Never opens files exclusively — BMad tooling may still be writing them.</summary>
public static class MarkdownConverter
{
    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    /// <summary>Advanced extensions minus single-tilde Subscript: BMad prose uses a bare <c>~</c> for
    /// "approximately" (e.g. <c>~7 ms</c>) constantly, and Subscript's single-tilde delimiter steals those
    /// characters from the emphasis-parsing stack — breaking unrelated <c>~~strikethrough~~</c> pairs
    /// (e.g. resolved-deferred-item markers) elsewhere on the same line. Subscript has no legitimate use
    /// in this project's authored content.</summary>
    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Use(new DocumentRendererWrappersExtension());
        builder.Extensions.ReplaceOrAdd<EmphasisExtraExtension>(new EmphasisExtraExtension(
            EmphasisExtraOptions.Strikethrough | EmphasisExtraOptions.Superscript
            | EmphasisExtraOptions.Inserted | EmphasisExtraOptions.Marked));
        return builder.Build();
    }

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static DocModel Convert(string sourceFullPath, string sourceRelativePath, string outputRelativePath)
    {
        var raw = ReadAllTextShared(sourceFullPath);
        var (frontmatter, body) = SplitFrontmatter(raw);

        var document = Markdown.Parse(body, Pipeline);

        var bodyHtml = RenderDocumentHtml(document);

        var hasMermaid = MermaidCodeBlockRenderer.HasMermaid(document);

        var headings = document.Descendants<HeadingBlock>()
            .Where(h => h.Level is >= 1 and <= 3)
            .Select(h => new Heading(h.Level, ExtractHeadingText(h), h.GetAttributes().Id ?? string.Empty))
            .Where(h => !string.IsNullOrEmpty(h.Id))
            .ToList();

        var title = frontmatter.Title
            ?? ExtractFirstH1(body)
            ?? PrettifyFilename(Path.GetFileNameWithoutExtension(sourceRelativePath));

        return new DocModel
        {
            SourceRelativePath = sourceRelativePath,
            OutputRelativePath = outputRelativePath,
            Title = title,
            Frontmatter = frontmatter,
            BodyHtml = ColorSwatchRewriter.Rewrite(TagTables(bodyHtml)),
            Headings = headings,
            HasMermaid = hasMermaid,
        };
    }

    /// <summary>Renders a parsed markdown document to HTML. The mermaid-aware code-block renderer and the
    /// comment-aware HTML renderers are installed once per <see cref="Pipeline"/> (by
    /// <see cref="DocumentRendererWrappersExtension"/>, run from <see cref="MarkdownPipeline.Setup(IMarkdownRenderer)"/>
    /// below) rather than re-applied on every call, so <c>```mermaid</c> fences become
    /// <c>&lt;pre class="mermaid"&gt;</c> and <c>&lt;!-- ... --&gt;</c> comments become visible annotations here
    /// exactly as they do in full-page <see cref="Convert"/>. Shared by <see cref="Convert"/>,
    /// <see cref="RenderBlock"/>, and <see cref="RenderInline"/> so fragment rendering (story remainder,
    /// dev-agent record, review findings, change log, epics overview/inventory, titles, AC lines) carries the
    /// same fidelity as a full page — otherwise a fence or comment authored inside an artifact body renders
    /// inert or invisible. [spec-epic2-deferred-debt-cleanup]</summary>
    private static string RenderDocumentHtml(MarkdownDocument document)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    /// <summary>Installs the mermaid-aware code-block renderer and the comment-aware HTML renderers onto every
    /// <see cref="HtmlRenderer"/> built from <see cref="Pipeline"/>, once per <see cref="MarkdownPipeline.Setup(IMarkdownRenderer)"/>
    /// call rather than as a separate per-call pass. Markdig runs every registered extension's
    /// <see cref="IMarkdownExtension.Setup(MarkdownPipeline, IMarkdownRenderer)"/> from inside that same
    /// <c>Setup</c>, and by the time it does, the renderer's own default object renderers (the
    /// <see cref="CodeBlockRenderer"/>/<see cref="Markdig.Renderers.Html.HtmlBlockRenderer"/>/
    /// <see cref="Markdig.Renderers.Html.Inlines.HtmlInlineRenderer"/> this wraps) are already registered by
    /// the <see cref="HtmlRenderer"/> constructor itself — so this is the idiomatic once-per-pipeline seam, not
    /// a race with pipeline setup order. [spec-epic2-deferred-debt-cleanup]</summary>
    private sealed class DocumentRendererWrappersExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            // No parser-side hooks — this extension only wraps renderer-side object renderers.
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is not HtmlRenderer htmlRenderer) return;
            UseMermaidCodeBlocks(htmlRenderer);
            UseCommentAnnotations(htmlRenderer);
        }
    }

    /// <summary>Replaces the default fenced-code renderer with one that emits mermaid blocks as
    /// <c>&lt;pre class="mermaid"&gt;</c>. Must run after the <see cref="HtmlRenderer"/> constructor's default
    /// renderer registration, which <see cref="DocumentRendererWrappersExtension"/>'s pipeline-Setup timing
    /// already guarantees.</summary>
    private static void UseMermaidCodeBlocks(HtmlRenderer renderer)
    {
        var existing = renderer.ObjectRenderers.OfType<CodeBlockRenderer>().FirstOrDefault();
        if (existing is null) return;

        renderer.ObjectRenderers.Remove(existing);
        renderer.ObjectRenderers.Add(new MermaidCodeBlockRenderer(existing));
    }

    /// <summary>Replaces the default HTML block/inline renderers with comment-aware wrappers that render
    /// <c>&lt;!-- ... --&gt;</c> as a visible, muted annotation; every other HTML block/inline is delegated to the
    /// wrapped defaults unchanged. Must run after the <see cref="HtmlRenderer"/> constructor's default renderer
    /// registration, which <see cref="DocumentRendererWrappersExtension"/>'s pipeline-Setup timing already
    /// guarantees.</summary>
    private static void UseCommentAnnotations(HtmlRenderer renderer)
    {
        var existingBlock = renderer.ObjectRenderers.OfType<Markdig.Renderers.Html.HtmlBlockRenderer>().FirstOrDefault();
        if (existingBlock is not null)
        {
            renderer.ObjectRenderers.Remove(existingBlock);
            renderer.ObjectRenderers.Add(new HtmlBlockCommentRenderer(existingBlock));
        }

        var existingInline = renderer.ObjectRenderers.OfType<Markdig.Renderers.Html.Inlines.HtmlInlineRenderer>().FirstOrDefault();
        if (existingInline is not null)
        {
            renderer.ObjectRenderers.Remove(existingInline);
            renderer.ObjectRenderers.Add(new HtmlInlineCommentRenderer(existingInline));
        }
    }

    /// <summary>Opens with FileShare.ReadWrite so an editor or the BMad tooling can hold/write the file concurrently without us blocking it.</summary>
    internal static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Strips a leading YAML frontmatter block (if any) and returns just the markdown body.</summary>
    public static string StripFrontmatter(string raw) => SplitFrontmatter(raw).Body;

    /// <summary>Renders a single line/run of markdown to inline HTML (bold, code spans, links) with no
    /// wrapping block element — used for AC lines, goal text, and other fragments pulled out of a larger doc.
    /// The unwrap only fires for a single-paragraph result: stripping the first <c>&lt;p&gt;</c> and last
    /// <c>&lt;/p&gt;</c> of a multi-paragraph body (e.g. an acceptance criterion with a trailing note) would
    /// leave unbalanced fragments ("clause…&lt;/p&gt;&lt;p&gt;note…") that break downstream HTML-aware
    /// passes and browser layout alike — those bodies keep their paragraph structure intact.</summary>
    public static string RenderInline(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var document = Markdown.Parse(markdown, Pipeline);
        var html = RenderDocumentHtml(document).Trim();
        if (html.StartsWith("<p>", StringComparison.Ordinal) && html.EndsWith("</p>", StringComparison.Ordinal)
            && html.IndexOf("<p>", 3, StringComparison.Ordinal) < 0)
        {
            html = html[3..^4];
        }
        return ColorSwatchRewriter.Rewrite(html);
    }

    /// <summary>Renders a markdown fragment as full block HTML (paragraphs, headings, lists) — used for
    /// multi-paragraph slices pulled out of a larger doc, e.g. the Overview section of epics.md, the story
    /// remainder, or the dev-agent record. Applies the mermaid-aware renderer so a <c>```mermaid</c> fence
    /// inside a fragment renders as a client-side diagram, not inert <c>&lt;code&gt;</c>; the hosting templater
    /// injects the init script when the composed page carries a mermaid block (see <see cref="Mermaid.ContainsBlock"/>).</summary>
    public static string RenderBlock(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        return ColorSwatchRewriter.Rewrite(TagTables(RenderDocumentHtml(document)));
    }

    /// <summary>Tags every Markdig-generated <c>&lt;table&gt;</c> with class <c>md-table</c> so a single CSS
    /// rule styles markdown tables consistently everywhere — including sections rendered outside the
    /// <c>.doc-body</c> article (Change Log, Review Findings, etc.). Markdig emits bare, attribute-less
    /// <c>&lt;table&gt;</c> tags for pipe tables, so the plain replace is unambiguous.</summary>
    private static string TagTables(string html) => html.Replace("<table>", "<table class=\"md-table\">");

    private static readonly Regex FrontmatterPattern = new(
        @"\A---\r?\n(?<yaml>.*?)\r?\n---\r?\n?",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static (Frontmatter Frontmatter, string Body) SplitFrontmatter(string raw)
    {
        var match = FrontmatterPattern.Match(raw);
        if (!match.Success)
        {
            return (Frontmatter.Empty, raw);
        }

        var yamlText = match.Groups["yaml"].Value;
        var body = raw[match.Length..];

        try
        {
            var map = YamlDeserializer.Deserialize<Dictionary<string, object>>(yamlText) ?? new();
            var fm = new Frontmatter
            {
                Title = GetString(map, "title"),
                Project = GetString(map, "project"),
                Date = GetString(map, "date"),
                Created = GetString(map, "created"),
                Author = GetString(map, "author"),
                Version = GetString(map, "version"),
                Status = GetString(map, "status"),
                Route = GetString(map, "route"),
                Type = GetString(map, "type"),
                Id = GetString(map, "id"),
                Companions = GetStringList(map, "companions"),
                Sources = GetStringList(map, "sources"),
            };
            return (fm, body);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // Malformed/unusual frontmatter (e.g. non-scalar top-level) — fall back to treating it as body content.
            return (Frontmatter.Empty, raw);
        }
    }

    private static string? GetString(Dictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    /// <summary>Reads a YAML sequence (e.g. <c>companions:</c>/<c>sources:</c>) as a list of strings.
    /// YamlDotNet deserializes a sequence into a <see cref="List{Object}"/>, so we project each element via
    /// <see cref="object.ToString"/>. A scalar or malformed value (not a sequence) degrades to an empty list
    /// rather than throwing, so a mis-authored frontmatter never breaks generation. [Story 2.2 Task 4]</summary>
    private static IReadOnlyList<string> GetStringList(Dictionary<string, object> map, string key)
    {
        // A YAML string is IEnumerable<char>, not IEnumerable<object>, so a scalar value falls through to the
        // empty return below — exactly the "scalar degrades to empty" contract.
        if (map.TryGetValue(key, out var value) && value is IEnumerable<object> items)
        {
            return items
                .Where(x => x is not null)
                .Select(x => x!.ToString() ?? string.Empty)
                .Where(s => s.Length > 0)
                .ToList();
        }

        return Array.Empty<string>();
    }

    private static string? ExtractFirstH1(string body)
    {
        var m = Regex.Match(body, @"^#\s+(.+)$", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string ExtractHeadingText(HeadingBlock heading)
    {
        if (heading.Inline is null)
        {
            return string.Empty;
        }

        var text = new List<string>();
        foreach (var inline in heading.Inline)
        {
            text.Add(inline.ToString() ?? string.Empty);
        }

        return string.Join(string.Empty, text);
    }

    private static string PrettifyFilename(string filename)
    {
        var spaced = filename.Replace('-', ' ').Replace('_', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }
}
