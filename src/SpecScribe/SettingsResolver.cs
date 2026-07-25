using System.Globalization;

namespace SpecScribe;

/// <summary>Where an effective configuration value came from. The three-way distinction is the point: it is what
/// lets a diagnostic answer "why is it building into <em>that</em> folder?" without the user reverse-engineering
/// the merge. Ordered by precedence, highest first. [Story 5.2 AC #2]</summary>
public enum ConfigSource
{
    /// <summary>Passed explicitly on this run's command line — always wins.</summary>
    CommandLine,

    /// <summary>Restored from the directory-scoped <c>.specscribe</c>.</summary>
    SavedSettings,

    /// <summary>Neither supplied it: auto-discovery, or the built-in default.</summary>
    Default,
}

/// <summary>One configurable field's effective value and where it came from.</summary>
/// <param name="Field">Stable machine key (<c>source</c>, <c>output</c>, …) — the token a CI script greps for, so it
/// must not drift with display wording.</param>
/// <param name="Option">The CLI option that sets this field, used as the human-facing tag when
/// <see cref="Source"/> is <see cref="ConfigSource.CommandLine"/> ("this came from <c>--output</c>").</param>
/// <param name="EffectiveValue">The value as resolved — an absolute path for the path fields, so the tag annotates
/// the very string the user is looking at.</param>
public sealed record ConfigProvenance(string Field, string Option, string EffectiveValue, ConfigSource Source);

/// <summary>Which fields this run's command line set, snapshotted BEFORE any saved settings are merged in.
/// Load-bearing ordering: <see cref="SettingsStore.ApplyTo"/> fills nulls in place, so once it has run there is no
/// way to tell a CLI-supplied value from a restored one — the distinction has to be captured first or it is lost.
/// [Story 5.2 AC #3]</summary>
public sealed record CliOverrides(bool Source, bool Adrs, bool Output, bool ProjectName, bool NoReadme, bool DeepGit, bool CodeUrl, bool TodayPolicy)
{
    public static CliOverrides Capture(SiteSettings settings) => new(
        // `is not null`, NOT `{ Length: > 0 }` — must agree with SettingsStore.ApplyTo's `??=`, which only fills a
        // field when it is null. An explicit `--source ""` is non-null, so ApplyTo leaves it alone and the CLI value
        // silently wins either way; the predicate here has to agree or the field is misattributed to SavedSettings/
        // Default while the CLI's (empty) value is what actually took effect. [Review][Patch]
        Source: settings.Source is not null,
        Adrs: settings.Adrs is not null,
        Output: settings.Output is not null,
        ProjectName: settings.ProjectName is not null,
        // Boolean flags have no "unset" on the command line: absent is indistinguishable from an explicit off, so
        // only the on-state can be claimed as a CLI override. Same reasoning as SettingsStore.ApplyTo's tri-state.
        NoReadme: settings.NoReadme,
        DeepGit: settings.DeepGit,
        CodeUrl: settings.CodeUrl is not null,
        // TodayPolicy is deliberately still `{ Length: > 0 }`, not `is not null`: SiteSettings.Validate() and
        // ResolveDatePolicy() both already treat an empty string identically to "not passed" (falls back to
        // MachineLocal) — unlike the path/name fields above, there is no ApplyTo `??=` divergence to reconcile
        // here, so aligning with THAT existing behavior is correct, not a bug. [Story 5.5]
        TodayPolicy: settings.TodayPolicy is { Length: > 0 });
}

/// <summary>The outcome of loading <c>.specscribe</c> for a run: what was found, where, and — captured before the
/// merge — which fields the command line had already set. Held as its own value so the interactive menu can load
/// once at entry (and show what was restored) while still resolving paths once per action, without either half
/// re-reading the file or losing the CLI snapshot. [Story 5.2 Task 2]</summary>
public sealed record SettingsLoad(SavedSettings? Saved, string? Path, CliOverrides Cli);

/// <summary>The single resolution seam every command routes through: load the directory-scoped settings, merge
/// them under the run's own overrides, resolve paths exactly once, and record where each effective value came from.
/// <para>This exists because the pieces were previously wired up only inside the interactive menu — <c>generate</c>
/// and <c>watch</c> called <see cref="SiteSettings.Resolve"/> directly and never read <c>.specscribe</c> at all, so
/// the same repository behaved differently depending on which surface you drove it from. One entry point makes
/// that divergence structurally impossible rather than merely fixed. [Story 5.2 AC #1, #2, #3]</para>
/// <para>Deliberately a thin layer: <see cref="ForgeOptions.Resolve"/> stays the one pure path-resolution
/// primitive. This adds only load, precedence snapshotting, and provenance on top of it.</para></summary>
public static class SettingsResolver
{
    /// <summary>Stable machine keys for the configurable fields, in display order. Constants because they appear in
    /// the <c>--show-config</c> output a CI script parses; display wording may change, these may not.</summary>
    public static class Fields
    {
        public const string Project = "project";
        public const string Source = "source";
        public const string Adrs = "adrs";
        public const string Output = "output";
        public const string Readme = "readme";
        public const string DeepGit = "deep_git";
        public const string CodeUrl = "code_url";
        public const string TodayPolicy = "today_policy";
    }

    /// <summary>Leading token of the machine-parseable config lines, mirroring
    /// <see cref="GenerationSummary.LinePrefix"/> so both CI surfaces are selected the same way. Distinct from the
    /// run summary's prefix on purpose: the two lines answer different questions and a script that wants one must
    /// never accidentally match the other.</summary>
    public const string LinePrefix = "SpecScribe config:";

    /// <summary>Reads the directory-scoped settings and merges them into <paramref name="settings"/> under the
    /// run's own overrides, returning what was found alongside the pre-merge CLI snapshot. Does not resolve paths,
    /// so it cannot throw a discovery error — the interactive menu needs to show restored settings even when the
    /// project root is not discoverable yet.</summary>
    public static SettingsLoad Load(SiteSettings settings, string? startDirectory = null)
    {
        // Snapshot FIRST: ApplyTo mutates in place and would otherwise erase the distinction. [AC #3]
        var cli = CliOverrides.Capture(settings);
        // The out-param overload walks up exactly once and reports the location that actually supplied `saved` —
        // not necessarily the nearest candidate, since a malformed nearer one is skipped rather than shadowing a
        // valid ancestor. [Review][Patch]
        var saved = SettingsStore.TryLoad(startDirectory, out var loadedFrom);
        if (saved is not null)
        {
            SettingsStore.ApplyTo(saved, settings);
        }

        return new SettingsLoad(saved, loadedFrom, cli);
    }

    /// <summary>Resolves an already-loaded configuration into absolute paths — exactly one call to the pure
    /// <see cref="ForgeOptions.Resolve"/> primitive — and derives per-field provenance from that same result, so the
    /// values reported and the values used are the same values, not two independent resolutions that could drift.
    /// <para>Propagates <see cref="DirectoryNotFoundException"/> from the underlying resolve untouched: for
    /// <c>generate</c>/<c>watch</c> that is a fatal, actionable error, while the interactive menu catches it and
    /// offers "Configure paths" instead. Precedence and diagnostics are this layer's job; deciding whether a
    /// discovery failure is fatal is the caller's.</para></summary>
    public static ResolvedConfig Resolve(SettingsLoad load, SiteSettings settings, string? startDirectory = null)
    {
        var options = settings.Resolve(startDirectory);
        return new ResolvedConfig(options, BuildProvenance(options, load), load.Path, load.Saved);
    }

    /// <summary>Load-and-resolve in one step — the shape <c>generate</c>/<c>watch</c> use, where there is exactly
    /// one resolution per process and no menu loop to hold the load across.</summary>
    public static ResolvedConfig Resolve(SiteSettings settings, string? startDirectory = null)
        => Resolve(Load(settings, startDirectory), settings, startDirectory);

    /// <summary>Attributes each configurable field to the highest-precedence source that actually supplied it:
    /// CLI &gt; <c>.specscribe</c> &gt; auto-discovery/default, evaluated identically for every field so the order
    /// cannot quietly differ between them. [AC #3]</summary>
    private static IReadOnlyList<ConfigProvenance> BuildProvenance(ForgeOptions options, SettingsLoad load)
    {
        var saved = load.Saved;
        var cli = load.Cli;

        return new[]
        {
            Entry(Fields.Project, "--project-name", options.SiteTitle, cli.ProjectName, saved?.ProjectName is not null),
            Entry(Fields.Source, "--source", options.SourceRoot, cli.Source, saved?.Source is not null),
            Entry(Fields.Adrs, "--adrs", options.AdrSourceRoot, cli.Adrs, saved?.Adrs is not null),
            Entry(Fields.Output, "--output", options.OutputRoot, cli.Output, saved?.Output is not null),
            // Reported as the positive "is the README included?" rather than the negative flag name, so the value
            // reads the same way the resolved ForgeOptions carries it.
            Entry(Fields.Readme, "--no-readme", Bool(options.IncludeReadme), cli.NoReadme, saved?.IncludeReadme is not null),
            Entry(Fields.DeepGit, "--deep-git", Bool(options.DeepGitAnalytics), cli.DeepGit, saved?.DeepGit is not null),
            // An auto-detected code URL is genuinely neither CLI nor saved — it is discovery, so it reports Default
            // exactly like an auto-discovered path does.
            Entry(Fields.CodeUrl, "--code-url", options.CodeSourceBaseUrl ?? string.Empty, cli.CodeUrl, saved?.CodeUrl is not null),
            // Reported as the canonical token (not the display label) — this is the machine surface a CI script
            // greps, and the token is exactly what could be passed back to --today-policy. [Story 5.5]
            Entry(Fields.TodayPolicy, "--today-policy", DatePolicies.Token(options.DatePolicy), cli.TodayPolicy, saved?.TodayPolicy is not null),
        };
    }

    private static ConfigProvenance Entry(string field, string option, string value, bool fromCli, bool fromSaved)
        => new(field, option, value, fromCli ? ConfigSource.CommandLine : fromSaved ? ConfigSource.SavedSettings : ConfigSource.Default);

    private static string Bool(bool value) => value ? "true" : "false";

    /// <summary>The dim tag shown beside a value in the human paths block: the flag that set it, the settings file
    /// that supplied it, or "auto" when nothing did. Pure so <see cref="ConsoleUi"/> holds no provenance logic.</summary>
    public static string DisplayTag(ConfigProvenance entry) => entry.Source switch
    {
        ConfigSource.CommandLine => entry.Option,
        ConfigSource.SavedSettings => SettingsStore.FileName,
        _ => "auto",
    };

    /// <summary>The machine-parseable provenance report behind <c>--show-config</c>: one line per field plus a line
    /// naming the settings file in effect. Pure string building with no <c>AnsiConsole</c> reference, so the wire
    /// shape is unit-testable without a live console — the same discipline as
    /// <see cref="GenerationSummary.FormatLine"/>.
    /// <para>Shape: <c>SpecScribe config: field=&lt;key&gt; origin=&lt;source&gt; value=&lt;value&gt;</c>. One line
    /// per field rather than one packed line, and <c>value=</c> last, because path values routinely contain spaces —
    /// a packed line could not be split back into fields without a quoting scheme, whereas here everything after
    /// <c>value=</c> is the value, and a script selects a field with a single fixed-string match.</para></summary>
    public static IReadOnlyList<string> FormatConfigLines(ResolvedConfig resolved)
    {
        var lines = new List<string>(resolved.Provenance.Count + 1)
        {
            string.Create(CultureInfo.InvariantCulture, $"{LinePrefix} settings_file={resolved.SavedSettingsPath ?? "(none)"}"),
        };

        foreach (var entry in resolved.Provenance)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{LinePrefix} field={entry.Field} origin={OriginToken(entry.Source)} value={EscapeForLine(entry.EffectiveValue)}"));
        }

        return lines;
    }

    /// <summary>Escapes an embedded newline out of a value bound for a <see cref="FormatConfigLines"/> line — reachable
    /// via a hand-edited <c>.specscribe</c> or a value containing a literal newline — so it cannot split the value
    /// across extra, unprefixed lines and break the documented one-line-per-field contract. [Review][Patch]</summary>
    private static string EscapeForLine(string value)
        => value.Contains('\n') || value.Contains('\r')
            ? value.Replace("\r\n", "\\n", StringComparison.Ordinal)
                .Replace("\r", "\\n", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
            : value;

    /// <summary>The lowercase token a script matches on. Spelled out rather than <c>ToString().ToLower()</c> so
    /// renaming an enum member cannot silently change a published contract.</summary>
    public static string OriginToken(ConfigSource source) => source switch
    {
        ConfigSource.CommandLine => "commandline",
        ConfigSource.SavedSettings => "savedsettings",
        _ => "default",
    };
}

/// <summary>One run's fully resolved configuration: the options generation actually uses, where every effective
/// value came from, and the settings file (if any) that contributed. [Story 5.2 AC #2]</summary>
/// <param name="Saved">The loaded <c>.specscribe</c> contents, so a caller can show what was restored without
/// re-reading the file.</param>
public sealed record ResolvedConfig(
    ForgeOptions Options,
    IReadOnlyList<ConfigProvenance> Provenance,
    string? SavedSettingsPath,
    SavedSettings? Saved)
{
    /// <summary>Provenance for one field by its <see cref="SettingsResolver.Fields"/> key, or null when the field is
    /// not tracked — lets the paths block annotate the rows it prints without indexing by position.</summary>
    public ConfigProvenance? For(string field)
        => Provenance.FirstOrDefault(p => string.Equals(p.Field, field, StringComparison.Ordinal));
}
