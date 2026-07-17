---
title: 'Accent/kicker slug heuristics — fail-closed defaults'
type: 'bugfix'
created: '2026-07-17'
status: 'done'
baseline_commit: '0ea1dd8e3bc033a06c1e394559054727e4d5840e'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `AccentForCommand` / `KickerForCommand` treat unknown catalog slugs as accent `ready` and kicker `"Also consider"`, so new or unmapped commands look like planned work. `sprint-status` already uses accent `active` (with Develop-family commands) and kicker `"Plan"` — keep that pairing.

**Approach:** Fail closed — unknown slugs get accent `pending` and kicker `"Also consider"`. Keep all existing known mappings, including `sprint-status` → accent `active`. Pin the known map with a coverage test so only truly unknown slugs hit the defaults.

## Boundaries & Constraints

**Always:**
- Unknown (unmapped) command slug → accent `pending`, kicker `"Also consider"`.
- `sprint-status` accent stays `active`; its kicker stays `"Plan"`.
- Existing known accent/kicker mappings for other BMM steps stay as they are today (except the unknown default).
- Primary card kicker remains `"Recommended"`; `Suggestion.Accent` / `Suggestion.Kicker` overrides still win.
- Coverage test: every slug family `BmadCommands` actually suggests has an **explicit** accent and kicker (no silent fall-through).
- Strike the matching deferred-work bullet and the 9.8 review `[Defer]` finding when done.

**Ask First:**
- Changing any known mapping other than the unknown default (e.g. moving `sprint-status` off `active`, renaming kickers).
- Adding new CSS tokens or redesigning next-step card chrome beyond a missing `.pending` kicker color rule if needed for parity.

**Never:**
- New authoring schema, sprint-status yaml keys for this polish, or epic/story status model changes.
- Broad Next Steps IA / card-layout rewrite (Story 9.8 already locked).
- Treating Close-out overrides (`Kicker: "Close"`, `Accent: "review"`) as catalog-slug heuristics.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Known develop slug | `/bmad-dev-story 1.2` (non-primary) | accent `active`, kicker `Develop` | N/A |
| sprint-status | `/bmad-sprint-status` (non-primary) | accent `active`, kicker `Plan` | N/A |
| Known review slug | `/bmad-code-review` (non-primary) | accent `review`, kicker `Review` | N/A |
| check-implementation* | `/bmad-check-implementation-readiness` (non-primary) | accent `pending`, **explicit** kicker (not default fall-through) | N/A |
| Unknown slug | `/bmad-totally-new-skill` (non-primary) | accent `pending`, kicker `Also consider` | N/A |
| Primary card | any mapped/unmapped as first card | kicker always `Recommended`; accent from override or heuristic | N/A |
| Override | `Suggestion` with `Accent`/`Kicker` set | heuristics skipped for those fields | N/A |

</frozen-after-approval>

## Code Map

- `src/SpecScribe/BmadCommands.cs` -- `AccentForCommand` / `KickerForCommand` / `CommandSlug`; `RenderInner` applies them; `For*` callers list the step keys that must stay mapped
- `src/SpecScribe/ModuleContext.cs` -- `CommandCatalog` (no Keys API today; coverage can hard-list BmadCommands step families)
- `src/SpecScribe/assets/specscribe.css` -- `.next-step-card.pending` / `.ready` rails; optional `.pending` kicker color if parity requires it
- `tests/SpecScribe.Tests/ModuleContextTests.cs` -- existing `BmadCommandsTests` next-step card coverage (extend or sibling)
- `src/SpecScribe/SpecScribe.csproj` -- add `InternalsVisibleTo` if helpers stay `internal` for direct unit tests
- `_bmad-output/implementation-artifacts/deferred-work.md` -- strike/resolve the 9.8 accent/kicker bullet
- `_bmad-output/implementation-artifacts/9-8-authoring-and-delivery-workflow-coherence.md` -- mark the `[Defer]` finding resolved

## Tasks & Acceptance

**Execution:**
- [x] `src/SpecScribe/BmadCommands.cs` -- change unknown accent default `ready` → `pending`; keep `sprint-status` → `active`; add an **explicit** kicker for `check-implementation*` (today it falls through to `"Also consider"`) so the coverage test can treat it as mapped — use `"Validate"` unless Ask First triggers
- [x] `src/SpecScribe/SpecScribe.csproj` (+ helpers visibility) -- expose `AccentForCommand` / `KickerForCommand` to tests via `internal` + `InternalsVisibleTo`, or an equivalent minimal test seam (no public product API expansion beyond what's needed)
- [x] `tests/SpecScribe.Tests/ModuleContextTests.cs` (or sibling) -- table-driven asserts for known slug→accent/kicker pairs from the I/O matrix; assert unknown slug → `pending` / `"Also consider"`; assert `sprint-status` stays `active`
- [x] `src/SpecScribe/assets/specscribe.css` -- skipped: pending-kicker color rule rejected in review (contrast regression vs base `--ink-light`)
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` + `9-8-…md` -- mark this deferred item resolved
- [x] Golden — regen `SiteGeneratorAdapterTests` fingerprint **only if** it moves (should not if fixture cards only use mapped slugs)

**Acceptance Criteria:**
- Given a non-primary next-step card whose command slug is not in the known map, when rendered, then the card class includes `pending` (not `ready`) and the kicker text is `Also consider`.
- Given `/bmad-sprint-status` as a non-primary card, when rendered, then accent class is `active` and kicker is `Plan`.
- Given each step family `BmadCommands` suggests (`dev-story`, `code-review`, `create-story`, `create-epics`, `sprint-status`, `sprint-planning`, `correct-course`, `quick-dev`, `retrospective`, `check-implementation*`), when mapped, then accent and kicker are explicit (unit coverage fails if a family is dropped from the map).
- Given primary card position, when rendered, then kicker is `Recommended` regardless of slug.

## Spec Change Log

## Design Notes

Owner lock (2026-07-17): **1A** fail-closed unknown → `pending` + `"Also consider"` + coverage test; **2B** keep `sprint-status` accent `active`.

`check-implementation-readiness` already matches accent via `Contains("check-implementation")` → `pending`. It has no kicker rule today; giving it an explicit `"Validate"` kicker closes the coverage gap without changing the unknown default.

Substring/`Contains` matching stays (order-sensitive); do not rewrite to exact-key dictionaries unless a known mapping breaks.

## Verification

**Commands:**
- `dotnet test --filter "FullyQualifiedName~BmadCommands|FullyQualifiedName~Accent|FullyQualifiedName~Kicker|FullyQualifiedName~ModuleContextTests"` -- expected: new + existing next-step tests green
- `dotnet test` -- expected: full suite green; golden unchanged unless CSS kicker rule forces regen

## Suggested Review Order

**Fail-closed heuristics**

- Unknown accent default is now `pending` (was `ready`); known arms unchanged including `sprint-status` → `active`
  [`BmadCommands.cs:201`](../../src/SpecScribe/BmadCommands.cs#L201)

- Explicit `Validate` kicker for `check-implementation*`; unknown still `"Also consider"`
  [`BmadCommands.cs:234`](../../src/SpecScribe/BmadCommands.cs#L234)

**Test seam**

- `internal` helpers + `InternalsVisibleTo` for table-driven coverage
  [`SpecScribe.csproj:40`](../../src/SpecScribe/SpecScribe.csproj#L40)

- Known family table + unknown fail-closed + tightened HTML binding for Validate alternate
  [`ModuleContextTests.cs:496`](../../tests/SpecScribe.Tests/ModuleContextTests.cs#L496)
