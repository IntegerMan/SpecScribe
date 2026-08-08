#!/usr/bin/env node
// `npm run report:ir-content-residue` — Story 23.4 AC #4's residue enumeration.
//
// ── Why this script exists ─────────────────────────────────────────────────────────────────────────────
//
// AC #4 offers two branches. The first is "`ir-content.manifest.json` is empty and the layer plus its gate
// are deleted". The second is "the residue is enumerated rule-by-rule with a named blocker per rule and an
// owner-visible count". Owner decision D5 chose the FIRST branch — but D5 was taken before anyone had
// measured what the 879 carried rules actually STYLE, and the measurement does not support it. This script
// is that measurement, made reproducible so the conclusion can be re-checked rather than believed.
//
// ── What it found ─────────────────────────────────────────────────────────────────────────────────────
//
// Only ~5 % of the layer is prose. The rest is the portal's whole bespoke visual vocabulary — chart legends,
// dashboard cards, status badges, nav chrome — injected into the region as rendered HTML by ~25 C#
// templaters. That matters because it changes which of ADR 0018's alternatives is even available:
//
//   · Keep injecting rendered HTML (ADR 0016 + owner decision D2) ⇒ rules for those vocabularies are needed.
//   · Get them by hand-copying monolith rules ⇒ ADR 0018's EXPLICITLY REJECTED alternative ("a second
//     definition free to drift … it is not a migration, it is a rewrite").
//   · Get them by authoring genuinely new styling for 380 vocabularies ⇒ a visual redesign of the whole
//     portal, not a migration, and not what D5 asked for.
//   · De-inject the markup instead ⇒ needs structured per-family data in the IR, which ADR 0016 deliberately
//     does not carry. Story 23.4's Dev Notes name this exactly: "a named Epic 22 ask".
//
// So the residue is real and its blocker is architectural rather than effortful. Dev Notes → "Escalation,
// not improvisation" is the instruction being followed here: enumerate, attach the blocker, raise it — do
// NOT silently keep the layer alive, and do NOT unilaterally pull Epic 22 scope into this story.
//
// ── Bucketing ─────────────────────────────────────────────────────────────────────────────────────────
//
// Buckets are matched in priority order (prose first, so a `.doc-body table` counts as prose rather than as
// a card) against the selector text. This is a coarse classifier over 380 vocabularies and it is REPORTED as
// approximate: the per-rule list is the authority, the bucket totals are the summary. A rule's bucket
// changes nothing about its blocker being named — every carried rule appears in the output exactly once.

import { mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { MEASUREMENTS_DIR, pad } from './harness-lib.mjs'

const MANIFEST = join(import.meta.dirname, '..', 'assets', 'ir-content.manifest.json')

/**
 * Each bucket names the BLOCKER, not the look. "What would have to change for this rule to stop existing"
 * is the only question that makes a residue actionable — a bucket called "charts" tells the owner nothing.
 */
const BUCKETS = [
  {
    key: 'prose',
    // ⚠️ **Bounded on purpose — this bucket's size is the number that overturned owner decisions D3/D5.**
    // [Story 23.4 code review, finding F-11] The original pattern carried the bare alternatives `code-` and
    // `pre`, matched as substrings against raw selector text. `code-` claimed `.code-line` — the
    // `CodeFileSurface` source-listing gutter, which is not Markdig prose and is not authorable — and `pre`
    // matched anything merely CONTAINING those letters (`.preview`, `.prefix`, `.presentation`). An
    // over-claiming prose bucket inflates "authorable today", which is the one figure that argues AC #4's
    // FIRST branch is within reach. Anchored to class-name boundaries, and `code-` dropped entirely.
    //
    // Note which way this cuts: tightening it makes `prose` SMALLER, so the D3/D5 amendment is more strongly
    // supported than the original measurement showed, not less. The conclusion stands; the evidence for it is
    // now honest. The CLASSIFIER_SELFTEST below runs on every report so this cannot silently loosen again.
    match: /(^|[.\s>+~[-])(doc-body|doc-header|prose|markdown|gherkin|pre|blockquote|footnote|admonition|callout)([\s.>+~\]:[-]|$)/i,
    blocker: 'NONE — authorable today',
    detail:
      'Markdig prose vocabulary. ADR 0016 puts rendered prose HTML in the IR, so this markup is injected ' +
      'and always will be — but its styling is generic typography that `web/` can legitimately AUTHOR ' +
      '(owner decision D5\'s "authored, owned prose stylesheet"). This is the one bucket the first branch ' +
      'of AC #4 actually reaches, and it is a single-digit percentage of the layer.',
  },
  {
    key: 'chart',
    match: /sunburst|donut|heatmap|chart|spark|treemap|quadrant|funnel|graph|legend|axis|plot|hierarchy|gauge|bar-|ribbon|lane|wheel/i,
    blocker: 'EPIC 22 — IR carries no structured chart data',
    detail:
      'Chart shells, legends, swatches and text-twin layout. The IR carries the finished inline SVG plus its ' +
      'sr-only twin as opaque HTML; there is no chart MODEL to render from, so these vocabularies cannot be ' +
      'componentized without Epic 22 adding structured per-chart data. ADR 0012 also forbids a second ' +
      'Hierarchy Explorer implementation, so re-drawing them in Vue is not an option either.',
  },
  {
    key: 'card',
    match: /card|tile|panel|chip|grid|row|list|table|band|section|surface|inventory|block|box|item/i,
    blocker: 'EPIC 22 — IR carries no per-family view models',
    detail:
      'Dashboard tiles, coverage/sprint/now-next cards, epic chips, requirement blocks. These are the ' +
      'page BODIES, produced by ~25 C# templaters as rendered HTML. Story 23.4 deliberately did NOT ' +
      'decompose them (that is the "nothing injected at all" alternative D5 rejected as pulling an Epic 22 ' +
      'dependency into this story). Largest bucket, and the one that most needs a real view-model contract.',
  },
  {
    key: 'chrome',
    match: /site-nav|site-menu|key-view|breadcrumb|local-context|toc-|ss-tab|wayfinding|skip-link|page-nav|pager|footer|site-/i,
    blocker: 'AC #3 BY DESIGN — C# composes the region, permanently',
    detail:
      'Site nav, key-views band, breadcrumb/wayfinding, TOC rail, tab strips. ⚠️ This bucket is NOT waiting ' +
      'on anything and will never empty: owner decision D2 keeps C# composing nav + wayfinding + <main> into ' +
      'the IR region, and the webview and SPA consume that same path. So these rules describe markup this ' +
      'story deliberately KEEPS injecting. They belong in an owned sheet in `web/`, but their provenance ' +
      'problem is real — they are currently a generated extract of the monolith.',
  },
  {
    key: 'status',
    match: /status|stage|badge|coverage|satisfaction|req-status|epic-status|pill|swatch/i,
    blocker: 'TOKEN BRIDGE — must stay in step with the six --status-* tokens',
    detail:
      'Status badges, stage swatches, satisfaction brackets. These cannot simply be re-authored in `web/`: ' +
      'the six `--status-*` custom properties in `specscribe.css` are the single stage→colour source of ' +
      'truth, and `npm run check:tokens` gates both directions. Re-authoring the RULES while the TOKENS stay ' +
      'generated is how the two drift — and UX-DR17 (no state by colour alone) is enforced by badge SHAPE, ' +
      'so a partial re-author risks an accessibility regression, not just a cosmetic one.',
  },
]

const OTHER = {
  key: 'other',
  blocker: 'EPIC 22 — uncategorized injected vocabulary',
  detail:
    'Vocabularies the coarse classifier above did not claim (explorer layout, related-work rails, story ' +
    'kickers, and the long tail of one-off page furniture). Same blocker class as `card`: injected rendered ' +
    'HTML with no view model behind it.',
}

// [Story 23.4 code review, finding F-17] Actionable failures, not a bare ENOENT/TypeError three frames deep.
// `check-ir-content.mjs` already gives this guidance for the same missing file; this script did not.
let manifest
try {
  manifest = JSON.parse(readFileSync(MANIFEST, 'utf8'))
} catch (err) {
  console.error(
    `✖ Could not read ${MANIFEST}\n` +
      `  ${err.message}\n` +
      `  The residue report derives entirely from the extracted manifest. Run \`npm run extract:ir-content\`\n` +
      `  first (it needs a generated IR — see CLAUDE.md § Changing specscribe.css for the ordering).`,
  )
  process.exit(1)
}
if (!Array.isArray(manifest.rules)) {
  console.error(`✖ ${MANIFEST} has no \`rules\` array — it is malformed or from an incompatible version.`)
  process.exit(1)
}
const carried = manifest.rules.filter((r) => r.carried)

const bucketOf = (selector) => BUCKETS.find((b) => b.match.test(selector))?.key ?? 'other'

/**
 * ⚠️ The classifier validates itself before it is trusted. [Story 23.4 code review, finding F-11]
 *
 * This coarse classifier produces the "prose and authorable today" percentage in the VERDICT below — the
 * single number used to AMEND owner decisions D3/D5 and take AC #4's second branch. It had no test, is not in
 * the coverage config, and carried substring alternatives that silently claimed non-prose vocabulary.
 *
 * These cases are the ones that were actually wrong, plus the boundaries most likely to rot. A misclassified
 * headline number is worse than no number, so this ABORTS rather than warning: the report must not print a
 * verdict its own classifier cannot pass.
 */
const CLASSIFIER_SELFTEST = [
  // The two that were genuinely misfiled before the boundaries were anchored.
  ['.code-line', 'other', 'the CodeFileSurface source gutter is not Markdig prose'],
  ['.ir-content .preview-pane', 'other', '"pre" must not match merely because a selector contains it'],
  // Boundaries that must keep working.
  ['.doc-body p', 'prose', 'the canonical prose body'],
  ['.doc-header', 'prose', 'the prose page title block'],
  ['.ir-content pre', 'prose', 'a real <pre> element selector IS prose'],
  ['.sunburst-legend', 'chart', 'chart vocabulary outranks nothing here but must stay classified'],
  ['.site-nav a', 'chrome', 'nav is the permanent AC #3 bucket'],
  ['.status-badge', 'status', 'status rides the token bridge'],
]
const selftestFailures = CLASSIFIER_SELFTEST.filter(([sel, want]) => bucketOf(sel) !== want)
if (selftestFailures.length > 0) {
  console.error('✖ The residue classifier failed its own self-test — its output would be misleading:')
  for (const [sel, want, why] of selftestFailures) {
    console.error(`    ${sel}  → got "${bucketOf(sel)}", expected "${want}"  (${why})`)
  }
  console.error('  Fix BUCKETS before trusting any percentage this script prints.')
  process.exit(1)
}

const byBucket = new Map([...BUCKETS.map((b) => [b.key, []]), [OTHER.key, []]])
for (const rule of carried) byBucket.get(bucketOf(rule.selector)).push(rule.selector)

const lines = []
const say = (s = '') => {
  lines.push(s)
  console.log(s)
}

const total = carried.length

// ⚠️ Zero carried rules is AC #4's FIRST-BRANCH SUCCESS STATE — the layer retired, manifest empty.
// [Story 23.4 code review, finding F-17] The report used to divide by `total` regardless, so in exactly that
// state it printed a table of `NaN%` under the headline "AC #4's FIRST branch is not reachable" and exited 0.
// The script whose whole purpose is to measure whether the layer can be retired produced its most
// authoritative-looking output at the moment the layer was already gone.
if (total === 0) {
  console.log('')
  console.log('ir-content.css carries ZERO rules.')
  console.log('')
  console.log("This is AC #4's FIRST branch reached: the transitional layer is empty and can be DELETED,")
  console.log('along with its manifest, its extractor, `npm run check:ir-content` and CONVENTIONS.md §10.')
  console.log('Mark ADR 0018 Superseded/Retired with the story that did it. There is no residue to enumerate.')
  console.log('')
  process.exit(0)
}
const pct = (n) => `${((n / total) * 100).toFixed(1)}%`

say('')
say('Story 23.4 AC #4 — ir-content.css residue, enumerated with a named blocker per rule')
say('')
say(pad('bucket', 10) + pad('rules', 8) + pad('share', 9) + 'blocker')
say('-'.repeat(100))
for (const b of [...BUCKETS, OTHER]) {
  const n = byBucket.get(b.key).length
  say(pad(b.key, 10) + pad(n, 8) + pad(pct(n), 9) + b.blocker)
}
say('-'.repeat(100))
say(pad('TOTAL', 10) + pad(total, 8))
say('')

const authorable = byBucket.get('prose').length
say(`VERDICT: AC #4's FIRST branch is not reachable, and the measurement is why.`)
say(`  ${authorable} of ${total} rules (${pct(authorable)}) are prose and authorable today.`)
say(
  `  The other ${total - authorable} (${pct(total - authorable)}) style INJECTED bespoke vocabulary across ` +
    `${new Set(carried.map((r) => (r.selector.match(/\.([a-z0-9-]+)/i) ?? [])[1] ?? '(element)')).size} distinct classes.`,
)
say('  Retiring them means either ADR 0018\'s explicitly rejected hand-copy, a full visual redesign, or')
say('  structured per-family data in the IR — the last of which is an EPIC 22 ASK, raised, not improvised.')
say('')
say('  Taking AC #4\'s SECOND branch: residue enumerated below, ADR 0018 amended, Epic 22 ask recorded.')
say('')

for (const b of [...BUCKETS, OTHER]) {
  const sels = byBucket.get(b.key)
  say(`── ${b.key} (${sels.length}) — ${b.blocker}`)
  say(`   ${b.detail.replace(/\s+/g, ' ')}`)
  say('')
}

mkdirSync(MEASUREMENTS_DIR, { recursive: true })
writeFileSync(join(MEASUREMENTS_DIR, 'ir-content-residue.txt'), lines.join('\n') + '\n')
writeFileSync(
  join(MEASUREMENTS_DIR, 'ir-content-residue.json'),
  JSON.stringify(
    {
      generatedBy: 'web/scripts/report-ir-content-residue.mjs',
      story: '23.4 AC #4 (second branch)',
      totalCarriedRules: total,
      authorableToday: authorable,
      buckets: [...BUCKETS, OTHER].map((b) => ({
        bucket: b.key,
        blocker: b.blocker,
        detail: b.detail,
        rules: byBucket.get(b.key),
      })),
    },
    null,
    2,
  ) + '\n',
)
say('  wrote measurements/ir-content-residue.txt + measurements/ir-content-residue.json')
