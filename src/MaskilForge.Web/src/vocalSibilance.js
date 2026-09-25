import { encodePreviewWav } from './vocalLowCut.js'

export const vocalSibilance = Object.freeze({
  processorId: 'maskil.vocal.sibilance.v1',
  role: 'SibilanceControl',
  cutoffHertz: 6000,
  q: 0.7071067811865476,
  thresholdDecibels: -20,
  ratio: 3,
  attackMilliseconds: 1,
  releaseMilliseconds: 40,
})

function channelsOf(buffer) {
  const { sampleRate, length, numberOfChannels } = buffer ?? {}
  if (!Number.isInteger(sampleRate) || sampleRate < 8000 || sampleRate > 96000 ||
      !Number.isInteger(length) || length < 1 || length / sampleRate > 60.001 ||
      !Number.isInteger(numberOfChannels) || numberOfChannels < 1 || numberOfChannels > 2)
    throw new Error('Sibilance preview supports up to one minute of mono or stereo audio at 8–96 kHz.')
  const channels = Array.from({ length: numberOfChannels }, (_, i) => buffer.getChannelData(i))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length !== length))
    throw new Error('The take decoded with an invalid audio channel.')
  return channels
}

function matchesFixed(settings) {
  return settings?.cutoffHertz === vocalSibilance.cutoffHertz && settings.q === vocalSibilance.q &&
    settings.thresholdDecibels === vocalSibilance.thresholdDecibels && settings.ratio === vocalSibilance.ratio &&
    settings.attackMilliseconds === vocalSibilance.attackMilliseconds && settings.releaseMilliseconds === vocalSibilance.releaseMilliseconds
}

export function renderVocalSibilance(buffer, settings = vocalSibilance) {
  if (!matchesFixed(settings)) throw new Error('This preview uses the fixed sibilance starting point.')
  const source = channelsOf(buffer)
  const omega = 2 * Math.PI * settings.cutoffHertz / buffer.sampleRate
  const cosine = Math.cos(omega)
  const alpha = Math.sin(omega) / (2 * settings.q)
  const a0 = 1 + alpha
  const b0 = (1 + cosine) / (2 * a0)
  const b1 = -(1 + cosine) / a0
  const b2 = b0
  const a1 = -2 * cosine / a0
  const a2 = (1 - alpha) / a0
  const threshold = 10 ** (settings.thresholdDecibels / 20)
  const attack = Math.exp(-1 / ((settings.attackMilliseconds / 1000) * buffer.sampleRate))
  const release = Math.exp(-1 / ((settings.releaseMilliseconds / 1000) * buffer.sampleRate))
  const high = source.map(() => ({ x1: 0, x2: 0, y1: 0, y2: 0 }))
  const output = source.map(channel => new Float32Array(channel.length))
  let envelope = 0
  for (let index = 0; index < buffer.length; index++) {
    let detector = 0
    const band = new Array(source.length)
    for (let channel = 0; channel < source.length; channel++) {
      const sample = source[channel][index]
      if (!Number.isFinite(sample) || Math.abs(sample) > 1)
        throw new Error('The decoded take exceeds preview headroom or contains invalid samples. No gain correction was applied.')
      const state = high[channel]
      const filtered = b0 * sample + b1 * state.x1 + b2 * state.x2 - a1 * state.y1 - a2 * state.y2
      if (!Number.isFinite(filtered))
        throw new Error('This sibilance preview would exceed available headroom. No limiter was applied.')
      state.x2 = state.x1
      state.x1 = sample
      state.y2 = state.y1
      state.y1 = filtered
      band[channel] = filtered
      detector = Math.max(detector, Math.abs(filtered))
    }
    const coefficient = detector > envelope ? attack : release
    envelope = detector + (envelope - detector) * coefficient
    const gain = envelope <= threshold ? 1 : (envelope / threshold) ** (1 / settings.ratio - 1)
    if (!Number.isFinite(gain) || gain <= 0 || gain > 1)
      throw new Error('This sibilance preview would exceed available headroom. No limiter was applied.')
    for (let channel = 0; channel < source.length; channel++) {
      const sample = source[channel][index] * gain
      if (!Number.isFinite(sample) || Math.abs(sample) > 1)
        throw new Error('This sibilance preview would exceed available headroom. No limiter was applied.')
      output[channel][index] = sample
    }
  }
  return output
}

export async function prepareVocalSibilance(url, asset, signal, environment = globalThis) {
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
    const processed = renderVocalSibilance(buffer)
    const original = channelsOf(buffer)
    originalUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(original, buffer.sampleRate)], { type: 'audio/wav' }))
    processedUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(processed, buffer.sampleRate)], { type: 'audio/wav' }))
    abort()
    let disposed = false
    return { originalUrl, processedUrl, settings: vocalSibilance, dispose() {
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
