namespace SpecScribe;

/// <summary>Tallies epic/story/task progress from a parsed <see cref="EpicsModel"/> and its resolved
/// implementation artifacts. As a side effect, fills in each <see cref="StoryInfo"/>'s TasksDone/TasksTotal
/// so per-story task bars need no extra plumbing downstream.</summary>
public static class ProgressCalculator
{
    public static ProgressModel Compute(EpicsModel epics, IReadOnlyDictionary<string, string> artifactMap, GitPulse? git)
    {
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

                    var (done, total, status) = ReadArtifactProgress(artifactFullPath);
                    story.TasksDone = done;
                    story.TasksTotal = total;
                    story.Status = status;
                    epicTasksDone += done;
                    epicTasksTotal += total;
                }
            }

            tasksDone += epicTasksDone;
            tasksTotal += epicTasksTotal;

            perEpic.Add(new EpicProgress
            {
                Number = epic.Number,
                Title = epic.Title,
                StoryCount = epic.Stories.Count,
                StoriesWithArtifact = epicStoriesWithArtifact,
                TasksDone = epicTasksDone,
                TasksTotal = epicTasksTotal,
                Status = epic.Status,
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
        };
    }

    private static (int Done, int Total, string? Status) ReadArtifactProgress(string artifactFullPath)
    {
        try
        {
            var raw = MarkdownConverter.ReadAllTextShared(artifactFullPath);
            int done = 0, total = 0;

            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("- [x]", StringComparison.OrdinalIgnoreCase))
                {
                    done++;
                    total++;
                }
                else if (trimmed.StartsWith("- [ ]", StringComparison.Ordinal))
                {
                    total++;
                }
            }

            return (done, total, EpicsParser.ExtractStatus(raw));
        }
        catch (IOException)
        {
            return (0, 0, null);
        }
    }
}
