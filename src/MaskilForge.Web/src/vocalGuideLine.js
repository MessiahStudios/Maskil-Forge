const letters = Object.freeze({ C: 0, D: 2, E: 4, F: 5, G: 7, A: 9, B: 11 })
const accidentals = Object.freeze({ Flat: -1, Natural: 0, Sharp: 1 })
export const vocalGuideMaximumMilliseconds = 60_000

export function midiFromSpelling(letter, accidental, octave) {
  const pitchClass = letters[letter]
  const shift = accidentals[accidental]
  if (pitchClass === undefined || shift === undefined || !Number.isInteger(octave))
    throw new Error('The written note could not be played.')
  const midi = (octave + 1) * 12 + pitchClass + shift
  if (midi < 0 || midi > 127) throw new Error('The written note could not be played.')
  return midi
}

export function guideLineFromNotes(notes, beatsPerMinute, ticksPerQuarterNote) {
  const tempo = Number(beatsPerMinute)
  if (!Number.isFinite(tempo) || tempo < 20 || tempo > 300 || !Number.isInteger(ticksPerQuarterNote) || ticksPerQuarterNote < 1)
    throw new Error('The guide uses the song tempo.')
  const events = []
  for (const note of notes ?? []) {
    if (!Number.isInteger(note?.midi) || note.midi < 0 || note.midi > 127 || !Number.isInteger(note.startTick) || note.startTick < 0 || !Number.isInteger(note.durationTicks) || note.durationTicks < 1)
      throw new Error('The written notes could not be played.')
    events.push({ tick: note.startTick, add: note.midi })
    events.push({ tick: note.startTick + note.durationTicks, remove: note.midi })
  }
  events.sort((left, right) => left.tick - right.tick || (left.remove !== undefined ? -1 : 1))
  const counts = new Map()
  const segments = []
  let current = null
  let cursor = 0
  const highest = () => {
    let best = null
    for (const [midi, count] of counts) if (count > 0 && (best === null || midi > best)) best = midi
    return best
  }
  for (let index = 0; index < events.length;) {
    const tick = events[index].tick
    if (current !== null && tick > cursor) segments.push({ midi: current, startTick: cursor, durationTicks: tick - cursor })
    while (index < events.length && events[index].tick === tick) {
      const event = events[index++]
      const midi = event.add ?? event.remove
      counts.set(midi, (counts.get(midi) ?? 0) + (event.add !== undefined ? 1 : -1))
    }
    current = highest()
    cursor = tick
  }
  let trimmed = false
  const line = []
  for (const segment of segments) {
    const startMs = Math.round(segment.startTick * 60_000 / (tempo * ticksPerQuarterNote))
    const endMs = Math.round((segment.startTick + segment.durationTicks) * 60_000 / (tempo * ticksPerQuarterNote))
    if (startMs >= vocalGuideMaximumMilliseconds) { trimmed = true; break }
    const durationMs = Math.min(endMs, vocalGuideMaximumMilliseconds) - startMs
    if (endMs > vocalGuideMaximumMilliseconds) trimmed = true
    if (durationMs > 0) line.push({ midi: segment.midi, startMs, durationMs })
  }
  return { line, trimmed }
}

export function playVocalGuideLine(line, environment = globalThis) {
  if (!line?.length) throw new Error('Write the notes you want to sing. This guide does not invent a melody.')
  const AudioContextType = environment.AudioContext ?? environment.webkitAudioContext
  if (!AudioContextType) throw new Error('This browser cannot play a rehearsal line.')
  const context = new AudioContextType()
  const start = context.currentTime + 0.05
  for (const segment of line) {
    const oscillator = context.createOscillator()
    const gain = context.createGain()
    oscillator.type = 'sine'
    oscillator.frequency.value = 440 * 2 ** ((segment.midi - 69) / 12)
    oscillator.connect(gain)
    gain.connect(context.destination)
    const at = start + segment.startMs / 1000
    const end = at + segment.durationMs / 1000
    gain.gain.setValueAtTime(0.0001, at)
    if (end - at > 0.04) {
      gain.gain.exponentialRampToValueAtTime(0.12, at + 0.02)
      gain.gain.exponentialRampToValueAtTime(0.0001, end)
    } else gain.gain.setValueAtTime(0.0001, end)
    oscillator.start(at)
    oscillator.stop(end + 0.02)
  }
  const totalMs = line[line.length - 1].startMs + line[line.length - 1].durationMs
  const setTimer = environment.setTimeout?.bind(environment) ?? setTimeout
  const clearTimer = environment.clearTimeout?.bind(environment) ?? clearTimeout
  let timer
  let settle
  const done = new Promise(resolve => { settle = resolve })
  const finish = () => {
    clearTimer(timer)
    context.close?.().catch?.(() => undefined)
    settle()
  }
  timer = setTimer(finish, totalMs + 80)
  return { stop: finish, done }
}
