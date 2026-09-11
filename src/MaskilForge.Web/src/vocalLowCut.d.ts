import type { ProjectAsset } from './api'
export const vocalLowCut: Readonly<{ processorId: string; role: 'CorrectiveTone'; cutoffHertz: number; q: number }>
export interface VocalComparison { originalUrl: string; processedUrl: string; dispose(): void }
export function renderVocalLowCut(buffer: AudioBuffer): Float32Array[]
export function encodePreviewWav(channels: Float32Array[], sampleRate: number): ArrayBuffer
export function prepareVocalLowCut(url: string, asset: ProjectAsset, signal: AbortSignal, environment?: unknown): Promise<VocalComparison>
