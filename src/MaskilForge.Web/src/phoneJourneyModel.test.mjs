import assert from 'node:assert/strict'
import test from 'node:test'
import {
  phoneCaptureReadiness,
  phoneCreatorStages,
  phoneDestination,
  phoneEditorChrome,
  phoneJourneyProgress,
  phoneShowsSongOutline,
  remapDesktopStageForPhone,
  remapPhoneStageForDesktop,
} from './phoneJourneyModel.js'

const line = text => ({ text })
const section = (id, title, lyrics = '') => ({ id, title, lyricLines: lyrics ? [line(lyrics)] : [line('')] })
const project = overrides => ({ rawLyricDraft: '', sections: [], ...overrides })

test('phone editor chrome keeps identity settings and hides music controls', () => {
  assert.deepEqual(phoneEditorChrome(), {
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
  })
})

test('phone shape outline waits until a second section exists', () => {
  assert.equal(phoneShowsSongOutline(0), false)
  assert.equal(phoneShowsSongOutline(1), false)
  assert.equal(phoneShowsSongOutline(2), true)
})

test('phone journey excludes music, harmony, and arrangement', () => {
  assert.deepEqual(phoneCreatorStages.map(stage => stage.id), ['idea', 'words', 'shape', 'review', 'approve'])
  assert.equal(remapDesktopStageForPhone('harmony'), 'shape')
  assert.equal(remapDesktopStageForPhone('arrangement'), 'shape')
  assert.equal(remapDesktopStageForPhone('music'), 'shape')
  assert.equal(remapDesktopStageForPhone('words'), 'words')
  assert.equal(remapPhoneStageForDesktop('review'), 'shape')
  assert.equal(remapPhoneStageForDesktop('approve'), 'shape')
  assert.equal(remapPhoneStageForDesktop('harmony'), 'harmony')
})

test('phone destinations keep capture and structure separate from production tools', () => {
  assert.deepEqual(phoneDestination('idea'), { view: 'capture', target: 'capture-title', open: false, focus: false })
  assert.deepEqual(phoneDestination('words'), { view: 'capture', target: 'raw-lyric-draft', open: false, focus: true })
  assert.deepEqual(phoneDestination('shape'), { view: 'structure', target: 'song-structure', open: false, focus: false })
  assert.deepEqual(phoneDestination('review'), { view: 'structure', target: 'phone-review', open: false, focus: false })
  assert.deepEqual(phoneDestination('approve'), { view: 'structure', target: 'phone-approve', open: false, focus: false })
  assert.equal(phoneDestination('harmony'), null)
})

test('review and approve guide an empty song toward its first section', () => {
  assert.deepEqual(phoneDestination('review', false), {
    view: 'structure',
    target: 'song-structure',
    open: false,
    focus: false,
    stage: 'shape',
    message: 'Add a section first, then review the song on this phone.',
  })
  assert.deepEqual(phoneDestination('approve', false), {
    view: 'structure',
    target: 'song-structure',
    open: false,
    focus: false,
    stage: 'shape',
    message: 'Shape the song first, then approve this capture.',
  })
})

test('phone progress ignores harmony and playable notes', () => {
  const progress = phoneJourneyProgress(project({
    rawLyricDraft: 'A first thought',
    sections: [section('verse', 'Verse', 'A line')],
    arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }],
    noteEvents: [{ id: 'note' }],
  }))
  assert.deepEqual(progress, {
    idea: true, words: true, shape: true, review: true, approve: true,
  })
})

test('empty phone captures start with words instead of a section toolbar', () => {
  const review = phoneCaptureReadiness(project())
  assert.equal(review.complete, false)
  assert.equal(review.nextAction, 'Write the first words on this phone.')
  assert.deepEqual(review.nextStep, {
    sectionId: null, stage: 'words', action: 'words', label: 'Write the first words',
  })
})

test('pasted lyric-sheet headings still prefer structure preview', () => {
  const review = phoneCaptureReadiness(project({
    rawLyricDraft: '[Intro]\nSpoken air\n[Verse 1]\nA line',
  }))
  assert.equal(review.nextAction, 'Review the pasted lyric sheet as song structure.')
  assert.deepEqual(review.nextStep, {
    sectionId: null, stage: 'shape', action: 'preview', label: 'Preview song structure',
  })
})

test('unknown lyric-sheet headings are reviewed before creating sections', () => {
  const unresolved = phoneCaptureReadiness(project({ rawLyricDraft: '[Post-Chorus]\nKeep the fire' }), {
    sections: [],
    unrecognizedHeadings: ['Post-Chorus'],
    unrecognizedSections: [{ heading: 'Post-Chorus' }],
  })
  assert.equal(unresolved.nextStep.action, 'resolve')
})

test('phone readiness asks for section lyrics, not chords', () => {
  const review = phoneCaptureReadiness(project({
    rawLyricDraft: 'A draft',
    sections: [section('verse', 'Verse')],
  }))
  assert.equal(review.sections[0].hasLyrics, false)
  assert.equal(review.nextAction, 'Write a lyric line in Verse.')
  assert.deepEqual(review.nextStep, {
    sectionId: 'verse', stage: 'shape', action: 'lyrics', label: 'Write Verse lyrics',
  })
})

test('a shaped lyric song asks to review before approve', () => {
  const review = phoneCaptureReadiness(project({
    rawLyricDraft: 'A draft',
    sections: [section('chorus', 'Chorus', 'A hook')],
  }))
  assert.equal(review.complete, true)
  assert.equal(review.nextAction, 'Review the words and song form on this phone.')
  assert.deepEqual(review.nextStep, {
    sectionId: null, stage: 'review', action: 'review', label: 'Review the song',
  })
})

test('review and approve ask to save a dirty capture', () => {
  const shaped = project({
    rawLyricDraft: 'A draft',
    sections: [section('chorus', 'Chorus', 'A hook')],
  })
  const review = phoneCaptureReadiness(shaped, null, { isDirty: true, activeStage: 'review' })
  assert.equal(review.complete, false)
  assert.equal(review.nextStep.action, 'approve')
  assert.equal(review.nextStep.label, 'Save and approve')

  const saved = phoneCaptureReadiness(shaped, null, { isDirty: false, activeStage: 'approve' })
  assert.equal(saved.complete, true)
  assert.equal(saved.nextAction, 'This capture is saved. Continue harmony and arrangement on a larger screen.')
  assert.equal(saved.nextStep.label, 'Approve this capture')
})
