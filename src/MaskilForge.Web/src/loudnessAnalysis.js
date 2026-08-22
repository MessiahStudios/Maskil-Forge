export const loudnessAnalyzerId = 'maskil.browser.loudness'
export const loudnessObservationKind = 'loudness.frame'
export const loudnessFrameDurationMs = 250
export const loudnessMaximumDurationMs = 60_000

function decibelsFullScale(amplitude) {
  if (!Number.isFinite(amplitude) || amplitude <= 0.000001) return -120
  const value = 20 * Math.log10(Math.min(1, amplitude))
  return Math.round(Math.max(-120, Math.min(0, value)) * 1000) / 1000
}

export function calculateLoudnessFrames(audioBuffer) {
  const sampleRate = audioBuffer?.sampleRate
  const sampleLength = audioBuffer?.length
  const channelCount = audioBuffer?.numberOfChannels
  if (!Number.isFinite(sampleRate) || sampleRate <= 0 || !Number.isInteger(sampleLength) || sampleLength <= 0 || !Number.isInteger(channelCount) || channelCount <= 0)
    throw new Error('The saved take did not decode into measurable audio.')
  if ((sampleLength / sampleRate) * 1000 > loudnessMaximumDurationMs + 1)
    throw new Error('Loudness analysis is limited to the one-minute rough-take boundary.')

  const channels = Array.from({ length: channelCount }, (_, index) => audioBuffer.getChannelData(index))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length < sampleLength))
    throw new Error('The saved take decoded with an invalid audio channel.')

  const frameSampleCount = Math.max(1, Math.round(sampleRate * loudnessFrameDurationMs / 1000))
  const frames = []
  for (let offset = 0; offset < sampleLength; offset += frameSampleCount) {
    const end = Math.min(sampleLength, offset + frameSampleCount)
    let peak = 0
    let sumSquares = 0
    for (const channel of channels) {
      for (let index = offset; index < end; index++) {
        const amplitude = Math.abs(channel[index])
        peak = Math.max(peak, amplitude)
        sumSquares += amplitude * amplitude
      }
    }
    const rms = Math.sqrt(sumSquares / ((end - offset) * channelCount))
    const startMilliseconds = Math.round(offset * 1000 / sampleRate)
    const endMilliseconds = Math.round(end * 1000 / sampleRate)
    frames.push({
      startMilliseconds,
      durationMilliseconds: Math.max(1, endMilliseconds - startMilliseconds),
      rmsDecibels: decibelsFullScale(rms),
      peakDecibels: decibelsFullScale(peak),
    })
  }
  return frames
}

export async function analyzeSavedVocalTake(url, environment = globalThis) {
  const AudioContextType = environment.AudioContext ?? environment.webkitAudioContext
  if (typeof AudioContextType !== 'function')
    throw new Error('This browser cannot decode a saved take for loudness analysis.')

  const context = new AudioContextType()
  try {
    const response = await environment.fetch(url, { cache: 'no-store' })
    if (!response.ok) throw new Error(`The saved take could not be loaded for analysis (${response.status}).`)
    const audioBuffer = await context.decodeAudioData(await response.arrayBuffer())
    return calculateLoudnessFrames(audioBuffer)
  } catch (error) {
    if (error instanceof Error && /saved take|Loudness analysis|decode into measurable|invalid audio channel/.test(error.message)) throw error
    throw new Error('This browser could not decode the saved take. Play it once to confirm the recording format is supported here.')
  } finally {
    await context.close().catch(() => undefined)
  }
}
