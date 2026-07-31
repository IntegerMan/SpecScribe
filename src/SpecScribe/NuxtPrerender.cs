using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Drives the static prerender by booting the prebuilt Nitro artefact and issuing one request per route
/// from the manifest SpecScribe just emitted. [Story 23.6 AC #7, ADR 0022 §Decision 3]
///
/// <para>This is the execution of ADR 0022 §Decision 3, verbatim: <i>"SpecScribe drives the prerender. At generate
/// time the CLI boots the artefact, sets <c>SPECSCRIBE_IR_DIR</c>, and issues one request per route from the
/// manifest it just emitted. It does not invoke <c>nuxt generate</c>, and the artefact does not crawl."</i>
/// Story 23.5 proved the transport end to end — 1,056/1,056 routes of this repo and 32/33 of a different project
/// from one 3.78 MB artefact at ~4 ms/route.</para>
///
/// <para><b>Why this lives in its own file.</b> <see cref="SiteGenerator"/> is ~5,900 lines and carries 91 open
/// analysis observations. The prerender is a self-contained concern with its own process lifetime, so it is a
/// collaborator rather than another region of that file.</para>
///
/// <para><b>Traps this deliberately inherits from <c>web/scripts/experiment-two-ir.mjs</c></b>, which is a
/// MEASUREMENT harness and is not reused as the shipping implementation — but which solved each of these first
/// and recorded them in its own header:</para>
/// <list type="bullet">
/// <item>The artefact must be a <c>build:package</c> build. A plain <c>nuxt build</c> bakes the building
/// project's pages into <c>public/</c>, and <b>Nitro serves <c>public/</c> ahead of the SSR route</b> — pointed
/// at another IR it returns the baked project's page with HTTP 200. A wrong answer with a success status.</item>
/// <item><c>IR_DIR</c> resolves at module scope, so one server process sees exactly one IR. Fine here (one
/// generate, one project), but the server cannot be kept warm across projects.</item>
/// <item>Readiness is POLLED, never slept. Any HTTP response means the server is listening — including a 500.
/// Waiting for a healthy status hangs for the whole timeout on a project whose entry page legitimately fails and
/// then reports "server did not listen", which is the wrong diagnosis entirely.</item>
/// <item>Never substring-probe rendered HTML. This portal renders its own source and its own docs, so
/// <c>_payload.json</c>, <c>window.__NUXT__</c> and <c>&lt;main&gt;</c> all appear as PROSE on real pages. The
/// emptiness check below anchors on the full <c>&lt;main id="main-content"</c> landmark for that reason.</item>
/// </list></summary>
public sealed class NuxtPrerender
{
    /// <summary>The Node range ADR 0022 §Decision 5 names, quoted verbatim in every failure message so a user
    /// never has to find the ADR to learn what to install. Matches <c>web/package.json</c>'s <c>engines.node</c>.</summary>
    public const string SupportedNodeRange = "^22.19.0 || ^24.11.0 || >=26.0.0";

    /// <summary>The universal Story 1.4 landmark. A 200 whose body does not contain this is an empty shell, and
    /// reporting it as a success is how a silently broken page ships.</summary>
    private const string MainLandmark = "<main id=\"main-content\"";

    private static readonly Regex NodeVersionPattern = new(@"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)", RegexOptions.Compiled);

    /// <summary>Result of one prerender pass. <paramref name="Events"/> carries one entry per route that failed
    /// plus a single summary entry; successful routes are not individually evented (1,469 Generated events would
    /// drown the console and the diagnostics page).</summary>
    public sealed record Result(int Rendered, int Failed, TimeSpan Elapsed, IReadOnlyList<GenerationEvent> Events);

    private readonly string _artefactDir;
    private readonly string _outputRoot;

    private NuxtPrerender(string artefactDir, string outputRoot)
    {
        _artefactDir = artefactDir;
        _outputRoot = outputRoot;
    }

    // ── Artefact resolution ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Resolves the renderer artefact directory (the one CONTAINING <c>server/index.mjs</c>), in order:
    /// <list type="number">
    /// <item><c>SPECSCRIBE_RENDERER_DIR</c> — the explicit override.</item>
    /// <item><c>renderer/</c> beside the executing assembly — the Epic 16 packaging shape.</item>
    /// <item><c>web/.output/</c> relative to the repo root — the developer path.</item>
    /// </list>
    /// <para>A miss is an actionable error naming ALL THREE, never a silent skip: a skipped prerender would leave
    /// an output root with an IR and no pages, and <c>errors=0</c> would call that a success.</para></summary>
    public static string ResolveArtefactDirectory(string? startDirectory = null)
    {
        var candidates = new List<(string Why, string Path)>();

        var env = Environment.GetEnvironmentVariable("SPECSCRIBE_RENDERER_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            // ⚠️ An EXPLICIT override that does not resolve is a hard error, NOT a fallback.
            //
            // Falling through to the next candidate would render with a different artefact than the one the
            // operator named, and report success — a wrong answer with a success status, which is the same class
            // of failure Story 23.5 hit when Nitro served a baked project's pages ahead of the SSR route. If you
            // point SpecScribe at a renderer, that is the renderer it uses or it stops.
            var explicitDir = Path.GetFullPath(env);
            if (!File.Exists(Path.Combine(explicitDir, "server", "index.mjs")))
            {
                throw new InvalidOperationException(
                    $"SPECSCRIBE_RENDERER_DIR is set to '{explicitDir}', but there is no 'server/index.mjs' "
                    + "under it, so it is not a renderer artefact.\n\n"
                    + "This is a hard failure rather than a fallback: rendering with a DIFFERENT artefact than "
                    + "the one you named would silently produce output you did not ask for.\n\n"
                    + "Build one with:  cd web && npm run build:package\n"
                    + "Or unset SPECSCRIBE_RENDERER_DIR to use the default search "
                    + "(renderer/ beside the executable, then web/.output/ in the repo).");
            }
            return explicitDir;
        }

        var beside = Path.Combine(AppContext.BaseDirectory, "renderer");
        candidates.Add(("renderer/ beside the executable", beside));

        var repoRoot = FindRepoRoot(startDirectory ?? Directory.GetCurrentDirectory());
        // ⚠️ Recorded as a candidate even when there is NO repo root, with the reason in place of a path. It was
        // originally added only when a root was found, which meant a user running outside a git checkout — the
        // most likely person to be missing the artefact — got an error naming two locations out of three and no
        // hint about the developer path. AC #4 asks for an ACTIONABLE failure, and a silently shortened list of
        // places-we-looked is not one.
        candidates.Add((
            "web/.output/ in the repo (developer path)",
            repoRoot is null
                ? "(skipped — no git repository above the working directory)"
                : Path.Combine(repoRoot, "web", ".output")));

        foreach (var (_, path) in candidates)
        {
            if (Path.IsPathFullyQualified(path) && File.Exists(Path.Combine(path, "server", "index.mjs"))) return path;
        }

        var tried = string.Join("\n", candidates.Select(c => $"  · {c.Why}\n      {c.Path}"));
        throw new InvalidOperationException(
            "The SpecScribe renderer artefact could not be found, so no HTML can be produced.\n\n"
            + $"Looked for 'server/index.mjs' under, in order:\n{tried}\n\n"
            + "Build it with:  cd web && npm run build:package\n"
            + "Or point SPECSCRIBE_RENDERER_DIR at a directory that already contains one.");
    }

    private static string? FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName;
    }

    // ── Node prerequisite (AC #4, ADR 0022 §Decision 5) ─────────────────────────────────────────────────────

    /// <summary>Verifies Node is on PATH and inside <see cref="SupportedNodeRange"/>.
    /// <para>ADR 0022 §Decision 5 assigned Node DETECTION to Story 16.3, which has not been built — every
    /// <c>16-*</c> key is still backlog. Until it is, this is the check, and it exists because the alternative
    /// is the failure AC #4 names by name: a silent empty output root reported at <c>errors=0</c>.</para>
    /// <para>Returns the resolved version string. Throws with an actionable message naming the range otherwise.</para></summary>
    public static string VerifyNodeAvailable()
    {
        string output;
        try
        {
            var psi = new ProcessStartInfo("node", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null for 'node --version'.");
            output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(15_000);
            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException(NodeMissingMessage($"'node --version' exited {proc.ExitCode}."));
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(NodeMissingMessage("'node' was not found on PATH."), ex);
        }

        ValidateNodeVersion(output);
        return output;
    }

    /// <summary>Parses <c>node --version</c>'s output and throws when it is unparseable or out of range.
    /// <para>Split out from <see cref="VerifyNodeAvailable"/> so the OUT-OF-RANGE message — the one a user with
    /// an old Node actually reads — is directly testable. It cannot be reached by shimming <c>node</c> on PATH:
    /// <see cref="Process"/> with <c>UseShellExecute=false</c> resolves a real executable, not a <c>.cmd</c>
    /// wrapper, so a shim-based test silently exercises the ABSENT path instead and passes for the wrong
    /// reason. (Real Node ships <c>node.exe</c> on Windows, so that resolution behaviour is correct in
    /// production — it is only the test double that cannot be built that way.)</para></summary>
    internal static void ValidateNodeVersion(string output)
    {
        var m = NodeVersionPattern.Match(output);
        if (!m.Success)
        {
            throw new InvalidOperationException(NodeMissingMessage($"'node --version' printed '{output}', which is not a version."));
        }

        var major = int.Parse(m.Groups["major"].Value);
        var minor = int.Parse(m.Groups["minor"].Value);
        var patch = int.Parse(m.Groups["patch"].Value);
        if (!IsSupported(major, minor, patch))
        {
            throw new InvalidOperationException(
                $"SpecScribe requires Node {SupportedNodeRange}, but found {output}.\n\n"
                + "SpecScribe renders its HTML with a prebuilt JavaScript renderer, so Node is a required "
                + "prerequisite for `generate` (ADR 0022 §Decision 5). Without a supported Node, SpecScribe can "
                + "emit the JSON intermediate representation but cannot produce any HTML page.\n"
                + "  https://nodejs.org/");
        }
    }

    /// <summary>`^22.19.0 || ^24.11.0 || &gt;=26.0.0`, expanded. Caret means "same major, at or above this
    /// minor/patch", so 22.18.x and 24.10.x are BELOW range while 23.x and 25.x are outside it entirely.</summary>
    internal static bool IsSupported(int major, int minor, int patch)
    {
        if (major >= 26) return true;
        if (major == 22) return minor > 19 || (minor == 19 && patch >= 0);
        if (major == 24) return minor > 11 || (minor == 11 && patch >= 0);
        return false;
    }

    private static string NodeMissingMessage(string detail) =>
        $"Node is required to generate a SpecScribe site, and it could not be run ({detail}).\n\n"
        + $"SpecScribe renders its HTML with a prebuilt JavaScript renderer, so Node {SupportedNodeRange} "
        + "is a prerequisite for `generate` (ADR 0022 §Decision 5). Without it SpecScribe can emit the JSON "
        + "intermediate representation but cannot produce any HTML page.\n"
        + "  https://nodejs.org/";

    // ── Driving the prerender ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Boots the artefact against <paramref name="outputRoot"/>'s IR, renders <paramref name="routes"/>,
    /// writes each response to its output-relative <c>.html</c> path, and copies the artefact's static assets.
    /// <para>The server is killed in a <c>finally</c>, so a failed generate cannot leak one.</para></summary>
    public static Result Render(string outputRoot, IReadOnlyList<string> routes, string? artefactDir = null, bool copyAssets = true)
    {
        // ⚠️ ORDER IS DELIBERATE: Node FIRST, then the artefact.
        //
        // Both are prerequisites, but they are not equally informative. A user with no Node who is told "the
        // renderer artefact could not be found" will go and build the artefact — and then hit the real problem
        // one step later, having been pointed at the wrong thing. ADR 0022 §Decision 5 promises an error naming
        // the supported Node range; that promise is only kept if the Node check runs before anything that can
        // fail first. Found by running a generate on a PATH with no Node and reading what the user actually got.
        VerifyNodeAvailable();
        // Resolved from the CURRENT DIRECTORY, never from the output root: `--output` routinely points at a temp
        // or publish directory outside the checkout, and walking up from THERE reports "no git repository above
        // the working directory" while the developer artefact sits happily in the repo the user ran from.
        var dir = artefactDir ?? ResolveArtefactDirectory();
        return new NuxtPrerender(dir, outputRoot).RenderCore(routes, copyAssets);
    }

    private Result RenderCore(IReadOnlyList<string> routes, bool copyAssets)
    {
        var sw = Stopwatch.StartNew();
        var events = new List<GenerationEvent>();
        var rendered = 0;
        var failed = 0;

        var port = FreePort();
        var psi = new ProcessStartInfo(NodeExecutable(), Path.Combine(_artefactDir, "server", "index.mjs"))
        {
            WorkingDirectory = _artefactDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["SPECSCRIBE_IR_DIR"] = Path.GetFullPath(_outputRoot);
        // Must NOT leak into the server: it stubs the manifest EMPTY, which is correct for BUILDING the artefact
        // and catastrophic for SERVING with it — every route would render an empty shell at HTTP 200.
        psi.Environment["SPECSCRIBE_PACKAGE_BUILD"] = string.Empty;
        psi.Environment["PORT"] = port.ToString();
        psi.Environment["NITRO_PORT"] = port.ToString();

        Process? proc = null;
        var serverLog = new List<string>();
        try
        {
            proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Process.Start returned null for the renderer.");
            proc.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (serverLog) serverLog.Add(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (serverLog) serverLog.Add(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60), BaseAddress = new Uri($"http://127.0.0.1:{port}/") };
            WaitForReady(proc, http, serverLog);

            foreach (var route in routes)
            {
                var routeSw = Stopwatch.StartNew();
                string? failure = null;
                string? body = null;
                try
                {
                    var res = http.GetAsync(route).GetAwaiter().GetResult();
                    body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (res.StatusCode != HttpStatusCode.OK)
                    {
                        failure = $"the renderer answered HTTP {(int)res.StatusCode} for a route the manifest names.";
                    }
                    else if (!body.Contains(MainLandmark, StringComparison.Ordinal))
                    {
                        // Anchored on the FULL landmark, never on `<main` — this portal renders its own source,
                        // so a loose probe matches prose. See the type doc.
                        failure = "the renderer answered 200 with no <main id=\"main-content\"> landmark — an empty shell.";
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    failure = $"the request failed: {ex.Message}";
                }

                routeSw.Stop();
                if (failure is not null)
                {
                    failed++;
                    // A reported error, never a silently missing page (AC #7). The diagnostics page is the surface.
                    events.Add(new GenerationEvent(GenerationOutcome.Error, route, routeSw.Elapsed, failure));
                    continue;
                }

                var target = Path.Combine(_outputRoot, route.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, body!);
                rendered++;
            }

            if (copyAssets) CopyArtefactAssets(events);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            events.Add(new GenerationEvent(GenerationOutcome.Error, "(renderer)", sw.Elapsed, ex.Message));
            failed++;
        }
        finally
        {
            TryKill(proc);
        }

        sw.Stop();
        return new Result(rendered, failed, sw.Elapsed, events);
    }

    private static string NodeExecutable() => "node";

    private static void WaitForReady(Process proc, HttpClient http, List<string> serverLog)
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            if (proc.HasExited)
            {
                throw new InvalidOperationException(
                    $"The renderer exited before it listened (code {proc.ExitCode}).\n{Tail(serverLog)}");
            }
            try
            {
                // ANY response means it is listening — including a 500. Waiting for a healthy status hangs for
                // the whole timeout on a project whose entry page legitimately fails to render.
                using var probe = new HttpClient { Timeout = TimeSpan.FromSeconds(2), BaseAddress = http.BaseAddress };
                probe.GetAsync(string.Empty).GetAwaiter().GetResult();
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // not listening yet
            }
            if (DateTime.UtcNow > deadline)
            {
                throw new InvalidOperationException($"The renderer did not listen within 60 s.\n{Tail(serverLog)}");
            }
            Thread.Sleep(150);
        }
    }

    private static string Tail(List<string> log)
    {
        lock (log) return string.Join("\n", log.TakeLast(40));
    }

    private static void TryKill(Process? proc)
    {
        if (proc is null) return;
        try
        {
            if (!proc.HasExited) proc.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { /* already gone */ }
        catch (NotSupportedException) { }
        finally
        {
            proc.Dispose();
        }
    }

    /// <summary>An OS-assigned free port. Bind-then-release rather than a fixed number, so two generates (or a
    /// generate beside a running watch) cannot collide.</summary>
    private static int FreePort()
    {
        using var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    /// <summary>Copies the artefact's own static assets (its <c>_nuxt/</c> chunks and anything else under
    /// <c>public/</c>) into the output root.
    ///
    /// <para><b>Asset ownership, decided here rather than left to drift (AC #7).</b> Four files exist in BOTH
    /// places today — <c>specscribe.css</c>, <c>specscribe.js</c>, <c>prism.js</c> and
    /// <c>plotly-hierarchy.min.js</c> — because <c>web/scripts/sync-runtime-assets.mjs</c> copies them from
    /// <c>src/SpecScribe/assets/</c> into <c>web/public/</c>, from where the artefact build bakes them in.
    /// <b>C# remains the single writer of those four.</b> They are embedded resources in this assembly, the sync
    /// script already treats C# as authoritative, and the webview and SPA delivery paths still need C# to place
    /// them — so making the artefact a second writer would be the exact drift this epic exists to end.</para>
    ///
    /// <para>Mechanically: <c>CopyEmbeddedAsset</c> has already run by the time the prerender does, so this copy
    /// SKIPS any file that already exists. One writer per file, no clobbering, and the order is deterministic
    /// rather than incidental.</para></summary>
    private void CopyArtefactAssets(List<GenerationEvent> events)
    {
        var publicDir = Path.Combine(_artefactDir, "public");
        if (!Directory.Exists(publicDir)) return;

        foreach (var source in Directory.EnumerateFiles(publicDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(publicDir, source);
            var target = Path.Combine(_outputRoot, relative);
            if (File.Exists(target)) continue; // C# owns it — see the ownership note above.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                events.Add(new GenerationEvent(
                    GenerationOutcome.Error,
                    PathUtil.NormalizeSlashes(relative),
                    TimeSpan.Zero,
                    $"could not copy the renderer asset: {ex.Message}"));
            }
        }
    }
}
