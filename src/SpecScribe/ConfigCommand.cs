using System.ComponentModel;
using System.Text.Json;
using Spectre.Console.Cli;

namespace SpecScribe;

/// <summary>Options for <c>specscribe config</c>. Extends <see cref="SiteSettings"/> rather than declaring a
/// parallel flag surface, so "the value you would pass to <c>generate</c>" and "the value you are saving" are
/// spelled identically and can never drift. [ADR 0037]</summary>
public sealed class ConfigSettings : SiteSettings
{
    [CommandOption("--json")]
    [Description("Emit the effective configuration and each field's provenance as JSON on stdout, then exit.")]
    public bool Json { get; set; }

    [CommandOption("--save")]
    [Description("Write the fields given on this command line to the directory-scoped .specscribe settings, then exit.")]
    public bool Save { get; set; }

    [CommandOption("--clear <FIELD>")]
    [Description("With --save, remove FIELD from the saved settings so it falls back to the default. Repeatable. Field names are the keys --show-config prints (source, adrs, output, project, readme, deep_git, code_url, today_policy).")]
    public string[] Clear { get; set; } = Array.Empty<string>();

    [CommandOption("--form")]
    [Description("Render the settings form as a self-contained VS Code webview document on stdout (used by the SpecScribe extension).")]
    public bool Form { get; set; }
}

/// <summary>`specscribe config` — read, render and write the directory-scoped settings.
///
/// <para><b>Why this command exists.</b> Before it, <c>.specscribe/config.json</c> could be written in exactly one
/// way: the interactive Spectre menu's "Configure paths". That made the settings unreachable from any
/// non-interactive caller, and in particular from the VS Code extension, whose one configuration affordance was to
/// open the file in an editor — which, since ADR 0014 made <c>.specscribe</c> a FOLDER, did not even work. Owner
/// field feedback 2026-08-01: "VS Code should be able to configure using the tool itself."</para>
///
/// <para><b>The core stays the only writer</b> (ADR 0037 Decision 1). The extension renders a form the core
/// produced and spawns this command to save it; the persist-only-when-set rules, the date-token vocabulary and the
/// ADR 0014 migration all stay in <see cref="SettingsStore"/> rather than being re-implemented in TypeScript.</para>
///
/// <para>With none of <c>--json</c>/<c>--save</c>/<c>--form</c> it prints the same machine-parseable diagnostics
/// <c>--show-config</c> does, so the command is independently useful and independently testable.</para></summary>
public sealed class ConfigCommand : Command<ConfigSettings>
{
    /// <summary>Every field name <c>--clear</c> accepts, mapped to the eraser that unsets it on a
    /// <see cref="SavedSettings"/>. Keyed on <see cref="SettingsResolver.Fields"/> — the SAME tokens
    /// <c>--show-config</c> prints and the form posts back, so a user reads one vocabulary everywhere.</summary>
    private static readonly IReadOnlyDictionary<string, Action<SavedSettings>> Erasers =
        new Dictionary<string, Action<SavedSettings>>(StringComparer.OrdinalIgnoreCase)
        {
            [SettingsResolver.Fields.Source] = s => s.Source = null,
            [SettingsResolver.Fields.Adrs] = s => s.Adrs = null,
            [SettingsResolver.Fields.Output] = s => s.Output = null,
            [SettingsResolver.Fields.Project] = s => s.ProjectName = null,
            [SettingsResolver.Fields.Readme] = s => s.IncludeReadme = null,
            [SettingsResolver.Fields.DeepGit] = s => s.DeepGit = null,
            [SettingsResolver.Fields.CodeUrl] = s => s.CodeUrl = null,
            [SettingsResolver.Fields.TodayPolicy] = s => s.TodayPolicy = null,
        };

    /// <summary>The field names <c>--clear</c> accepts, sorted — for the error message, so a typo is answered with
    /// the actual vocabulary rather than "invalid".</summary>
    public static IReadOnlyList<string> ClearableFields => Erasers.Keys.Order(StringComparer.Ordinal).ToList();

    protected override int Execute(CommandContext context, ConfigSettings settings, CancellationToken cancellationToken)
    {
        // Snapshot which fields THIS command line set BEFORE any merge — SettingsStore.ApplyTo fills nulls in
        // place, so after it runs a CLI value is indistinguishable from a restored one. Same load-bearing ordering
        // SettingsResolver.Load relies on, and the reason --save can write "only what the user just set".
        var cli = CliOverrides.Capture(settings);
        var load = SettingsResolver.Load(settings);

        if (settings.Save)
        {
            return SaveInvocation(settings, cli, load);
        }

        // Everything below REPORTS, and so must not fail on a project whose paths do not resolve — that is
        // precisely the project whose settings the caller is trying to read in order to fix them. Tolerant
        // resolution (no `_bmad-output` marker never throws) is the same posture `webview` takes.
        //
        // ⚠️ Resolved from the load ALREADY taken above, never by loading again. Load() merges the saved document
        // onto `settings` in place, so a second load would capture those merged values as CLI overrides and report
        // every saved field as having come from the command line — see ResolveTolerant's doc.
        var resolved = SettingsResolver.ResolveTolerant(load, settings);

        if (settings.Form)
        {
            Console.Out.Write(SettingsFormTemplater.RenderDocument(resolved, load));
            return ExitCodes.Success;
        }

        if (settings.Json)
        {
            Console.Out.WriteLine(SerializeConfig(resolved, load));
            return ExitCodes.Success;
        }

        ConsoleUi.PrintConfigDiagnostics(resolved);
        return ExitCodes.Success;
    }

    /// <summary>`--save`: overlay this invocation's explicitly-set fields onto the existing document, remove the
    /// <c>--clear</c>ed ones, and write the result.
    ///
    /// <para><b>Overlay, not replace.</b> A form posts every field it knows about, but a scripted caller may pass
    /// one — and a save that dropped the fields it was not told about would silently discard the user's other
    /// settings. The three-step shape (existing → overlay set → remove cleared) is the only one that can both set
    /// and unset.</para>
    ///
    /// <para>Validation errors go to stderr as one JSON object per line, the convention
    /// <see cref="WebviewCommand.SerializeDiagnostics"/> already uses for the Problems-panel wire — so the form can
    /// attach a message to the offending FIELD rather than screen-scraping a human sentence.</para></summary>
    private static int SaveInvocation(ConfigSettings settings, CliOverrides cli, SettingsLoad load)
    {
        var errors = new List<FieldError>();

        foreach (var field in settings.Clear)
        {
            if (!Erasers.ContainsKey(field))
            {
                errors.Add(new FieldError(
                    field,
                    $"Unknown field '{field}'. Valid fields: {string.Join(", ", ClearableFields)}."));
            }
        }

        // Validate the VALUES before touching disk, using the same gate every other command goes through, so the
        // form cannot persist something `generate` would then reject.
        if (settings.Validate() is { Successful: false } validation)
        {
            errors.Add(new FieldError(SettingsResolver.Fields.TodayPolicy, validation.Message ?? "Invalid settings."));
        }

        if (errors.Count > 0)
        {
            EmitErrors(errors);
            return ExitCodes.Failure;
        }

        // Start from what is on disk (null when there is none), so a save never discards a field it was not told
        // about. `Capture` is NOT used here — it would fold in auto-discovered values as if the user had chosen
        // them, the trap SettingsStore.TrySaveExplicit's doc records.
        var saved = load.Saved ?? new SavedSettings();

        if (cli.Source) saved.Source = settings.Source;
        if (cli.Adrs) saved.Adrs = settings.Adrs;
        if (cli.Output) saved.Output = settings.Output;
        if (cli.ProjectName) saved.ProjectName = settings.ProjectName;
        if (cli.CodeUrl) saved.CodeUrl = settings.CodeUrl;
        // The booleans have no "unset" on a command line (absent == off), so only the ON state can be claimed as an
        // override — the same asymmetry CliOverrides.Capture documents. `--clear` is how they go back to unset,
        // which is exactly why no `--no-deep-git` / `--readme` counter-flags are added (ADR 0037 Decision 4).
        if (cli.DeepGit) saved.DeepGit = true;
        if (cli.NoReadme) saved.IncludeReadme = false;
        if (cli.TodayPolicy && DatePolicies.TryParse(EffectiveTodayToken(settings), out var cutoff))
        {
            saved.TodayPolicy = cutoff;
        }

        // Cleared LAST, so `--output ./x --clear output` is an unset rather than order-dependent.
        foreach (var field in settings.Clear) Erasers[field](saved);

        if (SettingsStore.TrySaveExplicit(saved) is not { } path)
        {
            EmitErrors([new FieldError(
                string.Empty,
                "Could not write the settings file — the location may be read-only or in use.")]);
            return ExitCodes.Failure;
        }

        // stdout carries the machine answer ("where did it go"); the form reads it back to show the file.
        Console.Out.WriteLine(JsonSerializer.Serialize(
            new { savedTo = PathUtil.NormalizeSlashes(Path.Combine(path, SettingsStore.ConfigFileName)) },
            WebviewCommand.CamelCaseOptions));
        return ExitCodes.Success;
    }

    /// <summary>The token a <c>--today-policy</c>/<c>--as-of</c> pair resolves to. Mirrors
    /// <see cref="SiteSettings.ResolveDateCutoff"/>'s precedence — <c>--as-of</c> implies the policy — so a run
    /// driven only by <c>--as-of</c> still persists a cutoff. A disagreeing pair never reaches here; it is rejected
    /// by the validation gate above.</summary>
    private static string? EffectiveTodayToken(ConfigSettings settings)
        => settings.AsOf is { Length: > 0 } asOf ? $"as-of:{asOf}" : settings.TodayPolicy;

    /// <summary>One save-time failure, attributed to the field that caused it. <see cref="Field"/> is a
    /// <see cref="SettingsResolver.Fields"/> key, or empty when the failure is about the write itself.</summary>
    private sealed record FieldError(string Field, string Message);

    private static void EmitErrors(IReadOnlyList<FieldError> errors)
    {
        foreach (var error in errors)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(
                new { field = error.Field, severity = "error", message = error.Message },
                WebviewCommand.CamelCaseOptions));
        }
    }

    /// <summary>`--json`: every configurable field with its effective value, the default it would fall back to, and
    /// where the value actually came from. This is what populates the form — and it is why the form can show a
    /// provenance tag beside each control instead of presenting eight indistinguishable text boxes.</summary>
    internal static string SerializeConfig(ResolvedConfig resolved, SettingsLoad load)
    {
        var saved = load.Saved;
        var payload = new
        {
            settingsPath = load.Path is { } p
                ? PathUtil.NormalizeSlashes(Path.Combine(p, SettingsStore.ConfigFileName))
                : null,
            repoRoot = PathUtil.NormalizeSlashes(resolved.Options.RepoRoot),
            fields = resolved.Provenance.Select(entry => new
            {
                field = entry.Field,
                option = entry.Option,
                effective = entry.EffectiveValue,
                source = entry.Source.ToString(),
                // Whether the SAVED document carries this field — i.e. whether "Inherit default" is currently
                // selected. Distinct from `source`: a field can read Default because nothing set it, or read
                // CommandLine on a run that also has it saved.
                saved = IsSavedExplicitly(entry.Field, saved),
            }).ToList(),
        };
        return JsonSerializer.Serialize(payload, WebviewCommand.CamelCaseOptions);
    }

    private static bool IsSavedExplicitly(string field, SavedSettings? saved) => saved is not null && field switch
    {
        SettingsResolver.Fields.Source => saved.Source is not null,
        SettingsResolver.Fields.Adrs => saved.Adrs is not null,
        SettingsResolver.Fields.Output => saved.Output is not null,
        SettingsResolver.Fields.Project => saved.ProjectName is not null,
        SettingsResolver.Fields.Readme => saved.IncludeReadme is not null,
        SettingsResolver.Fields.DeepGit => saved.DeepGit is not null,
        SettingsResolver.Fields.CodeUrl => saved.CodeUrl is not null,
        SettingsResolver.Fields.TodayPolicy => saved.TodayPolicy is not null,
        _ => false,
    };
}
