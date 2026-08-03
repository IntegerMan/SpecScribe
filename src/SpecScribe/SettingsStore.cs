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

    /// <summary>Persisted date-page "today" cutoff (<c>--today-policy</c>, or <c>--as-of</c> as the composite
    /// <c>as-of:{iso}</c> token). Tri-state like <see cref="DeepGit"/>: null means "never configured" and keeps the
    /// machine-local default, so a <c>.specscribe</c> written by an earlier version (where the property does not
    /// exist) deserializes to null and loads unchanged. Only a NON-default cutoff is ever written — see
    /// <see cref="SettingsStore.Capture"/>. Kept as ONE field carrying one composite token, deliberately: the fixed
    /// date is part of the cutoff, not a second setting beside it. [Story 5.5, retyped in Story 5.7]</summary>
    public DateCutoff? TodayPolicy { get; set; }

    /// <summary>True when nothing was configured — an all-null file is not worth writing or logging.</summary>
    [JsonIgnore]
    public bool IsEmpty => Source is null && Adrs is null && Output is null && ProjectName is null
        && DeepGit is null && CodeUrl is null && IncludeReadme is null && TodayPolicy is null;
}

/// <summary>Reads and writes the optional <c>.specscribe</c> settings folder in the current directory. Persistence
/// is best-effort: a missing or malformed settings file is treated as "no saved settings" rather than an error,
/// since the interactive menu can always rediscover or re-enter paths.
/// <para><c>.specscribe</c> is a FOLDER containing <see cref="ConfigFileName"/>, not a flat file — a container
/// rather than a single document leaves room for future per-directory state (e.g. incremental-build caches, run
/// history) to live alongside the config without a second dotfile or a breaking format change. [ADR 0014]
/// A pre-existing flat-file <c>.specscribe</c> (written by any version before this) is still read directly — see
/// <see cref="ReadConfigJson"/> — and is transparently migrated to the folder form the next time settings are
/// saved from it; see <see cref="TrySave"/>.</para></summary>
public static class SettingsStore
{
    /// <summary>The settings folder name — same name a pre-migration flat file used, so an existing entry in
    /// <c>.gitignore</c> (which matches a file OR a folder of the same name) needs no change.</summary>
    public const string FileName = ".specscribe";

    /// <summary>The config document inside the <see cref="FileName"/> folder.</summary>
    public const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Persist the cutoff as a TOKEN ("utc", "as-of:2026-07-27"), not an ordinal — a `.specscribe` is a
        // user-editable file, and a bare number would be opaque there and would silently re-map if the enum were
        // ever reordered. The only non-primitive in SavedSettings is TodayPolicy; strings/bools are unaffected.
        // [Story 5.5]
        Converters = { new DateCutoffJsonConverter() },
    };

    /// <summary>Reads <see cref="DateCutoff"/> via the same forgiving vocabulary as <c>--today-policy</c>
    /// (<see cref="DatePolicies.TryParse"/>) rather than requiring an exact enum-member-name match, and degrades an
    /// unrecognized/malformed token to "field not set" instead of throwing. <c>.specscribe</c> is a hand-editable
    /// file (see <see cref="SerializerOptions"/>'s doc), and a plain <see cref="JsonStringEnumConverter"/> fails the
    /// WHOLE document — discarding Source/Output/every other saved field too — the moment this one field holds a
    /// value it doesn't recognize verbatim, e.g. the CLI's own accepted spelling <c>"last-commit"</c> instead of the
    /// enum's <c>"LastCommit"</c>. One field's typo must not cost every other saved setting (NFR8). [Review][Patch]
    /// <para>Story 5.7 EXTENDS this converter rather than retyping the field to <c>string?</c> and dropping it: an
    /// unvalidated string would flow through <see cref="ApplyTo"/> into
    /// <see cref="SiteSettings.ResolveDateCutoff"/>'s THROW, converting today's silent-and-safe degrade into a new
    /// hard failure — the opposite of what the requirement asks for. <see cref="Write"/> now emits
    /// <see cref="DatePolicies.Token"/> instead of <c>ToString()</c> (which on a record would emit
    /// <c>DateCutoff { Policy = AsOf, … }</c>), making read and write symmetric on one vocabulary for the first
    /// time; a pre-5.7 file holding <c>"Utc"</c>/<c>"LastCommit"</c> still loads, since
    /// <see cref="DatePolicies.TryParse"/> accepts those spellings case-insensitively.</para></summary>
    private sealed class DateCutoffJsonConverter : JsonConverter<DateCutoff?>
    {
        public override DateCutoff? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            return DatePolicies.TryParse(reader.GetString(), out var cutoff) ? cutoff : null;
        }

        public override void Write(Utf8JsonWriter writer, DateCutoff? value, JsonSerializerOptions options)
        {
            if (value is { } cutoff) writer.WriteStringValue(DatePolicies.Token(cutoff));
            else writer.WriteNullValue();
        }
    }

    /// <summary>The nearest existing <c>.specscribe</c> at or above <paramref name="startDirectory"/> (defaulting to
    /// the current working directory), or null when there is none. A git-style walk-up rather than raw-cwd anchoring:
    /// a run started in a subdirectory must still see the settings folder at the repo root, otherwise "directory-scoped"
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
                // Either form counts as "found": the current folder format, or a not-yet-migrated flat file.
                if (Directory.Exists(candidate) || File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Best-effort discovery: an unreadable or malformed path is "no saved settings", never a crash (NFR2).
        }

        return null;
    }

    /// <summary>Absolute path to the settings folder governing <paramref name="startDirectory"/> (defaulting to the
    /// current working directory): the nearest existing <c>.specscribe</c> up-tree, or — when none exists yet — the
    /// path a first save would create in the start directory itself. Read and write therefore address the same
    /// location, so configuring from a subdirectory updates the settings that subdirectory actually reads.</summary>
    public static string ResolvePath(string? startDirectory = null)
        => FindExisting(startDirectory) ?? Path.Combine(startDirectory ?? Directory.GetCurrentDirectory(), FileName);

    /// <summary>Loads saved settings, or returns null when no candidate up-tree exists, is empty, or every
    /// candidate is unreadable/malformed.</summary>
    public static SavedSettings? TryLoad(string? startDirectory = null) => TryLoad(startDirectory, out _);

    /// <summary>Like <see cref="TryLoad(string?)"/>, but also reports <paramref name="loadedFrom"/> — the exact
    /// location the settings came from, or null when nothing was loaded. Walks up from
    /// <paramref name="startDirectory"/> exactly once (unlike a separate <see cref="FindExisting"/> +
    /// <see cref="ReadConfigJson"/> pair, which would re-walk the same chain twice) and, unlike a single-shot
    /// nearest-only read, a malformed or unreadable candidate does NOT end the search: the walk continues to the
    /// next ancestor, so a broken subdirectory <c>.specscribe</c> cannot silently shadow a valid one further up.
    /// [Review][Patch]</summary>
    public static SavedSettings? TryLoad(string? startDirectory, out string? loadedFrom)
    {
        loadedFrom = null;
        try
        {
            var dir = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, FileName);
                if (Directory.Exists(candidate) || File.Exists(candidate))
                {
                    var saved = TryReadCandidate(candidate);
                    if (saved is not null)
                    {
                        loadedFrom = candidate;
                        return saved;
                    }
                }

                dir = dir.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Best-effort discovery: an unreadable directory in the chain ends the walk with "no saved settings",
            // never a crash (NFR2).
        }

        return null;
    }

    /// <summary>Reads and deserializes the config document at <paramref name="location"/> (a candidate from the
    /// <see cref="TryLoad(string?, out string?)"/> walk-up), transparently supporting both the current folder form
    /// (<c>location/config.json</c>) and a not-yet-migrated flat file at <paramref name="location"/> itself. Null
    /// when the location has nothing, is empty, or fails to parse — the caller keeps walking rather than treating
    /// this as fatal. [ADR 0014] [Review][Patch]</summary>
    private static SavedSettings? TryReadCandidate(string location)
    {
        try
        {
            var json = ReadConfigJson(location);
            if (json is null) return null;

            var saved = JsonSerializer.Deserialize<SavedSettings>(json, SerializerOptions);
            return saved is null or { IsEmpty: true } ? null : saved;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads the raw config text at <paramref name="location"/>, transparently supporting both the current
    /// folder form (<c>location/config.json</c>) and a not-yet-migrated flat file at <paramref name="location"/>
    /// itself. Null when neither exists. [ADR 0014]</summary>
    private static string? ReadConfigJson(string location)
    {
        if (Directory.Exists(location))
        {
            var configPath = Path.Combine(location, ConfigFileName);
            return File.Exists(configPath) ? MarkdownConverter.ReadAllTextShared(configPath) : null;
        }

        return File.Exists(location) ? MarkdownConverter.ReadAllTextShared(location) : null;
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
        // Same persist-only-the-non-default rule again: machine-local IS the default, so it stays null (absent)
        // rather than writing a value that would make every save produce a non-empty file and defeat IsEmpty.
        // An explicitly persisted `MachineLocal` (hand-edited) still reads back fine — the tri-state is preserved
        // on the load side. [Story 5.5]
        TodayPolicy = ResolveCutoffOrNull(settings),
    };

    /// <summary>The cutoff worth persisting for <paramref name="settings"/>, or null when there is nothing to save
    /// (unset, the default, or an unparseable value). Never throws: <see cref="SiteSettings.ResolveDateCutoff"/>
    /// rejects a typo loudly at RESOLVE time, which is the right moment for it — a save path must not additionally
    /// blow up and lose the user's other, valid choices.
    /// <para><c>--as-of</c> is checked FIRST and independently of <c>--today-policy</c>, matching
    /// <see cref="SiteSettings.ResolveDateCutoff"/>'s precedence: the flag implies the policy, so a run driven only
    /// by <c>--as-of</c> still has a cutoff worth persisting. A disagreeing pair never reaches here — it is rejected
    /// at the validation gate. [Story 5.7]</para></summary>
    private static DateCutoff? ResolveCutoffOrNull(SiteSettings settings)
    {
        if (settings.AsOf is { Length: > 0 } && DatePolicies.TryParseAsOfDate(settings.AsOf, out var pinned))
        {
            return new DateCutoff(DatePolicy.AsOf, pinned);
        }

        // `!= default` rather than `!= MachineLocal`: default(DateCutoff) IS (MachineLocal, null), so this keeps
        // saying "only a non-default cutoff is worth writing" with the shape change absorbed. [Story 5.7]
        return DatePolicies.TryParse(settings.TodayPolicy, out var cutoff) && cutoff != default ? cutoff : null;
    }

    /// <summary>Writes the configured path/name choices to the <c>.specscribe</c> folder's <see cref="ConfigFileName"/>.
    /// Returns the folder path on success, or null when there was nothing worth saving or the write failed. Targets
    /// the same location <see cref="TryLoad"/> would read (the nearest one up-tree, or a new one in the start
    /// directory), so a configure-then-run round trip is symmetric from any depth. [Story 5.2 AC #1]
    /// <para>A not-yet-migrated flat file at that location is replaced by the folder form: the old file is removed
    /// and a fresh <c>.specscribe/config.json</c> is written in its place. [ADR 0014]</para></summary>
    public static string? TrySave(SiteSettings settings, string? startDirectory = null)
    {
        var saved = Capture(settings);

        if (saved.IsEmpty) return null;

        var path = ResolvePath(startDirectory);
        try
        {
            // Migrate: a legacy flat file and the new folder can't coexist under the same name.
            if (File.Exists(path)) File.Delete(path);

            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, ConfigFileName), JsonSerializer.Serialize(saved, SerializerOptions));
            return path;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Writes an ALREADY-BUILT <see cref="SavedSettings"/> document verbatim, through the same location
    /// resolution and legacy-file migration <see cref="TrySave"/> uses. Returns the folder path on success, or null
    /// when the write failed. [ADR 0037]
    ///
    /// <para><b>Why not just call <see cref="TrySave"/>.</b> That overload runs <see cref="Capture"/> over the
    /// MERGED live settings, which would re-persist auto-discovered values as if the user had chosen them — the
    /// exact trap <c>ConfigurePaths</c> sidesteps for <c>CodeUrl</c>, where accepting an auto-detected branch URL as
    /// the prompt default would freeze future runs onto today's branch. A caller that has computed precisely which
    /// fields the user set (the <c>config --save</c> path, and the settings form behind it) must be able to write
    /// that and nothing else.</para>
    ///
    /// <para><b>⚠️ Two deliberate divergences from <see cref="TrySave"/>, both required by the form.</b></para>
    /// <list type="number">
    /// <item>It writes even when <see cref="SavedSettings.IsEmpty"/>. That guard exists to avoid creating a file
    /// nobody asked for; here the user pressed Save, and clearing every field back to its default is a legitimate
    /// thing to have asked for. <see cref="TryReadCandidate"/> already treats an empty document as "no saved
    /// settings", so <c>{}</c> is harmless and honest.</item>
    /// <item>It never deletes the <c>.specscribe</c> FOLDER. ADR 0014 made it a container precisely so other
    /// per-directory state could live beside the config; removing it because the config went empty would take that
    /// with it.</item>
    /// </list></summary>
    public static string? TrySaveExplicit(SavedSettings saved, string? startDirectory = null)
    {
        var path = ResolvePath(startDirectory);
        try
        {
            // Migrate: a legacy flat file and the new folder can't coexist under the same name. Same rule as
            // TrySave — this deletes the legacy FILE, never the folder.
            if (File.Exists(path)) File.Delete(path);

            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, ConfigFileName), JsonSerializer.Serialize(saved, SerializerOptions));
            return path;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // A read-only checkout or a permission-denied path is a "could not save", not a crash — the form shows
            // the failure and the user's typed values are still theirs to retry with. TrySave predates this arm and
            // would throw here; a save driven from a button must not take the editor down with it.
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
        // CLI wins: fill from the persisted cutoff only when NEITHER --today-policy NOR --as-of was passed this run.
        // The --as-of half of that guard is load-bearing, not defensive: restoring a saved "utc" on top of an
        // explicit --as-of would manufacture exactly the disagreement ResolveDateCutoff rejects, turning a valid
        // command line into an error because of a file the user never mentioned. Stored as the canonical token so
        // the restored value round-trips through the same parser the command line uses — one parse path, so a saved
        // value can never mean something the flag couldn't. [Story 5.5, --as-of guard added in Story 5.7]
        if (settings.TodayPolicy is not { Length: > 0 } && settings.AsOf is not { Length: > 0 }
            && saved.TodayPolicy is { } savedCutoff)
        {
            settings.TodayPolicy = DatePolicies.Token(savedCutoff);
        }
    }
}
