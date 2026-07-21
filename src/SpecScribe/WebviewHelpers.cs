namespace SpecScribe;

/// <summary>The generators behind the webview's read-only helper affordances (AC #2). Each helper is a PURE
/// text/command producer: given nothing but its inputs it returns a string, and that string is handed off to the
/// user by an explicit action (the extension host copies it to the clipboard) — nothing here, and nothing on the
/// handoff path, writes a source planning artifact or mutates settings. That read-only posture is a spine
/// invariant, not a convenience: "Helpers can generate prompts or commands, but any write action remains an
/// explicit external choice" (AD-6 / ARCHITECTURE-SPINE.md; FR-17; NFR-5). Keeping the generation in C# (rather
/// than the thin TS shim) makes the read-only contract unit-testable and keeps the shim brainless. [Story 6.5]</summary>
public static class WebviewHelpers
{
    /// <summary>The read-only instruction every helper prompt must carry (AD-6/NFR-5): asks for a text-only review
    /// and explicitly forbids file edits. Named so tests can assert the CONTRACT (this sentence is present) without
    /// duplicating its exact wording as a second literal — a copy-edit here can't silently desync from the test
    /// that pins it. [Story 6.5 deferred-work cleanup]</summary>
    public const string ReadOnlyDirective = "Do NOT modify any files — produce the review as text only.";

    /// <summary>FR-17's canonical example helper: a code-review prompt the user can paste into an AI assistant
    /// (or issue tracker) to review the project's current changes. It is deliberately GENERIC and read-only in its
    /// own instructions — it asks for a text review, and explicitly tells the reviewer not to modify files — so the
    /// helper can never become a write path even transitively. A pure function of its input: no I/O, no project
    /// state, deterministic. [Story 6.5]</summary>
    /// <param name="siteTitle">The project's display title, woven into the prompt so the copied text names the
    /// project it came from. Untrusted only in the sense of any project string; it is not interpreted, only
    /// embedded verbatim into the returned plain text.</param>
    public static string CodeReviewPrompt(string siteTitle)
    {
        var project = string.IsNullOrWhiteSpace(siteTitle) ? "this project" : siteTitle.Trim();
        return
            $"Please perform a thorough code review of the current uncommitted changes in {project}.\n\n" +
            "Focus on:\n" +
            "  - Correctness and edge cases (off-by-one, null/empty handling, error paths)\n" +
            "  - Security (input validation, injection, unsafe file/process/network use)\n" +
            "  - Adherence to the project's existing architecture, patterns, and conventions\n" +
            "  - Test coverage for the changed behavior\n\n" +
            "Report findings grouped by severity (High / Medium / Low), each with a file:line reference and a " +
            "concrete suggested fix. " + ReadOnlyDirective;
    }
}
