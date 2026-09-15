const normalized = value => String(value ?? '').normalize('NFC').toLowerCase()

export function candidateArchitecture(candidate) {
  const binary = candidate.binary
  if (binary?.status !== 'Inspected') return 'undetermined'
  const files = binary.files ?? []
  if (files.some(file => file.status === 'Recognized' && file.hostMatch === 'Match')) return 'match'
  if (files.length && files.every(file => file.status === 'Recognized' && file.hostMatch === 'Different')) return 'different'
  return 'undetermined'
}

export function reviewPluginInventory(result, { query = '', architecture = 'all', repeatedOnly = false } = {}) {
  const terms = normalized(query).trim().split(/\s+/u).filter(Boolean)
  const groups = new Map()
  const locations = (result?.locations ?? []).map((location, locationIndex) => {
    const rows = location.candidates.map((candidate, candidateIndex) => {
      const key = `${locationIndex}:${candidateIndex}`
      const module = candidate.metadata?.status === 'Available' ? candidate.metadata.module : null
      const classIds = new Set()
      for (const reported of module?.classes ?? []) {
        const id = String(reported.id ?? '').toUpperCase()
        if (!/^[0-9A-F]{32}$/.test(id) || classIds.has(id)) continue
        classIds.add(id)
        if (!groups.has(id)) groups.set(id, { id, occurrences: [] })
        groups.get(id).occurrences.push({ key, location: location.name, path: candidate.relativePath,
          name: reported.name, category: reported.category, version: reported.version ?? null })
      }
      const searchable = normalized([location.name, candidate.name, candidate.relativePath, module?.name, module?.vendor, module?.version,
        ...(module?.classes ?? []).flatMap(item => [item.id, item.name, item.category, item.vendor, item.version, ...(item.subCategories ?? [])])].join(' '))
      return { key, candidate, architecture: candidateArchitecture(candidate), classIds, searchable, repeatedClassCount: 0 }
    })
    return { ...location, rows, totalCount: rows.length }
  })
  const repeatedClasses = [...groups.values()].filter(group => group.occurrences.length > 1).sort((a, b) => a.id.localeCompare(b.id))
  const repeatedIds = new Set(repeatedClasses.map(group => group.id))
  let totalCount = 0, visibleCount = 0
  for (const location of locations) {
    totalCount += location.totalCount
    for (const row of location.rows) row.repeatedClassCount = [...row.classIds].filter(id => repeatedIds.has(id)).length
    location.rows = location.rows.filter(row => terms.every(term => row.searchable.includes(term))
      && (architecture === 'all' || row.architecture === architecture)
      && (!repeatedOnly || row.repeatedClassCount > 0))
    visibleCount += location.rows.length
  }
  return { locations, totalCount, visibleCount, repeatedClasses }
}
