using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Which BMad methodology module a source repo was produced with. Drives the workflow-command
/// suggestions and the well-known planning docs surfaced in nav.
/// <para><see cref="Unknown"/> and <see cref="Unmodeled"/> are deliberately NOT the same state, and merging
/// them would regress a surface that is already correct: <see cref="DiagnosticsTemplater"/>'s "Detected
/// framework" row prints "Unknown (not detected)" for <see cref="Unknown"/> and the module's real parsed
/// label otherwise — so routing a recognized-but-unmodeled module through <see cref="Unknown"/> would replace
/// a true label with a false "not detected". [Story 18.2; ADR 0015 Decision 2a]</para></summary>
public enum BmadModule
{
    /// <summary>Detection failed — no <c>_bmad/</c> install, or nothing that parsed. Carries no label.</summary>
    Unknown,

    BmadMethod,

    GameDevStudio,

    /// <summary>A real, recognized BMad module that SpecScribe does not model: it keeps its parsed
    /// <see cref="CommandCatalog"/> and its real <see cref="CommandCatalog.ModuleLabel"/>, and publishes no
    /// docs and no glossary. Open-world by necessity — BMad Builder mints modules with arbitrary codes, so no
    /// closed enumeration of module codes can ever be complete. [ADR 0015 Decision 2]</summary>
    Unmodeled,
}

/// <summary>A well-known planning document a module publishes, matched by filename anywhere in the source
/// tree. <see cref="InNav"/> docs also appear in the top nav; every discovered doc appears in the
/// dashboard quick links regardless.</summary>
public sealed record ModuleDoc(string FileName, string Label, string Description, bool InNav);

/// <summary>One portal-vocabulary entry a module publishes. <see cref="Term"/> is the token as it appears
/// in prose ("FR", "ADR", "spec kernel"); <see cref="Expansion"/> is the <c>&lt;abbr title&gt;</c> text
/// ("Functional Requirement"); <see cref="Definition"/> is the one-line gloss shown on the how-to-read
/// page. <see cref="IsAcronym"/>-true entries (short, all-caps acronyms) drive the in-page
/// <see cref="AbbreviationExpander"/>; longer terms appear only in the glossary list.</summary>
public sealed record GlossaryTerm(string Term, string Expansion, string Definition, bool IsAcronym);

/// <summary>The workflow slash-commands a module exposes, parsed from its <c>module-help.csv</c> so the
/// "Next Steps" panels show the commands that actually exist (<c>/bmad-*</c> for BMad Method, <c>/gds-*</c>
/// for Game Dev Studio) rather than a hard-coded set. Keyed by skill base-name — the skill id minus its
/// module prefix, e.g. <c>create-story</c>.</summary>
public sealed class CommandCatalog
{
    private readonly IReadOnlyDictionary<string, string> _byStep;

    public CommandCatalog(string moduleLabel, IReadOnlyDictionary<string, string> byStep, bool usesPhaseArguments = false)
    {
        ModuleLabel = moduleLabel;
        _byStep = byStep;
        UsesPhaseArguments = usesPhaseArguments;
    }

    /// <summary>Human label for the module (e.g. "BMad Method"), parsed from module-help.csv. Kept as
    /// module metadata for callers that want to name the detected methodology; the "Next Steps" heading no
    /// longer renders it. EMPTY means "no label" — never a name; see <see cref="HasLabel"/>.</summary>
    public string ModuleLabel { get; }

    /// <summary>Whether phase-targeted workflow steps use framework-native phase identifiers instead of the
    /// shared synthetic epic/story ids.</summary>
    public bool UsesPhaseArguments { get; }

    /// <summary>True when this catalog carries a real module label from a parsed <c>module-help.csv</c>. Every
    /// surface that NAMES the detected module must gate on this: <see cref="Empty"/> is the instance behind
    /// <see cref="ModuleContext.None"/>, so a label-blind surface would name a module on a repo that has no
    /// <c>_bmad/</c> at all. [Story 18.2; ADR 0015 Decision 2b]</summary>
    public bool HasLabel => ModuleLabel.Length > 0;

    public bool IsEmpty => _byStep.Count == 0;

    /// <summary>Fallback used when no module could be detected — every lookup misses, so callers omit the
    /// command panels entirely rather than print commands that don't exist.
    /// <para>Its label is deliberately EMPTY, not "BMad". This instance is <see cref="ModuleContext.None"/>'s
    /// catalog, so a placeholder name here is indistinguishable from a real detected label at exactly the
    /// surfaces that name the module — a repo with no BMad install would have announced "This project uses
    /// the BMad module", a worse false claim than saying nothing. [ADR 0015 Decision 2b]</para></summary>
    public static readonly CommandCatalog Empty = new(string.Empty, new Dictionary<string, string>());

    /// <summary>The slash command for a workflow step (e.g. <c>create-story</c> -> <c>/bmad-create-story</c>),
    /// optionally with an argument appended. Returns null when the module doesn't expose that step, so
    /// callers skip the suggestion instead of printing a command that isn't installed.</summary>
    public string? Command(string step, string? argument = null)
    {
        if (!_byStep.TryGetValue(step, out var command))
        {
            return null;
        }

        return argument is { Length: > 0 } ? $"{command} {argument}" : command;
    }
}

/// <summary>The detected methodology module for a source repo: its command catalog and its well-known
/// planning docs. Detection reads the installed-module registry (<c>_bmad/_config/manifest.yaml</c>) and
/// the chosen module's <c>module-help.csv</c>, so command prefixes and available workflows come from data
/// rather than being hard-coded to any one module.</summary>
public sealed class ModuleContext
{
    public required BmadModule Module { get; init; }
    public required CommandCatalog Commands { get; init; }
    public required IReadOnlyList<ModuleDoc> Docs { get; init; }
    public required IReadOnlyList<GlossaryTerm> Glossary { get; init; }

    /// <summary>The detected module's CODE — the <c>_bmad/{code}/</c> install-directory name (<c>bmm</c>,
    /// <c>gds</c>, <c>tea</c>, or any code BMad Builder minted), stored lower-invariant. This, not the
    /// skill-id prefix, is what <see cref="Module"/> is derived from: every first-party module except GDS
    /// prefixes its skills <c>bmad-</c>, so the prefix identifies nothing. Null only on <see cref="None"/> —
    /// i.e. when no module was detected at all. [Story 18.2; ADR 0015 Decisions 1, 1b]</summary>
    public string? Code { get; init; }

    /// <summary>True when a real module was detected but SpecScribe publishes no docs or glossary for it —
    /// an open-world outcome, not a failure: <see cref="Commands"/> and <see cref="CommandCatalog.ModuleLabel"/>
    /// are populated, <see cref="Docs"/> and <see cref="Glossary"/> are deliberately empty. Distinct from
    /// <see cref="None"/> (nothing detected) — surfaces that NAME the detected module need exactly that
    /// distinction (NFR8). [Story 18.2; ADR 0015 Decision 2]</summary>
    public bool IsUnmodeled => Module == BmadModule.Unmodeled;

    /// <summary>True when the primary module is one SpecScribe actually models, and therefore the only state
    /// in which a surface may present module-specific vocabulary. Gates the how-to-read command legend, which
    /// otherwise renders for an unmodeled module too. [ADR 0015 Decision 2c]</summary>
    public bool IsModeled => Module is BmadModule.BmadMethod or BmadModule.GameDevStudio;

    public static readonly ModuleContext None = new()
    {
        Module = BmadModule.Unknown,
        Commands = CommandCatalog.Empty,
        Docs = Array.Empty<ModuleDoc>(),
        Glossary = Array.Empty<GlossaryTerm>(),
    };

    /// <summary>Well-known BMad Method planning-doc filenames, matched anywhere in the source tree (folder
    /// layout varies; Epic 4 will generalize). The single source of truth for these names — <see cref="SiteNav"/>
    /// (top nav / quick links) and the home index's PRD-prominent planning grouping both classify against these
    /// constants rather than re-hard-coding the strings. The quality-review rubric is a PRD <em>companion</em>
    /// (folded under the PRD, not a peer doc), so it lives here too but stays out of <see cref="BmadMethodDocs"/>.</summary>
    public static class WellKnownDocs
    {
        public const string Prd = "prd.md";
        public const string ArchitectureSpine = "ARCHITECTURE-SPINE.md";
        public const string Brief = "brief.md";
        public const string UxDesign = "DESIGN.md";
        public const string UxExperience = "EXPERIENCE.md";

        /// <summary>The PRD's quality-review rubric — a companion of <see cref="Prd"/>, deliberately absent from
        /// the nav/quick-link doc list so it never reads as a co-equal planning document. [Story 2.4 Task 4]</summary>
        public const string PrdReviewRubric = "review-rubric.md";
    }

    // BMad Method publishes its planning artifacts in nested folders (prds/, briefs/, ux-designs/) plus a
    // spec architecture spine; they're matched by filename anywhere in the source tree. PRD + Architecture
    // ride the top nav; the brief and UX docs surface in the dashboard quick links to keep the nav lean.
    private static readonly IReadOnlyList<ModuleDoc> BmadMethodDocs = new[]
    {
        new ModuleDoc(WellKnownDocs.Prd, "PRD", "Read the product requirements.", InNav: true),
        new ModuleDoc(WellKnownDocs.ArchitectureSpine, "Architecture", "Inspect the architecture spine.", InNav: true),
        new ModuleDoc(WellKnownDocs.Brief, "Product Brief", "Review the product brief.", InNav: false),
        new ModuleDoc(WellKnownDocs.UxDesign, "UX Design", "Inspect the UX design system.", InNav: false),
        new ModuleDoc(WellKnownDocs.UxExperience, "UX Experience", "Inspect UX behavior and flows.", InNav: false),
    };

    private static readonly IReadOnlyList<ModuleDoc> GameDevStudioDocs = new[]
    {
        new ModuleDoc("gdd.md", "GDD", "Open the game design baseline.", InNav: true),
        new ModuleDoc("narrative-design.md", "Narrative", "Inspect narrative design artifacts.", InNav: true),
        new ModuleDoc("game-architecture.md", "Game Architecture", "Inspect source-derived architecture notes.", InNav: true),
    };

    /// <summary>The well-known planning docs a module publishes.</summary>
    public static IReadOnlyList<ModuleDoc> DocsFor(BmadModule module) => module switch
    {
        BmadModule.BmadMethod => BmadMethodDocs,
        BmadModule.GameDevStudio => GameDevStudioDocs,
        _ => Array.Empty<ModuleDoc>(),
    };

    // The portal vocabulary a module publishes: acronyms that expand in-page on first use, plus longer
    // terms that only appear in the how-to-read glossary. This is the single source of BMAD vocabulary —
    // shared rendering (AbbreviationExpander, HowToReadTemplater) holds zero acronym literals of its own.
    private static readonly IReadOnlyList<GlossaryTerm> BmadMethodGlossary = new[]
    {
        new GlossaryTerm("FR", "Functional Requirement", "A specific capability the system must provide.", IsAcronym: true),
        new GlossaryTerm("NFR", "Non-Functional Requirement", "A quality attribute the system must meet, such as performance or accessibility.", IsAcronym: true),
        new GlossaryTerm("AC", "Acceptance Criterion", "A testable condition that defines when a story is complete.", IsAcronym: true),
        new GlossaryTerm("ADR", "Architecture Decision Record", "A record of a significant architecture decision and its rationale.", IsAcronym: true),
        new GlossaryTerm("PRD", "Product Requirements Document", "The document defining what the product should do and why.", IsAcronym: true),
        new GlossaryTerm("spec kernel", "spec kernel", "The distilled, preservation-validated machine contract used for downstream work.", IsAcronym: false),
        new GlossaryTerm("quick-dev", "quick-dev", "A lightweight implementation workflow for small changes outside the full story pipeline.", IsAcronym: false),
        new GlossaryTerm("epic", "epic", "A grouping of related stories that together deliver a larger capability.", IsAcronym: false),
        new GlossaryTerm("story", "story", "A single unit of implementable work with its own acceptance criteria.", IsAcronym: false),
        new GlossaryTerm("sprint", "sprint", "The current slice of stories being actively developed.", IsAcronym: false),
    };

    private static readonly IReadOnlyList<GlossaryTerm> GameDevStudioGlossary = new[]
    {
        new GlossaryTerm("GDD", "Game Design Document", "The document defining the game's core design baseline.", IsAcronym: true),
        new GlossaryTerm("narrative beat", "narrative beat", "A discrete story moment or event in the narrative design.", IsAcronym: false),
        new GlossaryTerm("game architecture", "game architecture", "Source-derived architecture notes for the game codebase.", IsAcronym: false),
    };

    /// <summary>The portal vocabulary a module publishes. Unknown/undetected modules publish nothing, so
    /// the glossary section and the abbreviation expander both degrade to absent (NFR8).</summary>
    public static IReadOnlyList<GlossaryTerm> GlossaryFor(BmadModule module) => module switch
    {
        BmadModule.BmadMethod => BmadMethodGlossary,
        BmadModule.GameDevStudio => GameDevStudioGlossary,
        _ => Array.Empty<GlossaryTerm>(),
    };

    private static readonly Regex ManifestModulePattern = TimedRegex.New(
        @"^\s*-\s*name:\s*(?<name>[A-Za-z0-9_-]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>BMad Method's module code — its <c>_bmad/bmm/</c> install-directory name. The single home for
    /// this literal: identity, primary-module ranking, and the presence check all key on it. [Story 18.2]</summary>
    public const string BmmCode = "bmm";

    /// <summary>Game Dev Studio's module code. BMad's own docs advertise its commands as <c>/bmgd-*</c>, but
    /// its real <c>module-help.csv</c> uses <c>gds-*</c> and its <c>module.yaml</c> says <c>code: gds</c> —
    /// BMGD is branding, and <c>gds</c> is what is on disk. [Story 18.2]</summary>
    public const string GdsCode = "gds";

    /// <summary>Independent presence check: true when the BMad Method module is installed (manifest entry
    /// or on-disk <c>module-help.csv</c>). Does NOT rely on the single-winner <see cref="Detect"/> — a
    /// dual-install repo reports both Method and GDS as Present simultaneously. [SDD help page]</summary>
    public static bool IsMethodPresent(string repoRoot) => IsModulePresent(repoRoot, BmmCode);

    /// <summary>Independent presence check: true when the Game Dev Studio module is installed (manifest
    /// entry or on-disk <c>module-help.csv</c>). Does NOT rely on the single-winner <see cref="Detect"/>
    /// — a dual-install repo reports both Method and GDS as Present simultaneously. [SDD help page]</summary>
    public static bool IsGdsPresent(string repoRoot) => IsModulePresent(repoRoot, GdsCode);

    /// <summary>The file whose presence makes a <c>_bmad/</c> child a module candidate, and the source of its
    /// label and command catalog. The ONE home for this literal.</summary>
    private const string ModuleHelpFileName = "module-help.csv";

    /// <summary><c>_bmad/</c> children that are never modules whatever they contain. Since Story 18.2 the rule
    /// is open-world — "the directory name IS the module code" — which without this guard would GUARANTEE
    /// acceptance of <c>_bmad/scripts/</c> as a module the instant anything dropped a <c>module-help.csv</c>
    /// there. Any name beginning with <c>_</c> is reserved too (this repo's own <c>_bmad/</c> holds
    /// <c>_config/</c>). A reserved name carrying the file is skipped SILENTLY — it is not an error.
    /// [ADR 0015 Decision 1a]</summary>
    private static readonly IReadOnlySet<string> ReservedModuleNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "core", "custom", "scripts" };

    private static bool IsReservedModuleName(string name) =>
        name.Length == 0 || name[0] == '_' || ReservedModuleNames.Contains(name);

    /// <summary>The label each modeled code is expected to declare in its CSV's <c>module</c> column. Nothing
    /// stops a BMad Builder-minted module installing at <c>_bmad/gds/</c>; without this cross-check it would
    /// silently inherit Game Dev Studio's docs and glossary. [ADR 0015 Decision 1c]</summary>
    private static readonly IReadOnlyDictionary<string, string> ModeledModuleLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [BmmCode] = "BMad Method",
            [GdsCode] = "Game Dev Studio",
        };

    /// <summary>True when <paramref name="moduleName"/> is listed in the installed-module manifest
    /// <em>or</em> its <c>module-help.csv</c> exists on disk (OR semantics). Never throws.
    /// <para>The disk half resolves the directory by case-insensitive ENUMERATION rather than by constructing
    /// <c>{bmadRoot}/{code}/module-help.csv</c>: path construction is case-sensitive on Linux, so
    /// <c>_bmad/BMM/</c> used to report <c>IsMethodPresent == false</c> while <see cref="Detect"/>'s own scan
    /// still found it — two answers to the same question on the same repo. [ADR 0015 Decision 1b]</para>
    /// <para>PUBLIC since Story 18.5: module-artifact coverage must gate on the module being INSTALLED rather
    /// than on filenames alone (a repo with a coincidental <c>test-review.md</c> and no <c>_bmad/tea/</c> must
    /// produce nothing), and open-world identity means there is no <see cref="BmadModule"/> case to add an
    /// <c>Is…Present</c> wrapper for. Callers pass the module CODE — the same <c>_bmad/{code}/</c> directory name
    /// <see cref="Code"/> carries. Kept as the ONE presence check rather than a second one written beside it.
    /// [Story 18.5 Task 4; ADR 0015 Decisions 1, 1b, 2]</para></summary>
    public static bool IsModulePresent(string repoRoot, string moduleName)
    {
        try
        {
            var bmadRoot = Path.Combine(repoRoot, "_bmad");
            if (!Directory.Exists(bmadRoot)) return false;

            var installed = ReadInstalledModules(bmadRoot);
            if (installed.Any(n => string.Equals(n, moduleName, StringComparison.OrdinalIgnoreCase)))
                return true;

            return FindModuleCsv(bmadRoot, moduleName) is not null;
        }
        catch { return false; }
    }

    /// <summary>The context for ONE NAMED module, or null when that module is not installed or its catalog will
    /// not parse. This is NOT detection: <see cref="Detect"/> ranks every installed candidate and returns the
    /// single PRIMARY module, and calling it twice is the bug Story 18.2 closed. This answers a different
    /// question — "what does module <paramref name="code"/> itself declare?" — which a surface covering a
    /// SECONDARY module's artifacts has to ask.
    /// <para>Story 18.5's motivating case: in a BMM+TEA repo the primary module is BMad Method, so naming the
    /// Test Artifacts surface from the primary catalog's <see cref="CommandCatalog.ModuleLabel"/> would label
    /// Test Architect's own artifacts "BMad Method" — a silent misattribution of exactly the kind ADR 0015 exists
    /// to prevent. It also keeps AC #2's command vocabulary honest: a TEA command must come from TEA's own parsed
    /// CSV through <see cref="CommandCatalog.Command"/>, never a literal.</para> [Story 18.5; ADR 0015 Decision 1]</summary>
    public static ModuleContext? ForCode(string repoRoot, string code)
    {
        try
        {
            var bmadRoot = Path.Combine(repoRoot, "_bmad");
            if (!Directory.Exists(bmadRoot)) return null;
            if (IsReservedModuleName(code)) return null;

            return FindModuleCsv(bmadRoot, code) is { } csv ? BuildContext(csv) : null;
        }
        catch { return null; }
    }

    /// <summary>The <c>module-help.csv</c> of the install directory whose name matches <paramref name="code"/>
    /// case-insensitively, or null. Enumeration-based on purpose — see <see cref="IsModulePresent"/>.</summary>
    private static string? FindModuleCsv(string bmadRoot, string code)
    {
        foreach (var dir in SafeEnumerateDirectories(bmadRoot))
        {
            if (!string.Equals(Path.GetFileName(dir), code, StringComparison.OrdinalIgnoreCase)) continue;
            var csv = Path.Combine(dir, ModuleHelpFileName);
            if (File.Exists(csv)) return csv;
        }
        return null;
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try { return Directory.EnumerateDirectories(root).ToList(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    /// <summary>Detects the primary methodology module for a repo from the module CODE of every install it can
    /// see, and uses source-artifact shape only to break the tie between the two modeled modules. Never throws
    /// — an undetectable module yields <see cref="None"/>, which degrades to nav without module docs and no
    /// command panels.</summary>
    /// <param name="diagnostics">Optional sink, matching the <c>IngestSprint(options, diagnostics)</c>
    /// convention the sibling ingest paths already follow. Detection used to swallow everything in a bare
    /// <c>catch { return None; }</c>, so it had no way to REPORT the open-world outcomes Story 18.2 introduces
    /// — an unmodeled primary, a module skipped in favour of a higher-ranked one, or a candidate whose CSV
    /// would not parse. Callers with no event stream to merge into (tests, incremental nav rebuilds) omit it.
    /// [ADR 0015 Decision 2d]</param>
    public static ModuleContext Detect(
        string repoRoot, IReadOnlyList<string> sourceRelativePaths, List<AdapterDiagnostic>? diagnostics = null)
    {
        // Notices are buffered here and flushed to the caller ONLY once detection succeeds. Detection used to
        // append straight into the caller's list, so the catch-all below could return None — rendering
        // "Detected framework: Unknown (not detected)" — while that same list already carried notices naming a
        // real module code and label: a diagnostics page that contradicted itself. [Review][Patch P5]
        var pending = new List<AdapterDiagnostic>();
        try
        {
            var bmadRoot = Path.Combine(repoRoot, "_bmad");
            if (!Directory.Exists(bmadRoot))
            {
                return None;
            }

            var ranked = RankCandidates(DiscoverCandidates(bmadRoot), sourceRelativePaths);
            if (ranked.Count == 0)
            {
                return None;
            }

            // Descend the rank. A candidate that won't parse is REPORTED and skipped, and the rank of the
            // remaining candidates is untouched — so a lower-ranked module never inherits the primary slot
            // merely because a higher-ranked one was unreadable. [ADR 0015 Decision 4d]
            //
            // A candidate DEMOTED by the Decision-1c label cross-check is skipped the same way, and for the
            // same reason: ranking is computed from codes BEFORE any label is parsed, so a BMB-minted module
            // squatting `_bmad/gds/` outranks a genuine `_bmad/bmm/` whenever the source tree carries a game
            // hint. Accepting the squatter as primary would demote a MODELED module below an auxiliary one —
            // Defect B's exact symptom through a different door, and a violation of AC #2's first clause. The
            // demoted context is kept as a LAST-RESORT fallback so a repo whose only install is a squatter
            // still gets a context (its real label and catalog) rather than None.
            // [Review][Patch P10; ADR 0015 Decisions 4d + 1c, as amended]
            ModuleContext? primary = null;
            var chosenIndex = -1;
            ModuleContext? demoted = null;
            var demotedIndex = -1;
            for (var i = 0; i < ranked.Count; i++)
            {
                CandidateContext? candidate;
                try
                {
                    candidate = BuildCandidate(ranked[i], pending);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A CSV deleted or exclusively locked between FindModuleCsv's File.Exists and the read —
                    // a live watch-session race. Without this the throw escaped to the catch-all and discarded
                    // every lower-ranked, perfectly parseable module, so Decision 4d's guarantee held only for
                    // BuildContext returning null, never for it throwing. [Review][Patch P5]
                    candidate = null;
                }

                if (candidate is null)
                {
                    pending.Add(new AdapterDiagnostic(
                        AdapterDiagnosticCategory.Malformed, RepoRelativeCsv(DirNameOf(ranked[i])),
                        $"module help catalog could not be parsed; '{CodeOf(ranked[i])}' is skipped as a module candidate",
                        DiagnosticAnchorRoot.Repo));
                    continue;
                }

                if (candidate.Value.Demoted)
                {
                    // The Unsupported notice naming both labels was already emitted by BuildCandidate.
                    if (demoted is null)
                    {
                        demoted = candidate.Value.Context;
                        demotedIndex = i;
                    }

                    continue;
                }

                primary = candidate.Value.Context;
                chosenIndex = i;
                break;
            }

            if (primary is null)
            {
                if (demoted is null)
                {
                    return None;
                }

                primary = demoted;
                chosenIndex = demotedIndex;
            }

            ReportSecondaryModules(ranked, chosenIndex, primary, pending);
            ReportUnmodeledPrimary(primary, ranked[chosenIndex], pending);
            diagnostics?.AddRange(pending);
            return primary;
        }
        catch (Exception)
        {
            // Detection is best-effort: any failure (IO, permissions, malformed data) degrades to None
            // rather than aborting the whole site build. It is REPORTED rather than silent, though — an
            // operator whose `_bmad/` is unreadable used to get a portal indistinguishable from one with no
            // BMad install at all, and nothing to act on. Unsupported (not Malformed) keeps this new emission
            // path off the Error mapping: nothing about the site is wrong, one input could not be read.
            // [Review][Patch P5]
            diagnostics?.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported, "_bmad/",
                "the installed BMad module set could not be read, so no methodology module was detected; "
                + "module docs, glossary and command suggestions are omitted",
                DiagnosticAnchorRoot.Repo));
            return None;
        }
    }

    /// <summary>The installed module set: the UNION of the manifest's entries and the <c>_bmad/</c> directories
    /// that carry a <c>module-help.csv</c>, minus reserved names, deduped by code.
    /// <para>The disk scan used to fire only when the manifest yielded ZERO candidates, so a manifest listing
    /// <c>bmm</c> beside an installed <c>_bmad/tea/</c> never saw TEA — while <see cref="IsModulePresent"/>'s OR
    /// semantics reported TEA present. The disk scan therefore stops being a fallback.
    /// [ADR 0015 Decision 1d]</para>
    /// <para><b>This closes ONE direction, not both.</b> The candidate set is
    /// <c>(manifest ∪ disk) ∩ has-csv</c> while <see cref="IsModulePresent"/> is <c>manifest OR disk-csv</c>,
    /// so a manifest entry whose <c>module-help.csv</c> is not on disk still reports present while
    /// contributing no candidate — see <c>IsMethodPresent_TrueWhenManifestListsBmmWithoutCsv</c>, which pins
    /// exactly that. Decision 1d's "those two must not disagree" is therefore an overstatement of what the
    /// union achieved; the residual divergence is recorded as a known state rather than fixed here, because
    /// <see cref="IsModulePresent"/>'s presence contract is depended on by Story 18.5's artifact gating.
    /// [Review][Patch P11]</para></summary>
    private static List<string> DiscoverCandidates(string bmadRoot)
    {
        var byCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Consider(string name)
        {
            if (IsReservedModuleName(name) || byCode.ContainsKey(name)) return;
            if (FindModuleCsv(bmadRoot, name) is { } csv) byCode[name] = csv;
        }

        // The manifest read is guarded the way SafeEnumerateDirectories already is. It used to be enumerated
        // eagerly and unwrapped, so a `_bmad/_config/manifest.yaml` that was exclusively locked, permission-
        // denied, or mid-write — i.e. during a BMad install — threw straight past the whole union to Detect's
        // catch-all, yielding None even with `_bmad/bmm/module-help.csv` sitting on disk. The disk half is the
        // more reliable signal of the two; losing it to a manifest problem is the wrong failure.
        // [Review][Patch P5]
        foreach (var name in SafeReadInstalledModules(bmadRoot)) Consider(name);
        foreach (var dir in SafeEnumerateDirectories(bmadRoot)) Consider(Path.GetFileName(dir));

        return byCode.Values.ToList();
    }

    /// <summary>One <see cref="AdapterDiagnosticCategory.Informational"/> notice recording the installed modules
    /// that did NOT become the primary — so a multi-module repo can see why its second module's docs and
    /// commands are absent instead of guessing. [ADR 0015 Decision 4e, as amended]
    /// <para>Only candidates ranked BELOW the winner are listed. The set used to be "every index except the
    /// winner", which swept in the higher-ranked candidates that had just failed to parse — so an unparseable
    /// <c>bmm</c> beside a valid <c>tea</c> produced a <c>Malformed</c> notice saying bmm is unreadable AND
    /// this one saying bmm merely lost a ranking. The second was false and told the reader the ranking worked
    /// as designed. A candidate demoted by the label cross-check is excluded for the same reason: its
    /// <c>Unsupported</c> notice already explains, more specifically, why it is not the primary.
    /// [Review][Patch P3]</para>
    /// <para><see cref="AdapterDiagnosticCategory.Informational"/> rather than
    /// <see cref="AdapterDiagnosticCategory.Skipped"/>: this fires at ONE non-primary module, which is the
    /// ordinary healthy BMM+TEA install, and <c>Skipped</c> renders at Warning severity — a clean repo must not
    /// show a warning for being correctly configured. The threshold stays at one because that explanation is
    /// exactly what a BMM+TEA user needs. [Review][Patch P13; owner call D5, 2026-07-27]</para></summary>
    private static void ReportSecondaryModules(
        List<string> ranked, int chosenIndex, ModuleContext primary, List<AdapterDiagnostic>? diagnostics)
    {
        if (diagnostics is null) return;

        var others = ranked.Where((_, i) => i > chosenIndex).Select(CodeOf).ToList();
        if (others.Count == 0) return;

        // The clause is conditional because an UNMODELED primary publishes no planning docs and no glossary —
        // asserting they "come from" it would contradict ReportUnmodeledPrimary's notice emitted moments later
        // on the same page. [Review][Patch P3]
        var provenance = primary.IsModeled
            ? $"planning docs, glossary and workflow commands come from '{primary.Code}'."
            : $"workflow commands come from '{primary.Code}', which publishes no planning docs or glossary.";

        diagnostics.Add(new AdapterDiagnostic(
            AdapterDiagnosticCategory.Informational, RepoRelativeCsv(DirNameOf(ranked[chosenIndex])),
            $"{others.Count} other installed BMad module(s) ({string.Join(", ", others)}) are not the primary; "
            + provenance,
            DiagnosticAnchorRoot.Repo));
    }

    /// <summary>The one <see cref="AdapterDiagnosticCategory.Informational"/> "FYI, nothing to do" notice for a
    /// detected-but-unmodeled primary — the reported half of NFR8's honest absence, replacing what used to be a
    /// silent misattribution to BMad Method. Anchored on the winning candidate's REAL directory name rather
    /// than its lower-invariant code, so the path names a file that exists on a case-sensitive filesystem.
    /// [ADR 0015 Decision 2d; Review][Patch P6]</summary>
    private static void ReportUnmodeledPrimary(
        ModuleContext primary, string csvPath, List<AdapterDiagnostic>? diagnostics)
    {
        if (diagnostics is null || !primary.IsUnmodeled) return;

        diagnostics.Add(new AdapterDiagnostic(
            AdapterDiagnosticCategory.Informational, RepoRelativeCsv(DirNameOf(csvPath)),
            $"Detected BMad module '{primary.Code}' ({primary.Commands.ModuleLabel}); SpecScribe has no "
            + "module-specific docs or glossary for it, so those sections are omitted.",
            DiagnosticAnchorRoot.Repo));
    }

    /// <summary>A module diagnostic's subject, anchored at the REPO root (not the source root every other
    /// adapter diagnostic uses) — <c>_bmad/</c> is a sibling of the source tree, so a source-anchored path
    /// would resolve to a file that does not exist. <c>internal</c> rather than private so a module diagnostic
    /// raised OUTSIDE this class (Story 18.6's Planning Artifacts panel-omission notice, emitted from
    /// <c>SiteGenerator</c>) reuses the one place this path shape is written down, instead of re-literalizing
    /// <c>_bmad/{code}/module-help.csv</c> as a second source of truth. [ADR 0015 Decision 2d, anchor root]</summary>
    internal static string RepoRelativeCsv(string code) => $"_bmad/{code}/{ModuleHelpFileName}";

    private static IReadOnlyList<string> SafeReadInstalledModules(string bmadRoot)
    {
        try { return ReadInstalledModules(bmadRoot); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static IReadOnlyList<string> ReadInstalledModules(string bmadRoot)
    {
        var manifestPath = Path.Combine(bmadRoot, "_config", "manifest.yaml");
        if (!File.Exists(manifestPath))
        {
            return Array.Empty<string>();
        }

        var text = MarkdownConverter.ReadAllTextShared(manifestPath);
        return ManifestModulePattern.Matches(text).Select(m => m.Groups["name"].Value).ToList();
    }

    /// <summary>Orders the installed modules by explicit RANK, best first: the game-shape hint's GDS, then
    /// <see cref="BmmCode"/>, then <see cref="GdsCode"/>, then every other code ordinal by code.
    /// <para>Selection used to be "first non-GDS candidate" — i.e. installed-manifest order — which demoted a
    /// genuine BMM install the moment a sibling module (TEA, CIS, a BMB-minted one) happened to be listed
    /// first, stripping EVERY BMM command suggestion portal-wide while <see cref="IsMethodPresent"/> still
    /// reported the module present. Manifest order was also only half the story: on the disk path, candidate
    /// order came from <see cref="Directory.EnumerateDirectories"/> — filesystem order, platform-dependent.
    /// Ordering by code instead makes the outcome reproducible everywhere, and discovery order is never a
    /// tiebreak. [Story 18.2 Defect B; ADR 0015 Decisions 4, 4a, 4b]</para>
    /// <para>The <c>looksLikeGame</c> source hint is the only tiebreak BETWEEN the two modeled modules, and it
    /// is unconditionally false when <paramref name="sourceRelativePaths"/> is empty — so when the hint is
    /// genuinely absent for a BMM+GDS repo, BMM wins deterministically. [Decision 4c]</para></summary>
    private static List<string> RankCandidates(List<string> candidates, IReadOnlyList<string> sourceRelativePaths)
    {
        if (candidates.Count <= 1)
        {
            return candidates;
        }

        var looksLikeGame = sourceRelativePaths.Any(p =>
            p.Contains("gdds/", StringComparison.OrdinalIgnoreCase)
            || p.Contains("gdds\\", StringComparison.OrdinalIgnoreCase)
            || IsFile(p, "gdd.md")
            || IsFile(p, "narrative-design.md")
            || IsFile(p, "game-architecture.md"));

        int Rank(string csvPath)
        {
            var code = CodeOf(csvPath);
            if (looksLikeGame && string.Equals(code, GdsCode, StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(code, BmmCode, StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(code, GdsCode, StringComparison.OrdinalIgnoreCase)) return 2;
            return 3;
        }

        return candidates
            .OrderBy(Rank)
            .ThenBy(CodeOf, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>The module code a <c>module-help.csv</c> path belongs to: its containing
    /// <c>_bmad/{code}/</c> directory name, lower-invariant. This is the ONLY reliable on-disk identity signal
    /// — <c>module.yaml</c> (which carries a clean <c>code:</c>) is an installer-source file and is never
    /// installed, and <c>_bmad/{code}/config.yaml</c> carries no module identity at all.
    /// [Story 18.2; ADR 0015 Decisions 1, 1b]</summary>
    private static string CodeOf(string csvPath) => DirNameOf(csvPath).ToLowerInvariant();

    /// <summary>The install directory's name in its REAL on-disk casing — what a path must be built from.
    /// <see cref="CodeOf"/> lower-invariants for comparison, and building
    /// <see cref="RepoRelativeCsv"/> from that produced <c>_bmad/bmm/module-help.csv</c> for a repo whose
    /// directory is <c>_bmad/BMM/</c>: a file that does not exist on a case-sensitive filesystem, so the
    /// webview Problems entry (file-anchored via <see cref="DiagnosticAnchorRoot.Repo"/>) resolved to nothing.
    /// That is the same wrong-root failure the Repo anchor was introduced to prevent, reached through casing
    /// instead of rooting. Compare with <see cref="CodeOf"/>; construct paths with this. [Review][Patch P6]</summary>
    private static string DirNameOf(string csvPath) =>
        Path.GetFileName(Path.GetDirectoryName(csvPath) ?? string.Empty);

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

    /// <summary>A parsed candidate plus whether the Decision-1c label cross-check demoted it. The two are
    /// separated because <see cref="Detect"/> must not treat a demotion as "found the primary" — see the
    /// descend-the-rank loop. [Review][Patch P10]</summary>
    private readonly record struct CandidateContext(ModuleContext Context, bool Demoted);

    /// <summary>The context for one candidate CSV, or null when it will not parse. Wraps
    /// <see cref="BuildCandidate"/> for the callers that do not care whether the label cross-check fired
    /// (<see cref="ForCode"/>, which answers "what does this named module declare?" rather than ranking
    /// anything).</summary>
    private static ModuleContext? BuildContext(string csvPath, List<AdapterDiagnostic>? diagnostics = null) =>
        BuildCandidate(csvPath, diagnostics)?.Context;

    private static CandidateContext? BuildCandidate(string csvPath, List<AdapterDiagnostic>? diagnostics = null)
    {
        var rows = ParseCsv(csvPath);
        if (rows.Count < 2)
        {
            return null;
        }

        var header = rows[0];
        var moduleIdx = Array.FindIndex(header, h => h.Trim().Equals("module", StringComparison.OrdinalIgnoreCase));
        var skillIdx = Array.FindIndex(header, h => h.Trim().Equals("skill", StringComparison.OrdinalIgnoreCase));
        if (skillIdx < 0)
        {
            return null;
        }

        // EMPTY, never a placeholder. This used to start at the literal "BMad", and only `CommandCatalog.Empty`
        // was emptied for Decision 2b — so the fabrication simply moved here. A CSV with no `module` header
        // column (only `skill` is required to parse) or with every module cell blank kept the literal, which
        // made CommandCatalog.HasLabel true for EVERY context this method returns: the three HasLabel guards
        // added for Decision 2b were unreachable, how-to-read announced "This project uses the BMad module" —
        // verbatim the false claim 2b exists to prevent — and a genuine `_bmad/bmm/` in that shape failed the
        // label cross-check below and lost its docs, glossary and commands. Decision 2b's rule is "every label
        // consumer must treat an empty label as 'no label'"; that starts at the parse site. [Review][Patch P1]
        var moduleLabel = string.Empty;
        string? prefix = null;
        var byStep = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Skip(1))
        {
            if (row.Length <= skillIdx)
            {
                continue;
            }

            var skill = row[skillIdx].Trim();
            if (skill.Length == 0 || skill == "_meta")
            {
                continue;
            }

            if (moduleIdx >= 0 && row.Length > moduleIdx && row[moduleIdx].Trim().Length > 0)
            {
                moduleLabel = row[moduleIdx].Trim();
            }

            // The prefix is the skill's leading token (bmad-create-story -> "bmad"); the step key is the
            // remainder ("create-story"), shared across modules so the suggestion logic stays module-neutral.
            prefix ??= skill.Split('-')[0];
            var step = skill.StartsWith(prefix + "-", StringComparison.Ordinal) ? skill[(prefix.Length + 1)..] : skill;

            // First row wins for a given step (e.g. create-story's create action over its validate action).
            if (!byStep.ContainsKey(step))
            {
                byStep[step] = "/" + skill;
            }
        }

        if (prefix is null)
        {
            return null;
        }

        // Identity comes from the module CODE — the containing _bmad/{code}/ directory — never from the skill
        // prefix. Every first-party BMad module EXCEPT Game Dev Studio prefixes its skills `bmad-`
        // (bmad-testarch-trace, bmad-cis-innovation-strategy, bmad-bmb-setup), so prefix inference identified
        // GDS correctly only by coincidence and silently reported every other module as BMad Method — serving
        // BMM's whole glossary to a project that provably doesn't use it. An unrecognized code is a
        // first-class outcome, NOT a fallback to BmadMethod: no closed enumeration can be correct while BMad
        // Builder mints modules with arbitrary codes. [Story 18.2; ADR 0015 Decisions 1 & 2]
        var code = CodeOf(csvPath);
        var module = ModuleForCode(code);

        // A minted module installed at a modeled code (say _bmad/gds/) would otherwise inherit that module's
        // docs and glossary wholesale. The label is already parsed, so the guard costs one comparison: a
        // modeled code that declares the wrong label is demoted to Unmodeled and reported. [Decision 1c]
        var demoted = false;
        if (module is not BmadModule.Unmodeled
            && ModeledModuleLabels.TryGetValue(code, out var expectedLabel)
            && moduleLabel.Length > 0
            && !LabelMatchesModeled(moduleLabel, expectedLabel))
        {
            diagnostics?.Add(new AdapterDiagnostic(
                AdapterDiagnosticCategory.Unsupported, RepoRelativeCsv(DirNameOf(csvPath)),
                $"module '{code}' declares the label '{moduleLabel}', but SpecScribe models '{code}' as "
                + $"'{expectedLabel}'; treating it as an unmodeled module so it never inherits "
                + $"{expectedLabel}'s planning docs or glossary",
                DiagnosticAnchorRoot.Repo));
            module = BmadModule.Unmodeled;
            demoted = true;
        }

        return new CandidateContext(
            new ModuleContext
            {
                Module = module,
                Code = code,
                Commands = new CommandCatalog(moduleLabel, byStep),
                Docs = DocsFor(module),
                Glossary = GlossaryFor(module),
            },
            demoted);
    }

    /// <summary>Whether a CSV's declared label is close enough to a modeled module's expected label to be the
    /// same module. Deliberately TOLERANT, not exact.
    /// <para>Exact matching made the shipped happy path depend on a third-party display string. ADR 0015 itself
    /// documents that BMad's own labels drift — GDS's <c>module.yaml</c> says "BMGD: BMad Game Dev Studio"
    /// while its CSV says "Game Dev Studio", and TEA's say "Test Architect" vs "Test Architecture Enterprise".
    /// A cosmetic upstream rename such as <c>BMad Method v6</c> would therefore have stripped every real BMM
    /// install of its planning docs, its whole glossary, site-wide abbreviation expansion and its command
    /// legend, signalled only by one warning row. Interior whitespace is normalized and a prefix or containment
    /// match passes, so drift survives; <c>Totally Not GDS</c> still fails, which is the squatter case Decision
    /// 1c exists for. An ABSENT label is not evidence of squatting and never demotes — see the caller's
    /// <c>moduleLabel.Length > 0</c> guard. [Review][Patch P9; owner call D1, 2026-07-27]</para></summary>
    private static bool LabelMatchesModeled(string declared, string expected)
    {
        var d = CollapseWhitespace(declared);
        var e = CollapseWhitespace(expected);
        return d.Contains(e, StringComparison.OrdinalIgnoreCase)
            || e.Contains(d, StringComparison.OrdinalIgnoreCase);
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>The modeled module a code maps to, or <see cref="BmadModule.Unmodeled"/> for anything else —
    /// which keeps its real label and parsed command catalog but publishes no docs and no glossary (the
    /// existing <c>_ =&gt;</c> defaults on <see cref="DocsFor"/>/<see cref="GlossaryFor"/> already yield
    /// exactly that, so neither switch needs an arm). Never <see cref="BmadModule.Unknown"/>: a CSV that
    /// parsed is a module that was DETECTED, and <see cref="BmadModule.Unknown"/> is reserved for detection
    /// failure. [ADR 0015 Decisions 1, 2a]</summary>
    private static BmadModule ModuleForCode(string code) => code switch
    {
        _ when string.Equals(code, BmmCode, StringComparison.OrdinalIgnoreCase) => BmadModule.BmadMethod,
        _ when string.Equals(code, GdsCode, StringComparison.OrdinalIgnoreCase) => BmadModule.GameDevStudio,
        _ => BmadModule.Unmodeled,
    };

    private static List<string[]> ParseCsv(string path)
    {
        var text = MarkdownConverter.ReadAllTextShared(path).Replace("\r\n", "\n");
        var rows = new List<string[]>();
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            rows.Add(ParseCsvLine(line));
        }

        return rows;
    }

    /// <summary>Splits a single CSV line, honoring double-quoted fields (which may contain commas) and the
    /// doubled-quote escape. Embedded newlines aren't expected in these manifests, so this stays line-based.</summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
