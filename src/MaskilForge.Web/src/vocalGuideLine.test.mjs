import assert from 'node:assert/strict'
import test from 'node:test'
import { guideLineFromNotes, midiFromSpelling, playVocalGuideLine } from './vocalGuideLine.js'

test('the rehearsal line follows the highest written note and the song tempo', () => {
  assert.equal(midiFromSpelling('A', 'Natural', 4), 69)
  const { line, trimmed } = guideLineFromNotes([
    { midi: 60, startTick: 0, durationTicks: 480 },
    { midi: 64, startTick: 0, durationTicks: 240 },
    { midi: 67, startTick: 480, durationTicks: 480 },
  ], 120, 480)
  assert.equal(trimmed, false)
  assert.deepEqual(line, [
    { midi: 64, startMs: 0, durationMs: 250 },
    { midi: 60, startMs: 250, durationMs: 250 },
    { midi: 67, startMs: 500, durationMs: 500 },
  ])
})

test('the rehearsal line stops after one minute and does not invent notes', () => {
  const { line, trimmed } = guideLineFromNotes([{ midi: 69, startTick: 0, durationTicks: 480 * 240 }], 120, 480)
  assert.equal(trimmed, true)
  assert.equal(line.length, 1)
  assert.equal(line[0].durationMs, 60_000)
  assert.deepEqual(guideLineFromNotes([], 120, 480), { line: [], trimmed: false })
})

test('playing the line schedules the written pitch and can be stopped', async () => {
  const oscillators = []
  class FakeContext {
    currentTime = 2
    createOscillator() {
      const oscillator = { type: '', frequency: { value: 0 }, connect() {}, start(time) { this.started = time }, stop(time) { this.stopped = time } }
      oscillators.push(oscillator)
      return oscillator
    }
    createGain() { return { gain: { setValueAtTime() {}, exponentialRampToValueAtTime() {} }, connect() {} } }
    get destination() { return {} }
    close() { return Promise.resolve() }
  }
  let cleared = false
  const playback = playVocalGuideLine([{ midi: 69, startMs: 0, durationMs: 500 }], {
    AudioContext: FakeContext,
    setTimeout() { return 4 },
    clearTimeout() { cleared = true },
  })
  playback.stop()
  await playback.done
  assert.equal(oscillators.length, 1)
  assert.equal(oscillators[0].frequency.value, 440)
  assert.equal(oscillators[0].started, 2.05)
  assert.equal(cleared, true)
  assert.throws(() => playVocalGuideLine([]), /does not invent a melody/)
})
