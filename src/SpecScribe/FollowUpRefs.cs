using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Best-effort resolving links for follow-up surfaces: story-key filenames (<c>N-M-slug</c>,
/// <c>spec-*.md</c>) and "Story N.M"/"Epic N" prose. Pure and deterministic — no I/O. [Story 9.6]</summary>
public static class FollowUpRefs
{
    // Story artifact filenames: "8-8-generation-time-recency-signals" or with .md suffix.
    private static readonly Regex StoryKeyPattern = new(
        @"\b(?<epic>\d+)-(?<story>\d+)-[a-z0-9][a-z0-9-]*(?:\.md)?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Quick-dev / one-shot specs: "spec-webview-doc-page-surfaces" or with .md.
    private static readonly Regex SpecKeyPattern = new(
        @"\bspec-[a-z0-9][a-z0-9-]*(?:\.md)?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // "RESOLVED in 6.11" / "RESOLVED in Story 6.4" / "Resolved … `spec-…`"
    private static readonly Regex ResolvedInStory = new(
        @"RESOLVED\s+in\s+(?:Story\s+)?(?<epic>\d+)\.(?<story>\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SourceSpecLine = new(
        @"source_spec:\s*`?(?<file>[^`\s\n]+)`?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Builds a filename-stem → output-href map for docs under implementation-artifacts
    /// (spec pages) plus every story's generated page path. Keys are bare stems and <c>.md</c> forms.</summary>
    public static IReadOnlyDictionary<string, string> BuildHrefMap(EpicsModel? epics, IEnumerable<DocModel>? docs)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (epics is not null)
        {
            foreach (var epic in epics.Epics)
            {
                foreach (var story in epic.Stories)
                {
                    var href = story.ArtifactOutputPath ?? StoryEpicLinkifier.StoryPagePath(story.Id);
                    Add(map, story.Id.Replace('.', '-'), href);
                    Add(map, story.Id, href);
                    if (story.ArtifactSourcePath is { Length: > 0 } src)
                    {
                        var slash = src.LastIndexOf('/');
                        var file = slash >= 0 ? src[(slash + 1)..] : src;
                        Add(map, file, href);
                        Add(map, Path.GetFileNameWithoutExtension(file), href);
                    }
                }
            }
        }

        if (docs is not null)
        {
            foreach (var doc in docs)
            {
                var norm = PathUtil.NormalizeSlashes(doc.SourceRelativePath);
                if (!BmadArtifactAdapter.IsUnderImplementationArtifacts(norm)) continue;
                var slash = norm.LastIndexOf('/');
                var file = slash >= 0 ? norm[(slash + 1)..] : norm;
                var href = PathUtil.NormalizeSlashes(doc.OutputRelativePath);
                Add(map, file, href);
                Add(map, Path.GetFileNameWithoutExtension(file), href);
            }
        }

        return map;
    }

    /// <summary>Linkifies a plain-text fragment for visible display only — HTML-escapes first, then story-key
    /// and Story/Epic mentions. Never run on a copyable command payload.</summary>
    public static string LinkifyVisibleText(
        string plainText,
        EpicsModel? epicsModel,
        IReadOnlyDictionary<string, string>? hrefMap,
        string outputRelativePrefix)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var html = PathUtil.Html(plainText);
        html = LinkifyKeys(html, hrefMap, outputRelativePrefix);
        if (epicsModel is { Epics.Count: > 0 })
        {
            html = StoryEpicLinkifier.Linkify(html, epicsModel, outputRelativePrefix);
        }
        return html;
    }

    /// <summary>Resolves a story id ("N.M") from a filename or review-heading token —
    /// <c>8-8-slug.md</c>, <c>story-3-8</c>, <c>story-3.8</c>, or bare <c>3.8</c>.</summary>
    public static string? StoryIdFromKey(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var bare = token.Trim().Trim('`');
        if (bare.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            bare = bare[..^3];
        if (bare.StartsWith("story-", StringComparison.OrdinalIgnoreCase))
            bare = bare[6..];

        // "3.8" / leftover after stripping story-
        var dotted = Regex.Match(bare, @"^(?<epic>\d+)\.(?<story>\d+)$");
        if (dotted.Success
            && int.TryParse(dotted.Groups["epic"].Value, out var de)
            && int.TryParse(dotted.Groups["story"].Value, out var ds))
            return $"{de}.{ds}";

        var m = Regex.Match(bare, @"^(?<epic>\d+)-(?<story>\d+)(?:-|$)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups["epic"].Value, out var epic) || !int.TryParse(m.Groups["story"].Value, out var story))
            return null;
        return $"{epic}.{story}";
    }

    /// <summary>Extracts a resolving story id from RESOLVED markers in item markdown, if present.</summary>
    public static string? ResolvingStoryIdFromText(string markdown)
    {
        var m = ResolvedInStory.Match(markdown);
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups["epic"].Value, out var epic) || !int.TryParse(m.Groups["story"].Value, out var story))
            return null;
        return $"{epic}.{story}";
    }

    /// <summary>Extracts a <c>source_spec:</c> filename token from markdown text, if present.</summary>
    public static string? SourceSpecFileFromText(string markdown)
    {
        var m = SourceSpecLine.Match(markdown);
        return m.Success ? m.Groups["file"].Value.Trim() : null;
    }

    /// <summary>Resolves a filename or story id to an output href via the map, falling back to the always-
    /// present story placeholder page for <c>N-M-*</c> keys.</summary>
    public static string? ResolveHref(string? token, IReadOnlyDictionary<string, string>? hrefMap, string outputRelativePrefix = "")
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var bare = token.Trim().Trim('`');
        if (hrefMap is not null)
        {
            if (hrefMap.TryGetValue(bare, out var href)) return outputRelativePrefix + href;
            var stem = Path.GetFileNameWithoutExtension(bare);
            if (hrefMap.TryGetValue(stem, out href)) return outputRelativePrefix + href;
        }

        var storyId = StoryIdFromKey(bare);
        if (storyId is not null)
            return outputRelativePrefix + StoryEpicLinkifier.StoryPagePath(storyId);

        // "N.M" prose form
        if (Regex.IsMatch(bare, @"^\d+\.\d+$"))
            return outputRelativePrefix + StoryEpicLinkifier.StoryPagePath(bare);

        return null;
    }

    private static string LinkifyKeys(string html, IReadOnlyDictionary<string, string>? hrefMap, string prefix)
    {
        html = SpecKeyPattern.Replace(html, m =>
        {
            var href = ResolveHref(m.Value, hrefMap, prefix);
            return href is null ? m.Value : $"<a class=\"follow-up-ref\" href=\"{PathUtil.Html(href)}\">{m.Value}</a>";
        });

        return StoryKeyPattern.Replace(html, m =>
        {
            // Avoid double-wrapping already-linked text.
            var href = ResolveHref(m.Value, hrefMap, prefix);
            return href is null ? m.Value : $"<a class=\"follow-up-ref\" href=\"{PathUtil.Html(href)}\">{m.Value}</a>";
        });
    }

    private static void Add(Dictionary<string, string> map, string key, string href)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        map.TryAdd(key, PathUtil.NormalizeSlashes(href));
    }
}
