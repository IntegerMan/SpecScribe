#!/usr/bin/env bash
# ADR 0040 §Decision 2 + §Decision 5 — the renderer must be INSIDE the produced archive, at its exact archived
# path. [Story 16.4 AC #4, Task 6]
#
# 🚨 ASSERT THE PATH. NOT THE FILE COUNT, NOT THE BYTE TOTAL.
#
# Story 16.3 measured this exact false pass while packaging the nupkg: a destination-shape mistake (a doubled
# %(RecursiveDir), a mis-rooted Link) produced 203 files and a byte-identical total with
# `renderer/server/index.mjs` ABSENT (docs/Packaging.md § Trap 2). Every cheap check passed it. The same class
# of mistake is available here — a `tar -C` from the wrong directory, an archive rooted one level too deep —
# and its symptom is a download that unzips cleanly and then cannot render a single page.
#
# WHY THIS ASSERTION IS THIS STORY'S AND NOT 16.3's: `AssertRendererPacked` inspects the produced NUPKG and
# `AssertRendererAvailableForPublish` inspects the SOURCE directory. Neither can see what landed in a tarball
# that did not exist when they ran.
#
# WHY IT CARRIES MORE WEIGHT THAN ADR 0040 ASSUMED: §Decision 5 mitigated the self-contained channel's
# two-filesystem-objects problem with TWO controls — a version stamp on the artefact (assigned to Story 16.3,
# NOT delivered) and one-archive-per-RID (this story). Only one of the two exists, so this is the sole control
# preventing a desynchronized CLI/renderer pair. Shipping the executable and the renderer as separate release
# assets — even briefly, even "just for testing" — reintroduces the failure Story 16.9 AC #2 exists to prevent,
# and it "fails as wrong output rather than as an error".
#
# $1 : path to the archive (.zip or .tar.gz)
# $2 : the expected top-level directory inside it, e.g. specscribe-0.1.0-preview.1-win-x64
set -euo pipefail

ARCHIVE=${1:?usage: assert-archive-renderer.sh <archive> <root-dir-name>}
ROOT=${2:?usage: assert-archive-renderer.sh <archive> <root-dir-name>}

ENTRY="${ROOT}/renderer/server/index.mjs"

[ -f "$ARCHIVE" ] || { echo "FAIL — no archive at '${ARCHIVE}'." >&2; exit 1; }

# ⚠️ tar READS THE ARCHIVE FROM STDIN, and that is not a style choice.
#
# `tar -tzf C:\path\to\x.tar.gz` does not read that file: GNU tar parses a leading `C:` as a REMOTE HOST spec
# (`host:path`, the rsh transport) and tries to reach a machine called `C`. It exits 2 with
# "Cannot connect to C: resolve failed" — which fails CLOSED, so nothing unsafe ships, but the diagnosis points
# at the network instead of at the path and the assertion never actually inspects the archive. This job runs on
# a windows-latest runner where the workspace is `D:\a\…`, so it is reachable rather than theoretical; caught by
# selftest.mjs on the development machine. `--force-local` fixes it for GNU tar and is not a bsdtar (macOS)
# flag, so the portable answer is to let the SHELL open the file and hand tar a stream it cannot misparse.
case "$ARCHIVE" in
  *.zip)    listing=$(unzip -Z1 "$ARCHIVE") ;;
  *.tar.gz) listing=$(tar -tzf - < "$ARCHIVE") ;;
  *)
    # Refuse rather than guess. An unknown extension reaching here means the workflow's naming and this
    # assertion have drifted apart, and "I could not check it" must never pass for "I checked it".
    echo "FAIL — '${ARCHIVE}' is neither .zip nor .tar.gz, so its contents were NOT verified." >&2
    exit 1 ;;
esac

# Normalise the leading `./` that tar emits for some invocations, so the comparison is about the shape of the
# tree rather than about which tool wrote it. The SEPARATOR is deliberately NOT normalised — see below.
normalised=$(printf '%s\n' "$listing" | sed 's|^\./||')

if printf '%s\n' "$normalised" | grep -Fxq "$ENTRY"; then
  count=$(printf '%s\n' "$listing" | grep -c . || true)
  echo "assert-archive-renderer OK — '${ENTRY}' is present in $(basename "$ARCHIVE") (${count} entries)."
  exit 0
fi

# ⚠️ A SEPARATE, MUCH MORE ACTIONABLE DIAGNOSIS: the payload is all there, at the right depth, and the archive
# uses BACKSLASHES as its path separator.
#
# The ZIP spec (APPNOTE 4.4.17.1) requires forward slashes. PowerShell 5.1's `Compress-Archive` writes
# backslashes anyway, and the resulting archive extracts on many tools as single files literally NAMED
# `renderer\server\index.mjs` — one flat file, no directory — so the consumer gets a tree that cannot resolve.
# Measured on this project's development machine, 2026-08-08, while rehearsing the release job locally.
#
# Without this branch the failure reads as "the renderer is missing", and whoever hits it goes and debugs the
# publish step, which is fine. Use `7z` (as the workflow does) or `zip`, or the BCL's
# [System.IO.Compression.ZipFile]::CreateFromDirectory — never `Compress-Archive` on Windows PowerShell.
if printf '%s\n' "$normalised" | tr '\\' '/' | grep -Fxq "$ENTRY"; then
  echo "FAIL — '$(basename "$ARCHIVE")' stores its paths with BACKSLASH separators." >&2
  echo >&2
  echo "       The payload IS present and at the right depth — but the ZIP spec (APPNOTE 4.4.17.1) requires" >&2
  echo "       forward slashes, and many extractors will produce a single flat file literally named" >&2
  echo "       'renderer\\server\\index.mjs' instead of a directory. The consumer then gets a tree that" >&2
  echo "       cannot resolve, from an archive that looked fine to whoever built it." >&2
  echo >&2
  echo "       This is PowerShell 5.1 \`Compress-Archive\`'s signature. Use 7z (what release.yml uses), zip," >&2
  echo "       or [System.IO.Compression.ZipFile]::CreateFromDirectory." >&2
  exit 1
fi

echo "FAIL — '$(basename "$ARCHIVE")' does NOT contain the renderer entry point at '${ENTRY}'." >&2
echo >&2
echo "       A consumer would unzip this, get a working \`specscribe\` on PATH, and then fail EVERY generate" >&2
echo "       with 'the renderer artefact could not be found'." >&2
echo >&2
echo "       Note what this does NOT tell you: file count and byte total are useless here. Story 16.3" >&2
echo "       measured a wrong-path package at 203 files and an IDENTICAL byte total with the entry point" >&2
echo "       absent. If the payload is on disk but not at this path, the archive was rooted wrongly —" >&2
echo "       check the \`tar -C\` / working directory, not the publish step." >&2
echo >&2
echo "       Archive contains these renderer-ish paths (first 20):" >&2
printf '%s\n' "$listing" | grep -i 'renderer' | head -20 | sed 's/^/         /' >&2 || echo "         (none)" >&2
exit 1
