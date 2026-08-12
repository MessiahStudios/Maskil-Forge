import assert from 'node:assert/strict'
import test from 'node:test'
import { structuralRole, structuralRoles } from './structuralRoles.js'

test('structural roles provide distinct visible guidance without recommending a choice', () => {
  assert.deepEqual(structuralRoles.map(role => role.id), [
    'Unspecified', 'Setup', 'Development', 'Lift', 'Payoff', 'Contrast', 'Transition', 'Resolution',
  ])
  assert.equal(new Set(structuralRoles.map(role => role.help)).size, structuralRoles.length)
  assert.equal(structuralRole('Payoff').help, 'Deliver a primary lyrical, melodic, rhythmic, or energy peak.')
  assert.equal(structuralRole('Unknown').id, 'Unspecified')
})
