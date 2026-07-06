using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpecScribe;

/// <summary>Converts a single BMad markdown file (frontmatter + body) into a <see cref="DocModel"/>. Never opens files exclusively — BMad tooling may still be writing them.</summary>
public static class MarkdownConverter
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

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

    /// <summary>Renders a parsed markdown document to HTML with the mermaid-aware code-block renderer applied,
    /// so <c>```mermaid</c> fences become <c>&lt;pre class="mermaid"&gt;</c> here exactly as they do in full-page
    /// <see cref="Convert"/>. Shared by <see cref="Convert"/> and <see cref="RenderBlock"/> so fragment rendering
    /// (story remainder, dev-agent record, review findings, change log, epics overview/inventory) carries the same
    /// mermaid fidelity as a full page — otherwise a fence authored inside an artifact body renders inert.</summary>
    private static string RenderDocumentHtml(MarkdownDocument document)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        UseMermaidCodeBlocks(renderer);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }

    /// <summary>Replaces the default fenced-code renderer with one that emits mermaid blocks as
    /// <c>&lt;pre class="mermaid"&gt;</c>. Must run after <see cref="MarkdownPipeline.Setup(IMarkdownRenderer)"/>
    /// so the default renderer it wraps is already registered.</summary>
    private static void UseMermaidCodeBlocks(HtmlRenderer renderer)
    {
        var existing = renderer.ObjectRenderers.OfType<CodeBlockRenderer>().FirstOrDefault();
        if (existing is null) return;

        renderer.ObjectRenderers.Remove(existing);
        renderer.ObjectRenderers.Add(new MermaidCodeBlockRenderer(existing));
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
    /// wrapping block element — used for AC lines, goal text, and other fragments pulled out of a larger doc.</summary>
    public static string RenderInline(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdown.ToHtml(markdown, Pipeline).Trim();
        if (html.StartsWith("<p>", StringComparison.Ordinal) && html.EndsWith("</p>", StringComparison.Ordinal))
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
                Author = GetString(map, "author"),
                Version = GetString(map, "version"),
                Status = GetString(map, "status"),
                Route = GetString(map, "route"),
                Type = GetString(map, "type"),
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
