export const vocalLowCut = Object.freeze({ processorId: 'maskil.vocal.low-cut.v1', role: 'CorrectiveTone', cutoffHertz: 80, q: 0.7071067811865476 })

function channelsOf(buffer) {
  const { sampleRate, length, numberOfChannels } = buffer ?? {}
  if (!Number.isInteger(sampleRate) || sampleRate < 8000 || sampleRate > 96000 ||
      !Number.isInteger(length) || length < 1 || length / sampleRate > 60.001 ||
      !Number.isInteger(numberOfChannels) || numberOfChannels < 1 || numberOfChannels > 2)
    throw new Error('Low-cut preview supports up to one minute of mono or stereo audio at 8–96 kHz.')
  const channels = Array.from({ length: numberOfChannels }, (_, i) => buffer.getChannelData(i))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length !== length))
    throw new Error('The take decoded with an invalid audio channel.')
  return channels
}

export function renderVocalLowCut(buffer) {
  const source = channelsOf(buffer)
  // RBJ high-pass biquad, normalized by a0; W3C Audio EQ Cookbook:
  // https://www.w3.org/TR/audio-eq-cookbook/#:~:text=HPF
  const omega = 2 * Math.PI * vocalLowCut.cutoffHertz / buffer.sampleRate
  const cosine = Math.cos(omega)
  const alpha = Math.sin(omega) / (2 * vocalLowCut.q)
  const a0 = 1 + alpha
  const b0 = (1 + cosine) / (2 * a0)
  const b1 = -(1 + cosine) / a0
  const b2 = b0
  const a1 = -2 * cosine / a0
  const a2 = (1 - alpha) / a0
  return source.map(channel => {
    const output = new Float32Array(channel.length)
    let x1 = 0, x2 = 0, y1 = 0, y2 = 0
    for (let i = 0; i < channel.length; i++) {
      const x = channel[i]
      if (!Number.isFinite(x) || Math.abs(x) > 1)
        throw new Error('The decoded take exceeds preview headroom or contains invalid samples. No gain correction was applied.')
      const y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
      if (!Number.isFinite(y) || Math.abs(y) > 1)
        throw new Error('This low-cut preview would exceed available headroom. No limiting or gain correction was applied.')
      output[i] = y
      x2 = x1; x1 = x; y2 = y1; y1 = y
    }
    return output
  })
}

export function encodePreviewWav(channels, sampleRate) {
  const buffer = { sampleRate, length: channels[0]?.length, numberOfChannels: channels.length, getChannelData: i => channels[i] }
  channelsOf(buffer)
  const frameSize = channels.length * 2
  const bytes = new ArrayBuffer(44 + buffer.length * frameSize)
  const view = new DataView(bytes)
  const text = (offset, value) => { for (let i = 0; i < value.length; i++) view.setUint8(offset + i, value.charCodeAt(i)) }
  text(0, 'RIFF'); view.setUint32(4, bytes.byteLength - 8, true); text(8, 'WAVE'); text(12, 'fmt ')
  view.setUint32(16, 16, true); view.setUint16(20, 1, true); view.setUint16(22, channels.length, true)
  view.setUint32(24, sampleRate, true); view.setUint32(28, sampleRate * frameSize, true)
  view.setUint16(32, frameSize, true); view.setUint16(34, 16, true); text(36, 'data'); view.setUint32(40, bytes.byteLength - 44, true)
  let offset = 44
  for (let i = 0; i < buffer.length; i++) for (const channel of channels) {
    const value = channel[i]
    if (!Number.isFinite(value) || Math.abs(value) > 1) throw new Error('Preview samples must be finite and within available headroom.')
    view.setInt16(offset, Math.round(value * (value < 0 ? 32768 : 32767)), true)
    offset += 2
  }
  return bytes
}

export async function prepareVocalLowCut(url, asset, signal, environment = globalThis) {
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
    const processed = renderVocalLowCut(buffer)
    const original = channelsOf(buffer)
    // Encode both comparisons identically; no gain matching, normalization, or replacement source asset.
    originalUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(original, buffer.sampleRate)], { type: 'audio/wav' }))
    processedUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(processed, buffer.sampleRate)], { type: 'audio/wav' }))
    abort()
    let disposed = false
    return { originalUrl, processedUrl, dispose() {
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
