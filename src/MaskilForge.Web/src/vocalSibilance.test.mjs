import assert from 'node:assert/strict'
import test from 'node:test'
import { renderVocalSibilance } from './vocalSibilance.js'

const buffer = channels => ({ sampleRate: 48000, length: channels[0].length, numberOfChannels: channels.length, getChannelData: index => channels[index] })
const tone = (frequency, amplitude = 0.5) => Float32Array.from({ length: 4800 }, (_, index) => amplitude * Math.sin(2 * Math.PI * frequency * index / 48000))
const latePeak = channel => channel.slice(2400).reduce((max, sample) => Math.max(max, Math.abs(sample)), 0)

test('sibilance control eases a bright tone and leaves a low tone', () => {
  const bright = tone(7000)
  const low = tone(200)
  const eased = renderVocalSibilance(buffer([bright, bright.slice()]))[0]
  const kept = renderVocalSibilance(buffer([low]))[0]
  assert.ok(latePeak(eased) < latePeak(bright) * 0.75)
  assert.ok(latePeak(kept) > 0.45)
})

test('sibilance preview refuses other settings and samples beyond headroom', () => {
  assert.throws(() => renderVocalSibilance(buffer([Float32Array.of(0.2)]), { ...renderSettings(), cutoffHertz: 4000 }), /fixed sibilance/)
  assert.throws(() => renderVocalSibilance(buffer([Float32Array.of(1.01)])), /headroom/)
})

function renderSettings() {
  return {
    cutoffHertz: 6000,
    q: 0.7071067811865476,
    thresholdDecibels: -20,
    ratio: 3,
    attackMilliseconds: 1,
    releaseMilliseconds: 40,
  }
}
