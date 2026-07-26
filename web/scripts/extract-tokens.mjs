#!/usr/bin/env node
// `npm run extract:tokens` — regenerates web/assets/tokens.css from the C# stylesheet's `:root` block.
// Run this after ANY presentation-token change in src/SpecScribe/assets/specscribe.css; `check:tokens`
// fails the build if you forget. [Story 23.2 AC #1]

import { mkdirSync, writeFileSync } from 'node:fs'
import { dirname } from 'node:path'
import {
  SOURCE_LABEL,
  TOKENS_CSS,
  declaredTokenNames,
  readCommittedTokensCss,
  renderTokensCss,
  sliceRootBlock,
} from './tokens-lib.mjs'

const generated = renderTokensCss()
const previous = readCommittedTokensCss()

mkdirSync(dirname(TOKENS_CSS), { recursive: true })
writeFileSync(TOKENS_CSS, generated, 'utf8')

const count = declaredTokenNames(sliceRootBlock(generated).body).length
const verb = previous === null ? 'created' : previous === generated ? 'unchanged' : 'updated'
console.log(`extract:tokens — ${verb}: web/assets/tokens.css (${count} tokens from ${SOURCE_LABEL})`)
