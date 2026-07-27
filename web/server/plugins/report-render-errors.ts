/**
 * Prints the actual exception behind a prerender failure. [Story 23.3]
 *
 * Nitro's prerenderer reports a failed route as `[500] Server Error` and nothing else — no message, no
 * stack, no file. With a 1,042-route table that is close to useless: every route fails identically and the
 * log tells you only that they did. This plugin re-emits the real error, once per route, so a broken build
 * says what broke.
 *
 * Kept (not a scratch debugging aid): the route table is generated from the IR, so the first symptom of an
 * IR change this app cannot handle will be exactly this failure, and the next person deserves the message.
 */
export default defineNitroPlugin((nitroApp) => {
  nitroApp.hooks.hook('error', (error, ctx) => {
    const path = (ctx as { event?: { path?: string } })?.event?.path ?? '<unknown route>'
    console.error(`\n[render error] ${path}\n${(error as Error)?.stack ?? error}\n`)
  })
})
