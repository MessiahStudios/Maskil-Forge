import assert from 'node:assert/strict'
import test from 'node:test'
import { creatorDestination, creatorProgress } from './creatorJourney.js'

const project = (overrides = {}) => ({ rawLyricDraft: '', sections: [], ...overrides })

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
  assert.deepEqual(creatorDestination('music'), { view: 'structure', target: 'musical-refinement', open: true, focus: false })
  assert.deepEqual(creatorDestination('harmony'), { view: 'structure', target: 'harmony-tools', open: true, focus: false })
  assert.equal(creatorDestination('arrangement'), null)
})
