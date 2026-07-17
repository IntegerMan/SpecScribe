using System.Globalization;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Resolves Dev Agent Record File List rows to portal links and surface kinds — code pages, sprint board,
/// story artifacts. [ADR 0007; Story 9.4]</summary>
public sealed class ChangeSurfaceFileResolver
{
    private static readonly Regex StoryArtifactName =
        new(@"^(?<epic>\d+)-(?<story>\d+)-(?<slug>.+)\.md$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Same allowlist spirit as SiteGenerator.PrettyLabel — keep known acronyms shouty in story chip titles.
    private static readonly HashSet<string> AcronymLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "PRD", "SPEC", "UX", "API", "FR", "NFR", "AC", "TOC", "DR", "UI", "SPA",
    };

    private readonly string _storyPrefix;
    private readonly IReadOnlyDictionary<string, string> _referenceMap;
    private readonly Func<string, string?> _codePageHref;

    public ChangeSurfaceFileResolver(
        string storyPrefix,
        IReadOnlyDictionary<string, string> referenceMap,
        Func<string, string?> codePageHref)
    {
        _storyPrefix = storyPrefix;
        _referenceMap = referenceMap;
        _codePageHref = codePageHref;
    }

    public ChangeSurfaceFile Resolve(ChangeSurface.FileListEntry entry)
    {
        var path = entry.Path;
        var label = ChangeSurface.FileListLabelPublic(entry.DisplayLabel);
        var isNew = entry.DisplayLabel.Contains("(new)", StringComparison.OrdinalIgnoreCase);
        var fileName = Path.GetFileName(path.Replace('\\', '/'));

        if (BmadArtifactAdapter.IsSprintStatusFile(fileName))
        {
            return new ChangeSurfaceFile(
                path, "Sprint Status", _storyPrefix + SiteNav.SprintOutputPath, ChangeSurfaceFileKind.Sprint);
        }

        if (TryParseStoryArtifact(fileName, out var storyId, out var storyLabel))
        {
            var href = LookupReference(path) ?? _storyPrefix + StoryEpicLinkifier.StoryPagePath(storyId);
            return new ChangeSurfaceFile(path, storyLabel, href, ChangeSurfaceFileKind.StoryArtifact);
        }

        var codeHref = _codePageHref(path);
        if (codeHref is { Length: > 0 })
        {
            return new ChangeSurfaceFile(
                path, label, codeHref, isNew ? ChangeSurfaceFileKind.CodeNew : ChangeSurfaceFileKind.Code);
        }

        var refHref = LookupReference(path);
        if (refHref is { Length: > 0 })
        {
            return new ChangeSurfaceFile(path, label, refHref, ChangeSurfaceFileKind.Other);
        }

        return new ChangeSurfaceFile(path, label, null, isNew ? ChangeSurfaceFileKind.CodeNew : ChangeSurfaceFileKind.Other);
    }

    public FileListLink? ResolveForDevRecord(string displayPath)
    {
        var entry = new ChangeSurface.FileListEntry(
            ChangeSurface.NormalizeFileListPath(displayPath), displayPath);
        var file = Resolve(entry);
        return file.Href is { Length: > 0 } href
            ? new FileListLink(href, ChangeSurfaceFileKindCssClass(file.Kind))
            : null;
    }

    public static string ChangeSurfaceFileKindCssClass(ChangeSurfaceFileKind kind) => kind switch
    {
        ChangeSurfaceFileKind.CodeNew => "touch-file touch-file-new",
        ChangeSurfaceFileKind.Sprint => "touch-file touch-file-sprint",
        ChangeSurfaceFileKind.StoryArtifact => "touch-file touch-file-story",
        ChangeSurfaceFileKind.Code => "touch-file touch-file-code",
        _ => "touch-file",
    };

    /// <summary>Sprint board + story-artifact rows belong in the change-surface <c>Updated</c> chip row,
    /// not the Touched file grid.</summary>
    public static bool IsUpdatedArtifact(ChangeSurfaceFileKind kind)
        => kind is ChangeSurfaceFileKind.Sprint or ChangeSurfaceFileKind.StoryArtifact;

    private string? LookupReference(string path)
    {
        var norm = PathUtil.NormalizeSlashes(path.Replace('\\', '/'));
        if (_referenceMap.TryGetValue(norm, out var href))
            return _storyPrefix + href;

        var name = Path.GetFileName(norm);
        foreach (var (key, mapped) in _referenceMap)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase)
                || key.EndsWith('/' + name, StringComparison.OrdinalIgnoreCase))
            {
                return _storyPrefix + mapped;
            }
        }

        return null;
    }

    private static bool TryParseStoryArtifact(string fileName, out string storyId, out string storyLabel)
    {
        storyId = string.Empty;
        storyLabel = string.Empty;
        var m = StoryArtifactName.Match(fileName);
        if (!m.Success) return false;
        storyId = $"{m.Groups["epic"].Value}.{m.Groups["story"].Value}";
        storyLabel = PrettyStorySlug(m.Groups["slug"].Value);
        if (storyLabel.Length == 0) storyLabel = $"Story {storyId}";
        return true;
    }

    /// <summary>Filename slug → chip title (<c>nfr-and-ux-dr-coverage-maps</c> →
    /// <c>NFR and UX DR Coverage Maps</c>). Keeps known acronyms uppercase.</summary>
    public static string PrettyStorySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return string.Empty;
        var ti = CultureInfo.InvariantCulture.TextInfo;
        var words = slug.Split('-', '_', ' ')
            .Where(w => w.Length > 0)
            .Select(w =>
            {
                if (AcronymLabels.Contains(w)) return w.ToUpperInvariant();
                if (string.Equals(w, "and", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "or", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "of", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "on", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "the", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "a", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "an", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "in", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "to", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(w, "for", StringComparison.OrdinalIgnoreCase))
                {
                    return w.ToLowerInvariant();
                }
                return ti.ToTitleCase(w.ToLowerInvariant());
            });
        var joined = string.Join(" ", words);
        if (joined.Length == 0) return joined;
        return char.ToUpperInvariant(joined[0]) + joined[1..];
    }
}

/// <summary>Optional href + CSS class for a File List row link.</summary>
public sealed record FileListLink(string Href, string CssClass);
