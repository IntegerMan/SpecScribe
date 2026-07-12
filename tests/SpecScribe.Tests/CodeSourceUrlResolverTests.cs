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
