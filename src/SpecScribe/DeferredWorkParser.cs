using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Structured model of a deferred-work note: provenance groups and per-item resolved state.
/// <see cref="IsStructured"/> is false when the note has no <c>## Deferred from:</c> headings — callers
/// fall back to the plain body. [Story 9.6]</summary>
public sealed record DeferredWorkModel(
    bool IsStructured,
    IReadOnlyList<DeferredWorkGroup> Groups,
    string? PlainBodyHtml = null);

/// <summary>One <c>## Deferred from:</c> section with optional source-story provenance. [Story 9.6]</summary>
public sealed record DeferredWorkGroup(
    string ProvenanceLabel,
    string? SourceStoryId,
    string? SourceStoryHref,
    IReadOnlyList<DeferredWorkItem> Items);

/// <summary>One top-level deferred-work list item. [Story 9.6]</summary>
public sealed record DeferredWorkItem(
    string BodyHtml,
    bool Resolved,
    string? ResolvingRef,
    string? ResolvingHref);

/// <summary>Pure, never-throws parser that turns a deferred-work markdown note into
/// <see cref="DeferredWorkModel"/>. Derives provenance/resolution from existing prose — no new authoring
/// schema. [Story 9.6]</summary>
public static class DeferredWorkParser
{
    private static readonly Regex DeferredHeading = new(
        @"^##\s+Deferred from:\s*(?<label>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex ResolvedMarker = new(
        @"\[?\s*RESOLVED\b|\*\*\[RESOLVED",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Parses <paramref name="markdown"/> into a structured model. On any failure or when no
    /// Deferred-from headings exist, returns <c>IsStructured = false</c> with an optional plain-body
    /// fallback (rendered from <paramref name="fallbackBodyHtml"/> when supplied).</summary>
    public static DeferredWorkModel Parse(
        string? markdown,
        IReadOnlyDictionary<string, string>? hrefMap = null,
        string outputRelativePrefix = "",
        string? fallbackBodyHtml = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return Unstructured(fallbackBodyHtml);

            var matches = DeferredHeading.Matches(markdown);
            if (matches.Count == 0)
                return Unstructured(fallbackBodyHtml ?? MarkdownConverter.RenderBlock(markdown));

            var groups = new List<DeferredWorkGroup>();
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var label = match.Groups["label"].Value.Trim();
                var start = match.Index + match.Length;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
                if (end < start) end = start;
                var section = markdown[start..end];

                var sourceFile = FollowUpRefs.SourceSpecFileFromText(section)
                    ?? ExtractKeyFromLabel(label);
                var sourceId = sourceFile is null ? null : FollowUpRefs.StoryIdFromKey(sourceFile);
                // Prefer story id when it's a story key; otherwise keep the filename as the ref label.
                var sourceHref = FollowUpRefs.ResolveHref(sourceFile ?? sourceId, hrefMap, outputRelativePrefix);

                var items = ParseItems(section, hrefMap, outputRelativePrefix);
                groups.Add(new DeferredWorkGroup(label, sourceId, sourceHref, items));
            }

            return new DeferredWorkModel(true, groups);
        }
        catch
        {
            return Unstructured(fallbackBodyHtml);
        }
    }

    private static DeferredWorkModel Unstructured(string? bodyHtml) =>
        new(false, Array.Empty<DeferredWorkGroup>(), bodyHtml);

    private static IReadOnlyList<DeferredWorkItem> ParseItems(
        string section,
        IReadOnlyDictionary<string, string>? hrefMap,
        string prefix)
    {
        var items = new List<DeferredWorkItem>();
        var lines = section.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var current = new List<string>();

        void Flush()
        {
            if (current.Count == 0) return;
            // Strip the leading "- " from the first line.
            var first = current[0];
            if (first.StartsWith("- ", StringComparison.Ordinal))
                first = first[2..];
            else if (first.StartsWith("-\t", StringComparison.Ordinal))
                first = first[2..];

            var md = first;
            if (current.Count > 1)
                md = first + "\n" + string.Join("\n", current.Skip(1));

            var bodyHtml = MarkdownConverter.RenderBlock(md.Trim());
            var resolved = bodyHtml.Contains("<del", StringComparison.OrdinalIgnoreCase)
                || ResolvedMarker.IsMatch(md);

            string? resolvingRef = FollowUpRefs.ResolvingStoryIdFromText(md);
            string? resolvingHref = resolvingRef is null
                ? null
                : FollowUpRefs.ResolveHref(resolvingRef, hrefMap, prefix);

            // Item-level source_spec can also name a resolving/originating story when no RESOLVED marker.
            if (resolvingHref is null)
            {
                var itemSpec = FollowUpRefs.SourceSpecFileFromText(md);
                if (itemSpec is not null)
                {
                    resolvingRef ??= FollowUpRefs.StoryIdFromKey(itemSpec) ?? itemSpec;
                    resolvingHref = FollowUpRefs.ResolveHref(itemSpec, hrefMap, prefix);
                }
            }

            items.Add(new DeferredWorkItem(bodyHtml, resolved, resolvingRef, resolvingHref));
            current.Clear();
        }

        foreach (var line in lines)
        {
            // Top-level list item: starts with "- " at column 0 (no leading whitespace).
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("-\t", StringComparison.Ordinal))
            {
                Flush();
                current.Add(line);
            }
            else if (current.Count > 0)
            {
                // Continuation (nested bullets / prose) stays with the current item.
                // Stop if we hit another ## heading (shouldn't happen — section already sliced).
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    Flush();
                    break;
                }
                current.Add(line);
            }
            // Pre-list prose (e.g. a bare source_spec line) is ignored as an item — group provenance
            // already captured it via SourceSpecFileFromText on the whole section.
        }
        Flush();
        return items;
    }

    private static string? ExtractKeyFromLabel(string label)
    {
        // "code review of 8-8-generation-time-recency-signals.md (2026-07-15)"
        var m = Regex.Match(label, @"\b(\d+-\d+-[a-z0-9-]+(?:\.md)?)\b", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(label, @"\b(spec-[a-z0-9-]+(?:\.md)?)\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }
}
