import type { ProjectSummary } from './api'

export type ProjectLibraryStage = 'all' | 'structured' | 'raw' | 'empty'
export type ProjectLibraryItemStage = Exclude<ProjectLibraryStage, 'all'>

export interface LibraryResultStats {
  resultCount: number
  visibleCount: number
  hiddenCount: number
}

export const libraryRecentLimit: number
export function projectLibraryStage(project: ProjectSummary): ProjectLibraryItemStage
export function filterProjectLibrary(projects: ProjectSummary[], query?: string, stage?: ProjectLibraryStage): ProjectSummary[]
export function libraryResultStats(projects: ProjectSummary[], showAll?: boolean): LibraryResultStats
