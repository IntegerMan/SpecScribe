using System.Text;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Which BMad methodology module a source repo was produced with. Drives the workflow-command
/// suggestions and the well-known planning docs surfaced in nav.</summary>
public enum BmadModule { Unknown, BmadMethod, GameDevStudio }

/// <summary>A well-known planning document a module publishes, matched by filename anywhere in the source
/// tree. <see cref="InNav"/> docs also appear in the top nav; every discovered doc appears in the
/// dashboard quick links regardless.</summary>
public sealed record ModuleDoc(string FileName, string Label, string Description, bool InNav);

/// <summary>The workflow slash-commands a module exposes, parsed from its <c>module-help.csv</c> so the
/// "Next Steps" panels show the commands that actually exist (<c>/bmad-*</c> for BMad Method, <c>/gds-*</c>
/// for Game Dev Studio) rather than a hard-coded set. Keyed by skill base-name — the skill id minus its
/// module prefix, e.g. <c>create-story</c>.</summary>
public sealed class CommandCatalog
{
    private readonly IReadOnlyDictionary<string, string> _byStep;

    public CommandCatalog(string moduleLabel, IReadOnlyDictionary<string, string> byStep)
    {
        ModuleLabel = moduleLabel;
        _byStep = byStep;
    }

    /// <summary>Human label for the module (e.g. "BMad Method"), used in the "Next Steps" panel heading.</summary>
    public string ModuleLabel { get; }

    public bool IsEmpty => _byStep.Count == 0;

    /// <summary>Fallback used when no module could be detected — every lookup misses, so callers omit the
    /// command panels entirely rather than print commands that don't exist.</summary>
    public static readonly CommandCatalog Empty = new("BMad", new Dictionary<string, string>());

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

    public static readonly ModuleContext None = new()
    {
        Module = BmadModule.Unknown,
        Commands = CommandCatalog.Empty,
        Docs = Array.Empty<ModuleDoc>(),
    };

    // BMad Method publishes its planning artifacts in nested folders (prds/, briefs/, ux-designs/) plus a
    // spec architecture spine; they're matched by filename anywhere in the source tree. PRD + Architecture
    // ride the top nav; the brief and UX docs surface in the dashboard quick links to keep the nav lean.
    private static readonly IReadOnlyList<ModuleDoc> BmadMethodDocs = new[]
    {
        new ModuleDoc("prd.md", "PRD", "Read the product requirements.", InNav: true),
        new ModuleDoc("ARCHITECTURE-SPINE.md", "Architecture", "Inspect the architecture spine.", InNav: true),
        new ModuleDoc("brief.md", "Product Brief", "Review the product brief.", InNav: false),
        new ModuleDoc("DESIGN.md", "UX Design", "Inspect the UX design system.", InNav: false),
        new ModuleDoc("EXPERIENCE.md", "UX Experience", "Inspect UX behavior and flows.", InNav: false),
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

    private static readonly Regex ManifestModulePattern = new(
        @"^\s*-\s*name:\s*(?<name>[A-Za-z0-9_-]+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Detects the primary methodology module for a repo. Prefers the installed-module registry,
    /// falls back to whatever <c>module-help.csv</c> files exist, and uses source-artifact shape only to
    /// break ties when more than one methodology module is installed. Never throws — an undetectable module
    /// yields <see cref="None"/>, which degrades to nav without module docs and no command panels.</summary>
    public static ModuleContext Detect(string repoRoot, IReadOnlyList<string> sourceRelativePaths)
    {
        try
        {
            var bmadRoot = Path.Combine(repoRoot, "_bmad");
            if (!Directory.Exists(bmadRoot))
            {
                return None;
            }

            var candidates = ReadInstalledModules(bmadRoot)
                .Where(n => !string.Equals(n, "core", StringComparison.OrdinalIgnoreCase))
                .Select(n => Path.Combine(bmadRoot, n, "module-help.csv"))
                .Where(File.Exists)
                .ToList();

            // Fallback when the manifest is missing/unreadable: any non-core module-help.csv on disk.
            if (candidates.Count == 0)
            {
                candidates = Directory.EnumerateDirectories(bmadRoot)
                    .Where(d => !string.Equals(Path.GetFileName(d), "core", StringComparison.OrdinalIgnoreCase))
                    .Select(d => Path.Combine(d, "module-help.csv"))
                    .Where(File.Exists)
                    .ToList();
            }

            if (candidates.Count == 0)
            {
                return None;
            }

            // Try the best-guess primary first, then the remaining candidates — a parse failure on one
            // installed module must not discard another module whose module-help.csv would have parsed.
            var primary = ChoosePrimary(candidates, sourceRelativePaths);
            foreach (var candidate in candidates.OrderByDescending(c => string.Equals(c, primary, StringComparison.Ordinal)))
            {
                var context = BuildContext(candidate);
                if (context is not null)
                {
                    return context;
                }
            }

            return None;
        }
        catch (Exception)
        {
            // Detection is best-effort: any failure (IO, permissions, malformed data) degrades to None
            // rather than aborting the whole site build.
            return None;
        }
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

    private static string ChoosePrimary(List<string> candidates, IReadOnlyList<string> sourceRelativePaths)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        static string DirName(string csvPath) => Path.GetFileName(Path.GetDirectoryName(csvPath) ?? string.Empty);

        var looksLikeGame = sourceRelativePaths.Any(p =>
            p.Contains("gdds/", StringComparison.OrdinalIgnoreCase)
            || p.Contains("gdds\\", StringComparison.OrdinalIgnoreCase)
            || IsFile(p, "gdd.md")
            || IsFile(p, "narrative-design.md")
            || IsFile(p, "game-architecture.md"));

        if (looksLikeGame)
        {
            var gds = candidates.FirstOrDefault(c => DirName(c).StartsWith("gds", StringComparison.OrdinalIgnoreCase));
            if (gds is not null)
            {
                return gds;
            }
        }

        var nonGds = candidates.FirstOrDefault(c => !DirName(c).StartsWith("gds", StringComparison.OrdinalIgnoreCase));
        return nonGds ?? candidates[0];
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

    private static ModuleContext? BuildContext(string csvPath)
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

        var moduleLabel = "BMad";
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

        var module = prefix.StartsWith("gds", StringComparison.OrdinalIgnoreCase)
            ? BmadModule.GameDevStudio
            : BmadModule.BmadMethod;

        return new ModuleContext
        {
            Module = module,
            Commands = new CommandCatalog(moduleLabel, byStep),
            Docs = DocsFor(module),
        };
    }

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
