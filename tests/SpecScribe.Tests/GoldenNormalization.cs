using System.Text.RegularExpressions;

namespace SpecScribe.Tests;

/// <summary>The ONE volatile-token fold the byte-comparison gates share.
///
/// <para>Its consumers must not be able to disagree. <b>Story 17.1 correction:</b> this paragraph named
/// <c>GoldenContentFingerprint</c> in <c>SiteGeneratorAdapterTests</c> as one of two dependent gates — that gate
/// was RETIRED with its subject by ADR 0034 (Story 23.6) and only its tombstone comment remains. The live
/// consumers are the Story 22.5 oracle-diff harness in <see cref="IncrementalOracleParityTests"/> (which pins
/// incremental output AGAINST full-generation output) and the per-file snapshot in
/// <c>TestArtifactDiscoveryTests</c>, which Story 17.1 routed onto <see cref="StripFooterClock"/> rather than
/// leaving it on its own transcription. Story 22.5 AC #5 states the rule plainly: <i>"a second
/// copy that folds one extra token is a hole in the gate"</i> — a fold present here but missing there would
/// let a real staleness class read as noise, and a fold present there but missing here would make the oracle
/// harness red on unrelated per-run churn.</para>
///
/// <para>This is not a hypothetical: the Story 22.1 spike carried its own transcription of these regexes and
/// it had already drifted — its <c>BuildRow</c> pattern was the pre-Story-5.2 <c>[^&lt;]*</c> form, which
/// <see cref="FoldToday"/> silently defeats (see <see cref="BuildRow"/>'s remarks). The spike is quarantined
/// so that cost nothing; a second copy inside the suite would not be so harmless.</para>
///
/// <para>Only per-run / per-build / per-machine noise is folded, never artifact content — so the constants
/// these gates pin stay portable across machines and CI rather than pinned to one box.</para></summary>
internal static class GoldenNormalization
{
    /// <summary>The wall-clock generation stamp in the page footer — 24-hour time plus a machine-local
    /// UTC-offset zone label, so it varies per run AND per time zone. [spec-7-3-10-4 widened this]</summary>
    private static readonly Regex FooterClock = new(
        @"on [A-Za-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}", RegexOptions.Compiled);

    /// <summary>Folds the footer clock for the tests that need ONLY that token rather than the whole
    /// normalization pass — a per-file snapshot, or an A/B comparison of two runs of the same page.
    ///
    /// <para>They choose their own replacement text (it appears in their failure diffs), but they must not
    /// choose their own PATTERN: this class's whole premise is that a second transcription drifts. Story 17.1
    /// found two such transcriptions — <c>TestArtifactDiscoveryTests</c> and a <c>StripFooterClock</c> local in
    /// <c>SiteGeneratorStatusStylesTests</c> — and routed both here.</para></summary>
    internal static string StripFooterClock(string content, string replacement) =>
        FooterClock.Replace(content, replacement);

    /// <summary>The asset cache-bust token, derived from the assembly's ModuleVersionId — new on every build.</summary>
    private static readonly Regex AssetCacheBust = new(@"\?v=[0-9a-fA-F]+", RegexOptions.Compiled);

    private static readonly Regex SubtitleVersion = new(@"SpecScribe v[^<]+", RegexOptions.Compiled);
    private static readonly Regex VersionRow = new(@"(<dt>Version</dt><dd>)[^<]*(</dd>)", RegexOptions.Compiled);

    /// <summary>The About page's dynamic build identifier (build date · short commit hash), which varies per
    /// build and per commit.
    /// <para><c>.*?</c> rather than <c>[^&lt;]*</c>: <see cref="FoldToday"/> runs FIRST and rewrites the build
    /// date to the <c>&lt;date-iso&gt;</c> PLACEHOLDER, whose leading <c>&lt;</c> the negated class cannot
    /// cross — so the pattern silently stopped matching its own row and let the short commit hash through into
    /// the hash. That made the golden constant drift on every commit: captured pre-commit (when the row still
    /// showed the PREVIOUS sha), then failing the moment the work landed, which reads as a rendering regression
    /// and invites a needless regeneration. [Story 5.2; golden-diff-normalization-gotchas]</para></summary>
    private static readonly Regex BuildRow = new(@"(<dt>Build</dt><dd>).*?(</dd>)", RegexOptions.Compiled);

    /// <summary>Folds today's date (the ISO filename/href form and the readable heading form) to stable
    /// placeholders. Story 7.3's artifact-mtime date page + timeline are stamped with the generation date, so
    /// without this the gates would drift day to day with no rendering change behind it.</summary>
    public static string FoldToday(string s)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return s.Replace(Charts.DReadable(today), "<date-readable>").Replace(Charts.D(today), "<date-iso>");
    }

    /// <summary>Folds every volatile token in one rendered output file.
    ///
    /// <para><paramref name="rootsToFold"/> are absolute filesystem roots that leak into rendered markup and
    /// must be neutralized. <c>diagnostics.html</c> prints the configured OUTPUT root and the repo root
    /// verbatim, which makes it the one page whose bytes are machine- AND output-path dependent (Story 22.2
    /// ran this down; Story 22.5 Trap 5). Every root folds to the SAME <c>&lt;root&gt;</c> placeholder, which
    /// is what lets the oracle harness compare two trees generated into two DIFFERENT output directories
    /// without <c>diagnostics.html</c> diffing on every single case — the false alarm Story 22.2 hit with its
    /// own two-output-dir harness. Both forward-slash and native forms are folded because the page emits
    /// whichever the platform produced.</para></summary>
    public static string NormalizeVolatile(string content, params string[] rootsToFold)
    {
        content = content.Replace("\r\n", "\n");
        content = FoldToday(content);
        foreach (var root in rootsToFold)
        {
            if (string.IsNullOrEmpty(root)) continue;
            content = content.Replace(PathUtil.NormalizeSlashes(root), "<root>").Replace(root, "<root>");
        }
        content = FooterClock.Replace(content, "on <ts>");
        content = AssetCacheBust.Replace(content, "?v=<ver>");
        content = SubtitleVersion.Replace(content, "SpecScribe v<ver>");
        content = VersionRow.Replace(content, "$1<ver>$2");
        content = BuildRow.Replace(content, "$1<build>$2");
        return content;
    }
}
