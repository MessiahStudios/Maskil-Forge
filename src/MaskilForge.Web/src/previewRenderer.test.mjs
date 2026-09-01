import assert from 'node:assert/strict'
import test from 'node:test'
import { builtInPreviewRendererId, previewEnvelopeForDuration, previewRendererSummary, previewVoiceForInstrument } from './previewRendererModel.js'

test('the built-in renderer has a stable non-project identity', () => {
  assert.equal(builtInPreviewRendererId, 'maskil-browser-preview-v1')
})

test('every catalog instrument receives an explicit preview voice', () => {
  const catalogIds = [
    'cello', 'acoustic-guitar', 'piano', 'electric-bass', 'drum-kit', 'violin',
    'flute', 'clarinet', 'trumpet', 'synth-pad', 'synth-lead', 'electric-guitar',
  ]
  const mapped = catalogIds.map(previewVoiceForInstrument)

  assert.deepEqual(mapped.map(item => item.instrumentProfileId), catalogIds)
  assert.equal(new Set(mapped.map(item => `${item.oscillatorType}:${item.attackSeconds}:${item.filterFrequencyHz}:${item.pitchDrop}`)).size, catalogIds.length)
  assert.equal(previewVoiceForInstrument('drum-kit').pitchDrop, true)
})

test('unassigned and unknown instruments remain audible through the neutral voice', () => {
  assert.equal(previewVoiceForInstrument(null).name, 'Neutral')
  assert.equal(previewVoiceForInstrument('future-instrument').instrumentProfileId, null)
})

test('short notes clamp attack and release without exceeding their duration', () => {
  const envelope = previewEnvelopeForDuration(previewVoiceForInstrument('synth-pad'), 0.06)
  assert.equal(envelope.attackSeconds, 0.02)
  assert.equal(envelope.releaseSeconds, 0.02)
  assert.equal(Math.abs(envelope.sustainSeconds - 0.02) < Number.EPSILON, true)
})

test('renderer summary names the distinct audible voices', () => {
  assert.equal(previewRendererSummary([
    { instrumentProfileId: 'cello' },
    { instrumentProfileId: 'cello' },
    { instrumentProfileId: 'piano' },
    { instrumentProfileId: null },
  ]), 'Built-in instrument preview · Cello, Piano, Neutral')
})
