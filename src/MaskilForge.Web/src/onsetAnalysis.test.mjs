import assert from 'node:assert/strict'
import test from 'node:test'
import { analyzeSavedVocalTakeOnsets, calculateOnsetEvents } from './onsetAnalysis.js'

function buffer(channels, sampleRate) {
  return {
    sampleRate,
    length: channels[0].length,
    numberOfChannels: channels.length,
    getChannelData: index => channels[index],
  }
}

function gatedTone(durationSeconds, sampleRate, gates, amplitude = 0.7) {
  return Float32Array.from({ length: Math.round(durationSeconds * sampleRate) }, (_, index) => {
    const seconds = index / sampleRate
    const active = gates.some(([start, end]) => seconds >= start && seconds < end)
    return active ? amplitude * Math.sin(2 * Math.PI * 220 * seconds) : 0
  })
}

test('onset analysis reports separated energy-rise candidates without creating timing decisions', () => {
  const events = calculateOnsetEvents(buffer([gatedTone(1, 8000, [[0.2, 0.35], [0.6, 0.75]])], 8000))

  assert.equal(events.length, 2)
  assert.ok(Math.abs(events[0].startMilliseconds - 176) <= 16)
  assert.ok(Math.abs(events[1].startMilliseconds - 576) <= 16)
  for (const event of events) {
    assert.equal(event.startMilliseconds % 16, 0)
    assert.equal(event.durationMilliseconds, 32)
    assert.ok(event.strength >= 0 && event.strength <= 1)
    assert.ok(event.confidence >= 0.6 && event.confidence <= 1)
    assert.deepEqual(Object.keys(event), ['startMilliseconds', 'durationMilliseconds', 'strength', 'confidence'])
  }
})

test('silence, very quiet audio, and a gradual fade produce no onset claim', () => {
  const gradual = Float32Array.from({ length: 8000 }, (_, index) =>
    (index / 8000) * 0.25 * Math.sin(2 * Math.PI * 220 * index / 8000))

  assert.deepEqual(calculateOnsetEvents(buffer([new Float32Array(8000)], 8000)), [])
  assert.deepEqual(calculateOnsetEvents(buffer([gatedTone(1, 8000, [[0.2, 0.8]], 0.005)], 8000)), [])
  assert.deepEqual(calculateOnsetEvents(buffer([gradual], 8000)), [])
})

test('nearby rises are collapsed by the refractory boundary', () => {
  const events = calculateOnsetEvents(buffer([gatedTone(0.5, 44_100, [[0.1, 0.13], [0.17, 0.25]])], 44_100))

  assert.equal(events.length, 1)
})

test('saved-take onset analysis closes its decoder', async () => {
  let closed = false
  const environment = {
    fetch: async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(4) }),
    AudioContext: class {
      async decodeAudioData() { return buffer([gatedTone(0.4, 8000, [[0.1, 0.25]])], 8000) }
      async close() { closed = true }
    },
  }

  const events = await analyzeSavedVocalTakeOnsets('/api/take', environment)

  assert.equal(events.length, 1)
  assert.equal(closed, true)
})

test('invalid and overlong decoded audio is rejected before reporting onsets', () => {
  assert.throws(() => calculateOnsetEvents(buffer([new Float32Array(0)], 8000)), /measurable audio/)
  assert.throws(() => calculateOnsetEvents(buffer([new Float32Array(100)], 500)), /sample rate/)
  assert.throws(() => calculateOnsetEvents(buffer([new Float32Array(480_016)], 8000)), /one-minute/)
})
