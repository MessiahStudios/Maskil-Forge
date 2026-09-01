import assert from 'node:assert/strict'
import test from 'node:test'
import { plannedSongTiming } from './timingAuthorityModel.js'

test('planned timing names the current form boundary without calling it final duration', () => {
  const result = plannedSongTiming([
    { start: { bar: 1 }, durationBars: 8 },
    { start: { bar: 9 }, durationBars: 8 },
    { start: { bar: 57 }, durationBars: 8 },
  ])

  assert.equal(result.sectionCount, 3)
  assert.equal(result.plannedBars, 64)
  assert.equal(result.endBarExclusive, 65)
  assert.equal(result.label, '64 planned bars · current form ends when bar 65 begins')
  assert.match(result.structureNotice, /editable arrangement planning/)
  assert.match(result.structureNotice, /final performance can be shorter or longer/)
  assert.match(result.midiNotice, /Later stored notes or controller events can extend it/)
})

test('a one-bar form uses a singular label', () => {
  const result = plannedSongTiming([{ start: { bar: 1 }, durationBars: 1 }])

  assert.equal(result.label, '1 planned bar · current form ends when bar 2 begins')
})

test('songs without sections keep event-derived MIDI duration explicit', () => {
  const result = plannedSongTiming([])

  assert.equal(result.plannedBars, 0)
  assert.equal(result.endBarExclusive, null)
  assert.equal(result.label, 'No planned song form yet.')
  assert.match(result.structureNotice, /Lyrics alone do not determine musical duration/)
  assert.match(result.midiNotice, /latest stored event/)
})
