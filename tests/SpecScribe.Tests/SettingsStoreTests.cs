using SpecScribe;

namespace SpecScribe.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-settings-").FullName;
    private int _next;

    /// <summary>A fresh, isolated directory under this test class's temp root. Walk-up discovery means two tests
    /// sharing one directory would see each other's <c>.specscribe</c>, so each gets its own subtree.</summary>
    private string NewDir() => Directory.CreateDirectory(Path.Combine(_root, $"case-{++_next}")).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void ApplyTo_FillsUnsetValuesFromSavedSettings()
    {
        var saved = new SavedSettings { Source = "src", Adrs = "adrs", Output = "out", ProjectName = "Demo" };
        var settings = new SiteSettings();

        SettingsStore.ApplyTo(saved, settings);

        Assert.Equal("src", settings.Source);
        Assert.Equal("adrs", settings.Adrs);
        Assert.Equal("out", settings.Output);
        Assert.Equal("Demo", settings.ProjectName);
    }

    [Fact]
    public void ApplyTo_DoesNotOverrideExplicitCliValues()
    {
        var saved = new SavedSettings { Source = "saved-src", Output = "saved-out" };
        var settings = new SiteSettings { Source = "cli-src" };

        SettingsStore.ApplyTo(saved, settings);

        Assert.Equal("cli-src", settings.Source);   // CLI value preserved
        Assert.Equal("saved-out", settings.Output);  // unset value filled from saved
    }

    [Fact]
    public void IsEmpty_IsTrueWhenAllValuesNull()
    {
        Assert.True(new SavedSettings().IsEmpty);
    }

    [Fact]
    public void IsEmpty_IsFalseWhenAnyValueSet()
    {
        Assert.False(new SavedSettings { Output = "out" }.IsEmpty);
    }

    [Fact]
    public void IsEmpty_IsFalseWhenOnlyDeepGitSet()
    {
        // A config that persists just the deep-git opt-in is still worth writing. [Story 3.2]
        Assert.False(new SavedSettings { DeepGit = true }.IsEmpty);
    }

    [Fact]
    public void ApplyTo_RestoresPersistedDeepGitWhenCliDidNotRequestIt()
    {
        var saved = new SavedSettings { DeepGit = true };
        var settings = new SiteSettings(); // DeepGit defaults false -> "not requested this run"

        SettingsStore.ApplyTo(saved, settings);

        Assert.True(settings.DeepGit);
    }

    [Fact]
    public void ApplyTo_LeavesDeepGitOffWhenNeitherCliNorSavedEnabledIt()
    {
        SettingsStore.ApplyTo(new SavedSettings { Output = "out" }, new SiteSettings());
        // (no saved DeepGit) -> stays the default false
        var settings = new SiteSettings();
        SettingsStore.ApplyTo(new SavedSettings(), settings);
        Assert.False(settings.DeepGit);
    }

    [Fact]
    public void IsEmpty_IsFalseWhenOnlyCodeUrlSet()
    {
        // A config that persists just the code-link base URL is still worth writing. [Story 7.1]
        Assert.False(new SavedSettings { CodeUrl = "https://example.com" }.IsEmpty);
    }

    [Fact]
    public void ApplyTo_FillsCodeUrlFromSavedWhenCliDidNotProvideIt()
    {
        var saved = new SavedSettings { CodeUrl = "https://github.com/owner/repo/blob/main" };
        var settings = new SiteSettings();

        SettingsStore.ApplyTo(saved, settings);

        Assert.Equal("https://github.com/owner/repo/blob/main", settings.CodeUrl);
    }

    [Fact]
    public void ApplyTo_DoesNotOverrideExplicitCliCodeUrl()
    {
        var saved = new SavedSettings { CodeUrl = "https://saved.example/blob/main" };
        var settings = new SiteSettings { CodeUrl = "https://cli.example/blob/main" };

        SettingsStore.ApplyTo(saved, settings);

        Assert.Equal("https://cli.example/blob/main", settings.CodeUrl); // CLI wins over saved
    }

    [Fact]
    public void TrySaveThenTryLoad_RoundTripsCodeUrl()
    {
        var original = Directory.GetCurrentDirectory();
        var temp = Directory.CreateTempSubdirectory("specscribe-settings-").FullName;
        try
        {
            Directory.SetCurrentDirectory(temp);
            SettingsStore.TrySave(new SiteSettings { CodeUrl = "https://github.com/owner/repo/blob/main" });

            var loaded = SettingsStore.TryLoad();

            Assert.NotNull(loaded);
            Assert.Equal("https://github.com/owner/repo/blob/main", loaded!.CodeUrl);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { }
        }
    }

    // --- Story 5.2: directory-scoped (walk-up) discovery + README-inclusion parity ---

    /// <summary>A run started in a subdirectory must still see the repo-root `.specscribe` — the whole point of
    /// "directory-scoped" settings. Cwd-only anchoring (pre-5.2) silently missed it. [AC #1]</summary>
    [Fact]
    public void TryLoad_FindsSettingsFileInAnAncestorDirectory()
    {
        var repo = NewDir();
        var nested = Directory.CreateDirectory(Path.Combine(repo, "src", "deep", "nested")).FullName;
        File.WriteAllText(Path.Combine(repo, SettingsStore.FileName), """{ "Source": "from-root" }""");

        var loaded = SettingsStore.TryLoad(nested);

        Assert.NotNull(loaded);
        Assert.Equal("from-root", loaded!.Source);
    }

    /// <summary>The nearest file wins: a subdirectory that has its own `.specscribe` is not overridden by the
    /// repo root's. [AC #1]</summary>
    [Fact]
    public void TryLoad_PrefersTheNearestSettingsFile()
    {
        var repo = NewDir();
        var nested = Directory.CreateDirectory(Path.Combine(repo, "sub")).FullName;
        File.WriteAllText(Path.Combine(repo, SettingsStore.FileName), """{ "Source": "from-root" }""");
        File.WriteAllText(Path.Combine(nested, SettingsStore.FileName), """{ "Source": "from-sub" }""");

        Assert.Equal("from-sub", SettingsStore.TryLoad(nested)!.Source);
    }

    /// <summary>Read and write must be symmetric: a save from a subdirectory updates the ancestor file the load
    /// would have found, rather than stranding a second `.specscribe` in the subdirectory. [AC #1]</summary>
    [Fact]
    public void TrySave_WritesBackToTheAncestorFileThatWasFound()
    {
        var repo = NewDir();
        var nested = Directory.CreateDirectory(Path.Combine(repo, "sub")).FullName;
        var rootFile = Path.Combine(repo, SettingsStore.FileName);
        File.WriteAllText(rootFile, """{ "Source": "from-root" }""");

        var savedPath = SettingsStore.TrySave(new SiteSettings { Output = "out" }, nested);

        Assert.Equal(rootFile, savedPath);
        Assert.False(File.Exists(Path.Combine(nested, SettingsStore.FileName)));
    }

    /// <summary>With no ancestor file, the first save anchors at the start directory.</summary>
    [Fact]
    public void TrySave_AnchorsAtTheStartDirectoryOnFirstSave()
    {
        var repo = NewDir();

        var savedPath = SettingsStore.TrySave(new SiteSettings { Output = "out" }, repo);

        Assert.Equal(Path.Combine(repo, SettingsStore.FileName), savedPath);
    }

    [Fact]
    public void TrySaveThenTryLoad_RoundTripsIncludeReadme()
    {
        var repo = NewDir();
        SettingsStore.TrySave(new SiteSettings { NoReadme = true }, repo);

        var loaded = SettingsStore.TryLoad(repo);

        Assert.NotNull(loaded);
        Assert.False(loaded!.IncludeReadme);
    }

    /// <summary>Backward compatibility: a `.specscribe` written before the field existed still loads cleanly, with
    /// the new property simply absent (null = never configured). [AC #4]</summary>
    [Fact]
    public void TryLoad_AcceptsASettingsFileWrittenWithoutIncludeReadme()
    {
        var repo = NewDir();
        File.WriteAllText(
            Path.Combine(repo, SettingsStore.FileName),
            """{ "Source": "src", "Output": "out", "ProjectName": "Legacy" }""");

        var loaded = SettingsStore.TryLoad(repo);

        Assert.NotNull(loaded);
        Assert.Equal("Legacy", loaded!.ProjectName);
        Assert.Null(loaded.IncludeReadme);
    }

    /// <summary>Best-effort persistence (NFR2): a malformed file degrades to "no saved settings", never a crash.</summary>
    [Fact]
    public void TryLoad_ReturnsNullForMalformedJson()
    {
        var repo = NewDir();
        File.WriteAllText(Path.Combine(repo, SettingsStore.FileName), "{ not json at all");

        Assert.Null(SettingsStore.TryLoad(repo));
    }

    [Fact]
    public void IsEmpty_IsFalseWhenOnlyIncludeReadmeSet()
    {
        Assert.False(new SavedSettings { IncludeReadme = false }.IsEmpty);
    }

    [Fact]
    public void ApplyTo_RestoresPersistedReadmeExclusionWhenCliDidNotOptOut()
    {
        var settings = new SiteSettings(); // NoReadme defaults false -> "not requested this run"

        SettingsStore.ApplyTo(new SavedSettings { IncludeReadme = false }, settings);

        Assert.True(settings.NoReadme);
    }

    [Fact]
    public void ApplyTo_LeavesReadmeIncludedWhenSavedSaysInclude()
    {
        var settings = new SiteSettings();

        SettingsStore.ApplyTo(new SavedSettings { IncludeReadme = true }, settings);

        Assert.False(settings.NoReadme);
    }

    /// <summary>CLI wins: an explicit `--no-readme` is not undone by a saved "include". [AC #3]</summary>
    [Fact]
    public void ApplyTo_DoesNotOverrideAnExplicitNoReadme()
    {
        var settings = new SiteSettings { NoReadme = true };

        SettingsStore.ApplyTo(new SavedSettings { IncludeReadme = true }, settings);

        Assert.True(settings.NoReadme);
    }

    // ---- Story 5.5: the date-page "today" policy joins the persistence stack ----

    [Fact]
    public void IsEmpty_IsFalseWhenOnlyTodayPolicySet()
    {
        Assert.False(new SavedSettings { TodayPolicy = new DateCutoff(DatePolicy.Utc, null) }.IsEmpty);
    }

    /// <summary>Persist-only-the-non-default: a run at the default machine-local policy has nothing worth saving, so
    /// it must not turn an otherwise-empty config into a written file. [Story 5.5]</summary>
    [Fact]
    public void TrySave_DoesNotPersistTheDefaultMachineLocalPolicy()
    {
        var repo = NewDir();

        Assert.Null(SettingsStore.TrySave(new SiteSettings { TodayPolicy = "machine-local" }, repo));
        Assert.False(File.Exists(Path.Combine(repo, SettingsStore.FileName)));
    }

    [Fact]
    public void TrySaveThenTryLoad_RoundTripsANonDefaultPolicy()
    {
        var repo = NewDir();
        SettingsStore.TrySave(new SiteSettings { TodayPolicy = "utc" }, repo);

        var loaded = SettingsStore.TryLoad(repo);

        Assert.NotNull(loaded);
        Assert.Equal(new DateCutoff(DatePolicy.Utc, null), loaded!.TodayPolicy);
    }

    /// <summary>A forgiving CLI spelling is normalized to the canonical policy on save, so what round-trips through
    /// the file is exactly what the flag would parse. [Story 5.5]</summary>
    [Fact]
    public void TrySave_NormalizesAForgivingPolicySpelling()
    {
        var repo = NewDir();
        SettingsStore.TrySave(new SiteSettings { TodayPolicy = "last" }, repo);

        Assert.Equal(new DateCutoff(DatePolicy.LastCommit, null), SettingsStore.TryLoad(repo)!.TodayPolicy);
    }

    [Fact]
    public void ApplyTo_RestoresPersistedPolicyWhenCliDidNotProvideOne()
    {
        var settings = new SiteSettings(); // no --today-policy this run

        SettingsStore.ApplyTo(new SavedSettings { TodayPolicy = new DateCutoff(DatePolicy.LastCommit, null) }, settings);

        Assert.Equal(new DateCutoff(DatePolicy.LastCommit, null), settings.ResolveDateCutoff());
    }

    /// <summary>CLI wins: an explicit `--today-policy` is not overridden by a saved value. [Story 5.5 / AC #2]</summary>
    [Fact]
    public void ApplyTo_DoesNotOverrideAnExplicitCliPolicy()
    {
        var settings = new SiteSettings { TodayPolicy = "utc" };

        SettingsStore.ApplyTo(new SavedSettings { TodayPolicy = new DateCutoff(DatePolicy.LastCommit, null) }, settings);

        Assert.Equal(new DateCutoff(DatePolicy.Utc, null), settings.ResolveDateCutoff());
    }

    /// <summary>Backward compatibility: a `.specscribe` written before this field existed loads cleanly with the
    /// policy simply absent (null = never configured, keeps the default). [Story 5.5]</summary>
    [Fact]
    public void TryLoad_AcceptsASettingsFileWrittenWithoutTodayPolicy()
    {
        var repo = NewDir();
        File.WriteAllText(
            Path.Combine(repo, SettingsStore.FileName),
            """{ "Output": "out", "ProjectName": "Legacy" }""");

        var loaded = SettingsStore.TryLoad(repo);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.TodayPolicy);
    }

    /// <summary>TodayPolicy is the first enum-typed field in a hand-editable settings file, and a genuinely
    /// unrecognized token must not take the rest of the document down with it: it degrades that one field to "not
    /// configured" instead of failing JsonSerializer.Deserialize for the whole object and silently discarding
    /// Source/Output/every other saved field too. [Review][Patch — code review 2026-07-26]</summary>
    [Fact]
    public void TryLoad_ToleratesAnUnrecognizedTodayPolicyTokenWithoutLosingOtherFields()
    {
        var repo = NewDir();
        File.WriteAllText(
            Path.Combine(repo, SettingsStore.FileName),
            """{ "Source": "docs", "Output": "out", "TodayPolicy": "some-future-policy" }""");

        var loaded = SettingsStore.TryLoad(repo);

        Assert.NotNull(loaded);
        Assert.Equal("docs", loaded!.Source);
        Assert.Equal("out", loaded.Output);
        Assert.Null(loaded.TodayPolicy);
    }

    /// <summary>The CLI's own accepted spellings for `--today-policy` (case-insensitive, hyphenated) now also
    /// round-trip through the JSON field — reusing <see cref="DatePolicies.TryParse"/> instead of a bare
    /// enum-member-name match is strictly MORE forgiving, not just non-crashing, for the exact tokens a human is
    /// most likely to type by hand. [Review][Patch — code review 2026-07-26]</summary>
    [Fact]
    public void TryLoad_AcceptsTheCliSpellingOfTodayPolicyDirectlyInTheJsonField()
    {
        var repo = NewDir();
        File.WriteAllText(
            Path.Combine(repo, SettingsStore.FileName),
            """{ "TodayPolicy": "last-commit" }""");

        Assert.Equal(new DateCutoff(DatePolicy.LastCommit, null), SettingsStore.TryLoad(repo)!.TodayPolicy);
    }

    // ---- Story 5.7: the fixed --as-of date rides the SAME single persisted field ----

    /// <summary>The composite token is the whole point of D1: one field, one value, one round trip. A --as-of run
    /// persists <c>as-of:{iso}</c> and loads back as the same cutoff — through the folder form the settings file
    /// actually uses (ADR 0014), and through exactly the parser the command line uses. [Story 5.7 Task 6]</summary>
    [Fact]
    public void TrySaveThenTryLoad_RoundTripsThePinnedFixedDateThroughConfigJson()
    {
        var repo = NewDir();
        var savedPath = SettingsStore.TrySave(new SiteSettings { AsOf = "2026-07-27" }, repo);

        Assert.NotNull(savedPath);
        var json = File.ReadAllText(Path.Combine(savedPath!, SettingsStore.ConfigFileName));
        // Written as the TOKEN, not the record's ToString() — `DateCutoff { Policy = AsOf, ... }` would not parse.
        Assert.Contains("\"as-of:2026-07-27\"", json, StringComparison.Ordinal);
        Assert.Equal(new DateCutoff(DatePolicy.AsOf, new DateOnly(2026, 7, 27)), SettingsStore.TryLoad(repo)!.TodayPolicy);
    }

    /// <summary>A forgiving --as-of spelling is canonicalized to ISO on save, so what round-trips through the file
    /// is exactly what the flag would parse — the same normalization the policy tokens already get. [Story 5.7]</summary>
    [Fact]
    public void TrySave_CanonicalizesAForgivingAsOfDateToIso()
    {
        var repo = NewDir();
        SettingsStore.TrySave(new SiteSettings { AsOf = "07/27/2026" }, repo);

        Assert.Equal(new DateCutoff(DatePolicy.AsOf, new DateOnly(2026, 7, 27)), SettingsStore.TryLoad(repo)!.TodayPolicy);
    }

    /// <summary>Story 5.7 switched the converter's WRITE side from the enum member name to the canonical token, so a
    /// `.specscribe` written by ANY earlier version — holding "Utc"/"LastCommit" — must still load. Read and write
    /// are now symmetric on one vocabulary, but the old vocabulary was never dropped. [Story 5.7 Task 2]</summary>
    [Theory]
    [InlineData("Utc", DatePolicy.Utc)]
    [InlineData("LastCommit", DatePolicy.LastCommit)]
    public void TryLoad_StillAcceptsALegacyEnumMemberNameToken(string legacy, DatePolicy expected)
    {
        var repo = NewDir();
        File.WriteAllText(Path.Combine(repo, SettingsStore.FileName), $$"""{ "TodayPolicy": "{{legacy}}" }""");

        Assert.Equal(new DateCutoff(expected, null), SettingsStore.TryLoad(repo)!.TodayPolicy);
    }

    /// <summary>The defect this whole converter exists to prevent, re-proved for the new argument-bearing token: a
    /// malformed <c>as-of:</c> value degrades that ONE field to "not configured" and every sibling setting survives.
    /// Retyping the field to <c>string?</c> and dropping the converter would pass an unvalidated token through
    /// <see cref="SettingsStore.ApplyTo"/> into <see cref="SiteSettings.ResolveDateCutoff"/>'s throw, converting a
    /// silent-and-safe degrade into a hard failure — which is why the converter was EXTENDED. [Story 5.7 AC #2]</summary>
    [Fact]
    public void TryLoad_ToleratesAnUnrecognizedAsOfTokenWithoutLosingAnySiblingSetting()
    {
        var repo = NewDir();
        File.WriteAllText(
            Path.Combine(repo, SettingsStore.FileName),
            """
            {
              "Source": "docs",
              "Adrs": "docs/adrs",
              "Output": "out",
              "ProjectName": "Legacy",
              "DeepGit": true,
              "CodeUrl": "https://example.com/repo/blob/main",
              "IncludeReadme": false,
              "TodayPolicy": "as-of:nope"
            }
            """);

        var loaded = SettingsStore.TryLoad(repo);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.TodayPolicy);
        Assert.Equal("docs", loaded.Source);
        Assert.Equal("docs/adrs", loaded.Adrs);
        Assert.Equal("out", loaded.Output);
        Assert.Equal("Legacy", loaded.ProjectName);
        Assert.True(loaded.DeepGit);
        Assert.Equal("https://example.com/repo/blob/main", loaded.CodeUrl);
        Assert.False(loaded.IncludeReadme);
    }

    /// <summary>CLI wins, via the --as-of half of ApplyTo's guard. Without it a saved "utc" would be restored on top
    /// of an explicit --as-of and manufacture exactly the disagreement ResolveDateCutoff rejects — turning a valid
    /// command line into an error because of a file the user never mentioned. [Story 5.7 Task 2]</summary>
    [Fact]
    public void ApplyTo_DoesNotOverrideAnExplicitAsOfWithASavedPolicy()
    {
        var settings = new SiteSettings { AsOf = "2026-07-27" };

        SettingsStore.ApplyTo(new SavedSettings { TodayPolicy = new DateCutoff(DatePolicy.Utc, null) }, settings);

        Assert.Null(settings.TodayPolicy);
        Assert.Equal(new DateCutoff(DatePolicy.AsOf, new DateOnly(2026, 7, 27)), settings.ResolveDateCutoff());
    }

    // ---- ADR 0014: `.specscribe` is a folder containing config.json, not a flat file ----

    private static string WriteFolderSettings(string dir, string json)
    {
        var folder = Directory.CreateDirectory(Path.Combine(dir, SettingsStore.FileName)).FullName;
        File.WriteAllText(Path.Combine(folder, SettingsStore.ConfigFileName), json);
        return folder;
    }

    [Fact]
    public void TryLoad_ReadsTheFolderFormat()
    {
        var repo = NewDir();
        WriteFolderSettings(repo, """{ "Source": "from-folder" }""");

        var loaded = SettingsStore.TryLoad(repo);

        Assert.NotNull(loaded);
        Assert.Equal("from-folder", loaded!.Source);
    }

    [Fact]
    public void TryLoad_ReturnsNullForMalformedJsonInTheFolderForm()
    {
        var repo = NewDir();
        WriteFolderSettings(repo, "{ not json at all");

        Assert.Null(SettingsStore.TryLoad(repo));
    }

    [Fact]
    public void TrySave_WritesTheFolderFormWithConfigJsonInside()
    {
        var repo = NewDir();

        var savedPath = SettingsStore.TrySave(new SiteSettings { Output = "out" }, repo);

        Assert.NotNull(savedPath);
        Assert.True(Directory.Exists(savedPath));
        Assert.True(File.Exists(Path.Combine(savedPath!, SettingsStore.ConfigFileName)));
    }

    /// <summary>A save must not leave a flat file and a folder both claiming the same name — the flat file a
    /// pre-ADR-0014 version wrote is replaced by the folder form the next time settings are saved from it.</summary>
    [Fact]
    public void TrySave_MigratesALegacyFlatFileToTheFolderForm()
    {
        var repo = NewDir();
        var legacyFile = Path.Combine(repo, SettingsStore.FileName);
        File.WriteAllText(legacyFile, """{ "Source": "from-legacy-file" }""");

        var savedPath = SettingsStore.TrySave(new SiteSettings { Output = "out" }, repo);

        Assert.Equal(legacyFile, savedPath);
        Assert.True(Directory.Exists(savedPath));
        Assert.True(File.Exists(Path.Combine(savedPath!, SettingsStore.ConfigFileName)));
        var loaded = SettingsStore.TryLoad(repo);
        Assert.NotNull(loaded);
        Assert.Equal("out", loaded!.Output);
    }

    // ---- [Review][Patch]: a malformed nearest `.specscribe` must not shadow a valid ancestor ----

    /// <summary>A broken subdirectory `.specscribe` must not blind a run to a perfectly good repo-root one — the
    /// walk-up continues past the malformed candidate instead of stopping at the first (invalid) match.</summary>
    [Fact]
    public void TryLoad_SkipsAMalformedNearestFileAndFallsBackToAValidAncestor()
    {
        var repo = NewDir();
        var nested = Directory.CreateDirectory(Path.Combine(repo, "sub")).FullName;
        File.WriteAllText(Path.Combine(repo, SettingsStore.FileName), """{ "Source": "from-root" }""");
        File.WriteAllText(Path.Combine(nested, SettingsStore.FileName), "{ not json at all");

        var loaded = SettingsStore.TryLoad(nested);

        Assert.NotNull(loaded);
        Assert.Equal("from-root", loaded!.Source);
    }

    /// <summary>The out-param overload reports the ancestor that actually supplied the data, not the nearer
    /// malformed candidate that was skipped — so a "loaded from" display or `--show-config`'s `settings_file=`
    /// line never names a file that didn't actually load.</summary>
    [Fact]
    public void TryLoad_ReportsTheAncestorPathWhenTheNearestCandidateWasSkipped()
    {
        var repo = NewDir();
        var nested = Directory.CreateDirectory(Path.Combine(repo, "sub")).FullName;
        var rootFile = Path.Combine(repo, SettingsStore.FileName);
        File.WriteAllText(rootFile, """{ "Source": "from-root" }""");
        File.WriteAllText(Path.Combine(nested, SettingsStore.FileName), "{ not json at all");

        SettingsStore.TryLoad(nested, out var loadedFrom);

        Assert.Equal(rootFile, loadedFrom);
    }
}
