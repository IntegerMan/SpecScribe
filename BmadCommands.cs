using System.Text;

namespace DocsForge;

/// <summary>Suggests the BMad (GDS module) commands that make sense as next steps for a story or epic,
/// given its current status — rendered as a "Next Steps" panel so the workflow prompt is always one
/// copy-paste away. Mappings are grounded in the actual GDS production-phase workflow chain recorded in
/// _bmad/gds/module-help.csv (preceded-by column): sprint-planning -> create-story -> dev-story ->
/// code-review -> retrospective, with create-epics-and-stories -> check-implementation-readiness feeding
/// it from the planning phase.</summary>
public static class BmadCommands
{
    private sealed record Suggestion(string Command, string Description);

    public static string RenderNextSteps(StoryInfo story, string artifactSourcePath) =>
        RenderPanel(ForStory(story, artifactSourcePath));

    public static string RenderEpicNextSteps(EpicInfo epic) =>
        RenderPanel(ForEpic(epic));

    public static string RenderProjectNextSteps(EpicsModel model) =>
        RenderPanel(ForProject(model));

    /// <summary>The heading + list on their own (no chart-panel wrapper), for callers that want to fold
    /// the Next Steps into a shared panel rather than give it a standalone one.</summary>
    public static string RenderEpicNextStepsInner(EpicInfo epic) =>
        RenderInner(ForEpic(epic));

    private static string RenderPanel(List<Suggestion> suggestions)
    {
        var inner = RenderInner(suggestions);
        return inner.Length == 0
            ? string.Empty
            : $"<div class=\"chart-panel next-steps\">\n{inner}</div>\n\n";
    }

    private static string RenderInner(List<Suggestion> suggestions)
    {
        if (suggestions.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<h3>Next Steps (BMad)</h3>\n<ul class=\"next-steps-list\">\n");
        foreach (var s in suggestions)
        {
            sb.Append($"  <li><code>{PathUtil.Html(s.Command)}</code><span class=\"next-steps-desc\">{PathUtil.Html(s.Description)}</span></li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static List<Suggestion> ForStory(StoryInfo story, string artifactSourcePath)
    {
        var path = PathUtil.NormalizeSlashes(Path.Combine("_bmad-output", artifactSourcePath));
        var status = story.Status?.Trim().ToLowerInvariant() ?? string.Empty;

        if (status.Contains("ready"))
        {
            return new List<Suggestion>
            {
                new($"/gds-dev-story {path}",
                    "Implements the story exactly as specified — tasks, acceptance criteria, and dev notes drive the work."),
                new("/gds-code-review",
                    "Adversarial multi-layer review of the changes once implementation lands."),
            };
        }

        if (status.Contains("progress") || status.Contains("in-dev"))
        {
            return new List<Suggestion>
            {
                new($"/gds-dev-story {path}",
                    "Resumes implementation from the unchecked tasks in the story plan."),
                new("/gds-code-review",
                    "Review the work so far — worth running before marking the story done."),
            };
        }

        if (status.Contains("done") || status.Contains("complete") || status.Contains("review"))
        {
            return new List<Suggestion>
            {
                new("/gds-code-review",
                    "Final adversarial pass over the story's changes."),
                new("/gds-create-story",
                    "Drafts the next story file with full context for implementation."),
                new($"/gds-retrospective {story.EpicNumber}",
                    "Post-epic retrospective once the epic's last story closes."),
            };
        }

        // No recognizable status — the plan doesn't exist yet.
        var suggestions = new List<Suggestion>();

        // Before the epic's first story starts, it's worth confirming GDD/UX/architecture/epics still
        // agree with each other — cheaper to catch a misalignment here than mid-implementation.
        if (story.Id.EndsWith(".1", StringComparison.Ordinal))
        {
            suggestions.Add(new("/gds-check-implementation-readiness",
                "Verifies the GDD, UX spec, architecture, and epics stay aligned before this epic's first story begins."));
        }

        suggestions.Add(new($"/gds-create-story {story.Id}",
            "Generates the dedicated story file with full implementation context."));

        return suggestions;
    }

    private static List<Suggestion> ForEpic(EpicInfo epic)
    {
        var epicClass = StatusStyles.ForEpic(epic);

        if (epicClass == "pending")
        {
            return new List<Suggestion>
            {
                new("/gds-create-epics-and-stories",
                    $"Drafts the story breakdown for Epic {epic.Number} from the GDD and architecture — it doesn't have one yet."),
            };
        }

        if (epicClass == "done")
        {
            return new List<Suggestion>
            {
                new($"/gds-retrospective {epic.Number}",
                    "Runs a retrospective now that every story in this epic is done."),
            };
        }

        if (epicClass == "active")
        {
            return new List<Suggestion>
            {
                new("/gds-sprint-status",
                    "Surfaces this epic's current risks and the recommended next action — see the in-development story's own page for its specific dev command."),
            };
        }

        // "drafted" — stories are listed but none has a detailed implementation plan yet.
        var suggestions = new List<Suggestion>
        {
            new("/gds-sprint-planning",
                "Refreshes sprint tracking from the current epics/stories — the prerequisite gds-create-story expects."),
        };

        var nextUndetailed = epic.Stories.FirstOrDefault(s => s.ArtifactOutputPath is null);
        if (nextUndetailed is not null)
        {
            suggestions.Add(new($"/gds-create-story {nextUndetailed.Id}",
                "Generates the implementation plan for the next story in this epic."));
        }

        return suggestions;
    }

    /// <summary>The project-level moves that make sense right now, in the order a developer would reason
    /// through them: build the story that's the current front line, draft the next story that still lacks a
    /// plan, and break down the next epic that hasn't been sharded into stories. These are stacked (not
    /// mutually exclusive) so the home page shows the whole "what's next for the project" picture at once —
    /// story-level chores like code review live on the individual story pages, not here. Falls back to a
    /// project-wide retrospective only once every epic is drafted and every story detailed.</summary>
    private static List<Suggestion> ForProject(EpicsModel model)
    {
        var suggestions = new List<Suggestion>();
        var allStories = model.Epics.SelectMany(e => e.Stories).ToList();

        // The current front line — a story that's ready or already in development. Referenced by its short
        // id (e.g. "1.1"), which the gds-dev-story command resolves to the plan itself.
        var actionable = allStories.FirstOrDefault(s => StatusStyles.ForStory(s) is "ready" or "active");
        if (actionable is not null)
        {
            suggestions.Add(new($"/gds-dev-story {actionable.Id}",
                $"Story {actionable.Id} is the current front line — implements it per its plan."));
        }

        // The next story that still needs an implementation plan drawn up, in a drafted epic.
        var epicNeedingStory = model.Epics.FirstOrDefault(e =>
            e.Status == EpicStatus.Drafted && e.Stories.Any(s => s.ArtifactOutputPath is null));
        if (epicNeedingStory is not null)
        {
            var nextStory = epicNeedingStory.Stories.First(s => s.ArtifactOutputPath is null);
            suggestions.Add(new($"/gds-create-story {nextStory.Id}",
                $"Drafts the implementation plan for Epic {epicNeedingStory.Number}'s next story ({nextStory.Id}) — it doesn't have one yet."));
        }

        // The next epic still waiting to be broken down into stories.
        var pendingEpic = model.Epics.FirstOrDefault(e => e.Status == EpicStatus.Pending);
        if (pendingEpic is not null)
        {
            suggestions.Add(new("/gds-create-epics-and-stories",
                $"Breaks Epic {pendingEpic.Number} down into stories — the next epic still awaiting a story breakdown."));
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add(new("/gds-retrospective",
                "Every epic is drafted and every story detailed — a good point for a project-wide retrospective."));
        }

        return suggestions;
    }
}
