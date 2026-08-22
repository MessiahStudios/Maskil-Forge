import type { LoudnessFrameReport } from './api'

export const loudnessAnalyzerId: 'maskil.browser.loudness'
export const loudnessObservationKind: 'loudness.frame'
export const loudnessFrameDurationMs: 250
export const loudnessMaximumDurationMs: 60000

export interface DecodedAudioBuffer {
  sampleRate: number
  length: number
  numberOfChannels: number
  getChannelData(channel: number): Float32Array
}

export function calculateLoudnessFrames(audioBuffer: DecodedAudioBuffer): LoudnessFrameReport[]
export function analyzeSavedVocalTake(url: string, environment?: typeof globalThis): Promise<LoudnessFrameReport[]>
