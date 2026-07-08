using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace SpecScribe;

/// <summary>Parses <c>sprint-status.yaml</c> into an order-preserving <see cref="SprintStatus"/>. Reuses the
/// already-referenced YamlDotNet deserializer (no new dependency), but isolates the blocks it actually needs
/// (<c>development_status</c>, <c>action_items</c>) before parsing them. That isolation matters because real
/// BMad-generated tracking files carry lines that are <em>not</em> valid YAML — e.g.
/// <c>story_location: {project-root}/…</c>, whose unquoted <c>{</c> opens a flow mapping — so a
/// whole-document deserialize would throw on an unrelated sibling key and lose the whole ledger. Missing,
/// unreadable, or malformed <c>development_status</c> all degrade to <c>null</c>, the single "no sprint data"
/// signal every downstream surface (page, widget, nav) gates on (AC#1, NFR2). [Story 2.3 Task 1]</summary>
public static class SprintStatusParser
{
    // A plain deserializer: we read isolated blocks into Dictionary<string, object>, so yaml keys are taken
    // verbatim (development_status / action_items) rather than mapped onto typed properties.
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly Regex EpicKey = new(@"^epic-(?<n>\d+)$", RegexOptions.Compiled);
    private static readonly Regex RetroKey = new(@"^epic-(?<n>\d+)-retrospective$", RegexOptions.Compiled);
    private static readonly Regex StoryKey = new(@"^(?<epic>\d+)-(?<story>\d+)-", RegexOptions.Compiled);
    private static readonly Regex LastUpdatedLine = new(@"(?m)^last_updated:[ \t]*(?<v>.+?)[ \t]*$", RegexOptions.Compiled);

    /// <summary>Reads and parses the yaml at <paramref name="fullPath"/>. Returns <c>null</c> when the file is
    /// absent or unreadable — matching the "matched by presence, omit when absent" discipline of README/epics/ADRs.</summary>
    public static SprintStatus? ParseFile(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;
        try
        {
            return Parse(MarkdownConverter.ReadAllTextShared(fullPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Mid-write from another tool, or a permissions/access issue — treated as "no sprint data" for
            // this pass rather than throwing. (Deliberately not a bare `catch (Exception)`: a real parser bug
            // should still surface loudly rather than silently degrade.) [Story 2.3 review]
            return null;
        }
    }

    /// <summary>Parses yaml text (exposed for unit tests using inline strings, no disk needed). Returns
    /// <c>null</c> on a missing/empty/malformed <c>development_status</c> map.</summary>
    public static SprintStatus? Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;

        var devPairs = ParseMapBlock(yaml, "development_status");
        if (devPairs is null || devPairs.Count == 0) return null;

        // YamlDotNet preserves mapping order into the deserialized dictionary, so entries stay in file order
        // (epic-1, its 1-*, epic-2, its 2-*, …) — do not re-sort.
        var entries = new List<SprintEntry>();
        foreach (var (key, value) in devPairs)
        {
            var status = value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(status)) continue;

            // TryParse, not Parse: a key with an absurdly long digit run (still matched by the regex) would
            // otherwise throw OverflowException here and crash the whole site generation, contradicting the
            // "malformed data degrades to null" contract this parser exists to uphold. [Story 2.3 review]
            if (RetroKey.Match(key) is { Success: true } rm)
            {
                if (int.TryParse(rm.Groups["n"].Value, out var rn)) entries.Add(new SprintEntry(SprintEntryKind.Retrospective, key, rn, null, status));
            }
            else if (EpicKey.Match(key) is { Success: true } em)
            {
                if (int.TryParse(em.Groups["n"].Value, out var en)) entries.Add(new SprintEntry(SprintEntryKind.Epic, key, en, null, status));
            }
            else if (StoryKey.Match(key) is { Success: true } sm)
            {
                if (int.TryParse(sm.Groups["epic"].Value, out var se) && int.TryParse(sm.Groups["story"].Value, out var ss))
                    entries.Add(new SprintEntry(SprintEntryKind.Story, key, se, ss, status));
            }
            // Keys matching none of the patterns, or with an unparseable number, are ignored (forward-compat).
        }

        if (entries.Count == 0) return null;

        var lastUpdated = ExtractLastUpdated(yaml);
        var actionItems = ParseActionItems(yaml);

        return new SprintStatus
        {
            Entries = entries,
            LastUpdated = lastUpdated,
            ActionItems = actionItems,
        };
    }

    /// <summary>Deserializes a single top-level mapping block (e.g. <c>development_status:</c>) in isolation,
    /// so an invalid sibling key elsewhere in the file can't fail this parse. Returns the ordered key→value
    /// pairs, or <c>null</c> when the block is absent or itself malformed.</summary>
    private static IReadOnlyList<(string Key, object? Value)>? ParseMapBlock(string yaml, string topLevelKey)
    {
        var block = ExtractTopLevelBlock(yaml, topLevelKey);
        if (block is null) return null;
        try
        {
            var root = Deserializer.Deserialize<Dictionary<string, object>>(block);
            return root is not null && root.TryGetValue(topLevelKey, out var value) ? AsPairs(value) : null;
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }

    /// <summary>Reads the optional <c>action_items:</c> sequence in isolation (each element a map with
    /// <c>action</c>/<c>status</c>/<c>epic</c>/<c>owner</c>). Absent, scalar, or malformed → empty (never an
    /// error). An element with no action text is skipped. [Story 2.3 Task 1/3]</summary>
    private static IReadOnlyList<SprintActionItem> ParseActionItems(string yaml)
    {
        var block = ExtractTopLevelBlock(yaml, "action_items");
        if (block is null) return Array.Empty<SprintActionItem>();

        object? items;
        try
        {
            var root = Deserializer.Deserialize<Dictionary<string, object>>(block);
            if (root is null || !root.TryGetValue("action_items", out items) || items is not IEnumerable<object> seq)
            {
                return Array.Empty<SprintActionItem>();
            }
            items = seq;
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return Array.Empty<SprintActionItem>();
        }

        var result = new List<SprintActionItem>();
        foreach (var item in (IEnumerable<object>)items)
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

    private static string? ExtractLastUpdated(string yaml)
    {
        var m = LastUpdatedLine.Match(yaml);
        if (!m.Success) return null;
        var value = m.Groups["v"].Value.Trim().Trim('"', '\'');
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Slices out one top-level block: the line <c>key:</c> (no leading whitespace) plus every
    /// following line until the next top-level, non-comment key (or EOF). Comment and blank lines inside the
    /// block are kept (YAML ignores them). Returns <c>null</c> when the key isn't present at the top level.</summary>
    private static string? ExtractTopLevelBlock(string yaml, string key)
    {
        var lines = yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var header = key + ":";
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (start < 0)
            {
                if (line.StartsWith(header, StringComparison.Ordinal)
                    && (line.Length == header.Length || line[header.Length] is ' ' or '\t'))
                {
                    start = i;
                }
                continue;
            }

            // End at the next top-level key (a non-blank line that doesn't start with whitespace or '#').
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith("#", StringComparison.Ordinal))
            {
                return string.Join("\n", lines[start..i]);
            }
        }
        return start >= 0 ? string.Join("\n", lines[start..]) : null;
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
