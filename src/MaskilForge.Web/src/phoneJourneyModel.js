import { firstWritableEmptyLyricLine, hasLyricSheetHeadings } from './demoReadiness.js'

export const phoneLayoutMaxWidth = 620

export function phoneEditorChrome() {
  return {
    keepSaveInBar: true,
    keepUndoRedoInBar: false,
    showJourneyIntro: false,
    showJourneyProgress: false,
    showMusicSettings: false,
    showDeveloperDetails: false,
    compactHostStatus: true,
    showSectionTiming: false,
    showSectionPerformance: false,
    lyricsBeforeRole: true,
    collapseSectionRole: true,
    showRoleReview: false,
    showLyricLocks: false,
    compactShapeChrome: true,
    showReadyHostStatus: false,
    compactSectionToolbar: true,
    compactCaptureChrome: true,
    separateReviewFromShape: true,
  }
}

export const phoneCreatorStages = [
  { id: 'idea', label: 'Idea' },
  { id: 'words', label: 'Words' },
  { id: 'shape', label: 'Shape' },
  { id: 'review', label: 'Review' },
  { id: 'approve', label: 'Approve' },
]

const desktopProductionStages = new Set(['music', 'harmony', 'arrangement'])

function hasUnresolvedLyricSheetHeadings(preview) {
  return Boolean(preview?.unrecognizedSections?.length || preview?.unrecognizedHeadings?.length)
}

function lyricLines(project) {
  return project?.sections.flatMap(section => section.lyricLines) ?? []
}

function hasWords(project) {
  return Boolean(project?.rawLyricDraft?.trim()) || lyricLines(project).some(line => String(line.text ?? '').trim())
}

function sectionHasLyrics(section) {
  return (section?.lyricLines ?? []).some(line => String(line.text ?? '').trim())
}

export function remapDesktopStageForPhone(stage) {
  if (desktopProductionStages.has(stage)) return 'shape'
  return stage
}

export function remapPhoneStageForDesktop(stage) {
  if (stage === 'review' || stage === 'approve') return 'shape'
  return stage
}

export function phoneJourneyProgress(project) {
  const sections = project?.sections ?? []
  const shaped = sections.length > 0
  const reviewed = hasWords(project) && shaped
  return {
    idea: Boolean(project),
    words: hasWords(project),
    shape: shaped,
    review: reviewed,
    approve: reviewed && sections.every(sectionHasLyrics),
  }
}

export function phoneDestination(stage, hasSections = true) {
  if (stage === 'idea') return { view: 'capture', target: 'capture-title', open: false, focus: false }
  if (stage === 'words') return { view: 'capture', target: 'raw-lyric-draft', open: false, focus: true }
  if (stage === 'shape') return { view: 'structure', target: 'song-structure', open: false, focus: false }
  if (stage === 'review') {
    if (!hasSections) {
      return {
        view: 'structure',
        target: 'song-structure',
        open: false,
        focus: false,
        stage: 'shape',
        message: 'Add a section first, then review the song on this phone.',
      }
    }
    return { view: 'structure', target: 'phone-review', open: false, focus: false }
  }
  if (stage === 'approve') {
    if (!hasSections) {
      return {
        view: 'structure',
        target: 'song-structure',
        open: false,
        focus: false,
        stage: 'shape',
        message: 'Shape the song first, then approve this capture.',
      }
    }
    return { view: 'structure', target: 'phone-approve', open: false, focus: false }
  }
  return null
}

export function phoneCaptureReadiness(project, preview = null, options = {}) {
  const { isDirty = false, activeStage = 'idea', lockedLineIds = [] } = options
  if (!project) {
    return { complete: false, nextAction: 'Open or create a song.', nextStep: null, sections: [] }
  }

  const sections = (project.sections ?? []).map(section => {
    const lyrics = sectionHasLyrics(section)
    return { sectionId: section.id, title: section.title, hasLyrics: lyrics, ready: lyrics }
  })
  const firstLyricGap = sections.find(section => !section.hasLyrics)

  let nextAction = 'This capture is saved. Continue harmony and arrangement on a larger screen.'
  let nextStep = null

  if (!hasWords(project) && !sections.length) {
    nextAction = 'Write the first words on this phone.'
    nextStep = { sectionId: null, stage: 'words', action: 'words', label: 'Write the first words' }
  } else if (!sections.length) {
    if (hasLyricSheetHeadings(project.rawLyricDraft)) {
      if (hasUnresolvedLyricSheetHeadings(preview)) {
        nextAction = 'Map the unknown lyric-sheet heading to a section type.'
        nextStep = { sectionId: null, stage: 'shape', action: 'resolve', label: 'Review unknown heading' }
      } else {
        nextAction = 'Review the pasted lyric sheet as song structure.'
        nextStep = { sectionId: null, stage: 'shape', action: 'preview', label: preview?.sections?.length ? 'Create sections' : 'Preview song structure' }
      }
    } else {
      nextAction = 'Add the first song section.'
      nextStep = { sectionId: null, stage: 'shape', action: 'section', label: 'Add the first section' }
    }
  } else if (firstLyricGap) {
    const section = project.sections.find(item => item.id === firstLyricGap.sectionId)
    const writable = firstWritableEmptyLyricLine(section, lockedLineIds)
    nextAction = writable
      ? `Write a lyric line in ${firstLyricGap.title}.`
      : `Add a lyric line in ${firstLyricGap.title}.`
    nextStep = { sectionId: firstLyricGap.sectionId, stage: 'shape', action: 'lyrics', label: `Write ${firstLyricGap.title} lyrics` }
  } else if (activeStage === 'review' || activeStage === 'approve') {
    nextAction = isDirty
      ? 'Save this capture before continuing music on a larger screen.'
      : 'This capture is saved. Continue harmony and arrangement on a larger screen.'
    nextStep = { sectionId: null, stage: 'approve', action: 'approve', label: isDirty ? 'Save and approve' : 'Approve this capture' }
  } else {
    nextAction = 'Review the words and song form on this phone.'
    nextStep = { sectionId: null, stage: 'review', action: 'review', label: 'Review the song' }
  }

  return {
    complete: sections.length > 0 && sections.every(section => section.ready) && !isDirty,
    nextAction,
    nextStep,
    sections,
  }
}

export function phoneShowsSongOutline(sectionCount) {
  return sectionCount > 1
}
