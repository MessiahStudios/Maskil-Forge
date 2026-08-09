import assert from 'node:assert/strict'
import test from 'node:test'
import { positionInQuarterNotes, previewMidiNotes } from './chordAuditionModel.js'

const chord = (overrides = {}) => ({
  chord: { root: 'C', accidental: 'Natural', quality: 'Major' },
  start: { bar: 1, beat: 1, tick: 0 },
  voicing: null,
  ...overrides,
})

test('audition prefers registered voices', () => {
  const notes = previewMidiNotes(chord({ voicing: { voices: [
    { pitch: { letter: 'G', accidental: 'Natural', octave: 2 } },
    { pitch: { letter: 'D', accidental: 'Natural', octave: 3 } },
  ] } }))
  assert.deepEqual(notes, [43, 50])
})

test('audition creates a temporary root-position preview voicing', () => {
  assert.deepEqual(previewMidiNotes(chord({ chord: { root: 'F', accidental: 'Sharp', quality: 'Minor' } })), [54, 57, 61])
})

test('audition converts section-relative meter positions to quarter notes', () => {
  const position = positionInQuarterNotes(chord({ start: { bar: 2, beat: 2, tick: 240 } }), {
    beatsPerBar: 6, beatUnit: 8, ticksPerQuarterNote: 480,
  })
  assert.equal(position, 4)
})
