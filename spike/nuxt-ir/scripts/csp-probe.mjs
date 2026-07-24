// Serves the prerendered output under the VS Code webview's EXACT Content-Security-Policy so hydration can be
// observed failing or succeeding for real, instead of being argued about.
//
//   node scripts/csp-probe.mjs [port] [variant]
//     variant = webview        the shipped policy, verbatim (WebviewRenderAdapter.cs:113)
//               strict-dynamic the shipped policy + 'strict-dynamic' on script-src
//               relaxed        + 'strict-dynamic' + connect-src for the payload fetch
//               off            no CSP at all (control)
//
// The server substitutes the `__NONCE__` placeholder the Nitro plugin baked in, exactly as the webview host
// does per panel render.

import { createServer } from 'node:http'
import { readFileSync, existsSync, statSync } from 'node:fs'
import { join, extname, dirname, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..', '.output', 'public')

const port = Number(process.argv[2] ?? 5311)
if (!Number.isInteger(port) || port < 1 || port > 65535) {
  console.error(`csp-probe: first argument must be a port number, got '${process.argv[2]}'`)
  console.error(`usage: node scripts/csp-probe.mjs [port] [webview|strict-dynamic|relaxed|off]`)
  process.exit(1)
}
const self = `http://localhost:${port}`

// IMPORTANT (delivery-mechanism caveat): this probe serves the policy as an HTTP RESPONSE HEADER, while the
// webview ships it in a `<meta http-equiv="Content-Security-Policy">` tag. Meta-delivered CSP is not applied to
// resources requested before the tag is parsed and ignores some directives outright — precisely the class of
// difference that governs whether a `<link rel="preload" as="fetch">` is blocked. Treat the matrix as evidence
// about the POLICY STRING, not about meta-tag delivery. Recorded in the report's honesty boundary.
//
// Verbatim from src/SpecScribe/WebviewRenderAdapter.cs:113 (__CSP_SOURCE__ → this origin).
const POLICIES = {
  webview: (n) =>
    `default-src 'none'; base-uri 'none'; form-action 'none'; img-src ${self} data: https:; style-src 'unsafe-inline' ${self}; script-src 'nonce-${n}'; font-src ${self} data:;`,
  'strict-dynamic': (n) =>
    `default-src 'none'; base-uri 'none'; form-action 'none'; img-src ${self} data: https:; style-src 'unsafe-inline' ${self}; script-src 'nonce-${n}' 'strict-dynamic'; font-src ${self} data:;`,
  relaxed: (n) =>
    `default-src 'none'; base-uri 'none'; form-action 'none'; img-src ${self} data: https:; style-src 'unsafe-inline' ${self}; script-src 'nonce-${n}' 'strict-dynamic'; connect-src ${self}; font-src ${self} data:;`,
  off: () => null,
}

// Fail loudly on a typo'd variant. Falling back to `webview` meant a mistyped `strict-dynamic` would silently
// record a relaxation row under the SHIPPED policy — the one error that would invert a CSP-matrix verdict.
// `Object.hasOwn` also stops `constructor`/`toString` from resolving to an inherited function.
const variant = process.argv[3] ?? 'webview'
if (!Object.hasOwn(POLICIES, variant)) {
  console.error(`csp-probe: unknown variant '${variant}' — expected one of ${Object.keys(POLICIES).join(', ')}`)
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
  // A malformed percent-escape throws URIError, which would kill the server mid-measurement.
  let url
  try {
    url = decodeURIComponent(req.url.split('?')[0])
  } catch {
    res.writeHead(400, { 'content-type': 'text/plain' })
    return res.end('bad url')
  }

  // Confine every read to .output/public. `join` alone does not stop `/../../..` from escaping the root, and
  // this file is the template the report points 23.4 at for its CSP regression test.
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
  const policy = POLICIES[variant](nonce)
  const headers = { 'content-type': MIME[ext] ?? 'application/octet-stream' }
  if (policy) headers['content-security-policy'] = policy

  let body = readFileSync(file)
  if (ext === '.html') {
    // Same substitution the webview host performs on its one-per-render nonce. Anchored to the nonce ATTRIBUTE
    // rather than a bare replaceAll: a literal `__NONCE__` occurring inside the v-html'd IR content or the
    // __NUXT_DATA__ block would otherwise be rewritten, silently altering the bytes under measurement.
    body = Buffer.from(
      body.toString('utf8').replace(/(nonce=")__NONCE__(")/g, `$1${nonce}$2`),
      'utf8',
    )
  }
  res.writeHead(200, headers)
  res.end(body)
}).listen(port, () => {
  console.log(`csp-probe: serving ${root} on ${self} with variant '${variant}'`)
  console.log(`policy: ${POLICIES[variant]('<per-request>') ?? '(none — control)'}`)
  console.log(`NOTE: policy is delivered as an HTTP header here; the webview uses <meta http-equiv>.`)
  console.log(`Browse via ${self} (not 127.0.0.1) — the policy's origin sources are built from this host.`)
})
