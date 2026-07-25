// Story 20.4 Task 6 — serves spike/plotly/probe/ under the VS Code webview's EXACT policy so the CSP verdict is
// observed rather than argued. Same shape as spike/nuxt-ir/scripts/csp-probe.mjs (Story 23.1), reused deliberately.
//
//   node scripts/csp-probe.mjs [port] [variant]
//     webview        the shipped policy, byte-verbatim (read out of WebviewRenderAdapter.cs at startup)
//     unsafe-eval    the shipped policy + 'unsafe-eval' on script-src   (does Plotly need it?)
//     no-inline-style the shipped policy MINUS style-src 'unsafe-inline' (isolates the STYLE axis from the SCRIPT axis)
//     off            no CSP at all (control)
//
// HONESTY BOUNDARY, inherited verbatim from 23.1: this delivers the policy as an HTTP RESPONSE HEADER over an
// HTTP-served asset graph. The webview delivers it in a <meta http-equiv> tag, with no server, over
// vscode-resource: URIs, inside Electron. Meta-delivered CSP ignores some directives and does not apply to
// resources requested before the tag is parsed. A verdict measured here is a LOWER BOUND on the webview gap.

import { createServer } from 'node:http'
import { readFileSync, existsSync, statSync } from 'node:fs'
import { join, extname, dirname, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..', 'probe')

const port = Number(process.argv[2] ?? 5411)
if (!Number.isInteger(port) || port < 1 || port > 65535) {
  console.error(`csp-probe: first argument must be a port number, got '${process.argv[2]}'`)
  process.exit(1)
}
const self_ = `http://localhost:${port}`

// Read the policy from the SOURCE rather than pasting it, so an upstream change cannot silently invalidate this.
const adapter = readFileSync(resolve(here, '..', '..', '..', 'src', 'SpecScribe', 'WebviewRenderAdapter.cs'), 'utf8')
const shipped = adapter.match(/<meta http-equiv="Content-Security-Policy" content="([^"]+)"/)?.[1]
if (!shipped) { console.error('csp-probe: could not read the CSP string out of WebviewRenderAdapter.cs'); process.exit(1) }

const base = (n) => shipped.replace(/__CSP_SOURCE__/g, self_).replace(/__NONCE__/g, n)

const POLICIES = {
  webview: (n) => base(n),
  'unsafe-eval': (n) => base(n).replace(`script-src 'nonce-${n}'`, `script-src 'nonce-${n}' 'unsafe-eval'`),
  'no-inline-style': (n) => base(n).replace(`style-src 'unsafe-inline' ${self_}`, `style-src ${self_}`),
  off: () => null,
}

const variant = process.argv[3] ?? 'webview'
if (!Object.hasOwn(POLICIES, variant)) {
  console.error(`csp-probe: unknown variant '${variant}' — expected one of ${Object.keys(POLICIES).join(', ')}`)
  process.exit(1)
}

const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8' }

createServer((req, res) => {
  let url
  try { url = decodeURIComponent(req.url.split('?')[0]) } catch { res.writeHead(400); return res.end('bad url') }
  if (url === '/') url = '/index.html'
  let file = resolve(join(root, url))
  if (file !== root && !file.startsWith(root + sep)) { res.writeHead(403); return res.end('403') }
  if (!existsSync(file) || statSync(file).isDirectory()) { res.writeHead(404); return res.end('404') }

  const nonce = randomBytes(16).toString('base64')
  const policy = POLICIES[variant](nonce)
  const headers = { 'content-type': MIME[extname(file)] ?? 'application/octet-stream' }
  if (policy) headers['content-security-policy'] = policy

  let body = readFileSync(file)
  // Substitute the nonce ONLY for pages that get their policy from this header. A page carrying its own <meta>
  // policy is self-consistent already, and rewriting its script nonces (but not the one baked into the meta
  // content) would block every script for a reason that has nothing to do with the measurement.
  if (extname(file) === '.html' && !body.includes('http-equiv="Content-Security-Policy"')) {
    // The one substitution the extension shim performs per render. Anchored to the nonce ATTRIBUTE so a literal
    // occurrence inside the JSON island cannot be rewritten under measurement.
    body = Buffer.from(body.toString('utf8').replace(/(nonce=")ss20p4NONCEfixedForProbe(")/g, `$1${nonce}$2`), 'utf8')
  }
  res.writeHead(200, headers)
  res.end(body)
}).listen(port, () => {
  console.log(`csp-probe: serving ${root} on ${self_} variant '${variant}'`)
  console.log(`policy: ${POLICIES[variant]('<per-request>') ?? '(none — control)'}`)
  console.log(`NOTE: HTTP header delivery; the webview uses <meta http-equiv>. Verdict is a LOWER BOUND.`)
})
