using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Discovers <c>bmad-forge-idea</c> session workspaces under the source root and projects each into an
/// <see cref="IdeaEntry"/>. This is the IO half of the Ideas surface; every RULE it applies lives in the pure
/// <see cref="IdeaDerivation"/> beside it, the same IO/logic split <c>ArtifactCoverage</c> / <c>WorkInventory</c> /
/// <c>ProgressCalculator</c> already use.
/// <para><b>Why a cascade and not one rule.</b> The forge writes its workspace to
/// <c>{output_folder}/forge/{slug}/</c> and <c>{output_folder}</c> IS SpecScribe's source root, so the default
/// install is a path rule. But <c>.memlog.md</c> is written by the shared CORE tool <c>memlog.py</c>, which at
/// least five BMad skills use — this very repo carries four non-forge memlogs (brief / PRD / UX / spec) — so
/// "a directory holding a <c>.memlog.md</c>" is emphatically NOT "a forged idea". Hence:</para>
/// <list type="number">
/// <item><b>Path</b> (authoritative for the default install): any directory under <c>{SourceRoot}/forge/</c> that
/// holds a <c>.memlog.md</c>. The ONLY rule that catches an in-progress session, which has no other marker.</item>
/// <item><b>Marker</b> (covers an overridden output path): anywhere else, a directory holding a
/// <c>.memlog.md</c> AND a sibling <c>forge-report.html</c>.</item>
/// <item><b>Frontmatter corroboration</b>: the forge writes <c>idea:</c> where the other memlog-using skills write
/// <c>topic:</c>. Used only to REJECT a rule-1 false positive (a hand-made <c>forge/</c> folder) — never as the
/// sole positive signal, since a marker-rule workspace is already proven by its report.</item>
/// </list>
/// <para><b>Known limitation, stated rather than engineered around.</b> <c>forge_output_path</c> and
/// <c>run_folder_pattern</c> are both overridable in <c>_bmad/custom/bmad-forge-idea.toml</c>, and SpecScribe reads
/// NO BMad skill/module TOML or <c>config.yaml</c> at all today (the same gap Story 18.5 records for TEA's
/// <c>test_artifacts</c> key). Under an overridden path an <em>in-progress</em> session is therefore undiscoverable
/// until it completes and writes its report. Closing that needs a cross-cutting TOML-reading decision, not a
/// reader bolted on here.</para>
/// <para>Never throws (AD-4 / NFR2): any failure degrades to <see cref="IdeasModel.Empty"/> or drops one workspace
/// with a categorized non-fatal diagnostic, so the surface omits and generation still succeeds.</para>
/// [Story 18.4]</summary>
public static class IdeaDiscovery
{
    /// <summary>The forge's default workspace root under the source root — <c>customize.toml</c>'s
    /// <c>forge_output_path = "{output_folder}/forge"</c>, and <c>{output_folder}</c> resolves to
    /// <see cref="ForgeOptions.SourceDirName"/>. Also the <see cref="DashboardViewBuilder"/> folder-group key so
    /// <c>forge/</c> stops reading as an unrecognized top-level folder.</summary>
    public const string WorkspaceRootDirName = "forge";

    /// <summary>The forge's always-rendered self-contained report — present on EVERY exit, and the only place the
    /// persona objections / rationale AC #1 asks for actually live.</summary>
    public const string ReportFileName = "forge-report.html";

    /// <summary>The distilled hand-off, written ONLY on a hardened exit.</summary>
    public const string ForgedIdeaFileName = "forged-idea.md";

    /// <summary>Upper bound on a carried report. <c>SiteGenerator.WriteOutput</c> populates the SPA capture, whose
    /// chunker is byte-blind (a known perf defect), so one oversized foreign page would inflate every SPA chunk. A
    /// self-contained page with an inline-SVG seal is far under this; above it the report is skipped with a
    /// diagnostic rather than carried.</summary>
    public const int MaxCarriedReportBytes = 512 * 1024;

    // "Self-contained and script-free" (AC #6), as two checks over the raw report text.
    //
    // (a) SCRIPT. `SKILL.md` contracts the report as "self-contained … with inline CSS and an inline-SVG seal" but
    //     says nothing about scripts, and nothing enforces the contract — the file is LLM-authored HTML landing
    //     VERBATIM inside the portal's own output directory. Beyond a literal <script we also reject the other
    //     ways a page executes: <iframe>/<object>/<embed> (an iframe srcdoc runs script), inline event handlers,
    //     and javascript: URLs. That is a reading of AC #6's own words ("script-free"), not a widening of it.
    // (b) EXTERNAL SUBRESOURCE. Any src=/href= pointing at http://, https:// or a protocol-relative //host.
    //     Deliberately strict: it also catches an ordinary outbound <a href="https://…">, which is not literally a
    //     subresource. A report is meant to be openable offline from file://, so "no external origins at all" is
    //     the honest reading — and the cost of a false reject is one absent link plus a diagnostic that says why.
    //
    // Rejecting also keeps the site inside ADR 0013 / NFR-5's JS-optional posture: a carried page that only works
    // with JS would be a portal surface with no text twin.
    private static readonly Regex UnsafeReportPattern = new(
        @"<\s*(?:script|iframe|object|embed)\b|\son[a-z]+\s*=\s*[""']|javascript\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExternalSubresourcePattern = new(
        @"\b(?:src|href)\s*=\s*[""']?\s*(?:https?:)?//",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MarkdownLinkPattern = new(
        @"\[(?<text>[^\]]*)\]\((?<href>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled);

    /// <summary>Scans <paramref name="sourceRoot"/> for forge workspaces. <paramref name="diagnostics"/> collects
    /// the categorized non-fatal notices (every path here is under the source root, so every one anchors to
    /// <see cref="DiagnosticAnchorRoot.Source"/> — the default).</summary>
    public static IdeasModel Discover(string sourceRoot, List<AdapterDiagnostic>? diagnostics = null)
    {
        try
        {
            if (!Directory.Exists(sourceRoot)) return IdeasModel.Empty;

            // Ordinal path order makes first-wins slug de-duplication deterministic across filesystems — the same
            // discipline SiteNav.Build's module-doc loop uses for duplicate well-known filenames.
            var memlogs = Directory
                .EnumerateFiles(sourceRoot, Memlog.FileName, SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
            if (memlogs.Count == 0) return IdeasModel.Empty;

            var entries = new List<IdeaEntry>();
            var slugOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var slugCollisions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var memlogFullPath in memlogs)
            {
                var workspaceFullPath = Path.GetDirectoryName(memlogFullPath);
                if (workspaceFullPath is null) continue;

                var workspaceRelative = PathUtil.NormalizeSlashes(
                    Path.GetRelativePath(sourceRoot, workspaceFullPath));
                if (workspaceRelative is "." or "") continue; // a source-root memlog is the project journal, not a session

                var reportFullPath = Path.Combine(workspaceFullPath, ReportFileName);
                var hasReport = File.Exists(reportFullPath);
                var underForgeRoot = IsUnderWorkspaceRoot(workspaceRelative);

                // Rules 1 + 2. Everything else — including this repo's own brief/PRD/UX/spec memlogs — is not a
                // forge workspace and is passed over in silence: it is not a problem to report, it is another
                // skill's session.
                if (!underForgeRoot && !hasReport) continue;

                string raw;
                try
                {
                    raw = MarkdownConverter.ReadAllTextShared(memlogFullPath);
                }
                catch (Exception)
                {
                    diagnostics?.Add(new AdapterDiagnostic(
                        AdapterDiagnosticCategory.Error,
                        $"{workspaceRelative}/{Memlog.FileName}",
                        $"Forge session '{LastSegment(workspaceRelative)}' could not be read; it is omitted from the Ideas page."));
                    continue;
                }

                var parsed = Memlog.TrySplit(raw, out var frontmatter, out var bodyLines);
                if (!parsed)
                {
                    diagnostics?.Add(new AdapterDiagnostic(
                        AdapterDiagnosticCategory.Malformed,
                        $"{workspaceRelative}/{Memlog.FileName}",
                        "Forge session memlog could not be parsed; the idea is listed with its folder name and no summary."));
                }

                // Rule 3, reject-only. A rule-2 workspace is already proven by its report, so corroboration applies
                // solely to the path rule; and an unparseable memlog cannot corroborate either way, so the path rule
                // stays authoritative there rather than silently dropping a real session.
                if (underForgeRoot && !hasReport && parsed && !frontmatter.ContainsKey(IdeaDerivation.IdeaKey))
                {
                    continue;
                }

                var workspaceDirName = LastSegment(workspaceRelative);
                var slug = IdeaDerivation.Slugify(workspaceDirName);
                if (slugOwners.TryGetValue(slug, out _))
                {
                    slugCollisions[slug] = slugCollisions.GetValueOrDefault(slug) + 1;
                    continue; // first wins, in ordinal path order; the aggregated Skipped notice is emitted below
                }
                slugOwners[slug] = workspaceRelative;

                var memlogEntries = Memlog.ParseEntries(bodyLines);
                var forgedIdeaFullPath = Path.Combine(workspaceFullPath, ForgedIdeaFileName);
                var hasForgedIdea = File.Exists(forgedIdeaFullPath);

                string? forgedIdeaRaw = null;
                string? forgedIdeaHtml = null;
                if (hasForgedIdea)
                {
                    try
                    {
                        forgedIdeaRaw = MarkdownConverter.ReadAllTextShared(forgedIdeaFullPath);
                        forgedIdeaHtml = MarkdownConverter.Convert(
                            forgedIdeaFullPath,
                            $"{workspaceRelative}/{ForgedIdeaFileName}",
                            $"ideas/{slug}.html").BodyHtml;
                    }
                    catch (Exception)
                    {
                        // A hardened session whose hand-off cannot be rendered still IS hardened — the file's
                        // presence is what the verdict reads. The detail page simply omits that block.
                        forgedIdeaHtml = null;
                    }
                }

                var (verdict, exitWord) = IdeaDerivation.DeriveVerdict(frontmatter, memlogEntries, hasForgedIdea);

                entries.Add(new IdeaEntry
                {
                    Slug = slug,
                    Title = IdeaDerivation.DeriveTitle(
                        frontmatter,
                        forgedIdeaRaw is null ? null : MarkdownConverter.ExtractFirstH1(forgedIdeaRaw),
                        workspaceDirName),
                    Summary = IdeaDerivation.DeriveSummary(frontmatter, memlogEntries),
                    Verdict = verdict,
                    ExitWord = exitWord,
                    Date = Memlog.ParseUpdated(raw),
                    WorkspaceSourceRelative = workspaceRelative,
                    Entries = memlogEntries,
                    ForgedIdeaHtml = forgedIdeaHtml,
                    CarriedReportHtml = hasReport
                        ? TryCarryReport(reportFullPath, workspaceRelative, slug, diagnostics)
                        : null,
                    ForwardLinkCandidates = forgedIdeaRaw is null
                        ? Array.Empty<(string, string)>()
                        : HarvestForwardLinkCandidates(forgedIdeaRaw, workspaceRelative),
                });
            }

            foreach (var (slug, others) in slugCollisions.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                diagnostics?.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Skipped,
                    slugOwners[slug],
                    $"Duplicate idea slug '{slug}'; the first workspace in path order is listed and {others} other(s) skipped."));
            }

            if (entries.Count == 0) return IdeasModel.Empty;

            // Newest first within the whole model (the sections slice it, preserving this order), with fully
            // deterministic tiebreaks so a from-scratch regeneration is byte-identical.
            var ordered = entries
                .OrderByDescending(e => e.Date.HasValue)
                .ThenByDescending(e => e.Date ?? default)
                .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Slug, StringComparer.Ordinal)
                .ToList();

            return new IdeasModel(ordered);
        }
        catch (Exception)
        {
            // AD-4: an optional insight provider never owns baseline success.
            return IdeasModel.Empty;
        }
    }

    /// <summary>True when a source-relative workspace path sits under the forge's default workspace root. The run
    /// folder may be NESTED (<c>run_folder_pattern</c> is documented as overridable to add a <c>{date}</c> or other
    /// components — which is exactly why <c>SKILL.md</c> §5's own resume glob is recursive), so this is a prefix
    /// test, not a parent test, and the <c>{slug}</c> is the workspace directory's own name rather than
    /// necessarily a direct child.</summary>
    private static bool IsUnderWorkspaceRoot(string workspaceRelative) =>
        workspaceRelative.StartsWith(WorkspaceRootDirName + "/", StringComparison.OrdinalIgnoreCase);

    private static string LastSegment(string normalizedPath)
    {
        var slash = normalizedPath.LastIndexOf('/');
        return slash < 0 ? normalizedPath : normalizedPath[(slash + 1)..];
    }

    /// <summary>Reads a <c>forge-report.html</c> and returns it VERBATIM when it passes AC #6's gate, else null
    /// plus one <c>Skipped</c> diagnostic naming which half of the gate it failed. Nothing here rewrites,
    /// restyles, sanitizes-by-transformation, or wraps the report: it is carried whole or not at all.</summary>
    private static string? TryCarryReport(
        string reportFullPath, string workspaceRelative, string slug, List<AdapterDiagnostic>? diagnostics)
    {
        var reportRelative = $"{workspaceRelative}/{ReportFileName}";
        try
        {
            var length = new FileInfo(reportFullPath).Length;
            if (length > MaxCarriedReportBytes)
            {
                diagnostics?.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Skipped, reportRelative,
                    $"Forge report for '{slug}' was not carried into the portal: {FormatBytes(length)} exceeds the {FormatBytes(MaxCarriedReportBytes)} limit."));
                return null;
            }

            var raw = MarkdownConverter.ReadAllTextShared(reportFullPath);
            if (UnsafeReportPattern.IsMatch(raw) || ExternalSubresourcePattern.IsMatch(raw))
            {
                diagnostics?.Add(new AdapterDiagnostic(
                    AdapterDiagnosticCategory.Skipped, reportRelative,
                    $"Forge report for '{slug}' was not carried into the portal: it is not self-contained (script or external resource)."));
                return null;
            }

            return raw;
        }
        catch (Exception)
        {
            diagnostics?.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Error, reportRelative,
                $"Forge session '{slug}' could not be read; it is omitted from the Ideas page."));
            return null;
        }
    }

    internal static string FormatBytes(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / (1024.0 * 1024.0):0.#} MB"
            : $"{bytes / 1024.0:0.#} KB";

    /// <summary>AC #2's first admissible evidence source (§9): markdown links inside <c>forged-idea.md</c>,
    /// resolved from workspace-relative to source-relative keys. Pure path math — whether the target actually HAS a
    /// generated page is only knowable after the pages phase, so that half is completed by the caller. External
    /// URLs, in-page anchors, and anything escaping the source root are dropped here.</summary>
    internal static IReadOnlyList<(string SourceRelative, string Label)> HarvestForwardLinkCandidates(
        string forgedIdeaRaw, string workspaceRelative)
    {
        var results = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in MarkdownLinkPattern.Matches(forgedIdeaRaw))
        {
            if (!TryResolveWorkspaceHref(workspaceRelative, m.Groups["href"].Value, out var resolved)) continue;

            var label = m.Groups["text"].Value.Trim();
            if (label.Length == 0) label = LastSegment(resolved);
            if (seen.Add(resolved)) results.Add((resolved, label));
        }

        return results;
    }

    /// <summary>Resolves one href written inside a workspace file to a source-relative key, or returns false when
    /// it is not a source-tree reference at all (external URL, <c>mailto:</c>, bare in-page anchor) or escapes the
    /// source root. Shared by the forward-link harvest above and by the generator's hand-off body rewrite, so both
    /// answer "which source file does this href mean?" identically. [Story 18.4]</summary>
    internal static bool TryResolveWorkspaceHref(string workspaceRelative, string rawHref, out string sourceRelative)
    {
        sourceRelative = string.Empty;
        var href = rawHref.Trim();
        if (href.Length == 0 || href.StartsWith('#')) return false;
        if (href.Contains("://", StringComparison.Ordinal) || href.StartsWith("//", StringComparison.Ordinal)) return false;
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;

        // Drop any fragment/query before resolving — the KEY is the file.
        var cut = href.IndexOfAny(new[] { '#', '?' });
        if (cut >= 0) href = href[..cut];
        if (href.Length == 0) return false;

        var combined = PathUtil.NormalizeSlashes(Path.Combine(workspaceRelative, href.Replace('\\', '/')));
        var resolved = NormalizeDotSegments(combined);
        if (resolved is null || PathUtil.EscapesRepoRoot(resolved)) return false;

        sourceRelative = resolved;
        return true;
    }

    /// <summary>Collapses <c>.</c>/<c>..</c> segments in a forward-slashed relative path. Returns null when the
    /// path climbs above its own root (the caller drops it rather than emitting a link outside the source tree).</summary>
    private static string? NormalizeDotSegments(string path)
    {
        var stack = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count == 0) return null;
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(segment);
        }

        return stack.Count == 0 ? null : string.Join('/', stack);
    }
}
