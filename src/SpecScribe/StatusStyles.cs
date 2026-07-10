namespace SpecScribe;

/// <summary>The single source of truth for status → color semantics across every chart, chip, and badge:
/// parchment = pending · gold = drafted/ready (planned, no dev yet) · teal = active dev · green = done only.
/// Keeping this in one place is what stops "green creep" — planned work must never read as finished.</summary>
public static class StatusStyles
{
    /// <summary>CSS class for a story: from its artifact's Status line when present, else whether it's
    /// been drafted at all.</summary>
    public static string ForStory(StoryInfo story) => ForStatus(story.Status);

    /// <summary>Maps a raw "Status:" string (a story's, or a quick-dev doc's frontmatter status) onto the
    /// shared six-stage lifecycle css class, so every chart/badge routes through this one classifier rather
    /// than reimplementing the keyword match. An empty/unrecognized status falls back to "drafted" (listed,
    /// not yet classified) — the same fallback a drafted-but-unstarted story has always used.</summary>
    public static string ForStatus(string? status)
    {
        if (status is { Length: > 0 } value)
        {
            var s = value.Trim().ToLowerInvariant();
            if (s.Contains("done") || s.Contains("complete")) return "done";
            if (s.Contains("review")) return "review";
            if (s.Contains("progress") || s.Contains("in-dev")) return "active";
            if (s.Contains("ready")) return "ready";
        }

        // Drafted in epics.md but no implementation artifact yet.
        return "drafted";
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
        _ => "Pending",
    };

    /// <summary>The story-lifecycle css classes in narrative order (done → … → drafted), the canonical order
    /// the delivery mosaic and any status roll-up iterate so segments and legends read consistently.
    /// "pending" is excluded: <see cref="ForStory"/>/<see cref="ForStatus"/> never produce it (an
    /// unrecognized status falls back to "drafted"), so it would only ever be a dead, unreachable stage
    /// here — unlike <see cref="ForEpic"/>, which does have a real "pending" tier.</summary>
    public static readonly IReadOnlyList<string> StoryStages = new[] { "done", "review", "active", "ready", "drafted" };

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
    public static readonly IReadOnlyList<string> EpicStages = new[] { "done", "review", "active", "ready", "drafted", "pending" };

    public static string EpicLabel(string cssClass) => cssClass switch
    {
        "done" => "Done",
        "review" => "In review",
        "active" => "In development",
        "ready" => "Ready for dev",
        "drafted" => "Stories drafted",
        _ => "Pending",
    };

    /// <summary>CSS class for a requirement, mapping its rolled-up status onto the same color vocabulary
    /// used for epics/stories, with a distinct grey "deferred" for requirements shelved for later.</summary>
    public static string ForRequirement(RequirementInfo req) => req.Status switch
    {
        RequirementStatus.Done => "done",       // green
        RequirementStatus.Active => "active",   // teal — partially implemented
        RequirementStatus.Ready => "ready",     // gold
        RequirementStatus.Planned => "pending", // tan
        _ => "deferred",                        // grey
    };

    public static string RequirementLabel(RequirementStatus status) => status switch
    {
        RequirementStatus.Done => "Done",
        RequirementStatus.Active => "Partially implemented",
        RequirementStatus.Ready => "Ready for dev",
        RequirementStatus.Planned => "Planned",
        _ => "Deferred",
    };

    /// <summary>Maps a <c>sprint-status.yaml</c> lifecycle value onto the SAME six-stage color vocabulary as
    /// stories/epics — the yaml is the authoritative <em>tracking</em> ledger (distinct from the derived
    /// artifact status), but a reader who learned the colors on the sunburst must read the sprint page for
    /// free. Covers development_status (<c>backlog→ready-for-dev→in-progress→review→done</c>), retrospective
    /// (<c>optional</c>/<c>done</c>), and action-item (<c>open</c>/<c>in-progress</c>/<c>done</c>) values.
    /// Unknown/forward-compat values fall back to <c>pending</c> (parchment) rather than inventing a color.
    /// [Story 2.3 Task 2]</summary>
    public static string ForSprint(string? status) => Normalize(status) switch
    {
        "done" => "done",                 // green
        "review" => "review",             // deep teal
        "in-progress" => "active",        // teal
        "ready-for-dev" => "ready",       // gold
        "open" => "ready",                // action item awaiting action — gold, same "to do" tier as ready
        "backlog" => "pending",           // parchment
        "optional" => "pending",          // retrospective not yet done — parchment
        _ => "pending",
    };

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
        "" => "Unknown",
        _ => TitleCase(Normalize(status)),
    };

    /// <summary>Maps a planning <em>document's own</em> frontmatter <c>status:</c> (the free-text words BMad
    /// docs actually declare — <c>final</c>, <c>draft</c>, <c>ready</c>, …) onto the SAME six-stage badge
    /// vocabulary as stories/epics/sprint, so a reader who learned the colors elsewhere reads a planning-card
    /// badge for free. This is a third, independent signal — the document's self-reported state — deliberately
    /// NOT reconciled with the sprint or derived-artifact status (Story 1.5 truthfulness). Empty/null/unknown
    /// falls back to <c>pending</c> (parchment) rather than inventing a color. [Story 2.4 Task 1]</summary>
    public static string ForDoc(string? status) => Normalize(status) switch
    {
        "final" or "approved" or "done" or "complete" or "published" => "done",   // green
        "review" or "in review" or "in-review" => "review",                       // deep teal
        "in-progress" or "in progress" or "wip" or "active" => "active",           // teal
        "ready" or "ready-for-dev" => "ready",                                     // gold
        "draft" or "drafting" or "proposed" => "drafted",                          // gold
        _ => "pending",                                                            // parchment
    };

    /// <summary>Human, on-brand badge text for a planning doc's status — the doc's own declared word, title-
    /// cased ("final" → "Final", "ready-for-dev" → "Ready For Dev"), NOT remapped to a lifecycle noun, so the
    /// badge stays truthful to what the document itself says (Story 1.5). Empty → "Pending" to match the
    /// fallback class, though callers omit the badge entirely when a doc carries no status. [Story 2.4 Task 1]</summary>
    public static string DocLabel(string? status)
    {
        var s = Normalize(status);
        return s.Length == 0 ? "Pending" : TitleCase(s);
    }

    /// <summary>The status glyph for a lifecycle css-class, delegating to <see cref="Icons"/> so the icon stays
    /// anchored to this one status seam rather than letting callers reach into <see cref="Icons"/> with ad-hoc
    /// strings. Adds a shape channel alongside the existing color+text — no new status vocabulary. [Story 2.5]</summary>
    public static string Icon(string cssClass) => Icons.ForStatus(cssClass);

    /// <summary>Renders a complete <c>.status-badge</c> span with its icon prepended before the text — the one
    /// place icon+text pairing is defined, so every badge site calls this instead of hand-inlining the icon
    /// and risking drift (UX-DR17: color + icon + word, never icon-only). [Story 2.5 Task 3]</summary>
    public static string Badge(string cssClass, string label) =>
        $"<span class=\"status-badge {cssClass}\">{Icon(cssClass)}{PathUtil.Html(label)}</span>";

    private static string Normalize(string? status) => (status ?? string.Empty).Trim().ToLowerInvariant();

    private static string TitleCase(string value) =>
        System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('-', ' '));
}
