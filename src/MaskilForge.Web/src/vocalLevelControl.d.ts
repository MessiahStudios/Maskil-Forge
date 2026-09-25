export const vocalLevelControl: {
  readonly processorId: 'maskil.vocal.level-control.v1'
  readonly role: 'TransparentDynamics'
  readonly thresholdDecibels: -18
  readonly ratio: 2
  readonly attackMilliseconds: 20
  readonly releaseMilliseconds: 120
}

export function renderVocalLevelControl(buffer: AudioBuffer, settings?: typeof vocalLevelControl): Float32Array[]

export interface VocalLevelComparison {
  originalUrl: string
  processedUrl: string
  settings: typeof vocalLevelControl
  dispose(): void
}

export function prepareVocalLevelControl(
  url: string,
  asset: { byteLength: number; sha256: string },
  signal?: AbortSignal,
  environment?: typeof globalThis,
): Promise<VocalLevelComparison>
