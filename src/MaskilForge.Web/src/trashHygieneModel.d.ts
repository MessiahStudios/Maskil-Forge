import type { TrashedProjectSummary } from './api'

export interface TrashQueueItem extends TrashedProjectSummary {
  ageDays: number
  isOld: boolean
}

export interface TrashResultStats {
  resultCount: number
  visibleCount: number
  hiddenCount: number
  oldCount: number
}

export const trashRecentLimit: number
export const trashOldDays: number
export function buildTrashQueue(projects: TrashedProjectSummary[], nowUtc?: string): TrashQueueItem[]
export function filterTrashQueue(projects: TrashQueueItem[], query?: string): TrashQueueItem[]
export function trashResultStats(projects: TrashQueueItem[], showAll?: boolean): TrashResultStats
export function trashAgeLabel(ageDays: number): string
