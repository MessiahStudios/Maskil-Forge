import assert from 'node:assert/strict'
import test from 'node:test'
import { analyzeSavedVocalTakePitch, calculatePitchFrames } from './pitchAnalysis.js'

function buffer(channels, sampleRate) {
  return {
    sampleRate,
    length: channels[0].length,
    numberOfChannels: channels.length,
    getChannelData: index => channels[index],
  }
}

function sine(frequencyHertz, durationSeconds, sampleRate, amplitude = 0.5) {
  return Float32Array.from({ length: Math.round(durationSeconds * sampleRate) }, (_, index) =>
    amplitude * Math.sin(2 * Math.PI * frequencyHertz * index / sampleRate))
}

test('pitch analysis reports confident voiced frequency without creating note data', () => {
  const frames = calculatePitchFrames(buffer([sine(440, 1, 8000)], 8000))

  assert.equal(frames.length, 5)
  assert.deepEqual(frames.map(frame => frame.startMilliseconds), [0, 200, 400, 600, 800])
  for (const frame of frames) {
    assert.equal(frame.durationMilliseconds, 80)
    assert.ok(Math.abs(frame.frequencyHertz - 440) < 3, `Expected 440 Hz, received ${frame.frequencyHertz}`)
    assert.ok(frame.confidence >= 0.95)
    assert.deepEqual(Object.keys(frame), ['startMilliseconds', 'durationMilliseconds', 'frequencyHertz', 'confidence'])
  }
})

test('silence and very quiet audio produce no pitch claim', () => {
  assert.deepEqual(calculatePitchFrames(buffer([new Float32Array(8000)], 8000)), [])
  assert.deepEqual(calculatePitchFrames(buffer([sine(220, 1, 8000, 0.001)], 8000)), [])
})

test('downsampled analysis retains frequency and respects the upper search boundary', () => {
  const downsampled = calculatePitchFrames(buffer([sine(330, 0.2, 44_100)], 44_100))
  const upperBoundary = calculatePitchFrames(buffer([sine(1000, 0.2, 8000)], 8000))

  assert.ok(Math.abs(downsampled[0].frequencyHertz - 330) < 3)
  assert.ok(Math.abs(upperBoundary[0].frequencyHertz - 1000) < 3)
})

test('saved-take pitch analysis closes its decoder', async () => {
  let closed = false
  const environment = {
    fetch: async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(4) }),
    AudioContext: class {
      async decodeAudioData() { return buffer([sine(220, 0.2, 8000)], 8000) }
      async close() { closed = true }
    },
  }

  const frames = await analyzeSavedVocalTakePitch('/api/take', environment)

  assert.equal(frames.length, 1)
  assert.ok(Math.abs(frames[0].frequencyHertz - 220) < 3)
  assert.equal(closed, true)
})

test('invalid and overlong decoded audio is rejected before reporting pitch', () => {
  assert.throws(() => calculatePitchFrames(buffer([new Float32Array(0)], 8000)), /measurable audio/)
  assert.throws(() => calculatePitchFrames(buffer([new Float32Array(100)], 1000)), /sample rate/)
  assert.throws(() => calculatePitchFrames(buffer([new Float32Array(480_016)], 8000)), /one-minute/)
})
