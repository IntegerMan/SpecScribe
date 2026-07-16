namespace SpecScribe;

/// <summary>Tallies epic/story/task progress from a parsed <see cref="EpicsModel"/> and its resolved
/// implementation artifacts. As a side effect, fills in each <see cref="StoryInfo"/>'s TasksDone/TasksTotal/
/// Status/<see cref="StoryInfo.LastUpdatedDate"/> so per-story badges and recency markers need no extra
/// plumbing downstream.</summary>
public static class ProgressCalculator
{
    public static ProgressModel Compute(EpicsModel epics, IReadOnlyDictionary<string, string> artifactMap, GitPulse? git, DeepGitPulse? deep = null)
    {
        // Deep-git per-file dates from uncapped CodeMapMetrics, keyed by normalized repo-root-relative path.
        // Built once; unmatched story paths fall through to the change-log date. [Story 8.8]
        var gitFileDates = BuildGitFileDateMap(deep);

        var perEpic = new List<EpicProgress>();
        int storiesTotal = 0, storiesWithArtifact = 0, tasksDone = 0, tasksTotal = 0;

        foreach (var epic in epics.Epics)
        {
            int epicTasksDone = 0, epicTasksTotal = 0, epicStoriesWithArtifact = 0;

            foreach (var story in epic.Stories)
            {
                storiesTotal++;

                if (artifactMap.TryGetValue(story.Id, out var artifactFullPath))
                {
                    storiesWithArtifact++;
                    epicStoriesWithArtifact++;

                    var (done, total, status, changeLogDate) = ReadArtifactProgress(artifactFullPath);
                    story.TasksDone = done;
                    story.TasksTotal = total;
                    story.Status = status;
                    story.LastUpdatedDate = ResolveLastUpdated(story, gitFileDates, changeLogDate);
                    epicTasksDone += done;
                    epicTasksTotal += total;
                }
                else
                {
                    // Reused StoryInfo graphs (e.g. watch-mode) must not keep a stale marker. [Review][Patch]
                    story.LastUpdatedDate = null;
                }
            }

            tasksDone += epicTasksDone;
            tasksTotal += epicTasksTotal;

            // Per-status delivery tally for the mosaic (A6). Story.Status was filled above, so ForStory now
            // reflects each story's real lifecycle stage rather than "has an artifact or not."
            var statusCounts = epic.Stories
                .GroupBy(StatusStyles.ForStory)
                .ToDictionary(g => g.Key, g => g.Count());

            perEpic.Add(new EpicProgress
            {
                Number = epic.Number,
                Title = epic.Title,
                StoryCount = epic.Stories.Count,
                StoriesWithArtifact = epicStoriesWithArtifact,
                TasksDone = epicTasksDone,
                TasksTotal = epicTasksTotal,
                Status = epic.Status,
                StoryStatusCounts = statusCounts,
            });
        }

        return new ProgressModel
        {
            EpicsTotal = epics.Epics.Count,
            EpicsDrafted = epics.Epics.Count(e => e.Status == EpicStatus.Drafted),
            EpicsPending = epics.Epics.Count(e => e.Status == EpicStatus.Pending),
            StoriesTotal = storiesTotal,
            StoriesWithArtifact = storiesWithArtifact,
            TasksDone = tasksDone,
            TasksTotal = tasksTotal,
            PerEpic = perEpic,
            Git = git,
            DeepGit = deep,
        };
    }

    /// <summary>Git date at <c>SourceDirName/ArtifactSourcePath</c> wins; else the change-log date; else null.
    /// Never invents a date. [Story 8.8]</summary>
    private static DateOnly? ResolveLastUpdated(
        StoryInfo story,
        IReadOnlyDictionary<string, DateOnly> gitFileDates,
        DateOnly? changeLogDate)
    {
        if (story.ArtifactSourcePath is { Length: > 0 } sourceRel)
        {
            var key = PathUtil.NormalizeSlashes($"{ForgeOptions.SourceDirName}/{sourceRel}");
            if (gitFileDates.TryGetValue(key, out var gitDate)) return gitDate;
        }
        return changeLogDate;
    }

    /// <summary>Uncapped per-file last-change dates from <see cref="DeepGitPulse.CodeMapMetrics"/> —
    /// not the top-N <see cref="GitInsightsData.Files"/> hub list, which would miss quietly-edited story
    /// markdown. [Story 8.8][Review][Patch]</summary>
    private static Dictionary<string, DateOnly> BuildGitFileDateMap(DeepGitPulse? deep)
    {
        var map = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
        var metrics = deep?.CodeMapMetrics;
        if (metrics is null || metrics.Count == 0) return map;

        foreach (var (path, file) in metrics)
        {
            if (file.LastDate is not { } date) continue;
            map.TryAdd(PathUtil.NormalizeSlashes(path), date);
        }
        return map;
    }

    private static (int Done, int Total, string? Status, DateOnly? ChangeLogDate) ReadArtifactProgress(string artifactFullPath)
    {
        try
        {
            var raw = MarkdownConverter.ReadAllTextShared(artifactFullPath);

            // Top-level tasks only — subtasks are counted separately by the per-story TaskSunburst.
            // Flattening both into one tally previously reported the combined checkbox count as "tasks"
            // everywhere else (home page, epic page, sunburst outer ring), which reads as the subtask
            // count on any story with a handful of tasks and many subtasks.
            var tasks = TaskListParser.Parse(raw);
            var done = tasks.Count(t => t.Done);
            var total = tasks.Count;

            return (done, total, EpicsParser.ExtractStatus(raw), EpicsParser.ExtractLatestChangeLogDate(raw));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (0, 0, null, null);
        }
    }
}
