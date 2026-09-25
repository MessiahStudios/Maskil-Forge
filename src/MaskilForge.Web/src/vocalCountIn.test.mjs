import assert from 'node:assert/strict'
import test from 'node:test'
import { playVocalCountIn, vocalCountInDurationMs, vocalCountInOffsets } from './vocalCountIn.js'

test('count-in places one click on each beat and starts recording on the next bar', () => {
  assert.deepEqual(vocalCountInOffsets(120, 4), [0, 500, 1000, 1500])
  assert.equal(vocalCountInDurationMs(120, 4), 2000)
  assert.equal(vocalCountInDurationMs(60, 1), 1000)
  assert.throws(() => vocalCountInOffsets(10, 4), /tempo/)
  assert.throws(() => vocalCountInOffsets(120, 0), /meter/)
})

test('count-in plays the bar and never asks for a microphone', async () => {
  const oscillators = []
  class FakeContext {
    currentTime = 1
    createOscillator() {
      const oscillator = {
        frequency: { value: 0 },
        connect() {},
        start(time) { this.started = time },
        stop(time) { this.stopped = time },
      }
      oscillators.push(oscillator)
      return oscillator
    }
    createGain() {
      return { gain: { setValueAtTime() {}, exponentialRampToValueAtTime() {} }, connect() {} }
    }
    get destination() { return {} }
    close() { return Promise.resolve() }
  }
  const waits = []
  await playVocalCountIn({
    beatsPerMinute: 120,
    beatsPerBar: 4,
    environment: {
      AudioContext: FakeContext,
      setTimeout(fn, ms) { waits.push(ms); fn(); return 1 },
      clearTimeout() {},
    },
  })
  assert.equal(oscillators.length, 4)
  assert.equal(oscillators[0].frequency.value, 660)
  assert.equal(oscillators[3].frequency.value, 880)
  assert.equal(oscillators[3].started, 1.05 + 1.5)
  assert.equal(waits[0], 2000)
})

test('cancelling a count-in stops the wait', async () => {
  const controller = new AbortController()
  class FakeContext {
    currentTime = 0
    createOscillator() { return { frequency: { value: 0 }, connect() {}, start() {}, stop() {} } }
    createGain() { return { gain: { setValueAtTime() {}, exponentialRampToValueAtTime() {} }, connect() {} } }
    get destination() { return {} }
    close() { return Promise.resolve() }
  }
  const pending = playVocalCountIn({
    beatsPerMinute: 120,
    beatsPerBar: 4,
    signal: controller.signal,
    environment: {
      AudioContext: FakeContext,
      setTimeout() { return 7 },
      clearTimeout() {},
    },
  })
  controller.abort()
  await assert.rejects(pending, /cancelled/)
})
