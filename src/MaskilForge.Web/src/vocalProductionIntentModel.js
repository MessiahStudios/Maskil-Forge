export const vocalProductionDescriptors = Object.freeze([
  { id: 'Clean', label: 'Clean', description: 'Clear and natural, with distractions kept out of the way.' },
  { id: 'Warm', label: 'Warm', description: 'Full and inviting without hiding the words.' },
  { id: 'Intimate', label: 'Intimate', description: 'Close, personal, and emotionally near.' },
  { id: 'Forward', label: 'Forward', description: 'Present and confidently in front of the arrangement.' },
  { id: 'SoftRock', label: 'Soft Rock', description: 'Polished and controlled while retaining band energy.' },
  { id: 'Cinematic', label: 'Cinematic', description: 'Wide, dramatic, and suited to a larger emotional frame.' },
  { id: 'Aggressive', label: 'Aggressive', description: 'Urgent and forceful without replacing the performance.' },
])

export function vocalProductionIntentSummary(intent) {
  if (!intent) return 'No vocal direction chosen yet.'
  const labels = intent.descriptors.map(id => vocalProductionDescriptors.find(item => item.id === id)?.label ?? id)
  return labels.join(' · ')
}
