import { encodePreviewWav } from './vocalLowCut.js'

export const vocalSpace = Object.freeze({
  processorId: 'maskil.vocal.space.v1',
  role: 'Space',
  delayMilliseconds: 80,
  feedback: 0.3,
  wetAmount: 0.15,
})

function channelsOf(buffer) {
  const { sampleRate, length, numberOfChannels } = buffer ?? {}
  if (!Number.isInteger(sampleRate) || sampleRate < 8000 || sampleRate > 96000 ||
      !Number.isInteger(length) || length < 1 || length / sampleRate > 60.001 ||
      !Number.isInteger(numberOfChannels) || numberOfChannels < 1 || numberOfChannels > 2)
    throw new Error('Space preview supports up to one minute of mono or stereo audio at 8–96 kHz.')
  const channels = Array.from({ length: numberOfChannels }, (_, i) => buffer.getChannelData(i))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length !== length))
    throw new Error('The take decoded with an invalid audio channel.')
  return channels
}

function fitWet(dry, wet) {
  if (!Number.isFinite(dry) || Math.abs(dry) > 1)
    throw new Error('The decoded take exceeds preview headroom or contains invalid samples. No gain correction was applied.')
  if (!Number.isFinite(wet))
    throw new Error('This space preview would exceed available headroom. The dry voice was left unchanged.')
  const added = wet >= 0 ? Math.min(wet, Math.max(0, 1 - dry)) : Math.max(wet, Math.min(0, -1 - dry))
  const sample = dry + added
  if (!Number.isFinite(sample) || Math.abs(sample) > 1)
    throw new Error('This space preview would exceed available headroom. The dry voice was left unchanged.')
  return sample
}

export function renderVocalSpace(buffer, settings = vocalSpace) {
  if (settings?.delayMilliseconds !== vocalSpace.delayMilliseconds || settings.feedback !== vocalSpace.feedback || settings.wetAmount !== vocalSpace.wetAmount)
    throw new Error('This preview uses the fixed space starting point.')
  const source = channelsOf(buffer)
  const delaySamples = Math.round(settings.delayMilliseconds / 1000 * buffer.sampleRate)
  if (delaySamples < 1) throw new Error('This preview uses the fixed space starting point.')
  const lines = source.map(() => new Float32Array(delaySamples))
  const output = source.map(channel => new Float32Array(channel.length))
  for (let index = 0; index < buffer.length; index++) {
    const cursor = index % delaySamples
    for (let channel = 0; channel < source.length; channel++) {
      const dry = source[channel][index]
      const delayed = lines[channel][cursor]
      output[channel][index] = fitWet(dry, delayed * settings.wetAmount)
      lines[channel][cursor] = dry + delayed * settings.feedback
    }
  }
  return output
}

export async function prepareVocalSpace(url, asset, signal, environment = globalThis) {
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
    const processed = renderVocalSpace(buffer)
    const original = channelsOf(buffer)
    originalUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(original, buffer.sampleRate)], { type: 'audio/wav' }))
    processedUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(processed, buffer.sampleRate)], { type: 'audio/wav' }))
    abort()
    let disposed = false
    return { originalUrl, processedUrl, settings: vocalSpace, dispose() {
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
