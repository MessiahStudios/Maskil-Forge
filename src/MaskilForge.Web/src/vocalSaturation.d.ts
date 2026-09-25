export const vocalSaturation: {
  readonly processorId: 'maskil.vocal.saturation.v1'
  readonly role: 'Saturation'
  readonly colorAmount: 0.15
}

export function renderVocalSaturation(buffer: AudioBuffer, settings?: typeof vocalSaturation): Float32Array[]

export interface VocalSaturationComparison {
  originalUrl: string
  processedUrl: string
  settings: typeof vocalSaturation
  dispose(): void
}

export function prepareVocalSaturation(
  url: string,
  asset: { byteLength: number; sha256: string },
  signal?: AbortSignal,
  environment?: typeof globalThis,
): Promise<VocalSaturationComparison>
