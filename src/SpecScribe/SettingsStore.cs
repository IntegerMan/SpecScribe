using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpecScribe;

/// <summary>The persisted shape of <c>.specscribe</c>: the same optional path/name choices a user makes
/// via "Configure paths", stored verbatim (relative strings stay relative) so they survive between runs.</summary>
public sealed class SavedSettings
{
    public string? Source { get; set; }
    public string? Adrs { get; set; }
    public string? Output { get; set; }
    public string? ProjectName { get; set; }

    /// <summary>Persisted opt-in for deep git analytics (<c>--deep-git</c>). Nullable for tri-state: null means
    /// "never configured" (distinct from an explicit false), so a fresh <c>.specscribe</c> stays empty. Only a
    /// <c>true</c> is ever written — the flag defaults off, so there is nothing to persist for the off case. [Story 3.2]</summary>
    public bool? DeepGit { get; set; }

    /// <summary>Persisted code-link base URL (<c>--code-url</c>). Null means "never configured" — citations render
    /// as in-portal code pages. A non-null value switches to external links against this base. [Story 7.1]</summary>
    public string? CodeUrl { get; set; }

    /// <summary>True when nothing was configured — an all-null file is not worth writing or logging.</summary>
    [JsonIgnore]
    public bool IsEmpty => Source is null && Adrs is null && Output is null && ProjectName is null && DeepGit is null && CodeUrl is null;
}

/// <summary>Reads and writes the optional <c>.specscribe</c> settings file in the current directory. Persistence
/// is best-effort: a missing or malformed file is treated as "no saved settings" rather than an error, since the
/// interactive menu can always rediscover or re-enter paths.</summary>
public static class SettingsStore
{
    public const string FileName = ".specscribe";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Absolute path to the settings file for the current working directory.</summary>
    public static string ResolvePath() => Path.Combine(Directory.GetCurrentDirectory(), FileName);

    /// <summary>Loads saved settings, or returns null when the file is absent, empty, or unreadable/malformed.</summary>
    public static SavedSettings? TryLoad()
    {
        var path = ResolvePath();
        try
        {
            if (!File.Exists(path)) return null;

            var json = MarkdownConverter.ReadAllTextShared(path);
            var saved = JsonSerializer.Deserialize<SavedSettings>(json, SerializerOptions);
            return saved is null or { IsEmpty: true } ? null : saved;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes the configured path/name choices to <c>.specscribe</c>. Returns the file path on success,
    /// or null when there was nothing worth saving or the write failed.</summary>
    public static string? TrySave(SiteSettings settings)
    {
        var saved = new SavedSettings
        {
            Source = settings.Source,
            Adrs = settings.Adrs,
            Output = settings.Output,
            ProjectName = settings.ProjectName,
            // Persist the opt-in only when it is on; false is the default, so leave it null (nothing to save)
            // and keep an otherwise-empty config from being written just because the flag is off. [Story 3.2]
            DeepGit = settings.DeepGit ? true : null,
            CodeUrl = settings.CodeUrl,
        };

        if (saved.IsEmpty) return null;

        var path = ResolvePath();
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(saved, SerializerOptions));
            return path;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Copies saved values onto the live settings, but only where the user didn't already pass a value
    /// on the command line — explicit CLI options always win over the persisted file.</summary>
    public static void ApplyTo(SavedSettings saved, SiteSettings settings)
    {
        settings.Source ??= saved.Source;
        settings.Adrs ??= saved.Adrs;
        settings.Output ??= saved.Output;
        settings.ProjectName ??= saved.ProjectName;
        // The CLI bool defaults false and there is no --no-deep-git, so settings.DeepGit == false unambiguously
        // means "not requested on this run" — safe to restore a persisted true; an explicit --deep-git stays on.
        if (!settings.DeepGit && saved.DeepGit == true) settings.DeepGit = true;
        // CLI wins over saved: only fill from the persisted value when no --code-url was passed this run.
        settings.CodeUrl ??= saved.CodeUrl;
    }
}
