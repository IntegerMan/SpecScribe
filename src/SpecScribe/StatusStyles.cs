using System.Text;

namespace SpecScribe;

/// <summary>
/// <para><b>Native-vocabulary → canonical-lifecycle mapping seam</b> (Story 8.2 / FR20). This class is the
/// single place a framework's native status words become SpecScribe's canonical lifecycle stages. Today BMad's
/// keyword map lives here; a future framework adapter (Epic 4 Stories 4.3–4.7) plugs its own native→canonical
/// map into this seam rather than into templaters. <see cref="IArtifactAdapter"/> (Story 4.1) deliberately does
/// <em>not</em> classify status — it emits raw native strings on the parsed models; classification stays here.</para>
/// <para><b>Canonical lifecycle</b> (one vocabulary per entity type that uses it):
/// <c>pending → drafted → ready → active → review → done</c>, plus <c>deferred</c> for requirements,
/// <c>retired</c> for sprint-ledger history (removed from the active plan), and <c>unrecognized</c> when a
/// present native word has no mapping. Entity → classifier:
/// stories / free-text Status lines → <see cref="ForStatus"/>; epics → <see cref="ForEpic"/> /
/// <see cref="ForEpicWithRetrospective"/>; requirements → <see cref="ForRequirement"/>; sprint ledger →
/// <see cref="ForSprint"/>.</para>
/// <para>Color comes only from the <c>--status-*</c> CSS tokens — parchment = pending · gold = drafted/ready
/// (planned, no dev yet) · teal = active · deep teal = review · green = done only · grey = deferred/retired ·
/// distinct hatched neutral = unrecognized. Keeping classification here stops "green creep" and silent mislabel.</para>
/// </summary>
public static class StatusStyles
{
    /// <summary>CSS class for a story: from its artifact's Status line when present, else whether it's
    /// been drafted at all.</summary>
    public static string ForStory(StoryInfo story) => ForStatus(story.Status);

    /// <summary>Maps a raw "Status:" string (a story's, or a quick-dev doc's frontmatter status) onto the
    /// shared six-stage lifecycle css class, so every chart/badge routes through this one classifier rather
    /// than reimplementing the keyword match.
    /// <para><b>Absent vs. unmapped (Story 8.2 AC #3):</b> null/empty/whitespace → <c>drafted</c> (listed in
    /// epics.md, not yet classified — unchanged). A non-empty string matching no known keyword →
    /// <c>unrecognized</c> (never silently coerced to a real stage).</para></summary>
    public static string ForStatus(string? status)
    {
        if (status is not { Length: > 0 } value)
        {
            // Drafted in epics.md but no implementation artifact yet — OR a blank Status line.
            return "drafted";
        }

        var s = value.Trim();
        if (s.Length == 0) return "drafted";

        // ForSprint-shaped: normalize then exact/synonym match. Token checks (not bare Contains) so
        // "incomplete" cannot invent "done" via a "complete" substring. [spec-epic8-deferred-debt-cleanup]
        var n = Normalize(s).Replace('_', '-').Replace(' ', '-').TrimEnd('.', '!', '?', ':', ',');
        return n switch
        {
            "done" or "complete" or "completed" => "done",
            "review" or "in-review" => "review",
            "active" or "wip" or "in-progress" or "in-dev" or "progress" => "active",
            "ready" or "ready-for-dev" => "ready",
            "draft" or "drafted" => "drafted",
            _ => ForStatusFromTokens(n),
        };
    }

    /// <summary>Word-token fallback after exact synonym miss — matches known stage tokens as whole kebab
    /// segments so substring traps like <c>incomplete</c>⊇<c>complete</c> cannot invent a stage.
    /// Done/complete stay exact-only above so <c>not-complete</c> / <c>almost-complete</c> stay unrecognized.</summary>
    private static string ForStatusFromTokens(string normalized)
    {
        var tokens = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Multi-segment "…-in-dev-…" keeps the explicit in-dev synonym without bare Contains.
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (string.Equals(tokens[i], "in", StringComparison.Ordinal)
                && string.Equals(tokens[i + 1], "dev", StringComparison.Ordinal))
                return "active";
        }

        if (tokens.Contains("review", StringComparer.Ordinal)) return "review";
        if (tokens.Contains("progress", StringComparer.Ordinal)
            || tokens.Contains("active", StringComparer.Ordinal)
            || tokens.Contains("wip", StringComparer.Ordinal))
            return "active";
        if (tokens.Contains("ready", StringComparer.Ordinal)) return "ready";
        if (tokens.Contains("draft", StringComparer.Ordinal)
            || tokens.Contains("drafted", StringComparer.Ordinal))
            return "drafted";
        return "unrecognized";
    }

    /// <summary>Human label for a story-lifecycle css class — the accessible/tooltip name for the delivery
    /// mosaic's per-status ring segments. Mirrors <see cref="EpicLabel"/> but from the story's point of view
    /// ("Drafted", not "Stories drafted").</summary>
    public static string StoryLabel(string cssClass) => cssClass switch
    {
        "done" => "Done",
        "review" => "In review",
        "active" => "In development",
        "ready" => "Ready for dev",
        "drafted" => "Drafted",
        "unrecognized" => "Unrecognized",
        _ => "Pending",
    };

    /// <summary>The story-lifecycle css classes in narrative order (done → … → drafted), then
    /// <c>unrecognized</c>. The delivery mosaic and status roll-ups iterate this list so segments and legends
    /// stay consistent. "pending" is excluded: <see cref="ForStory"/>/<see cref="ForStatus"/> never produce it
    /// (an absent status falls back to "drafted"), so it would only ever be a dead stage here — unlike
    /// <see cref="ForEpic"/>, which does have a real "pending" tier. [Story 8.2]</summary>
    public static readonly IReadOnlyList<string> StoryStages =
        new[] { "done", "review", "active", "ready", "drafted", "unrecognized" };

    /// <summary>CSS class for an epic, derived from its stories: green only when every story is done;
    /// teal once any story has entered dev; gold "ready" once any story is ready-for-dev (mirroring the
    /// "any active → active" rule); gold "drafted" while merely drafted; parchment when pending.</summary>
    public static string ForEpic(EpicInfo epic)
    {
        if (epic.Status == EpicStatus.Pending || epic.Stories.Count == 0) return "pending";

        var storyClasses = epic.Stories.Select(ForStory).ToList();
        if (storyClasses.All(c => c == "done")) return "done";
        if (storyClasses.Any(c => c is "active" or "review" or "done")) return "active";
        // Any ready-for-dev story (with none further along) lifts the epic to the ready tier the story layer
        // already distinguishes, so the epic ring stops reading as merely "drafted" under ready stories.
        if (storyClasses.Any(c => c == "ready")) return "ready";
        // All present-but-unmapped stories → visible unrecognized (never silently "drafted"). Mixed
        // unrecognized + drafted (or empty Status → drafted) stays drafted. [Story 8.2 review]
        if (storyClasses.Count > 0 && storyClasses.All(c => c == "unrecognized")) return "unrecognized";
        return "drafted";
    }

    /// <summary>The retro-gated epic class for the VISUAL epic-status surfaces (sunburst, Epic Status donut,
    /// epics-index chips, epic page/card badges): an epic whose every story is done but which has no
    /// retrospective yet reads as "review" (delivered, retro pending) rather than "done" — so those surfaces
    /// don't call an epic finished until its retro closes it out. Every other tier defers to
    /// <see cref="ForEpic"/>. Kept SEPARATE from <see cref="ForEpic"/> on purpose: requirements roll-up
    /// (<c>RequirementsParser.DeriveStatus</c>) maps epic status onto implementation-completeness, where a retro
    /// (a closure ritual, not an implementation signal) must never downgrade a fully-built epic. [spec-sunburst-retro]</summary>
    public static string ForEpicWithRetrospective(EpicInfo epic)
    {
        var cls = ForEpic(epic);
        return cls == "done" && !epic.HasRetrospective ? "review" : cls;
    }

    /// <summary>The complete set of css classes <see cref="ForEpic"/> can return, in narrative order
    /// (done → … → pending). Mirrors <see cref="StoryStages"/>: one authored list, iterated by every epic
    /// roll-up consumer (e.g. the Epic Status donut), so a class can never be silently dropped by a consumer
    /// that forgot to bucket it. Unlike <see cref="StoryStages"/>, "pending" is a real reachable tier here.</summary>
    public static readonly IReadOnlyList<string> EpicStages =
        new[] { "done", "review", "active", "ready", "drafted", "pending", "unrecognized" };

    public static string EpicLabel(string cssClass) => cssClass switch
    {
        "done" => "Done",
        "review" => "In review",
        "active" => "In development",
        "ready" => "Ready for dev",
        "drafted" => "Stories drafted",
        "unrecognized" => "Unrecognized",
        _ => "Pending",
    };

    /// <summary>CSS class for a requirement, mapping its rolled-up status onto the same color vocabulary
    /// used for epics/stories, with a distinct grey "deferred" for requirements shelved for later.
    /// Requirement status is a parsed enum — it cannot be "unrecognized" (that applies only to free-text
    /// classifiers). [Story 8.2]</summary>
    public static string ForRequirement(RequirementInfo req) => req.Status switch
    {
        RequirementStatus.Done => "done",       // green
        RequirementStatus.Active => "active",   // teal — partially implemented
        RequirementStatus.Ready => "ready",     // gold
        RequirementStatus.Planned => "pending", // tan — covered, not started
        // Unmapped reuses the SAME tan --status-pending family as Planned (owner decision #1: no 7th token) —
        // the two states differ by icon + word (and, in the requirements-flow Sankey, a distinct bucket), never
        // by color. Deferred keeps its own dedicated grey --status-deferred. [Story 9.3 Task 2]
        RequirementStatus.Unmapped => "pending",
        _ => "deferred",                        // grey
    };

    public static string RequirementLabel(RequirementStatus status) => status switch
    {
        RequirementStatus.Done => "Done",
        RequirementStatus.Active => "Partially implemented",
        RequirementStatus.Ready => "Ready for dev",
        RequirementStatus.Planned => "Planned",
        RequirementStatus.Unmapped => "Not yet mapped",
        _ => "Deferred",
    };

    /// <summary>The complete status badge (color + icon + word) for a requirement, keyed off its enum status so
    /// the one <see cref="RequirementStatus.Unmapped"/> case carries its OWN distinct icon glyph while still
    /// routing its COLOR through the shared <c>pending</c>/tan class (owner decision #1: Unmapped reuses
    /// <c>--status-pending</c>, no 7th <c>--status-*</c> token). Because Unmapped and Planned share a color, the
    /// icon + word are what keep them distinct — satisfying "never color-only" (UX-DR17) independently of the
    /// color difference from Deferred. Every other status reads its icon from the same class as its color.
    /// [Story 9.3 Task 2]</summary>
    public static string RequirementBadge(RequirementInfo req)
    {
        var cssClass = ForRequirement(req);
        var iconClass = req.Status == RequirementStatus.Unmapped ? "unmapped" : cssClass;
        return Badge(cssClass, RequirementLabel(req.Status), iconClass);
    }

    /// <summary>Maps a <c>sprint-status.yaml</c> lifecycle value onto the SAME six-stage color vocabulary as
    /// stories/epics — the yaml is the authoritative <em>tracking</em> ledger (distinct from the derived
    /// artifact status), but a reader who learned the colors on the sunburst must read the sprint page for
    /// free. Covers development_status (<c>backlog→ready-for-dev→in-progress→review→done</c>), retrospective
    /// (<c>optional</c>/<c>done</c>), and action-item (<c>open</c>/<c>in-progress</c>/<c>done</c>) values.
    /// <para><b>Absent vs. unmapped (Story 8.2 AC #3):</b> null/empty → <c>pending</c> (unchanged). A non-empty
    /// string matching no known value → <c>unrecognized</c> (preserves the word via <see cref="SprintLabel"/>,
    /// never invents a lifecycle color).</para> [Story 2.3 Task 2; Story 8.2]</summary>
    public static string ForSprint(string? status)
    {
        // Spaced aliases ("in progress", "ready for dev") normalize to kebab so they match the yaml forms.
        var n = Normalize(status).Replace(' ', '-');
        if (n.Length == 0) return "pending";

        return n switch
        {
            "done" => "done",                 // green
            "review" => "review",             // deep teal
            "in-progress" => "active",        // teal
            "ready-for-dev" => "ready",       // gold
            "open" => "ready",                // action item awaiting action — gold, same "to do" tier as ready
            "backlog" => "pending",           // parchment
            "optional" => "pending",          // retrospective not yet done — parchment
            "retired" => "retired",           // removed from active plan; ledger history (not deferred)
            _ => "unrecognized",
        };
    }

    /// <summary>Human, on-brand label for a sprint lifecycle value — the visible badge text. Every status is a
    /// word (UX-DR17), color is reinforcement only. Unknown values are title-cased so a forward-compat status
    /// (e.g. <c>blocked</c>) still reads as a real word rather than a raw token. [Story 2.3 Task 2]</summary>
    public static string SprintLabel(string? status) => Normalize(status) switch
    {
        "done" => "Done",
        "review" => "In review",
        "in-progress" => "In progress",
        "ready-for-dev" => "Ready for dev",
        "backlog" => "Backlog",
        "optional" => "Optional",
        "open" => "Open",
        "retired" => "Retired",
        "" => "Unknown",
        _ => TitleCase(Normalize(status)),
    };

    /// <summary>The status glyph for a lifecycle css-class, delegating to <see cref="Icons"/> so the icon stays
    /// anchored to this one status seam rather than letting callers reach into <see cref="Icons"/> with ad-hoc
    /// strings. Adds a shape channel alongside the existing color+text — no new status vocabulary. [Story 2.5]</summary>
    public static string Icon(string cssClass) => Icons.ForStatus(cssClass);

    /// <summary>One-line plain-language meaning for a lifecycle css class — the shared source for badge
    /// tooltips and the portal-wide legend key. Unknown classes get a conservative fallback. [Story 8.2]
    /// <para>// 10.3: vocabulary-explanation seam — extend, don't duplicate</para></summary>
    public static string StageMeaning(string cssClass) => cssClass switch
    {
        // Pending/ready phrasings distinguish backlog vs ready-for-dev readiness (Story 8.4 UX-DR24);
        // also the shared source for sprint column-header tooltips.
        "pending" => "Not yet ready to pick up",
        "drafted" => "Stories or a plan exist; work has not started",
        "ready" => "Task plan exists and dependencies met",
        "active" => "Actively being developed",
        "review" => "Implementation complete; awaiting review or retrospective",
        "done" => "Finished and closed",
        "deferred" => "Shelved on purpose for later",
        // Distinct from "deferred" (a deliberate shelving) — a requirement with no covering epic at all. Reuses
        // the pending/tan color but carries this own meaning + glyph in a requirement badge. [Story 9.3]
        "unmapped" => "Listed, but not yet mapped to any epic or story",
        "retired" => "Removed from the active plan; kept for ledger history",
        "unrecognized" => "Native status word has no canonical mapping",
        _ => "Status stage",
    };

    /// <summary>Canonical stages shown in the portal-wide status legend key, in teaching order
    /// (pending → … → done), then deferred, retired, and unrecognized. Always complete — never zero-suppressed.
    /// [Story 8.2] <para>// 10.3: vocabulary-explanation seam — extend, don't duplicate</para></summary>
    public static readonly IReadOnlyList<string> LegendStages =
        new[] { "pending", "drafted", "ready", "active", "review", "done", "deferred", "unmapped", "retired", "unrecognized" };

    /// <summary>Renders a complete <c>.status-badge</c> span with its icon prepended before the text — the one
    /// place icon+text pairing is defined, so every badge site calls this instead of hand-inlining the icon
    /// and risking drift (UX-DR17: color + icon + word, never icon-only). Attaches <c>js-tip</c> +
    /// <c>data-tip</c> (and a native <c>title</c> fallback for non-JS surfaces) from <see cref="StageMeaning"/>.
    /// [Story 2.5 Task 3; Story 8.2]</summary>
    public static string Badge(string cssClass, string label) => Badge(cssClass, label, cssClass);

    /// <summary>Badge overload that lets the icon + tooltip meaning come from a DIFFERENT key than the color
    /// class. The one caller is <see cref="RequirementBadge"/>'s Unmapped case: it needs the tan
    /// <c>pending</c> color (owner decision #1) but its own <c>unmapped</c> glyph + meaning, so Unmapped stays
    /// visually distinct from Planned by icon + word alone. When <paramref name="iconClass"/> equals
    /// <paramref name="cssClass"/> the output is byte-identical to the two-arg overload. [Story 9.3 Task 2]</summary>
    public static string Badge(string cssClass, string label, string iconClass)
    {
        var tip = PathUtil.Html(StageMeaning(iconClass));
        var cls = PathUtil.Html(cssClass);
        return $"<span class=\"status-badge {cls} js-tip\" data-tip=\"{tip}\" title=\"{tip}\">{Icon(iconClass)}{PathUtil.Html(label)}</span>";
    }

    /// <summary>On-demand status legend disclosure: a compact "?" toggle that opens a single-column popover
    /// (swatch + icon + word + meaning) for every canonical stage. Static reference — never zero-suppresses.
    /// Call beside status-bearing headers/badges; native <c>&lt;details&gt;</c> so it works with JS off and under
    /// webview CSP. Token-driven swatches only.
    /// [Story 8.2] <para>// 10.3: vocabulary-explanation seam — extend, don't duplicate</para></summary>
    public static string LegendKey()
    {
        var sb = new StringBuilder();
        sb.Append("<details class=\"status-legend\">\n");
        sb.Append("  <summary class=\"status-legend-toggle\" aria-label=\"Show status legend\">?</summary>\n");
        sb.Append("  <div class=\"status-legend-panel\" role=\"region\" aria-label=\"Status legend\">\n");
        sb.Append("    <div class=\"status-legend-key-title\">Status legend</div>\n");
        sb.Append("    <ul class=\"status-legend-key-list\">\n");
        foreach (var stage in LegendStages)
        {
            // Single label seam — no parallel stage→word switch that can drift from StoryLabel / siblings.
            // [spec-epic8-deferred-debt-cleanup]
            var word = stage switch
            {
                "deferred" => RequirementLabel(RequirementStatus.Deferred),
                "unmapped" => RequirementLabel(RequirementStatus.Unmapped),
                "retired" => SprintLabel("retired"),
                _ => StoryLabel(stage),
            };
            var meaning = PathUtil.Html(StageMeaning(stage));
            // Unmapped reuses the pending/tan swatch (no 7th token) while icon + word stay distinct. [Story 9.9]
            var swatchClass = stage == "unmapped" ? "pending" : stage;
            sb.Append("      <li class=\"status-legend-key-row\">\n");
            sb.Append($"        <span class=\"status-legend-key-swatch {swatchClass}\" aria-hidden=\"true\"></span>\n");
            sb.Append($"        <span class=\"status-legend-key-label\">{Icon(stage)}{PathUtil.Html(word)}</span>\n");
            sb.Append($"        <span class=\"status-legend-key-meaning\">{meaning}</span>\n");
            sb.Append("      </li>\n");
        }
        sb.Append("    </ul>\n  </div>\n</details>");
        return sb.ToString();
    }

    /// <summary>True when a free-text classifier would emit the unrecognized stage for a <em>present</em>
    /// status string. Used by generation to emit non-fatal <see cref="AdapterDiagnostic"/> notices. [Story 8.2]</summary>
    public static bool IsUnrecognizedStatus(string? status) =>
        status is { Length: > 0 } s && s.Trim().Length > 0 && ForStatus(s) == "unrecognized";

    /// <summary>True when a sprint-ledger value is present but unmapped. Empty stays pending (not a notice).
    /// [Story 8.2]</summary>
    public static bool IsUnrecognizedSprintStatus(string? status) =>
        status is { Length: > 0 } s && s.Trim().Length > 0 && ForSprint(s) == "unrecognized";

    private static string Normalize(string? status) => (status ?? string.Empty).Trim().ToLowerInvariant();

    private static string TitleCase(string value) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('-', ' '));
}
