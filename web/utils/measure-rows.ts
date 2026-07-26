/**
 * The dataset behind AC #4's payload experiment.
 *
 * Deterministic by construction — no clock, no randomness — so two `nuxt generate` runs produce
 * byte-identical output and a measured delta is a real difference rather than noise. (The same discipline
 * the C# golden fingerprint enforces on its side.)
 *
 * Deliberately built rather than committed as JSON: what matters is that all three measured routes render
 * the SAME rows through the SAME primitive, differing only in how the data reaches the component.
 */

export interface MeasureRow {
  id: string
  summary: string
  stage: 'pending' | 'drafted' | 'ready' | 'active' | 'review' | 'done'
  label: string
  chips: string[]
}

const STAGES = [
  { stage: 'pending', label: 'Pending' },
  { stage: 'drafted', label: 'Drafted' },
  { stage: 'ready', label: 'Ready for dev' },
  { stage: 'active', label: 'In development' },
  { stage: 'review', label: 'In review' },
  { stage: 'done', label: 'Done' },
] as const

/** Rows shaped like a real story index — the surface 23.3 migrates first. */
export function buildRows(count = 200): MeasureRow[] {
  return Array.from({ length: count }, (_, i) => {
    const s = STAGES[i % STAGES.length]!
    const epic = Math.floor(i / 8) + 1
    const story = (i % 8) + 1
    return {
      id: `${epic}-${story}`,
      summary: `Story ${epic}.${story}: a representative row of the length an index page actually carries, long enough that its share of the payload is not a rounding error.`,
      stage: s.stage,
      label: s.label,
      chips: [`Epic ${epic}`, `${(i % 5) + 1} tasks`],
    }
  })
}
