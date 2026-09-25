import { encodePreviewWav } from './vocalLowCut.js'

export const vocalSaturation = Object.freeze({
  processorId: 'maskil.vocal.saturation.v1',
  role: 'Saturation',
  colorAmount: 0.15,
})

function channelsOf(buffer) {
  const { sampleRate, length, numberOfChannels } = buffer ?? {}
  if (!Number.isInteger(sampleRate) || sampleRate < 8000 || sampleRate > 96000 ||
      !Number.isInteger(length) || length < 1 || length / sampleRate > 60.001 ||
      !Number.isInteger(numberOfChannels) || numberOfChannels < 1 || numberOfChannels > 2)
    throw new Error('Saturation preview supports up to one minute of mono or stereo audio at 8–96 kHz.')
  const channels = Array.from({ length: numberOfChannels }, (_, i) => buffer.getChannelData(i))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length !== length))
    throw new Error('The take decoded with an invalid audio channel.')
  return channels
}

export function renderVocalSaturation(buffer, settings = vocalSaturation) {
  if (settings?.colorAmount !== vocalSaturation.colorAmount)
    throw new Error('This preview uses the fixed saturation starting point.')
  const source = channelsOf(buffer)
  const amount = settings.colorAmount
  const output = source.map(channel => new Float32Array(channel.length))
  for (let channel = 0; channel < source.length; channel++) {
    for (let index = 0; index < buffer.length; index++) {
      const sample = source[channel][index]
      if (!Number.isFinite(sample) || Math.abs(sample) > 1)
        throw new Error('The decoded take exceeds preview headroom or contains invalid samples. No gain correction was applied.')
      const shaped = sample - amount * sample * sample * sample
      if (!Number.isFinite(shaped) || Math.abs(shaped) > 1)
        throw new Error('This saturation preview would exceed available headroom. No makeup gain or limiter was applied.')
      output[channel][index] = shaped
    }
  }
  return output
}

export async function prepareVocalSaturation(url, asset, signal, environment = globalThis) {
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
    const processed = renderVocalSaturation(buffer)
    const original = channelsOf(buffer)
    originalUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(original, buffer.sampleRate)], { type: 'audio/wav' }))
    processedUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(processed, buffer.sampleRate)], { type: 'audio/wav' }))
    abort()
    let disposed = false
    return { originalUrl, processedUrl, settings: vocalSaturation, dispose() {
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
