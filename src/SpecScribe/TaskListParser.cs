using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>A single checkbox line from a story's "## Tasks / Subtasks" list.</summary>
public sealed record TaskItem(string Text, bool Done, IReadOnlyList<TaskItem> Subtasks);

/// <summary>Parses the "## Tasks / Subtasks" checklist out of an implementation artifact into a two-level
/// tree (top-level tasks + their subtasks) for the per-story task sunburst. Plain, non-checkbox bullets
/// (dev notes tucked under a task) are skipped — only real `- [ ]`/`- [x]` lines count.</summary>
public static class TaskListParser
{
    private static readonly Regex ChecklistLine = new(
        @"^(?<indent>\s*)-\s*\[(?<mark>[ xX])\]\s*(?<text>.+)$",
        RegexOptions.Compiled);

    public static IReadOnlyList<TaskItem> Parse(string rawArtifactMarkdown)
    {
        var lines = rawArtifactMarkdown.Replace("\r\n", "\n").Split('\n');

        var headingIdx = Array.FindIndex(lines, l => l.TrimStart().StartsWith("## Tasks", StringComparison.OrdinalIgnoreCase));
        if (headingIdx < 0) return Array.Empty<TaskItem>();

        var end = lines.Length;
        for (var i = headingIdx + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal)) { end = i; break; }
        }

        var tasks = new List<TaskItem>();
        string? topText = null;
        var topDone = false;
        var subtasks = new List<TaskItem>();

        void FlushTop()
        {
            if (topText is not null)
            {
                tasks.Add(new TaskItem(topText, topDone, subtasks.ToList()));
                subtasks.Clear();
            }
        }

        for (var i = headingIdx + 1; i < end; i++)
        {
            var m = ChecklistLine.Match(lines[i]);
            if (!m.Success) continue;

            var indent = m.Groups["indent"].Value.Length;
            var done = m.Groups["mark"].Value is "x" or "X";
            var text = CleanTaskText(m.Groups["text"].Value);

            if (indent == 0)
            {
                FlushTop();
                topText = text;
                topDone = done;
            }
            else
            {
                subtasks.Add(new TaskItem(text, done, Array.Empty<TaskItem>()));
            }
        }
        FlushTop();

        return tasks;
    }

    /// <summary>Strips bold markers and trailing "(AC: ...)" annotations for a compact tooltip label.</summary>
    private static string CleanTaskText(string text)
    {
        var t = text.Replace("**", string.Empty).Trim();
        var acIdx = t.IndexOf(" (AC:", StringComparison.OrdinalIgnoreCase);
        if (acIdx > 0) t = t[..acIdx].Trim();
        return t;
    }
}
