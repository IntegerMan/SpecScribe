// Shared plumbing for the Story 23.3 harnesses (`measure:parity`, `check:links`, `check:a11y`).
//
// Zero npm dependencies, by ADR 0010's deliberate posture: `web/` runs on nuxt + vue + vue-router and the
// vendored Plotly build. Pulling an HTML parser, a CSS parser or a link checker off npm to satisfy three
// scripts would trade that posture for convenience. Node built-ins and plain string work, the way
// `tokens-lib.mjs` and `measure-payload.mjs` already do it.

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, posix, relative, resolve, sep } from 'node:path'

export const PUBLIC_DIR = resolve(process.cwd(), '.output', 'public')
export const MEASUREMENTS_DIR = resolve(process.cwd(), 'measurements')

/** Every file under a directory, as output-relative POSIX paths. */
export function walk(dir, root = dir, out = []) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name)
    if (statSync(full).isDirectory()) walk(full, root, out)
    else out.push(relative(root, full).split(sep).join(posix.sep))
  }
  return out
}

/** The `<main …>…</main>` region, or null. Greedy on purpose — a page has exactly one. */
export function mainRegion(html) {
  const m = html.match(/<main\b[^>]*>[\s\S]*<\/main>/i)
  return m ? m[0] : null
}

/**
 * The golden gate's normalization, verbatim in intent: neutralize the wall-clock footer, the
 * `?v=<ModuleVersionId>` asset cache-bust, the build-derived product version, CRLF and the BOM. A diff that
 * is only build-token noise is not a parity failure — and a comparison that does NOT normalize these would
 * report one on every run, which is the same as reporting none.
 */
export function normalizeVolatile(s) {
  return s
    .replace(/^﻿/, '')
    .replace(/\r\n/g, '\n')
    .replace(/on [A-Za-z]+ \d{1,2}, \d{4} at \d{1,2}:\d{2} UTC[+-]\d{2}:\d{2}/g, 'on <DATE>')
    .replace(/\?v=[0-9a-fA-F]+/g, '?v=<MVID>')
    .replace(/SpecScribe v[^<]+/g, 'SpecScribe v<VERSION>')
}

/** First differing offset between two strings, or -1. */
export function firstDifference(a, b) {
  const n = Math.min(a.length, b.length)
  for (let i = 0; i < n; i += 1) if (a[i] !== b[i]) return i
  return a.length === b.length ? -1 : n
}

/** A short, readable excerpt around an offset — enough to name a cause, not enough to drown a log. */
export function excerpt(s, at, radius = 90) {
  return JSON.stringify(s.slice(Math.max(0, at - radius), at + radius))
}

/** Loads a file, or null when absent. */
export function readOrNull(file) {
  try {
    return readFileSync(file, 'utf8')
  } catch (err) {
    if (err.code === 'ENOENT') return null
    throw err
  }
}

/** Right-pads for the fixed-width tables these harnesses print. */
export function pad(s, n) {
  return String(s).padEnd(n)
}

export function kb(bytes) {
  return `${(bytes / 1024).toFixed(1)} KB`
}

/**
 * Refuses to publish a number produced from a truncated prerender.
 *
 * `SPECSCRIBE_IR_ROUTE_LIMIT` exists so a developer can iterate without paying for 1,042 routes. It must
 * never silently become the basis of a recorded measurement — "covered everything" is exactly what a
 * partial run reads like once its output is in a story file.
 */
export function assertFullRun(label) {
  const limit = Number(process.env.SPECSCRIBE_IR_ROUTE_LIMIT ?? 0)
  if (limit > 0) {
    console.error(
      `${label} — SPECSCRIBE_IR_ROUTE_LIMIT=${limit} is set. The prerendered output is a ${limit}-route ` +
        `subset, so any number produced from it is not a site measurement. Unset it and re-run ` +
        `\`npm run generate\`.`,
    )
    process.exit(1)
  }
}
