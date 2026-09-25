export interface VocalChainStep { role: 'CorrectiveTone' | 'TransparentDynamics' | 'Saturation' | 'CharacterCompression' | 'Cleanup'; cutoffHertz?: number; q?: number }
export interface VocalChainAsset { id: string; sha256: string; byteLength?: number }
export interface VocalChainRecipe { assetId: string; role: string; sourceSha256: string; cutoffHertz?: number; q?: number }

export function acceptedVocalChainSteps(chainRoles: readonly string[] | undefined, recipes: readonly VocalChainRecipe[] | undefined, asset: VocalChainAsset): readonly VocalChainStep[]
export function describeAcceptedVocalChain(steps: readonly VocalChainStep[] | undefined): string
export function renderAcceptedVocalChain(buffer: AudioBuffer, steps: readonly VocalChainStep[]): Float32Array[]

export interface VocalChainComparison {
  originalUrl: string
  processedUrl: string
  steps: readonly VocalChainStep[]
  dispose(): void
}

export function prepareAcceptedVocalChain(
  url: string,
  asset: VocalChainAsset & { byteLength: number },
  steps: readonly VocalChainStep[],
  signal?: AbortSignal,
  environment?: typeof globalThis,
): Promise<VocalChainComparison>
