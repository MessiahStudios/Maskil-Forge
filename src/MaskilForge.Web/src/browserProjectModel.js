export function summarizeBrowserProject(record) {
  const structuredLines = record.project.sections.flatMap(section => section.lyricLines).filter(line => line.text.trim())
  const rawLines = record.project.rawLyricDraft.split(/\r?\n/).filter(line => line.trim())
  return {
    id: record.projectId,
    title: record.project.title,
    artist: record.project.artist,
    genre: record.project.genre,
    savedAtUtc: record.savedAtUtc,
    lastModifiedUtc: record.project.lastModifiedUtc,
    sectionCount: record.project.sections.length,
    lyricLineCount: record.project.sections.length ? structuredLines.length : rawLines.length,
    hasRawLyrics: Boolean(record.project.rawLyricDraft.trim()),
    sectionTitles: record.project.sections.map(section => section.title),
  }
}

export function browserProjectNotice(count) {
  if (count === 0) return 'No saved song snapshots are available for offline review on this device yet.'
  return `${count} explicitly saved song snapshot${count === 1 ? ' is' : 's are'} available for view-only review on this device.`
}
