const naturalPitchClasses = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 }
const qualityIntervals = {
  Major: [0, 4, 7],
  Minor: [0, 3, 7],
  Diminished: [0, 3, 6],
  Augmented: [0, 4, 8],
  DominantSeventh: [0, 4, 7, 10],
}
const pitchClassNames = ['C', 'C♯', 'D', 'D♯', 'E', 'F', 'F♯', 'G', 'G♯', 'A', 'A♯', 'B']

function pitchClass(pitch) {
  const accidental = pitch.accidental === 'Sharp' ? 1 : pitch.accidental === 'Flat' ? -1 : 0
  return (naturalPitchClasses[pitch.letter] + accidental + 12) % 12
}

function midiNumber(pitch) {
  return (pitch.octave + 1) * 12 + pitchClass(pitch)
}

export function chordPitchClasses(chord) {
  const root = pitchClass({ letter: chord.root, accidental: chord.accidental })
  return qualityIntervals[chord.quality].map(interval => (root + interval) % 12)
}

export function chordToneNames(chord) {
  return chordPitchClasses(chord).map(value => pitchClassNames[value])
}

export function voicingIssues(chord, pitches, minimumMidiNote = 21, maximumMidiNote = 108) {
  if (!pitches.length) return []
  const allowed = new Set(chordPitchClasses(chord))
  const numbers = pitches.map(midiNumber)
  const issues = []
  if (pitches.some(pitch => !allowed.has(pitchClass(pitch)))) issues.push('Use only tones from the owning chord.')
  if (numbers.some(number => number < minimumMidiNote || number > maximumMidiNote)) issues.push('Keep every voice between A0 and C8.')
  if (numbers.some((number, index) => index > 0 && number < numbers[index - 1])) issues.push('Enter voices from low to high.')
  return issues
}
