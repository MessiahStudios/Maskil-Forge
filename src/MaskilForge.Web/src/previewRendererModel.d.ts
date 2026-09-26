export type PreviewOscillatorType = 'sine' | 'square' | 'sawtooth' | 'triangle'

export interface PreviewPartial {
  multiplier: number
  gain: number
  oscillatorType?: PreviewOscillatorType
}

export interface PreviewVoice {
  instrumentProfileId: string | null
  name: string
  oscillatorType: PreviewOscillatorType
  attackSeconds: number
  releaseSeconds: number
  sustainLevel: number
  filterFrequencyHz: number
  pitchDrop: boolean
  partials?: PreviewPartial[]
}

export const builtInPreviewRendererId: 'maskil-browser-preview-v1'
export const builtInPreviewRendererName: 'Built-in instrument preview'
export function previewVoiceForInstrument(instrumentProfileId?: string | null): PreviewVoice
export function previewEnvelopeForDuration(voice: PreviewVoice, durationSeconds: number): {
  attackSeconds: number
  releaseSeconds: number
  sustainSeconds: number
}
export function previewRendererSummary(notes: Array<{ instrumentProfileId?: string | null }>): string
