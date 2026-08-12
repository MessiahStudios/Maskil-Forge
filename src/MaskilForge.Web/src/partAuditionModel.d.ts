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
export function scheduleAbsoluteNotes(
  notes: Array<{ pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>,
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): Array<{ midi: number; startSeconds: number; durationSeconds: number; velocity: number }>
export function musicalPositionFromTicks(
  absoluteTick: number,
  timing: { beatsPerBar: number; beatUnit: number; ticksPerQuarterNote: number },
): { bar: number; beat: number; tick: number }
export function formatTransportPosition(position: { bar: number; beat: number; tick: number }): string
export function tickFromSeconds(
  seconds: number,
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): number
