import { encodePreviewWav } from './vocalLowCut.js'

export const vocalCharacterCompression = Object.freeze({
  processorId: 'maskil.vocal.character-compression.v1',
  role: 'CharacterCompression',
  thresholdDecibels: -24,
  ratio: 4,
  attackMilliseconds: 0,
  releaseMilliseconds: 250,
})

function channelsOf(buffer) {
  const { sampleRate, length, numberOfChannels } = buffer ?? {}
  if (!Number.isInteger(sampleRate) || sampleRate < 8000 || sampleRate > 96000 ||
      !Number.isInteger(length) || length < 1 || length / sampleRate > 60.001 ||
      !Number.isInteger(numberOfChannels) || numberOfChannels < 1 || numberOfChannels > 2)
    throw new Error('Character-compression preview supports up to one minute of mono or stereo audio at 8–96 kHz.')
  const channels = Array.from({ length: numberOfChannels }, (_, i) => buffer.getChannelData(i))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length !== length))
    throw new Error('The take decoded with an invalid audio channel.')
  return channels
}

export function renderVocalCharacterCompression(buffer, settings = vocalCharacterCompression) {
  if (settings?.thresholdDecibels !== vocalCharacterCompression.thresholdDecibels || settings.ratio !== vocalCharacterCompression.ratio ||
      settings.attackMilliseconds !== vocalCharacterCompression.attackMilliseconds || settings.releaseMilliseconds !== vocalCharacterCompression.releaseMilliseconds)
    throw new Error('This preview uses the fixed character-compression starting point.')
  const source = channelsOf(buffer)
  const threshold = 10 ** (settings.thresholdDecibels / 20)
  const attack = settings.attackMilliseconds === 0 ? 0 : Math.exp(-1 / ((settings.attackMilliseconds / 1000) * buffer.sampleRate))
  const release = Math.exp(-1 / ((settings.releaseMilliseconds / 1000) * buffer.sampleRate))
  const compressed = source.map(channel => new Float32Array(channel.length))
  let envelope = 0
  let inputPeak = 0
  let outputPeak = 0
  for (let index = 0; index < buffer.length; index++) {
    let detector = 0
    for (const channel of source) {
      const sample = channel[index]
      if (!Number.isFinite(sample) || Math.abs(sample) > 1)
        throw new Error('The decoded take exceeds preview headroom or contains invalid samples. No gain correction was applied.')
      detector = Math.max(detector, Math.abs(sample))
      inputPeak = Math.max(inputPeak, detector)
    }
    const coefficient = detector > envelope ? attack : release
    envelope = detector + (envelope - detector) * coefficient
    const gain = envelope <= threshold ? 1 : (envelope / threshold) ** (1 / settings.ratio - 1)
    if (!Number.isFinite(gain) || gain <= 0 || gain > 1)
      throw new Error('This character-compression preview would exceed available headroom. No limiter was applied.')
    for (let channel = 0; channel < source.length; channel++) {
      const sample = source[channel][index] * gain
      if (!Number.isFinite(sample) || Math.abs(sample) > 1)
        throw new Error('This character-compression preview would exceed available headroom. No limiter was applied.')
      compressed[channel][index] = sample
      outputPeak = Math.max(outputPeak, Math.abs(sample))
    }
  }
  const makeup = outputPeak > 0 ? inputPeak / outputPeak : 1
  if (!Number.isFinite(makeup) || makeup < 1)
    throw new Error('This character-compression preview would exceed available headroom. No limiter was applied.')
  for (let channel = 0; channel < compressed.length; channel++) {
    for (let index = 0; index < buffer.length; index++) {
      const sample = compressed[channel][index] * makeup
      if (!Number.isFinite(sample) || Math.abs(sample) > 1.000001)
        throw new Error('This character-compression preview would exceed available headroom. No limiter was applied.')
      compressed[channel][index] = sample
    }
  }
  return compressed
}

export async function prepareVocalCharacterCompression(url, asset, signal, environment = globalThis) {
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
    const processed = renderVocalCharacterCompression(buffer)
    const original = channelsOf(buffer)
    originalUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(original, buffer.sampleRate)], { type: 'audio/wav' }))
    processedUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(processed, buffer.sampleRate)], { type: 'audio/wav' }))
    abort()
    let disposed = false
    return { originalUrl, processedUrl, settings: vocalCharacterCompression, dispose() {
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
