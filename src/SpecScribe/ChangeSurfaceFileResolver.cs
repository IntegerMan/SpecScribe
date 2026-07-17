using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Resolves Dev Agent Record File List rows to portal links and surface kinds — code pages, sprint board,
/// story artifacts. [ADR 0007; Story 9.4]</summary>
public sealed class ChangeSurfaceFileResolver
{
    private static readonly Regex StoryArtifactName =
        new(@"^(?<epic>\d+)-(?<story>\d+)-.+\.md$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                path, label, _storyPrefix + SiteNav.SprintOutputPath, ChangeSurfaceFileKind.Sprint);
        }

        if (TryParseStoryArtifactId(fileName, out var storyId))
        {
            var href = LookupReference(path) ?? _storyPrefix + StoryEpicLinkifier.StoryPagePath(storyId);
            return new ChangeSurfaceFile(path, label, href, ChangeSurfaceFileKind.StoryArtifact);
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

    private static bool TryParseStoryArtifactId(string fileName, out string storyId)
    {
        storyId = string.Empty;
        var m = StoryArtifactName.Match(fileName);
        if (!m.Success) return false;
        storyId = $"{m.Groups["epic"].Value}.{m.Groups["story"].Value}";
        return true;
    }
}

/// <summary>Optional href + CSS class for a File List row link.</summary>
public sealed record FileListLink(string Href, string CssClass);
