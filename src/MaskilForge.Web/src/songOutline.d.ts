import type { SectionDelivery, SectionKind, SongProject } from './api'

export interface SongOutlineItem {
  sectionId: string
  order: number
  title: string
  kind: SectionKind
  delivery: SectionDelivery
  durationBars: number
  lyricLineCount: number
  ready: boolean
  progress: string
}

export function songOutline(project: SongProject | null, readiness: {
  sections: Array<{
    sectionId: string
    hasLyrics: boolean
    hasHarmony: boolean
    hasRole: boolean
    hasPlayablePart: boolean
    ready: boolean
  }>
}): SongOutlineItem[]
