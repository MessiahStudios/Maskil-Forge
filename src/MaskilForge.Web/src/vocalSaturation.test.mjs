import assert from 'node:assert/strict'
import test from 'node:test'
import { renderVocalSaturation, vocalSaturation } from './vocalSaturation.js'

const buffer = channels => ({ sampleRate: 48000, length: channels[0].length, numberOfChannels: channels.length, getChannelData: index => channels[index] })

test('fixed saturation colors a loud sample without raising it or moving silence', () => {
  const loud = 0.9
  const rendered = renderVocalSaturation(buffer([Float32Array.of(-loud, 0, loud), Float32Array.of(loud, 0, -loud)]))
  const expected = loud - vocalSaturation.colorAmount * loud ** 3
  assert.ok(expected < loud)
  assert.ok(Math.abs(rendered[0][0] + expected) < 1e-6)
  assert.equal(rendered[0][1], 0)
  assert.ok(Math.abs(rendered[0][2] - expected) < 1e-6)
  assert.ok(Math.abs(rendered[1][2] + expected) < 1e-6)
  assert.ok(Math.abs(renderVocalSaturation(buffer([Float32Array.of(0.01)]))[0][0] - 0.01) < 0.000001)
})

test('saturation preview refuses other amounts and samples beyond headroom', () => {
  assert.throws(() => renderVocalSaturation(buffer([Float32Array.of(0.2)]), { colorAmount: 0.4 }), /fixed saturation/)
  assert.throws(() => renderVocalSaturation(buffer([Float32Array.of(1.01)])), /headroom/)
})
