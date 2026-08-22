import type { OnsetEventReport } from './api'
import type { DecodedAudioBuffer } from './loudnessAnalysis.js'

export const onsetAnalyzerId: 'maskil.browser.onset-energy'
export const onsetObservationKind: 'onset.event'
export const onsetWindowDurationMs: 32
export const onsetHopDurationMs: 16
export const onsetMinimumSeparationMs: 96
export const onsetMinimumConfidence: 0.6
export const onsetMaximumDurationMs: 60000

export function calculateOnsetEvents(audioBuffer: DecodedAudioBuffer): OnsetEventReport[]
export function analyzeSavedVocalTakeOnsets(url: string, environment?: typeof globalThis): Promise<OnsetEventReport[]>
