namespace SpecScribe;

/// <summary>One sanctioned cross-surface divergence: a named semantic fact (<paramref name="FactId"/>) that a
/// specific surface (<paramref name="SurfaceId"/>) is ALLOWED to render differently from the shared view model,
/// with a documented <paramref name="Reason"/>. This is the ONLY legitimate way a surface may diverge (AC #2:
/// "differences are documented as host-specific exceptions only"). A divergence the parity harness finds that is
/// NOT registered here is a BUG, not an exception. Story 6.1 registers none — the HTML adapter reproduces every
/// fact; Story 6.2's webview registers here rather than drifting silently. [Story 6.1]</summary>
/// <param name="SurfaceId">The <see cref="IRenderAdapter.Id"/> the exception applies to (e.g. <c>webview</c>).</param>
/// <param name="FactId">The semantic-fact id (see <see cref="RenderParity"/>'s fact ids, e.g. <c>asset.css</c>).</param>
/// <param name="Reason">Why this surface legitimately diverges on this fact.</param>
public sealed record HostRenderException(string SurfaceId, string FactId, string Reason);

/// <summary>The single documented home for sanctioned cross-surface divergence — the registry AC #2's parity
/// checks consult. EMPTY in Story 6.1 (the HTML adapter is the only surface and drops/reinterprets nothing), so
/// the harness treats every divergence as a regression. A future surface adds its host-specific exceptions here
/// so they are visible and reviewed, never silent drift. [Story 6.1]</summary>
public static class HostRenderExceptions
{
    /// <summary>The sanctioned divergences. Empty this story.</summary>
    public static readonly IReadOnlyList<HostRenderException> Registry = Array.Empty<HostRenderException>();
}
