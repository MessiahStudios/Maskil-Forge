export function songOutline(project, readiness) {
  if (!project) return []
  const readinessBySection = new Map(readiness.sections.map(section => [section.sectionId, section]))
  const placements = new Map(project.timeline.sectionPlacements.map(placement => [placement.sectionId, placement]))
  return project.sections.map((section, index) => {
    const review = readinessBySection.get(section.id)
    let progress = 'Not started'
    if (review?.ready) progress = 'hasHarmony' in review ? 'Ready to hear' : 'Ready to review'
    else if (review && !review.hasLyrics) progress = 'Needs lyrics'
    else if (review && review.hasHarmony === false) progress = 'Needs harmony'
    else if (review && review.hasRole === false) progress = 'Needs a musical job'
    else if (review?.needsSourceNotes) progress = 'Needs playable notes'
    else if (review) progress = 'Needs a playable part'
    return {
      sectionId: section.id,
      order: index + 1,
      title: section.title,
      kind: section.kind,
      delivery: section.delivery,
      structuralFunction: section.structuralFunction,
      durationBars: placements.get(section.id)?.durationBars ?? 0,
      lyricLineCount: section.lyricLines.length,
      ready: Boolean(review?.ready),
      progress,
    }
  })
}

export function adjacentSectionId(sections, currentSectionId, offset) {
  if (offset !== -1 && offset !== 1) return null
  const index = sections.findIndex(section => section.id === currentSectionId)
  if (index < 0) return null
  return sections[index + offset]?.id ?? null
}

export function structuralRoleReview(project) {
  const sections = project?.sections ?? []
  const decidedCount = sections.filter(section => section.structuralFunction !== 'Unspecified').length
  const nextSection = sections.find(section => section.structuralFunction === 'Unspecified')
  return {
    sectionCount: sections.length,
    decidedCount,
    complete: sections.length > 0 && decidedCount === sections.length,
    nextSectionId: nextSection?.id ?? null,
    nextSectionTitle: nextSection?.title ?? null,
  }
}
