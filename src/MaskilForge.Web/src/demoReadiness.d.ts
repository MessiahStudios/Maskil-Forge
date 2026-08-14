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
    sectionId: string
    stage: 'shape' | 'harmony' | 'arrangement'
    action: 'lyrics' | 'harmony' | 'role' | 'part' | 'sketch'
    label: string
  } | null
  sections: DemoSectionReadiness[]
}

export function demoReadiness(project: SongProject | null): DemoReadiness
