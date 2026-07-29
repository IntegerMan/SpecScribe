# ADR 0020: A Module May Declare Non-Markdown Sources, Read by Exact Filename and Gated on a Schema Version

**Status:** **Accepted** — ratified 2026-07-29 at the Epic 18 retrospective (authored 2026-07-27 by Story 18.5). The owner resolved the one open question in the **opposite** direction to the shipped code — a higher schema major is now to be accepted with a warning, not skipped — so this ADR leads its implementation; see § Resolved question.
**Date:** 2026-07-27
**Deciders:** Matthew-Hope Eland
**Relates to:** [ADR 0015 — BMad Module Identity Is Open-World and Multi-Valued](0015-bmad-module-identity-open-world-and-multi-valued.md) (the module-code identity this ingest is gated on); [ADR 0021 — Foreign Artifacts May Be Carried Verbatim Into the Portal](0021-carrying-foreign-artifacts-verbatim-into-the-portal.md) (the sibling answer for foreign *pages*, where this is the answer for foreign *data*); [ADR 0002 — Shared Rendering Core and Host-Neutral View Models](0002-shared-rendering-core-and-host-neutral-view-models.md) (AD-2's source → normalized-records boundary, which this widens by one input format and no more); [ADR 0016 — The Canonical IR Carries Rendered Prose HTML](0016-ir-carries-rendered-prose-html.md); Epic 18 (Story 18.5)

**Numbering note.** `0019` is claimed-but-unwritten by **two** stories — Story 18.3's *"LLM-Generated Artifacts Are Enrichment-Only Inputs, Never Authoritative Ones"* and Story 22.3's IR-projection reconciliation. `0020` was pre-claimed by this story and is still free, so it is taken here. **This ADR deliberately does not decide 18.3's question**; §5 below states the enrichment-only constraint as something this decision *depends on and must not contradict*, so that when 0019 lands the two agree rather than compete.

## Context

SpecScribe discovers its source artifacts with exactly one glob:

```csharp
Directory.EnumerateFiles(_options.SourceRoot, "*.md", SearchOption.AllDirectories)
```

That single line has been the whole definition of "what SpecScribe can see" since Epic 1. Everything else — the `sprint-status.yaml` tracking ledger, the `_bmad/{code}/module-help.csv` command catalogs, the ADR tree — is read by a *named, targeted* path outside the source scan, never by widening it.

Story 18.5 hit the first case where that boundary loses information a user actively needs. The Test Architect module's `bmad-testarch-trace` workflow writes three files into `{test_artifacts}/`, which by its own `module.yaml` default resolves **inside** SpecScribe's source root:

| File | Ext | Seen today? |
|---|---|---|
| `traceability-matrix.md` | md | ✅ renders as a generic page |
| `gate-decision.json` | **json** | ❌ invisible |
| `e2e-trace-summary.json` | **json** | ❌ invisible |

`gate-decision.json` carries the actual `PASS | CONCERNS | FAIL | WAIVED` verdict — the single most decision-relevant thing that module produces. Under the `*.md` scan it is never discovered, never rendered, and **never diagnosed**: the run reports `errors=0` and the artifact simply does not exist as far as the portal is concerned. That is the same silent-loss class as Story 18.4's `forge-report.html`, and the same class as the GitMetrics timeout that dropped three surfaces at `errors=0`.

The tempting fix — widen the glob to `*.md;*.json` — is wrong, and it is worth being precise about why. A BMad source root routinely holds `package.json`, `tsconfig.json`, tool caches, lockfiles, coverage exports, and whatever else a project keeps beside its planning artifacts. A widened glob would ingest all of it, produce a page or a diagnostic for each, and make the portal's honesty about what it read strictly worse. The question is not *"can SpecScribe read JSON"* — `System.Text.Json` is in the BCL and `sprint-status.yaml` proves non-markdown reads are already fine. The question is **under what conditions a non-markdown file becomes a first-class source**, so the next module that ships a JSON summary has a rule to follow instead of a precedent to copy loosely.

## Decision

**1. The `*.md` source scan is unchanged, and stays the definition of a *document*.** Nothing in this ADR widens it. A non-markdown source is not a document; it is a **declared data input**, read on a separate, narrower path.

**2. A non-markdown source may be read only when all four conditions hold.** All four, not any:

- **Module-declared.** The filename is one the producing module's own on-disk workflow definition declares as an output. Not inferred, not pattern-matched, not "any JSON that looks like a report". For Story 18.5 the two names come from `bmad-testarch-trace/workflow.yaml`'s `gate_decision_output` and `e2e_trace_summary_output` keys.
- **Exact filename.** Matched by full name, case-insensitively — never by extension, glob, or prefix. `gate-decision.json` is a source; `playwright-report.json` sitting beside it is not.
- **Directory-scoped.** Read only inside the directory the module declares for its outputs, resolved by enumeration under the source root. Never a tree-wide walk for the filename.
- **Presence-gated on the module.** The module must actually be installed, established by ADR 0015's module-code check (`ModuleContext.IsModulePresent(repoRoot, code)`). A repo with a coincidentally-named file and no `_bmad/{code}/` install reads nothing at all.

**3. Every non-markdown source declares its schema version, and the parse is gated on the MAJOR component before any field is touched.** An unrecognized major yields a `Skipped` diagnostic and **no parse attempt** — never a best-effort read of a shape this build does not know. A file that will not say what shape it is, is not read. Concretely: both TEA files ship `"schema_version": "0.1.0"`; a `1.0.0` is skipped, a `0.2.x` is accepted.

This is the clause that makes the seam safe to extend. Reading a foreign module's file is a contract with an upstream project that moves independently and fast; without a version gate, an upstream shape change becomes a silent misparse — a *wrong* verdict on the dashboard, which is worse than an absent one.

**4. Failure is always non-fatal and always categorized, using the existing five-value vocabulary.** No sixth `AdapterDiagnosticCategory` is introduced:

| Situation | Category |
|---|---|
| Unrecognized `schema_version` major | `Skipped` |
| Present but will not parse | `Malformed` |
| Unreadable (IO, permissions) | `Error` |
| A declared-output family the product does not model | `Unsupported` |
| Module installed, its output directory not inside the scanned tree | `Informational` |

A file that fails to parse is still **listed as discovered** and labelled uninterpreted. Dropping the row would re-hide exactly the artifact this ADR exists to stop hiding.

**5. A non-markdown source is an ENRICHMENT input, never an authority.** Whatever it says layers on top of SpecScribe's own source-derived analysis and is visibly attributed to the module that produced it; it never overrides, replaces, or silently merges into a SpecScribe-derived figure. This is the same constraint Story 18.3 is expected to ratify as ADR 0019 for LLM-generated artifacts generally, and it applies here with full force — every TEA artifact is LLM-authored. **When 0019 lands, this clause defers to it.** It is stated here only so that a decision made in the meantime cannot contradict it.

Concretely, in Story 18.5: the module's traceability matrix may contribute rows to the requirement-coverage surface **only** where an oracle item resolves to an id SpecScribe itself defines, and only where the module's own declared coverage basis says its items are formal acceptance criteria. Everything else renders as its own attributed dimension with the reason stated in words. An honest gap beats a fabricated link.

**6. Explicit non-goals.** This is emphatically **not**:

- a general "ingest any JSON" seam, nor a widened source glob;
- a schema registry, a plugin format, or a user-configurable source-type list;
- permission to read a module's `config.yaml` or skill TOML to *resolve* a non-default output path. That remains an open cross-cutting question shared with Story 18.4's identical `forge_output_path` gap; under an overridden path the outcome is one `Informational` notice and nothing else;
- a route into the canonical IR (ADR 0016) for the raw file. What travels is the **normalized records** parsed from it, exactly as AD-2 requires; the bytes stay on disk.

## Consequences

**Good.**

- The most decision-relevant artifact a covered module produces stops being invisible at `errors=0`. Absence becomes a *stated* outcome instead of a silent one.
- The four conditions in §2 are each independently falsifiable, so "may SpecScribe read this file?" has a checkable answer rather than a judgement call.
- The version gate in §3 converts an upstream shape change from a wrong answer into a visible skip — the failure mode a portal about honesty can actually live with.
- Zero new dependencies: `System.Text.Json` is in the BCL, consistent with ADR 0010's zero-dependency posture for tooling.

**Costs, stated plainly.**

- **This is a second discovery path.** "What SpecScribe reads" is no longer one glob; it is one glob plus a bounded, module-gated set. That is genuinely more to hold in your head, and the mitigation is only that the second path is narrow and its bounds are written down here.
- **Per-module filename knowledge now lives in `src/`.** Story 18.5 pins TEA's filenames as constants, and if upstream renames one, SpecScribe silently finds nothing until the constant is updated — which is precisely how Story 18.1 got `traceability-matrix.csv` and `nfr-report.md` wrong from doc-site prose. The mitigation is procedural, not architectural: every pinned filename is fixtured with the upstream commit SHA it was read from (ADR 0015 Decision 7), and re-verified at the start of any story that touches it.
- **A minor-version bump inside the accepted major is trusted.** If upstream removes a field in `0.2.0`, the reader sees a null and degrades rather than skipping. Accepted deliberately: gating on the full version would skip every harmless additive change, which trades a rare wrong-shaped read for a common needless blindness.
- **The generic-pages pass and this path can both see the same markdown file.** Story 18.5 resolves that by having the module surface *link* the generic page rather than re-render it, and pins the invariant with a test; a future story that instead re-renders must add the path to `ArtifactBundle.ConsumedSourceRelatives` or it will emit the document twice.

## Resolved question (ratified 2026-07-29, Epic 18 retrospective)

> **Previously open:** should the version gate accept a **higher** major with a warning rather than skipping outright?

**Decided: accept a higher major and warn.** Upstream's `0.1.0` is manifestly an early version and a `1.0.0` is likely to be shape-compatible, so skipping outright costs the user the signal for no benefit in the case most likely to actually occur. The reader parses, degrades field-by-field on anything it does not recognize (the same tolerance a minor bump already gets, § Consequences), and emits one non-fatal diagnostic naming the version it read and the version it was built against.

The conservative argument — *a wrong verdict on a quality gate is worse than an absent one* — is not discarded; it is bounded. It applies to a **lower-or-unparseable** major, which remains a hard skip with no parse attempted. It does not apply to a higher major, because the failure mode there is a missing field (which degrades to null, visibly) rather than a field that means something different.

**⚠️ This ratification runs AHEAD of the implementation.** `TestArtifactDerivation.IsSchemaSupported` currently tests `major == SupportedSchemaMajor` — exact equality — so a higher major is skipped today, before any field is touched. That is the pre-ratification behaviour and it now diverges from this ADR. Aligning it is seated as an action item at the Epic 18 retrospective; until that lands, the shipped behaviour is the conservative one and this section is the authority on where it is going. Do not "fix" the ADR to match the code.
