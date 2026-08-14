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
  assert.equal(review.sections[0].needsSourceNotes, false)
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
  assert.equal(needsPart.sections[0].needsSourceNotes, false)
})

const timeline = (placements) => ({
  ticksPerQuarterNote: 480,
  tempoMap: { events: [{ beat: 0, beatsPerMinute: 120 }] },
  timeSignatureMap: { events: [{ beat: 0, numerator: 4, denominator: 4 }] },
  sectionPlacements: placements,
})

test('note-dependent jobs without in-section notes ask for a harmony sketch', () => {
  const review = demoReadiness(project({
    sections: [section('verse', 'Verse', 'A line', [{ id: 'chord' }])],
    arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }],
    timeline: timeline([{ sectionId: 'verse', start: { bar: 1, beat: 1, tick: 0 }, durationBars: 8 }]),
  }))
  assert.equal(review.sections[0].needsSourceNotes, true)
  assert.equal(review.nextAction, 'Turn Verse harmony into playable notes.')
  assert.deepEqual(review.nextStep, {
    sectionId: 'verse',
    stage: 'harmony',
    action: 'sketch',
    label: 'Prepare Verse notes',
  })
})

test('note-dependent jobs keep the part action once the section has notes', () => {
  const review = demoReadiness(project({
    sections: [section('verse', 'Verse', 'A line', [{ id: 'chord' }])],
    arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }],
    noteEvents: [{ id: 'note', startTick: 0 }],
    timeline: timeline([{ sectionId: 'verse', start: { bar: 1, beat: 1, tick: 0 }, durationBars: 8 }]),
  }))
  assert.equal(review.sections[0].needsSourceNotes, false)
  assert.equal(review.nextStep.action, 'part')
})

test('notes from another section do not satisfy a note-dependent job', () => {
  const review = demoReadiness(project({
    sections: [
      section('verse', 'Verse', 'A line', [{ id: 'chord' }]),
      section('chorus', 'Chorus', 'A hook', [{ id: 'chord' }]),
    ],
    arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }],
    noteEvents: [{ id: 'chorus-note', startTick: 15360 }],
    timeline: timeline([
      { sectionId: 'verse', start: { bar: 1, beat: 1, tick: 0 }, durationBars: 8 },
      { sectionId: 'chorus', start: { bar: 9, beat: 1, tick: 0 }, durationBars: 8 },
    ]),
  }))
  assert.equal(review.sections[0].needsSourceNotes, true)
  assert.equal(review.nextStep.action, 'sketch')
})

test('harmony-support and texture can build a part from chords without prior notes', () => {
  const texture = demoReadiness(project({
    sections: [section('bridge', 'Bridge', 'A turn', [{ id: 'chord' }])],
    arrangementRoles: [{ sectionId: 'bridge', role: 'Texture' }],
  }))
  assert.equal(texture.sections[0].needsSourceNotes, false)
  assert.equal(texture.nextStep.action, 'part')
})
