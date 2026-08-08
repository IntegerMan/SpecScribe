using System.Text.RegularExpressions;

namespace SpecScribe.Tests;

/// <summary>Pins the CI supply-chain posture Story 17.2 Task 8 audited. [AC #2]
///
/// <para><b>Why tests and not a paragraph in a story file.</b> Task 8's last instruction was to "record the
/// already-correct posture so a future change that regresses it is visible as a regression". Prose in a story
/// file is not visible as a regression — it is visible as archaeology. These are the four properties that
/// actually matter, asserted over the shipped workflow files.</para>
///
/// <para><b>The audit's finding, for the record.</b> The posture was mostly sound: least-privilege
/// <c>permissions: contents: read</c> with a written rationale, <c>pull_request</c> (never
/// <c>pull_request_target</c>, so a fork PR never receives secrets — the single most important thing to get
/// right, and it was right), <c>SONAR_TOKEN</c> reaching the job through <c>env:</c> and referenced only as
/// <c>$env:SONAR_TOKEN</c>, and Sonar steps guarded by <c>if: env.SONAR_TOKEN != ''</c> so a fork PR degrades
/// rather than fails. Two real gaps were fixed: the scanner was installed unpinned via
/// <c>dotnet tool update</c> while its cache key carried no version component (unpinned on first write, frozen
/// forever after), and the SHA-pinning question for actions was undecided.</para></summary>
public class CiSupplyChainTests
{
    private static string WorkflowDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, ".github", "workflows");
    }

    private static IEnumerable<(string Name, string Text)> Workflows() =>
        Directory.EnumerateFiles(WorkflowDir(), "*.yml")
            .Concat(Directory.EnumerateFiles(WorkflowDir(), "*.yaml"))
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f)));

    [Fact]
    public void NoWorkflowUsesPullRequestTarget()
    {
        // THE ONE THAT MATTERS MOST. `pull_request_target` runs with the BASE repo's secrets against the HEAD
        // ref's code — a fork PR could exfiltrate SONAR_TOKEN. `pull_request` does not provide secrets to
        // forks at all. This is currently correct; the test exists so it stays correct.
        foreach (var (name, text) in Workflows())
        {
            Assert.DoesNotContain("pull_request_target", text, StringComparison.Ordinal);
            Assert.NotNull(name);
        }
    }

    [Fact]
    public void SecretsAreNeverInterpolatedIntoRunBodies()
    {
        // A secret referenced as ${{ secrets.X }} INSIDE a `run:` body is inlined into the rendered command
        // line, where it can reach process listings and logs. The correct form — which this repo already uses
        // — is to bind it to `env:` once and reference the environment variable.
        var offenders = new List<string>();
        var runLine = new Regex(@"^(?<indent>[ \t]*)run:\s*(?<value>.*)$", RegexOptions.Multiline);

        foreach (var (name, text) in Workflows())
        {
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            foreach (Match m in runLine.Matches(text))
            {
                var body = m.Groups["value"].Value;
                if (body is "|" or ">")
                {
                    var runLineIndex = text[..m.Index].Count(character => character == '\n');
                    var bodyIndent = m.Groups["indent"].Length;
                    var bodyLines = lines
                        .Skip(runLineIndex + 1)
                        .TakeWhile(line => line.Length == 0 || line.TakeWhile(char.IsWhiteSpace).Count() > bodyIndent);
                    body = string.Join('\n', bodyLines);
                }
                if (body.Contains("secrets.", StringComparison.Ordinal))
                    offenders.Add($"{name}: a `run:` body interpolates a secret");
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n  ", offenders));
    }

    [Fact]
    public void ThirdPartyActionsMustBeShaPinned()
    {
        // THE DECISION, ENFORCED. [Story 17.2 Task 8]
        //
        // Every action this repository uses today is FIRST-PARTY `actions/*` (checkout, setup-dotnet,
        // setup-node, setup-java, cache, upload-pages-artifact, deploy-pages) — zero third-party actions.
        // For those, a floating major tag is ACCEPTED: trusting `actions/*` is the same trust already extended
        // to GitHub by running on their runners, and SHA-pinning them buys little against ongoing bump churn.
        //
        // For a THIRD-PARTY action the calculus inverts — a tag is mutable and the publisher is not GitHub.
        // (The well-known supply-chain incidents in this space have all been third-party actions.) So the rule
        // is: first-party may float, everyone else must be pinned to a full commit SHA. Recorded as a test
        // rather than a note, because the next contributor adding a third-party action is exactly the person
        // who will not read the note.
        var uses = new Regex(@"uses:\s*(?<ref>[^\s#]+)");
        var offenders = new List<string>();

        foreach (var (name, text) in Workflows())
        {
            foreach (Match m in uses.Matches(text))
            {
                var reference = m.Groups["ref"].Value;
                if (reference.StartsWith("./", StringComparison.Ordinal)) continue;   // local composite action
                if (reference.StartsWith("docker://", StringComparison.Ordinal)) continue;
                if (reference.StartsWith("actions/", StringComparison.Ordinal)) continue; // first-party: may float

                var at = reference.LastIndexOf('@');
                var pinned = at > 0 && Regex.IsMatch(reference[(at + 1)..], "^[0-9a-f]{40}$");
                if (!pinned) offenders.Add($"{name}: {reference}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Third-party actions must be pinned to a full 40-character commit SHA (a tag is mutable and the "
            + "publisher is not GitHub). First-party `actions/*` may use a floating major tag — that is this "
            + "repository's recorded decision. Offending:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void TheSonarScannerIsPinnedAndItsVersionIsInTheCacheKey()
    {
        // Both halves together, because either one alone reproduces the original defect: a pinned install with
        // an unversioned cache key still serves the stale cached binary, and a versioned key with an unpinned
        // install still installs "latest" on a miss.
        var text = File.ReadAllText(Path.Combine(WorkflowDir(), "build-test-analyze.yml"));

        Assert.Contains("SONAR_SCANNER_VERSION:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet tool update dotnet-sonarscanner --tool-path", text, StringComparison.Ordinal);
        Assert.Contains("--version $env:SONAR_SCANNER_VERSION", text, StringComparison.Ordinal);
        Assert.Contains("key: ${{ runner.os }}-sonar-scanner-${{ env.SONAR_SCANNER_VERSION }}",
            text, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowsDeclareLeastPrivilegePermissions()
    {
        // A workflow with no `permissions:` block inherits the repository default, which can be
        // read/write on every scope. Every workflow must state its own.
        var offenders = Workflows()
            .Where(w => !w.Text.Contains("permissions:", StringComparison.Ordinal))
            .Select(w => w.Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These workflows declare no `permissions:` block and inherit the repository default:\n  "
            + string.Join("\n  ", offenders));
    }
}
