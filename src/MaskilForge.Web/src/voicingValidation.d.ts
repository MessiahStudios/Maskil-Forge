import type { ChordSymbol, RegisteredPitch } from './api'

export function chordPitchClasses(chord: ChordSymbol): number[]
export function chordToneNames(chord: ChordSymbol): string[]
export function voicingIssues(chord: ChordSymbol, pitches: RegisteredPitch[], minimumMidiNote?: number, maximumMidiNote?: number): string[]
