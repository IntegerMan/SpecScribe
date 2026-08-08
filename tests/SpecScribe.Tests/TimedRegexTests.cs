using System.Diagnostics;
using System.Text.RegularExpressions;
using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Turns the <c>csharpsquid:S6444</c> band into an INVARIANT rather than a sweep.
/// [Story 17.2 Task 3]
///
/// <para>The band was 156 findings on 2026-07-27, 174 on 2026-08-07 and <b>175</b> at this story's baseline —
/// it grew while the story was being written. That is the whole argument for
/// <see cref="EveryRegexIsConstructedThroughTheFactory"/>: a one-time pass over 175 sites would have been
/// re-rotting before it landed. This test is what stops the 176th.</para></summary>
public class TimedRegexTests
{
    private static string SourceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "SpecScribe")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "SpecScribe");
    }

    [Fact]
    public void EveryRegexIsConstructedThroughTheFactory()
    {
        // THE ENFORCING GATE. A bare `new Regex(` anywhere in src/ reopens the band one site at a time.
        // TimedRegex.cs is the one legitimate construction point and is excluded by name.
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "TimedRegex.cs") continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"new\s+Regex\s*\("))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Regex must be constructed through TimedRegex.New so the match timeout is a property of the "
            + "codebase rather than of each call site (Sonar csharpsquid:S6444; Story 17.2 Task 3). "
            + "Offending sites:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void RegexFieldsDoNotUseTargetTypedNew()
    {
        // The other half of the same gate. `private static readonly Regex X = new(...)` constructs a Regex
        // without the token `new Regex(`, so the check above cannot see it — and that was the shape 162 of the
        // 163 rewritten sites actually used.
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "TimedRegex.cs") continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i], @"\bRegex\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*new\s*\("))
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "A Regex field must be initialized with TimedRegex.New(...), not target-typed `new(...)`. "
            + "Offending sites:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void FactoryAppliesTheDefaultTimeout()
    {
        var rx = TimedRegex.New(@"\d+");

        Assert.Equal(TimedRegex.DefaultTimeout, rx.MatchTimeout);
    }

    [Fact]
    public void FactoryPreservesOptions()
    {
        var rx = TimedRegex.New("abc", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        Assert.True(rx.Options.HasFlag(RegexOptions.IgnoreCase));
        Assert.True(rx.IsMatch("ABC"));
    }

    [Fact]
    public void ExplicitTimeoutOverloadIsHonoured()
    {
        var rx = TimedRegex.New("abc", RegexOptions.None, TimeSpan.FromMilliseconds(250));

        Assert.Equal(TimeSpan.FromMilliseconds(250), rx.MatchTimeout);
    }

    [Fact]
    public void CatastrophicBacktrackingIsBoundedRatherThanHanging()
    {
        // The property the whole band exists to buy, demonstrated on a genuinely catastrophic pattern.
        // `(a+)+$` against a long run of 'a' followed by a non-match is the textbook exponential case; without
        // a timeout this does not return in any practical time.
        //
        // Bounded to 200 ms rather than the 2 s house default so the test is fast; the assertion is about the
        // MECHANISM (a bounded failure instead of a hang), not the specific duration.
        var rx = TimedRegex.New(@"^(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(200));
        var hostile = new string('a', 40) + "!";

        var sw = Stopwatch.StartNew();
        Assert.Throws<RegexMatchTimeoutException>(() => rx.IsMatch(hostile));
        sw.Stop();

        // Generous ceiling: .NET checks the timeout periodically rather than pre-emptively, so the observed
        // time exceeds the bound somewhat. The point is that it is bounded at all — an unbounded run of this
        // pattern does not finish.
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10),
            $"expected a bounded failure, took {sw.Elapsed}");
    }

    [Fact]
    public void NormalMatchesAreUnaffectedByTheTimeout()
    {
        // The timeout must be invisible to legitimate work — it is a bound, not a budget being spent.
        var rx = TimedRegex.New(@"^### Story (\d+)\.(\d+):\s*(.+)$");

        Assert.True(rx.IsMatch("### Story 17.2: Security and Privacy Hardening"));
    }
}
