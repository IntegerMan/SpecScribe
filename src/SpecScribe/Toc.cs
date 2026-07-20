using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>The single shared table-of-contents seam: one sidebar renderer, one two-column page shell, and a
/// rendered-order heading extractor, reused by every TOC-bearing page type (generic docs, ADRs, README via
/// <see cref="HtmlTemplater"/>; epic/story detail via <see cref="EpicsTemplater"/>). The TOC is never forked
/// per page — callers just build an ordered <see cref="Entry"/> list in the page's actual rendered order.</summary>
public static class Toc
{
    /// <summary>One contents entry: a heading/section <paramref name="Level"/> (2 or 3 — 3 renders indented),
    /// its visible <paramref name="Text"/>, and the in-page <paramref name="AnchorId"/> it links to.</summary>
    public sealed record Entry(int Level, string Text, string AnchorId);

    /// <summary>Renders the accessible "On this page" sidebar nav from an ordered entry list. Returns "" when
    /// there are no entries so the caller can fall back to a single-column layout. A level-2 entry immediately
    /// followed by one or more level-3 entries groups them under a native, pure-CSS <c>&lt;details
    /// class="toc-group"&gt;</c> disclosure (open by default — the "On this page" TOC stays visible at a
    /// glance); a level-2 with no level-3 children stays a plain link (no empty caret). A stray level-3 with no
    /// preceding level-2 (shouldn't occur in practice) degrades to a plain link rather than being dropped
    /// (NFR8). [Story 10.5, AC2]</summary>
    public static string RenderSidebar(IReadOnlyList<Entry> entries)
    {
        if (entries.Count == 0) return string.Empty;

        // Detail-page TOCs merge hardcoded panel ids (sec-*, ac-N) with remainder-heading auto-ids into one
        // namespace; keep only the first entry per AnchorId so we never render two links to the same anchor
        // (a browser jumps to the first matching id, making the later link a silent dead end). Dedupe BEFORE
        // grouping so a parent's child count reflects only entries that will actually render.
        var seen = new HashSet<string>();
        var deduped = new List<Entry>();
        foreach (var e in entries)
        {
            if (seen.Add(e.AnchorId)) deduped.Add(e);
        }

        var sb = new StringBuilder();
        sb.Append("<nav class=\"toc-sidebar\" aria-label=\"On this page\">\n");
        sb.Append("  <span class=\"toc-label\">On this page</span>\n");

        for (var i = 0; i < deduped.Count; i++)
        {
            var e = deduped[i];
            if (e.Level >= 3)
            {
                sb.Append(RenderLink(e)); // stray leading h3 — degrade to a plain link, never drop it.
                continue;
            }

            var children = new List<Entry>();
            var j = i + 1;
            while (j < deduped.Count && deduped[j].Level >= 3)
            {
                children.Add(deduped[j]);
                j++;
            }

            if (children.Count == 0)
            {
                sb.Append(RenderLink(e));
                continue;
            }

            sb.Append("  <details class=\"toc-group\" open>\n");
            sb.Append($"    <summary><a class=\"toc-link\" href=\"#{PathUtil.Html(e.AnchorId)}\">{PathUtil.Html(e.Text)}</a></summary>\n");
            foreach (var child in children)
            {
                sb.Append("  ").Append(RenderLink(child));
            }
            sb.Append("  </details>\n");
            i = j - 1; // skip past the children just rendered
        }

        sb.Append("</nav>\n");
        return sb.ToString();
    }

    private static string RenderLink(Entry e)
    {
        var cls = e.Level >= 3 ? "toc-link toc-h3" : "toc-link";
        return $"  <a class=\"{cls}\" href=\"#{PathUtil.Html(e.AnchorId)}\">{PathUtil.Html(e.Text)}</a>\n";
    }

    /// <summary>Wraps main-content HTML and its sidebar rail into the shared two-column page shell. The rail
    /// holds the "On this page" TOC and, optionally, extra rail content stacked beneath it (e.g. a spec page's
    /// companion-documents block). With no TOC entries and no rail extra the content is returned unwrapped
    /// (single column) so pages with nothing to index keep their existing full-width layout.</summary>
    public static string WrapWithSidebar(string mainHtml, IReadOnlyList<Entry> entries, string? railExtra = null)
    {
        if (entries.Count == 0 && string.IsNullOrEmpty(railExtra)) return mainHtml;

        var sb = new StringBuilder();
        sb.Append("<div class=\"page-shell\">\n");
        sb.Append("<div class=\"page-main\">\n");
        sb.Append(mainHtml);
        sb.Append("</div>\n");
        // The sidebar rail: the TOC and any extra rail content (e.g. a spec page's companion-documents block)
        // stack in one sticky column so they scroll together and never fight for the same sticky slot.
        sb.Append("<div class=\"page-rail\">\n");
        sb.Append(RenderSidebar(entries));
        if (!string.IsNullOrEmpty(railExtra))
        {
            sb.Append(railExtra);
        }
        sb.Append("</div>\n");
        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    /// <summary>A minimal progressive-enhancement script that tracks which section is currently in view and
    /// toggles <c>.is-current</c> on the matching <c>.toc-link</c> — the ambient "you are here" feedback the
    /// sticky TOC lacked (every link already works with JS off; this only adds highlighting). Callers append
    /// this OUTSIDE the <c>&lt;main id="main-content"&gt;</c> landmark (mirroring exactly where
    /// <see cref="Mermaid.InitScript"/> is emitted) — never inside it, because <c>&lt;main&gt;</c> is the
    /// swappable "content region" the webview/SPA surfaces extract verbatim; a script embedded there would ride
    /// along into an <c>innerHTML</c> swap where it can never execute (dead code) and would trip the webview's
    /// no-script-in-content-region invariant (<c>SiteGeneratorWebviewTests.EverySurface_CarriesTheChromeAndNoScript</c>).
    /// Placed outside <c>&lt;main&gt;</c>, it simply never reaches those surfaces at all — a clean NFR8 degrade
    /// to today's static TOC there, matching the webview's own strict-CSP inline-script block. No
    /// <c>localStorage</c>/cookie/session write anywhere: the current section is recomputed live from scroll
    /// position on every load (FR31). Self-locating via <c>document.currentScript</c> so it finds the page's
    /// one <c>.toc-sidebar</c> regardless of exactly where after <c>&lt;main&gt;</c> it lands. [Story 10.11]</summary>
    public const string ActiveSectionScript = "<script>(function(){var toc=document.querySelector('.toc-sidebar');if(!toc)return;var links=Array.prototype.slice.call(toc.querySelectorAll('a.toc-link'));if(!links.length)return;if(typeof IntersectionObserver==='undefined')return;var map=[];links.forEach(function(link){var href=link.getAttribute('href');if(!href||href.charAt(0)!=='#')return;var target=document.getElementById(href.slice(1));if(target)map.push({link:link,target:target});});if(!map.length)return;var current=null;function setCurrent(link){if(current===link)return;if(current)current.classList.remove('is-current');current=link;if(current)current.classList.add('is-current');}var observer=new IntersectionObserver(function(entries){var visible=entries.filter(function(e){return e.isIntersecting;});if(!visible.length)return;visible.sort(function(a,b){return a.boundingClientRect.top-b.boundingClientRect.top;});var top=visible[0].target;var match=map.filter(function(m){return m.target===top;})[0];if(match)setCurrent(match.link);},{rootMargin:'-15% 0px -70% 0px',threshold:0});map.forEach(function(m){observer.observe(m.target);});})();</script>\n\n";

    private static readonly Regex HeadingTag = new(
        @"<h(?<lvl>[23])\b[^>]*\bid=""(?<id>[^""]+)""[^>]*>(?<text>.*?)</h\k<lvl>>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>Extracts level-2/3 headings that carry an id from already-rendered fragment HTML, in document
    /// order, as TOC entries — used to slot a detail page's remainder-fragment headings into its rendered-order
    /// TOC. Headings without an id are skipped (there is no anchor to link to), so no entry is ever a dead link.</summary>
    public static IReadOnlyList<Entry> ExtractHeadings(string html)
    {
        var entries = new List<Entry>();
        if (string.IsNullOrEmpty(html)) return entries;

        foreach (Match m in HeadingTag.Matches(html))
        {
            var level = m.Groups["lvl"].Value == "3" ? 3 : 2;
            var text = PathUtil.StripHtmlTags(m.Groups["text"].Value).Trim();
            if (text.Length == 0) continue;
            entries.Add(new Entry(level, text, m.Groups["id"].Value));
        }
        return entries;
    }
}
