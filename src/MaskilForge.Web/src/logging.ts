import { activityLogDeviceKind } from './remoteActivityLogModel.js'

export type LogLevel = 'info' | 'success' | 'warning' | 'error'

export interface LogEntry {
  id: string
  timestamp: string
  level: LogLevel
  action: string
  message: string
  details?: Record<string, string | number | boolean | null>
}

const storageKey = 'maskilForge.activityLog.v1'
const channelName = 'maskilForge.activityLog'
const remoteSessionKey = 'maskilForge.remoteActivityLogSession.v1'
const maximumEntries = 1_000
const maximumRemoteQueue = 1_000

let remoteEnabled = false
let remoteSending = false
let remoteTimer: number | undefined
let remoteQueue: LogEntry[] = []

function readStoredEntries(): LogEntry[] {
  try {
    const value = JSON.parse(localStorage.getItem(storageKey) ?? '[]') as unknown
    return Array.isArray(value) ? value as LogEntry[] : []
  } catch {
    return []
  }
}

function writeStoredEntries(entries: LogEntry[]) {
  localStorage.setItem(storageKey, JSON.stringify(entries.slice(-maximumEntries)))
}

function publish(type: 'changed' | 'cleared') {
  if ('BroadcastChannel' in window) {
    const channel = new BroadcastChannel(channelName)
    channel.postMessage({ type })
    channel.close()
  }
}

function remoteSessionId() {
  const existing = sessionStorage.getItem(remoteSessionKey)
  if (existing) return existing
  const created = crypto.randomUUID()
  sessionStorage.setItem(remoteSessionKey, created)
  return created
}

function remoteClientContext() {
  const coarsePointer = window.matchMedia?.('(pointer: coarse)').matches ?? false
  return {
    sessionId: remoteSessionId(),
    deviceKind: activityLogDeviceKind(window.innerWidth, window.innerHeight, coarsePointer),
    viewportWidth: Math.max(1, Math.round(window.innerWidth)),
    viewportHeight: Math.max(1, Math.round(window.innerHeight)),
    standalone: window.matchMedia?.('(display-mode: standalone)').matches ?? false,
  }
}

function enqueueRemote(entry: LogEntry) {
  if (!remoteEnabled) return
  remoteQueue.push(entry)
  if (remoteQueue.length > maximumRemoteQueue) remoteQueue = remoteQueue.slice(-maximumRemoteQueue)
  if (remoteTimer !== undefined) return
  remoteTimer = window.setTimeout(() => {
    remoteTimer = undefined
    void flushRemote()
  }, 150)
}

async function flushRemote() {
  if (!remoteEnabled || remoteSending || remoteQueue.length === 0) return
  remoteSending = true
  const entries = remoteQueue.splice(0, 100)
  try {
    const response = await fetch('/api/dev/activity-logs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...remoteClientContext(), entries }),
      keepalive: true,
    })
    if (!response.ok) throw new Error(`Remote activity logging failed with status ${response.status}.`)
  } catch {
    remoteQueue = [...entries, ...remoteQueue].slice(-maximumRemoteQueue)
    remoteEnabled = false
  } finally {
    remoteSending = false
    if (remoteEnabled && remoteQueue.length > 0) void flushRemote()
  }
}

export const activityLog = {
  read: readStoredEntries,

  configureRemote(enabled: boolean) {
    if (!enabled) {
      remoteEnabled = false
      if (remoteTimer !== undefined) window.clearTimeout(remoteTimer)
      remoteTimer = undefined
      return
    }
    if (remoteEnabled) return
    remoteEnabled = true
    this.write('info', 'development.remote-logging', 'This browser session is now visible in the development activity console.', {
      deviceKind: remoteClientContext().deviceKind,
      standalone: remoteClientContext().standalone,
    })
  },

  write(level: LogLevel, action: string, message: string, details?: LogEntry['details']) {
    const entries = readStoredEntries()
    const entry = {
      id: crypto.randomUUID(),
      timestamp: new Date().toISOString(),
      level,
      action,
      message,
      details,
    }
    entries.push(entry)
    writeStoredEntries(entries)
    publish('changed')
    enqueueRemote(entry)
  },

  clear() {
    localStorage.removeItem(storageKey)
    publish('cleared')
  },

  subscribe(callback: () => void) {
    const storageListener = (event: StorageEvent) => {
      if (event.key === storageKey) callback()
    }
    window.addEventListener('storage', storageListener)

    const channel = 'BroadcastChannel' in window ? new BroadcastChannel(channelName) : null
    if (channel) channel.onmessage = callback

    return () => {
      window.removeEventListener('storage', storageListener)
      channel?.close()
    }
  },
}

export function formatLogEntries(entries: LogEntry[]): string {
  return entries.map(entry => {
    const details = entry.details ? ` ${JSON.stringify(entry.details)}` : ''
    return `${entry.timestamp} [${entry.level.toUpperCase()}] ${entry.action}: ${entry.message}${details}`
  }).join('\n')
}
