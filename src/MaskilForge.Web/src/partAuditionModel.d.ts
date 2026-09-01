export function midiNumber(pitch: { letter: string; accidental: string; octave: number }): number
export function assemblePartNotes(
  parts: Array<{ sectionId: string; noteEventIds: string[] }>,
  noteEvents: Array<{ id: string; pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>,
  sectionId?: string | null,
): Array<{ id: string; pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>
export interface PartVoice {
  id: string
  pitch: { letter: string; accidental: string; octave: number }
  startTick: number
  durationTicks: number
  velocity: number
  partId: string
  partLabel: string
  instrumentProfileId: string | null
}
export interface ScheduledPartVoice {
  midi: number
  startSeconds: number
  durationSeconds: number
  velocity: number
  partId: string
  partLabel: string
  instrumentProfileId: string | null
}
export function assemblePartVoices(
  parts: Array<{ id: string; sectionId: string; label: string; noteEventIds: string[]; instrumentProfileId: string | null }>,
  noteEvents: Array<{ id: string; pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>,
  sectionId?: string | null,
): PartVoice[]
export function scheduleAssembledNotes(
  notes: Array<{ pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>,
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): Array<{ midi: number; startSeconds: number; durationSeconds: number; velocity: number }>
export function scheduleAbsoluteNotes(
  notes: Array<{ pitch: { letter: string; accidental: string; octave: number }; startTick: number; durationTicks: number; velocity: number }>,
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): Array<{ midi: number; startSeconds: number; durationSeconds: number; velocity: number }>
export function scheduleAssembledPartVoices(
  voices: PartVoice[],
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): ScheduledPartVoice[]
export function scheduleAbsolutePartVoices(
  voices: PartVoice[],
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): ScheduledPartVoice[]
export function musicalPositionFromTicks(
  absoluteTick: number,
  timing: { beatsPerBar: number; beatUnit: number; ticksPerQuarterNote: number },
): { bar: number; beat: number; tick: number }
export function formatTransportPosition(position: { bar: number; beat: number; tick: number }): string
export function tickFromSeconds(
  seconds: number,
  timing: { beatsPerMinute: number; ticksPerQuarterNote: number },
): number
export function peakPolyphony(
  notes: Array<{ midi: number; startSeconds: number; durationSeconds: number; velocity: number }>,
): number
