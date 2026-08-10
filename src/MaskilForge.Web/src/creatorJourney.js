export const creatorStages = [
  { id: 'idea', label: 'Idea' },
  { id: 'words', label: 'Words' },
  { id: 'shape', label: 'Shape' },
  { id: 'music', label: 'Music' },
  { id: 'harmony', label: 'Harmony' },
  { id: 'arrangement', label: 'Arrangement' },
]

export function creatorProgress(project) {
  const lines = project?.sections.flatMap(section => section.lyricLines) ?? []
  return {
    idea: Boolean(project),
    words: Boolean(project?.rawLyricDraft.trim()) || lines.some(line => line.text.trim()),
    shape: Boolean(project?.sections.length),
    music: lines.some(line => line.syllablePlacements.length || line.rhythmCandidates.length),
    harmony: Boolean(project?.sections.some(section => section.harmony.length)),
    arrangement: Boolean(project?.arrangement?.length || project?.arrangementRoles?.length),
  }
}

export function creatorDestination(stage, hasSections = true) {
  if (!hasSections && (stage === 'harmony' || stage === 'arrangement')) {
    return {
      view: 'structure',
      target: 'song-structure',
      open: false,
      focus: false,
      stage: 'shape',
      message: `Add a section first, then ${stage === 'harmony' ? 'explore harmony' : 'plan its arrangement'}.`,
    }
  }
  if (stage === 'idea') return { view: 'capture', target: 'capture-title', open: false, focus: false }
  if (stage === 'words') return { view: 'capture', target: 'raw-lyric-draft', open: false, focus: true }
  if (stage === 'shape') return { view: 'structure', target: 'song-structure', open: false, focus: false }
  if (stage === 'music') return { view: 'structure', target: 'musical-refinement', open: true, focus: false }
  if (stage === 'harmony') return { view: 'structure', target: 'harmony-tools', open: true, focus: false }
  if (stage === 'arrangement') return { view: 'structure', target: 'arrangement-blueprint', open: false, focus: false }
  return null
}
