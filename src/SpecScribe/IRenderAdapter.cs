namespace SpecScribe;

/// <summary>One host's rendered output for a <see cref="PageView"/>: the output-relative path plus the
/// host-specific artifact content (for the HTML surface, the full page string). [Story 6.1]</summary>
public sealed record RenderedArtifact(string OutputRelativePath, string Content);

/// <summary>The DELIVERY seam of the rendering architecture (AD-2 / the rendering sketch's
/// <c>IViewModelRenderer → IRenderAdapter</c>): given host-neutral <see cref="PageView"/>s, emit host-specific
/// artifacts. A delivery adapter TRANSLATES the shared view models into its host's output; it MUST NOT
/// reinterpret source artifacts (AD-1/AD-2) — all framework knowledge already lives behind the INGESTION seam
/// (<see cref="IArtifactAdapter"/>). Keep the two ideas distinct: <see cref="IArtifactAdapter"/> is
/// source → normalized records; <see cref="IRenderAdapter"/> is view models → host output. This contract is what
/// makes new surfaces additive (NFR4): a VS Code webview (Story 6.2) is a second <see cref="IRenderAdapter"/>,
/// not a core rewrite. Story 6.1 ships exactly one concrete adapter, <see cref="HtmlRenderAdapter"/>. [Story 6.1]</summary>
public interface IRenderAdapter
{
    /// <summary>A stable surface id (e.g. <c>html</c>) — the key a <see cref="HostRenderException"/> is scoped to,
    /// so a sanctioned divergence names the surface it applies to.</summary>
    string Id { get; }

    /// <summary>Renders one page's host-neutral view model into a host-specific artifact. Pure and deterministic
    /// apart from the generation timestamp the shared footer carries.</summary>
    RenderedArtifact Render(PageView page);
}
