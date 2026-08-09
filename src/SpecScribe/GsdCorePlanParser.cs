using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Extracts GSD Core's XML-authored plan tasks without assigning completion the source never records.</summary>
public static class GsdCorePlanParser
{
    private static readonly Regex PlanFileName = TimedRegex.New(
        @"^\d+(?:\.\d+)?-\d+-PLAN\.md$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ObjectiveBlock = TimedRegex.New(
        @"<objective\b[^>]*>(?<body>.*?)</objective\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TaskBlock = TimedRegex.New(
        @"<task\b[^>]*>(?<body>.*?)</task\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex NameBlock = TimedRegex.New(
        @"<name\b[^>]*>(?<name>.*?)</name\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MarkdownHeading = TimedRegex.New(
        @"^#{1,2}\s+(?<title>.+?)\s*$", RegexOptions.Compiled);

    private static readonly Regex XmlTag = TimedRegex.New(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly string[] SummaryHeadings = ["Tasks Completed", "What Was Built", "Verification Results"];

    /// <summary>GSD-specific detail projected from a plan and its optional same-stem completion summary.</summary>
    public sealed record Detail(
        string? Objective,
        IReadOnlyList<TaskItem> Tasks,
        IReadOnlyList<SummarySection> SummarySections);

    /// <summary>One selected summary section, preserved as authored Markdown until the shared renderer projects it.</summary>
    public sealed record SummarySection(string Title, string Markdown);

    /// <summary>Reads GSD's plan/detail shape when the artifact is a GSD plan; other artifact names return null.</summary>
    public static Detail? ReadDetail(string planFullPath, string rawPlanMarkdown)
    {
        var planName = Path.GetFileName(planFullPath);
        if (!PlanFileName.IsMatch(planName)) return null;

        var objective = ObjectiveBlock.Match(rawPlanMarkdown);
        var summaryPath = Path.Combine(
            Path.GetDirectoryName(planFullPath) ?? string.Empty,
            planName.Replace("-PLAN.md", "-SUMMARY.md", StringComparison.OrdinalIgnoreCase));
        return new Detail(
            objective.Success ? CleanText(objective.Groups["body"].Value) : ReadMarkdownSection(rawPlanMarkdown, "Goal"),
            ParseTasks(rawPlanMarkdown),
            ReadSummarySections(summaryPath));
    }

    /// <summary>Returns one unmarked item per well-formed task block, preferring its <c>&lt;name&gt;</c> text.</summary>
    public static IReadOnlyList<TaskItem> ParseTasks(string rawPlanMarkdown)
    {
        if (string.IsNullOrWhiteSpace(rawPlanMarkdown)) return Array.Empty<TaskItem>();

        var tasks = new List<TaskItem>();
        var taskSection = ReadMarkdownSection(rawPlanMarkdown, "Tasks");
        if (taskSection is null) return Array.Empty<TaskItem>();

        foreach (Match taskMatch in TaskBlock.Matches(taskSection))
        {
            var body = taskMatch.Groups["body"].Value;
            var name = NameBlock.Match(body);
            var text = name.Success
                ? CleanText(name.Groups["name"].Value)
                : ReadDirectTaskText(body);
            if (text.Length == 0) continue;

            tasks.Add(new TaskItem(text, Done: false, Array.Empty<TaskItem>(), TaskState.Unmarked));
        }
        return tasks;
    }

    private static IReadOnlyList<SummarySection> ReadSummarySections(string summaryPath)
    {
        if (!File.Exists(summaryPath)) return Array.Empty<SummarySection>();

        try
        {
            var lines = MarkdownConverter.ReadAllTextShared(summaryPath).Replace("\r\n", "\n").Split('\n');
            var sections = new List<SummarySection>();
            foreach (var heading in SummaryHeadings)
            {
                var start = Array.FindIndex(lines, line => string.Equals(line.TrimEnd(), "## " + heading, StringComparison.OrdinalIgnoreCase));
                if (start < 0) continue;

                var end = lines.Length;
                for (var index = start + 1; index < lines.Length; index++)
                {
                    if (lines[index].StartsWith("## ", StringComparison.Ordinal))
                    {
                        end = index;
                        break;
                    }
                }

                var markdown = string.Join("\n", lines[(start + 1)..end]).Trim();
                if (markdown.Length > 0) sections.Add(new SummarySection(heading, markdown));
            }
            return sections;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<SummarySection>();
        }
    }

    private static string? ReadMarkdownSection(string markdown, string headingTitle)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, line =>
        {
            var match = MarkdownHeading.Match(line);
            return match.Success && string.Equals(match.Groups["title"].Value.Trim(), headingTitle, StringComparison.OrdinalIgnoreCase);
        });
        if (start < 0) return null;

        var end = lines.Length;
        for (var index = start + 1; index < lines.Length; index++)
        {
            if (MarkdownHeading.IsMatch(lines[index]))
            {
                end = index;
                break;
            }
        }

        return string.Join("\n", lines[(start + 1)..end]);
    }

    private static string ReadDirectTaskText(string taskBody)
    {
        var depth = 0;
        foreach (var line in taskBody.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            var tags = XmlTag.Matches(trimmed);
            if (tags.Count > 0)
            {
                foreach (Match tag in tags)
                {
                    if (!tag.Value.StartsWith("</", StringComparison.Ordinal) && !tag.Value.EndsWith("/>", StringComparison.Ordinal)) depth++;
                    if (tag.Value.StartsWith("</", StringComparison.Ordinal)) depth = Math.Max(0, depth - 1);
                }
                continue;
            }

            if (depth == 0) return CleanText(trimmed);
        }

        return string.Empty;
    }

    private static string CleanText(string value)
    {
        var text = XmlTag.Replace(value, " ");
        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Select(line => Regex.Replace(line, @"\s+", " ").Trim())
            .Where(line => line.Length > 0)
            .ToList();
        return string.Join(" ", lines);
    }
}