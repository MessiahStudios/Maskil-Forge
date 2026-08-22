import type { PitchFrameReport } from './api'
import type { DecodedAudioBuffer } from './loudnessAnalysis.js'

export const pitchAnalyzerId: 'maskil.browser.pitch-acf'
export const pitchObservationKind: 'pitch.frame'
export const pitchWindowDurationMs: 80
export const pitchHopDurationMs: 200
export const pitchMinimumHertz: 65
export const pitchMaximumHertz: 1000
export const pitchMinimumConfidence: 0.72
export const pitchMaximumDurationMs: 60000

export function calculatePitchFrames(audioBuffer: DecodedAudioBuffer): PitchFrameReport[]
export function analyzeSavedVocalTakePitch(url: string, environment?: typeof globalThis): Promise<PitchFrameReport[]>
