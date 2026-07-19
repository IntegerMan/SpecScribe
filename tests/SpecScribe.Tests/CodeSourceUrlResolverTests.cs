using SpecScribe;

namespace SpecScribe.Tests;

/// <summary>Unit coverage for Story 7.7's external-source-base detection: parsing every common git remote shape into
/// a platform "blob" base, the GitHub Actions / Pages environment shortcut, and the CI-over-remote precedence. The
/// string parsers are pure, so they're exercised directly (no git, no network).</summary>
public class CodeSourceUrlResolverTests
{
    [Theory]
    // HTTPS, with and without the .git suffix.
    [InlineData("https://github.com/owner/repo.git", "main", "https://github.com/owner/repo/blob/main")]
    [InlineData("https://github.com/owner/repo", "dev", "https://github.com/owner/repo/blob/dev")]
    // HTTPS with embedded credentials — userinfo is dropped.
    [InlineData("https://user:token@github.com/owner/repo.git", "main", "https://github.com/owner/repo/blob/main")]
    // SSH scp-like form.
    [InlineData("git@github.com:owner/repo.git", "feature/x", "https://github.com/owner/repo/blob/feature/x")]
    // ssh:// URI form.
    [InlineData("ssh://git@github.com/owner/repo.git", "main", "https://github.com/owner/repo/blob/main")]
    // GitLab uses /-/blob/ and keeps subgroups in the owner path.
    [InlineData("https://gitlab.com/group/subgroup/repo.git", "main", "https://gitlab.com/group/subgroup/repo/-/blob/main")]
    [InlineData("git@gitlab.com:group/repo.git", "main", "https://gitlab.com/group/repo/-/blob/main")]
    public void FromRemoteUrl_BuildsBlobBase(string remote, string branch, string expected)
    {
        Assert.Equal(expected, CodeSourceUrlResolver.FromRemoteUrl(remote, branch));
    }

    [Fact]
    public void FromRemoteUrl_NullOrDetachedBranch_FallsBackToMain()
    {
        Assert.Equal("https://github.com/owner/repo/blob/main",
            CodeSourceUrlResolver.FromRemoteUrl("https://github.com/owner/repo.git", branch: null));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("https://github.com/owner")]   // no repo segment
    [InlineData("")]
    public void FromRemoteUrl_UnparseableInput_ReturnsNull(string remote)
    {
        Assert.Null(CodeSourceUrlResolver.FromRemoteUrl(remote, "main"));
    }

    [Fact]
    public void FromRemoteUrl_Bitbucket_UsesSrcNotBlob()
    {
        // Bitbucket Cloud's real file-view path is /src/{branch}/{path}, not GitHub's /blob/.
        Assert.Equal("https://bitbucket.org/owner/repo/src/main",
            CodeSourceUrlResolver.FromRemoteUrl("https://bitbucket.org/owner/repo.git", "main"));
    }

    [Theory]
    // Self-hosted / unrecognized hosts degrade to null (AC1: unrecognizable remote -> in-portal-only, no error)
    // rather than a fabricated, likely-broken GitHub-shaped link.
    [InlineData("https://git.example-internal.com/owner/repo.git")]
    [InlineData("https://dev.azure.com/org/project/_git/repo")]
    [InlineData("git@codeberg.internal:owner/repo.git")]
    public void FromRemoteUrl_UnrecognizedHost_DegradesToNull(string remote)
    {
        Assert.Null(CodeSourceUrlResolver.FromRemoteUrl(remote, "main"));
    }

    [Fact]
    public void FromRemoteUrl_BranchWithSpecialCharacters_IsPercentEncoded()
    {
        Assert.Equal("https://github.com/owner/repo/blob/feature/weird%20name%23tag",
            CodeSourceUrlResolver.FromRemoteUrl("https://github.com/owner/repo.git", "feature/weird name#tag"));
    }

    [Theory]
    // IPv6 literal hosts are bracketed; a bare first-colon split would truncate the host to garbage.
    [InlineData("git@[::1]:owner/repo.git")]
    [InlineData("https://[::1]/owner/repo.git")]
    public void FromRemoteUrl_Ipv6Host_DegradesToNullRatherThanGarbage(string remote)
    {
        // Neither github/gitlab/bitbucket recognizes an IPv6-literal host, so this degrades to null either way —
        // the point of this test is that it does NOT produce a URL built from a mangled host like "[".
        Assert.Null(CodeSourceUrlResolver.FromRemoteUrl(remote, "main"));
    }

    [Theory]
    // A query string or fragment on the remote itself (rare, but seen on some self-hosted/proxied remotes) must
    // not leak into the parsed repo name. [Story 7.7 deferred fix]
    [InlineData("https://github.com/owner/repo.git?ref=x", "repo")]
    [InlineData("https://github.com/owner/repo.git#readme", "repo")]
    [InlineData("https://github.com/owner/repo?ref=x", "repo")]
    public void ParseRemote_QueryOrFragmentOnRemote_DoesNotLeakIntoRepoName(string remote, string expectedRepo)
    {
        var parsed = CodeSourceUrlResolver.ParseRemote(remote);
        Assert.NotNull(parsed);
        Assert.Equal(expectedRepo, parsed!.Value.Repo);
    }

    [Fact]
    public void FromRemoteUrl_QueryStringOnRemote_BuildsCleanBlobBase()
    {
        Assert.Equal("https://github.com/owner/repo/blob/main",
            CodeSourceUrlResolver.FromRemoteUrl("https://github.com/owner/repo.git?ref=x", "main"));
    }

    [Fact]
    public void ParseRemote_Ipv6HostWithPort_StripsOnlyThePort()
    {
        var parsed = CodeSourceUrlResolver.ParseRemote("ssh://git@[::1]:2222/owner/repo.git");
        Assert.NotNull(parsed);
        Assert.Equal("[::1]", parsed!.Value.Host);
    }

    [Fact]
    public void EscapeUrlSegments_PreservesSlashesButEncodesEachSegment()
    {
        Assert.Equal("src/My%20File%23One.cs", CodeSourceUrlResolver.EscapeUrlSegments("src/My File#One.cs"));
    }

    [Fact]
    public void FromCiEnvironment_GitHubActions_PinsToImmutableSha()
    {
        var env = new Dictionary<string, string?>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["GITHUB_SERVER_URL"] = "https://github.com",
            ["GITHUB_REPOSITORY"] = "owner/repo",
            ["GITHUB_SHA"] = "abc123",
            ["GITHUB_REF_NAME"] = "main",
        };

        // The commit SHA is preferred over the ref name so the deployed link never rots as the branch moves.
        Assert.Equal("https://github.com/owner/repo/blob/abc123", CodeSourceUrlResolver.FromCiEnvironment(env));
    }

    [Fact]
    public void FromCiEnvironment_NoShaFallsBackToRefNameThenMain()
    {
        var noSha = new Dictionary<string, string?>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["GITHUB_REPOSITORY"] = "owner/repo",
            ["GITHUB_REF_NAME"] = "release",
        };
        Assert.Equal("https://github.com/owner/repo/blob/release", CodeSourceUrlResolver.FromCiEnvironment(noSha));

        var nothing = new Dictionary<string, string?>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["GITHUB_REPOSITORY"] = "owner/repo",
        };
        Assert.Equal("https://github.com/owner/repo/blob/main", CodeSourceUrlResolver.FromCiEnvironment(nothing));
    }

    [Fact]
    public void FromCiEnvironment_NotInCiOrNoRepo_ReturnsNull()
    {
        Assert.Null(CodeSourceUrlResolver.FromCiEnvironment(new Dictionary<string, string?>()));
        Assert.Null(CodeSourceUrlResolver.FromCiEnvironment(new Dictionary<string, string?> { ["GITHUB_ACTIONS"] = "false" }));
        Assert.Null(CodeSourceUrlResolver.FromCiEnvironment(new Dictionary<string, string?> { ["GITHUB_ACTIONS"] = "true" }));
    }

    [Fact]
    public void TryDetect_CiContextWins_WithoutTouchingGit()
    {
        // A bogus repo root proves CI detection short-circuits before any git call (which would find no remote here).
        var env = new Dictionary<string, string?>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["GITHUB_REPOSITORY"] = "owner/repo",
            ["GITHUB_SHA"] = "deadbeef",
        };
        Assert.Equal("https://github.com/owner/repo/blob/deadbeef",
            CodeSourceUrlResolver.TryDetect(repoRoot: "/nonexistent", env: env));
    }
}
