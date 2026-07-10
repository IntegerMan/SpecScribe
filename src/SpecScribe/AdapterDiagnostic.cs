namespace SpecScribe;

/// <summary>How an <see cref="AdapterDiagnostic"/> should be understood — and how it maps onto the existing
/// <see cref="GenerationOutcome"/> reporting surface. Diagnostics are ALWAYS non-fatal: an adapter categorizes
/// and reports what it could not ingest, and the run continues so every successful artifact still renders
/// (AC #2, NFR2). [Story 4.1]</summary>
public enum AdapterDiagnosticCategory
{
    /// <summary>The artifact was discovered and recognized by name, but its shape isn't one the adapter can
    /// interpret (e.g. a <c>sprint-status.yaml</c> with no usable <c>development_status</c> map). Surfaces as
    /// a skip, not an error — the matching surface simply omits.</summary>
    Unsupported,

    /// <summary>The artifact should have parsed but the attempt failed (unreadable file, parser exception).
    /// Surfaces as an error event, matching how per-file parse failures already report today.</summary>
    Malformed,

    /// <summary>The artifact was deliberately not ingested (e.g. superseded by a sibling). Reserved for
    /// adapters that must choose between candidates; surfaces as a skip.</summary>
    Skipped,

    /// <summary>An ingestion failure that isn't tied to a single artifact's shape (I/O, environment). Still
    /// non-fatal to the run; surfaces as an error event.</summary>
    Error,
}

/// <summary>One categorized, non-fatal problem an <see cref="IArtifactAdapter"/> hit while ingesting a source
/// tree — the typed form of AC #2's "unsupported items are categorized and reported as non-fatal". These ride
/// the <see cref="ArtifactBundle.Diagnostics"/> channel and are surfaced by the generator on the existing
/// <see cref="GenerationEvent"/>/<see cref="IGenerationReporter"/> path (never a new console UI), with the
/// category deciding the outcome so a single failure is reported exactly once. [Story 4.1]</summary>
/// <param name="Category">What kind of problem this is; drives the <see cref="GenerationOutcome"/> mapping.</param>
/// <param name="RelativePath">The offending artifact, relative to the source root (matching the relative
/// paths <see cref="GenerationEvent"/> already reports).</param>
/// <param name="Message">Human-readable detail, e.g. the parser exception message.</param>
public sealed record AdapterDiagnostic(AdapterDiagnosticCategory Category, string RelativePath, string Message);
