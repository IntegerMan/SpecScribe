using System.Text.RegularExpressions;
using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>ADR 0037 — `specscribe config`, the non-interactive door onto `.specscribe`, and the settings form it
/// renders for the VS Code extension.
///
/// <para><b>Why the coverage sits here rather than in the extension.</b> `extension/` has no test project, so the
/// only automated gate on that side is `tsc --noEmit`. The logic is therefore deliberately kept in C# — the save
/// semantics, the field vocabulary, the form's markup — and tested here. What genuinely cannot be covered this way
/// (the panel, the message channel, the folder picker) is manual `F5`, exactly as ADR 0005 §"Not yet proven"
/// already records for every host-runtime path.</para></summary>
public class ConfigCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("specscribe-config-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    private string ConfigFile => Path.Combine(_root, SettingsStore.FileName, SettingsStore.ConfigFileName);

    private SavedSettings? ReadBack()
    {
        if (!File.Exists(ConfigFile)) return null;
        return JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(ConfigFile), new JsonSerializerOptions());
    }

    // ===== TrySaveExplicit: the writer the form drives ===========================================================

    [Fact]
    public void TrySaveExplicit_WritesTheDocumentVerbatim_WithoutFoldingInAutoDiscoveredValues()
    {
        // The whole reason this exists beside TrySave. TrySave runs Capture over the MERGED live settings, which
        // would promote an auto-discovered value into an explicitly-saved one — the CodeUrl trap ConfigurePaths
        // sidesteps by hand. A caller that has computed exactly which fields the user set must be able to write
        // that and only that.
        var saved = new SavedSettings { ProjectName = "Chosen" };

        var path = SettingsStore.TrySaveExplicit(saved, _root);

        Assert.NotNull(path);
        var readBack = ReadBack();
        Assert.Equal("Chosen", readBack!.ProjectName);
        Assert.Null(readBack.Source);
        Assert.Null(readBack.Output);
        Assert.Null(readBack.DeepGit);
    }

    [Fact]
    public void TrySaveExplicit_WritesAnEmptyDocument_WhereTrySaveWouldWriteNothing()
    {
        // Divergence #1, and it is required by the form: clearing every field back to its default IS something the
        // user asked for. TrySave's IsEmpty guard exists to avoid creating a file nobody wanted, which is a
        // different situation. TryReadCandidate already reads `{}` as "no saved settings", so this is honest.
        var path = SettingsStore.TrySaveExplicit(new SavedSettings(), _root);

        Assert.NotNull(path);
        Assert.True(File.Exists(ConfigFile));
        Assert.True(ReadBack()!.IsEmpty);
        // …and for contrast, the same input through TrySave writes nothing at all.
        Assert.Null(SettingsStore.TrySave(new SiteSettings(), Path.Combine(_root, "elsewhere")));
    }

    [Fact]
    public void TrySaveExplicit_NeverDeletesTheContainerFolder()
    {
        // Divergence #2. ADR 0014 made `.specscribe` a FOLDER precisely so other per-directory state could live
        // beside the config; removing it because the config went empty would take that with it.
        var sibling = Path.Combine(_root, SettingsStore.FileName, "sibling-state.json");
        SettingsStore.TrySaveExplicit(new SavedSettings { ProjectName = "x" }, _root);
        File.WriteAllText(sibling, "{}");

        SettingsStore.TrySaveExplicit(new SavedSettings(), _root);

        Assert.True(File.Exists(sibling), "an unrelated file in the container survives an emptying save");
    }

    [Fact]
    public void TrySaveExplicit_MigratesALegacyFlatFileToTheFolderForm()
    {
        // The ADR 0014 migration must not be re-implemented per writer — this is the same rule TrySave follows,
        // asserted on the new path so the two cannot drift.
        var legacy = Path.Combine(_root, SettingsStore.FileName);
        File.WriteAllText(legacy, "{\"ProjectName\":\"old\"}");

        SettingsStore.TrySaveExplicit(new SavedSettings { ProjectName = "new" }, _root);

        Assert.True(Directory.Exists(legacy), "the entry is now a folder");
        Assert.Equal("new", ReadBack()!.ProjectName);
    }

    // ===== The field vocabulary =================================================================================

    [Fact]
    public void ClearableFields_AreExactlyTheKeysShowConfigPrints()
    {
        // One vocabulary across `--show-config`, `--clear`, the `--json` payload and the form's `data-field`
        // attributes. A field the resolver reports but `--clear` cannot unset would be a setting the form could set
        // and never take back.
        var reported = typeof(SettingsResolver.Fields)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(f => (string)f.GetValue(null)!)
            .Order(StringComparer.Ordinal);

        Assert.Equal(reported, ConfigCommand.ClearableFields);
    }

    // ===== --json: what populates the form ======================================================================

    [Fact]
    public void SerializeConfig_CarriesEveryFieldWithItsProvenanceAndSavedState()
    {
        SettingsStore.TrySaveExplicit(new SavedSettings { ProjectName = "Saved Name" }, _root);
        var settings = new SiteSettings { Source = Path.Combine(_root, "_bmad-output") };
        Directory.CreateDirectory(settings.Source!);
        // ⚠️ ONE load, reused. Load() merges the saved document onto `settings` in place, so resolving through a
        // SECOND load would capture those merged values as CLI overrides and report every saved field as having
        // come from the command line. `ConfigCommand` had exactly that bug; this test is what found it.
        var load = SettingsResolver.Load(settings, _root);
        var resolved = SettingsResolver.ResolveTolerant(load, settings, _root);

        using var doc = JsonDocument.Parse(ConfigCommand.SerializeConfig(resolved, load));
        var fields = doc.RootElement.GetProperty("fields").EnumerateArray().ToList();

        Assert.Equal(8, fields.Count);
        foreach (var name in new[] { "field", "option", "effective", "source", "saved" })
        {
            Assert.True(fields[0].TryGetProperty(name, out _), $"entry carries camelCase `{name}`");
        }

        // `saved` and `source` answer DIFFERENT questions, and the form needs both: `source` is what is in effect
        // this run, `saved` is whether the document pins it — i.e. whether "Inherit default" is selected.
        var project = fields.Single(f => f.GetProperty("field").GetString() == "project");
        Assert.True(project.GetProperty("saved").GetBoolean());
        Assert.Equal(nameof(ConfigSource.SavedSettings), project.GetProperty("source").GetString());

        var source = fields.Single(f => f.GetProperty("field").GetString() == "source");
        Assert.False(source.GetProperty("saved").GetBoolean());
        Assert.Equal(nameof(ConfigSource.CommandLine), source.GetProperty("source").GetString());
    }

    // ===== The form document ====================================================================================

    private string RenderForm()
    {
        var settings = new SiteSettings();
        var load = SettingsResolver.Load(settings, _root);
        return SettingsFormTemplater.RenderDocument(
            SettingsResolver.ResolveTolerant(load, settings, _root), load);
    }

    [Fact]
    public void RenderDocument_EmitsNoFormElement_BecauseTheCspBlocksSubmission()
    {
        // A correctness requirement, not a style choice: the webview CSP carries `form-action 'none'`, so a submit
        // is blocked outright, and `script-src 'nonce-…'` blocks an inline onsubmit. A <form> here would look
        // right and do nothing.
        var html = RenderForm();

        Assert.DoesNotContain("<form ", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form>", html, StringComparison.Ordinal);
        Assert.Contains("form-action 'none'", html, StringComparison.Ordinal);
        // The submit path is a plain button the nonce'd bridge listens on.
        Assert.Contains("id=\"ss-form-save\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDocument_SharesOneCspStringWithThePortalDocument()
    {
        // Two webview documents, one policy. A second copy is how one of them quietly becomes the weaker one.
        Assert.Contains(WebviewRenderAdapter.CspPolicy, RenderForm(), StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDocument_LeavesTheTwoHostPlaceholdersForTheShim()
    {
        // The same two-value seam the portal document uses (ADR 0005 §1), so the shim's job is identical for both
        // surfaces and it still renders nothing.
        var html = RenderForm();

        Assert.Contains("__NONCE__", html, StringComparison.Ordinal);
        Assert.Contains("__CSP_SOURCE__", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDocument_RendersOneRowPerPersistedField_KeyedOnTheSharedVocabulary()
    {
        var html = RenderForm();

        foreach (var field in ConfigCommand.ClearableFields)
        {
            Assert.Contains($"data-field=\"{field}\"", html, StringComparison.Ordinal);
        }
        // Only the three path fields get a folder picker.
        Assert.Contains("data-browse=\"source\"", html, StringComparison.Ordinal);
        Assert.Contains("data-browse=\"adrs\"", html, StringComparison.Ordinal);
        Assert.Contains("data-browse=\"output\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-browse=\"project\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDocument_DeepGitOffersOnlyInheritAndOn_BecauseTheCoreCannotHonourAPinnedOff()
    {
        // ⚠️ The ADR 0037 open item, pinned so it cannot be "fixed" into a control that lies. ApplyTo reads
        // `saved.DeepGit == true` only — there is no --no-deep-git for a persisted false to suppress — so a pinned
        // "Off" would be a choice the core silently ignores. IncludeReadme reads `== false` and has no such gap,
        // which is why it DOES offer all three.
        var html = RenderForm();

        var deepGit = Section(html, "deep_git");
        Assert.Contains(">On</option>", deepGit, StringComparison.Ordinal);
        Assert.Contains("Inherit default", deepGit, StringComparison.Ordinal);
        Assert.DoesNotContain(">Off</option>", deepGit, StringComparison.Ordinal);

        var readme = Section(html, "readme");
        Assert.Contains(">Yes</option>", readme, StringComparison.Ordinal);
        Assert.Contains(">No</option>", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDocument_TheFixedDatePolicyOptionCarriesTheAsOfPrefix_NotADegradedToken()
    {
        // Regression guard for a defect found in a live browser. DatePolicies.Token DEGRADES a dateless AsOf to the
        // machine-local token — correct behaviour, since a token must never claim a pin the run did not use — so
        // building this option from Token() gave "fixed date" the value `machine-local`: a duplicate of another
        // option, and one the bridge's `as-of` test could never match, so selecting it never revealed the companion
        // date input. The option looked right and did nothing, which no unit test was asking about.
        var options = Regex.Matches(Section(RenderForm(), "today_policy"), @"<option value=""([^""]*)""[^>]*>([^<]*)</option>")
            .Select(m => (Value: m.Groups[1].Value, Text: m.Groups[2].Value))
            .ToList();

        var fixedDate = Assert.Single(options, o => o.Text.StartsWith("fixed date", StringComparison.Ordinal));
        Assert.Equal(DatePolicies.AsOfTokenPrefix, fixedDate.Value);
        // Every option value is distinct — the property the degrade broke.
        Assert.Equal(options.Count, options.Select(o => o.Value).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void RenderDocument_APinnedDateIsRenderedIntoTheCompanionDateInput()
    {
        SettingsStore.TrySaveExplicit(
            new SavedSettings { TodayPolicy = new DateCutoff(DatePolicy.AsOf, new DateOnly(2026, 7, 27)) }, _root);

        var section = Section(RenderForm(), "today_policy");

        Assert.Contains($"value=\"{DatePolicies.AsOfTokenPrefix}\" selected", section, StringComparison.Ordinal);
        Assert.Contains("type=\"date\" id=\"ss-f-today_policy-date\" value=\"2026-07-27\"", section, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDocument_AnUnsavedFieldRendersBlankRatherThanPrefillingTheResolvedValue()
    {
        // Pre-filling would be the CodeUrl trap in general form: pressing Save would silently promote an
        // auto-discovered path into an explicit, frozen one. The resolved value is shown in the provenance line
        // instead — informative, but not submitted.
        var html = RenderForm();
        var source = Section(html, "source");

        Assert.Contains("value=\"\"", source, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Inherit default\"", source, StringComparison.Ordinal);
        // …and the resolved value is still visible, as text.
        Assert.Contains("In effect:", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderDocument_ASavedFieldRendersItsValueAndSaysWhereItCameFrom()
    {
        SettingsStore.TrySaveExplicit(new SavedSettings { ProjectName = "Pinned Name" }, _root);

        var project = Section(RenderForm(), "project");

        Assert.Contains("value=\"Pinned Name\"", project, StringComparison.Ordinal);
        // Provenance as a WORD, never color alone (UX-DR17).
        Assert.Contains($"from {SettingsStore.FileName}", project, StringComparison.Ordinal);
    }

    /// <summary>One row's markup, sliced on its <c>data-field</c> marker so an assertion about one control cannot
    /// accidentally be satisfied by a different row.</summary>
    private static string Section(string html, string field)
    {
        var start = html.IndexOf($"data-field=\"{field}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"the form renders a row for `{field}`");
        var end = html.IndexOf("</div>\n<div class=\"ss-form-row\"", start, StringComparison.Ordinal);
        return end > start ? html[start..end] : html[start..];
    }
}
