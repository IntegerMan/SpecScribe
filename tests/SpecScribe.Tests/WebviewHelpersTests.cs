using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Story 6.5 AC #2: the webview helper affordances are pure, read-only TEXT generators. These pin the
/// generator contract — it returns a prompt string, weaves in the project title, instructs the reviewer not to
/// write files, and is a deterministic function of its input (no I/O, no project state). The write-free HANDOFF
/// (clipboard only) is asserted at the document level in <see cref="WebviewThemingTests"/> and enforced by
/// construction in the extension shim. [Story 6.5]</summary>
public class WebviewHelpersTests
{
    [Fact]
    public void CodeReviewPrompt_ReturnsNonEmptyText_NamingTheProject()
    {
        var prompt = WebviewHelpers.CodeReviewPrompt("SpecScribe");

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("SpecScribe", prompt);
        Assert.Contains("code review", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CodeReviewPrompt_IsExplicitlyReadOnly_TellingTheReviewerNotToModifyFiles()
    {
        // The generated prompt must itself be read-only in intent (AD-6/NFR-5): it asks for a text review and
        // explicitly forbids file edits, so the helper can never become a write path even transitively.
        var prompt = WebviewHelpers.CodeReviewPrompt("SpecScribe");

        // Structural check: the prompt actually carries the directive (catches "forgot to append it" bugs) —
        // asserted against the named constant, not a duplicated literal, so a copy-edit to the wording can't
        // desync this from the constant it's built from. [deferred-work]
        Assert.Contains(WebviewHelpers.ReadOnlyDirective, prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyDirective_ActuallySaysReadOnly()
    {
        // Semantic check on the constant ITSELF (not the composed prompt): the directive genuinely forbids writes
        // and asks for text only. Deliberately loose (case-insensitive keywords, not the exact sentence) so a
        // copy-edit survives, but this can still fail if the directive were ever emptied or replaced with text that
        // no longer conveys the read-only contract — unlike asserting the constant against itself. [deferred-work,
        // review patch: the constant-vs-constant check above alone is tautological and can never catch that]
        Assert.Contains("not", WebviewHelpers.ReadOnlyDirective, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("modify", WebviewHelpers.ReadOnlyDirective, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text only", WebviewHelpers.ReadOnlyDirective, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CodeReviewPrompt_IsDeterministic_ForTheSameInput()
    {
        // A pure function of its input — no clock, no filesystem, no randomness — so the copied text is stable and
        // the read-only contract is trivially auditable.
        Assert.Equal(WebviewHelpers.CodeReviewPrompt("SpecScribe"), WebviewHelpers.CodeReviewPrompt("SpecScribe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CodeReviewPrompt_FallsBackGracefully_OnAnEmptyTitle(string title)
    {
        // A blank site title must not produce a broken "review of ." sentence.
        var prompt = WebviewHelpers.CodeReviewPrompt(title);

        Assert.Contains("this project", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("review of  ", prompt, StringComparison.Ordinal);
    }
}
