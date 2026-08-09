import type { SongProject } from './api'

export type CreatorStage = 'idea' | 'words' | 'shape' | 'music' | 'harmony' | 'arrangement'

export const creatorStages: Array<{ id: CreatorStage; label: string }>

export function creatorProgress(project: SongProject | null): Record<CreatorStage, boolean>

export function creatorDestination(stage: CreatorStage): {
  view: 'capture' | 'structure'
  target: string
  open: boolean
  focus: boolean
} | null
