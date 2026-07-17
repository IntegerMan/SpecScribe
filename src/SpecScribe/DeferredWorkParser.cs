using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Structured model of a deferred-work note: provenance groups and per-item resolved state.
/// <see cref="IsStructured"/> is false when the note has no <c>## Deferred from:</c> headings — callers
/// fall back to the plain body. [Story 9.6]</summary>
public sealed record DeferredWorkModel(
    bool IsStructured,
    IReadOnlyList<DeferredWorkGroup> Groups,
    string? PlainBodyHtml = null,
    string? PreambleHtml = null);

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

    // Bracketed forms only — bare "RESOLVED" in prose must not flip status (e.g. "not RESOLVED yet").
    private static readonly Regex ResolvedMarker = new(
        @"\*\*\[\s*RESOLVED\b|\[\s*RESOLVED\b",
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

            // Headings with no parseable list items → keep the plain body rather than empty groups.
            var nonEmpty = groups.Where(g => g.Items.Count > 0).ToList();
            if (nonEmpty.Count == 0)
                return Unstructured(fallbackBodyHtml ?? MarkdownConverter.RenderBlock(markdown));

            string? preambleHtml = null;
            var preambleMd = markdown[..matches[0].Index].Trim();
            if (preambleMd.Length > 0)
                preambleHtml = MarkdownConverter.RenderBlock(preambleMd);

            return new DeferredWorkModel(true, nonEmpty, PreambleHtml: preambleHtml);
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

            // Item-level source_spec is origin metadata on open items (group provenance already covers it).
            // Only promote it to Resolving* when the item is actually resolved and no RESOLVED-in id was found.
            if (resolvingHref is null && resolved)
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
                // Non-Deferred ## inside a section must not drop later list items — flush and keep scanning.
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    Flush();
                    continue;
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
        // Prefer explicit story review headings: "code review of story-3-8" / "story-3.5".
        // Must come before the N-M-slug pattern so trailing dates like (2026-07-09) aren't
        // mistaken for artifact keys.
        var m = Regex.Match(label, @"\b(story-\d+[.-]\d+)\b", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        // Full artifact slug: "8-8-generation-time-recency-signals.md" — require a letter in the
        // slug segment so bare dates (2026-07-09) never match.
        m = Regex.Match(label, @"\b(\d+-\d+-[a-z0-9-]*[a-z][a-z0-9-]*(?:\.md)?)\b", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(label, @"\b(spec-[a-z0-9-]+(?:\.md)?)\b", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }
}
