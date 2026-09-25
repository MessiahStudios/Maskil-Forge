import assert from 'node:assert/strict'
import test from 'node:test'
import { renderVocalCleanup } from './vocalCleanup.js'
import { renderVocalLevelControl } from './vocalLevelControl.js'

const buffer = channels => ({ sampleRate: 48000, length: channels[0].length, numberOfChannels: channels.length, getChannelData: index => channels[index] })

test('cleanup lowers a quiet gap and leaves a sung level in place', () => {
  const quiet = new Float32Array(48000).fill(0.001)
  const sung = new Float32Array(48000).fill(0.4)
  const cleanedQuiet = renderVocalCleanup(buffer([quiet]))[0]
  const cleanedSung = renderVocalCleanup(buffer([sung]))[0]
  const leveledQuiet = renderVocalLevelControl(buffer([quiet.slice()]))[0]
  assert.ok(cleanedQuiet.at(-1) < quiet.at(-1) * 0.3)
  assert.ok(cleanedQuiet[0] > quiet[0] * 0.9)
  assert.ok(Math.abs(cleanedSung.at(-1) - 0.4) < 0.001)
  assert.equal(leveledQuiet.at(-1), quiet.at(-1))
})

test('cleanup preview refuses other settings and samples beyond headroom', () => {
  assert.throws(() => renderVocalCleanup(buffer([Float32Array.of(0.2)]), { thresholdDecibels: -40, ratio: 2, attackMilliseconds: 10, releaseMilliseconds: 200 }), /fixed cleanup/)
  assert.throws(() => renderVocalCleanup(buffer([Float32Array.of(1.01)])), /headroom/)
})
