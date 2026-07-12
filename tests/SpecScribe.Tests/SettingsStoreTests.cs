using SpecScribe;

namespace SpecScribe.Tests;

public class SettingsStoreTests
{
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
}
