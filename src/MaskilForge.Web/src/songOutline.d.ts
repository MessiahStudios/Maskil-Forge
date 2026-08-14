import type { SectionDelivery, SectionKind, SongProject, StructuralFunction } from './api'

export interface SongOutlineItem {
  sectionId: string
  order: number
  title: string
  kind: SectionKind
  delivery: SectionDelivery
  structuralFunction: StructuralFunction
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
    needsSourceNotes?: boolean
    ready: boolean
  }>
}): SongOutlineItem[]
export function adjacentSectionId(
  sections: Array<{ id: string }>,
  currentSectionId: string,
  offset: -1 | 1,
): string | null
export function structuralRoleReview(project: SongProject | null): {
  sectionCount: number
  decidedCount: number
  complete: boolean
  nextSectionId: string | null
  nextSectionTitle: string | null
}
