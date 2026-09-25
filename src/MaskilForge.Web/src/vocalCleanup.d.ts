export const vocalCleanup: {
  readonly processorId: 'maskil.vocal.cleanup.v1'
  readonly role: 'Cleanup'
  readonly thresholdDecibels: -40
  readonly ratio: 4
  readonly attackMilliseconds: 10
  readonly releaseMilliseconds: 200
}

export function renderVocalCleanup(buffer: AudioBuffer, settings?: typeof vocalCleanup): Float32Array[]

export interface VocalCleanupComparison {
  originalUrl: string
  processedUrl: string
  settings: typeof vocalCleanup
  dispose(): void
}

export function prepareVocalCleanup(
  url: string,
  asset: { byteLength: number; sha256: string },
  signal?: AbortSignal,
  environment?: typeof globalThis,
): Promise<VocalCleanupComparison>
