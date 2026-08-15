export interface VocalCaptureSupport {
  supported: boolean
  reason: string
}

export interface MicrophonePreflightResult {
  label: string
  trackCount: number
}

export function vocalCaptureSupport(environment?: typeof globalThis): VocalCaptureSupport
export function verifyMicrophoneInput(
  getUserMedia: (constraints: MediaStreamConstraints) => Promise<MediaStream>,
): Promise<MicrophonePreflightResult>
export function microphonePreflightFailure(error: unknown): string
