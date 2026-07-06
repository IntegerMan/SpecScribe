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

    /// <summary>The story-lifecycle css classes in narrative order (done → … → pending), the canonical order
    /// the delivery mosaic and any status roll-up iterate so segments and legends read consistently.</summary>
    public static readonly IReadOnlyList<string> StoryStages = new[] { "done", "review", "active", "ready", "drafted", "pending" };

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

    public static string EpicLabel(string cssClass) => cssClass switch
    {
        "done" => "Done",
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
        RequirementStatus.Ready => "ready",     // gold
        RequirementStatus.Planned => "pending", // tan
        _ => "deferred",                        // grey
    };

    public static string RequirementLabel(RequirementStatus status) => status switch
    {
        RequirementStatus.Done => "Done",
        RequirementStatus.Ready => "Ready for dev",
        RequirementStatus.Planned => "Planned",
        _ => "Deferred",
    };
}
