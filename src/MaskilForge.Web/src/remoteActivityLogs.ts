import type { LogEntry } from './logging'
import type { ActivityLogDeviceKind } from './remoteActivityLogModel.js'

export interface RemoteActivityLogSessionSummary {
  sessionId: string
  deviceKind: ActivityLogDeviceKind
  viewportWidth: number
  viewportHeight: number
  standalone: boolean
  startedUtc: string
  lastSeenUtc: string
  entryCount: number
}

export interface RemoteActivityLogEntry extends LogEntry {
  sequence: number
}

export interface RemoteActivityLogSession {
  session: RemoteActivityLogSessionSummary
  entries: RemoteActivityLogEntry[]
}

async function readJson<T>(url: string): Promise<T> {
  const response = await fetch(url, { headers: { Accept: 'application/json' }, cache: 'no-store' })
  if (!response.ok) throw new Error(`Remote device logs are unavailable (${response.status}).`)
  return response.json() as Promise<T>
}

export function listRemoteActivityLogSessions() {
  return readJson<RemoteActivityLogSessionSummary[]>('/api/dev/activity-logs/sessions')
}

export function readRemoteActivityLogSession(sessionId: string) {
  return readJson<RemoteActivityLogSession>(`/api/dev/activity-logs/sessions/${encodeURIComponent(sessionId)}`)
}

export async function removeRemoteActivityLogSession(sessionId: string) {
  const response = await fetch(`/api/dev/activity-logs/sessions/${encodeURIComponent(sessionId)}`, {
    method: 'DELETE',
  })
  if (!response.ok) throw new Error(`Remote device session could not be removed (${response.status}).`)
}
