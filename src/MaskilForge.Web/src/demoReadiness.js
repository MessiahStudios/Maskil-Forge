export function demoReadiness(project) {
  if (!project) return { readySectionCount: 0, sectionCount: 0, complete: false, nextAction: 'Open or create a song.', nextStep: null, sections: [] }
  const noteIds = new Set(project.noteEvents.map(note => note.id))
  const sections = project.sections.map(section => {
    const roles = project.arrangementRoles.filter(item => item.sectionId === section.id)
    const parts = project.musicalParts.filter(item => item.sectionId === section.id)
    const hasLyrics = section.lyricLines.some(line => line.text.trim())
    const hasHarmony = section.harmony.length > 0
    const hasRole = roles.length > 0
    const hasPlayablePart = parts.some(part => part.noteEventIds.some(id => noteIds.has(id)))
    return {
      sectionId: section.id,
      title: section.title,
      hasLyrics,
      hasHarmony,
      hasRole,
      hasPlayablePart,
      ready: hasLyrics && hasHarmony && hasRole && hasPlayablePart,
    }
  })
  const readySectionCount = sections.filter(section => section.ready).length
  const firstGap = sections.find(section => !section.ready)
  let nextAction = 'Your structured demo is ready to hear, revise, save, and export.'
  let nextStep = null
  if (!sections.length) nextAction = 'Add the first song section.'
  else if (firstGap && !firstGap.hasLyrics) {
    nextAction = `Write a lyric line in ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'shape', action: 'lyrics', label: `Write ${firstGap.title} lyrics` }
  } else if (firstGap && !firstGap.hasHarmony) {
    nextAction = `Add harmony to ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'harmony', action: 'harmony', label: `Open ${firstGap.title} harmony` }
  } else if (firstGap && !firstGap.hasRole) {
    nextAction = `Choose an arrangement job for ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'arrangement', action: 'role', label: `Choose ${firstGap.title} job` }
  } else if (firstGap && !firstGap.hasPlayablePart) {
    nextAction = `Accept or create a playable part for ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'arrangement', action: 'part', label: `Build ${firstGap.title} part` }
  }
  return {
    readySectionCount,
    sectionCount: sections.length,
    complete: sections.length > 0 && readySectionCount === sections.length,
    nextAction,
    nextStep,
    sections,
  }
}
