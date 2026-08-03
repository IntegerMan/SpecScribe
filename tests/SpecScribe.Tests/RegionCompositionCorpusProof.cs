// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════
// RETIRED by Story 23.6 (owner decision D3, AC #2). 146 lines removed; this record replaces them.
//
// ── What it proved ─────────────────────────────────────────────────────────────────────────────────────────
//
// `RegionCompositionCorpusProof` was the REAL-CORPUS half of Story 23.4 AC #3's byte-equality proof — the half
// `RegionCompositionParityTests` structurally could not give. That fixture cited no real repo files, so it
// emitted no `code/` and no `commit/` page and missed `CodeFileTemplater`'s 254 pages and
// `CommitDetailTemplater`'s 300 — together ~40 % of the site. This one ran a full `--deep-git --spa` generate
// against THIS repository's own artifacts and compared every composed region to its sliced oracle.
//
// Opt-in by design (`SPECSCRIBE_CORPUS_PROOF=1`): a deep-git generate takes ~65 s and shells out to
// `git log --numstat`, so it was a gate to run deliberately — before deleting the slice, and whenever a
// templater's body boundary moved — not a unit test.
//
// ── The evidence ───────────────────────────────────────────────────────────────────────────────────────────
//
// **1,469 pages, 0 unexpected deltas.** Recorded in Story 23.4's Dev Agent Record and quoted in Story 23.6's
// Dev Notes. That number is what licensed this story's deletion.
//
// It also carried its own anti-vacuity guard, worth preserving as a pattern: `GitMetrics` has a hard-coded
// 3,000 ms budget that `git log --numstat` has been measured to exceed (6,496 ms cold), and it loses SILENTLY
// at `errors=0`, taking `git-insights.html`, `deep-analytics.html`, `impact-map.html` and the whole `commit/`
// family with it. A run that quietly produced 1,100 pages instead of 1,408 would have reported "0 deltas" and
// meant nothing — so the proof asserted the page count and three named surfaces BEFORE trusting its own
// comparison. Any future corpus-scale gate should do the same.
//
// The one EXPECTED delta it pinned is the finding worth carrying forward: on `deep-analytics.html` the composed
// region is LARGER than the sliced one, because the slice truncated at `</main>` and dropped the
// `id="coupling-zoom"` lightbox target that sits after it. The `href="#coupling-zoom"` link shipped inside
// `<main>` and the target did not, so that link resolved to nothing in the SPA and the webview. Composition
// fixed it; the deletion of the slicer makes the whole failure mode unreachable.
//
// ── Why retired rather than re-pointed ─────────────────────────────────────────────────────────────────────
//
// Its subject is gone: Story 23.6 deletes the rendered document and `SpaDelivery.ExtractContentRegion`, which
// were the two things it compared. With `_spaCapture` removed it would have iterated nothing and reported
// "0 deltas" forever — VACUOUS, not red, which Story 23.6 AC #2 defines as a failure of that AC rather than a
// pass.
//
// ── What covers this ground now ────────────────────────────────────────────────────────────────────────────
//
// `npm run check:parity` (ADR 0033) over a frozen 24-route corpus spanning all 14 families, with a `mainSha`
// carrying this proof's C# lineage and a `pageSha` over the whole page — the first gate this project has had
// over the chrome. The lineage was re-verified live, 24/24, against the C# writer immediately before it was
// deleted, because after that there is no golden side left to verify against.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════
