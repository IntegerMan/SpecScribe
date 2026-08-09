// ADR 0040 §Decision 9 Stage A — allocate the next mainline preview tag.
//
// stdin: one existing tag per line. stdout: exactly one `vMAJOR.MINOR.PATCH-preview.N` tag.
//
// The optional --base-file is the owner-reviewed semantic target. Without it, Stage A advances PATCH from
// the highest existing base. The preview counter is always global and monotonic. Tags outside this policy
// are ignored: they must not turn a maintenance or experimental tag into a release version.

import { existsSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const Bootstrap = { major: 0, minor: 1, patch: 0 };
const NumberPart = String.raw`(?:0|[1-9]\d*)`;
const BasePattern = new RegExp(String.raw`^(?<major>${NumberPart})\.(?<minor>${NumberPart})\.(?<patch>${NumberPart})$`);
const TagPattern = new RegExp(String.raw`^v(?<major>${NumberPart})\.(?<minor>${NumberPart})\.(?<patch>${NumberPart})-preview\.(?<preview>[1-9]\d*)$`);

function compareBase(left, right) {
  return left.major - right.major || left.minor - right.minor || left.patch - right.patch;
}

function parseBase(value, source) {
  const match = BasePattern.exec(value.trim());
  if (!match) throw new Error(`${source} must contain MAJOR.MINOR.PATCH without a v prefix or prerelease label.`);
  return {
    major: Number(match.groups.major),
    minor: Number(match.groups.minor),
    patch: Number(match.groups.patch),
  };
}

function baseFileArgument(args) {
  const index = args.indexOf('--base-file');
  if (index === -1) return null;
  const path = args[index + 1];
  if (!path || args.length !== 2) throw new Error('usage: next-preview-tag.mjs [--base-file path]');
  return path;
}

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) chunks[chunks.length] = chunk;
  return Buffer.concat(chunks).toString('utf8');
}

export function allocateNextPreviewTag(tagInput, requestedBaseText = null) {
  const tags = tagInput
  .split(/\r?\n/)
  .map((tag) => tag.trim())
  .filter(Boolean)
  .map((tag) => {
    const match = TagPattern.exec(tag);
    if (!match) return null;
    return {
      major: Number(match.groups.major),
      minor: Number(match.groups.minor),
      patch: Number(match.groups.patch),
      preview: Number(match.groups.preview),
    };
  })
  .filter(Boolean);

  const latest = tags.toSorted((left, right) => compareBase(left, right) || left.preview - right.preview).at(-1);
  const highestPreview = tags.reduce((highest, tag) => Math.max(highest, tag.preview), 0);
  const requestedBase = requestedBaseText === null ? null : parseBase(requestedBaseText, 'release base');

  if (requestedBase && latest && compareBase(requestedBase, latest) < 0) {
    throw new Error(`release base requests ${requestedBase.major}.${requestedBase.minor}.${requestedBase.patch}, which is below the latest release base ${latest.major}.${latest.minor}.${latest.patch}.`);
  }

  const base = requestedBase ?? (latest
    ? { major: latest.major, minor: latest.minor, patch: latest.patch + 1 }
    : Bootstrap);
  return `v${base.major}.${base.minor}.${base.patch}-preview.${highestPreview + 1}`;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const baseFile = baseFileArgument(process.argv.slice(2));
  const requestedBaseText = baseFile && existsSync(baseFile) ? readFileSync(baseFile, 'utf8') : null;
  process.stdout.write(`${allocateNextPreviewTag(await readStdin(), requestedBaseText)}\n`);
}