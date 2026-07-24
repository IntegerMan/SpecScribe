// Serves one neutral IrPage, keyed by its route path. Runs at prerender time (Nitro), so the content lands in
// the emitted static HTML. Reading from disk here rather than `import`ing the JSON into the component keeps the
// extracted IR out of the client bundle — only the page being rendered travels.
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

let cache: any = null
function ir() {
  if (!cache) cache = JSON.parse(readFileSync(resolve(process.cwd(), 'ir-data/ir.json'), 'utf8'))
  return cache
}

export default defineEventHandler((event) => {
  const path = getQuery(event).path as string
  const data = ir()
  const page = data.pages[path]
  if (!page) throw createError({ statusCode: 404, statusMessage: `no IR surface '${path}'` })
  return { site: data.site, page }
})
