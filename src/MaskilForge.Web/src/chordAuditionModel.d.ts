import type { HarmonyChord } from './api'
import type { AuditionTiming } from './chordAudition'

export function previewMidiNotes(chord: HarmonyChord): number[]
export function positionInQuarterNotes(chord: HarmonyChord, timing: AuditionTiming): number
