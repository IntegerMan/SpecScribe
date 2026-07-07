using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace SpecScribe;

/// <summary>Parses <c>sprint-status.yaml</c> into an order-preserving <see cref="SprintStatus"/>. Reuses the
/// already-referenced YamlDotNet deserializer (the same approach <see cref="MarkdownConverter"/> uses for
/// frontmatter) — no new dependency. Missing, unreadable, malformed, or development_status-less input all
/// degrade to <c>null</c>, the single "no sprint data" signal every downstream surface (page, widget, nav)
/// gates on, so partial/absent tracking data never throws or produces a broken link (AC#1, NFR2). [Story 2.3 Task 1]</summary>
public static class SprintStatusParser
{
    // A plain deserializer: we read into Dictionary<string, object>, so yaml keys are taken verbatim
    // (development_status / last_updated / action_items) rather than mapped onto typed properties.
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly Regex EpicKey = new(@"^epic-(?<n>\d+)$", RegexOptions.Compiled);
    private static readonly Regex RetroKey = new(@"^epic-(?<n>\d+)-retrospective$", RegexOptions.Compiled);
    private static readonly Regex StoryKey = new(@"^(?<epic>\d+)-(?<story>\d+)-", RegexOptions.Compiled);

    /// <summary>Reads and parses the yaml at <paramref name="fullPath"/>. Returns <c>null</c> when the file is
    /// absent or unreadable — matching the "matched by presence, omit when absent" discipline of README/epics/ADRs.</summary>
    public static SprintStatus? ParseFile(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;
        try
        {
            return Parse(MarkdownConverter.ReadAllTextShared(fullPath));
        }
        catch (IOException)
        {
            // Mid-write from another tool — treated as "no sprint data" for this pass rather than throwing.
            return null;
        }
    }

    /// <summary>Parses yaml text (exposed for unit tests using inline strings, no disk needed). Returns
    /// <c>null</c> on malformed yaml or a missing/empty <c>development_status</c> map.</summary>
    public static SprintStatus? Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;

        Dictionary<string, object>? root;
        try
        {
            root = Deserializer.Deserialize<Dictionary<string, object>>(yaml);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            // Malformed yaml — same catch MarkdownConverter.SplitFrontmatter uses; degrade to "no sprint data".
            return null;
        }

        if (root is null) return null;

        var devPairs = root.TryGetValue("development_status", out var ds) ? AsPairs(ds) : Array.Empty<(string, object?)>();
        if (devPairs.Count == 0) return null;

        // YamlDotNet preserves mapping order into the deserialized dictionary, so entries stay in file order
        // (epic-1, its 1-*, epic-2, its 2-*, …) — do not re-sort.
        var entries = new List<SprintEntry>();
        foreach (var (key, value) in devPairs)
        {
            var status = value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(status)) continue;

            if (RetroKey.Match(key) is { Success: true } rm)
            {
                entries.Add(new SprintEntry(SprintEntryKind.Retrospective, key, int.Parse(rm.Groups["n"].Value), null, status));
            }
            else if (EpicKey.Match(key) is { Success: true } em)
            {
                entries.Add(new SprintEntry(SprintEntryKind.Epic, key, int.Parse(em.Groups["n"].Value), null, status));
            }
            else if (StoryKey.Match(key) is { Success: true } sm)
            {
                entries.Add(new SprintEntry(SprintEntryKind.Story, key, int.Parse(sm.Groups["epic"].Value), int.Parse(sm.Groups["story"].Value), status));
            }
            // Keys matching none of the patterns are ignored (forward-compat).
        }

        if (entries.Count == 0) return null;

        var lastUpdated = root.TryGetValue("last_updated", out var lu) ? lu?.ToString()?.Trim() : null;
        var actionItems = ParseActionItems(root);

        return new SprintStatus
        {
            Entries = entries,
            LastUpdated = string.IsNullOrEmpty(lastUpdated) ? null : lastUpdated,
            ActionItems = actionItems,
        };
    }

    /// <summary>Reads the optional <c>action_items:</c> sequence (each element a map with
    /// <c>action</c>/<c>status</c>/<c>epic</c>/<c>owner</c>). Absent, scalar, or malformed → empty (never an
    /// error). An element with no action text is skipped. [Story 2.3 Task 1/3]</summary>
    private static IReadOnlyList<SprintActionItem> ParseActionItems(Dictionary<string, object> root)
    {
        if (!root.TryGetValue("action_items", out var value) || value is not IEnumerable<object> items)
        {
            return Array.Empty<SprintActionItem>();
        }

        var result = new List<SprintActionItem>();
        foreach (var item in items)
        {
            var fields = AsPairs(item);
            if (fields.Count == 0) continue;

            var lookup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in fields) lookup[k] = v?.ToString();

            var action = FirstNonEmpty(lookup, "action", "text", "description", "item");
            if (string.IsNullOrWhiteSpace(action)) continue;

            var status = FirstNonEmpty(lookup, "status");
            var owner = FirstNonEmpty(lookup, "owner");
            int? epic = int.TryParse(FirstNonEmpty(lookup, "epic"), out var e) ? e : null;

            result.Add(new SprintActionItem(action!.Trim(), string.IsNullOrWhiteSpace(status) ? "open" : status!.Trim(), epic, owner));
        }
        return result;
    }

    private static string? FirstNonEmpty(IReadOnlyDictionary<string, string?> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }

    /// <summary>Flattens a deserialized yaml mapping (whatever concrete dictionary YamlDotNet produced — nested
    /// maps come back as <see cref="IDictionary{Object,Object}"/>, the root as <c>Dictionary&lt;string,object&gt;</c>)
    /// into an ordered key→value list, coercing keys to strings. Non-map values yield an empty list.</summary>
    private static IReadOnlyList<(string Key, object? Value)> AsPairs(object? value)
    {
        var list = new List<(string, object?)>();
        switch (value)
        {
            case IDictionary<object, object> od:
                foreach (var kv in od) list.Add((kv.Key?.ToString() ?? string.Empty, kv.Value));
                break;
            case IDictionary<string, object> sd:
                foreach (var kv in sd) list.Add((kv.Key, kv.Value));
                break;
        }
        return list;
    }
}
