// Serves the prerendered output under the VS Code webview's EXACT Content-Security-Policy so hydration can be
// observed failing or succeeding for real, instead of being argued about.
//
//   node scripts/csp-probe.mjs [port] [variant]
//     variant = webview        the shipped policy, verbatim (WebviewRenderAdapter.cs:102)
//               strict-dynamic the shipped policy + 'strict-dynamic' on script-src
//               relaxed        + 'strict-dynamic' + connect-src for the payload fetch
//               off            no CSP at all (control)
//
// The server substitutes the `__NONCE__` placeholder the Nitro plugin baked in, exactly as the webview host
// does per panel render.

import { createServer } from 'node:http'
import { readFileSync, existsSync, statSync } from 'node:fs'
import { join, extname, dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..', '.output', 'public')
const port = Number(process.argv[2] ?? 5311)
const variant = process.argv[3] ?? 'webview'
const self = `http://localhost:${port}`

// Verbatim from src/SpecScribe/WebviewRenderAdapter.cs:102 (__CSP_SOURCE__ → this origin).
const POLICIES = {
  webview: (n) =>
    `default-src 'none'; base-uri 'none'; form-action 'none'; img-src ${self} data: https:; style-src 'unsafe-inline' ${self}; script-src 'nonce-${n}'; font-src ${self} data:;`,
  'strict-dynamic': (n) =>
    `default-src 'none'; base-uri 'none'; form-action 'none'; img-src ${self} data: https:; style-src 'unsafe-inline' ${self}; script-src 'nonce-${n}' 'strict-dynamic'; font-src ${self} data:;`,
  relaxed: (n) =>
    `default-src 'none'; base-uri 'none'; form-action 'none'; img-src ${self} data: https:; style-src 'unsafe-inline' ${self}; script-src 'nonce-${n}' 'strict-dynamic'; connect-src ${self}; font-src ${self} data:;`,
  off: () => null,
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
  const url = decodeURIComponent(req.url.split('?')[0])
  let file = join(root, url)
  if (existsSync(file) && statSync(file).isDirectory()) file = join(file, 'index.html')
  if (!existsSync(file)) {
    res.writeHead(404, { 'content-type': 'text/plain' })
    return res.end('404')
  }

  const ext = extname(file)
  const nonce = randomBytes(16).toString('base64')
  const policy = (POLICIES[variant] ?? POLICIES.webview)(nonce)
  const headers = { 'content-type': MIME[ext] ?? 'application/octet-stream' }
  if (policy) headers['content-security-policy'] = policy

  let body = readFileSync(file)
  if (ext === '.html') {
    // Same substitution the webview host performs on its one-per-render nonce.
    body = Buffer.from(body.toString('utf8').replaceAll('__NONCE__', nonce), 'utf8')
  }
  res.writeHead(200, headers)
  res.end(body)
}).listen(port, () => {
  console.log(`csp-probe: serving ${root} on ${self} with variant '${variant}'`)
  console.log(`policy: ${(POLICIES[variant] ?? POLICIES.webview)('<per-request>')}`)
})
