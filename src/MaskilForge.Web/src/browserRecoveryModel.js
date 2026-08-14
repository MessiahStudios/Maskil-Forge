export function summarizeBrowserRecovery(record) {
  const structuredLines = record.project.sections.flatMap(section => section.lyricLines).filter(line => line.text.trim())
  const rawLines = record.project.rawLyricDraft.split(/\r?\n/).filter(line => line.trim())
  return {
    id: record.projectId,
    title: record.project.title,
    artist: record.project.artist,
    capturedAtUtc: record.capturedAtUtc,
    sectionCount: record.project.sections.length,
    lyricLineCount: record.project.sections.length ? structuredLines.length : rawLines.length,
    hasRawLyrics: Boolean(record.project.rawLyricDraft.trim()),
    sectionTitles: record.project.sections.map(section => section.title),
  }
}

export function browserRecoveryNotice(count, connected) {
  if (count === 0) return ''
  const snapshots = `${count} browser recovery snapshot${count === 1 ? '' : 's'}`
  return connected
    ? `${snapshots} ${count === 1 ? 'is' : 'are'} ready to return to the local project service.`
    : `${snapshots} ${count === 1 ? 'is' : 'are'} protected on this device until the local project service reconnects.`
}
