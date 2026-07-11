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

        Assert.Contains("Do NOT modify any files", prompt, StringComparison.Ordinal);
        Assert.Contains("text only", prompt, StringComparison.OrdinalIgnoreCase);
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
