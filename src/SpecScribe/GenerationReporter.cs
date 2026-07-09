using Spectre.Console;

namespace SpecScribe;

public enum GenerationPhase { Scan, Epics, Pages, Adrs, CommitDays, GitInsights, Index }

/// <summary>Receives phase-by-phase progress from <see cref="SiteGenerator.GenerateAll"/> so the
/// console layer can show a live breakdown without the generator knowing about Spectre.</summary>
public interface IGenerationReporter
{
    /// <param name="itemCount">Number of items the phase will process, or 0 when unknown (rendered as indeterminate).</param>
    void BeginPhase(GenerationPhase phase, int itemCount = 0);
    void Tick(GenerationPhase phase);
    void EndPhase(GenerationPhase phase);
}

/// <summary>Maps generation phases onto Spectre.Console progress tasks.</summary>
public sealed class SpectreGenerationReporter : IGenerationReporter
{
    private static readonly IReadOnlyDictionary<GenerationPhase, string> Descriptions = new Dictionary<GenerationPhase, string>
    {
        [GenerationPhase.Scan] = "Scanning source artifacts",
        [GenerationPhase.Epics] = "Parsing epics & stories",
        [GenerationPhase.Pages] = "Rendering pages",
        [GenerationPhase.Adrs] = "Rendering ADRs",
        [GenerationPhase.CommitDays] = "Rendering commit-day pages",
        [GenerationPhase.GitInsights] = "Rendering git insights hub",
        [GenerationPhase.Index] = "Writing dashboard & index",
    };

    private readonly ProgressContext _ctx;
    private readonly Dictionary<GenerationPhase, ProgressTask> _tasks = new();

    public SpectreGenerationReporter(ProgressContext ctx)
    {
        _ctx = ctx;
    }

    public void BeginPhase(GenerationPhase phase, int itemCount = 0)
    {
        var task = _ctx.AddTask(Descriptions[phase], maxValue: Math.Max(itemCount, 1));
        task.IsIndeterminate = itemCount == 0;
        _tasks[phase] = task;
    }

    public void Tick(GenerationPhase phase)
    {
        if (_tasks.TryGetValue(phase, out var task))
        {
            task.Increment(1);
        }
    }

    public void EndPhase(GenerationPhase phase)
    {
        if (_tasks.TryGetValue(phase, out var task))
        {
            task.IsIndeterminate = false;
            task.Value = task.MaxValue;
            task.StopTask();
        }
    }
}
