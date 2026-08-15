import type { DeviceLyricCaptureRecord } from './browserRecovery'

export interface DeviceLyricCaptureSummary {
  id: string
  title: string
  artist: string
  genre: DeviceLyricCaptureRecord['genre']
  savedAtUtc: string
  lyricLineCount: number
}

export const deviceLyricCaptureRecentLimit: number
export function deviceLyricCaptureSnapshot(capture: DeviceLyricCaptureRecord): string
export function summarizeDeviceLyricCapture(capture: DeviceLyricCaptureRecord): DeviceLyricCaptureSummary
export function deviceLyricCaptureNotice(count: number): string
export function filterDeviceLyricCaptures(captures: DeviceLyricCaptureSummary[], query?: string): DeviceLyricCaptureSummary[]
export function deviceLyricCaptureResultStats(captures: DeviceLyricCaptureSummary[], showAll?: boolean): {
  resultCount: number
  visibleCount: number
  hiddenCount: number
}
