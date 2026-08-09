const naturalPitchClasses = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 }
const chordIntervals = {
  Major: [0, 4, 7], Minor: [0, 3, 7], Diminished: [0, 3, 6],
  Augmented: [0, 4, 8], DominantSeventh: [0, 4, 7, 10],
}

function midiNumber(pitch) {
  const accidental = pitch.accidental === 'Sharp' ? 1 : pitch.accidental === 'Flat' ? -1 : 0
  return (pitch.octave + 1) * 12 + naturalPitchClasses[pitch.letter] + accidental
}

export function previewMidiNotes(chord) {
  if (chord.voicing?.voices.length) return chord.voicing.voices.map(voice => midiNumber(voice.pitch))
  const accidental = chord.chord.accidental === 'Sharp' ? 1 : chord.chord.accidental === 'Flat' ? -1 : 0
  const root = 48 + (naturalPitchClasses[chord.chord.root] + accidental + 12) % 12
  return chordIntervals[chord.chord.quality].map(interval => root + interval)
}

export function positionInQuarterNotes(chord, timing) {
  const quartersPerBeat = 4 / timing.beatUnit
  return ((chord.start.bar - 1) * timing.beatsPerBar + chord.start.beat - 1) * quartersPerBeat
    + chord.start.tick / timing.ticksPerQuarterNote
}
