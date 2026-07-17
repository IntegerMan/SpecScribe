namespace SpecScribe;

/// <summary>One quick-dev / one-shot work item — an <c>implementation-artifacts/spec-*.md</c> file carrying
/// frontmatter <c>route: one-shot</c>, the output of the <c>bmad-quick-dev</c> workflow. It has a generated
/// standalone page (<see cref="OutputPath"/>) but is NOT part of the epic/story roll-up.
/// <see cref="AuthoredDate"/> is best-effort from existing <c>created</c>/<c>date</c> frontmatter only.</summary>
public sealed record QuickDevEntry(
    string Title,
    string OutputPath,
    string? Status,
    string? Type,
    DateOnly? AuthoredDate = null);

/// <summary>The deferred-work note (<c>deferred-work.md</c>) with a count of open (not struck-through) items,
/// so the home page can surface how much real-but-not-now work is parked.</summary>
public sealed record DeferredWorkEntry(string Title, string OutputPath, int OpenItemCount);

/// <summary>A lightweight inventory of the work classes the epic/story roll-up doesn't cover: quick-dev
/// one-shot changes and the deferred-work note. Built from the already-generated <see cref="DocModel"/> set,
/// so a missing / partial / empty file simply yields fewer entries — never an exception (NFR2). These are a
/// SEPARATE signal: they are never folded into the epic/story/task tallies (that would misrepresent
/// completion, which AC #1 forbids). The spec KERNEL under <c>specs/spec-specscribe/</c> is deliberately NOT
/// matched here — that is Story 2.2's domain.</summary>
public sealed class WorkInventory
{
    public required IReadOnlyList<QuickDevEntry> QuickDev { get; init; }
    public DeferredWorkEntry? Deferred { get; init; }

    /// <summary>True when there is no quick-dev or deferred work to surface, so callers omit the section
    /// entirely (Story 1.1 graceful omission) rather than render an empty header.</summary>
    public bool IsEmpty => QuickDev.Count == 0 && Deferred is null;

    public static readonly WorkInventory Empty = new() { QuickDev = Array.Empty<QuickDevEntry>(), Deferred = null };

    public static WorkInventory Build(IReadOnlyList<DocModel> docs)
    {
        var quickDev = new List<QuickDevEntry>();
        DeferredWorkEntry? deferred = null;

        foreach (var doc in docs)
        {
            // Location-tolerant since Story 4.2: any implementation-artifacts/ ancestor segment classifies
            // (shared adapter convention), so a project nesting the folder deeper keeps its work inventory.
            var norm = PathUtil.NormalizeSlashes(doc.SourceRelativePath);
            if (!BmadArtifactAdapter.IsUnderImplementationArtifacts(norm)) continue;

            var slash = norm.LastIndexOf('/');
            var fileName = slash >= 0 ? norm[(slash + 1)..] : norm;
            var output = PathUtil.NormalizeSlashes(doc.OutputRelativePath);

            if (string.Equals(fileName, "deferred-work.md", StringComparison.OrdinalIgnoreCase))
            {
                // First match wins if more than one deferred-work.md somehow exists — deterministic rather
                // than last-write-wins, and still never an exception (NFR2).
                deferred ??= new DeferredWorkEntry(doc.Title, output, CountOpenItems(doc.BodyHtml));
            }
            else if (fileName.StartsWith("spec-", StringComparison.OrdinalIgnoreCase)
                     && string.Equals(doc.Frontmatter.Route?.Trim(), "one-shot", StringComparison.OrdinalIgnoreCase))
            {
                quickDev.Add(new QuickDevEntry(
                    doc.Title, output, doc.Frontmatter.Status, doc.Frontmatter.Type,
                    doc.Frontmatter.AuthoredDay()));
            }
        }

        return new WorkInventory
        {
            QuickDev = quickDev.OrderBy(q => q.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            Deferred = deferred,
        };
    }

    /// <summary>Counts the open items in the deferred-work note: top-level rendered list items, minus those
    /// struck through (a resolved item is written <c>~~…~~</c>, which Markdig renders as <c>&lt;del&gt;</c>).
    /// Only list depth 1 is tallied so a nested sub-bullet under an open item doesn't inflate the count as if
    /// it were its own deferred item. Purely a rough "how much is parked" signal — a partial or unusually-
    /// shaped note just yields a smaller count, never an error.</summary>
    public static int CountOpenItems(string bodyHtml)
    {
        int depth = 0, topLevelLi = 0, topLevelDel = 0, i = 0;
        while (i < bodyHtml.Length)
        {
            if (StartsWithAt(bodyHtml, i, "<ul") || StartsWithAt(bodyHtml, i, "<ol")) { depth++; i += 3; }
            else if (StartsWithAt(bodyHtml, i, "</ul") || StartsWithAt(bodyHtml, i, "</ol")) { depth = Math.Max(0, depth - 1); i += 4; }
            else if (depth == 1 && StartsWithAt(bodyHtml, i, "<li")) { topLevelLi++; i += 3; }
            else if (depth == 1 && StartsWithAt(bodyHtml, i, "<del")) { topLevelDel++; i += 4; }
            else { i++; }
        }

        return Math.Max(0, topLevelLi - topLevelDel);

        static bool StartsWithAt(string haystack, int index, string needle) =>
            index + needle.Length <= haystack.Length
            && string.CompareOrdinal(haystack, index, needle, 0, needle.Length) == 0;
    }
}
