---
title: 'AbbreviationExpander: skip numbered references'
type: 'bugfix'
created: '2026-07-20'
status: 'done'
route: 'one-shot'
---

# AbbreviationExpander: skip numbered references

## Intent

**Problem:** `AbbreviationExpander`'s `\bADR\b` word-boundary match wraps the bare acronym mid-reference in real story prose like "ADR-0005" / "ADR 0005", because hyphen and space are non-word characters — so the first such hit per page gets an `<abbr>` tooltip on the acronym adjacent to its number.

**Approach:** After the trailing word boundary, add a negative lookahead for optional space/hyphen/en/em-dash separators followed by ≥2 digits, so numbered citations stay plain while bare "ADR", "ADR-driven", and "ADR 5 years" still expand on first use. Pin with unit tests; close the 10-3 deferred-work bullet.

## Suggested Review Order

- Numbered-ref skip: `(?![\s\-\u2013\u2014]*\d{2,})` after `\b`
  [`AbbreviationExpander.cs:48`](../../src/SpecScribe/AbbreviationExpander.cs#L48)

- Pins: hyphen/space/en-dash/FR skip, single-digit still expands, first-use not consumed, ADR-driven wraps
  [`AbbreviationExpanderTests.cs:66`](../../tests/SpecScribe.Tests/AbbreviationExpanderTests.cs#L66)

- Ledger: 10-3 bullet RESOLVED; punctuation-separator styles deferred
  [`deferred-work.md:31`](./deferred-work.md#L31)
