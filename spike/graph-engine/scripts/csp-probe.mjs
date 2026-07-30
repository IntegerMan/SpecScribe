// Story 24.6 Task 7 — serve the probe under the VS Code webview's EXACT Content-Security-Policy.
//
// Adapted from spike/nuxt-ir/scripts/csp-probe.mjs (Story 23.1) as the story directs, with two upgrades that
// Story 20.4 established and this probe keeps:
//
//   1. THE POLICY IS READ OUT OF WebviewRenderAdapter.cs AT RUNTIME, never pasted. An upstream policy change
//      cannot silently invalidate this report. (23.1's version had the string inlined with a "verbatim from
//      :113" comment, which had already drifted to :140 by the time this story ran — exactly the failure mode
//      [[cite-adrs-by-symbol-not-line-number]] records.)
//   2. Both HEADER and META delivery are offered, because 20.4 showed they can be reported separately and the
//      webview uses meta.
//
//   node scripts/csp-probe.mjs [port] [variant] [delivery]
//     variant  = webview | no-style-inline | wrong-nonce | unsafe-eval | off
//     delivery = header | meta        (meta injects the policy into the document instead of the response)

import { createServer } from 'node:http'
import { readFileSync, existsSync, statSync } from 'node:fs'
import { join, extname, dirname, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..', 'probe')
const repoRoot = resolve(here, '..', '..', '..')
const adapter = join(repoRoot, 'src/SpecScribe/WebviewRenderAdapter.cs')

const port = Number(process.argv[2] ?? 8130)
const variant = process.argv[3] ?? 'webview'
const delivery = process.argv[4] ?? 'header'
const self = `http://localhost:${port}`

/** Extracts the shipped policy from the `<meta http-equiv="Content-Security-Policy" content="…">` line in the
 *  adapter's document template. Fails loudly: a silent fallback to a pasted string is the one error that would
 *  invert this report's verdict. */
function shippedPolicy() {
  if (!existsSync(adapter)) throw new Error(`cannot read policy: ${adapter} missing`)
  const src = readFileSync(adapter, 'utf8')
  const m = src.match(/http-equiv="Content-Security-Policy"\s+content="([^"]+)"/)
  if (!m) throw new Error('cannot locate the CSP meta line in WebviewRenderAdapter.cs — the template moved')
  return m[1]
}

const SHIPPED = shippedPolicy()

const VARIANTS = {
  // The shipped policy, byte-verbatim, with only the two host-runtime placeholders substituted.
  webview: (n) => SHIPPED.replace(/__CSP_SOURCE__/g, self).replace(/__NONCE__/g, n),
  // Story 20.4 §4.2's harder hypothetical: is style-src 'unsafe-inline' load-bearing for this engine?
  'no-style-inline': (n) =>
    SHIPPED.replace(/__CSP_SOURCE__/g, self).replace(/__NONCE__/g, n).replace(/'unsafe-inline' /, ''),
  // Story 20.4 §4.3's partial-relaxation state: the shape of a half-applied policy fix.
  'wrong-nonce': () => SHIPPED.replace(/__CSP_SOURCE__/g, self).replace(/__NONCE__/g, 'deliberately-mismatched'),
  // Does the candidate NEED 'unsafe-eval'? Compare against `webview` to answer it rather than assert it.
  'unsafe-eval': (n) =>
    SHIPPED.replace(/__CSP_SOURCE__/g, self)
      .replace(/__NONCE__/g, n)
      .replace(/script-src 'nonce-([^']+)'/, "script-src 'nonce-$1' 'unsafe-eval'"),
  off: () => null,
}

if (!Object.hasOwn(VARIANTS, variant)) {
  console.error(`csp-probe: unknown variant '${variant}' — expected ${Object.keys(VARIANTS).join(', ')}`)
  process.exit(1)
}
if (!['header', 'meta'].includes(delivery)) {
  console.error(`csp-probe: delivery must be 'header' or 'meta', got '${delivery}'`)
  process.exit(1)
}

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
}

createServer((req, res) => {
  let url
  try {
    url = decodeURIComponent(req.url.split('?')[0])
  } catch {
    res.writeHead(400, { 'content-type': 'text/plain' })
    return res.end('bad url')
  }

  // Confine every read to probe/. `join` alone does not stop `/../../..` from escaping the root.
  let file = resolve(join(root, url))
  if (file !== root && !file.startsWith(root + sep)) {
    res.writeHead(403, { 'content-type': 'text/plain' })
    return res.end('403')
  }
  if (existsSync(file) && statSync(file).isDirectory()) file = join(file, 'index.html')
  if (!existsSync(file)) {
    res.writeHead(404, { 'content-type': 'text/plain' })
    return res.end('404')
  }

  const ext = extname(file)
  const nonce = randomBytes(16).toString('base64')
  const policy = VARIANTS[variant](nonce)
  const headers = { 'content-type': MIME[ext] ?? 'application/octet-stream' }

  let body = readFileSync(file)
  if (ext === '.html') {
    let text = body.toString('utf8')
    // The same substitution the webview host performs on its one-per-render nonce. Anchored to the nonce
    // ATTRIBUTE so a literal __NONCE__ inside a data island can never be rewritten under measurement.
    text = text.replace(/(nonce=")__NONCE__(")/g, `$1${nonce}$2`)
    text = text.replace(
      '<!--CSP-META-->',
      policy && delivery === 'meta'
        ? `<meta http-equiv="Content-Security-Policy" content="${policy.replace(/"/g, '&quot;')}" />`
        : '',
    )
    text = text.replace(/__PROBE_META__/g, JSON.stringify({ variant, delivery, policy }).replace(/"/g, '&quot;'))
    body = Buffer.from(text, 'utf8')
  }
  if (policy && delivery === 'header') headers['content-security-policy'] = policy
  res.writeHead(200, headers)
  res.end(body)
}).listen(port, () => {
  console.log(`csp-probe: ${root}`)
  console.log(`           on ${self}  variant='${variant}'  delivery='${delivery}'`)
  console.log(`policy (read from WebviewRenderAdapter.cs at runtime):`)
  console.log(`  ${VARIANTS[variant]('<per-request>') ?? '(none — control)'}`)
  console.log(`Browse via ${self} (not 127.0.0.1) — the policy's origin sources are built from this host.`)
})
