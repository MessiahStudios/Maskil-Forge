const naturalPitchClasses = { C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 }

export function midiNumber(pitch) {
  const accidental = pitch.accidental === 'Sharp' ? 1 : pitch.accidental === 'Flat' ? -1 : 0
  return (pitch.octave + 1) * 12 + naturalPitchClasses[pitch.letter] + accidental
}

/** Resolve musical-part note IDs into unique note events, optionally limited to one section. */
export function assemblePartNotes(parts, noteEvents, sectionId = null) {
  const noteById = new Map(noteEvents.map(note => [note.id, note]))
  const selected = new Map()
  for (const part of parts) {
    if (sectionId && part.sectionId !== sectionId) continue
    for (const noteEventId of part.noteEventIds) {
      if (selected.has(noteEventId)) continue
      const note = noteById.get(noteEventId)
      if (note) selected.set(noteEventId, note)
    }
  }
  return [...selected.values()].sort((left, right) =>
    left.startTick - right.startTick
    || midiNumber(left.pitch) - midiNumber(right.pitch)
    || left.id.localeCompare(right.id))
}

/** Convert absolute-tick notes into a one-shot schedule starting at time zero. */
export function scheduleAssembledNotes(notes, timing) {
  if (!notes.length) return []
  const secondsPerTick = (60 / timing.beatsPerMinute) / timing.ticksPerQuarterNote
  const originTick = Math.min(...notes.map(note => note.startTick))
  return [...notes]
    .sort((left, right) => left.startTick - right.startTick || midiNumber(left.pitch) - midiNumber(right.pitch))
    .map(note => ({
      midi: midiNumber(note.pitch),
      startSeconds: (note.startTick - originTick) * secondsPerTick,
      durationSeconds: Math.max(secondsPerTick, note.durationTicks * secondsPerTick),
      velocity: note.velocity,
    }))
}
