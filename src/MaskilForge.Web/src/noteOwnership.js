export function noteOwners(musicalParts, noteEventId) {
  return musicalParts.filter(part => part.noteEventIds.includes(noteEventId))
}

export function noteRemovalGuidance(musicalParts, noteEventId) {
  const owners = noteOwners(musicalParts, noteEventId)
  if (!owners.length) return ''
  const labels = owners.map(part => part.label).join(', ')
  return `Used by ${labels}. Remove the note from ${owners.length === 1 ? 'that part' : 'those parts'} first.`
}
