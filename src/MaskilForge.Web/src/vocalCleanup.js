import { encodePreviewWav } from './vocalLowCut.js'

export const vocalCleanup = Object.freeze({
  processorId: 'maskil.vocal.cleanup.v1',
  role: 'Cleanup',
  thresholdDecibels: -40,
  ratio: 4,
  attackMilliseconds: 10,
  releaseMilliseconds: 200,
})

function channelsOf(buffer) {
  const { sampleRate, length, numberOfChannels } = buffer ?? {}
  if (!Number.isInteger(sampleRate) || sampleRate < 8000 || sampleRate > 96000 ||
      !Number.isInteger(length) || length < 1 || length / sampleRate > 60.001 ||
      !Number.isInteger(numberOfChannels) || numberOfChannels < 1 || numberOfChannels > 2)
    throw new Error('Cleanup preview supports up to one minute of mono or stereo audio at 8–96 kHz.')
  const channels = Array.from({ length: numberOfChannels }, (_, i) => buffer.getChannelData(i))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length !== length))
    throw new Error('The take decoded with an invalid audio channel.')
  return channels
}

export function renderVocalCleanup(buffer, settings = vocalCleanup) {
  if (settings?.thresholdDecibels !== vocalCleanup.thresholdDecibels || settings.ratio !== vocalCleanup.ratio ||
      settings.attackMilliseconds !== vocalCleanup.attackMilliseconds || settings.releaseMilliseconds !== vocalCleanup.releaseMilliseconds)
    throw new Error('This preview uses the fixed cleanup starting point.')
  const source = channelsOf(buffer)
  const threshold = 10 ** (settings.thresholdDecibels / 20)
  const closed = 1 / settings.ratio
  const attack = Math.exp(-1 / ((settings.attackMilliseconds / 1000) * buffer.sampleRate))
  const release = Math.exp(-1 / ((settings.releaseMilliseconds / 1000) * buffer.sampleRate))
  const output = source.map(channel => new Float32Array(channel.length))
  let envelope = 0
  let gain = 1
  for (let index = 0; index < buffer.length; index++) {
    let detector = 0
    for (const channel of source) {
      const sample = channel[index]
      if (!Number.isFinite(sample) || Math.abs(sample) > 1)
        throw new Error('The decoded take exceeds preview headroom or contains invalid samples. No gain correction was applied.')
      detector = Math.max(detector, Math.abs(sample))
    }
    const envelopeCoefficient = detector > envelope ? attack : release
    envelope = detector + (envelope - detector) * envelopeCoefficient
    const target = envelope >= threshold ? 1 : closed
    const gainCoefficient = target > gain ? attack : release
    gain = target + (gain - target) * gainCoefficient
    if (!Number.isFinite(gain) || gain <= 0 || gain > 1)
      throw new Error('This cleanup preview would exceed available headroom. The quiet gaps are lowered, not silenced or boosted.')
    for (let channel = 0; channel < source.length; channel++) {
      const sample = source[channel][index] * gain
      if (!Number.isFinite(sample) || Math.abs(sample) > 1)
        throw new Error('This cleanup preview would exceed available headroom. The quiet gaps are lowered, not silenced or boosted.')
      output[channel][index] = sample
    }
  }
  return output
}

export async function prepareVocalCleanup(url, asset, signal, environment = globalThis) {
  const AudioContextType = environment.AudioContext ?? environment.webkitAudioContext
  if (!AudioContextType || !environment.crypto?.subtle) throw new Error('This browser cannot prepare verified vocal processing previews.')
  if (!Number.isInteger(asset.byteLength) || asset.byteLength < 1 || asset.byteLength > 25 * 1024 * 1024)
    throw new Error('The source take is outside the preview size limit.')
  let context, originalUrl, processedUrl
  const abort = () => { if (signal?.aborted) throw new Error('Preview cancelled.') }
  try {
    abort()
    const response = await environment.fetch(url, { cache: 'no-store', signal })
    if (!response.ok) throw new Error(`The original take could not be loaded (${response.status}).`)
    const bytes = await response.arrayBuffer()
    if (bytes.byteLength !== asset.byteLength) throw new Error('The source take length no longer matches this project.')
    const digest = Array.from(new Uint8Array(await environment.crypto.subtle.digest('SHA-256', bytes)), byte => byte.toString(16).padStart(2, '0')).join('')
    if (digest !== asset.sha256) throw new Error('The source take digest no longer matches this project.')
    abort()
    context = new AudioContextType({ sampleRate: 48000 })
    const buffer = await context.decodeAudioData(bytes)
    abort()
    const processed = renderVocalCleanup(buffer)
    const original = channelsOf(buffer)
    originalUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(original, buffer.sampleRate)], { type: 'audio/wav' }))
    processedUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(processed, buffer.sampleRate)], { type: 'audio/wav' }))
    abort()
    let disposed = false
    return { originalUrl, processedUrl, settings: vocalCleanup, dispose() {
      if (disposed) return
      disposed = true
      environment.URL.revokeObjectURL(originalUrl)
      environment.URL.revokeObjectURL(processedUrl)
    } }
  } catch (error) {
    if (originalUrl) environment.URL.revokeObjectURL(originalUrl)
    if (processedUrl) environment.URL.revokeObjectURL(processedUrl)
    throw error
  } finally { if (context) await context.close().catch(() => undefined) }
}
