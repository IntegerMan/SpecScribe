using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Stable, human-readable slug identity for follow-up detail pages.
/// Pure function of authored text — no new authoring schema. Content-hash disambiguation
/// only on collision (never positional -2/-3). [Story 9.11]</summary>
public static class FollowUpSlug
{
    public const string Folder = "follow-ups";
    public const int MaxWords = 8;

    private static readonly Regex NonSlug = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>Output-root-relative path: <c>follow-ups/{slug}.html</c>.</summary>
    public static string OutputPath(string slug) => $"{Folder}/{slug}.html";

    /// <summary>Kind-prefixed base slug for an action item (no collision suffix).</summary>
    public static string BaseForAction(SprintActionItem item) =>
        "action-" + Kebabize(item.Action);

    /// <summary>Kind-prefixed base slug for a deferred item (no collision suffix).
    /// Derives from the item lead + provenance label — text already authored.</summary>
    public static string BaseForDeferred(DeferredWorkItem item, string provenanceLabel)
    {
        var lead = FollowUpRow.SummarizeFromHtml(item.BodyHtml, maxChars: 200);
        var source = string.IsNullOrWhiteSpace(lead)
            ? provenanceLabel
            : $"{lead} {provenanceLabel}";
        return "deferred-" + Kebabize(source);
    }

    /// <summary>Assigns unique slugs across a collection. Identical base slugs get a short
    /// content-hash suffix; order of the collection does not change any item's slug.</summary>
    public static IReadOnlyDictionary<SprintActionItem, string> AssignActionSlugs(
        IReadOnlyList<SprintActionItem> items)
    {
        var bases = items.Select(i => (Item: i, Base: BaseForAction(i), Identity: ActionIdentity(i))).ToList();
        return Assign(bases, t => t.Item, t => t.Base, t => t.Identity);
    }

    /// <summary>Assigns unique deferred slugs. <paramref name="items"/> is (item, provenanceLabel) pairs.</summary>
    public static IReadOnlyDictionary<DeferredWorkItem, string> AssignDeferredSlugs(
        IReadOnlyList<(DeferredWorkItem Item, string ProvenanceLabel)> items)
    {
        var bases = items
            .Select(t => (t.Item, Base: BaseForDeferred(t.Item, t.ProvenanceLabel), Identity: DeferredIdentity(t.Item, t.ProvenanceLabel)))
            .ToList();
        return Assign(bases, t => t.Item, t => t.Base, t => t.Identity);
    }

    /// <summary>Kebab-case, lowercase, <c>[a-z0-9-]</c> only, collapse repeats, trim, cap words.</summary>
    public static string Kebabize(string text, int maxWords = MaxWords)
    {
        if (string.IsNullOrWhiteSpace(text)) return "item";

        var lower = text.Trim().ToLowerInvariant();
        var collapsed = NonSlug.Replace(lower, "-").Trim('-');
        if (collapsed.Length == 0) return "item";

        var words = collapsed.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > maxWords)
            words = words.Take(maxWords).ToArray();

        var result = string.Join('-', words);
        return result.Length == 0 ? "item" : result;
    }

    /// <summary>First 6 hex chars of SHA-256 over the identity string — stable under reordering.</summary>
    public static string ContentSuffix(string identityText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identityText));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..6];
    }

    private static string ActionIdentity(SprintActionItem item) =>
        $"{item.Action}\n{item.EpicNumber?.ToString() ?? ""}\n{item.Status}\n{item.Owner ?? ""}";

    private static string DeferredIdentity(DeferredWorkItem item, string provenanceLabel) =>
        $"{PathUtil.StripHtmlTags(item.BodyHtml)}\n{provenanceLabel}\n{item.ResolvingRef ?? ""}\n{item.Resolved}";

    private static IReadOnlyDictionary<TKey, string> Assign<TKey, TRow>(
        IReadOnlyList<TRow> rows,
        Func<TRow, TKey> key,
        Func<TRow, string> baseSlug,
        Func<TRow, string> identity)
        where TKey : notnull
    {
        var baseCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var b = baseSlug(row);
            baseCounts[b] = baseCounts.GetValueOrDefault(b) + 1;
        }

        var result = new Dictionary<TKey, string>();
        foreach (var row in rows)
        {
            var b = baseSlug(row);
            var slug = baseCounts[b] > 1
                ? $"{b}-{ContentSuffix(identity(row))}"
                : b;
            result[key(row)] = slug;
        }
        return result;
    }
}
