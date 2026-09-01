import assert from 'node:assert/strict'
import test from 'node:test'
import { assemblePartNotes, assemblePartVoices, formatTransportPosition, midiNumber, musicalPositionFromTicks, peakPolyphony, scheduleAbsoluteNotes, scheduleAbsolutePartVoices, scheduleAssembledNotes, scheduleAssembledPartVoices, tickFromSeconds } from './partAuditionModel.js'

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

test('assemblePartVoices preserves instrument ownership when a note is shared by two parts', () => {
  const voices = assemblePartVoices([
    { id: 'cello-part', sectionId: 'verse', label: 'Cello foundation', instrumentProfileId: 'cello', noteEventIds: ['a'] },
    { id: 'pad-part', sectionId: 'verse', label: 'Pad texture', instrumentProfileId: 'synth-pad', noteEventIds: ['a'] },
  ], [note('a', 'C', 3, 480)])

  assert.deepEqual(voices.map(item => ({ partId: item.partId, instrument: item.instrumentProfileId })), [
    { partId: 'cello-part', instrument: 'cello' },
    { partId: 'pad-part', instrument: 'synth-pad' },
  ])
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

test('scheduleAbsoluteNotes keeps song-timeline silence before the first note', () => {
  const scheduled = scheduleAbsoluteNotes([
    note('late', 'G', 4, 960, 240, 100),
  ], { beatsPerMinute: 120, ticksPerQuarterNote: 480 })
  assert.deepEqual(scheduled, [
    { midi: 67, startSeconds: 1, durationSeconds: 0.25, velocity: 100 },
  ])
})

test('part-voice schedules retain renderer inputs for section and song playback', () => {
  const voices = assemblePartVoices([
    { id: 'part', sectionId: 'verse', label: 'Bass pulse', instrumentProfileId: 'electric-bass', noteEventIds: ['late'] },
  ], [note('late', 'E', 2, 960, 240, 100)])
  const timing = { beatsPerMinute: 120, ticksPerQuarterNote: 480 }

  assert.deepEqual(scheduleAssembledPartVoices(voices, timing), [{
    midi: 40, startSeconds: 0, durationSeconds: 0.25, velocity: 100,
    partId: 'part', partLabel: 'Bass pulse', instrumentProfileId: 'electric-bass',
  }])
  assert.equal(scheduleAbsolutePartVoices(voices, timing)[0].startSeconds, 1)
})

test('musicalPositionFromTicks converts absolute ticks with constant meter', () => {
  const position = musicalPositionFromTicks(2_400, {
    beatsPerBar: 4, beatUnit: 4, ticksPerQuarterNote: 480,
  })
  assert.deepEqual(position, { bar: 2, beat: 2, tick: 0 })
  assert.equal(formatTransportPosition(position), 'Bar 2 · Beat 2')
})

test('tickFromSeconds reverses the tempo conversion', () => {
  assert.equal(tickFromSeconds(1, { beatsPerMinute: 120, ticksPerQuarterNote: 480 }), 960)
})

test('peakPolyphony measures overlap instead of total song length', () => {
  assert.equal(peakPolyphony([
    { midi: 60, startSeconds: 0, durationSeconds: 1, velocity: 90 },
    { midi: 64, startSeconds: 0, durationSeconds: 0.5, velocity: 90 },
    { midi: 67, startSeconds: 0.5, durationSeconds: 0.5, velocity: 90 },
    { midi: 72, startSeconds: 2, durationSeconds: 0.01, velocity: 90 },
  ]), 2)
})
