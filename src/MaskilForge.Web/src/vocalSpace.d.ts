export const vocalSpace: {
  readonly processorId: 'maskil.vocal.space.v1'
  readonly role: 'Space'
  readonly delayMilliseconds: 80
  readonly feedback: 0.3
  readonly wetAmount: 0.15
}

export function renderVocalSpace(buffer: AudioBuffer, settings?: typeof vocalSpace): Float32Array[]

export interface VocalSpaceComparison {
  originalUrl: string
  processedUrl: string
  settings: typeof vocalSpace
  dispose(): void
}

export function prepareVocalSpace(
  url: string,
  asset: { byteLength: number; sha256: string },
  signal?: AbortSignal,
  environment?: typeof globalThis,
): Promise<VocalSpaceComparison>
