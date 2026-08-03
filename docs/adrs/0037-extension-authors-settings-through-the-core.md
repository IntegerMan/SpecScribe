# ADR 0037: The Extension May Author Directory-Scoped Settings — Through the Core, Never Itself

**Status:** Proposed (authored 2026-08-01 from owner field feedback, "VS Code should be able to configure using the tool itself"; ratification is the owner's)
**Date:** 2026-08-01
**Deciders:** Matthew-Hope Eland
**Amends:** [ADR 0003](0003-directory-scoped-settings-and-read-only-helpers.md) §Decision — the clause *"keep IDE helpers limited to generating prompts or commands rather than editing project artifacts"* — and its §Consequences/Positive bullet *"The extension stays aligned with the current read-only/local-first posture."* Also amends **AD-6** in `_bmad-output/specs/spec-specscribe/ARCHITECTURE-SPINE.md`: *"helpers can generate prompts or commands, but any write action remains an explicit external choice."*
**Relates to (and does NOT amend):** [ADR 0005](0005-vs-code-webview-runtime-and-packaging.md) §1 (the shim renders nothing — upheld, see Decision 3); [ADR 0014](0014-specscribe-settings-folder-format.md) (the `.specscribe/config.json` format being written); [ADR 0032](0032-csp-posture-after-the-projection-layer.md) §Decision 1 and [ADR 0036](0036-the-webview-shell-supplies-chrome-scripts.md) (CSP policy string untouched); ADR 0002 / AD-1 / AD-2 (the shim holds no project knowledge — upheld by keeping the field set core-emitted).

## Context

SpecScribe's project settings — source root, ADR root, output root, project name, deep-git, README inclusion, code URL, date policy — live in a directory-scoped `.specscribe/config.json` (ADR 0014). There are exactly two ways to write it: hand-edit the JSON, or run the CLI's interactive Spectre.Console menu and choose "Configure paths".

From inside VS Code there is a third way, and it does not work. `openProjectSettings` did `existsSync('.specscribe')` and then opened the result as a text document. Since ADR 0014 made `.specscribe` a **folder**, that asks VS Code to open a directory — so on every project a current CLI has configured, the extension's one configuration affordance fails outright. A second defect sat beside it: it anchored on the workspace folder rather than the resolved repo root, so a subdirectory-open missed the settings `SettingsStore.FindExisting` walks up to and actually reads.

Owner field feedback, 2026-08-01, after running the extension against a real project: *"VS Code should be able to configure using the tool itself."*

The narrow fix — resolve the document properly and open it in an editor — is worth doing on its own and has been done. But it leaves the user hand-editing JSON, which is not what "configure using the tool" means, and it leaves the extension unable to help a user whose settings are wrong in exactly the situation where they most need help: generation is failing *because* the paths are wrong, so there is no rendered portal to configure from.

The obstacle is not technical. It is that ADR 0003 and AD-6 say the extension does not write.

## Decision

**1. The extension gains one authoring affordance — a settings form — and it writes nothing itself.**

The form's markup, field set, current values, resolved defaults and per-field provenance are produced by the C# core and delivered through the existing `__CSP_SOURCE__` / `__NONCE__` two-value seam. **Save spawns `specscribe config --save`.** `SettingsStore` therefore remains the single writer of `.specscribe/config.json`, and the persist-only-when-set rules, the date-token vocabulary and the ADR 0014 folder-vs-legacy-file migration stay in exactly one place.

**2. AD-6's "explicit external choice" is re-read, not discarded.**

AD-6 was written to prevent SpecScribe silently mutating a user's project. Read literally — "external" meaning *outside the editor* — it forbids a Save button while permitting the same write from a terminal the extension itself opened and pre-filled. That distinction protects nothing: staging `specscribe` at a prompt and having the user press Enter is not more deliberate than having them press Save on a form they filled in.

The clause is re-read as: **an explicit, user-initiated, clearly-labelled action that writes only the settings document.** Scope is what makes it safe, and the scope is narrow on three axes:

- **One file.** `.specscribe/config.json` and nothing else. No source artifact, no `_bmad-output` file, no output tree becomes writable from the editor.
- **Configuration, not content.** The settings document is how the user tells the tool where to look. It is not a project artifact in the sense ADR 0003 was protecting — nothing the user authored, nothing a BMad workflow produces.
- **Generate and Watch stay staged-not-executed.** The terminal handoff (`stageCommandLine`, `sendText(cmd, false)`) is unchanged. This ADR licenses writing settings; it does not license running generation.

**3. ADR 0005 §1 is upheld: the shim still renders nothing.**

A new `SettingsFormTemplater` builds a `PageView`, wrapped by `WebviewRenderAdapter.WrapDocument` — the same CSP meta, the same inlined stylesheet, the same nonce'd bridge as every other webview document.

**Shim-authored form HTML is rejected**, and the reason is recorded rather than assumed: it would be the first HTML the shim ever authors, and every field label, hint and validation message would become shim-owned copy. That is AD-2's exact failure mode, and it would put SpecScribe's configuration vocabulary in TypeScript where the CLI's copy could drift away from it silently.

The document is delivered by a one-shot `specscribe config --form` spawn rather than as a field on `WebviewBundle`. Two reasons, both load-bearing: the settings panel must open without waiting on (or holding) the multi-megabyte portal payload, and **it must work in a workspace where generation fails** — which is precisely the workspace whose paths need fixing.

**4. `--clear <field>` is the unset mechanism; no `--no-*` counter-flags are added.**

Every existing option treats absent as "not passed" (`CliOverrides.Capture`), so without an explicit unset a form can never return a field to "inherit default" — it could only ever add. `--clear deep_git` *is* "back to the default", which is the tri-state's null. Field names are `SettingsResolver.Fields`' keys, which already exist as the tokens a CI script greps for.

**5. The CSP policy string is unchanged, and the form carries no `<form>` element.**

The webview CSP includes `form-action 'none'`, and inline `onsubmit` is blocked by the nonce-locked `script-src`. So the form is plain controls plus a `<button type="button">`, with the nonce'd bridge reading them and posting to the host. This is a correctness requirement, not a style preference; a `<form>` would be silently inert.

## Consequences

### Good

- The folder-vs-file defect gets its real fix — a form the user can actually use — rather than an editor pointed at a directory.
- NFR7's menu/CLI parity gains a third surface without a third implementation: one field set, one validator, one writer.
- `--show-config`'s per-field provenance finally has a UI. The form shows, per field, whether a value came from `.specscribe`, from auto-discovery, or from the default.
- `specscribe config --json` is independently useful — a machine-readable view of effective configuration that CI and other tooling can consume.

### Bad, and accepted

- **"The extension is read-only" stops being literally true**, and must be corrected in the same change wherever it is asserted: `extension/src/extension.ts`'s header comment, `extension/package.json`'s `description`, `extension/README.md`, ADR 0003 §Consequences, ADR 0005 §Context. Left uncorrected it becomes a lie in five places, which is worse than the change itself.
- The extension now spawns the tool in a **mutating** mode. The `resolveWorkspacePath` containment discipline must extend to folder paths picked through `showOpenDialog`.
- A second `WebviewPanel` is a second CSP surface to keep in sync with the first.
- `extension/` has no test project, so the panel itself is covered only by manual `F5` — the same gap ADR 0005 §"Not yet proven" already records for every host-runtime path. The C#-side pieces (`ConfigCommand`, `TrySaveExplicit`, `SettingsFormTemplater`) are unit-tested, which is where the logic deliberately lives.

### Neutral

- CSP policy string unchanged; ADR 0032 §Decision 1 untouched.
- ADR 0005 §1 unchanged — the shim substitutes exactly two placeholders and renders nothing.

## Rejected alternatives

**The shim writes `.specscribe/config.json` with `fs.writeFileSync`.** Simplest to build, and wrong: it duplicates `SettingsStore.Capture`'s persist-only-when-set rules, the `DateCutoffJsonConverter`'s token vocabulary, the ADR 0014 migration and the walk-up path resolution — all of it project knowledge, in a shim whose stated contract is that it holds none. Crossing AD-6 once, narrowly, is a smaller cost than crossing AD-1/AD-2 permanently.

**Keep writes CLI-only and just make the file reachable.** This is what the codebase did, and it is what the owner's feedback was about. Worth noting it is not *removed*: the terminal handoff to the interactive menu stays reachable for users who prefer it.

**Mirror the CLI's prompts as a QuickPick/InputBox sequence.** No new HTML, no second webview — but eight sequential modal prompts is a worse form than a form, it cannot show provenance beside a field, and it still needs the same `config --save` plumbing. The webview costs one more surface and is the better shape for the same underlying work.

**Put the settings in VS Code's own settings UI.** Explicitly rejected by R5.1 and unchanged here: project behaviour stays in the directory-scoped file, or CLI/watch/editor drift becomes possible and provenance gets murky. VS Code settings stay host concerns only (`toolPath`, `openLocation`).

## Open item

`SettingsStore.ApplyTo` reads `saved.DeepGit == true` only, so a persisted `false` is indistinguishable from unset — there is no `--no-deep-git` for it to suppress. A form offering a pinnable "Off" would therefore show a choice the core cannot honour. **Until that read-side gap is closed, the DeepGit control offers only "Inherit default (off)" and "On".** `IncludeReadme` reads `== false` and has no such gap, so it offers all three states. Recorded here rather than worked around silently, because the asymmetry is otherwise indistinguishable from an oversight.
