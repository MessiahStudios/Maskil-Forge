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

function secondsPerTick(timing) {
  return (60 / timing.beatsPerMinute) / timing.ticksPerQuarterNote
}

function ticksPerBeat(timing) {
  return timing.ticksPerQuarterNote * 4 / timing.beatUnit
}

/** Convert absolute-tick notes into a one-shot schedule starting at time zero. */
export function scheduleAssembledNotes(notes, timing) {
  if (!notes.length) return []
  const tickSeconds = secondsPerTick(timing)
  const originTick = Math.min(...notes.map(note => note.startTick))
  return [...notes]
    .sort((left, right) => left.startTick - right.startTick || midiNumber(left.pitch) - midiNumber(right.pitch))
    .map(note => ({
      midi: midiNumber(note.pitch),
      startSeconds: (note.startTick - originTick) * tickSeconds,
      durationSeconds: Math.max(tickSeconds, note.durationTicks * tickSeconds),
      velocity: note.velocity,
    }))
}

/** Convert absolute-tick notes into a song-timeline schedule from tick zero. */
export function scheduleAbsoluteNotes(notes, timing) {
  if (!notes.length) return []
  const tickSeconds = secondsPerTick(timing)
  return [...notes]
    .sort((left, right) => left.startTick - right.startTick || midiNumber(left.pitch) - midiNumber(right.pitch))
    .map(note => ({
      midi: midiNumber(note.pitch),
      startSeconds: note.startTick * tickSeconds,
      durationSeconds: Math.max(tickSeconds, note.durationTicks * tickSeconds),
      velocity: note.velocity,
    }))
}

export function musicalPositionFromTicks(absoluteTick, timing) {
  const beatTicks = ticksPerBeat(timing)
  const barTicks = timing.beatsPerBar * beatTicks
  const safeTick = Math.max(0, absoluteTick)
  const bar = Math.floor(safeTick / barTicks) + 1
  const inBar = safeTick % barTicks
  const beat = Math.floor(inBar / beatTicks) + 1
  const tick = Math.floor(inBar % beatTicks)
  return { bar, beat, tick }
}

export function formatTransportPosition(position) {
  return `Bar ${position.bar} · Beat ${position.beat}`
}

export function tickFromSeconds(seconds, timing) {
  return Math.max(0, seconds / secondsPerTick(timing))
}

/** Return the peak number of sounding notes; note-offs win ties with note-ons. */
export function peakPolyphony(notes) {
  const events = notes.flatMap(note => [
    { seconds: note.startSeconds, delta: 1 },
    { seconds: note.startSeconds + note.durationSeconds, delta: -1 },
  ]).sort((left, right) => left.seconds - right.seconds || left.delta - right.delta)
  let active = 0
  let peak = 0
  for (const event of events) {
    active += event.delta
    peak = Math.max(peak, active)
  }
  return peak
}
