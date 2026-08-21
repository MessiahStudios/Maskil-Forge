export function activityLogDeviceKind(width, height, coarsePointer) {
  const shortestSide = Math.min(width, height)
  if (coarsePointer && shortestSide <= 600) return 'phone'
  if (coarsePointer && shortestSide <= 1_100) return 'tablet'
  return 'desktop'
}

export function remoteActivityLogSessionLabel(session) {
  const device = session.deviceKind[0].toUpperCase() + session.deviceKind.slice(1)
  const display = session.standalone ? 'installed' : 'browser'
  return `${device} · ${session.viewportWidth}×${session.viewportHeight} · ${display}`
}

export function remoteActivityLogSessionOptions(sessions, formatTime = value => new Date(value).toLocaleTimeString()) {
  const fingerprints = sessions.map(remoteActivityLogSessionLabel)
  const totals = new Map()
  fingerprints.forEach(fingerprint => totals.set(fingerprint, (totals.get(fingerprint) ?? 0) + 1))
  const seen = new Map()

  return sessions.map((session, index) => {
    const fingerprint = fingerprints[index]
    const occurrence = seen.get(fingerprint) ?? 0
    seen.set(fingerprint, occurrence + 1)
    const activity = totals.get(fingerprint) > 1
      ? occurrence === 0 ? 'latest' : 'earlier'
      : 'last active'
    return {
      sessionId: session.sessionId,
      label: `${fingerprint} · ${activity} ${formatTime(session.lastSeenUtc)}`,
    }
  })
}
