import type { PreviewScheduledNote } from './builtInPreviewRenderer'

export interface PreviewPlaybackHandle {
  durationSeconds: number
  stop: () => void
}

export interface ExternalPreviewRenderer {
  play: (notes: PreviewScheduledNote[]) => Promise<PreviewPlaybackHandle>
}
