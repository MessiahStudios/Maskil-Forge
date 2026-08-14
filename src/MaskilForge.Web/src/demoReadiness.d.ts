import type { SongProject } from './api'

export interface DemoSectionReadiness {
  sectionId: string
  title: string
  hasLyrics: boolean
  hasHarmony: boolean
  hasRole: boolean
  hasPlayablePart: boolean
  needsSourceNotes: boolean
  ready: boolean
}

export interface DemoReadiness {
  readySectionCount: number
  sectionCount: number
  complete: boolean
  nextAction: string
  nextStep: {
    sectionId: string | null
    stage: 'shape' | 'harmony' | 'arrangement'
    action: 'lyrics' | 'harmony' | 'role' | 'part' | 'sketch' | 'hear' | 'section' | 'preview' | 'resolve'
    label: string
  } | null
  sections: DemoSectionReadiness[]
}

export function demoReadiness(
  project: SongProject | null,
  preview?: {
    sections?: unknown[]
    unrecognizedHeadings?: string[]
    unrecognizedSections?: unknown[]
  } | null,
): DemoReadiness
export function firstWritableEmptyLyricLine(
  section: { lyricLines?: Array<{ id?: string; text?: string }> } | null | undefined,
  lockedLineIds?: Iterable<string>,
): { id?: string; text?: string } | null
export function hasLyricSheetHeadings(draft: string | null | undefined): boolean
