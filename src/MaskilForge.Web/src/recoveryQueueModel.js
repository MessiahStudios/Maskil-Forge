export const recoveryRecentLimit = 5
export const recoverySoftCap = 10
export const recoveryStaleDays = 30

export function buildRecoveryQueue(hostSnapshots, browserSnapshots, nowUtc = new Date().toISOString()) {
  const entries = new Map()

  for (const snapshot of hostSnapshots) {
    entries.set(snapshot.id, { hostSnapshot: snapshot, browserSnapshot: null })
  }
  for (const snapshot of browserSnapshots) {
    const existing = entries.get(snapshot.id)
    entries.set(snapshot.id, {
      hostSnapshot: existing?.hostSnapshot ?? null,
      browserSnapshot: snapshot,
    })
  }

  const now = Date.parse(nowUtc)
  return [...entries.values()]
    .map(entry => {
      const candidates = [entry.hostSnapshot, entry.browserSnapshot].filter(Boolean)
      const summary = candidates.sort((left, right) => right.capturedAtUtc.localeCompare(left.capturedAtUtc))[0]
      const ageMilliseconds = Number.isFinite(now) ? Math.max(0, now - Date.parse(summary.capturedAtUtc)) : 0
      const ageDays = Number.isFinite(ageMilliseconds) ? Math.floor(ageMilliseconds / 86_400_000) : 0
      return {
        ...summary,
        hasHostSnapshot: Boolean(entry.hostSnapshot),
        hasBrowserSnapshot: Boolean(entry.browserSnapshot),
        sourceLabel: entry.hostSnapshot && entry.browserSnapshot
          ? 'Local host + this browser'
          : entry.browserSnapshot ? 'This browser' : 'Local host',
        ageDays,
        isStale: ageDays >= recoveryStaleDays,
      }
    })
    .sort((left, right) => right.capturedAtUtc.localeCompare(left.capturedAtUtc))
}

export function recoveryQueueStats(queue) {
  return {
    uniqueCount: queue.length,
    hiddenCount: Math.max(0, queue.length - recoveryRecentLimit),
    staleCount: queue.filter(item => item.isStale).length,
    overSoftCap: queue.length > recoverySoftCap,
  }
}
