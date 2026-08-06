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
const maximumEntries = 1_000

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

export const activityLog = {
  read: readStoredEntries,

  write(level: LogLevel, action: string, message: string, details?: LogEntry['details']) {
    const entries = readStoredEntries()
    entries.push({
      id: crypto.randomUUID(),
      timestamp: new Date().toISOString(),
      level,
      action,
      message,
      details,
    })
    writeStoredEntries(entries)
    publish('changed')
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
