export const structuralRoles = [
  { id: 'Unspecified', label: 'Not decided', help: 'Leave this section’s larger song job open.' },
  { id: 'Setup', label: 'Setup', help: 'Establish the world, premise, groove, or musical language.' },
  { id: 'Development', label: 'Development', help: 'Advance the story, idea, harmony, or musical material.' },
  { id: 'Lift', label: 'Lift', help: 'Increase anticipation or momentum toward a payoff.' },
  { id: 'Payoff', label: 'Payoff', help: 'Deliver a primary lyrical, melodic, rhythmic, or energy peak.' },
  { id: 'Contrast', label: 'Contrast', help: 'Create meaningful difference from surrounding material.' },
  { id: 'Transition', label: 'Transition', help: 'Move the listener between larger song states.' },
  { id: 'Resolution', label: 'Resolution', help: 'Settle, release, conclude, or reframe the song.' },
]

export function structuralRole(id) {
  return structuralRoles.find(role => role.id === id) ?? structuralRoles[0]
}
