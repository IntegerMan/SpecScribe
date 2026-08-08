using System.Text.Json;

namespace SpecScribe.Tests;

/// <summary>Makes Story 6.8's Workspace-Trust posture "present AND EFFECTIVE" rather than merely present.
/// [Story 17.2 Task 6, AC #1]
///
/// <para><b>What was already true, and why it still needed a test.</b> The extension declares
/// <c>untrustedWorkspaces.supported = "limited"</c> with <c>specscribe.toolPath</c> in
/// <c>restrictedConfigurations</c>. It contributes exactly two settings, and the execution-bearing one is the
/// restricted one — so coverage is COMPLETE, not partial. `restrictedConfigurations` is the declarative
/// mechanism VS Code enforces, and it is the correct one; the absence of a `workspace.isTrusted` check in the
/// source is fine. What was missing was EVIDENCE: nothing stopped a future contribution of a third,
/// execution-bearing setting from shipping unrestricted, silently.</para>
///
/// <para><b>Why this test lives in the C# suite.</b> <c>extension/</c> has no TypeScript test harness at all —
/// no test script, no runner, no test files — and standing one up is Story 17.4's cluster, not this story's.
/// The story file named three honest routes for this situation; this is the second of them ("assert
/// <c>package.json</c>'s shape from the C# suite"). Recorded rather than silently shipped unpinned.</para>
///
/// <para><b>The design that makes it a gate rather than a snapshot.</b> The test does not assert "there are
/// two settings" — that would be a change-detector that the next contributor edits without thinking. It
/// asserts that <b>every contributed setting is explicitly classified</b>, as either restricted-in-untrusted
/// or as a member of a named, justified safe list below. A new setting therefore fails until someone decides
/// which it is, which is exactly the decision that would otherwise be skipped.</para></summary>
public class WorkspaceTrustTests
{
    /// <summary>Settings deliberately allowed to apply in an UNTRUSTED workspace, each with the reason it
    /// cannot lead to code execution. Adding to this list is the conscious act the test exists to force.</summary>
    private static readonly Dictionary<string, string> SafeInUntrustedWorkspaces = new()
    {
        ["specscribe.openLocation"] = "Selects WHERE an already-resolved file is opened (editor group / preview). "
            + "Carries no path to an executable and cannot influence what is spawned.",
    };

    private static string ExtensionPackageJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "extension", "package.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "extension", "package.json");
    }

    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllText(ExtensionPackageJson()));

    [Fact]
    public void ExtensionDeclaresLimitedUntrustedWorkspaceSupport()
    {
        using var doc = Load();

        var caps = doc.RootElement.GetProperty("capabilities").GetProperty("untrustedWorkspaces");
        var supported = caps.GetProperty("supported").GetString();

        // "limited" (with restrictions) or false (no untrusted support at all) are both safe. "true" would
        // mean every setting applies in an untrusted workspace, including toolPath.
        Assert.True(supported is "limited" or "false",
            $"untrustedWorkspaces.supported is '{supported}'. 'true' would let a workspace-supplied "
            + "specscribe.toolPath point at an arbitrary executable in an UNTRUSTED workspace.");
    }

    [Fact]
    public void EveryContributedSettingIsClassifiedForUntrustedWorkspaces()
    {
        // THE GATE. A newly contributed setting must be either restricted or explicitly justified as safe.
        using var doc = Load();

        var restricted = doc.RootElement
            .GetProperty("capabilities").GetProperty("untrustedWorkspaces")
            .GetProperty("restrictedConfigurations")
            .EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);

        var contributed = ContributedSettingKeys(doc).ToList();
        Assert.NotEmpty(contributed);

        var unclassified = contributed
            .Where(k => !restricted.Contains(k) && !SafeInUntrustedWorkspaces.ContainsKey(k))
            .ToList();

        Assert.True(unclassified.Count == 0,
            "These extension settings are neither listed in `restrictedConfigurations` nor recorded as safe "
            + "in WorkspaceTrustTests.SafeInUntrustedWorkspaces:\n  "
            + string.Join("\n  ", unclassified)
            + "\n\nDecide which they are. If a setting can influence WHAT GETS EXECUTED (a path, an argument "
            + "prefix, an interpreter, a shell), it must be restricted — that is Story 6.8's posture and "
            + "Story 17.2 AC #1's 'present and effective'.");
    }

    [Fact]
    public void ToolPathRemainsRestricted()
    {
        // The one setting whose value becomes a spawned executable. Named explicitly so a refactor that
        // renames or drops it has to confront this test rather than quietly widening the surface.
        using var doc = Load();

        var restricted = doc.RootElement
            .GetProperty("capabilities").GetProperty("untrustedWorkspaces")
            .GetProperty("restrictedConfigurations")
            .EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Contains("specscribe.toolPath", restricted);
    }

    [Fact]
    public void EveryRestrictedConfigurationIsActuallyContributed()
    {
        // The inverse drift: a restriction naming a setting that no longer exists reads as protection while
        // protecting nothing.
        using var doc = Load();

        var contributed = ContributedSettingKeys(doc).ToHashSet(StringComparer.Ordinal);
        var restricted = doc.RootElement
            .GetProperty("capabilities").GetProperty("untrustedWorkspaces")
            .GetProperty("restrictedConfigurations")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

        var stale = restricted.Where(r => !contributed.Contains(r)).ToList();

        Assert.True(stale.Count == 0,
            "restrictedConfigurations names settings that are not contributed:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>The contributed setting keys. <c>contributes.configuration</c> is permitted by VS Code to be
    /// either a single object or an ARRAY of categories, and reading only the object form would silently
    /// return nothing the day someone splits the settings into groups — turning every assertion above green
    /// for the wrong reason.</summary>
    private static IEnumerable<string> ContributedSettingKeys(JsonDocument doc)
    {
        var configuration = doc.RootElement.GetProperty("contributes").GetProperty("configuration");

        var blocks = configuration.ValueKind == JsonValueKind.Array
            ? configuration.EnumerateArray()
            : new[] { configuration }.AsEnumerable();

        foreach (var block in blocks)
        {
            if (!block.TryGetProperty("properties", out var props)) continue;
            foreach (var p in props.EnumerateObject()) yield return p.Name;
        }
    }
}
