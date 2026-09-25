export const vocalSibilance: {
  readonly processorId: 'maskil.vocal.sibilance.v1'
  readonly role: 'SibilanceControl'
  readonly cutoffHertz: 6000
  readonly q: 0.7071067811865476
  readonly thresholdDecibels: -20
  readonly ratio: 3
  readonly attackMilliseconds: 1
  readonly releaseMilliseconds: 40
}

export function renderVocalSibilance(buffer: AudioBuffer, settings?: typeof vocalSibilance): Float32Array[]

export interface VocalSibilanceComparison {
  originalUrl: string
  processedUrl: string
  settings: typeof vocalSibilance
  dispose(): void
}

export function prepareVocalSibilance(
  url: string,
  asset: { byteLength: number; sha256: string },
  signal?: AbortSignal,
  environment?: typeof globalThis,
): Promise<VocalSibilanceComparison>
