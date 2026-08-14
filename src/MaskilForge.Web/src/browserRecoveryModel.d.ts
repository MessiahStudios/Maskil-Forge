import type { RecoverySummary } from './api'
import type { BrowserRecoveryRecord } from './browserRecovery'

export function summarizeBrowserRecovery(record: BrowserRecoveryRecord): RecoverySummary
export function browserRecoveryNotice(count: number, connected: boolean): string
