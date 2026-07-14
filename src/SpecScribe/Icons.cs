namespace SpecScribe;

/// <summary>The single source of truth for every inline-SVG icon the portal renders — a recognition layer
/// paired with existing text labels (never icon-only). Mirrors the "one classifier / one seam" discipline of
/// <see cref="StatusStyles"/>/<see cref="ModuleContext"/>: every glyph lives here, keyed by either the
/// <see cref="StatusStyles"/> css-class vocabulary (<see cref="ForStatus"/>) or the same artifact-type/section
/// labels <see cref="SiteNav"/>/<see cref="ModuleContext"/> already emit (<see cref="ForConcept"/>). Every icon
/// is decorative (<c>aria-hidden</c>, <c>focusable="false"</c>) because its text label always accompanies it,
/// colored with <c>currentColor</c> so it inherits the surrounding label/badge color (theme-consistent, no
/// hard-coded hex), and static (no animation, so reduced-motion is trivially satisfied). An unrecognized key
/// gracefully returns an empty string — the caller renders the label alone, never a broken glyph. [Story 2.5]</summary>
public static class Icons
{
    /// <summary>One glyph per <see cref="StatusStyles"/> lifecycle css-class, 1:1 with the six <c>--status-*</c>
    /// color tokens plus <c>deferred</c>. Unknown class → empty string (graceful, no icon).</summary>
    public static string ForStatus(string cssClass) => cssClass switch
    {
        "done" => Svg("<path d=\"M3 8.5 6.2 11.5 13 4.5\"/>"),
        "active" => Svg("<path d=\"M5 3.5 12 8 5 12.5Z\"/>", solid: true),
        "review" => Svg("<circle cx=\"6.8\" cy=\"6.8\" r=\"4.3\"/><path d=\"M10 10 13.5 13.5\"/>"),
        "ready" => Svg("<path d=\"M4 13.5V3.2\"/><path d=\"M4 3.5h7l-2 2.6 2 2.6H4\"/>"),
        "drafted" => Svg("<path d=\"M10.6 2.9 13 5.3 5.4 12.9 2.6 13.3 3 10.5Z\"/>"),
        "pending" => Svg("<circle cx=\"8\" cy=\"8\" r=\"5.2\"/><path d=\"M8 5.2V8l2.3 1.4\"/>"),
        "deferred" => Svg("<circle cx=\"8\" cy=\"8\" r=\"5.2\"/><path d=\"M5.6 5.6 10.4 10.4M10.4 5.6 5.6 10.4\"/>"),
        // Same crossed-circle as deferred (grey ledger history); class+label keep retired distinct. [spec-3-4]
        "retired" => Svg("<circle cx=\"8\" cy=\"8\" r=\"5.2\"/><path d=\"M5.6 5.6 10.4 10.4M10.4 5.6 5.6 10.4\"/>"),
        // Question-mark in a dashed ring — visibly distinct from every lifecycle glyph. [Story 8.2]
        "unrecognized" => Svg("<circle cx=\"8\" cy=\"8\" r=\"5.2\" stroke-dasharray=\"2 1.5\"/><path d=\"M5.8 5.6c0-1.4 1-2.2 2.2-2.2s2.2.8 2.2 2c0 1.2-1.1 1.6-1.6 2.2-.4.4-.6.8-.6 1.4\"/><circle cx=\"8\" cy=\"12.2\" r=\".7\" fill=\"currentColor\" stroke=\"none\"/>"),
        _ => string.Empty,
    };

    /// <summary>One glyph per artifact-type / navigation-section concept, keyed by the exact label
    /// <see cref="SiteNav"/> (nav items, quick-links) and the home index's section-title bands already use, so
    /// there is no third naming scheme to keep in sync. Unknown/uncurated label → empty string (graceful).</summary>
    public static string ForConcept(string? label)
    {
        if (label is not { Length: > 0 }) return string.Empty;

        return label.Trim() switch
        {
            "Home" => Svg("<path d=\"M2.5 8 8 3.3 13.5 8\"/><path d=\"M4 6.7V13h8V6.7\"/>"),
            "Readme" => Svg("<path d=\"M3 2.8h10v10.4H3Z\"/><path d=\"M5.2 5.6h5.6M5.2 8h5.6M5.2 10.4h3.4\"/>"),
            "PRD" => Svg("<path d=\"M4 2.5h6.2L12.5 4.8V13.5H4Z\"/><path d=\"M6 7h4M6 9.4h4M6 11.8h2.6\"/>"),
            "Product Brief" => Svg("<path d=\"M3.2 4.8h9.6v8.4H3.2Z\"/><path d=\"M5.6 4.8V3.2h4.8v1.6\"/><path d=\"M5.6 8h4.8\"/>"),
            "UX Design" => Svg("<circle cx=\"8\" cy=\"8\" r=\"5.3\"/><path d=\"M8 2.7v1.6M8 11.7v1.6M2.7 8h1.6M11.7 8h1.6\"/>"),
            "UX Experience" => Svg("<path d=\"M2.8 8c1.6-3.2 4-4.8 5.2-4.8s3.6 1.6 5.2 4.8c-1.6 3.2-4 4.8-5.2 4.8S4.4 11.2 2.8 8Z\"/><circle cx=\"8\" cy=\"8\" r=\"1.6\"/>"),
            "Architecture" => Svg("<path d=\"M3 13.5V6.2L8 2.7l5 3.5v7.3\"/><path d=\"M6 13.5V9h4v4.5\"/>"),
            "Epics" => Svg("<path d=\"M2.7 5.5 8 3l5.3 2.5L8 8Z\"/><path d=\"M2.7 8.7 8 11.2l5.3-2.5\"/>"),
            "Requirements" => Svg("<path d=\"M4 2.8h8v10.4H4Z\"/><path d=\"m5.8 6 1.1 1.1 1.8-2.1\"/><path d=\"M5.8 9.8h4.4\"/>"),
            "ADRs" => Svg("<path d=\"M8 3v9.5\"/><path d=\"M8 4.2 4.6 4.2 3 7.3h3.6ZM8 4.2 11.4 4.2 13 7.3H9.4Z\"/><path d=\"M4.6 13h6.8\"/>"),
            "Spec" => Svg("<path d=\"M6 3.2 2.7 8l3.3 4.8\"/><path d=\"M10 3.2 13.3 8 10 12.8\"/>"),
            "Sprint" => Svg("<path d=\"M3.5 3.6h9v9.8h-9Z\"/><path d=\"M3.5 6.4h9M5.8 2.4v2.4M10.2 2.4v2.4\"/>"),
            "Overview" => Svg("<path d=\"M2.8 2.8h4.4v4.4H2.8Z\"/><path d=\"M8.8 2.8h4.4v4.4H8.8Z\"/><path d=\"M2.8 8.8h4.4v4.4H2.8Z\"/><path d=\"M8.8 8.8h4.4v4.4H8.8Z\"/>"),
            // A folder outline — the shared glyph for the code-map surface (nav item + dashboard quick link both
            // reuse this ONE key). Re-keyed from the retired Story 3.4 "Structure" surface. [Story 7.6]
            "Code Map" => Svg("<path d=\"M2.5 4.2H6l1.2 1.4h6.3v6.9a1 1 0 0 1-1 1H3.5a1 1 0 0 1-1-1Z\"/>"),
            "Planning Artifacts" => Svg("<path d=\"M8 2.6v1.8M8 11.6v1.8M2.6 8h1.8M11.6 8h1.8\"/><circle cx=\"8\" cy=\"8\" r=\"3.4\"/>"),
            "Spec Kernel" => Svg("<path d=\"M4.2 7.2h7.6v6H4.2Z\"/><path d=\"M5.8 7.2V5.4a2.2 2.2 0 0 1 4.4 0v1.8\"/>"),
            "Implementation Artifacts" => Svg("<path d=\"M9.6 2.9a3 3 0 0 1 3.5 3.5l-5.4 5.4-3.9.9.9-3.9Z\"/>"),
            "Direct & Quick-Dev Work" => Svg("<path d=\"M8.6 2.5 3.8 9h3.2L7 13.5 12.2 6.6H9Z\"/>", solid: true),
            "Deferred" => Svg("<circle cx=\"8\" cy=\"8\" r=\"5.2\"/><path d=\"M5.6 5.6 10.4 10.4M10.4 5.6 5.6 10.4\"/>"),
            _ => string.Empty,
        };
    }

    /// <summary>One glyph per requirement kind — a shape channel distinguishing Functional from Non-Functional
    /// requirements on the compact status tiles, so the tiles carry kind as shape (not color, which encodes
    /// status). Functional = a gear/cog (behavior the system <em>does</em>); Non-Functional = a shield (a
    /// quality/constraint the system must <em>uphold</em>). Decorative like every other icon — the tile's id
    /// text + rich tooltip carry the meaning. [Story 3.7 follow-up]</summary>
    public static string ForRequirementKind(RequirementKind kind) => kind switch
    {
        RequirementKind.Functional => Svg("<circle cx=\"8\" cy=\"8\" r=\"2.2\"/><path d=\"M8 1.8v2M8 12.2v2M1.8 8h2M12.2 8h2M3.6 3.6 5 5M11 11l1.4 1.4M12.4 3.6 11 5M5 11l-1.4 1.4\"/>"),
        _ => Svg("<path d=\"M8 2.2 12.8 4v4.2c0 3-2.1 4.9-4.8 5.8-2.7-.9-4.8-2.8-4.8-5.8V4Z\"/>"),
    };

    /// <summary>One glyph per code-file-page tab (Story 7.x tab split): the four view labels the code page's tab
    /// strip emits — "Insights" (a lightbulb: what the file <em>means</em>), "Relationships" (a node-link/share
    /// mark: what refers to it), "History" (a clock with a rewind arc: how it changed over time), and "Code" (angle
    /// brackets: the source itself). Decorative like every other icon — each tab keeps its visible text label, so an
    /// unrecognized label gracefully returns an empty string and the tab renders label-only.</summary>
    public static string ForCodeTab(string label) => label switch
    {
        "Insights" => Svg("<path d=\"M8 2.2a3.6 3.6 0 0 0-2.2 6.4c.5.4.8 1 .9 1.6h2.6c.1-.6.4-1.2.9-1.6A3.6 3.6 0 0 0 8 2.2Z\"/><path d=\"M6.4 12.2h3.2M7 13.6h2\"/>"),
        "Relationships" => Svg("<circle cx=\"4\" cy=\"8\" r=\"1.7\"/><circle cx=\"12\" cy=\"4.2\" r=\"1.7\"/><circle cx=\"12\" cy=\"11.8\" r=\"1.7\"/><path d=\"M5.5 7.2 10.5 4.8M5.5 8.8 10.5 11.2\"/>"),
        "History" => Svg("<path d=\"M2.8 8a5.2 5.2 0 1 1 1.6 3.75\"/><path d=\"M2.4 11.6 2.4 9 5 9\"/><path d=\"M8 5.2V8l2 1.3\"/>"),
        "Code" => Svg("<path d=\"M5.6 4 2.2 8l3.4 4\"/><path d=\"M10.4 4 13.8 8l-3.4 4\"/><path d=\"M9.2 3.2 6.8 12.8\"/>"),
        _ => string.Empty,
    };

    /// <summary>Wraps glyph markup in the shared decorative-icon shell: <c>aria-hidden</c>/<c>focusable="false"</c>
    /// (the text label carries the meaning), the <c>ss-icon</c> css hook for sizing, and <c>currentColor</c>
    /// stroke/fill so every icon inherits the surrounding label/badge color — never a hard-coded hex.</summary>
    private static string Svg(string paths, bool solid = false)
    {
        var fill = solid ? "currentColor" : "none";
        return "<svg class=\"ss-icon\" viewBox=\"0 0 16 16\" aria-hidden=\"true\" focusable=\"false\" " +
               $"fill=\"{fill}\" stroke=\"currentColor\" stroke-width=\"1.3\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{paths}</svg>";
    }
}
