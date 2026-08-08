using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The single home for "what counts as executable HTML" in SpecScribe. [Story 17.2 Task 1, ADR 0042]
///
/// <para>Two consumers, one policy, deliberately NOT one method:</para>
/// <list type="bullet">
/// <item><b>Detection</b> — <see cref="ContainsExecutableMarkup"/> answers yes/no over a whole document and is
/// what <see cref="IdeaDiscovery"/> uses to REFUSE a foreign carried artifact outright. ADR 0021 requires
/// carried artifacts to be taken verbatim or not at all, so transformation is not available there.</item>
/// <item><b>Neutralization</b> — <see cref="SanitizeRawHtml"/> rewrites raw HTML that the repository's OWN
/// markdown passed through Markdig, dropping only the executable parts. ADR 0021's rejection of
/// sanitising-by-transformation is scoped to artifacts carried VERBATIM; the repo's own markdown is already
/// transformed by Markdig on the way to HTML, so a further transformation at that same seam does not
/// misrepresent an original the way rewriting a carried <c>forge-report.html</c> would. ADR 0042 argues the
/// distinction rather than assuming it.</item>
/// </list>
///
/// <para><b>Why this is not applied to rendered output as a whole.</b> This portal renders its own source, so
/// the string <c>onerror=</c> appears legitimately — escaped — inside code spans and fences on the generated
/// Code Map and on this story's own page. A regex pass over finished HTML would corrupt that documentation
/// while looking like it worked. Every entry point here operates on RAW HTML PASSTHROUGH ONLY (Markdig's
/// <c>HtmlBlock</c>/<c>HtmlInline</c> nodes and link destinations), which by construction excludes code spans
/// and fenced blocks — those are separate node types that Markdig already escapes.</para>
/// </summary>
public static class HtmlSafety
{
    /// <summary>Elements that execute regardless of attributes. Escaped whole rather than attribute-filtered:
    /// an <c>&lt;iframe srcdoc&gt;</c> runs script, and <c>&lt;object&gt;</c>/<c>&lt;embed&gt;</c> load a
    /// document. Same list ADR 0021 §Decision already writes, and the same one
    /// <see cref="ContainsExecutableMarkup"/> detects.</summary>
    private static readonly HashSet<string> ForbiddenElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "iframe", "object", "embed", "base", "form", "meta", "link",
    };

    /// <summary>Attributes that carry a URL, and so must be scheme-checked. <c>srcdoc</c> is deliberately absent
    /// — it is not a URL but an inline document, and is dropped unconditionally by
    /// <see cref="IsForbiddenAttribute"/>.</summary>
    private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "src", "action", "formaction", "data", "poster", "xlink:href", "ping", "background",
    };

    /// <summary>"Self-contained and script-free", as one detection over raw report text. Moved here from
    /// <see cref="IdeaDiscovery"/> by Story 17.2 so the repository has ONE statement of the policy — a second
    /// copy is precisely the SSOT defect Story 17.1 is sweeping up. <see cref="IdeaDiscovery"/> still owns the
    /// DECISION to reject; this owns only the definition of "executable".
    ///
    /// [Story 18.4 review] The handler branch does not require a quote after <c>=</c>, so an unquoted
    /// <c>onerror=alert(1)</c> is caught too.
    ///
    /// [Story 17.2] <c>matchTimeout</c> added per Task 3 — this pattern reads third-party repository content.
    /// It is a flat alternation with no nested quantifier, so it is not a backtracking risk today; the timeout
    /// is the house invariant, not a diagnosis.</summary>
    private static readonly Regex ExecutableMarkupPattern = TimedRegex.New(
        @"<\s*(?:script|iframe|object|embed)\b|\son[a-z]+\s*=|javascript\s*:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>True when <paramref name="html"/> contains a script tag, an embedding element, an inline event
    /// handler, or a <c>javascript:</c> URL. Whole-document detection for the carried-artifact gate; it does not
    /// distinguish a real handler from one quoted inside prose, which is correct for its caller (a foreign
    /// artifact that merely DISCUSSES handlers is still refused, and the cost is one diagnostic).</summary>
    public static bool ContainsExecutableMarkup(string html) =>
        !string.IsNullOrEmpty(html) && ExecutableMarkupPattern.IsMatch(html);

    /// <summary>True for a URL whose scheme can execute. Leading control characters and whitespace are stripped
    /// first because browsers ignore them when resolving a scheme, so <c>"java\tscript:alert(1)"</c> and
    /// <c>"&#160;javascript:alert(1)"</c> both navigate — comparing the raw string would miss both.</summary>
    public static bool IsDangerousUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        // Strip everything a browser ignores while sniffing the scheme: leading whitespace, C0 controls, and
        // the NBSP/BOM pair that survives a copy-paste. Then remove interior whitespace and NUL up to the
        // first ':' — "java\nscript:" is a live vector, "java script :" is not a scheme at all but is
        // rejected anyway because the cost of a false reject here is one dead link.
        var span = url.AsSpan().TrimStart();
        var sb = new StringBuilder(span.Length);
        foreach (var ch in span)
        {
            if (ch == ':')
            {
                sb.Append(ch);
                break;
            }
            // IsWhiteSpace covers NBSP (U+00A0); the BOM (U+FEFF) is neither whitespace nor a control
            // char but is ignored by browsers sniffing a scheme, so it is named explicitly.
            if (char.IsWhiteSpace(ch) || char.IsControl(ch) || ch == '﻿') continue;
            sb.Append(ch);
        }

        var scheme = sb.ToString();
        if (scheme.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) return true;
        if (scheme.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase)) return true;

        // `data:` is not blanket-rejected — the site's own favicon is a data URI. Only the two media types a
        // browser will execute script from are.
        if (scheme.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = url.AsSpan().TrimStart().ToString();
            if (rest.Contains("text/html", StringComparison.OrdinalIgnoreCase)) return true;
            if (rest.Contains("image/svg+xml", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>Rewrites a raw-HTML passthrough fragment so nothing in it can execute, preserving everything
    /// that cannot. Legitimate structural markup this repository's own artifacts already use —
    /// <c>&lt;details&gt;</c>, <c>&lt;summary&gt;</c>, <c>&lt;kbd&gt;</c>, <c>&lt;br&gt;</c>, tables — passes
    /// through byte-identical, which is what keeps ADR 0016's verbatim carriage meaningful for benign content.
    ///
    /// <para><b>Fails closed.</b> A tag this cannot parse (an unterminated <c>&lt;</c>, a stray <c>&lt;!</c>
    /// processing instruction) is ESCAPED to visible text rather than passed through. Being unable to prove a
    /// fragment inert is treated the same as proving it dangerous.</para></summary>
    public static string SanitizeRawHtml(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw ?? string.Empty;
        // Fast path: nothing that could open a tag means nothing to rewrite.
        if (!raw.Contains('<')) return raw;

        var sb = new StringBuilder(raw.Length);
        var i = 0;
        while (i < raw.Length)
        {
            var lt = raw.IndexOf('<', i);
            if (lt < 0)
            {
                sb.Append(raw, i, raw.Length - i);
                break;
            }

            sb.Append(raw, i, lt - i);

            // An HTML comment is inert and is copied through whole. Markdig's own comment renderers handle the
            // cases that become visible annotations; anything reaching here is a comment embedded inside a
            // larger raw block.
            if (raw.AsSpan(lt).StartsWith("<!--", StringComparison.Ordinal))
            {
                var end = raw.IndexOf("-->", lt, StringComparison.Ordinal);
                if (end < 0)
                {
                    // Unterminated comment: escape the remainder rather than emit an open comment that would
                    // swallow the rest of the page.
                    sb.Append(PathUtil.Html(raw[lt..]));
                    break;
                }
                sb.Append(raw, lt, end + 3 - lt);
                i = end + 3;
                continue;
            }

            var tagEnd = FindTagEnd(raw, lt);
            if (tagEnd < 0)
            {
                sb.Append(PathUtil.Html(raw[lt..]));
                break;
            }

            var tag = raw[lt..(tagEnd + 1)];
            sb.Append(SanitizeTag(tag));
            i = tagEnd + 1;
        }

        return sb.ToString();
    }

    /// <summary>Index of the '&gt;' closing the tag opened at <paramref name="start"/>, skipping any '&gt;'
    /// inside a quoted attribute value. Returns -1 when the tag never closes.</summary>
    private static int FindTagEnd(string raw, int start)
    {
        var quote = '\0';
        for (var i = start + 1; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (quote != '\0')
            {
                if (ch == quote) quote = '\0';
                continue;
            }
            if (ch is '"' or '\'') { quote = ch; continue; }
            if (ch == '>') return i;
        }
        return -1;
    }

    private static string SanitizeTag(string tag)
    {
        // tag is "<...>" inclusive.
        var inner = tag[1..^1];
        var isClosing = inner.StartsWith('/');
        if (isClosing) inner = inner[1..];

        // Not a tag at all (`<!DOCTYPE`, `<?xml`, `< 5`). Escape — fail closed.
        if (inner.Length == 0 || !char.IsLetter(inner[0])) return PathUtil.Html(tag);

        var nameEnd = 0;
        while (nameEnd < inner.Length && (char.IsLetterOrDigit(inner[nameEnd]) || inner[nameEnd] is '-' or ':'))
            nameEnd++;
        var name = inner[..nameEnd];

        if (ForbiddenElements.Contains(name)) return PathUtil.Html(tag);

        // A closing tag carries no attributes; nothing to filter.
        if (isClosing) return tag;

        var attrs = ParseAttributes(inner[nameEnd..], out var selfClosing);
        if (attrs is null) return PathUtil.Html(tag); // unparseable attribute soup — fail closed

        var kept = new List<string>();
        foreach (var (attrName, rendered, value) in attrs)
        {
            if (IsForbiddenAttribute(attrName)) continue;
            if (UrlAttributes.Contains(attrName) && IsDangerousUrl(value)) continue;
            // A `style` attribute can reach a URL via url(...) and, historically, expression(). The portal's
            // own markdown does not author inline styles, so dropping is cheaper than parsing CSS.
            if (attrName.Equals("style", StringComparison.OrdinalIgnoreCase)
                && value is not null
                && (value.Contains("url", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("expression", StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add(rendered);
        }

        var sb = new StringBuilder();
        sb.Append('<').Append(name);
        foreach (var a in kept) sb.Append(' ').Append(a);
        if (selfClosing) sb.Append(" /");
        sb.Append('>');
        return sb.ToString();
    }

    /// <summary>Any <c>on*</c> handler, plus <c>srcdoc</c> (an inline document, so scheme-checking it is
    /// meaningless) and <c>xmlns:xlink</c>-era <c>xlink:href</c>'s scriptable cousin.</summary>
    private static bool IsForbiddenAttribute(string name) =>
        name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
        || name.Equals("srcdoc", StringComparison.OrdinalIgnoreCase)
        || name.Equals("http-equiv", StringComparison.OrdinalIgnoreCase);

    /// <summary>Splits an attribute region into (name, verbatim-rendered-text, decoded-value) triples.
    /// Returns null when the region cannot be parsed, so the caller can fail closed.</summary>
    private static List<(string Name, string Rendered, string? Value)>? ParseAttributes(string s, out bool selfClosing)
    {
        selfClosing = false;
        var result = new List<(string, string, string?)>();
        var i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) break;

            if (s[i] == '/')
            {
                selfClosing = true;
                i++;
                continue;
            }

            var nameStart = i;
            while (i < s.Length && !char.IsWhiteSpace(s[i]) && s[i] != '=' && s[i] != '/') i++;
            if (i == nameStart) return null; // a character that cannot start a name — fail closed
            var name = s[nameStart..i];

            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length || s[i] != '=')
            {
                result.Add((name, name, null)); // valueless attribute (e.g. `open`, `hidden`)
                continue;
            }

            i++; // '='
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) return null;

            string value;
            string rendered;
            if (s[i] is '"' or '\'')
            {
                var q = s[i];
                var close = s.IndexOf(q, i + 1);
                if (close < 0) return null;
                value = s[(i + 1)..close];
                rendered = $"{name}={q}{value}{q}";
                i = close + 1;
            }
            else
            {
                var valStart = i;
                while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
                value = s[valStart..i];
                // Re-quote a bare value so the emitted tag is well-formed regardless of what it contained.
                rendered = $"{name}=\"{PathUtil.Html(value)}\"";
            }

            // Browsers decode entities before resolving a URL, so `href="&#106;avascript:alert(1)"` navigates.
            // Inspecting the raw attribute text would miss it.
            result.Add((name, rendered, System.Net.WebUtility.HtmlDecode(value)));
        }

        return result;
    }
}
