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
    /// <param name="DisplayLabel">When set, the command badge shows this label while copying <see cref="Command"/>.</param>
    /// <param name="Kicker">Optional non-primary kicker override (e.g. "Close" for follow-up close-out).</param>
    /// <param name="Accent">Optional left-rail accent override (<c>review</c>/<c>active</c>/…).</param>
    private sealed record Suggestion(
        string Command,
        string Description,
        string? DisplayLabel = null,
        string? Kicker = null,
        string? Accent = null);

    /// <summary>A destination the command's "send elsewhere" menu can target. Cursor is the one IDE with a
    /// public "open with the prompt pre-filled" deeplink (it never auto-runs), so it gets a real link. Every
    /// other destination — Copilot, Claude Code, Codex — has no such URL today and rides the primary Copy
    /// button instead (paste the command in yourself). Append here to add a destination: give it a template
    /// with a <c>{cmd}</c> placeholder that receives the URL-encoded command.
    /// <para>EXPANSION POINT: SpecScribe is planned to also ship as a VS Code extension. Once hosted
    /// in-extension, a Copilot target can post the command straight into Copilot Chat (extension command API /
    /// trusted-webview <c>command:</c> URI) rather than copy — add it to <see cref="SendTargets"/> then.</para></summary>
    private sealed record SendTarget(string Label, string UriTemplate);

    private static readonly IReadOnlyList<SendTarget> SendTargets = new[]
    {
        new SendTarget("Open in Cursor", "cursor://anysphere.cursor-deeplink/prompt?text={cmd}"),
    };

    public static string RenderNextSteps(
        StoryInfo story, CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred = null)
    {
        if (StatusStyles.ForStory(story) == "done")
        {
            var open = OpenOnly(openDeferred);
            return open.Count > 0 && commands.Command("quick-dev") is { Length: > 0 }
                ? RenderDoneWithDeferredPanel(story.Id, "Story", open, commands)
                : RenderAllDonePanel(commands);
        }

        return RenderPanel(ForStory(story, commands, openDeferred));
    }

    /// <summary>The FULL status-gated next-step command list for a story — the exact set the story page's
    /// "Next Steps" panel renders (<see cref="RenderNextSteps"/>), projected as data for a non-HTML host
    /// (the VS Code outline's "Copy BMad Command…" Quick Pick shows the LITERAL command each entry copies),
    /// in the page's order — first surviving entry is the primary recommended command (dev-story when
    /// ready/active, code-review when in review, create-story when undrafted; an undrafted first-of-epic
    /// (X.1) may also carry check-implementation-readiness as an alternate). For a done story the list is
    /// empty, or a single muted <c>correct-course</c> escape hatch when the module exposes it (mirrors the
    /// celebratory panel's Other actions — never a primary; see <see cref="PrimaryStoryCommand"/>). Empty
    /// when the detected module exposes none — the host then omits the action entirely, so the gating
    /// (e.g. no code-review before work is reviewable) lives here, never in TypeScript (AD-2).
    /// Whitespace-only catalog values are dropped so the emitted list never carries a blank command.
    /// [spec-vscode-sidebar-shortcuts-and-story-command-quickpick; Story 8.5]</summary>
    public static IReadOnlyList<OutlineStoryCommand> StoryCommands(
        StoryInfo story, CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred = null)
    {
        if (StatusStyles.ForStory(story) == "done")
        {
            var cmds = new List<OutlineStoryCommand>();
            var open = OpenOnly(openDeferred);
            if (open.Count > 0)
            {
                var addr = BuildAddressDeferredSuggestion(story.Id, "Story", open, commands);
                if (addr is not null)
                    cmds.Add(new OutlineStoryCommand(addr.Command, addr.Description));
            }

            var hatch = DoneEscapeHatch(commands);
            if (hatch is not null)
                cmds.Add(new OutlineStoryCommand(hatch.Command, hatch.Description));

            return cmds;
        }

        return ForStory(story, commands, openDeferred)
            .Where(s => !string.IsNullOrWhiteSpace(s.Command))
            .Select(s => new OutlineStoryCommand(s.Command, s.Description))
            .ToList();
    }

    /// <summary>The single most-actionable slash command for a story — the FIRST entry of
    /// <see cref="StoryCommands"/> for non-done statuses, kept as one string for payload back-compat
    /// (an older shim reads only this) and as the single-command convenience for tests/hosts.
    /// Returns null when the story is done with no Address-deferred primary (celebratory terminal state —
    /// a muted <c>correct-course</c> hatch in <see cref="StoryCommands"/> is never a primary), or when
    /// the detected module exposes no matching command. When done with open deferred and quick-dev is
    /// installed, returns the Address-deferred command. Composed here in C# so the shim authors no
    /// command (AD-2). [Story 6.9; Story 8.5; spec-address-deferred-next-steps]</summary>
    public static string? PrimaryStoryCommand(
        StoryInfo story, CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred = null)
    {
        if (StatusStyles.ForStory(story) == "done")
        {
            var open = OpenOnly(openDeferred);
            if (open.Count == 0) return null;
            // Address deferred is a primary when present; correct-course hatch never is.
            return BuildAddressDeferredSuggestion(story.Id, "Story", open, commands)?.Command;
        }

        return StoryCommands(story, commands, openDeferred).FirstOrDefault()?.Command;
    }

    public static string RenderEpicNextSteps(
        EpicInfo epic, CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred = null)
    {
        var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
        if (epicClass == "done")
        {
            var open = OpenOnly(openDeferred);
            if (open.Count > 0 && commands.Command("quick-dev") is { Length: > 0 })
                return RenderDoneWithDeferredPanel($"{epic.Number}", "Epic", open, commands);
        }

        return RenderPanel(ForEpic(epic, commands, openDeferred));
    }

    public static string RenderProjectNextSteps(EpicsModel model, CommandCatalog commands) =>
        RenderPanel(ForProject(model, commands));

    /// <summary>Project Next Steps panel <em>body</em> only (heading + cards) — Home wraps this with
    /// work-mode panel classes so stage filters never depend on mutating a fixed class string. [Story 9.8]</summary>
    public static string RenderProjectNextStepsBody(EpicsModel model, CommandCatalog commands) =>
        RenderInner(ForProject(model, commands));

    /// <summary>Next Steps panel for an action-item detail page — same card grammar as story pages
    /// (Recommended primary + room for alternates). Long resolve prompts use a labeled badge so the
    /// user sees what they're copying without dumping the full payload into the card. [Story 9.11]</summary>
    public static string RenderActionItemNextSteps(SprintActionItem item, CommandCatalog commands)
    {
        if (FollowUpGeometry.IsDone(item))
            return RenderFollowUpSettledPanel("This action item is marked done.");

        return RenderPanel(ForActionItem(item, commands));
    }

    /// <summary>Next Steps panel for a deferred-work detail page. Resolved items get a settled panel;
    /// open items offer quick-dev (and create-story when exposed). [Story 9.11]</summary>
    public static string RenderDeferredItemNextSteps(DeferredWorkItem item, CommandCatalog commands)
    {
        if (item.Resolved)
            return RenderFollowUpSettledPanel("This deferred item is already resolved.");

        return RenderPanel(ForDeferredItem(item, commands));
    }

    /// <summary>The heading + list on their own (no chart-panel wrapper), for callers that want to fold
    /// the Next Steps into a shared panel rather than give it a standalone one (epic page Up Next).
    /// Done epics with open deferred get the same done-with-deferred body as
    /// <see cref="RenderEpicNextSteps"/> — without a nested chart-panel. [spec-address-deferred-next-steps]</summary>
    public static string RenderEpicNextStepsInner(
        EpicInfo epic, CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred = null)
    {
        var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
        if (epicClass == "done")
        {
            var open = OpenOnly(openDeferred);
            if (open.Count > 0 && commands.Command("quick-dev") is { Length: > 0 })
                return RenderDoneWithDeferredBody($"{epic.Number}", "Epic", open, commands, includeHeading: true);
        }

        return RenderInner(ForEpic(epic, commands, openDeferred));
    }

    /// <summary>The story actions pane for a DONE story: celebratory terminal state (checkmark + success
    /// styling) instead of a primary command or code-review nudge. When the module exposes
    /// <c>correct-course</c>, a single muted "Other actions" escape hatch is appended for the rare re-open
    /// case; otherwise the panel stays purely celebratory. Reuses the shared done glyph
    /// (<see cref="Icons.ForStatus"/>). [spec-sunburst-retro; Story 8.5]</summary>
    private static string RenderAllDonePanel(CommandCatalog commands)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"chart-panel next-steps all-done\">\n<h3>Next Steps</h3>\n");
        sb.Append($"<p class=\"all-done-complete\"><span class=\"all-done-icon\">{Icons.ForStatus("done")}</span>All done — this story is complete.</p>\n");

        var hatch = DoneEscapeHatch(commands);
        if (hatch is not null)
        {
            sb.Append(RenderAlternatesGroup([hatch]));
        }

        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    /// <summary>Muted done-panel / outline escape hatch when the module exposes a non-blank
    /// <c>correct-course</c>; null otherwise (whitespace-only catalog values drop like
    /// <see cref="StoryCommands"/>). [Story 8.5]</summary>
    private static Suggestion? DoneEscapeHatch(CommandCatalog commands)
    {
        var escape = commands.Command("correct-course");
        return string.IsNullOrWhiteSpace(escape)
            ? null
            : new Suggestion(escape, "Re-open this story if it needs rework.");
    }

    private static string RenderPanel(List<Suggestion> suggestions)
    {
        var inner = RenderInner(suggestions);
        return inner.Length == 0
            ? string.Empty
            : $"<div class=\"chart-panel next-steps\">\n{inner}</div>\n\n";
    }

    /// <summary>Shared panel body: up to three horizontal command cards (first emphasized as primary); any
    /// further survivors render under a labeled "Other actions" group. Primacy is decided here at render
    /// time — not at <see cref="Add"/> — so a null-dropped intended primary correctly promotes the next
    /// survivor. [Story 8.5; Story 9.8 card polish]</summary>
    private static string RenderInner(List<Suggestion> suggestions)
    {
        if (suggestions.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<h3>Next Steps</h3>\n<div class=\"next-steps-cards\">\n");

        var cardCount = Math.Min(suggestions.Count, 3);
        for (var i = 0; i < cardCount; i++)
        {
            var s = suggestions[i];
            var isPrimary = i == 0;
            var accent = s.Accent ?? AccentForCommand(s.Command);
            var kicker = isPrimary ? "Recommended" : (s.Kicker ?? KickerForCommand(s.Command, isPrimary: false));
            var cardClass = isPrimary ? "next-step-card next-step-card-primary" : "next-step-card";
            sb.Append($"  <div class=\"{cardClass} {accent}\">\n");
            sb.Append($"    <span class=\"next-step-kicker\">{PathUtil.Html(kicker)}</span>\n");
            var badge = s.DisplayLabel is { Length: > 0 } label
                ? RenderLabeledCommand(label, s.Command)
                : RenderCommandBadge(s.Command);
            sb.Append($"    <div class=\"next-step-command\">{badge}</div>\n");
            sb.Append($"    <p class=\"next-step-desc\">{PathUtil.Html(s.Description)}</p>\n");
            sb.Append("  </div>\n");
        }

        sb.Append("</div>\n");

        if (suggestions.Count > 3)
        {
            sb.Append(RenderAlternatesGroup(suggestions.Skip(3)));
        }

        return sb.ToString();
    }

    /// <summary>Status accent for a next-step card's left rail — derived from the command slug, not color-only.
    /// Unknown slugs fail closed to <c>pending</c> (not <c>ready</c>). [Story 9.8 polish]</summary>
    internal static string AccentForCommand(string command)
    {
        var slug = CommandSlug(command);
        if (slug.Contains("code-review", StringComparison.Ordinal) || slug.Contains("retrospective", StringComparison.Ordinal))
            return "review";
        if (slug.Contains("dev-story", StringComparison.Ordinal) || slug.Contains("sprint-status", StringComparison.Ordinal)
            || slug.Contains("correct-course", StringComparison.Ordinal)
            || slug.Contains("quick-dev", StringComparison.Ordinal))
            return "active";
        if (slug.Contains("create-story", StringComparison.Ordinal))
            return "drafted";
        if (slug.Contains("create-epics", StringComparison.Ordinal) || slug.Contains("sprint-planning", StringComparison.Ordinal))
            return "ready";
        if (slug.Contains("check-implementation", StringComparison.Ordinal))
            return "pending";
        return "pending";
    }

    /// <summary>Non-primary card kicker from the command slug. Unknown slugs fail closed to
    /// <c>Also consider</c>. Primary cards always use <c>Recommended</c> at the call site. [Story 9.8 polish]</summary>
    internal static string KickerForCommand(string command, bool isPrimary)
    {
        if (isPrimary) return "Recommended";
        var slug = CommandSlug(command);
        if (slug.Contains("code-review", StringComparison.Ordinal) || slug.Contains("retrospective", StringComparison.Ordinal))
            return "Review";
        if (slug.Contains("dev-story", StringComparison.Ordinal)) return "Develop";
        if (slug.Contains("quick-dev", StringComparison.Ordinal)) return "Implement";
        if (slug.Contains("create-story", StringComparison.Ordinal)) return "Draft";
        if (slug.Contains("sprint-status", StringComparison.Ordinal) || slug.Contains("sprint-planning", StringComparison.Ordinal))
            return "Plan";
        if (slug.Contains("create-epics", StringComparison.Ordinal)) return "Break down";
        if (slug.Contains("correct-course", StringComparison.Ordinal)) return "Recover";
        if (slug.Contains("check-implementation", StringComparison.Ordinal)) return "Validate";
        return "Also consider";
    }

    private static string CommandSlug(string command) => command.Split(' ')[0];

    private static string RenderAlternatesGroup(IEnumerable<Suggestion> alternates)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"next-steps-alternates\">\n");
        sb.Append("<p class=\"next-steps-alternates-label\">Other actions</p>\n");
        sb.Append("<ul class=\"next-steps-list next-steps-overflow\">\n");
        foreach (var s in alternates)
        {
            var badge = s.DisplayLabel is { Length: > 0 } label
                ? RenderLabeledCommand(label, s.Command)
                : RenderCommandBadge(s.Command);
            sb.Append("  <li class=\"next-steps-alt\">" +
                      badge +
                      $"<span class=\"next-steps-desc\">{PathUtil.Html(s.Description)}</span></li>\n");
        }
        sb.Append("</ul>\n</div>\n");
        return sb.ToString();
    }

    /// <summary>Renders a caller-supplied set of (command, description) pairs as a native <c>&lt;details&gt;</c>
    /// dropdown popout triggered from a header button — the same standard command badge (command text + Copy +
    /// send menu) each Next Steps panel uses, but with a short description beside each, tucked behind a summary
    /// so it costs no layout height until opened. Null commands (the module doesn't expose them) are dropped;
    /// the whole menu is omitted when none resolve. Reuses the <c>.send-menu</c> dropdown pattern and its
    /// specscribe.js click-away/Escape dismissal (which also targets <c>.cmd-menu</c>). Degrades to a native
    /// disclosure with JS off. [Story 2.3 redesign]</summary>
    public static string RenderCommandMenu(string label, IEnumerable<(string? Command, string Description)> items)
    {
        var resolved = items.Where(i => !string.IsNullOrWhiteSpace(i.Command)).ToList();
        if (resolved.Count == 0) return string.Empty;

        var lbl = PathUtil.Html(label);
        var sb = new StringBuilder();
        sb.Append("<details class=\"cmd-menu\">");
        sb.Append($"<summary class=\"cmd-menu-toggle\" aria-label=\"{lbl}\">{lbl} ▾</summary>");
        sb.Append($"<div class=\"cmd-menu-pop\" role=\"group\" aria-label=\"{lbl}\"><ul class=\"cmd-menu-list\">");
        foreach (var (command, description) in resolved)
        {
            sb.Append("<li>" + RenderCommandBadge(command!) + $"<span class=\"cmd-menu-desc\">{PathUtil.Html(description)}</span></li>");
        }
        sb.Append("</ul></div></details>");
        return sb.ToString();
    }

    /// <summary>A command badge whose VISIBLE text is a short human label (e.g. "Resolve with AI") while its
    /// copy/deeplink payload is the full composed command — for long prompts that would be ugly shown verbatim.
    /// Reuses the same copy button + Cursor send-menu as <see cref="RenderCommandBadge"/>. Returns empty when
    /// the command is null/blank (the module doesn't expose it). [Story 2.3 retro action items]</summary>
    public static string RenderLabeledCommand(string label, string? rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand)) return string.Empty;

        var cmd = PathUtil.Html(rawCommand);
        var lbl = PathUtil.Html(label);
        var sb = new StringBuilder();
        sb.Append("<span class=\"cmd-badge\">");
        sb.Append($"<button type=\"button\" class=\"cmd-copy\" data-copy=\"{cmd}\" data-tooltip=\"Copy command\" aria-label=\"{lbl} — copy command\">");
        sb.Append($"<span class=\"cmd-text\">{lbl}</span>{CopyIconSvg}</button>");
        sb.Append("<details class=\"send-menu\">");
        sb.Append("<summary class=\"send-toggle\" data-tooltip=\"More options\" aria-label=\"Other ways to send this command\">▾</summary>");
        sb.Append($"<div class=\"send-menu-list\" role=\"group\" aria-label=\"Send {lbl} to an editor\">");
        sb.Append($"<button type=\"button\" class=\"send-item\" data-copy=\"{cmd}\" aria-label=\"Copy command\">Copy</button>");
        if (SendTargets.Count > 0)
        {
            var encoded = Uri.EscapeDataString(rawCommand!);
            foreach (var target in SendTargets)
            {
                var href = PathUtil.Html(target.UriTemplate.Replace("{cmd}", encoded));
                sb.Append($"<a class=\"send-item\" href=\"{href}\">{PathUtil.Html(target.Label)}</a>");
            }
        }
        sb.Append("</div></details>");
        sb.Append("</span>");
        return sb.ToString();
    }

    /// <summary>A dependency-free "copy" glyph (two overlapping pages) that inherits its stroke from the
    /// button's text color via <c>currentColor</c>. Marked aria-hidden because the button carries the label.</summary>
    private const string CopyIconSvg =
        "<svg class=\"icon\" viewBox=\"0 0 16 16\" width=\"13\" height=\"13\" aria-hidden=\"true\" focusable=\"false\">" +
        "<rect x=\"2\" y=\"5\" width=\"9\" height=\"9\" rx=\"1.5\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.3\"/>" +
        "<path d=\"M5 5V3.5A1.5 1.5 0 0 1 6.5 2H13A1.5 1.5 0 0 1 14.5 3.5V10A1.5 1.5 0 0 1 13 11.5H11\" " +
        "fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.3\"/></svg>";

    /// <summary>The unified command badge for one suggestion: the slash-command text, then (past a vertical
    /// separator) an icon-only Copy button and a caret that opens a native <c>&lt;details&gt;</c> menu. The
    /// menu leads with a labelled "Copy command" row, then the per-destination deep links from
    /// <see cref="SendTargets"/>. Copy triggers are any element carrying <c>data-copy</c> (wired by
    /// specscribe.js, which also adds click-away / Escape dismissal); the deep links are plain anchors and the
    /// toggle is native, so it degrades to selectable command text + native disclosure with JS off. The command
    /// is URL-encoded into each link's <c>href</c> and then HTML-escaped, matching the <c>data-copy</c> attribute.</summary>
    private static string RenderCommandBadge(string rawCommand)
    {
        var cmd = PathUtil.Html(rawCommand);
        var sb = new StringBuilder();
        sb.Append("<span class=\"cmd-badge\">");
        // The command text + copy icon are one click-to-copy button (no separator between them); clicking
        // anywhere on the command copies it. The rich on-brand tooltip (data-tooltip) names the action; the
        // aria-label serves screen readers. The <code> stays selectable as the no-JS fallback.
        sb.Append($"<button type=\"button\" class=\"cmd-copy\" data-copy=\"{cmd}\" data-tooltip=\"Copy command\" aria-label=\"Copy command\">");
        sb.Append($"<code class=\"cmd-text\">{cmd}</code>{CopyIconSvg}</button>");

        // The menu leads with a plain "Copy" row (a second, labelled way to copy), then any per-destination
        // deep links. Rendered even if SendTargets is empty, so the caret is always useful. Menu rows are
        // text-only (no icons) — there's no fitting glyph for the deep-link targets, so none carry one.
        sb.Append("<details class=\"send-menu\">");
        sb.Append("<summary class=\"send-toggle\" data-tooltip=\"More options\" aria-label=\"Other ways to send this command\">▾</summary>");
        sb.Append($"<div class=\"send-menu-list\" role=\"group\" aria-label=\"Send {cmd} to an editor\">");
        sb.Append($"<button type=\"button\" class=\"send-item\" data-copy=\"{cmd}\" aria-label=\"Copy command\">Copy</button>");

        if (SendTargets.Count > 0)
        {
            var encoded = Uri.EscapeDataString(rawCommand);
            foreach (var target in SendTargets)
            {
                var href = PathUtil.Html(target.UriTemplate.Replace("{cmd}", encoded));
                sb.Append($"<a class=\"send-item\" href=\"{href}\">{PathUtil.Html(target.Label)}</a>");
            }
        }

        sb.Append("</div></details>");
        sb.Append("</span>");
        return sb.ToString();
    }

    /// <summary>A short lead-in followed by the same command badge the "Next Steps" panels use, turning a
    /// dead-end note ("Stories not yet drafted.") into a signposted action — so every "draft it with X" prompt
    /// (undrafted story cards, pending epics, empty states) renders through one consistent renderer. Returns
    /// <paramref name="fallback"/> unchanged when the module doesn't expose the command, so we never print a
    /// command that isn't installed (the same discipline <see cref="Add"/> enforces). [Story 2.1 Task 6]</summary>
    public static string InlineGuidance(string? command, string lead, string fallback)
    {
        if (command is null) return fallback;

        return $"{PathUtil.Html(lead)} {RenderCommandBadge(command)}";
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

    /// <summary>A story page only suggests actions on *this* story — drafting other stories and
    /// retrospectives are epic/project-level moves that belong on those pages (<see cref="ForEpic"/>,
    /// <see cref="ForProject"/>). The one exception is `create-story` with the story's own id when no plan
    /// exists yet: that drafts the story being viewed, not a different one. Suggestions are built in
    /// priority order (most-recommended first); <see cref="RenderInner"/> treats index 0 as the primary
    /// and demotes the rest under "Other actions". Where a code-review suggestion is offered, it carries
    /// the story id — but a `ready` story has no changes yet, so it gets no code-review at all. Mid-sprint
    /// / review recovery uses a demoted <c>correct-course</c> alternate when the module exposes it.
    /// [Story 8.5]</summary>
    private static List<Suggestion> ForStory(
        StoryInfo story, CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred = null)
    {
        var status = story.Status?.Trim().ToLowerInvariant() ?? string.Empty;
        var suggestions = new List<Suggestion>();

        if (status.Contains("ready"))
        {
            Add(suggestions, commands.Command("dev-story", story.Id),
                "Implements the story exactly as specified — tasks, acceptance criteria, and dev notes drive the work.");
            AppendDeferredAlternate(suggestions, story.Id, "Story", openDeferred, commands);
            return suggestions;
        }

        if (status.Contains("progress") || status.Contains("in-dev"))
        {
            Add(suggestions, commands.Command("dev-story", story.Id),
                "Resumes implementation from the unchecked tasks in the story plan.");
            Add(suggestions, commands.Command("code-review", story.Id),
                "Review the work so far — worth running before marking the story done.");
            Add(suggestions, commands.Command("correct-course"),
                "Re-plan this story mid-sprint if scope shifted or something's blocking.");
            AppendDeferredAlternate(suggestions, story.Id, "Story", openDeferred, commands);
            return suggestions;
        }

        if (status.Contains("review"))
        {
            Add(suggestions, commands.Command("code-review", story.Id),
                "Final adversarial pass over the story's changes.");
            Add(suggestions, commands.Command("correct-course"),
                "If review surfaces a scope problem, re-plan before re-review.");
            AppendDeferredAlternate(suggestions, story.Id, "Story", openDeferred, commands);
            return suggestions;
        }

        if (status.Contains("done") || status.Contains("complete"))
        {
            // Done stories are handled by RenderNextSteps (all-done or done-with-deferred panel),
            // not this list — keep empty so callers never treat a hatch as a ForStory primary.
            return suggestions;
        }

        Add(suggestions, commands.Command("create-story", story.Id),
            "Generates the dedicated story file with full implementation context.");

        if (story.Id.EndsWith(".1", StringComparison.Ordinal))
        {
            Add(suggestions, commands.Command("check-implementation-readiness"),
                "Verifies the requirements, UX, architecture, and epics stay aligned before this epic's first story begins.");
        }

        AppendDeferredAlternate(suggestions, story.Id, "Story", openDeferred, commands);
        return suggestions;
    }

    private static List<Suggestion> ForEpic(
        EpicInfo epic, CommandCatalog commands,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred = null)
    {
        var epicClass = StatusStyles.ForEpicWithRetrospective(epic);
        var suggestions = new List<Suggestion>();
        var entityId = epic.Number.ToString();

        if (epicClass == "pending")
        {
            Add(suggestions, commands.Command("create-epics-and-stories"),
                $"Drafts the story breakdown for Epic {epic.Number} from the plan and architecture — it doesn't have one yet.");
            AppendDeferredAlternate(suggestions, entityId, "Epic", openDeferred, commands);
            return suggestions;
        }

        if (epicClass == "review")
        {
            Add(suggestions, commands.Command("retrospective", epic.Number.ToString()),
                "Runs a retrospective now that every story in this epic is done.");
            AppendDeferredAlternate(suggestions, entityId, "Epic", openDeferred, commands);
            return suggestions;
        }

        if (epicClass == "done")
        {
            return suggestions;
        }

        if (epicClass == "active")
        {
            // Even mid-epic, the next story without a plan is worth drafting so it's ready when the
            // current front line closes. When Up Next would spotlight that undrafted story (no active /
            // review / ready front line — only done work + undrafted remaining), promote create-story
            // to primary so the epic page doesn't bury the coherent next draft under sprint-status.
            // [Story 8.5; Story 9.8]
            var nextToDetail = epic.Stories.FirstOrDefault(s => s.ArtifactOutputPath is null);
            var hasFrontLine = epic.Stories.Any(s => StatusStyles.ForStory(s) is "active" or "review" or "ready");

            if (!hasFrontLine && nextToDetail is not null)
            {
                Add(suggestions, commands.Command("create-story", nextToDetail.Id),
                    "Drafts the next story in this epic that doesn't have an implementation plan yet.");
                Add(suggestions, commands.Command("sprint-status"),
                    "Surfaces this epic's current risks and the recommended next action.");
                AppendDeferredAlternate(suggestions, entityId, "Epic", openDeferred, commands);
                return suggestions;
            }

            Add(suggestions, commands.Command("sprint-status"),
                "Surfaces this epic's current risks and the recommended next action — see the in-development story's own page for its specific dev command.");

            if (nextToDetail is not null)
            {
                Add(suggestions, commands.Command("create-story", nextToDetail.Id),
                    "Drafts the next story in this epic that doesn't have an implementation plan yet.");
            }

            AppendDeferredAlternate(suggestions, entityId, "Epic", openDeferred, commands);
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

        AppendDeferredAlternate(suggestions, entityId, "Epic", openDeferred, commands);
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

        // The next story that still needs an implementation plan — in any drafted/ready/active epic
        // (same undrafted scan ForEpic uses mid-flight). EpicStatus.Drafted covers all non-pending
        // epics with stories; StatusStyles narrows so Home and epic pages recommend the same next draft.
        // [Story 9.8]
        var epicNeedingStory = model.Epics.FirstOrDefault(e =>
        {
            var cls = StatusStyles.ForEpicWithRetrospective(e);
            return (cls is "drafted" or "ready" or "active")
                && e.Stories.Any(s => s.ArtifactOutputPath is null);
        });
        if (epicNeedingStory is not null)
        {
            var nextStory = epicNeedingStory.Stories.First(s => s.ArtifactOutputPath is null);
            Add(suggestions, commands.Command("create-story", nextStory.Id),
                // "Story N.M" (not a bare "(N.M)") so StoryEpicLinkifier links it to the story's page.
                $"Drafts the implementation plan for Story {nextStory.Id} — Epic {epicNeedingStory.Number}'s next story that doesn't have one yet.");
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

    private static List<Suggestion> ForActionItem(SprintActionItem item, CommandCatalog commands)
    {
        var suggestions = new List<Suggestion>();
        var quickDev = commands.Command("quick-dev");
        if (quickDev is not { Length: > 0 }) return suggestions;

        var epicNote = item.EpicNumber is { } e ? $" (Epic {e})" : string.Empty;
        // RAW action text in payloads — never linkified (copy-payload corruption trap).
        suggestions.Add(new Suggestion(
            $"{quickDev} Resolve this retrospective action item{epicNote}: {item.Action}",
            "Copies a quick-dev prompt with this action item's full text so AI can implement the fix.",
            DisplayLabel: "Resolve with AI"));

        // Close-out: mark the sprint-status action item done — not draft/plan workflows that
        // don't carry enough context to solve or settle this specific follow-up. [Story 9.11]
        suggestions.Add(new Suggestion(
            $"{quickDev} Close this retrospective action item{epicNote} in sprint-status.yaml (set its status to done). Only close it if the work is already complete or you just finished it: {item.Action}",
            "Copies a prompt that asks AI to mark this action item done in sprint-status.yaml once the work is settled.",
            DisplayLabel: "Close with AI",
            Kicker: "Close",
            Accent: "review"));

        return suggestions;
    }

    private static List<Suggestion> ForDeferredItem(DeferredWorkItem item, CommandCatalog commands)
    {
        var suggestions = new List<Suggestion>();
        var quickDev = commands.Command("quick-dev");
        if (quickDev is not { Length: > 0 }) return suggestions;

        var lead = FollowUpRow.SummarizeFromHtml(item.BodyHtml, maxChars: 200);
        var body = string.IsNullOrWhiteSpace(lead)
            ? PathUtil.StripHtmlTags(item.BodyHtml).Trim()
            : lead;

        suggestions.Add(new Suggestion(
            $"{quickDev} Address this deferred-work item: {body}",
            "Copies a quick-dev prompt with this deferred item's text so AI can pick up the parked work.",
            DisplayLabel: "Address with AI"));

        // Close-out: mark the deferred-work note item resolved — not draft/plan workflows that
        // don't produce a usable fix for this specific parked item. [Story 9.11]
        suggestions.Add(new Suggestion(
            $"{quickDev} Close this deferred-work item in deferred-work.md (mark it RESOLVED, keep the audit trail). Only close it if the work is already complete or you just finished it: {body}",
            "Copies a prompt that asks AI to mark this item RESOLVED in deferred-work.md once the work is settled.",
            DisplayLabel: "Close with AI",
            Kicker: "Close",
            Accent: "review"));

        return suggestions;
    }

    private static string RenderFollowUpSettledPanel(string message)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"chart-panel next-steps all-done\">\n<h3>Next Steps</h3>\n");
        sb.Append($"<p class=\"all-done-complete\"><span class=\"all-done-icon\">{Icons.ForStatus("done")}</span>{PathUtil.Html(message)}</p>\n");
        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    // ---- Address deferred helpers -------------------------------------------------------------------

    private static IReadOnlyList<FollowUpDeferredSlot> OpenOnly(IReadOnlyList<FollowUpDeferredSlot>? slots) =>
        slots is null ? Array.Empty<FollowUpDeferredSlot>()
            : slots.Where(s => !s.Item.Resolved).ToList();

    private static Suggestion? BuildAddressDeferredSuggestion(
        string entityId, string entityKind,
        IReadOnlyList<FollowUpDeferredSlot> openSlots, CommandCatalog commands)
    {
        var quickDev = commands.Command("quick-dev");
        if (quickDev is not { Length: > 0 } || openSlots.Count == 0) return null;

        var promptSb = new StringBuilder();
        promptSb.Append($"{quickDev} Address open deferred work for {entityKind} {entityId} ({openSlots.Count} item{(openSlots.Count == 1 ? "" : "s")}). Find writeups in deferred-work.md and follow-up detail pages:");
        for (var i = 0; i < openSlots.Count; i++)
        {
            var slot = openSlots[i];
            var summary = FollowUpRow.SummarizeFromHtml(slot.Item.BodyHtml ?? string.Empty, maxChars: 200);
            if (string.IsNullOrWhiteSpace(summary))
                summary = PathUtil.StripHtmlTags(slot.Item.BodyHtml ?? string.Empty).Trim();
            var provenance = slot.SourceKey is { Length: > 0 } sk ? $" [{sk}]" : string.Empty;
            var cue = slot.DetailHref is { Length: > 0 } dh ? $" → {dh}" : string.Empty;
            promptSb.Append($"\n{i + 1}. {summary}{provenance}{cue}");
        }

        return new Suggestion(
            promptSb.ToString(),
            $"Copies a quick-dev prompt listing the {openSlots.Count} open deferred item{(openSlots.Count == 1 ? "" : "s")} so AI can address them.",
            DisplayLabel: "Address deferred");
    }

    private static void AppendDeferredAlternate(
        List<Suggestion> suggestions, string entityId, string entityKind,
        IReadOnlyList<FollowUpDeferredSlot>? openDeferred, CommandCatalog commands)
    {
        var open = OpenOnly(openDeferred);
        var addr = BuildAddressDeferredSuggestion(entityId, entityKind, open, commands);
        if (addr is not null)
            suggestions.Add(addr);
    }

    private static string RenderDoneWithDeferredPanel(
        string entityId, string entityKind,
        IReadOnlyList<FollowUpDeferredSlot> openSlots, CommandCatalog commands)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"chart-panel next-steps\">\n");
        sb.Append(RenderDoneWithDeferredBody(entityId, entityKind, openSlots, commands, includeHeading: true));
        sb.Append("</div>\n\n");
        return sb.ToString();
    }

    /// <summary>Done-with-deferred status line + Address deferred primary card (and story hatch when
    /// exposed). Shared by the standalone Next Steps panel and the epic Up Next fold-in.</summary>
    private static string RenderDoneWithDeferredBody(
        string entityId, string entityKind,
        IReadOnlyList<FollowUpDeferredSlot> openSlots, CommandCatalog commands,
        bool includeHeading)
    {
        var sb = new StringBuilder();
        if (includeHeading)
            sb.Append("<h3>Next Steps</h3>\n");
        sb.Append($"<p class=\"done-deferred-status\">{entityKind} {PathUtil.Html(entityId)} is complete — {openSlots.Count} open deferred item{(openSlots.Count == 1 ? "" : "s")} remain{(openSlots.Count == 1 ? "s" : "")}.</p>\n");

        var addr = BuildAddressDeferredSuggestion(entityId, entityKind, openSlots, commands);
        if (addr is not null)
        {
            sb.Append("<div class=\"next-steps-cards\">\n");
            var accent = addr.Accent ?? AccentForCommand(addr.Command);
            var badge = addr.DisplayLabel is { Length: > 0 } label
                ? RenderLabeledCommand(label, addr.Command)
                : RenderCommandBadge(addr.Command);
            sb.Append($"  <div class=\"next-step-card next-step-card-primary {accent}\">\n");
            sb.Append($"    <span class=\"next-step-kicker\">Recommended</span>\n");
            sb.Append($"    <div class=\"next-step-command\">{badge}</div>\n");
            sb.Append($"    <p class=\"next-step-desc\">{PathUtil.Html(addr.Description)}</p>\n");
            sb.Append("  </div>\n");
            sb.Append("</div>\n");
        }

        var hatch = entityKind == "Story" ? DoneEscapeHatch(commands) : null;
        if (hatch is not null)
            sb.Append(RenderAlternatesGroup([hatch]));

        return sb.ToString();
    }

    // ---- List-batch pane (deferred-work / action-items / follow-up group pages) --------------------

    /// <summary>One open item projected for a list-batch Address/Close prompt — plain (un-linkified) text
    /// so the numbered cue line in the copyable prompt never carries HTML. <see cref="ProvenanceKey"/> is the
    /// bracketed cue (deferred <c>SourceKey</c>/heading label, or an "Epic N"/"Unattributed" chip for action
    /// items); <see cref="DetailHref"/> the per-item detail deep link, when one exists.
    /// [spec-follow-up-list-batch-actions]</summary>
    public sealed record ListBatchEntry(string Summary, string? ProvenanceKey, string? DetailHref);

    /// <summary>Renders the horizontal Next Steps–style batch pane for a follow-up LIST page (whole-site
    /// deferred backlog, whole-site open action-items, or a generated follow-up group page): three cards —
    /// Address all, Address first 5 (omitted per kind under 6 open), Close all — each holding a
    /// Deferred | Action items button pair (or a single button when only one kind is present on the page).
    /// Gated on <c>quick-dev</c> plus at least one open item across both kinds (NFR8); <c>Kind == "direct"</c>
    /// members never reach this renderer — callers filter them out before building the entry lists.
    /// [spec-follow-up-list-batch-actions]</summary>
    public static string RenderListBatchPane(
        string pageTitle, CommandCatalog commands,
        IReadOnlyList<ListBatchEntry>? openDeferred = null,
        IReadOnlyList<ListBatchEntry>? openActions = null)
    {
        var quickDev = commands.Command("quick-dev");
        var deferred = openDeferred ?? Array.Empty<ListBatchEntry>();
        var actions = openActions ?? Array.Empty<ListBatchEntry>();
        if (quickDev is not { Length: > 0 } || (deferred.Count == 0 && actions.Count == 0))
            return string.Empty;

        var cards = new StringBuilder();
        var cardCount = 0;

        var addressAll = RenderListBatchCard("Address all", "active",
            ("Deferred", BuildDeferredAddressBatch(pageTitle, deferred, quickDev)),
            ("Action items", BuildActionAddressBatch(pageTitle, actions, quickDev)));
        if (addressAll.Length > 0) { cards.Append(addressAll); cardCount++; }

        if (deferred.Count >= 6 || actions.Count >= 6)
        {
            var addressFirst5 = RenderListBatchCard("Address first 5", "active",
                ("Deferred", deferred.Count >= 6
                    ? BuildDeferredAddressBatch(pageTitle, deferred.Take(5).ToList(), quickDev, deferred.Count, isFirstN: true)
                    : null),
                ("Action items", actions.Count >= 6
                    ? BuildActionAddressBatch(pageTitle, actions.Take(5).ToList(), quickDev, actions.Count, isFirstN: true)
                    : null));
            if (addressFirst5.Length > 0) { cards.Append(addressFirst5); cardCount++; }
        }

        var closeAll = RenderListBatchCard("Close all", "review",
            ("Deferred", BuildDeferredCloseBatch(pageTitle, deferred, quickDev)),
            ("Action items", BuildActionCloseBatch(pageTitle, actions, quickDev)));
        if (closeAll.Length > 0) { cards.Append(closeAll); cardCount++; }

        if (cardCount == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<div class=\"chart-panel next-steps list-batch-actions\">\n<h3>Next Steps</h3>\n<div class=\"next-steps-cards\">\n");
        sb.Append(cards);
        sb.Append("</div>\n</div>\n\n");
        return sb.ToString();
    }

    /// <summary>One list-batch card: up to two labeled command buttons (<c>Deferred</c> / <c>Action items</c>)
    /// side by side when both kinds resolve, a single button when only one does, or empty (card omitted
    /// entirely) when neither resolves.</summary>
    private static string RenderListBatchCard(
        string kicker, string accent,
        (string Label, Suggestion? Suggestion) deferredButton,
        (string Label, Suggestion? Suggestion) actionButton)
    {
        var active = new List<(string Label, Suggestion Suggestion)>();
        if (deferredButton.Suggestion is not null) active.Add((deferredButton.Label, deferredButton.Suggestion));
        if (actionButton.Suggestion is not null) active.Add((actionButton.Label, actionButton.Suggestion));
        if (active.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append($"  <div class=\"next-step-card {accent}\">\n");
        sb.Append($"    <span class=\"next-step-kicker\">{PathUtil.Html(kicker)}</span>\n");

        if (active.Count == 1)
        {
            var (label, s) = active[0];
            sb.Append($"    <div class=\"next-step-command\">{RenderLabeledCommand(label, s.Command)}</div>\n");
            sb.Append($"    <p class=\"next-step-desc\">{PathUtil.Html(s.Description)}</p>\n");
        }
        else
        {
            sb.Append("    <div class=\"next-step-command-group\">\n");
            foreach (var (label, s) in active)
            {
                sb.Append("      <div class=\"next-step-command-item\">\n");
                sb.Append($"        <div class=\"next-step-command\">{RenderLabeledCommand(label, s.Command)}</div>\n");
                sb.Append($"        <p class=\"next-step-desc\">{PathUtil.Html(s.Description)}</p>\n");
                sb.Append("      </div>\n");
            }
            sb.Append("    </div>\n");
        }

        sb.Append("  </div>\n");
        return sb.ToString();
    }

    private static void AppendNumberedList(StringBuilder sb, IReadOnlyList<ListBatchEntry> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var provenance = item.ProvenanceKey is { Length: > 0 } pk ? $" [{pk}]" : string.Empty;
            var cue = item.DetailHref is { Length: > 0 } dh ? $" \u2192 {dh}" : string.Empty;
            sb.Append($"\n{i + 1}. {item.Summary}{provenance}{cue}");
        }
    }

    /// <summary>Parenthetical count for the prompt lead line — e.g. <c>3 items</c> or
    /// <c>first 5 of 12 open items</c>.</summary>
    private static string ScopeNote(int shown, int? totalCount, bool isFirstN) =>
        isFirstN
            ? $"first {shown} of {totalCount ?? shown} open items"
            : $"{shown} item{(shown == 1 ? "" : "s")}";

    /// <summary>Human description under the card button — avoids doubling "of open …" when
    /// <see cref="ScopeNote"/> already says "open items".</summary>
    private static string BatchCardDescription(string verbPhrase, string pageTitle, int shown, int? totalCount, bool isFirstN)
    {
        var countPhrase = isFirstN
            ? $"the first {shown} of {totalCount ?? shown} open"
            : $"{shown} open";
        return $"Copies a quick-dev prompt listing {countPhrase} {verbPhrase} on {pageTitle}.";
    }

    private static Suggestion? BuildDeferredAddressBatch(
        string pageTitle, IReadOnlyList<ListBatchEntry> items, string quickDev,
        int? totalCount = null, bool isFirstN = false)
    {
        if (items.Count == 0) return null;
        var scope = ScopeNote(items.Count, totalCount, isFirstN);

        var promptSb = new StringBuilder();
        promptSb.Append($"{quickDev} Address open deferred work on {pageTitle} ({scope}). Find writeups in deferred-work.md and follow-up detail pages:");
        AppendNumberedList(promptSb, items);

        return new Suggestion(
            promptSb.ToString(),
            BatchCardDescription("deferred items so AI can address them", pageTitle, items.Count, totalCount, isFirstN));
    }

    private static Suggestion? BuildDeferredCloseBatch(string pageTitle, IReadOnlyList<ListBatchEntry> items, string quickDev)
    {
        if (items.Count == 0) return null;
        var n = items.Count;
        var scope = $"{n} item{(n == 1 ? "" : "s")}";

        var promptSb = new StringBuilder();
        promptSb.Append($"{quickDev} Close open deferred work on {pageTitle} ({scope}) \u2014 mark each RESOLVED in deferred-work.md (keep the audit trail). Only close an item if the work is already complete or you just finished it. Find writeups in deferred-work.md and follow-up detail pages:");
        AppendNumberedList(promptSb, items);

        return new Suggestion(
            promptSb.ToString(),
            BatchCardDescription("deferred items so AI can mark them RESOLVED once settled", pageTitle, n, null, isFirstN: false));
    }

    private static Suggestion? BuildActionAddressBatch(
        string pageTitle, IReadOnlyList<ListBatchEntry> items, string quickDev,
        int? totalCount = null, bool isFirstN = false)
    {
        if (items.Count == 0) return null;
        var scope = ScopeNote(items.Count, totalCount, isFirstN);

        var promptSb = new StringBuilder();
        promptSb.Append($"{quickDev} Resolve open retrospective action items on {pageTitle} ({scope}). Find full text in sprint-status.yaml and follow-up detail pages:");
        AppendNumberedList(promptSb, items);

        return new Suggestion(
            promptSb.ToString(),
            BatchCardDescription("action items so AI can resolve them", pageTitle, items.Count, totalCount, isFirstN));
    }

    private static Suggestion? BuildActionCloseBatch(string pageTitle, IReadOnlyList<ListBatchEntry> items, string quickDev)
    {
        if (items.Count == 0) return null;
        var n = items.Count;
        var scope = $"{n} item{(n == 1 ? "" : "s")}";

        var promptSb = new StringBuilder();
        promptSb.Append($"{quickDev} Close open retrospective action items on {pageTitle} ({scope}) in sprint-status.yaml (set each to done). Only close an item if the work is already complete or you just finished it. Find full text in sprint-status.yaml and follow-up detail pages:");
        AppendNumberedList(promptSb, items);

        return new Suggestion(
            promptSb.ToString(),
            BatchCardDescription("action items so AI can mark them done once settled", pageTitle, n, null, isFirstN: false));
    }
}
