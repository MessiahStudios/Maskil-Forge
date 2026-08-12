import assert from 'node:assert/strict'
import test from 'node:test'
import { assemblePartNotes, midiNumber, scheduleAssembledNotes } from './partAuditionModel.js'

const pitch = (letter, octave, accidental = 'Natural') => ({ letter, accidental, octave })
const note = (id, letter, octave, startTick, durationTicks = 480, velocity = 96) => ({
  id, pitch: pitch(letter, octave), startTick, durationTicks, velocity,
})

test('assemblePartNotes resolves and dedupes notes referenced by musical parts', () => {
  const notes = [
    note('a', 'C', 4, 0),
    note('b', 'E', 4, 0),
    note('c', 'G', 4, 480),
    note('d', 'A', 4, 960),
  ]
  const parts = [
    { sectionId: 'verse', noteEventIds: ['a', 'b'] },
    { sectionId: 'verse', noteEventIds: ['b', 'c'] },
    { sectionId: 'chorus', noteEventIds: ['d'] },
  ]

  const assembled = assemblePartNotes(parts, notes, 'verse')
  assert.deepEqual(assembled.map(item => item.id), ['a', 'b', 'c'])
})

test('assemblePartNotes ignores missing note references', () => {
  const assembled = assemblePartNotes(
    [{ sectionId: 'verse', noteEventIds: ['missing', 'a'] }],
    [note('a', 'C', 3, 120)],
  )
  assert.equal(assembled.length, 1)
  assert.equal(assembled[0].id, 'a')
})

test('scheduleAssembledNotes converts absolute ticks and normalizes the earliest onset to zero', () => {
  const scheduled = scheduleAssembledNotes([
    note('late', 'G', 4, 960, 240, 100),
    note('early', 'C', 4, 480, 480, 80),
  ], { beatsPerMinute: 120, ticksPerQuarterNote: 480 })

  assert.equal(midiNumber(pitch('C', 4)), 60)
  assert.deepEqual(scheduled, [
    { midi: 60, startSeconds: 0, durationSeconds: 0.5, velocity: 80 },
    { midi: 67, startSeconds: 0.5, durationSeconds: 0.25, velocity: 100 },
  ])
})
