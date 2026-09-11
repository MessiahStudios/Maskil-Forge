import assert from 'node:assert/strict'
import test from 'node:test'
import { vocalProductionDescriptors, vocalProductionIntentSummary } from './vocalProductionIntentModel.js'

test('vocal-production vocabulary stays artist-facing and processor independent', () => {
  assert.deepEqual(vocalProductionDescriptors.map(item => item.id), [
    'Clean', 'Warm', 'Intimate', 'Forward', 'SoftRock', 'Cinematic', 'Aggressive',
  ])
  assert.equal(vocalProductionDescriptors.some(item => /compress|equalizer|plugin|vst/i.test(`${item.label} ${item.description}`)), false)
})

test('vocal-production summary preserves the selected result language', () => {
  assert.equal(vocalProductionIntentSummary(null), 'No vocal direction chosen yet.')
  assert.equal(vocalProductionIntentSummary({ descriptors: ['Warm', 'Intimate'], artistNotes: '', updatedUtc: '2026-09-01T00:00:00Z' }), 'Warm · Intimate')
})
