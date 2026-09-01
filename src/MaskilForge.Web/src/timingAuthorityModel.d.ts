export interface SectionPlacementTiming {
  start: { bar: number }
  durationBars: number
}

export interface PlannedSongTiming {
  sectionCount: number
  plannedBars: number
  endBarExclusive: number | null
  label: string
  structureNotice: string
  midiNotice: string
}

export function plannedSongTiming(sectionPlacements?: SectionPlacementTiming[]): PlannedSongTiming
