import assert from 'node:assert/strict'
import test from 'node:test'
import { demoReadiness } from './demoReadiness.js'

const line = text => ({ text })
const section = (id, title, lyrics = '', harmony = []) => ({ id, title, lyricLines: lyrics ? [line(lyrics)] : [], harmony })
const project = overrides => ({ sections: [], arrangementRoles: [], musicalParts: [], noteEvents: [], ...overrides })

test('demo readiness reports the first artist-actionable gap', () => {
  const review = demoReadiness(project({ sections: [section('verse', 'Verse')] }))
  assert.equal(review.complete, false)
  assert.equal(review.nextAction, 'Write a lyric line in Verse.')
  assert.deepEqual(review.nextStep, { sectionId: 'verse', stage: 'shape', action: 'lyrics', label: 'Write Verse lyrics' })
})

test('next step targets the first incomplete section and its required workspace', () => {
  const review = demoReadiness(project({
    sections: [
      section('verse', 'Verse', 'Complete words', [{ id: 'chord' }]),
      section('chorus', 'Chorus', 'Hook needs chords'),
    ],
    arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }],
    musicalParts: [{ sectionId: 'verse', noteEventIds: ['note'] }],
    noteEvents: [{ id: 'note' }],
  }))
  assert.deepEqual(review.nextStep, { sectionId: 'chorus', stage: 'harmony', action: 'harmony', label: 'Open Chorus harmony' })
})

test('demo readiness requires resolved playable part notes in every section', () => {
  const review = demoReadiness(project({
    sections: [section('verse', 'Verse', 'A line', [{ id: 'chord' }])],
    arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }],
    musicalParts: [{ sectionId: 'verse', noteEventIds: ['note'] }],
    noteEvents: [{ id: 'note' }],
  }))
  assert.equal(review.complete, true)
  assert.equal(review.readySectionCount, 1)
  assert.match(review.nextAction, /ready to hear, revise, save, and export/)
  assert.equal(review.nextStep, null)
})

test('orphaned part references do not count as an audible section', () => {
  const review = demoReadiness(project({
    sections: [section('chorus', 'Chorus', 'A hook', [{ id: 'chord' }])],
    arrangementRoles: [{ sectionId: 'chorus', role: 'Harmony' }],
    musicalParts: [{ sectionId: 'chorus', noteEventIds: ['missing'] }],
    noteEvents: [],
  }))
  assert.equal(review.sections[0].hasPlayablePart, false)
  assert.equal(review.nextAction, 'Accept or create a playable part for Chorus.')
  assert.equal(review.nextStep.action, 'part')
})

test('arrangement gaps distinguish choosing a job from building its part', () => {
  const needsRole = demoReadiness(project({
    sections: [section('bridge', 'Bridge', 'A turn', [{ id: 'chord' }])],
  }))
  assert.equal(needsRole.nextStep.action, 'role')

  const needsPart = demoReadiness(project({
    sections: [section('bridge', 'Bridge', 'A turn', [{ id: 'chord' }])],
    arrangementRoles: [{ sectionId: 'bridge', role: 'Harmony' }],
  }))
  assert.equal(needsPart.nextStep.action, 'part')
})
