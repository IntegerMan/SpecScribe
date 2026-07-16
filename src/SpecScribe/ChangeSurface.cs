using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Projects a story's testable change footprint from standard BMAD sections (File List, Acceptance
/// Criteria, Status/Change Log) — no new authoring schema. Implements ADR 0007.</summary>
public static class ChangeSurface
{
    // Bullet or backtick-wrapped path in the File List subsection.
    private static readonly Regex FileListBullet = new(@"^\s*[-*]\s+`?([^`\n]+?)`?\s*$", RegexOptions.Compiled);
    private static readonly Regex FileListBacktick = new(@"`([^`\n]+)`", RegexOptions.Compiled);

    /// <summary>Extracts file paths from <c>## Dev Agent Record</c> → <c>### File List</c>. Returns an empty
    /// list when the subsection is absent or has no parseable paths. Never throws. [ADR 0007]</summary>
    public static IReadOnlyList<string> ExtractFileList(string? raw)
    {
        if (raw is null) return Array.Empty<string>();

        var lines = raw.Replace("\r\n", "\n").Split('\n');
        var devIdx = Array.FindIndex(lines, l => l.TrimEnd() == "## Dev Agent Record");
        if (devIdx < 0) return Array.Empty<string>();

        var sectionEnd = lines.Length;
        for (var i = devIdx + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { sectionEnd = i; break; }
        }

        var fileListIdx = -1;
        for (var i = devIdx + 1; i < sectionEnd; i++)
        {
            if (lines[i].TrimEnd() == "### File List") { fileListIdx = i; break; }
        }
        if (fileListIdx < 0) return Array.Empty<string>();

        var subEnd = sectionEnd;
        for (var i = fileListIdx + 1; i < sectionEnd; i++)
        {
            if (lines[i].StartsWith("### ", StringComparison.Ordinal)) { subEnd = i; break; }
        }

        var paths = new List<string>();
        for (var i = fileListIdx + 1; i < subEnd; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;

            var bullet = FileListBullet.Match(line);
            if (bullet.Success)
            {
                var path = bullet.Groups[1].Value.Trim();
                if (path.Length > 0) paths.Add(NormalizePath(path));
                continue;
            }

            foreach (Match m in FileListBacktick.Matches(line))
            {
                var path = m.Groups[1].Value.Trim();
                if (path.Length > 0) paths.Add(NormalizePath(path));
            }
        }

        return paths;
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/').Trim();

    /// <summary>Classifies changed file paths into surface buckets per ADR 0007. Returns human-readable
    /// labels suitable for display (e.g. "visual", "rendered UI", "plumbing (no new visible surface)").</summary>
    public static IReadOnlyList<string> ClassifyPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return Array.Empty<string>();

        var hasVisual = false;
        var hasRenderedUi = false;
        var hasTests = false;
        var hasPackaging = false;
        var hasLogic = false;

        foreach (var path in paths)
        {
            var p = path.Replace('\\', '/');
            var lower = p.ToLowerInvariant();
            var fileName = Path.GetFileName(lower);

            if (lower.EndsWith(".css", StringComparison.Ordinal) ||
                lower.EndsWith(".scss", StringComparison.Ordinal) ||
                lower.Contains("/assets/", StringComparison.Ordinal))
            {
                hasVisual = true;
                continue;
            }

            if (fileName.Contains("templater", StringComparison.Ordinal) ||
                fileName.Contains("renderadapter", StringComparison.Ordinal) ||
                fileName.Contains("viewbuilder", StringComparison.Ordinal) ||
                lower.Contains("/extension/", StringComparison.Ordinal) ||
                lower.Contains("/webview/", StringComparison.Ordinal) ||
                lower.EndsWith(".html", StringComparison.Ordinal) ||
                lower.EndsWith(".tsx", StringComparison.Ordinal) ||
                lower.EndsWith(".jsx", StringComparison.Ordinal))
            {
                hasRenderedUi = true;
                continue;
            }

            if (fileName.Contains("tests", StringComparison.Ordinal) ||
                lower.Contains("/test/", StringComparison.Ordinal) ||
                lower.Contains("/tests/", StringComparison.Ordinal) ||
                lower.Contains("/spec/", StringComparison.Ordinal))
            {
                hasTests = true;
                continue;
            }

            if (lower.Contains("/.github/", StringComparison.Ordinal) ||
                lower.Contains("/workflows/", StringComparison.Ordinal) ||
                lower.EndsWith(".csproj", StringComparison.Ordinal) ||
                lower.EndsWith(".sln", StringComparison.Ordinal) ||
                lower.EndsWith("package.json", StringComparison.Ordinal) ||
                lower.EndsWith(".yml", StringComparison.Ordinal) ||
                lower.EndsWith(".yaml", StringComparison.Ordinal) ||
                lower.Contains("manifest", StringComparison.Ordinal))
            {
                hasPackaging = true;
                continue;
            }

            hasLogic = true;
        }

        if (hasVisual || hasRenderedUi)
        {
            var labels = new List<string>();
            if (hasVisual) labels.Add("visual");
            if (hasRenderedUi) labels.Add("rendered UI");
            return labels;
        }

        if (hasTests && hasLogic && !hasPackaging)
            return new[] { "plumbing (no new visible surface)" };

        var result = new List<string>();
        if (hasLogic) result.Add("logic");
        if (hasTests) result.Add("tests");
        if (hasPackaging) result.Add("packaging");
        return result.Count > 0 ? result : new[] { "logic" };
    }

    /// <summary>Builds the host-neutral change-surface view from standard BMAD artifact sections. [ADR 0007]</summary>
    public static StoryChangeSurface Build(
        string? rawArtifact,
        string? status,
        IReadOnlyList<AcceptanceCriterion> acceptanceCriteria)
    {
        var files = ExtractFileList(rawArtifact);
        var classifications = ClassifyPaths(files);

        var checklist = acceptanceCriteria
            .Select(ac => (ac.Number, ac.PlainText))
            .ToList();

        string? shipLine = null;
        if (status is { Length: > 0 } s)
        {
            var changelog = EpicsParser.ExtractChangeLogVerification(rawArtifact);
            if (changelog is { } cl)
            {
                var dateText = cl.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                shipLine = $"{s} · {dateText} — {cl.Action}";
            }
            else
            {
                shipLine = s;
            }
        }

        return new StoryChangeSurface(classifications, checklist, files, shipLine);
    }
}
