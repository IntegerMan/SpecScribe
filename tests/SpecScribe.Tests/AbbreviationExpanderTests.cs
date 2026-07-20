using SpecScribe;

namespace SpecScribe.Tests;

public class AbbreviationExpanderTests
{
    private static readonly GlossaryTerm Fr = new("FR", "Functional Requirement", "A specific capability.", IsAcronym: true);
    private static readonly GlossaryTerm Nfr = new("NFR", "Non-Functional Requirement", "A quality attribute.", IsAcronym: true);
    private static readonly GlossaryTerm Adr = new("ADR", "Architecture Decision Record", "A decision record.", IsAcronym: true);
    private static readonly GlossaryTerm SpecKernel = new("spec kernel", "spec kernel", "The distilled contract.", IsAcronym: false);

    private static readonly IReadOnlyList<GlossaryTerm> Glossary = new[] { Fr, Nfr, Adr, SpecKernel };

    [Fact]
    public void Expand_WrapsFirstOccurrence_LeavesLaterOccurrencesPlain()
    {
        var html = AbbreviationExpander.Expand("<p>An FR is tracked. Later, another FR appears.</p>", Glossary);

        Assert.Equal(
            "<p>An <abbr title=\"Functional Requirement\">FR</abbr> is tracked. Later, another FR appears.</p>",
            html);
    }

    [Fact]
    public void Expand_TracksEachAcronymIndependently()
    {
        var html = AbbreviationExpander.Expand("<p>FR and NFR and FR and NFR.</p>", Glossary);

        Assert.Equal(
            "<p><abbr title=\"Functional Requirement\">FR</abbr> and <abbr title=\"Non-Functional Requirement\">NFR</abbr> and FR and NFR.</p>",
            html);
    }

    [Fact]
    public void Expand_NeverRewritesInsideExistingAnchor()
    {
        var html = AbbreviationExpander.Expand("<a href=\"x.html\">FR</a> but FR here", Glossary);

        Assert.StartsWith("<a href=\"x.html\">FR</a>", html);
        Assert.Contains("but <abbr title=\"Functional Requirement\">FR</abbr> here", html);
    }

    [Fact]
    public void Expand_NeverRewritesInsideCodeOrPreOrAbbrOrAttribute()
    {
        var input = "<code>FR</code> then <pre>FR</pre> then <abbr title=\"x\">FR</abbr> then <span data-tip=\"FR\">y</span> then FR";
        var html = AbbreviationExpander.Expand(input, Glossary);

        Assert.Contains("<code>FR</code>", html);
        Assert.Contains("<pre>FR</pre>", html);
        Assert.Contains("<abbr title=\"x\">FR</abbr>", html);
        Assert.Contains("data-tip=\"FR\"", html);
        // The bare FR outside all of those is the actual first use and gets wrapped.
        Assert.Contains("then <abbr title=\"Functional Requirement\">FR</abbr>", html);
    }

    [Fact]
    public void Expand_MatchesWholeWordOnly_NotPartialTokens()
    {
        var html = AbbreviationExpander.Expand("<p>ACTION and heACHe and FRee stay plain.</p>", Glossary);

        Assert.DoesNotContain("<abbr", html);
    }

    [Fact]
    public void Expand_SkipsNumberedReferences_HyphenOrSpaceThenDigits()
    {
        // Real story prose cites "ADR-0005" / "ADR 0005"; `\b` alone would wrap the bare acronym mid-ref.
        var hyphen = AbbreviationExpander.Expand("<p>See ADR-0005 for the decision.</p>", Glossary);
        Assert.Equal("<p>See ADR-0005 for the decision.</p>", hyphen);

        var spaced = AbbreviationExpander.Expand("<p>See ADR 0005 for the decision.</p>", Glossary);
        Assert.Equal("<p>See ADR 0005 for the decision.</p>", spaced);

        var enDash = AbbreviationExpander.Expand("<p>See ADR\u20130005 for the decision.</p>", Glossary);
        Assert.Equal("<p>See ADR\u20130005 for the decision.</p>", enDash);

        // Policy is glossary-wide, not ADR-only.
        var fr = AbbreviationExpander.Expand("<p>See FR-0001 next.</p>", Glossary);
        Assert.Equal("<p>See FR-0001 next.</p>", fr);
    }

    [Fact]
    public void Expand_SingleDigitAfterSpace_StillExpands()
    {
        // ≥2-digit lookahead: "ADR 5 years" is prose, not a padded citation id.
        var html = AbbreviationExpander.Expand("<p>An ADR 5 years ago.</p>", Glossary);

        Assert.Equal(
            "<p>An <abbr title=\"Architecture Decision Record\">ADR</abbr> 5 years ago.</p>",
            html);
    }

    [Fact]
    public void Expand_NumberedReferenceDoesNotConsumeFirstUse_BareAcronymStillExpands()
    {
        var html = AbbreviationExpander.Expand(
            "<p>See ADR-0005. An ADR records the decision.</p>", Glossary);

        Assert.Equal(
            "<p>See ADR-0005. An <abbr title=\"Architecture Decision Record\">ADR</abbr> records the decision.</p>",
            html);
    }

    [Fact]
    public void Expand_StillWrapsAcronymBeforeHyphenatedWord()
    {
        // "ADR-driven" is not a numbered ref — digit lookahead must not fire on a letter after the hyphen.
        var html = AbbreviationExpander.Expand("<p>An ADR-driven approach.</p>", Glossary);

        Assert.Equal(
            "<p>An <abbr title=\"Architecture Decision Record\">ADR</abbr>-driven approach.</p>",
            html);
    }

    [Fact]
    public void Expand_LongestMatchWins_NfrOverFr()
    {
        var html = AbbreviationExpander.Expand("<p>NFR12 requires attention.</p>", Glossary);

        // Word-boundary means "NFR12" isn't matched at all (trailing digit breaks the boundary) — proving
        // the alternation didn't partially match "FR" out of the middle of "NFR12".
        Assert.DoesNotContain("<abbr", html);

        var html2 = AbbreviationExpander.Expand("<p>NFR requires attention.</p>", Glossary);
        Assert.Equal("<p><abbr title=\"Non-Functional Requirement\">NFR</abbr> requires attention.</p>", html2);
    }

    [Fact]
    public void Expand_TitleIsHtmlEscaped()
    {
        var withQuotes = new GlossaryTerm("FR", "Say \"hi\" & bye", "def", IsAcronym: true);
        var html = AbbreviationExpander.Expand("<p>FR</p>", new[] { withQuotes });

        Assert.Contains("title=\"Say &quot;hi&quot; &amp; bye\"", html);
    }

    [Fact]
    public void Expand_DuplicateAcronymTerms_UsesFirstAndDoesNotThrow()
    {
        var first = new GlossaryTerm("FR", "Functional Requirement", "A capability.", IsAcronym: true);
        var duplicate = new GlossaryTerm("FR", "Wrong Expansion", "Should not win.", IsAcronym: true);

        var html = AbbreviationExpander.Expand("<p>FR here.</p>", new[] { first, duplicate });

        Assert.Equal("<p><abbr title=\"Functional Requirement\">FR</abbr> here.</p>", html);
    }

    [Fact]
    public void Expand_EmptyGlossary_ReturnsInputUnchanged()
    {
        const string input = "<p>FR and NFR mentioned here.</p>";
        Assert.Same(input, AbbreviationExpander.Expand(input, Array.Empty<GlossaryTerm>()));
    }

    [Fact]
    public void Expand_GlossaryWithNoAcronyms_ReturnsInputUnchanged()
    {
        const string input = "<p>A story and an epic.</p>";
        Assert.Same(input, AbbreviationExpander.Expand(input, new[] { SpecKernel }));
    }

    [Fact]
    public void Expand_EmptyOrNullHtml_ReturnsInputUnchanged()
    {
        Assert.Equal(string.Empty, AbbreviationExpander.Expand(string.Empty, Glossary));
        Assert.Null(AbbreviationExpander.Expand(null!, Glossary));
    }

    [Fact]
    public void Expand_NonAcronymTermsAreNeverWrapped()
    {
        // Longer glossary-only terms ("spec kernel") aren't abbr-shaped — only the glossary page lists them.
        var html = AbbreviationExpander.Expand("<p>Read the spec kernel first.</p>", Glossary);

        Assert.DoesNotContain("<abbr", html);
        Assert.Contains("Read the spec kernel first.", html);
    }
}
