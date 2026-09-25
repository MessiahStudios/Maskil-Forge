export const vocalCharacterCompression: {
  readonly processorId: 'maskil.vocal.character-compression.v1'
  readonly role: 'CharacterCompression'
  readonly thresholdDecibels: -24
  readonly ratio: 4
  readonly attackMilliseconds: 0
  readonly releaseMilliseconds: 250
}

export function renderVocalCharacterCompression(buffer: AudioBuffer, settings?: typeof vocalCharacterCompression): Float32Array[]

export interface VocalCharacterComparison {
  originalUrl: string
  processedUrl: string
  settings: typeof vocalCharacterCompression
  dispose(): void
}

export function prepareVocalCharacterCompression(
  url: string,
  asset: { byteLength: number; sha256: string },
  signal?: AbortSignal,
  environment?: typeof globalThis,
): Promise<VocalCharacterComparison>
