export function firstWritableEmptyLyricLine(section, lockedLineIds = []) {
  const locked = new Set(lockedLineIds)
  return (section?.lyricLines ?? []).find(line => !String(line.text ?? '').trim() && !locked.has(line.id)) ?? null
}

const chordRealizableRoles = new Set(['Harmony', 'Texture'])

function sectionTickRange(project, sectionId) {
  const placement = project.timeline?.sectionPlacements?.find(item => item.sectionId === sectionId)
  const meter = project.timeline?.timeSignatureMap?.events?.[0]
  const ticksPerQuarterNote = project.timeline?.ticksPerQuarterNote
  if (!placement?.start || !meter || !ticksPerQuarterNote) return null
  const ticksPerBeat = ticksPerQuarterNote * 4 / meter.denominator
  const ticksPerBar = meter.numerator * ticksPerBeat
  const startTick = (placement.start.bar - 1) * ticksPerBar
  return { startTick, endTick: startTick + placement.durationBars * ticksPerBar }
}

function sectionHasApprovedNotes(project, sectionId) {
  const range = sectionTickRange(project, sectionId)
  if (!range) return false
  return (project.noteEvents ?? []).some(note => note.startTick >= range.startTick && note.startTick < range.endTick)
}

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
    const needsSourceNotes = hasRole && !hasPlayablePart
      && !roles.some(item => chordRealizableRoles.has(item.role))
      && !sectionHasApprovedNotes(project, section.id)
    return {
      sectionId: section.id,
      title: section.title,
      hasLyrics,
      hasHarmony,
      hasRole,
      hasPlayablePart,
      needsSourceNotes,
      ready: hasLyrics && hasHarmony && hasRole && hasPlayablePart,
    }
  })
  const readySectionCount = sections.filter(section => section.ready).length
  const firstGap = sections.find(section => !section.ready)
  let nextAction = 'Your structured demo is ready to hear, revise, save, and export.'
  let nextStep = null
  if (!sections.length) {
    nextAction = 'Add the first song section.'
    nextStep = { sectionId: null, stage: 'shape', action: 'section', label: 'Add the first section' }
  }
  else if (firstGap && !firstGap.hasLyrics) {
    nextAction = `Write a lyric line in ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'shape', action: 'lyrics', label: `Write ${firstGap.title} lyrics` }
  } else if (firstGap && !firstGap.hasHarmony) {
    nextAction = `Add harmony to ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'harmony', action: 'harmony', label: `Open ${firstGap.title} harmony` }
  } else if (firstGap && !firstGap.hasRole) {
    nextAction = `Choose an arrangement job for ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'arrangement', action: 'role', label: `Choose ${firstGap.title} job` }
  } else if (firstGap && firstGap.needsSourceNotes) {
    nextAction = `Turn ${firstGap.title} harmony into playable notes.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'harmony', action: 'sketch', label: `Prepare ${firstGap.title} notes` }
  } else if (firstGap && !firstGap.hasPlayablePart) {
    nextAction = `Accept or create a playable part for ${firstGap.title}.`
    nextStep = { sectionId: firstGap.sectionId, stage: 'arrangement', action: 'part', label: `Build ${firstGap.title} part` }
  } else {
    nextStep = { sectionId: null, stage: 'arrangement', action: 'hear', label: 'Hear the song' }
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
