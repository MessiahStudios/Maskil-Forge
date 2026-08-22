import assert from 'node:assert/strict'
import test from 'node:test'
import { analyzeSavedVocalTake, calculateLoudnessFrames } from './loudnessAnalysis.js'

function buffer(channels, sampleRate) {
  return {
    sampleRate,
    length: channels[0].length,
    numberOfChannels: channels.length,
    getChannelData: index => channels[index],
  }
}

test('loudness frames cover decoded audio contiguously with bounded dBFS measurements', () => {
  const frames = calculateLoudnessFrames(buffer([
    new Float32Array(12_000).fill(0.5),
    new Float32Array(12_000).fill(-0.5),
  ], 24_000))

  assert.deepEqual(frames, [
    { startMilliseconds: 0, durationMilliseconds: 250, rmsDecibels: -6.021, peakDecibels: -6.021 },
    { startMilliseconds: 250, durationMilliseconds: 250, rmsDecibels: -6.021, peakDecibels: -6.021 },
  ])
})

test('silent and partial final frames retain the evidence floor and exact decoded span', () => {
  const frames = calculateLoudnessFrames(buffer([new Float32Array(10)], 16))

  assert.deepEqual(frames, [
    { startMilliseconds: 0, durationMilliseconds: 250, rmsDecibels: -120, peakDecibels: -120 },
    { startMilliseconds: 250, durationMilliseconds: 250, rmsDecibels: -120, peakDecibels: -120 },
    { startMilliseconds: 500, durationMilliseconds: 125, rmsDecibels: -120, peakDecibels: -120 },
  ])
})

test('saved-take analysis closes its decoder after producing deterministic frames', async () => {
  let closed = false
  const environment = {
    fetch: async () => ({ ok: true, arrayBuffer: async () => new ArrayBuffer(4) }),
    AudioContext: class {
      async decodeAudioData() { return buffer([new Float32Array(2).fill(1)], 8) }
      async close() { closed = true }
    },
  }

  const frames = await analyzeSavedVocalTake('/api/take', environment)

  assert.deepEqual(frames, [{ startMilliseconds: 0, durationMilliseconds: 250, rmsDecibels: 0, peakDecibels: 0 }])
  assert.equal(closed, true)
})

test('invalid or overlong decoded audio is rejected before any report is sent', () => {
  assert.throws(() => calculateLoudnessFrames(buffer([new Float32Array(0)], 48_000)), /measurable audio/)
  assert.throws(() => calculateLoudnessFrames(buffer([new Float32Array(61)], 1)), /one-minute/)
})
