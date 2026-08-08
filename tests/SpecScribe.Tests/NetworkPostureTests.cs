using System.Text.RegularExpressions;

namespace SpecScribe.Tests;

/// <summary>Pins SpecScribe's network posture — NFR3's "local-first, no remote telemetry".
/// [Story 17.2 Task 5]
///
/// <para><b>Why a source-reading test rather than a behavioural one.</b> The properties that matter here are
/// ABSENCES (no outbound host, no wildcard bind), and an absence cannot be observed by exercising the happy
/// path — a test that spawns the renderer and fetches over loopback passes identically whether the server
/// bound <c>127.0.0.1</c> or <c>0.0.0.0</c>. That is exactly how this defect survived: every existing test
/// fetched over loopback and was satisfied. So the enumeration is asserted over the shipped source, and the
/// live measurement is recorded below rather than re-run on every build.</para>
///
/// <para><b>The measurement this pins (2026-08-08, baseline <c>e8a689d</c>, Windows 11).</b> With only
/// <c>PORT</c>/<c>NITRO_PORT</c> set — the shipped state before this story — the prerender server logged
/// <c>Listening on http://[::]:39117</c>, the IPv6 wildcard. Its listening socket's <c>LocalAddress</c> was
/// <c>::</c>, and the fully rendered portal was fetched over this machine's two real LAN addresses, both
/// answering <b>HTTP 200 with 1,305,409 bytes</b>. For the duration of every <c>generate</c>, a PRIVATE
/// repository's entire portal was readable by anyone on the same network. After setting
/// <c>HOST</c>/<c>NITRO_HOST</c>: <c>Listening on http://127.0.0.1:39118</c>, <c>LocalAddress 127.0.0.1</c>,
/// loopback still HTTP 200 with the identical 1,305,409 bytes, both LAN addresses refused.</para></summary>
public class NetworkPostureTests
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
    public void PrerenderServerIsPinnedToLoopback()
    {
        var source = File.ReadAllText(Path.Combine(SourceDir(), "NuxtPrerender.cs"));

        Assert.Contains("psi.Environment[\"NITRO_HOST\"] = \"127.0.0.1\"", source, StringComparison.Ordinal);
        Assert.Contains("psi.Environment[\"HOST\"] = \"127.0.0.1\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOnlyOutboundHttpIsLoopback()
    {
        // THE NFR3 ENUMERATION, as a gate rather than a paragraph. Every HttpClient BaseAddress in the product
        // must be loopback. A future story that adds a real outbound call has to change this test, which makes
        // the crossing deliberate and visible in review — the thing AC #2 asks for ("re-confirmed for every
        // code path added since it was last verified").
        var offenders = new List<string>();
        var baseAddress = new Regex(@"BaseAddress\s*=\s*new\s+Uri\(\s*(?<uri>[$@""].*?)\)", RegexOptions.Singleline);

        foreach (var file in Directory.EnumerateFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            var text = File.ReadAllText(file);
            foreach (Match m in baseAddress.Matches(text))
            {
                var uri = m.Groups["uri"].Value;
                var loopback = uri.Contains("127.0.0.1", StringComparison.Ordinal)
                    || uri.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Contains("[::1]", StringComparison.Ordinal);
                if (!loopback) offenders.Add($"{Path.GetFileName(file)}: {uri}");
            }
        }

        Assert.True(offenders.Count == 0,
            "NFR3: every HttpClient in the product must target loopback. Non-loopback base addresses:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NoTelemetryOrAnalyticsEndpointsInProductSource()
    {
        // A blunt but honest check: no product source file may name an outbound analytics/telemetry host.
        // Deliberately excludes comments? No — deliberately does NOT, because a commented-out endpoint is
        // still a thing a reviewer should see, and the cost of the check is one rename.
        var banned = new[] { "google-analytics", "segment.io", "sentry.io", "applicationinsights", "mixpanel" };
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            var text = File.ReadAllText(file);
            foreach (var host in banned)
            {
                if (text.Contains(host, StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{Path.GetFileName(file)}: {host}");
            }
        }

        Assert.True(offenders.Count == 0, "NFR3 violation:\n  " + string.Join("\n  ", offenders));
    }
}
