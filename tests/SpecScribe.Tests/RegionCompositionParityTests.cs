// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════
// RETIRED by Story 23.6 (owner decision D3, AC #2). 176 lines removed; this record replaces them.
//
// ── What it proved ─────────────────────────────────────────────────────────────────────────────────────────
//
// `RegionCompositionParityTests` was Story 23.4 AC #3's byte-equality gate: the content region COMPOSED from a
// page's own `PageView` had to equal, byte for byte, the region SLICED out of that page's rendered document by
// `SpaDelivery.ExtractContentRegion`.
//
// It was that story's hinge. For ~1,217 of the site's 1,408 pages the IR was produced by the very code Story
// 23.4 retired — render the page, reference-linkify the WHOLE DOCUMENT, capture it at the write seam, cut the
// region back out — so deleting the writer without first standing up a composed-region producer would have
// taken the IR dark for 82 % of the site. It was also the only gate that could see its own failure class: a
// region that silently loses its reference links, its `<abbr>` expansions or its doc-header still renders a
// perfectly valid page and passes every other harness in the suite.
//
// ── The evidence, and where it lives ───────────────────────────────────────────────────────────────────────
//
// Green in the suite, and — the number that actually licensed the deletion — **1,469 pages with 0 unexpected
// deltas** on a real `--deep-git --spa` corpus run through `SiteGenerator.RegionCompositionDeltas()`. Recorded
// in Story 23.4's Dev Agent Record and quoted in Story 23.6's Dev Notes § "What Story 23.4 already did".
//
// This fixture alone was never sufficient, and said so: it cites no real repo files, so it emitted no `code/`
// and no `commit/` page and covered neither `CodeFileTemplater`'s 254 pages nor `CommitDetailTemplater`'s 300.
// `RegionCompositionCorpusProof` (retired in the same change) carried the real-corpus half.
//
// ── Why retired rather than re-pointed ─────────────────────────────────────────────────────────────────────
//
// Its job is finished and one of its two subjects no longer exists. Story 23.6 deletes both the rendered
// document and `SpaDelivery.ExtractContentRegion`; re-pointing the test would leave it comparing the composed
// region against itself.
//
// ⚠️ AND IT WOULD NOT HAVE GONE RED — IT WOULD HAVE GONE VACUOUS. With `_spaCapture` deleted, the comparison
// had no basis to iterate and would have reported zero deltas forever while asserting nothing. Story 23.6
// AC #2 names exactly that: "a gate that silently passes because its basis is empty is a failure of this AC,
// not a pass." That is why the file is emptied rather than skipped — the same discipline the project applied
// when `GoldenIrFingerprint` and `GoldenContentFingerprint` were removed.
//
// ── What covers this ground now ────────────────────────────────────────────────────────────────────────────
//
// `npm run check:parity` (ADR 0033), which renders a frozen 24-route corpus spanning all 14 families and
// compares two digests per route: `mainSha`, carrying this proof's C# lineage, and `pageSha` over the whole
// page. Its oracle was re-pinned with that lineage re-verified live, 24/24, against the C# writer while it
// still existed — the last moment at which that verification was possible.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════
