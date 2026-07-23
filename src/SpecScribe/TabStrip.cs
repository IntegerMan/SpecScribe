using System.Text;

namespace SpecScribe;

/// <summary>The portal's standard pure-CSS, no-JS tab control (the same radio + <c>:has(:checked)</c> idiom the
/// code pages use, generalized). A <c>&lt;fieldset&gt;</c> of visually-hidden radios drives sibling panels; the
/// first tab is checked so the page leads with it. Panel visibility is resolved in CSS per modifier — the shipped
/// modifiers are <c>overview</c> and <c>graph</c> (Story 19.2); add a matching <c>.ss-tabpanel--{mod}</c> reveal
/// rule to introduce a new one. The radio group name must be unique per page so several pages consolidated into
/// one document (SPA/webview capture) don't cross-wire their tabs. [Story 19.2; charting-is-pure-svg-no-js]</summary>
public static class TabStrip
{
    /// <summary>One tab: a CSS <paramref name="Mod"/> shared by its <c>.ss-tab--{Mod}</c> label and
    /// <c>.ss-tabpanel--{Mod}</c> panel, a visible <paramref name="Label"/>, an optional decorative
    /// <paramref name="Icon"/> glyph, and the pre-rendered <paramref name="Panel"/> HTML.</summary>
    public readonly record struct Tab(string Mod, string Label, string Icon, string Panel);

    /// <summary>Renders the tab strip. <paramref name="groupName"/> is the radio-group name (unique per page);
    /// <paramref name="legend"/> names the choice for assistive tech (visually hidden). Fewer than two tabs → the
    /// lone panel renders bare (a one-tab control is not meaningful chrome).</summary>
    public static string Render(string groupName, string legend, IReadOnlyList<Tab> tabs)
    {
        if (tabs.Count == 0) return string.Empty;
        if (tabs.Count == 1) return tabs[0].Panel;

        var g = PathUtil.Html(groupName);
        var sb = new StringBuilder();
        sb.Append("<div class=\"ss-tabs\">\n  <fieldset class=\"ss-tablist\">\n");
        sb.Append($"    <legend class=\"sr-only\">{PathUtil.Html(legend)}</legend>\n");
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var check = i == 0 ? " checked" : "";
            sb.Append(
                $"    <label class=\"ss-tab ss-tab--{tab.Mod}\"><input type=\"radio\" class=\"ss-tab-input\" name=\"{g}\"{check}>" +
                $"{tab.Icon}<span>{PathUtil.Html(tab.Label)}</span></label>\n");
        }
        sb.Append("  </fieldset>\n");
        foreach (var tab in tabs)
        {
            sb.Append($"  <div class=\"ss-tabpanel ss-tabpanel--{tab.Mod}\">\n");
            sb.Append(tab.Panel);
            sb.Append("\n  </div>\n");
        }
        sb.Append("</div>\n");
        return sb.ToString();
    }

    /// <summary>A per-page-unique radio-group name derived from the page's output path (mirrors the code page's
    /// own tab-group naming so consolidated SPA/webview documents never cross-wire their tab radios).</summary>
    public static string GroupName(string outputRelativePath, string suffix)
    {
        var sb = new StringBuilder("tabs-");
        foreach (var c in PathUtil.NormalizeSlashes(outputRelativePath))
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        sb.Append('-').Append(suffix);
        return sb.ToString();
    }
}
