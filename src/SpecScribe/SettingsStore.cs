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

    /// <summary>Persisted README-inclusion preference — the interactive half of <c>--no-readme</c>, which before
    /// Story 5.2 could only be passed on the command line and so was the last interactive/CLI parity gap (NFR7).
    /// Tri-state like <see cref="DeepGit"/>: null means "never configured" and keeps the include-by-default
    /// behavior, so a <c>.specscribe</c> written by an earlier version (where the property simply does not exist)
    /// deserializes to null and loads unchanged. Only an explicit exclusion (<c>false</c>) is ever written — see
    /// <see cref="SettingsStore.Capture"/>. [Story 5.2 AC #4]</summary>
    public bool? IncludeReadme { get; set; }

    /// <summary>True when nothing was configured — an all-null file is not worth writing or logging.</summary>
    [JsonIgnore]
    public bool IsEmpty => Source is null && Adrs is null && Output is null && ProjectName is null
        && DeepGit is null && CodeUrl is null && IncludeReadme is null;
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

    /// <summary>The nearest existing <c>.specscribe</c> at or above <paramref name="startDirectory"/> (defaulting to
    /// the current working directory), or null when there is none. A git-style walk-up rather than raw-cwd anchoring:
    /// a run started in a subdirectory must still see the settings file at the repo root, otherwise "directory-scoped"
    /// silently means "cwd-scoped" and the same repo behaves differently depending on where you stood.
    /// <para>Deliberately independent of <see cref="ForgeOptions"/>'s <c>_bmad-output</c> walk-up: a saved
    /// <c>--source</c> can itself relocate the repo root, so anchoring the settings file at the discovered root would
    /// be circular. Never throws — an unreadable directory in the chain ends the walk. [Story 5.2 AC #1]</para></summary>
    public static string? FindExisting(string? startDirectory = null)
    {
        try
        {
            var dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, FileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Best-effort discovery: an unreadable or malformed path is "no saved settings", never a crash (NFR2).
        }

        return null;
    }

    /// <summary>Absolute path to the settings file governing <paramref name="startDirectory"/> (defaulting to the
    /// current working directory): the nearest existing file up-tree, or — when none exists yet — the path a first
    /// save would create in the start directory itself. Read and write therefore address the same file, so
    /// configuring from a subdirectory updates the settings that subdirectory actually reads.</summary>
    public static string ResolvePath(string? startDirectory = null)
        => FindExisting(startDirectory) ?? Path.Combine(startDirectory ?? Directory.GetCurrentDirectory(), FileName);

    /// <summary>Loads saved settings, or returns null when the file is absent, empty, or unreadable/malformed.</summary>
    public static SavedSettings? TryLoad(string? startDirectory = null)
    {
        var path = ResolvePath(startDirectory);
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

    /// <summary>Projects the live settings into the persisted shape, without writing anything. Split out of
    /// <see cref="TrySave"/> so a caller that has just saved can hold the exact <see cref="SavedSettings"/> now on
    /// disk (the interactive menu re-bases its provenance on it) without re-reading the file. [Story 5.2]</summary>
    public static SavedSettings Capture(SiteSettings settings) => new()
    {
        Source = settings.Source,
        Adrs = settings.Adrs,
        Output = settings.Output,
        ProjectName = settings.ProjectName,
        // Persist the opt-in only when it is on; false is the default, so leave it null (nothing to save)
        // and keep an otherwise-empty config from being written just because the flag is off. [Story 3.2]
        DeepGit = settings.DeepGit ? true : null,
        CodeUrl = settings.CodeUrl,
        // Same persist-only-the-non-default discipline, mirrored: the README is included by default, so only an
        // explicit exclusion is worth writing. "Include" therefore stays null (absent) rather than writing `true`
        // — which would make every save produce a non-empty file and defeat the IsEmpty guard. Reading still
        // honors an explicit `true` (a hand-edited file), so the tri-state is preserved on the load side.
        // [Story 5.2 AC #4]
        IncludeReadme = settings.NoReadme ? false : null,
    };

    /// <summary>Writes the configured path/name choices to <c>.specscribe</c>. Returns the file path on success,
    /// or null when there was nothing worth saving or the write failed. Targets the same file
    /// <see cref="TryLoad"/> would read (the nearest one up-tree, or a new one in the start directory), so a
    /// configure-then-run round trip is symmetric from any depth. [Story 5.2 AC #1]</summary>
    public static string? TrySave(SiteSettings settings, string? startDirectory = null)
    {
        var saved = Capture(settings);

        if (saved.IsEmpty) return null;

        var path = ResolvePath(startDirectory);
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
        // Same shape as DeepGit: the CLI bool defaults false and there is no --readme counter-flag, so
        // settings.NoReadme == false unambiguously means "not requested this run" and a persisted exclusion may be
        // restored; an explicit --no-readme stays on. A persisted `true` (include) needs no action — it agrees with
        // the default — but is still honored as an explicit source for provenance. [Story 5.2 AC #4]
        if (!settings.NoReadme && saved.IncludeReadme == false) settings.NoReadme = true;
    }
}
