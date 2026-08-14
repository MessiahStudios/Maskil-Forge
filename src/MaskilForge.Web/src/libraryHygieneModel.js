export const libraryRecentLimit = 12

export function projectLibraryStage(project) {
  if (project.sectionCount > 0) return 'structured'
  if (project.hasRawLyrics) return 'raw'
  return 'empty'
}

export function filterProjectLibrary(projects, query = '', stage = 'all') {
  const normalizedQuery = query.trim().toLocaleLowerCase()
  return projects.filter(project => {
    if (stage !== 'all' && projectLibraryStage(project) !== stage) return false
    if (!normalizedQuery) return true
    return `${project.title} ${project.artist}`.toLocaleLowerCase().includes(normalizedQuery)
  })
}

export function libraryResultStats(projects, showAll = false) {
  const resultCount = projects.length
  const visibleCount = showAll ? resultCount : Math.min(resultCount, libraryRecentLimit)
  return {
    resultCount,
    visibleCount,
    hiddenCount: resultCount - visibleCount,
  }
}
