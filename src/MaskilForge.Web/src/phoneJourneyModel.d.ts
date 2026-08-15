import type { SongProject } from './api'

export type PhoneCreatorStage = 'idea' | 'words' | 'shape' | 'review' | 'approve'
export type DesktopProductionStage = 'music' | 'harmony' | 'arrangement'
export type CreatorJourneyStage = PhoneCreatorStage | DesktopProductionStage

export const phoneLayoutMaxWidth: 620

export function phoneEditorChrome(): {
  keepSaveInBar: boolean
  keepUndoRedoInBar: boolean
  showJourneyIntro: boolean
  showJourneyProgress: boolean
  showMusicSettings: boolean
  showDeveloperDetails: boolean
  compactHostStatus: boolean
  showSectionTiming: boolean
  showSectionPerformance: boolean
  lyricsBeforeRole: boolean
  collapseSectionRole: boolean
  showRoleReview: boolean
  showLyricLocks: boolean
  compactShapeChrome: boolean
  showReadyHostStatus: boolean
}

export const phoneCreatorStages: Array<{ id: PhoneCreatorStage; label: string }>

export function remapDesktopStageForPhone(stage: CreatorJourneyStage): PhoneCreatorStage
export function remapPhoneStageForDesktop(stage: CreatorJourneyStage): Exclude<CreatorJourneyStage, 'review' | 'approve'>

export function phoneJourneyProgress(project: SongProject | null): Record<PhoneCreatorStage, boolean>

export function phoneDestination(stage: CreatorJourneyStage, hasSections?: boolean): {
  view: 'capture' | 'structure'
  target: string
  open: boolean
  focus: boolean
  stage?: PhoneCreatorStage
  message?: string
} | null

export function phoneCaptureReadiness(
  project: SongProject | null,
  preview?: {
    sections?: unknown[]
    unrecognizedHeadings?: string[]
    unrecognizedSections?: unknown[]
  } | null,
  options?: {
    isDirty?: boolean
    activeStage?: CreatorJourneyStage
    lockedLineIds?: Iterable<string>
  },
): {
  complete: boolean
  nextAction: string
  nextStep: {
    sectionId: string | null
    stage: PhoneCreatorStage
    action: 'words' | 'section' | 'preview' | 'resolve' | 'lyrics' | 'review' | 'approve'
    label: string
  } | null
  sections: Array<{
    sectionId: string
    title: string
    hasLyrics: boolean
    ready: boolean
  }>
}

export function phoneShowsSongOutline(sectionCount: number): boolean
