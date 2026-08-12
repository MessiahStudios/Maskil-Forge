import assert from 'node:assert/strict'
import test from 'node:test'
import { chordToneNames, voicingIssues } from './voicingValidation.js'

const cMajor = { root: 'C', accidental: 'Natural', quality: 'Major' }
const pitch = (letter, accidental, octave) => ({ letter, accidental, octave })

test('voicing guidance derives chord tones without assuming an octave', () => {
  assert.deepEqual(chordToneNames(cMajor), ['C', 'E', 'G'])
  assert.deepEqual(chordToneNames({ root: 'D', accidental: 'Flat', quality: 'DominantSeventh' }), ['C♯', 'F', 'G♯', 'B'])
})

test('voicing validation accepts chord tones and empty clearing', () => {
  assert.deepEqual(voicingIssues(cMajor, []), [])
  assert.deepEqual(voicingIssues(cMajor, [pitch('C', 'Natural', 3), pitch('G', 'Natural', 3), pitch('E', 'Natural', 4)]), [])
})

test('voicing validation explains chord, register, and ordering constraints', () => {
  assert.deepEqual(voicingIssues(cMajor, [pitch('D', 'Natural', 4)]), ['Use only tones from the owning chord.'])
  assert.deepEqual(voicingIssues(cMajor, [pitch('C', 'Natural', 9)]), ['Keep every voice between A0 and C8.'])
  assert.deepEqual(voicingIssues(cMajor, [pitch('E', 'Natural', 4), pitch('C', 'Natural', 4)]), ['Enter voices from low to high.'])
})
