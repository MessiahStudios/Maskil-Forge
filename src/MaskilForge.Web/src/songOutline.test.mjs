import assert from 'node:assert/strict'
import test from 'node:test'
import { adjacentSectionId, songOutline, structuralRoleReview } from './songOutline.js'

test('song outline keeps song order and summarizes navigation context', () => {
  const project = {
    sections: [
      { id: 'intro', title: 'Intro', kind: 'Intro', delivery: 'Spoken', structuralFunction: 'Setup', lyricLines: [{ id: 'l1' }] },
      { id: 'chorus', title: 'Final Chorus', kind: 'Chorus', delivery: 'Sung', structuralFunction: 'Payoff', lyricLines: [{ id: 'l2' }, { id: 'l3' }] },
    ],
    timeline: { sectionPlacements: [
      { sectionId: 'intro', durationBars: 4 },
      { sectionId: 'chorus', durationBars: 8 },
    ] },
  }
  const readiness = { sections: [
    { sectionId: 'intro', hasLyrics: true, hasHarmony: false, hasRole: false, hasPlayablePart: false, ready: false },
    { sectionId: 'chorus', hasLyrics: true, hasHarmony: true, hasRole: true, hasPlayablePart: true, ready: true },
  ] }

  assert.deepEqual(songOutline(project, readiness), [
    { sectionId: 'intro', order: 1, title: 'Intro', kind: 'Intro', delivery: 'Spoken', structuralFunction: 'Setup', durationBars: 4, lyricLineCount: 1, ready: false, progress: 'Needs harmony' },
    { sectionId: 'chorus', order: 2, title: 'Final Chorus', kind: 'Chorus', delivery: 'Sung', structuralFunction: 'Payoff', durationBars: 8, lyricLineCount: 2, ready: true, progress: 'Ready to hear' },
  ])
})

test('song outline reports the first actionable gap for each section', () => {
  const project = {
    sections: [
      { id: 'a', title: 'A', kind: 'Verse', delivery: 'Sung', structuralFunction: 'Unspecified', lyricLines: [] },
      { id: 'b', title: 'B', kind: 'Verse', delivery: 'Sung', structuralFunction: 'Development', lyricLines: [] },
      { id: 'c', title: 'C', kind: 'Verse', delivery: 'Sung', structuralFunction: 'Transition', lyricLines: [] },
    ],
    timeline: { sectionPlacements: [] },
  }
  const readiness = { sections: [
    { sectionId: 'a', hasLyrics: false, hasHarmony: false, hasRole: false, hasPlayablePart: false, ready: false },
    { sectionId: 'b', hasLyrics: true, hasHarmony: true, hasRole: false, hasPlayablePart: false, ready: false },
    { sectionId: 'c', hasLyrics: true, hasHarmony: true, hasRole: true, hasPlayablePart: false, ready: false },
  ] }

  assert.deepEqual(songOutline(project, readiness).map(item => item.progress), [
    'Needs lyrics', 'Needs a musical job', 'Needs a playable part',
  ])
})

test('song outline asks for playable notes before a note-dependent part', () => {
  const project = {
    sections: [
      { id: 'verse', title: 'Verse', kind: 'Verse', delivery: 'Sung', structuralFunction: 'Setup', lyricLines: [{ id: 'l1' }] },
      { id: 'chorus', title: 'Chorus', kind: 'Chorus', delivery: 'Sung', structuralFunction: 'Payoff', lyricLines: [{ id: 'l2' }] },
    ],
    timeline: { sectionPlacements: [
      { sectionId: 'verse', durationBars: 8 },
      { sectionId: 'chorus', durationBars: 8 },
    ] },
  }
  const readiness = { sections: [
    { sectionId: 'verse', hasLyrics: true, hasHarmony: true, hasRole: true, hasPlayablePart: false, needsSourceNotes: true, ready: false },
    { sectionId: 'chorus', hasLyrics: true, hasHarmony: true, hasRole: true, hasPlayablePart: false, needsSourceNotes: false, ready: false },
  ] }

  assert.deepEqual(songOutline(project, readiness).map(item => item.progress), [
    'Needs playable notes', 'Needs a playable part',
  ])
})

test('phone capture readiness treats lyric-complete sections as ready to review', () => {
  const project = {
    sections: [
      { id: 'verse', title: 'Verse', kind: 'Verse', delivery: 'Sung', structuralFunction: 'Setup', lyricLines: [{ id: 'l1' }] },
    ],
    timeline: { sectionPlacements: [{ sectionId: 'verse', durationBars: 8 }] },
  }
  const readiness = { sections: [
    { sectionId: 'verse', hasLyrics: true, ready: true },
  ] }

  assert.deepEqual(songOutline(project, readiness).map(item => item.progress), ['Ready to review'])
})

test('focused navigation stays within the known song form', () => {
  const sections = [{ id: 'intro' }, { id: 'verse' }, { id: 'chorus' }]

  assert.equal(adjacentSectionId(sections, 'verse', -1), 'intro')
  assert.equal(adjacentSectionId(sections, 'verse', 1), 'chorus')
  assert.equal(adjacentSectionId(sections, 'intro', -1), null)
  assert.equal(adjacentSectionId(sections, 'missing', 1), null)
})

test('structural role review stays optional and finds the first open decision', () => {
  const project = { sections: [
    { id: 'intro', title: 'Intro', structuralFunction: 'Setup' },
    { id: 'verse', title: 'Verse 1', structuralFunction: 'Unspecified' },
    { id: 'chorus', title: 'Chorus', structuralFunction: 'Payoff' },
  ] }

  assert.deepEqual(structuralRoleReview(project), {
    sectionCount: 3,
    decidedCount: 2,
    complete: false,
    nextSectionId: 'verse',
    nextSectionTitle: 'Verse 1',
  })
  assert.deepEqual(structuralRoleReview(null), {
    sectionCount: 0,
    decidedCount: 0,
    complete: false,
    nextSectionId: null,
    nextSectionTitle: null,
  })
  assert.deepEqual(structuralRoleReview({ sections: [
    { id: 'intro', title: 'Intro', structuralFunction: 'Setup' },
    { id: 'outro', title: 'Outro', structuralFunction: 'Resolution' },
  ] }), {
    sectionCount: 2,
    decidedCount: 2,
    complete: true,
    nextSectionId: null,
    nextSectionTitle: null,
  })
})
