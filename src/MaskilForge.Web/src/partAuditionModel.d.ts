export function midiNumber(pitch: { letter: string; accidental: string; octave: number }): number
export function assemblePartNotes(
  parts: Array<{ sectionId: string; noteEventIds: string[] }>,
  noteEvents: Array<{ id: string; pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>,
  sectionId?: string | null,
): Array<{ id: string; pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>
export function scheduleAssembledNotes(
  notes: Array<{ pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>,
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): Array<{ midi: number; startSeconds: number; durationSeconds: number; velocity: number }>
