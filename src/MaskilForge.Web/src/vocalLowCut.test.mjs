import assert from 'node:assert/strict'
import test from 'node:test'
import { webcrypto } from 'node:crypto'
import { encodePreviewWav, prepareVocalLowCut, renderVocalLowCut, vocalLowCut } from './vocalLowCut.js'

const buffer = (channels, sampleRate = 48000) => ({ sampleRate, length: channels[0].length, numberOfChannels: channels.length, getChannelData: i => channels[i] })
const sine = (frequency, sampleRate = 48000) => Float32Array.from({ length: sampleRate }, (_, i) => .2 * Math.sin(2 * Math.PI * frequency * i / sampleRate))
const rms = data => Math.sqrt(data.slice(Math.floor(data.length / 2)).reduce((sum, value) => sum + value * value, 0) / Math.ceil(data.length / 2))

test('low-cut attenuates rumble, is -3 dB at 80 Hz, and preserves the upper passband', () => {
  for (const rate of [8000, 44100, 48000, 96000]) {
    const gain = frequency => { const input = sine(frequency, rate); return rms(renderVocalLowCut(buffer([input], rate))[0]) / rms(input) }
    assert.ok(gain(20) < .07)
    assert.ok(Math.abs(gain(80) - Math.SQRT1_2) < .002)
    assert.ok(Math.abs(gain(1000) - 1) < .002)
  }
})

test('filter preserves length, source samples, stereo independence, and silence', () => {
  const left = sine(40), right = new Float32Array(left.length), original = left.slice()
  const output = renderVocalLowCut(buffer([left, right]))
  assert.deepEqual(left, original)
  assert.equal(output[0].length, left.length)
  assert.notEqual(output[0], left)
  assert.deepEqual(output[1], right)
  assert.deepEqual(renderVocalLowCut(buffer([left]))[0], output[0])
})

test('preview rejects invalid, oversized, and headroom-exceeding audio without automatic gain changes', () => {
  assert.throws(() => renderVocalLowCut(buffer([new Float32Array([NaN])])), /invalid samples/)
  assert.throws(() => renderVocalLowCut(buffer([new Float32Array([1.1])])), /headroom/)
  assert.throws(() => renderVocalLowCut(buffer([new Float32Array(48000 * 61)])), /one minute/)
  assert.throws(() => renderVocalLowCut(buffer([sine(40), sine(40), sine(40)])), /mono or stereo/)
  assert.throws(() => renderVocalLowCut(buffer([new Float32Array(0)])), /one minute/)
  assert.throws(() => renderVocalLowCut(buffer([sine(40)], 192000)), /8–96/)
  const transition = new Float32Array(4800).fill(-.95); transition.fill(.95, 2400)
  assert.throws(() => renderVocalLowCut(buffer([transition])), /headroom/)
})

test('comparison WAVs encode exact frame counts and channel order without normalizing', () => {
  const wav = encodePreviewWav([new Float32Array([.25, -.5]), new Float32Array([0, .5])], 48000)
  const view = new DataView(wav)
  assert.equal(new TextDecoder().decode(wav.slice(0, 4)), 'RIFF')
  assert.equal(view.getUint16(22, true), 2)
  assert.equal(view.getUint32(24, true), 48000)
  assert.equal(view.getUint32(40, true), 8)
  assert.equal(view.getInt16(44, true), 8192)
  assert.equal(view.getInt16(46, true), 0)
  assert.equal(view.getInt16(48, true), -16384)
  assert.equal(view.getInt16(50, true), 16384)
})

async function fixture(overrides = {}) {
  const bytes = new TextEncoder().encode('verified original').buffer
  const digest = Buffer.from(await webcrypto.subtle.digest('SHA-256', bytes)).toString('hex')
  const stats = { closed: 0, decoded: 0, created: [], revoked: [] }
  const environment = {
    crypto: webcrypto, Blob,
    fetch: async () => ({ ok: true, arrayBuffer: async () => bytes }),
    AudioContext: class {
      async decodeAudioData() { stats.decoded++; return buffer([sine(40)]) }
      async close() { stats.closed++ }
    },
    URL: {
      createObjectURL(blob) { const url = `blob:preview-${stats.created.length}`; stats.created.push(blob); return url },
      revokeObjectURL(url) { stats.revoked.push(url) },
    },
    ...overrides,
  }
  return { environment, stats, asset: { sha256: digest, byteLength: bytes.byteLength } }
}

test('verified preview closes decoding resources and releases both temporary URLs exactly once', async () => {
  const { environment, stats, asset } = await fixture()
  const result = await prepareVocalLowCut('/take', asset, new AbortController().signal, environment)
  assert.equal(stats.closed, 1)
  assert.equal(stats.created.length, 2)
  assert.notDeepEqual(await stats.created[0].arrayBuffer(), await stats.created[1].arrayBuffer())
  result.dispose(); result.dispose()
  assert.deepEqual(stats.revoked, ['blob:preview-0', 'blob:preview-1'])
  assert.equal(vocalLowCut.role, 'CorrectiveTone')
})

test('mismatched source is rejected before decoding or rendering', async () => {
  const { environment, stats, asset } = await fixture()
  await assert.rejects(prepareVocalLowCut('/take', { ...asset, sha256: '0'.repeat(64) }, undefined, environment), /digest/)
  await assert.rejects(prepareVocalLowCut('/take', { ...asset, byteLength: 5 }, undefined, environment), /length/)
  assert.equal(stats.decoded, 0)
  assert.equal(stats.created.length, 0)
})

test('cancellation during decoding closes the context without publishing preview audio', async () => {
  const controller = new AbortController()
  let closed = false
  const { environment, stats, asset } = await fixture({ AudioContext: class {
    async decodeAudioData() { controller.abort(); return buffer([sine(40)]) }
    async close() { closed = true }
  } })
  await assert.rejects(prepareVocalLowCut('/take', asset, controller.signal, environment), /cancelled/)
  assert.equal(closed, true)
  assert.equal(stats.created.length, 0)
})

test('decode and partial URL failures clean up all allocated resources', async () => {
  let closed = 0
  const failed = await fixture({ AudioContext: class {
    async decodeAudioData() { throw new Error('decode failed') }
    async close() { closed++ }
  } })
  await assert.rejects(prepareVocalLowCut('/take', failed.asset, undefined, failed.environment), /decode failed/)
  assert.equal(closed, 1)
  const partial = await fixture()
  const create = partial.environment.URL.createObjectURL
  partial.environment.URL.createObjectURL = blob => {
    if (partial.stats.created.length) throw new Error('allocation failed')
    return create(blob)
  }
  await assert.rejects(prepareVocalLowCut('/take', partial.asset, undefined, partial.environment), /allocation failed/)
  assert.equal(partial.stats.closed, 1)
  assert.deepEqual(partial.stats.revoked, ['blob:preview-0'])
})
