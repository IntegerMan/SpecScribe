import { describe, expect, it } from 'vitest'
import { allocateNextPreviewTag } from '../../.github/scripts/release/next-preview-tag.mjs'

describe('allocateNextPreviewTag', () => {
  it('bootstraps the first preview tag', () => {
    expect(allocateNextPreviewTag('')).toBe('v0.1.0-preview.1')
  })

  it('advances the patch from the highest semantic base and keeps preview numbers global', () => {
    expect(allocateNextPreviewTag('v0.1.0-preview.4\nv0.2.0-preview.1\nv0.1.1-preview.9\nignored')).toBe('v0.2.1-preview.10')
  })

  it('uses a reviewed release base', () => {
    expect(allocateNextPreviewTag('v0.2.1-preview.10', '1.0.0\n')).toBe('v1.0.0-preview.11')
  })

  it('rejects malformed and backward release bases', () => {
    expect(() => allocateNextPreviewTag('', '1.0')).toThrow(/MAJOR\.MINOR\.PATCH/)
    expect(() => allocateNextPreviewTag('v1.0.0-preview.1', '0.2.0')).toThrow(/below the latest release base/)
  })
})