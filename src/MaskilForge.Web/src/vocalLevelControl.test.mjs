import assert from 'node:assert/strict'
import test from 'node:test'
import { renderVocalLevelControl, vocalLevelControl } from './vocalLevelControl.js'

const buffer = (channels, sampleRate = 48000) => ({ sampleRate, length: channels[0].length, numberOfChannels: channels.length, getChannelData: i => channels[i] })
const constant = (value, length = 48000) => Float32Array.from({ length }, () => value)
const threshold = 10 ** (-18 / 20)
const settledGain = level => level <= threshold ? 1 : (level / threshold) ** (1 / 2 - 1)

test('level control leaves quiet audio unchanged and eases a sustained peak without boosting it', () => {
  const quiet = constant(.05)
  assert.deepEqual(renderVocalLevelControl(buffer([quiet]))[0], quiet)
  const loud = constant(.5)
  const output = renderVocalLevelControl(buffer([loud]))[0]
  const gain = settledGain(.5)
  assert.ok(Math.abs(output.at(-1) - .5 * gain) < 1e-4)
  assert.ok(output.every((sample, index) => sample <= loud[index] + 1e-12 && sample > 0))
  assert.equal(vocalLevelControl.processorId, 'maskil.vocal.level-control.v1')
})

test('stereo channels share one level envelope', () => {
  const left = constant(.5), right = constant(.1)
  const [outLeft, outRight] = renderVocalLevelControl(buffer([left, right]))
  const gain = settledGain(.5)
  assert.ok(Math.abs(outLeft.at(-1) - .5 * gain) < 1e-4)
  assert.ok(Math.abs(outRight.at(-1) - .1 * gain) < 1e-4)
})

test('level control rejects invalid audio and non-fixed settings', () => {
  assert.throws(() => renderVocalLevelControl(buffer([new Float32Array([NaN])])), /invalid samples/)
  assert.throws(() => renderVocalLevelControl(buffer([new Float32Array([1.1])])), /headroom/)
  assert.throws(() => renderVocalLevelControl(buffer([constant(.2, 48000 * 61)])), /one minute/)
  assert.throws(() => renderVocalLevelControl(buffer([constant(.2), constant(.2), constant(.2)])), /mono or stereo/)
  assert.throws(() => renderVocalLevelControl(buffer([constant(.2)]), { ...vocalLevelControl, thresholdDecibels: -12 }), /fixed level-control/)
})
