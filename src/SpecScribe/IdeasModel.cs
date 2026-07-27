using System.Text;

namespace SpecScribe;

/// <summary>The three verdict buckets the Ideas list groups by, in the order they render
/// (strongest outcome first; killed last, as history). Deliberately three, not four: <c>bmad-forge-idea</c> has
/// THREE terminal exits (<c>Hardened</c>, <c>Killed</c>, <c>Clarified</c>) plus "not yet complete", and the owner
/// locked epics.md's three-value vocabulary with <em>clarified</em> folded into <see cref="InProgress"/>
/// (owner decision D2, 2026-07-27).
/// <para>The honest mitigation for that fold: <see cref="IdeaEntry.ExitWord"/> carries the TRUE exit word from the
/// same derivation, and the idea's detail page states it — so the bucketing is a grouping choice and never the
/// only record of how a session actually ended.</para> [Story 18.4]</summary>
public enum IdeaVerdict
{
    /// <summary>Session complete AND a <c>forged-idea.md</c> was distilled — the idea is ready to hand off.</summary>
    Hardened,

    /// <summary>Either genuinely unfinished (no <c>status: complete</c>) or complete-but-not-hardened
    /// (<em>clarified</em>, folded here per D2). <see cref="IdeaEntry.ExitWord"/> tells the two apart.</summary>
    InProgress,

    /// <summary>Session complete, no <c>forged-idea.md</c>, and the memlog records a <c>- (kill)</c> entry.</summary>
    Killed,
}

/// <summary>One forward link from an idea to a downstream artifact it produced — AC #2's traceability. Only ever
/// built from evidence that exists on disk (a resolvable markdown link inside <c>forged-idea.md</c>, or a
/// downstream doc whose frontmatter <c>sources:</c> names this workspace). No slug/title fuzzy matching, ever
/// (owner decision D4): a false provenance chain is worse than none, and Story 21.1's review already caught that
/// exact defect class. No evidence ⇒ the collection is empty and NO forward-link element renders at all — absent,
/// not "none found" (NFR8). [Story 18.4]</summary>
/// <param name="Label">Visible link text.</param>
/// <param name="OutputRelativePath">Root-relative output path of the target page (never re-prefixed here).</param>
/// <param name="Evidence">Why this link exists — rendered as the link's title so the provenance is inspectable.</param>
public sealed record IdeaForwardLink(string Label, string OutputRelativePath, string Evidence);

/// <summary>One discovered forge session workspace, projected into everything both the Ideas list row and the
/// idea detail page need. Mirrors <see cref="AdrEntry"/>'s shape (title + output path + source path + status +
/// date + summary) — the list-page row model this surface was built from. [Story 18.4]</summary>
public sealed record IdeaEntry
{
    /// <summary>Path-safe, de-duplicated slug — the page-path segment. NOT the raw workspace directory name: that
    /// is LLM-derived from free user text and only conventionally kebab-case, so it is slugified and collision-
    /// resolved before it can become a path (see <see cref="IdeaDerivation.Slugify"/>).</summary>
    public required string Slug { get; init; }

    public required string Title { get; init; }

    /// <summary>One-line blurb (the memlog's own <c>goal:</c>, then the first lock, then the first decision), or
    /// null when the workspace carries none — the row is then the bare title.</summary>
    public string? Summary { get; init; }

    public required IdeaVerdict Verdict { get; init; }

    /// <summary>The TRUE exit word — <c>Hardened</c> / <c>Killed</c> / <c>Clarified</c> / <c>In progress</c> —
    /// which is NOT always the bucket's word (D2 folds <em>Clarified</em> into <see cref="IdeaVerdict.InProgress"/>).
    /// The detail page states this; the list groups by <see cref="Verdict"/>.</summary>
    public required string ExitWord { get; init; }

    /// <summary>The memlog's <c>updated:</c> day, or null when unparseable.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Workspace directory, relative to the source root, forward-slashed (e.g. <c>forge/my-idea</c>).</summary>
    public required string WorkspaceSourceRelative { get; init; }

    /// <summary>The memlog's chronology, in order — the detail page's spine.</summary>
    public IReadOnlyList<MemlogEntry> Entries { get; init; } = Array.Empty<MemlogEntry>();

    /// <summary>Rendered body of <c>forged-idea.md</c> when the session hardened, else null.</summary>
    public string? ForgedIdeaHtml { get; init; }

    /// <summary>The forge's own <c>forge-report.html</c>, verbatim — non-null ONLY when a report exists AND it
    /// passed the self-contained safety gate and the size cap (AC #6). Carried, never rewritten: the report has its
    /// own <c>&lt;html&gt;</c> and its own inline CSS, so it is a LEAF artifact written straight through
    /// <c>SiteGenerator.WriteOutput</c> and never wrapped in <see cref="HtmlTemplater.RenderPage"/> (that would nest
    /// documents — the exact defect class Story 23.3 hit).</summary>
    public string? CarriedReportHtml { get; init; }

    /// <summary>Unresolved forward-link candidates harvested from <c>forged-idea.md</c>'s markdown links, already
    /// resolved to source-relative keys but NOT yet checked for a generated page — page existence is only knowable
    /// after the pages phase, so <c>SiteGenerator</c> completes the resolution and drops any target with no page
    /// (§9 / D4). Pure path math here; no page knowledge.</summary>
    public IReadOnlyList<(string SourceRelative, string Label)> ForwardLinkCandidates { get; init; }
        = Array.Empty<(string, string)>();

    public IReadOnlyList<IdeaForwardLink> ForwardLinks { get; init; } = Array.Empty<IdeaForwardLink>();

    /// <summary>The idea's own detail page, root-relative.</summary>
    public string DetailOutputPath => $"ideas/{Slug}.html";

    /// <summary>The carried report's output path, or null when nothing was carried. DERIVED from
    /// <see cref="CarriedReportHtml"/> rather than stored, so the link and the write can never disagree — the same
    /// discipline <see cref="SiteNav"/>'s shared output-path constants enforce between a page and its nav entry.</summary>
    public string? ReportOutputPath => CarriedReportHtml is null ? null : $"ideas/{Slug}-report.html";
}

/// <summary>Every discovered forge session, ordered. A pure <c>Build</c>-shaped model over already-gathered
/// inputs with an <see cref="Empty"/> singleton and an <see cref="IsEmpty"/> flag callers use to omit the whole
/// surface — the same shape <see cref="ArtifactCoverage"/> / <see cref="WorkInventory"/> /
/// <see cref="WorkGraphModel"/> use. Never throws by its producer's contract: any failure degrades to
/// <see cref="Empty"/>, so the surface omits and baseline generation still succeeds (AD-4 / NFR2). [Story 18.4]</summary>
public sealed record IdeasModel(IReadOnlyList<IdeaEntry> Ideas)
{
    public static IdeasModel Empty { get; } = new(Array.Empty<IdeaEntry>());

    /// <summary>True when no forge workspace was discovered — no <c>ideas.html</c> is written and no nav entry or
    /// quick link is emitted (AC #3 / NFR8: absent artifacts → absent surfaces, never an empty page).</summary>
    public bool IsEmpty => Ideas.Count == 0;

    /// <summary>Section order for the grouped list (owner decision D3): strongest outcome first, killed last as
    /// history. A verdict with zero ideas emits NO section at all — never an empty heading (NFR8).</summary>
    public static readonly IReadOnlyList<IdeaVerdict> SectionOrder =
        new[] { IdeaVerdict.Hardened, IdeaVerdict.InProgress, IdeaVerdict.Killed };

    public IReadOnlyList<IdeaEntry> InVerdict(IdeaVerdict verdict) =>
        Ideas.Where(i => i.Verdict == verdict).ToList();
}

/// <summary>The pure derivation rules — verdict, title, summary, slug — over a memlog's already-parsed
/// frontmatter/body plus the one filesystem fact that matters (<c>forged-idea.md</c> present or not). Split from
/// <see cref="IdeaDiscovery"/>'s disk walk the same way <see cref="ArtifactCoverage.Build"/> /
/// <see cref="WorkInventory"/> / <c>ProgressCalculator</c> are split from their callers' IO, so every rule here is
/// directly unit-testable without a repo on disk. [Story 18.4]</summary>
public static class IdeaDerivation
{
    /// <summary>Frontmatter key the forge writes for the idea itself (<c>memlog.py init --field idea=…</c>).
    /// Also the rule-3 corroboration signal: the other four BMad skills that use <c>memlog.py</c> write
    /// <c>topic:</c> instead, so an <c>idea:</c> key distinguishes a forge session from a PRD/brief/UX/spec one.</summary>
    public const string IdeaKey = "idea";

    /// <summary>Frontmatter key carrying the session goal — the row blurb.</summary>
    public const string GoalKey = "goal";

    /// <summary>The lifecycle field the forge sets on exit (<c>memlog.py set --key status --value complete</c>).
    /// <para>⚠️ <c>memlog.py</c>'s own docstring invariant 3 says a memory log has NO lifecycle status; the forge
    /// sets one anyway through the generic <c>set</c> subcommand, which enforces no key vocabulary. So this is an
    /// OBSERVED convention, not a guarantee — an absent <c>status</c> must mean "in progress", never an error.</para></summary>
    public const string StatusKey = "status";

    public const string CompleteValue = "complete";

    /// <summary>The memlog entry type that marks an idea as killed.</summary>
    public const string KillEntryType = "kill";

    /// <summary>Derives the verdict bucket and the TRUE exit word from the three signals that exist on disk.
    /// <list type="bullet">
    /// <item>no <c>status: complete</c> → in-progress (genuinely unfinished)</item>
    /// <item>complete + <c>forged-idea.md</c> → hardened</item>
    /// <item>complete, no <c>forged-idea.md</c>, a <c>- (kill)</c> entry → killed</item>
    /// <item>complete, no <c>forged-idea.md</c>, no kill entry → <em>clarified</em>, bucketed in-progress (D2)</item>
    /// </list>
    /// The <c>forge-report.html</c> stamp word (<c>HARDENED</c>/<c>KILLED</c>/<c>CLARIFIED</c>) is the most
    /// AUTHORITATIVE record but the least PARSEABLE — LLM-rendered prose HTML with no fixed markup — so it is never
    /// string-matched to decide a bucket; the detail page links it as corroboration instead. [Story 18.4]</summary>
    public static (IdeaVerdict Verdict, string ExitWord) DeriveVerdict(
        IReadOnlyDictionary<string, string> frontmatter,
        IReadOnlyList<MemlogEntry> entries,
        bool hasForgedIdea)
    {
        var complete = frontmatter.TryGetValue(StatusKey, out var status)
            && string.Equals(status.Trim(), CompleteValue, StringComparison.OrdinalIgnoreCase);

        if (!complete) return (IdeaVerdict.InProgress, "In progress");
        if (hasForgedIdea) return (IdeaVerdict.Hardened, "Hardened");
        if (entries.Any(e => string.Equals(e.Type, KillEntryType, StringComparison.OrdinalIgnoreCase)))
            return (IdeaVerdict.Killed, "Killed");
        return (IdeaVerdict.InProgress, "Clarified");
    }

    /// <summary>Title cascade, first non-empty wins: the memlog's <c>idea:</c> value → <c>forged-idea.md</c>'s
    /// first H1 → the workspace directory name, de-kebabed. The <c>idea:</c> value is free user text that
    /// <c>memlog.py</c>'s <c>render()</c> has already newline-collapsed, so it is safe as a single line but may be
    /// long — it goes through the SAME <see cref="SiteGenerator.CollapseSummary"/> the ADR cards use rather than a
    /// second truncator. [Story 18.4]</summary>
    public static string DeriveTitle(
        IReadOnlyDictionary<string, string> frontmatter,
        string? forgedIdeaH1,
        string workspaceDirName)
    {
        if (frontmatter.TryGetValue(IdeaKey, out var idea)
            && SiteGenerator.CollapseSummary(idea) is { Length: > 0 } fromFrontmatter)
        {
            return fromFrontmatter;
        }

        if (forgedIdeaH1 is { Length: > 0 } && SiteGenerator.CollapseSummary(forgedIdeaH1) is { Length: > 0 } fromH1)
        {
            return fromH1;
        }

        return DeKebab(workspaceDirName);
    }

    /// <summary>Blurb cascade: the memlog's <c>goal:</c> → the first <c>- (lock)</c> entry → the first
    /// <c>- (decision)</c> entry. None ⇒ null, and the row is the bare title (<see cref="ListRow"/> already handles
    /// a summary-only row). [Story 18.4]</summary>
    public static string? DeriveSummary(
        IReadOnlyDictionary<string, string> frontmatter,
        IReadOnlyList<MemlogEntry> entries)
    {
        if (frontmatter.TryGetValue(GoalKey, out var goal)
            && SiteGenerator.CollapseSummary(goal) is { Length: > 0 } fromGoal)
        {
            return fromGoal;
        }

        foreach (var type in new[] { "lock", "decision" })
        {
            var match = entries.FirstOrDefault(e => string.Equals(e.Type, type, StringComparison.OrdinalIgnoreCase));
            if (match is not null && SiteGenerator.CollapseSummary(match.Text) is { Length: > 0 } fromEntry)
            {
                return fromEntry;
            }
        }

        return null;
    }

    /// <summary>Turns a workspace directory name into a path-safe page slug: lower-cased ASCII alphanumerics,
    /// every other run collapsed to a single hyphen, trimmed. The name is LLM-derived from free user text
    /// [<c>SKILL.md</c> §Set up the session] and only CONVENTIONALLY kebab-case, so it can carry spaces, dots, or
    /// non-ASCII before it becomes a path segment. An input that slugifies to nothing falls back to
    /// <c>idea</c> — de-duplication upstream keeps that from colliding. [Story 18.4]</summary>
    public static string Slugify(string name)
    {
        var sb = new StringBuilder(name.Length);
        var pendingHyphen = false;
        foreach (var ch in name)
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pendingHyphen && sb.Length > 0) sb.Append('-');
                pendingHyphen = false;
                sb.Append(ch);
            }
            else if (ch is >= 'A' and <= 'Z')
            {
                if (pendingHyphen && sb.Length > 0) sb.Append('-');
                pendingHyphen = false;
                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                pendingHyphen = true;
            }
        }

        var slug = sb.ToString();
        return slug.Length > 0 ? slug : "idea";
    }

    /// <summary>Display fallback for a workspace with no <c>idea:</c> and no H1: <c>my-cache-idea</c> →
    /// <c>My cache idea</c>. Never invents words — only re-spaces and sentence-cases what the folder already says.</summary>
    public static string DeKebab(string name)
    {
        var spaced = name.Replace('-', ' ').Replace('_', ' ').Trim();
        if (spaced.Length == 0) return "Untitled idea";
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    /// <summary>The verdict's visible word — what the badge and section heading say. "In progress" covers both a
    /// genuinely unfinished session and a clarified one (D2); an idea's own
    /// <see cref="IdeaEntry.ExitWord"/> is what distinguishes them on the detail page.</summary>
    public static string VerdictWord(IdeaVerdict verdict) => verdict switch
    {
        IdeaVerdict.Hardened => "Hardened",
        IdeaVerdict.Killed => "Killed",
        _ => "In progress",
    };

    /// <summary>Section heading for a verdict group on <c>ideas.html</c>.</summary>
    public static string SectionHeading(IdeaVerdict verdict) => verdict switch
    {
        IdeaVerdict.Hardened => "Hardened",
        IdeaVerdict.Killed => "Killed",
        _ => "In progress",
    };
}
