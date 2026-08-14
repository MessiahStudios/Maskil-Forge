import type { RecoverySummary } from './api'

export interface RecoveryQueueItem extends RecoverySummary {
  hasHostSnapshot: boolean
  hasBrowserSnapshot: boolean
  sourceLabel: string
  ageDays: number
  isStale: boolean
}

export interface RecoveryQueueStats {
  uniqueCount: number
  hiddenCount: number
  staleCount: number
  overSoftCap: boolean
}

export const recoveryRecentLimit: number
export const recoverySoftCap: number
export const recoveryStaleDays: number
export function buildRecoveryQueue(hostSnapshots: RecoverySummary[], browserSnapshots: RecoverySummary[], nowUtc?: string): RecoveryQueueItem[]
export function recoveryQueueStats(queue: RecoveryQueueItem[]): RecoveryQueueStats
