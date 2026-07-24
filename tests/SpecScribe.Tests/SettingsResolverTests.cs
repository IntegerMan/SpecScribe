using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Covers the Story 5.2 resolution seam: CLI &gt; <c>.specscribe</c> &gt; auto-discovery precedence, the
/// per-field provenance that precedence produces, and the machine-parseable <c>--show-config</c> shape. Headless
/// throughout — every case drives the injected <c>startDirectory</c> rather than the process working directory, so
/// the suite stays parallel-safe and never touches a live console.</summary>
public class SettingsResolverTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-resolver-").FullName;
    private int _next;

    /// <summary>A fresh repo-shaped directory: its own subtree (walk-up discovery means shared parents leak between
    /// cases) containing the <c>_bmad-output</c> marker auto-discovery looks for.</summary>
    private string NewRepo()
    {
        var repo = Directory.CreateDirectory(Path.Combine(_root, $"case-{++_next}", "repo")).FullName;
        Directory.CreateDirectory(Path.Combine(repo, ForgeOptions.SourceDirName));
        return repo;
    }

    private static void WriteSettings(string dir, string json)
        => File.WriteAllText(Path.Combine(dir, SettingsStore.FileName), json);

    private static ConfigSource OriginOf(ResolvedConfig resolved, string field)
    {
        var entry = resolved.For(field);
        Assert.NotNull(entry);
        return entry!.Source;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    // --- Precedence (AC #2, #3) ---

    [Fact]
    public void Resolve_CommandLineBeatsSavedSettings()
    {
        var repo = NewRepo();
        var cliOutput = Path.Combine(repo, "cli-out");
        WriteSettings(repo, $$"""{ "Output": {{Json(Path.Combine(repo, "saved-out"))}}, "ProjectName": "Saved" }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings { Output = cliOutput }, repo);

        Assert.Equal(cliOutput, resolved.Options.OutputRoot);
        Assert.Equal("Saved", resolved.Options.SiteTitle); // untouched field still comes from the file
    }

    [Fact]
    public void Resolve_SavedSettingsBeatDefaults()
    {
        var repo = NewRepo();
        var savedOutput = Path.Combine(repo, "saved-out");
        WriteSettings(repo, $$"""{ "Output": {{Json(savedOutput)}} }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.Equal(savedOutput, resolved.Options.OutputRoot);
    }

    [Fact]
    public void Resolve_FallsBackToAutoDiscoveryWhenNothingWasConfigured()
    {
        var repo = NewRepo();

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.Equal(Path.Combine(repo, ForgeOptions.SourceDirName), resolved.Options.SourceRoot);
        Assert.Equal(Path.Combine(repo, ForgeOptions.OutputDirName), resolved.Options.OutputRoot);
        Assert.Null(resolved.SavedSettingsPath);
    }

    /// <summary>AC #1: the parity case the story exists for — settings configured interactively at the repo root are
    /// picked up by a later non-interactive run started anywhere beneath it.</summary>
    [Fact]
    public void Resolve_AppliesTheRepoRootSettingsFileFromASubdirectory()
    {
        var repo = NewRepo();
        var nested = Directory.CreateDirectory(Path.Combine(repo, "src", "deep")).FullName;
        var savedOutput = Path.Combine(repo, "saved-out");
        WriteSettings(repo, $$"""{ "Output": {{Json(savedOutput)}}, "ProjectName": "From Root" }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings(), nested);

        Assert.Equal(savedOutput, resolved.Options.OutputRoot);
        Assert.Equal("From Root", resolved.Options.SiteTitle);
        Assert.Equal(Path.Combine(repo, SettingsStore.FileName), resolved.SavedSettingsPath);
    }

    // --- Provenance (AC #2, #3) ---

    /// <summary>AC #3 in full: overriding ONE field must not relabel the others. Only the overridden field reports
    /// CommandLine; fields the file supplied still report SavedSettings; fields neither supplied report Default.</summary>
    [Fact]
    public void Resolve_AttributesOnlyTheOverriddenFieldToTheCommandLine()
    {
        var repo = NewRepo();
        WriteSettings(repo, $$"""{ "Output": {{Json(Path.Combine(repo, "saved-out"))}}, "ProjectName": "Saved" }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings { Output = Path.Combine(repo, "cli-out") }, repo);

        Assert.Equal(ConfigSource.CommandLine, OriginOf(resolved, SettingsResolver.Fields.Output));
        Assert.Equal(ConfigSource.SavedSettings, OriginOf(resolved, SettingsResolver.Fields.Project));
        Assert.Equal(ConfigSource.Default, OriginOf(resolved, SettingsResolver.Fields.Source));
        Assert.Equal(ConfigSource.Default, OriginOf(resolved, SettingsResolver.Fields.Adrs));
    }

    [Fact]
    public void Resolve_AttributesEveryPathFieldToTheCommandLineWhenAllArePassed()
    {
        var repo = NewRepo();
        WriteSettings(repo, """{ "ProjectName": "Saved" }""");
        var settings = new SiteSettings
        {
            Source = Path.Combine(repo, ForgeOptions.SourceDirName),
            Adrs = Path.Combine(repo, "docs", "adrs"),
            Output = Path.Combine(repo, "out"),
            ProjectName = "From CLI",
        };

        var resolved = SettingsResolver.Resolve(settings, repo);

        Assert.Equal("From CLI", resolved.Options.SiteTitle);
        foreach (var field in new[]
                 {
                     SettingsResolver.Fields.Source, SettingsResolver.Fields.Adrs,
                     SettingsResolver.Fields.Output, SettingsResolver.Fields.Project,
                 })
        {
            Assert.Equal(ConfigSource.CommandLine, OriginOf(resolved, field));
        }
    }

    [Fact]
    public void Resolve_ReportsDefaultProvenanceWhenNeitherSourceSuppliedAField()
    {
        var repo = NewRepo();

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.All(resolved.Provenance, p => Assert.Equal(ConfigSource.Default, p.Source));
    }

    // --- README-inclusion parity (AC #4) ---

    [Fact]
    public void Resolve_RestoresAndAttributesAPersistedReadmeExclusion()
    {
        var repo = NewRepo();
        WriteSettings(repo, """{ "IncludeReadme": false }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.False(resolved.Options.IncludeReadme);
        Assert.Equal(ConfigSource.SavedSettings, OriginOf(resolved, SettingsResolver.Fields.Readme));
        Assert.Equal("false", resolved.For(SettingsResolver.Fields.Readme)!.EffectiveValue);
    }

    [Fact]
    public void Resolve_AttributesAnExplicitNoReadmeToTheCommandLine()
    {
        var repo = NewRepo();
        WriteSettings(repo, """{ "IncludeReadme": true }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings { NoReadme = true }, repo);

        Assert.False(resolved.Options.IncludeReadme);
        Assert.Equal(ConfigSource.CommandLine, OriginOf(resolved, SettingsResolver.Fields.Readme));
    }

    [Fact]
    public void Resolve_KeepsTheReadmeIncludedByDefault()
    {
        var repo = NewRepo();

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.True(resolved.Options.IncludeReadme);
        Assert.Equal(ConfigSource.Default, OriginOf(resolved, SettingsResolver.Fields.Readme));
    }

    [Fact]
    public void Resolve_RestoresAndAttributesAPersistedDeepGitOptIn()
    {
        var repo = NewRepo();
        WriteSettings(repo, """{ "DeepGit": true }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.True(resolved.Options.DeepGitAnalytics);
        Assert.Equal(ConfigSource.SavedSettings, OriginOf(resolved, SettingsResolver.Fields.DeepGit));
    }

    // --- Story 5.5: the date-page "today" policy joins the resolution seam ---

    [Fact]
    public void Resolve_CommandLineTodayPolicyBeatsSavedAndReportsCommandLine()
    {
        var repo = NewRepo();
        WriteSettings(repo, """{ "TodayPolicy": "LastCommit" }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings { TodayPolicy = "utc" }, repo);

        Assert.Equal(DatePolicy.Utc, resolved.Options.DatePolicy);
        Assert.Equal(ConfigSource.CommandLine, OriginOf(resolved, SettingsResolver.Fields.TodayPolicy));
        // Reported as the canonical token — the grep surface a CI script consumes.
        Assert.Equal("utc", resolved.For(SettingsResolver.Fields.TodayPolicy)!.EffectiveValue);
    }

    [Fact]
    public void Resolve_RestoresAndAttributesAPersistedTodayPolicy()
    {
        var repo = NewRepo();
        WriteSettings(repo, """{ "TodayPolicy": "Utc" }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.Equal(DatePolicy.Utc, resolved.Options.DatePolicy);
        Assert.Equal(ConfigSource.SavedSettings, OriginOf(resolved, SettingsResolver.Fields.TodayPolicy));
    }

    [Fact]
    public void Resolve_DefaultsTodayPolicyToMachineLocalReportedAsDefault()
    {
        var repo = NewRepo();

        var resolved = SettingsResolver.Resolve(new SiteSettings(), repo);

        Assert.Equal(DatePolicy.MachineLocal, resolved.Options.DatePolicy);
        Assert.Equal(ConfigSource.Default, OriginOf(resolved, SettingsResolver.Fields.TodayPolicy));
        Assert.Equal("machine-local", resolved.For(SettingsResolver.Fields.TodayPolicy)!.EffectiveValue);
    }

    /// <summary>Reject-don't-silently-accept: a typo'd policy fails Spectre's parse-time validation gate rather than
    /// falling back to the default, and the message lists every valid value. [Story 5.5]</summary>
    [Fact]
    public void Validate_RejectsAnUnrecognizedTodayPolicyWithAnActionableMessage()
    {
        var result = new SiteSettings { TodayPolicy = "yesterday" }.Validate();

        Assert.False(result.Successful);
        Assert.Contains("yesterday", result.Message!, StringComparison.Ordinal);
        Assert.All(DatePolicies.CanonicalTokens, t => Assert.Contains(t, result.Message!, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AcceptsACanonicalTodayPolicy_AndAnAbsentOne()
    {
        Assert.True(new SiteSettings { TodayPolicy = "utc" }.Validate().Successful);
        Assert.True(new SiteSettings().Validate().Successful);
    }

    [Fact]
    public void ResolveDatePolicy_ThrowsOnAnUnrecognizedValue()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SiteSettings { TodayPolicy = "nope" }.ResolveDatePolicy());
        Assert.Contains("nope", ex.Message, StringComparison.Ordinal);
    }

    // --- Resolve once (AC #2, "resolve effective settings once per run, preserving provenance") ---

    /// <summary>The requirement's real risk is not the cost of a second resolve but its ability to DISAGREE with the
    /// first. Asserting that every reported value is the value carried by the returned options proves both readings
    /// came from the one resolution.</summary>
    [Fact]
    public void Resolve_ReportsProvenanceValuesTakenFromTheSameResolvedOptions()
    {
        var repo = NewRepo();
        WriteSettings(repo, $$"""{ "Output": {{Json(Path.Combine(repo, "saved-out"))}} }""");

        var resolved = SettingsResolver.Resolve(new SiteSettings { ProjectName = "Once" }, repo);
        var o = resolved.Options;

        Assert.Equal(o.SiteTitle, resolved.For(SettingsResolver.Fields.Project)!.EffectiveValue);
        Assert.Equal(o.SourceRoot, resolved.For(SettingsResolver.Fields.Source)!.EffectiveValue);
        Assert.Equal(o.AdrSourceRoot, resolved.For(SettingsResolver.Fields.Adrs)!.EffectiveValue);
        Assert.Equal(o.OutputRoot, resolved.For(SettingsResolver.Fields.Output)!.EffectiveValue);
    }

    /// <summary>The menu resolves repeatedly from one load. Because <see cref="SettingsStore.ApplyTo"/> mutates the
    /// settings in place, a second resolve that re-snapshotted the command line would see the restored values as
    /// overrides and silently relabel them. Capturing the snapshot in <see cref="SettingsResolver.Load"/> is what
    /// makes the second answer identical to the first.</summary>
    [Fact]
    public void Resolve_KeepsProvenanceStableAcrossRepeatedResolvesFromOneLoad()
    {
        var repo = NewRepo();
        WriteSettings(repo, $$"""{ "Output": {{Json(Path.Combine(repo, "saved-out"))}}, "ProjectName": "Saved" }""");
        var settings = new SiteSettings();

        var load = SettingsResolver.Load(settings, repo);
        var first = SettingsResolver.Resolve(load, settings, repo);
        var second = SettingsResolver.Resolve(load, settings, repo);

        Assert.Equal(first.Provenance, second.Provenance);
        Assert.Equal(ConfigSource.SavedSettings, OriginOf(second, SettingsResolver.Fields.Output));
        Assert.Equal(ConfigSource.SavedSettings, OriginOf(second, SettingsResolver.Fields.Project));
    }

    /// <summary>A genuine discovery failure must still reach the caller: <c>generate</c>/<c>watch</c> treat it as
    /// fatal, the menu catches it as a hint. The resolver does not decide which.</summary>
    [Fact]
    public void Resolve_PropagatesDiscoveryFailure()
    {
        var bare = Directory.CreateDirectory(Path.Combine(_root, $"bare-{++_next}")).FullName;

        Assert.Throws<DirectoryNotFoundException>(() => SettingsResolver.Resolve(new SiteSettings(), bare));
    }

    // --- Machine-parseable diagnostic (AC #2) ---

    [Fact]
    public void FormatConfigLines_EmitsOneGreppableLinePerFieldPlusTheSettingsFile()
    {
        var repo = NewRepo();
        WriteSettings(repo, """{ "ProjectName": "Diag" }""");

        var lines = SettingsResolver.FormatConfigLines(SettingsResolver.Resolve(new SiteSettings(), repo));

        Assert.All(lines, l => Assert.StartsWith(SettingsResolver.LinePrefix, l, StringComparison.Ordinal));
        Assert.All(lines, l => Assert.DoesNotContain('\n', l));
        Assert.Contains($"{SettingsResolver.LinePrefix} settings_file={Path.Combine(repo, SettingsStore.FileName)}", lines);
        Assert.Contains($"{SettingsResolver.LinePrefix} field=project origin=savedsettings value=Diag", lines);
        Assert.Contains(lines, l => l.StartsWith($"{SettingsResolver.LinePrefix} field=source origin=default value=", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatConfigLines_ReportsNoSettingsFileWhenNoneContributed()
    {
        var repo = NewRepo();

        var lines = SettingsResolver.FormatConfigLines(SettingsResolver.Resolve(new SiteSettings(), repo));

        Assert.Contains($"{SettingsResolver.LinePrefix} settings_file=(none)", lines);
    }

    /// <summary>Values go last precisely so a path containing spaces stays parseable: everything after
    /// <c>value=</c> is the value, with no quoting scheme to get wrong.</summary>
    [Fact]
    public void FormatConfigLines_KeepsAPathWithSpacesIntactAfterTheValueKey()
    {
        var repo = Directory.CreateDirectory(Path.Combine(_root, $"case-{++_next}", "my repo")).FullName;
        Directory.CreateDirectory(Path.Combine(repo, ForgeOptions.SourceDirName));

        var lines = SettingsResolver.FormatConfigLines(SettingsResolver.Resolve(new SiteSettings(), repo));
        var outputLine = Assert.Single(lines, l => l.StartsWith($"{SettingsResolver.LinePrefix} field=output ", StringComparison.Ordinal));

        const string ValueKey = " value=";
        var value = outputLine[(outputLine.IndexOf(ValueKey, StringComparison.Ordinal) + ValueKey.Length)..];
        Assert.Equal(Path.Combine(repo, ForgeOptions.OutputDirName), value);
    }

    /// <summary>The origin tokens are a published contract for CI scripts — pinned so an enum rename cannot change
    /// them silently.</summary>
    [Fact]
    public void OriginToken_UsesTheStableLowercaseSpelling()
    {
        Assert.Equal("commandline", SettingsResolver.OriginToken(ConfigSource.CommandLine));
        Assert.Equal("savedsettings", SettingsResolver.OriginToken(ConfigSource.SavedSettings));
        Assert.Equal("default", SettingsResolver.OriginToken(ConfigSource.Default));
    }

    [Fact]
    public void DisplayTag_NamesTheFlagTheFileOrAutoDiscovery()
    {
        Assert.Equal("--output", SettingsResolver.DisplayTag(
            new ConfigProvenance(SettingsResolver.Fields.Output, "--output", "x", ConfigSource.CommandLine)));
        Assert.Equal(SettingsStore.FileName, SettingsResolver.DisplayTag(
            new ConfigProvenance(SettingsResolver.Fields.Output, "--output", "x", ConfigSource.SavedSettings)));
        Assert.Equal("auto", SettingsResolver.DisplayTag(
            new ConfigProvenance(SettingsResolver.Fields.Output, "--output", "x", ConfigSource.Default)));
    }

    /// <summary>JSON-encodes a path so Windows backslashes survive into the settings-file fixtures.</summary>
    private static string Json(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
