using System.Text;

namespace SpecScribe;

/// <summary>Suggests the workflow commands that make sense as next steps for a story or epic, given its
/// current status — rendered as a "Next Steps" panel so the workflow prompt is always one copy-paste away.
/// The command strings come from the detected module's <see cref="CommandCatalog"/> (parsed from its
/// module-help.csv), so a BMad Method project shows <c>/bmad-*</c> commands and a Game Dev Studio project
/// shows <c>/gds-*</c> — the status-to-step logic is shared; only the concrete command names differ. A step
/// the active module doesn't expose is omitted rather than printed as a command that doesn't exist.</summary>
public static class BmadCommands
{
    private sealed record Suggestion(string Command, string Description);

    public static string RenderNextSteps(StoryInfo story, CommandCatalog commands) =>
        RenderPanel(ForStory(story, commands));

    public static string RenderEpicNextSteps(EpicInfo epic, CommandCatalog commands) =>
        RenderPanel(ForEpic(epic, commands));

    public static string RenderProjectNextSteps(EpicsModel model, CommandCatalog commands) =>
        RenderPanel(ForProject(model, commands));

    /// <summary>The heading + list on their own (no chart-panel wrapper), for callers that want to fold
    /// the Next Steps into a shared panel rather than give it a standalone one.</summary>
    public static string RenderEpicNextStepsInner(EpicInfo epic, CommandCatalog commands) =>
        RenderInner(ForEpic(epic, commands));

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
        sb.Append("<h3>Next Steps</h3>\n<ul class=\"next-steps-list\">\n");
        foreach (var s in suggestions)
        {
            // Each command carries a copy button (F2) so the exact next command is one click onto the
            // clipboard. The visible <code> stays selectable as the no-JS fallback; the button is wired by
            // specscribe.js. The command text is HTML-escaped both as visible text and as the data attribute.
            var cmd = PathUtil.Html(s.Command);
            sb.Append($"  <li><code>{cmd}</code>" +
                      $"<button type=\"button\" class=\"copy-btn\" data-copy=\"{cmd}\" aria-label=\"Copy command\">Copy</button>" +
                      $"<span class=\"next-steps-desc\">{PathUtil.Html(s.Description)}</span></li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    /// <summary>A de-emphasized inline "next action" for a partial/empty planning surface: a short lead-in
    /// followed by the copy-pasteable command, turning a dead-end note ("Stories not yet drafted.") into a
    /// signposted action. Returns <paramref name="fallback"/> unchanged when the module doesn't expose the
    /// command, so we never print a command that isn't installed (the same discipline <see cref="Add"/>
    /// enforces). The command carries the shared copy button wired by specscribe.js. [Story 2.1 Task 6]</summary>
    public static string InlineGuidance(string? command, string lead, string fallback)
    {
        if (command is null) return fallback;

        var cmd = PathUtil.Html(command);
        return $"{PathUtil.Html(lead)} <code class=\"inline-cmd\">{cmd}</code>" +
               $"<button type=\"button\" class=\"copy-btn\" data-copy=\"{cmd}\" aria-label=\"Copy command\">Copy</button>";
    }

    /// <summary>Appends a suggestion only when the module exposes that command — a missing step is dropped
    /// so we never render a command that isn't installed.</summary>
    private static void Add(List<Suggestion> list, string? command, string description)
    {
        if (command is not null)
        {
            list.Add(new Suggestion(command, description));
        }
    }

    private static List<Suggestion> ForStory(StoryInfo story, CommandCatalog commands)
    {
        var status = story.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        var suggestions = new List<Suggestion>();

        if (status.Contains("ready"))
        {
            Add(suggestions, commands.Command("dev-story", story.Id),
                "Implements the story exactly as specified — tasks, acceptance criteria, and dev notes drive the work.");
            Add(suggestions, commands.Command("code-review"),
                "Adversarial multi-layer review of the changes once implementation lands.");
            return suggestions;
        }

        if (status.Contains("progress") || status.Contains("in-dev"))
        {
            Add(suggestions, commands.Command("dev-story", story.Id),
                "Resumes implementation from the unchecked tasks in the story plan.");
            Add(suggestions, commands.Command("code-review"),
                "Review the work so far — worth running before marking the story done.");
            return suggestions;
        }

        if (status.Contains("done") || status.Contains("complete") || status.Contains("review"))
        {
            Add(suggestions, commands.Command("code-review"),
                "Final adversarial pass over the story's changes.");
            Add(suggestions, commands.Command("create-story"),
                "Drafts the next story file with full context for implementation.");
            Add(suggestions, commands.Command("retrospective", story.EpicNumber.ToString()),
                "Post-epic retrospective once the epic's last story closes.");
            return suggestions;
        }

        // No recognizable status — the plan doesn't exist yet. Before the epic's first story starts, it's
        // worth confirming the plan artifacts still agree — cheaper to catch a misalignment here than
        // mid-implementation.
        if (story.Id.EndsWith(".1", StringComparison.Ordinal))
        {
            Add(suggestions, commands.Command("check-implementation-readiness"),
                "Verifies the requirements, UX, architecture, and epics stay aligned before this epic's first story begins.");
        }

        Add(suggestions, commands.Command("create-story", story.Id),
            "Generates the dedicated story file with full implementation context.");

        return suggestions;
    }

    private static List<Suggestion> ForEpic(EpicInfo epic, CommandCatalog commands)
    {
        var epicClass = StatusStyles.ForEpic(epic);
        var suggestions = new List<Suggestion>();

        if (epicClass == "pending")
        {
            Add(suggestions, commands.Command("create-epics-and-stories"),
                $"Drafts the story breakdown for Epic {epic.Number} from the plan and architecture — it doesn't have one yet.");
            return suggestions;
        }

        if (epicClass == "done")
        {
            Add(suggestions, commands.Command("retrospective", epic.Number.ToString()),
                "Runs a retrospective now that every story in this epic is done.");
            return suggestions;
        }

        if (epicClass == "active")
        {
            Add(suggestions, commands.Command("sprint-status"),
                "Surfaces this epic's current risks and the recommended next action — see the in-development story's own page for its specific dev command.");

            // Even mid-epic, the next story without a plan is worth drafting so it's ready when the
            // current front line closes.
            var nextToDetail = epic.Stories.FirstOrDefault(s => s.ArtifactOutputPath is null);
            if (nextToDetail is not null)
            {
                Add(suggestions, commands.Command("create-story", nextToDetail.Id),
                    "Drafts the next story in this epic that doesn't have an implementation plan yet.");
            }

            return suggestions;
        }

        // "drafted" — stories are listed but none has a detailed implementation plan yet.
        Add(suggestions, commands.Command("sprint-planning"),
            "Refreshes sprint tracking from the current epics/stories — the prerequisite create-story expects.");

        var nextUndetailed = epic.Stories.FirstOrDefault(s => s.ArtifactOutputPath is null);
        if (nextUndetailed is not null)
        {
            Add(suggestions, commands.Command("create-story", nextUndetailed.Id),
                "Generates the implementation plan for the next story in this epic.");
        }

        return suggestions;
    }

    /// <summary>The project-level moves that make sense right now, in the order a developer would reason
    /// through them: review any story that's sitting in code review, build the story that's the current
    /// front line, draft the next story that still lacks a plan, and break down the next epic that hasn't
    /// been sharded into stories. These are stacked (not mutually exclusive) so the home page shows the whole
    /// "what's next for the project" picture at once. Falls back to a project-wide retrospective only once
    /// every epic is drafted and every story detailed.</summary>
    private static List<Suggestion> ForProject(EpicsModel model, CommandCatalog commands)
    {
        var suggestions = new List<Suggestion>();
        var allStories = model.Epics.SelectMany(e => e.Stories).ToList();

        // Stories sitting in code review are the most immediate move. A single review story passes its id
        // straight to the command (`/bmad-code-review 1.4`) so it's one copy-paste away; multiple stories are
        // grouped into one action row that lists their ids in the description (a single invocation can't
        // meaningfully take them all). Dropped silently when the module exposes no code-review command.
        var awaitingReview = allStories.Where(s => StatusStyles.ForStory(s) == "review").Select(s => s.Id).ToList();
        if (awaitingReview.Count == 1)
        {
            Add(suggestions, commands.Command("code-review", awaitingReview[0]),
                $"Story {awaitingReview[0]} is awaiting code review — adversarial multi-layer review of its changes.");
        }
        else if (awaitingReview.Count > 1)
        {
            Add(suggestions, commands.Command("code-review"),
                $"Stories {string.Join(", ", awaitingReview)} are awaiting code review — adversarial multi-layer review of their changes.");
        }

        // The current front line — a story that's ready or already in development. Referenced by its short
        // id (e.g. "1.1"), which the dev-story command resolves to the plan itself.
        var actionable = allStories.FirstOrDefault(s => StatusStyles.ForStory(s) is "ready" or "active");
        if (actionable is not null)
        {
            Add(suggestions, commands.Command("dev-story", actionable.Id),
                $"Story {actionable.Id} is the current front line — implement it per its plan.");
        }

        // The next story that still needs an implementation plan drawn up, in a drafted epic.
        var epicNeedingStory = model.Epics.FirstOrDefault(e =>
            e.Status == EpicStatus.Drafted && e.Stories.Any(s => s.ArtifactOutputPath is null));
        if (epicNeedingStory is not null)
        {
            var nextStory = epicNeedingStory.Stories.First(s => s.ArtifactOutputPath is null);
            Add(suggestions, commands.Command("create-story", nextStory.Id),
                $"Drafts the implementation plan for Epic {epicNeedingStory.Number}'s next story ({nextStory.Id}) — it doesn't have one yet.");
        }

        // The next epic still waiting to be broken down into stories.
        var pendingEpic = model.Epics.FirstOrDefault(e => e.Status == EpicStatus.Pending);
        if (pendingEpic is not null)
        {
            Add(suggestions, commands.Command("create-epics-and-stories"),
                $"Breaks Epic {pendingEpic.Number} down into stories — the next epic still awaiting a story breakdown.");
        }

        if (suggestions.Count == 0)
        {
            Add(suggestions, commands.Command("retrospective"),
                "Every epic is drafted and every story detailed — a good point for a project-wide retrospective.");
        }

        return suggestions;
    }
}
