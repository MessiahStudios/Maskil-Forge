export interface CapturedRoughVocal {
  blob: Blob
  durationMs: number
  mediaType: string
}

export interface RoughVocalCaptureSession {
  mediaType: string
  stop(): Promise<CapturedRoughVocal>
  discard(): void
}

export const roughVocalMaximumDurationMs: number
export const roughVocalMaximumByteLength: number
export function preferredRoughVocalMediaType(MediaRecorderType: typeof MediaRecorder): string
export function formatRoughVocalDuration(durationMs: number): string
export function formatRoughVocalBytes(byteLength: number): string
export function beginRoughVocalCapture(environment?: typeof globalThis): Promise<RoughVocalCaptureSession>
