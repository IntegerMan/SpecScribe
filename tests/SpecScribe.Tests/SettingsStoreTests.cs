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
}
