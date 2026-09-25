import assert from 'node:assert/strict'
import test from 'node:test'
import { renderVocalCharacterCompression } from './vocalCharacterCompression.js'
import { renderVocalLevelControl } from './vocalLevelControl.js'

const buffer = channels => ({ sampleRate: 48000, length: channels[0].length, numberOfChannels: channels.length, getChannelData: index => channels[index] })
const peak = channel => channel.reduce((max, sample) => Math.max(max, Math.abs(sample)), 0)

test('character compression lifts a quiet phrase and restores the original peak', () => {
  const quiet = new Float32Array(2000).fill(0.05)
  const loud = new Float32Array(4000).fill(0.8)
  const source = Float32Array.of(...quiet, ...loud)
  const rendered = renderVocalCharacterCompression(buffer([source, source.slice()]))[0]
  const leveled = renderVocalLevelControl(buffer([source.slice()]))[0]
  assert.ok(rendered[0] > source[0])
  assert.ok(Math.abs(peak(rendered) - peak(source)) < 0.001)
  assert.equal(leveled[0], source[0])
  assert.ok(leveled[4000] < source[4000])
  assert.notDeepEqual(rendered, leveled)
})

test('character compression refuses other settings and samples beyond headroom', () => {
  assert.throws(() => renderVocalCharacterCompression(buffer([Float32Array.of(0.2)]), { thresholdDecibels: -24, ratio: 2, attackMilliseconds: 0, releaseMilliseconds: 250 }), /fixed character-compression/)
  assert.throws(() => renderVocalCharacterCompression(buffer([Float32Array.of(1.01)])), /headroom/)
})
