using System.Text.RegularExpressions;

namespace SpecScribe;

/// <summary>Detects the base URL for "view source online" links (Story 7.7) when the user has not passed
/// <c>--code-url</c>. Precedence, highest first: an explicit CI/Pages deployment context (the environment variables
/// GitHub Actions injects — deterministic there), then the local git <c>origin</c> remote. It produces a platform
/// "blob" base like <c>https://github.com/owner/repo/blob/main</c>, to which a code page appends
/// <c>/&lt;repo-relative-path&gt;</c>. Pure and failure-tolerant: any gap (no remote, unrecognized host, no git,
/// detached HEAD) yields null and the site simply renders in-portal only — the external link is additive, never
/// required. The URL parsing (<see cref="FromRemoteUrl"/>, <see cref="FromCiEnvironment"/>) is split from the git/env
/// I/O so it is unit-testable on raw strings.</summary>
public static class CodeSourceUrlResolver
{
    // SSH scp-like form: git@host:owner/repo(.git) — the one remote shape that is not a normal URI.
    private static readonly Regex ScpLike = new(
        @"^(?<user>[^@]+)@(?<host>[^:]+):(?<path>.+)$", RegexOptions.Compiled);

    /// <summary>Best-effort detection from the environment: CI context first, then the git remote at
    /// <paramref name="repoRoot"/>. <paramref name="env"/> is injectable for tests; defaults to the process
    /// environment.</summary>
    public static string? TryDetect(string repoRoot, IReadOnlyDictionary<string, string?>? env = null)
    {
        env ??= ReadProcessEnv();

        var fromCi = FromCiEnvironment(env);
        if (fromCi is not null) return fromCi;

        var remote = GitMetrics.TryGetRemoteUrl(repoRoot);
        if (remote is null) return null;
        // Prefer the actually-checked-out branch; fall back to the remote's default branch (its origin/HEAD
        // symref) on detached HEAD, rather than guessing "main" for repos that still default to "master"/"trunk".
        var branch = GitMetrics.TryGetCurrentBranch(repoRoot) ?? GitMetrics.TryGetDefaultBranch(repoRoot);
        return FromRemoteUrl(remote, branch);
    }

    /// <summary>Builds a blob base from the variables GitHub Actions sets (<c>GITHUB_ACTIONS</c>,
    /// <c>GITHUB_SERVER_URL</c>, <c>GITHUB_REPOSITORY</c>, <c>GITHUB_SHA</c>/<c>GITHUB_REF_NAME</c>). This makes a site
    /// deployed to GitHub Pages link out automatically without any flag. Pins to the immutable commit SHA when present
    /// (so the link never rots as the branch moves), falling back to the ref name and then <c>main</c>. Returns null
    /// when not running in that CI context.</summary>
    public static string? FromCiEnvironment(IReadOnlyDictionary<string, string?> env)
    {
        if (!IsTruthy(Get(env, "GITHUB_ACTIONS"))) return null;

        var repo = Get(env, "GITHUB_REPOSITORY");        // "owner/repo"
        if (string.IsNullOrWhiteSpace(repo)) return null;

        var server = Get(env, "GITHUB_SERVER_URL");
        if (string.IsNullOrWhiteSpace(server)) server = "https://github.com";

        var reff = Get(env, "GITHUB_SHA");
        if (string.IsNullOrWhiteSpace(reff)) reff = Get(env, "GITHUB_REF_NAME");
        if (string.IsNullOrWhiteSpace(reff)) reff = "main";

        return $"{server.TrimEnd('/')}/{repo.Trim('/')}/blob/{EscapeUrlSegments(reff)}";
    }

    /// <summary>Turns a git remote URL into a blob base — but ONLY for a recognized hosting platform (AC1: "an
    /// unrecognizable or absent remote degrades to in-portal-only with no error"). Handles HTTPS
    /// (<c>https://github.com/owner/repo(.git)</c>, including embedded credentials), SSH
    /// (<c>git@github.com:owner/repo.git</c>), and <c>ssh://</c>/<c>git://</c> URIs. GitHub(-Enterprise)'s and
    /// Bitbucket's blob paths differ (<c>/blob/</c> vs <c>/src/</c>); GitLab's is <c>/-/blob/</c>. A host that
    /// matches none of the three returns null rather than guessing a URL scheme that would 404.</summary>
    public static string? FromRemoteUrl(string remoteUrl, string? branch)
    {
        var parsed = ParseRemote(remoteUrl);
        if (parsed is null) return null;

        var (host, owner, repo) = parsed.Value;
        var b = EscapeUrlSegments(string.IsNullOrWhiteSpace(branch) ? "main" : branch.Trim());

        if (host.Contains("gitlab", StringComparison.OrdinalIgnoreCase))
            return $"https://{host}/{owner}/{repo}/-/blob/{b}";
        if (host.Contains("bitbucket", StringComparison.OrdinalIgnoreCase))
            return $"https://{host}/{owner}/{repo}/src/{b}";
        if (host.Contains("github", StringComparison.OrdinalIgnoreCase))
            return $"https://{host}/{owner}/{repo}/blob/{b}";

        // Unrecognized host (self-hosted Gitea/Gogs/sourcehut/Azure DevOps/etc.) — no URL scheme we can trust,
        // so degrade to in-portal-only rather than fabricate a link that would 404.
        return null;
    }

    /// <summary>Percent-encodes each <c>/</c>-delimited segment of a URL path/branch component (never the
    /// separators themselves), so a branch or repo-relative path containing a space, <c>#</c>, <c>?</c>, or
    /// non-ASCII character produces a link that actually resolves instead of silently truncating at the browser.</summary>
    public static string EscapeUrlSegments(string value) =>
        string.Join('/', value.Split('/').Select(Uri.EscapeDataString));

    /// <summary>Extracts <c>(host, owner, repo)</c> from any supported remote form. <c>owner</c> keeps intermediate
    /// path segments so GitLab subgroups (<c>group/subgroup/repo</c>) survive; <c>repo</c> is the final segment with
    /// any <c>.git</c> suffix stripped. Returns null when host or path can't be recovered.</summary>
    public static (string Host, string Owner, string Repo)? ParseRemote(string remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl)) return null;
        var url = remoteUrl.Trim();

        string host;
        string path;

        var scp = ScpLike.Match(url);
        if (scp.Success && !url.Contains("://", StringComparison.Ordinal))
        {
            host = scp.Groups["host"].Value;
            path = scp.Groups["path"].Value;
        }
        else
        {
            var schemeIdx = url.IndexOf("://", StringComparison.Ordinal);
            var afterScheme = schemeIdx >= 0 ? url[(schemeIdx + 3)..] : url;

            // Drop any embedded userinfo (user[:pass]@host).
            var at = afterScheme.IndexOf('@');
            if (at >= 0) afterScheme = afterScheme[(at + 1)..];

            var slash = afterScheme.IndexOf('/');
            if (slash < 0) return null;
            host = afterScheme[..slash];
            path = afterScheme[(slash + 1)..];
        }

        host = host.Trim();
        // Strip a trailing :port so the blob host stays clean (rare for public hosts, but ssh:// URIs carry it).
        // An IPv6 literal host is bracketed ("[::1]:22") — its internal colons are not a port separator, so only
        // look for a port after the closing bracket; a missing bracket close is malformed, not a stray port.
        if (host.StartsWith('['))
        {
            var closeBracket = host.IndexOf(']');
            if (closeBracket < 0) return null;
            host = host[..(closeBracket + 1)];
        }
        else
        {
            var colon = host.IndexOf(':');
            if (colon >= 0) host = host[..colon];
        }

        // A query string or fragment on the remote itself (e.g. "repo.git?ref=x" from an unusual host config)
        // is not part of the repo path — strip it before segmenting, or it rides along into the parsed repo name.
        var queryOrFragment = path.IndexOfAny(['?', '#']);
        if (queryOrFragment >= 0) path = path[..queryOrFragment];

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || host.Length == 0) return null;

        var repo = segments[^1];
        if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) repo = repo[..^4];
        if (repo.Length == 0) return null;

        var owner = string.Join('/', segments[..^1]);
        return (host, owner, repo);
    }

    private static bool IsTruthy(string? value) =>
        value is not null &&
        (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static string? Get(IReadOnlyDictionary<string, string?> env, string key) =>
        env.TryGetValue(key, out var v) ? v : null;

    private static IReadOnlyDictionary<string, string?> ReadProcessEnv()
    {
        var keys = new[]
        {
            "GITHUB_ACTIONS", "GITHUB_SERVER_URL", "GITHUB_REPOSITORY", "GITHUB_SHA", "GITHUB_REF_NAME",
        };
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var k in keys) map[k] = Environment.GetEnvironmentVariable(k);
        return map;
    }
}
