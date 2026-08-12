import type { SongProject } from './api'

export interface DemoSectionReadiness {
  sectionId: string
  title: string
  hasLyrics: boolean
  hasHarmony: boolean
  hasRole: boolean
  hasPlayablePart: boolean
  ready: boolean
}

export interface DemoReadiness {
  readySectionCount: number
  sectionCount: number
  complete: boolean
  nextAction: string
  sections: DemoSectionReadiness[]
}

export function demoReadiness(project: SongProject | null): DemoReadiness
