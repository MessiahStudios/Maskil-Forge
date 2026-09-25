import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptedVocalChainSteps, describeAcceptedVocalChain, renderAcceptedVocalChain } from './vocalChainPreview.js'
import { renderVocalLevelControl } from './vocalLevelControl.js'
import { renderVocalLowCut } from './vocalLowCut.js'
import { renderVocalSaturation } from './vocalSaturation.js'

const buffer = (channels, sampleRate = 48000) => ({ sampleRate, length: channels[0].length, numberOfChannels: channels.length, getChannelData: index => channels[index] })
const sine = (frequency, amplitude = .8, sampleRate = 48000) => Float32Array.from({ length: sampleRate }, (_, index) => amplitude * Math.sin(2 * Math.PI * frequency * index / sampleRate))
const asset = { id: 'take', sha256: 'a'.repeat(64) }
const lowCut = { assetId: 'take', role: 'CorrectiveTone', sourceSha256: asset.sha256, cutoffHertz: 80, q: 0.7071067811865476 }
const level = { assetId: 'take', role: 'TransparentDynamics', sourceSha256: asset.sha256 }
const saturation = { assetId: 'take', role: 'Saturation', sourceSha256: asset.sha256 }

test('accepted chain steps follow plan order and ignore other jobs and other takes', () => {
  const steps = acceptedVocalChainSteps(['Space', 'TransparentDynamics', 'CorrectiveTone'], [lowCut, level, { ...level, assetId: 'other' }], asset)
  assert.deepEqual(steps.map(step => step.role), ['TransparentDynamics', 'CorrectiveTone'])
  assert.equal(describeAcceptedVocalChain(steps), 'Level control → Low-cut')
  assert.equal(describeAcceptedVocalChain([]), 'Accept a low-cut, level-control, saturation, character-compression, or cleanup treatment on this take to hear it here.')
})

test('chain order changes a low tone because level control and the low-cut do not commute', () => {
  const source = sine(40)
  const lowThenLevel = renderAcceptedVocalChain(buffer([source]), [
    { role: 'CorrectiveTone', cutoffHertz: 80, q: 0.7071067811865476 },
    { role: 'TransparentDynamics' },
  ])[0]
  const levelThenLow = renderAcceptedVocalChain(buffer([source.slice()]), [
    { role: 'TransparentDynamics' },
    { role: 'CorrectiveTone', cutoffHertz: 80, q: 0.7071067811865476 },
  ])[0]
  const lowOnly = renderVocalLowCut(buffer([source.slice()]))[0]
  const levelOnly = renderVocalLevelControl(buffer([source.slice()]))[0]
  assert.notDeepEqual(lowThenLevel, levelThenLow)
  assert.deepEqual(lowThenLevel, renderVocalLevelControl(buffer([lowOnly]))[0])
  assert.deepEqual(levelThenLow, renderVocalLowCut(buffer([levelOnly]))[0])
  assert.deepEqual(source, sine(40))
})

test('accepted saturation follows the plan and does not commute with level control', () => {
  const steps = acceptedVocalChainSteps(['Saturation', 'TransparentDynamics'], [level, saturation], asset)
  assert.deepEqual(steps.map(step => step.role), ['Saturation', 'TransparentDynamics'])
  assert.equal(describeAcceptedVocalChain(steps), 'Saturation → Level control')
  const source = sine(220, 0.9)
  const colorThenLevel = renderAcceptedVocalChain(buffer([source]), [{ role: 'Saturation' }, { role: 'TransparentDynamics' }])[0]
  const levelThenColor = renderAcceptedVocalChain(buffer([source.slice()]), [{ role: 'TransparentDynamics' }, { role: 'Saturation' }])[0]
  assert.notDeepEqual(colorThenLevel, levelThenColor)
  assert.deepEqual(colorThenLevel, renderVocalLevelControl(buffer([renderVocalSaturation(buffer([source.slice()]))[0]]))[0])
})

test('chain preview rejects an empty plan and an unknown treatment', () => {
  const source = buffer([new Float32Array([.1, .1])])
  assert.throws(() => renderAcceptedVocalChain(source, []), /plan order/)
  assert.throws(() => renderAcceptedVocalChain(source, [{ role: 'Space' }]), /cleanup/)
  assert.deepEqual(acceptedVocalChainSteps(['CorrectiveTone', 'CorrectiveTone'], [lowCut], asset).map(step => step.role), ['CorrectiveTone'])
})
