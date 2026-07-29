#!/usr/bin/env node
// `npm run extract:tokens` — regenerates web/assets/tokens.css from EVERY top-level `:root` block in the
// C# stylesheet. (It carried only the first until the 2026-07-28 re-review; the Impact Map's `--impact-lvl-*`
// ramp had silently never crossed, and the drift gate reported "in sync" the whole time.)
// Run this after ANY presentation-token change in src/SpecScribe/assets/specscribe.css; `check:tokens`
// fails the build if you forget. [Story 23.2 AC #1]

import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname } from 'node:path'
import {
  SOURCE_LABEL,
  TOKENS_CSS,
  declaredTokenNames,
  findRootBlocks,
  readCommittedTokensCss,
  renderTokensCss,
} from './tokens-lib.mjs'

const generated = renderTokensCss()
const previous = readCommittedTokensCss()

mkdirSync(dirname(TOKENS_CSS), { recursive: true })
writeFileSync(TOKENS_CSS, generated, 'utf8')

const blocks = findRootBlocks(generated, 'web/assets/tokens.css')
const count = blocks.reduce((n, b) => n + declaredTokenNames(b.body).length, 0)
const verb = previous === null ? 'created' : previous === generated ? 'unchanged' : 'updated'
console.log(
  `extract:tokens — ${verb}: web/assets/tokens.css ` +
    `(${count} tokens across ${blocks.length} \`:root\` block(s) from ${SOURCE_LABEL})`,
)
