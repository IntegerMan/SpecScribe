using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Parses a retrospective participant string ("Amelia (Developer)", "facilitator") into a name, an
/// optional parenthetical role, and a stable role css-class. The class drives a role icon (<see cref="Icons.ForPersona"/>)
/// and a role tint on the retro page's Personas pills — a recognition layer over the freeform role text, with a
/// neutral <c>role-generic</c> fallback so an unknown role never breaks. Kept as its own classifier (one seam),
/// mirroring <see cref="StatusStyles"/>. [Story 2.3 retro personas]</summary>
public static class Personas
{
    // "Name (Role)" — role is the last parenthesized group; everything before it (trimmed) is the name.
    private static readonly Regex NameRole = new(@"^(?<name>.*?)\s*\((?<role>[^()]*)\)\s*$", RegexOptions.Compiled);

    public readonly record struct Persona(string Name, string? Role, string RoleClass);

    /// <summary>Splits a participant into name/role and classifies the role. A participant with no
    /// parenthetical (e.g. "facilitator") keeps its whole text as the name and classifies off that text.</summary>
    public static Persona Parse(string participant)
    {
        var text = participant.Trim();
        var m = NameRole.Match(text);
        var (name, role) = m.Success
            ? (m.Groups["name"].Value.Trim(), m.Groups["role"].Value.Trim())
            : (text, (string?)null);
        if (name.Length == 0) name = text; // "(Role)" with no name — fall back to the raw text.

        return new Persona(name, string.IsNullOrEmpty(role) ? null : role, RoleClass(role ?? name));
    }

    /// <summary>Maps role (or, when there's no explicit role, the whole participant text) to a css-class by
    /// keyword. Order matters where terms overlap (e.g. "Senior Dev" and "Product Owner").</summary>
    public static string RoleClass(string roleText)
    {
        var r = roleText.ToLowerInvariant();
        if (r.Contains("facilitat")) return "role-facil";
        if (r.Contains("architect")) return "role-arch";
        if (r.Contains("qa") || r.Contains("test") || r.Contains("quality")) return "role-qa";
        if (r.Contains("ux") || r.Contains("design")) return "role-ux";
        if (r.Contains("analyst")) return "role-analyst";
        if (r.Contains("writer") || r.Contains("doc")) return "role-writer";
        if (r.Contains("product") || r.Contains("owner") || r == "po") return "role-po";
        if (r.Contains("dev") || r.Contains("engineer")) return "role-dev";
        if (r.Contains("lead") || r.Contains("manager") || r.Contains("pm") || r.Contains("scrum")) return "role-lead";
        return "role-generic";
    }
}
