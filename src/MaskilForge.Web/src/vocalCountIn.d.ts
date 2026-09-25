export function vocalCountInOffsets(beatsPerMinute: number, beatsPerBar: number): number[]
export function vocalCountInDurationMs(beatsPerMinute: number, beatsPerBar: number): number
export function playVocalCountIn(options: {
  beatsPerMinute: number
  beatsPerBar: number
  signal?: AbortSignal
  environment?: typeof globalThis
}): Promise<void>
