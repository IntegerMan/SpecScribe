using System.Globalization;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Resolves source-code citations left inert by Story 7.1 into real links to the in-portal code pages
/// (<c>code/&lt;repo-rel&gt;.html#L{n}</c>) — or, in external-link mode, to <c>{CodeSourceBaseUrl}/&lt;repo-rel&gt;#L{n}</c>
/// (Story 7.2, FR15). Runs as a whole-page post-process in <see cref="SiteGenerator.ApplyReferenceLinks"/> — the same
/// pass that already runs <see cref="RequirementLinkifier"/>/<see cref="StoryEpicLinkifier"/> on every generated page —
/// so a citation resolves no matter which template emitted it and no matter whether it sits in body prose or inside a
/// rendered markdown comment (<c>&lt;aside class="md-comment"&gt;</c>, Story 2.6 — the "comment linking" half of the story).
///
/// Two matchers share one resolver, mirroring the two real citation shapes:
/// <list type="number">
/// <item><b>Href rewriter</b> (models <see cref="AdrLinkRewriter"/>): the Markdig-emitted <c>&lt;a href="…/src/…(:line)?"&gt;</c>
/// "view-source" links. The line number is read from the <c>:N</c> href suffix; unresolved links have their anchor
/// dropped so only plain text remains (AC #1 — never a broken link).</item>
/// <item><b>Anchor-aware plain-text matcher</b> (models <see cref="RequirementLinkifier"/>): inert
/// <c>[Source: `src/…`]</c> / <c>[Source: src/…]</c> code-span citations that never became links, including inside
/// comment asides. Split on <c>&lt;a&gt;…&lt;/a&gt;</c> so an already-produced link is never re-wrapped.</item>
/// </list>
///
/// The <c>#L{n}</c> fragment is identical in both link modes — Story 7.1 locked that so this logic never branches on
/// the fragment. The <c>.md</c> citation path (<see cref="SourceLinkifier"/>) is deliberately untouched: this is a
/// PARALLEL resolver for repository code files, not a rewrite of doc-citation handling.</summary>
public static class CodeReferenceLinkifier
{
    // Any anchor; the evaluator decides whether its href is a resolvable code reference. Singleline so a label may
    // span lines. Attribute order is not assumed (href may sit anywhere in the tag).
    private static readonly Regex AnchorPattern = new(
        "<a\\b[^>]*?\\bhref=\"(?<href>[^\"]*)\"[^>]*>(?<label>.*?)</a>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // Splits out whole anchor spans so the plain-text matcher only rewrites the non-anchor segments (mirrors
    // RequirementLinkifier) — never re-linking the href rewriter's own output or any nav/req-ref link.
    private static readonly Regex AnchorSplit = new(
        "(<a\\b[^>]*>.*?</a>)",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    // Inert "[Source: <path>]" citation in plain text — optionally wrapped in a <code> span (a rendered code-span)
    // or literal backticks (the shape a citation keeps INSIDE an HTML-escaped comment aside). The path stops before
    // any '<' so it never swallows an adjacent tag. Mirrors CodeReferenceScanner.SourceInline over rendered HTML.
    private static readonly Regex InlineCitation = new(
        "\\[Source:\\s*(?<inner>(?:<code>)?`?(?<path>[^\\[\\]`)\\r\\n<]+?)`?(?:</code>)?)\\s*\\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Rewrites both citation shapes on one rendered page.</summary>
    /// <param name="html">Already-rendered page HTML.</param>
    /// <param name="codePages">Story 7.1's forward map: repo-relative source path (forward slashes) → code-page
    /// output-relative path (<c>code/&lt;path&gt;.html</c>). Empty in external mode.</param>
    /// <param name="codeSourceBaseUrl">External-link base (<c>--code-url</c>) when set; null selects in-portal mode.</param>
    /// <param name="prefix">The page's climb back to the output root (<see cref="PathUtil.RelativePrefix"/>).</param>
    /// <param name="repoRoot">Repo root — for the external-mode "is this a real repo file?" gate.</param>
    /// <param name="sourceRoot">The <c>_bmad-output</c> source root — excluded from code resolution (doc pages already exist).</param>
    public static string Linkify(
        string html,
        IReadOnlyDictionary<string, string> codePages,
        string? codeSourceBaseUrl,
        string prefix,
        string repoRoot,
        string sourceRoot)
    {
        if (string.IsNullOrEmpty(html)) return html;

        var external = codeSourceBaseUrl is { Length: > 0 };
        // No in-portal pages and no external base ⇒ nothing can resolve. Leave the page exactly as-is.
        if (!external && codePages.Count == 0) return html;

        var baseUrl = external ? codeSourceBaseUrl!.TrimEnd('/') : null;

        // Form A — rewrite/strip the view-source anchors first, then form B — the inert code-span/comment citations.
        html = RewriteHrefs(html, codePages, baseUrl, prefix, repoRoot, sourceRoot);
        html = RewriteInline(html, codePages, baseUrl, prefix, repoRoot, sourceRoot);
        return html;
    }

    private static string RewriteHrefs(
        string html, IReadOnlyDictionary<string, string> codePages, string? baseUrl, string prefix, string repoRoot, string sourceRoot)
    {
        return AnchorPattern.Replace(html, m =>
        {
            var href = m.Groups["href"].Value;
            var label = m.Groups["label"].Value;

            // Only relative, non-page hrefs are candidate view-source links. Absolute URLs, in-page fragments,
            // and already-generated .html/.md pages are left exactly as they are (the latter keeps this idempotent
            // and off SourceLinkifier/AdrLinkRewriter's turf).
            if (!IsRelativeCodeHref(href)) return m.Value;

            var pathPart = CodeReferenceScanner.StripLocator(href, out var line);
            if (pathPart.Length == 0) return m.Value;

            if (TryBuildHref(pathPart, line, codePages, baseUrl, prefix, repoRoot, sourceRoot, out var resolved))
            {
                return $"<a href=\"{PathUtil.Html(resolved)}\">{label}</a>";
            }

            // Unresolved view-source link ⇒ drop the dead anchor, keep its text (AC #1). Never a broken link.
            return label;
        });
    }

    private static string RewriteInline(
        string html, IReadOnlyDictionary<string, string> codePages, string? baseUrl, string prefix, string repoRoot, string sourceRoot)
    {
        var parts = AnchorSplit.Split(html);
        for (var i = 0; i < parts.Length; i++)
        {
            // Odd indices are the captured <a>…</a> spans — leave them untouched.
            if (i % 2 == 1) continue;
            parts[i] = InlineCitation.Replace(parts[i], m =>
            {
                var inner = m.Groups["inner"].Value;
                var rawPath = m.Groups["path"].Value.Trim();
                var pathPart = CodeReferenceScanner.StripLocator(rawPath, out var line);
                if (pathPart.Length == 0) return m.Value;

                if (TryBuildHref(pathPart, line, codePages, baseUrl, prefix, repoRoot, sourceRoot, out var resolved))
                {
                    // Wrap the original inner text (code-span markup and locator preserved) so the citation still
                    // reads as written but is now clickable.
                    return $"[Source: <a href=\"{PathUtil.Html(resolved)}\">{inner}</a>]";
                }

                // Unresolved ⇒ already plain text; leave it exactly as-is (AC #1).
                return m.Value;
            });
        }
        return string.Concat(parts);
    }

    /// <summary>Resolves one citation path to a final href, or returns false to degrade it to plain text. In-portal
    /// mode gates strictly on membership in Story 7.1's page map (so a link can only ever point at a page that exists);
    /// external mode gates on the candidate resolving to a real, non-ignored repository file (so it never points at a
    /// path that isn't really in the repo). The <c>#L{n}</c> fragment is appended identically in both.</summary>
    private static bool TryBuildHref(
        string pathPart, int? line, IReadOnlyDictionary<string, string> codePages, string? baseUrl, string prefix,
        string repoRoot, string sourceRoot, out string href)
    {
        href = string.Empty;
        var fragment = line is { } n ? "#L" + n.ToString(CultureInfo.InvariantCulture) : string.Empty;

        if (baseUrl is not null)
        {
            // External mode: reconstruct the repo-relative candidate (strip a view-source href's leading ../) and
            // confirm it is a real repository file before emitting an external link.
            var candidate = StripLeadingRelative(PathUtil.NormalizeSlashes(pathPart));
            if (candidate.Length == 0) return false;
            if (!CodeReferenceScanner.TryResolveRepoFile(candidate, repoRoot, sourceRoot, out var repoRel)) return false;
            href = baseUrl + "/" + repoRel + fragment;
            return true;
        }

        // In-portal mode: the strip-leading-../ candidate must name a generated code page.
        var key = StripLeadingRelative(PathUtil.NormalizeSlashes(pathPart));
        if (key.Length == 0) return false;
        if (!codePages.TryGetValue(key, out var outputRelative)) return false;
        href = prefix + outputRelative + fragment;
        return true;
    }

    /// <summary>True when an href is a relative reference to a NON-page file — the shape of a view-source code link.
    /// Absolute URLs (any scheme, protocol-relative, root- or fragment-relative) and generated <c>.html</c>/<c>.md</c>
    /// pages are excluded so this never touches nav, requirement, doc-citation, or ADR links.</summary>
    private static bool IsRelativeCodeHref(string href)
    {
        if (string.IsNullOrEmpty(href)) return false;
        if (href[0] is '#' or '/' or '?') return false;
        if (href.Contains("://", StringComparison.Ordinal)) return false;
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return false;

        var bare = CodeReferenceScanner.StripLocator(href);
        if (bare.Length == 0) return false;
        if (bare.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) return false;
        if (bare.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return false;

        // A code reference names a file: require an extension on the last path segment so extension-less relative
        // links (e.g. "../overview") are left alone.
        var lastSlash = bare.LastIndexOfAny(new[] { '/', '\\' });
        var lastSegment = lastSlash >= 0 ? bare[(lastSlash + 1)..] : bare;
        return lastSegment.Contains('.');
    }

    /// <summary>Strips a run of leading <c>./</c> / <c>../</c> segments, recovering the repo-relative tail a
    /// well-formed view-source href climbs to. In-portal membership / external existence is the real gate, so an
    /// over-climbing href simply fails to resolve and degrades to plain text.</summary>
    private static string StripLeadingRelative(string path)
    {
        var i = 0;
        while (true)
        {
            if (path.AsSpan(i).StartsWith("../")) i += 3;
            else if (path.AsSpan(i).StartsWith("./")) i += 2;
            else break;
        }
        return path[i..];
    }
}
