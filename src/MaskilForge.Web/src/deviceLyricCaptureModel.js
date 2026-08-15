export const deviceLyricCaptureRecentLimit = 12

export function deviceLyricCaptureSnapshot(capture) {
  return JSON.stringify({
    title: capture.title,
    artist: capture.artist,
    genre: capture.genre,
    description: capture.description,
    rawLyricDraft: capture.rawLyricDraft,
  })
}

export function summarizeDeviceLyricCapture(capture) {
  return {
    id: capture.captureId,
    title: capture.title.trim() || 'Untitled capture',
    artist: capture.artist.trim(),
    genre: capture.genre,
    savedAtUtc: capture.savedAtUtc,
    lyricLineCount: capture.rawLyricDraft.split(/\r?\n/).filter(line => line.trim()).length,
  }
}

export function deviceLyricCaptureNotice(count) {
  if (count === 0) return 'No browser-owned lyric captures are saved on this device yet.'
  return `${count} browser-owned lyric capture${count === 1 ? ' is' : 's are'} saved only on this device.`
}

export function filterDeviceLyricCaptures(captures, query = '') {
  const normalizedQuery = query.trim().toLocaleLowerCase()
  if (!normalizedQuery) return captures
  return captures.filter(capture => `${capture.title} ${capture.artist}`.toLocaleLowerCase().includes(normalizedQuery))
}

export function deviceLyricCaptureResultStats(captures, showAll = false) {
  const resultCount = captures.length
  const visibleCount = showAll ? resultCount : Math.min(resultCount, deviceLyricCaptureRecentLimit)
  return {
    resultCount,
    visibleCount,
    hiddenCount: resultCount - visibleCount,
  }
}
