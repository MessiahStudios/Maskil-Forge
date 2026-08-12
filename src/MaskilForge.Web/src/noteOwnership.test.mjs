import assert from 'node:assert/strict'
import test from 'node:test'
import { noteOwners, noteRemovalGuidance } from './noteOwnership.js'

const parts = [
  { id: 'p1', label: 'Verse foundation', noteEventIds: ['n1', 'n2'] },
  { id: 'p2', label: 'Hook doubles', noteEventIds: ['n2'] },
]

test('note ownership identifies every musical part that protects a note', () => {
  assert.deepEqual(noteOwners(parts, 'n2').map(part => part.id), ['p1', 'p2'])
  assert.deepEqual(noteOwners(parts, 'n3'), [])
})

test('note removal guidance names owning parts and the required action', () => {
  assert.equal(noteRemovalGuidance(parts, 'n1'), 'Used by Verse foundation. Remove the note from that part first.')
  assert.equal(noteRemovalGuidance(parts, 'n2'), 'Used by Verse foundation, Hook doubles. Remove the note from those parts first.')
  assert.equal(noteRemovalGuidance(parts, 'n3'), '')
})
