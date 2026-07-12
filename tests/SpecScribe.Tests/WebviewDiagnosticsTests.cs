using System.Text.Json;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.12 coverage for the pure <see cref="WebviewCommand.SerializeDiagnostics"/> projection — the
/// JSON-lines stderr contract the VS Code shim parses into the Problems panel. Exercised without a spawn, mirroring
/// <see cref="WebviewCommandTests"/>: build notices + resolved options, assert the emitted wire shape. The
/// coherence guarantee (the wire mirrors the diagnostics page's <see cref="DiagnosticNotice.FromEvents"/> set) is
/// pinned separately against a real generation fixture, so the two surfaces can never disagree (AC #2).</summary>
public class WebviewDiagnosticsTests
{
    private static ForgeOptions Options()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "specscribe-diag-tests");
        return new ForgeOptions
        {
            RepoRoot = repoRoot,
            SourceRoot = Path.Combine(repoRoot, "_bmad-output"),
            AdrSourceRoot = Path.Combine(repoRoot, "docs", "adrs"),
            AdrSourceExplicit = false,
            OutputRoot = Path.Combine(repoRoot, ForgeOptions.OutputDirName),
            SiteTitle = "SpecScribe",
            IncludeReadme = false,
            DeepGitAnalytics = false,
        };
    }

    /// <summary>Splits the newline-terminated payload into its parsed JSON objects (one per notice).</summary>
    private static List<JsonElement> Lines(string payload) =>
        payload.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToList();

    [Fact]
    public void SerializeDiagnostics_SourceAnchoredNotice_EmitsRepoRelativeForwardSlashedAnchoredLine()
    {
        // An ingest notice (SourceAnchored) becomes a repo-relative path the shim joins to the workspace folder,
        // carrying the fine category in the message and fileAnchored: true.
        var notices = new[]
        {
            new DiagnosticNotice(
                "Unsupported", "implementation-artifacts/sprint-status.yaml",
                "no development_status map", DiagnosticSeverity.Warning, DiagnosticAnchorRoot.Source),
        };

        var line = Assert.Single(Lines(WebviewCommand.SerializeDiagnostics(notices, Options())));

        Assert.Equal("_bmad-output/implementation-artifacts/sprint-status.yaml", line.GetProperty("path").GetString());
        Assert.DoesNotContain('\\', line.GetProperty("path").GetString()!); // never a backslash, even on Windows
        Assert.Equal("warning", line.GetProperty("severity").GetString());
        Assert.True(line.GetProperty("fileAnchored").GetBoolean());
        // The message is self-describing: category prefix + detail.
        Assert.Equal("Unsupported: no development_status map", line.GetProperty("message").GetString());
    }

    [Fact]
    public void SerializeDiagnostics_RenderTimeNotice_KeepsOutputRelativePath_NotAnchored()
    {
        // A render-time notice carries an output-relative .html path verbatim and is NOT file-anchored — it rides
        // the wire for page-coherence but the shim leaves it on the diagnostics page.
        var notices = new[]
        {
            new DiagnosticNotice(
                "Error", "deep-analytics.html", "template blew up", DiagnosticSeverity.Error, DiagnosticAnchorRoot.None),
        };

        var line = Assert.Single(Lines(WebviewCommand.SerializeDiagnostics(notices, Options())));

        Assert.Equal("deep-analytics.html", line.GetProperty("path").GetString());
        Assert.False(line.GetProperty("fileAnchored").GetBoolean());
        Assert.Equal("error", line.GetProperty("severity").GetString());
        Assert.Equal("Error: template blew up", line.GetProperty("message").GetString());
    }

    [Fact]
    public void SerializeDiagnostics_AdrAnchoredNotice_ResolvesAgainstAdrSourceRoot_NotSourceRoot()
    {
        // The unnumbered-ADR notice (SiteGenerator.GenerateAdrsInternal) carries a SourcePath prefixed with the
        // ADR OUTPUT subdir ("adrs/…"), relative to AdrSourceRoot — NOT SourceRoot. Combining it with SourceRoot
        // (the pre-patch bug) resolves to a nonexistent file; this pins the correct AdrSourceRoot-relative join.
        var notices = new[]
        {
            new DiagnosticNotice(
                "Unsupported", "adrs/0007-foo.md",
                "no ADR number derivable from the filename; record rendered unnumbered and sorted last",
                DiagnosticSeverity.Warning, DiagnosticAnchorRoot.Adr),
        };

        var line = Assert.Single(Lines(WebviewCommand.SerializeDiagnostics(notices, Options())));

        // AdrSourceRoot in Options() is "<repoRoot>/docs/adrs" — the resolved path must land there, not under
        // "_bmad-output/adrs/…" (which is what combining with SourceRoot would produce).
        Assert.Equal("docs/adrs/0007-foo.md", line.GetProperty("path").GetString());
        Assert.True(line.GetProperty("fileAnchored").GetBoolean());
    }

    [Fact]
    public void SerializeDiagnostics_MapsSeverity_ErrorAndWarning()
    {
        var notices = new[]
        {
            new DiagnosticNotice("Malformed", "a.md", "bad", DiagnosticSeverity.Error, DiagnosticAnchorRoot.Source),
            new DiagnosticNotice("Skipped", "b.md", "later", DiagnosticSeverity.Warning, DiagnosticAnchorRoot.Source),
        };

        var lines = Lines(WebviewCommand.SerializeDiagnostics(notices, Options()));

        Assert.Equal(2, lines.Count);
        Assert.Equal("error", lines[0].GetProperty("severity").GetString());
        Assert.Equal("warning", lines[1].GetProperty("severity").GetString());
    }

    [Fact]
    public void SerializeDiagnostics_NoNotices_ReturnsEmptyString()
    {
        // Degrade-clean: a clean run emits nothing at all (no Problems noise, no blank line).
        Assert.Equal(string.Empty, WebviewCommand.SerializeDiagnostics(Array.Empty<DiagnosticNotice>(), Options()));
    }

    [Fact]
    public void SerializeDiagnostics_NullMessage_FallsBackToCategoryWord_NoNullInJson()
    {
        // A bare skip can carry no Message — the entry falls back to the category word so it still reads, and the
        // JSON never contains a literal null message.
        var notices = new[]
        {
            new DiagnosticNotice("Skipped", "c.md", null, DiagnosticSeverity.Warning, DiagnosticAnchorRoot.Source),
        };

        var line = Assert.Single(Lines(WebviewCommand.SerializeDiagnostics(notices, Options())));

        Assert.Equal("Skipped", line.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.String, line.GetProperty("message").ValueKind); // not JsonValueKind.Null
    }
}
