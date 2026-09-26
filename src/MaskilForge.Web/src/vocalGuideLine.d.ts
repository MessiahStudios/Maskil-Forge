export const vocalGuideMaximumMilliseconds: number
export function midiFromSpelling(letter: string, accidental: string, octave: number): number
export function guideLineFromNotes(
  notes: Array<{ midi: number; startTick: number; durationTicks: number }>,
  beatsPerMinute: number,
  ticksPerQuarterNote: number,
): { line: Array<{ midi: number; startMs: number; durationMs: number }>; trimmed: boolean }
export function playVocalGuideLine(
  line: Array<{ midi: number; startMs: number; durationMs: number }>,
  environment?: typeof globalThis,
): { stop(): void; done: Promise<void> }
