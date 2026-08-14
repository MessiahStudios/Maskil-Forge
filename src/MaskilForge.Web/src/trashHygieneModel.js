export const trashRecentLimit = 12
export const trashOldDays = 30

export function buildTrashQueue(projects, nowUtc = new Date().toISOString()) {
  const now = Date.parse(nowUtc)
  return projects.map(project => {
    const ageMilliseconds = Number.isFinite(now) ? Math.max(0, now - Date.parse(project.deletedAtUtc)) : 0
    const ageDays = Number.isFinite(ageMilliseconds) ? Math.floor(ageMilliseconds / 86_400_000) : 0
    return { ...project, ageDays, isOld: ageDays >= trashOldDays }
  })
}

export function filterTrashQueue(projects, query = '') {
  const normalizedQuery = query.trim().toLocaleLowerCase()
  if (!normalizedQuery) return projects
  return projects.filter(project => `${project.title} ${project.artist}`.toLocaleLowerCase().includes(normalizedQuery))
}

export function trashResultStats(projects, showAll = false) {
  const resultCount = projects.length
  const visibleCount = showAll ? resultCount : Math.min(resultCount, trashRecentLimit)
  return {
    resultCount,
    visibleCount,
    hiddenCount: resultCount - visibleCount,
    oldCount: projects.filter(project => project.isOld).length,
  }
}

export function trashAgeLabel(ageDays) {
  if (ageDays === 0) return 'Today'
  if (ageDays === 1) return '1 day'
  return `${ageDays} days`
}
