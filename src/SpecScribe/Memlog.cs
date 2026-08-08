using System.Globalization;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>One parsed body line of a <c>.memlog.md</c>: <c>- (type) text</c>, where <paramref name="Type"/> is
/// null for an untagged <c>- text</c> entry. The host skill owns the type vocabulary — <c>memlog.py</c> itself
/// enforces none — so this record never validates the word, it only carries it. [Story 18.4]</summary>
public sealed record MemlogEntry(string? Type, string Text);

/// <summary>The <c>.memlog.md</c> file shape written by the shared core tool <c>_bmad/scripts/memlog.py</c>:
/// a plain <c>key: value</c> frontmatter block followed by an append-only body of one-line
/// <c>- (type) text</c> entries.
/// <para><b>Not forge-specific.</b> <c>memlog.py</c> is a <em>core</em> script used by at least five BMad skills
/// (product brief, PRD, UX, spec, forge), so "a directory holding a <c>.memlog.md</c>" identifies a
/// <em>session workspace</em> — never a particular skill's. Deciding whose session it is belongs to the
/// consumer (see <see cref="IdeaDiscovery"/>'s cascade), not here.</para>
/// <para>The ONE seam for reading this file shape. <see cref="SiteGenerator.BuildMemlogMap"/> (the coverage
/// panel's freshness enrichment) and <see cref="IdeaDiscovery"/> (the Ideas surface) both read the same file for
/// different reasons; a second <c>updated:</c> regex or a second frontmatter split beside these would be exactly
/// the kind of duplicate classifier the project keeps out. [Story 3.3; relocated + widened in Story 18.4]</para>
/// </summary>
public static class Memlog
{
    /// <summary>The fixed memlog filename — a dotfile, so it is invisible to the <c>*.md</c> source enumeration
    /// (<see cref="PathUtil.IsIgnoredSourceFile"/>) and every reader must discover it explicitly.</summary>
    public const string FileName = ".memlog.md";

    // The memlog frontmatter's single "updated: <date>" field — a one-line regex read (like ForgeOptions'
    // project_name read), NOT a full YAML parse. Captures just the yyyy-MM-dd prefix of the timestamp.
    private static readonly Regex UpdatedPattern = TimedRegex.New(
        @"^\s*updated:\s*(?<date>\d{4}-\d{2}-\d{2})", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>The memlog's <c>updated:</c> day, or null when the field is absent or unparseable. Never throws:
    /// a memlog with no usable date simply adds no enrichment. [Story 3.3]</summary>
    public static DateOnly? ParseUpdated(string raw)
    {
        var m = UpdatedPattern.Match(raw);
        if (!m.Success) return null;
        return DateOnly.TryParseExact(
            m.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var updated)
            ? updated
            : null;
    }

    /// <summary>Splits a memlog into its frontmatter map and body lines, mirroring <c>memlog.py</c>'s own
    /// <c>split()</c> exactly: the first line must be exactly <c>---</c>, the closing fence is the FIRST later line
    /// that is exactly <c>---</c> (so a <c>---</c> inside a free-text <c>idea:</c>/<c>goal:</c> value can never
    /// truncate the block), and each frontmatter line splits on its FIRST colon. Returns false for a memlog with no
    /// frontmatter or an unterminated block — the caller reports that as <c>Malformed</c> rather than guessing.
    /// Keys are compared case-insensitively; values arrive already newline-collapsed by <c>memlog.py</c>'s
    /// <c>render()</c>, so each is safe to treat as a single line. [Story 18.4]</summary>
    public static bool TrySplit(
        string raw,
        out IReadOnlyDictionary<string, string> frontmatter,
        out IReadOnlyList<string> bodyLines)
    {
        frontmatter = EmptyFrontmatter;
        bodyLines = Array.Empty<string>();

        var lines = raw.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0] != "---") return false;

        var end = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i] == "---") { end = i; break; }
        }
        if (end < 0) return false;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < end; i++)
        {
            var line = lines[i];
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            map[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        frontmatter = map;
        bodyLines = lines[(end + 1)..];
        return true;
    }

    /// <summary>Parses the append-only body into typed entries. Every entry is one line beginning <c>- </c>;
    /// an optional leading <c>(type)</c> or <c>(type by who)</c> tag names the entry KIND. Only the first word of
    /// the tag is kept as the type (the <c>by …</c> attribution half is display-only), lower-cased so a caller can
    /// match a vocabulary word without re-normalizing. Lines that are not entries are ignored — the body has no
    /// other structure by design. [Story 18.4]</summary>
    public static IReadOnlyList<MemlogEntry> ParseEntries(IReadOnlyList<string> bodyLines)
    {
        var entries = new List<MemlogEntry>();
        foreach (var line in bodyLines)
        {
            var trimmed = line.TrimEnd();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal)) continue;
            var rest = trimmed[2..].TrimStart();

            string? type = null;
            if (rest.StartsWith('(') && rest.IndexOf(')') is var close and > 0)
            {
                var label = rest[1..close].Trim();
                rest = rest[(close + 1)..].TrimStart();
                // "(idea by user)" / "(by coach)" → the KIND is the first word; a bare "(by …)" tag has no kind.
                var firstWord = label.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
                    ? parts[0]
                    : string.Empty;
                if (firstWord.Length > 0 && !string.Equals(firstWord, "by", StringComparison.OrdinalIgnoreCase))
                {
                    type = firstWord.ToLowerInvariant();
                }
            }

            if (rest.Length == 0 && type is null) continue;
            entries.Add(new MemlogEntry(type, rest));
        }

        return entries;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyFrontmatter =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
