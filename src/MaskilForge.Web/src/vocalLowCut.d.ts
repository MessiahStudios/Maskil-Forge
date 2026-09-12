import type { ProjectAsset } from './api'
export const vocalLowCut: Readonly<{ processorId: string; role: 'CorrectiveTone'; cutoffHertz: number; q: number }>
export interface LowCutSettings { cutoffHertz: number; q: number }
export function validateLowCutSettings(settings?: LowCutSettings): Readonly<LowCutSettings>
export interface VocalComparison { originalUrl: string; processedUrl: string; settings: Readonly<LowCutSettings>; dispose(): void }
export function renderVocalLowCut(buffer: AudioBuffer, settings?: LowCutSettings): Float32Array[]
export function encodePreviewWav(channels: Float32Array[], sampleRate: number): ArrayBuffer
export function prepareVocalLowCut(url: string, asset: ProjectAsset, signal: AbortSignal, environment?: unknown, settings?: LowCutSettings): Promise<VocalComparison>
