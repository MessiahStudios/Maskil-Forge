export const onsetAnalyzerId = 'maskil.browser.onset-energy'
export const onsetObservationKind = 'onset.event'
export const onsetWindowDurationMs = 32
export const onsetHopDurationMs = 16
export const onsetMinimumSeparationMs = 96
export const onsetMinimumConfidence = 0.6
export const onsetMaximumDurationMs = 60_000

const minimumRms = 0.015
const minimumRise = 0.012
const minimumRatio = 1.5

function clamp(value, minimum, maximum) {
  return Math.max(minimum, Math.min(maximum, value))
}

function analysisSamples(audioBuffer) {
  const sampleRate = audioBuffer?.sampleRate
  const sampleLength = audioBuffer?.length
  const channelCount = audioBuffer?.numberOfChannels
  if (!Number.isFinite(sampleRate) || sampleRate <= 0 || !Number.isInteger(sampleLength) || sampleLength <= 0 || !Number.isInteger(channelCount) || channelCount <= 0)
    throw new Error('The saved take did not decode into measurable audio.')
  if (sampleRate < 1000)
    throw new Error('The saved take sample rate is too low for bounded onset analysis.')
  if ((sampleLength / sampleRate) * 1000 > onsetMaximumDurationMs + 1)
    throw new Error('Onset analysis is limited to the one-minute rough-take boundary.')

  const channels = Array.from({ length: channelCount }, (_, index) => audioBuffer.getChannelData(index))
  if (channels.some(channel => !(channel instanceof Float32Array) || channel.length < sampleLength))
    throw new Error('The saved take decoded with an invalid audio channel.')

  const analysisRate = Math.min(sampleRate, 8000)
  const sourceSamplesPerAnalysisSample = sampleRate / analysisRate
  const outputLength = Math.floor(sampleLength / sourceSamplesPerAnalysisSample)
  const samples = new Float32Array(outputLength)
  for (let outputIndex = 0; outputIndex < outputLength; outputIndex++) {
    const sourceStart = Math.floor(outputIndex * sourceSamplesPerAnalysisSample)
    const sourceEnd = Math.max(sourceStart + 1, Math.min(sampleLength, Math.floor((outputIndex + 1) * sourceSamplesPerAnalysisSample)))
    let sum = 0
    for (const channel of channels)
      for (let sourceIndex = sourceStart; sourceIndex < sourceEnd; sourceIndex++) sum += channel[sourceIndex]
    samples[outputIndex] = sum / ((sourceEnd - sourceStart) * channelCount)
  }
  return { samples, sampleRate: analysisRate }
}

function rootMeanSquare(samples) {
  let sumSquares = 0
  for (const sample of samples) sumSquares += sample * sample
  return Math.sqrt(sumSquares / samples.length)
}

export function calculateOnsetEvents(audioBuffer) {
  const analysis = analysisSamples(audioBuffer)
  const windowSamples = Math.max(1, Math.round(analysis.sampleRate * onsetWindowDurationMs / 1000))
  const hopSamples = Math.max(1, Math.round(analysis.sampleRate * onsetHopDurationMs / 1000))
  if (analysis.samples.length < windowSamples) return []

  const frameCount = Math.floor((analysis.samples.length - windowSamples) / hopSamples) + 1
  const levels = new Float64Array(frameCount)
  const rises = new Float64Array(frameCount)
  const ratios = new Float64Array(frameCount)
  for (let frameIndex = 0; frameIndex < frameCount; frameIndex++) {
    const offset = frameIndex * hopSamples
    levels[frameIndex] = rootMeanSquare(analysis.samples.subarray(offset, offset + windowSamples))
    const previous = frameIndex ? levels[frameIndex - 1] : 0
    rises[frameIndex] = Math.max(0, levels[frameIndex] - previous)
    ratios[frameIndex] = levels[frameIndex] / Math.max(previous, 0.005)
  }

  const events = []
  for (let frameIndex = 0; frameIndex < frameCount; frameIndex++) {
    const rise = rises[frameIndex]
    const nextRise = frameIndex + 1 < frameCount ? rises[frameIndex + 1] : 0
    if (levels[frameIndex] < minimumRms
      || rise < minimumRise
      || ratios[frameIndex] < minimumRatio
      || rise < nextRise)
      continue

    const startMilliseconds = frameIndex * onsetHopDurationMs
    if (events.length && startMilliseconds - events.at(-1).startMilliseconds < onsetMinimumSeparationMs)
      continue

    const riseScore = clamp((rise - minimumRise) / 0.15, 0, 1)
    const ratioScore = clamp((ratios[frameIndex] - minimumRatio) / 6, 0, 1)
    events.push({
      startMilliseconds,
      durationMilliseconds: onsetWindowDurationMs,
      strength: Math.round(clamp(rise / 0.35, 0, 1) * 1000) / 1000,
      confidence: Math.round((onsetMinimumConfidence + (0.25 * riseScore) + (0.15 * ratioScore)) * 1000) / 1000,
    })
  }
  return events
}

export async function analyzeSavedVocalTakeOnsets(url, environment = globalThis) {
  const AudioContextType = environment.AudioContext ?? environment.webkitAudioContext
  if (typeof AudioContextType !== 'function')
    throw new Error('This browser cannot decode a saved take for onset analysis.')

  const context = new AudioContextType()
  try {
    const response = await environment.fetch(url, { cache: 'no-store' })
    if (!response.ok) throw new Error(`The saved take could not be loaded for analysis (${response.status}).`)
    const audioBuffer = await context.decodeAudioData(await response.arrayBuffer())
    return calculateOnsetEvents(audioBuffer)
  } catch (error) {
    if (error instanceof Error && /saved take|Onset analysis|decode into measurable|invalid audio channel/.test(error.message)) throw error
    throw new Error('This browser could not decode the saved take. Play it once to confirm the recording format is supported here.')
  } finally {
    await context.close().catch(() => undefined)
  }
}
