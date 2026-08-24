import assert from 'node:assert/strict'
import test from 'node:test'
import { creatorDestination, creatorProgress } from './creatorJourney.js'

const project = (overrides = {}) => ({ rawLyricDraft: '', sections: [], arrangement: [], arrangementRoles: [], ...overrides })

test('progress is independent from the active workspace', () => {
  assert.deepEqual(creatorProgress(project({ rawLyricDraft: 'A first thought', sections: [{ harmony: [], lyricLines: [] }] })), {
    idea: true, words: true, shape: true, music: false, harmony: false, arrangement: false,
  })
})

test('words has a distinct focusable destination from idea', () => {
  assert.deepEqual(creatorDestination('idea'), { view: 'capture', target: 'capture-title', open: false, focus: false })
  assert.deepEqual(creatorDestination('words'), { view: 'capture', target: 'raw-lyric-draft', open: false, focus: true })
})

test('music and harmony reveal their optional panels', () => {
  assert.deepEqual(creatorDestination('music'), { view: 'structure', target: 'vocal-take-studio', open: false, focus: false })
  assert.deepEqual(creatorDestination('harmony'), { view: 'structure', target: 'harmony-tools', open: true, focus: false })
  assert.deepEqual(creatorDestination('arrangement'), { view: 'structure', target: 'arrangement-blueprint', open: false, focus: false })
})

test('harmony and arrangement guide an empty song toward its first section', () => {
  assert.deepEqual(creatorDestination('harmony', false), {
    view: 'structure', target: 'song-structure', open: false, focus: false, stage: 'shape',
    message: 'Add a section first, then explore harmony.',
  })
  assert.deepEqual(creatorDestination('arrangement', false), {
    view: 'structure', target: 'song-structure', open: false, focus: false, stage: 'shape',
    message: 'Add a section first, then plan its arrangement.',
  })
})

test('harmony and arrangement require coverage across the whole song', () => {
  assert.equal(creatorProgress(project()).arrangement, false)
  const sections = [
    { id: 'verse', harmony: [{}], lyricLines: [] },
    { id: 'chorus', harmony: [], lyricLines: [] },
  ]
  assert.equal(creatorProgress(project({ sections, arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }] })).harmony, false)
  assert.equal(creatorProgress(project({ sections, arrangementRoles: [{ sectionId: 'verse', role: 'Pulse' }] })).arrangement, false)
  sections[1].harmony.push({})
  const progress = creatorProgress(project({ sections, arrangementRoles: [
    { sectionId: 'verse', role: 'Pulse' }, { sectionId: 'chorus', role: 'HookReinforcement' },
  ] }))
  assert.equal(progress.harmony, true)
  assert.equal(progress.arrangement, true)
})
