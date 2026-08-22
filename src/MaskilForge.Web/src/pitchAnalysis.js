export const pitchAnalyzerId = 'maskil.browser.pitch-acf'
export const pitchObservationKind = 'pitch.frame'
export const pitchWindowDurationMs = 80
export const pitchHopDurationMs = 200
export const pitchMinimumHertz = 65
export const pitchMaximumHertz = 1000
export const pitchMinimumConfidence = 0.72
export const pitchMaximumDurationMs = 60_000

function analysisSamples(audioBuffer) {
  const sampleRate = audioBuffer?.sampleRate
  const sampleLength = audioBuffer?.length
  const channelCount = audioBuffer?.numberOfChannels
  if (!Number.isFinite(sampleRate) || sampleRate <= 0 || !Number.isInteger(sampleLength) || sampleLength <= 0 || !Number.isInteger(channelCount) || channelCount <= 0)
    throw new Error('The saved take did not decode into measurable audio.')
  if (sampleRate < pitchMaximumHertz * 2)
    throw new Error('The saved take sample rate is too low for bounded pitch analysis.')
  if ((sampleLength / sampleRate) * 1000 > pitchMaximumDurationMs + 1)
    throw new Error('Pitch analysis is limited to the one-minute rough-take boundary.')

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

function framePitch(samples, sampleRate) {
  let mean = 0
  for (const sample of samples) mean += sample
  mean /= samples.length

  const centered = new Float32Array(samples.length)
  let sumSquares = 0
  for (let index = 0; index < samples.length; index++) {
    centered[index] = samples[index] - mean
    sumSquares += centered[index] * centered[index]
  }
  if (Math.sqrt(sumSquares / centered.length) < 0.005) return null

  const minimumLag = Math.max(2, Math.floor(sampleRate / pitchMaximumHertz))
  const maximumLag = Math.min(centered.length - 2, Math.ceil(sampleRate / pitchMinimumHertz))
  const correlations = new Float64Array(maximumLag + 2)
  let strongestCorrelation = -1
  let strongestLag = minimumLag
  for (let lag = minimumLag; lag <= maximumLag; lag++) {
    let numerator = 0
    let leftEnergy = 0
    let rightEnergy = 0
    for (let index = 0; index < centered.length - lag; index++) {
      const left = centered[index]
      const right = centered[index + lag]
      numerator += left * right
      leftEnergy += left * left
      rightEnergy += right * right
    }
    const correlation = leftEnergy > 0 && rightEnergy > 0 ? numerator / Math.sqrt(leftEnergy * rightEnergy) : 0
    correlations[lag] = correlation
    if (correlation > strongestCorrelation) {
      strongestCorrelation = correlation
      strongestLag = lag
    }
  }
  if (strongestCorrelation < pitchMinimumConfidence) return null

  const preferredThreshold = Math.max(pitchMinimumConfidence, strongestCorrelation * 0.98)
  let selectedLag = strongestLag
  if (correlations[minimumLag] >= preferredThreshold && correlations[minimumLag] >= correlations[minimumLag + 1]) {
    selectedLag = minimumLag
  } else {
    for (let lag = minimumLag + 1; lag < maximumLag; lag++) {
      if (correlations[lag] >= preferredThreshold
        && correlations[lag] >= correlations[lag - 1]
        && correlations[lag] >= correlations[lag + 1]) {
        selectedLag = lag
        break
      }
    }
  }

  const center = correlations[selectedLag]
  const left = correlations[selectedLag - 1]
  const right = correlations[selectedLag + 1]
  const denominator = left - (2 * center) + right
  const offset = selectedLag > minimumLag
    && selectedLag < maximumLag
    && Math.abs(denominator) > 0.000001
    ? 0.5 * (left - right) / denominator
    : 0
  const refinedLag = selectedLag + Math.max(-0.5, Math.min(0.5, offset))
  const frequencyHertz = sampleRate / refinedLag
  if (frequencyHertz < pitchMinimumHertz || frequencyHertz > pitchMaximumHertz) return null
  return {
    frequencyHertz: Math.round(frequencyHertz * 100) / 100,
    confidence: Math.round(Math.max(0, Math.min(1, center)) * 1000) / 1000,
  }
}

export function calculatePitchFrames(audioBuffer) {
  const analysis = analysisSamples(audioBuffer)
  const windowSamples = Math.max(1, Math.round(analysis.sampleRate * pitchWindowDurationMs / 1000))
  const hopSamples = Math.max(1, Math.round(analysis.sampleRate * pitchHopDurationMs / 1000))
  const frames = []
  for (let offset = 0; offset + windowSamples <= analysis.samples.length; offset += hopSamples) {
    const detected = framePitch(analysis.samples.subarray(offset, offset + windowSamples), analysis.sampleRate)
    if (!detected) continue
    frames.push({
      startMilliseconds: Math.round(offset * 1000 / analysis.sampleRate),
      durationMilliseconds: pitchWindowDurationMs,
      frequencyHertz: detected.frequencyHertz,
      confidence: detected.confidence,
    })
  }
  return frames
}

export async function analyzeSavedVocalTakePitch(url, environment = globalThis) {
  const AudioContextType = environment.AudioContext ?? environment.webkitAudioContext
  if (typeof AudioContextType !== 'function')
    throw new Error('This browser cannot decode a saved take for pitch analysis.')

  const context = new AudioContextType()
  try {
    const response = await environment.fetch(url, { cache: 'no-store' })
    if (!response.ok) throw new Error(`The saved take could not be loaded for analysis (${response.status}).`)
    const audioBuffer = await context.decodeAudioData(await response.arrayBuffer())
    return calculatePitchFrames(audioBuffer)
  } catch (error) {
    if (error instanceof Error && /saved take|Pitch analysis|decode into measurable|invalid audio channel/.test(error.message)) throw error
    throw new Error('This browser could not decode the saved take. Play it once to confirm the recording format is supported here.')
  } finally {
    await context.close().catch(() => undefined)
  }
}
