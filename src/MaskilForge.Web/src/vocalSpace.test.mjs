import assert from 'node:assert/strict'
import test from 'node:test'
import { renderVocalSpace, vocalSpace } from './vocalSpace.js'

const buffer = channels => ({ sampleRate: 48000, length: channels[0].length, numberOfChannels: channels.length, getChannelData: index => channels[index] })

test('space places a quieter reflection after the voice and leaves the dry sample', () => {
  const source = new Float32Array(48000)
  source[0] = 0.5
  const rendered = renderVocalSpace(buffer([source, source.slice()]))[0]
  const reflection = Math.round(vocalSpace.delayMilliseconds / 1000 * 48000)
  assert.equal(rendered[0], 0.5)
  assert.ok(Math.abs(rendered[reflection] - 0.5 * vocalSpace.wetAmount) < 1e-6)
  assert.equal(rendered[1], 0)
})

test('space keeps a full-scale voice inside headroom and refuses other settings', () => {
  const hot = new Float32Array(8000).fill(1)
  const rendered = renderVocalSpace(buffer([hot]))[0]
  assert.ok(rendered.every(sample => sample <= 1 && sample >= 0.99))
  assert.throws(() => renderVocalSpace(buffer([new Float32Array([0.2])]), { delayMilliseconds: 40, feedback: 0.3, wetAmount: 0.15 }), /fixed space/)
  assert.throws(() => renderVocalSpace(buffer([Float32Array.of(1.01)])), /headroom/)
})
