export const performanceEvidencePageSize = 12

const knownKinds = new Map([
  ['loudness.frame', { label: 'Loudness frames', order: 0 }],
  ['pitch.frame', { label: 'Pitch frames', order: 1 }],
  ['onset.event', { label: 'Onset candidates', order: 2 }],
])

function groupKey(observation) {
  return [observation.kind, observation.analyzerId, observation.analyzerVersion].join('\u001f')
}

function titleCase(value) {
  return String(value ?? '')
    .replace(/[._-]+/g, ' ')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/\b\w/g, letter => letter.toUpperCase())
}

function formatDecimal(value, digits = 1) {
  return Number(value).toFixed(digits).replace('-', '−')
}

function formatMeasurement(measurement) {
  const name = String(measurement?.name ?? '')
  const unit = String(measurement?.unit ?? '')
  const value = Number(measurement?.value)
  if (name === 'rmsDbfs') return `RMS ${formatDecimal(value)} dBFS`
  if (name === 'peakDbfs') return `peak ${formatDecimal(value)} dBFS`
  if (name === 'frequencyHertz') return `${formatDecimal(value)} Hz`
  if (name === 'strength' && unit === 'normalized') return `strength ${Math.round(value * 100)}%`
  return `${titleCase(name)} ${formatDecimal(value, 3)}${unit ? ` ${unit}` : ''}`
}

function formatTime(milliseconds) {
  return `${(Number(milliseconds) / 1000).toFixed(3)}s`
}

function formatProvenance(value) {
  if (value === 'DeterministicAnalyzer' || value === 'deterministicAnalyzer') return 'Deterministic analyzer'
  if (value === 'ImportedAnalyzer' || value === 'importedAnalyzer') return 'Imported analyzer'
  if (value === 'AudioModel' || value === 'audioModel') return 'Audio model'
  return titleCase(value)
}

function evidenceRow(observation) {
  return {
    id: observation.id,
    timeLabel: `${formatTime(observation.startMilliseconds)}–${formatTime(observation.startMilliseconds + observation.durationMilliseconds)}`,
    measurementLabel: (observation.measurements ?? []).map(formatMeasurement).join(' · '),
    confidenceLabel: observation.confidence == null
      ? 'Confidence not reported'
      : `Confidence ${Math.round(Number(observation.confidence) * 100)}%`,
  }
}

export function nextPerformanceEvidenceVisibleCount(currentCount, totalCount) {
  const current = Number.isFinite(currentCount) ? Math.max(0, Math.floor(currentCount)) : performanceEvidencePageSize
  const total = Number.isFinite(totalCount) ? Math.max(0, Math.floor(totalCount)) : 0
  return Math.min(total, current + performanceEvidencePageSize)
}

export function buildPerformanceEvidenceGroups(observations, sourceAssetId, visibleCounts = {}) {
  const grouped = new Map()
  for (const observation of observations ?? []) {
    if (observation?.sourceAssetId !== sourceAssetId) continue
    const key = groupKey(observation)
    if (!grouped.has(key)) grouped.set(key, [])
    grouped.get(key).push(observation)
  }

  return [...grouped.entries()]
    .map(([key, items]) => {
      const ordered = [...items].sort((left, right) => left.startMilliseconds - right.startMilliseconds
        || left.durationMilliseconds - right.durationMilliseconds
        || String(left.id).localeCompare(String(right.id)))
      const first = ordered[0]
      const known = knownKinds.get(first.kind)
      const requestedCount = Number(visibleCounts[key])
      const visibleCount = Math.min(
        ordered.length,
        Number.isFinite(requestedCount) && requestedCount > 0 ? Math.floor(requestedCount) : performanceEvidencePageSize,
      )

      return {
        key,
        kind: first.kind,
        label: known?.label ?? titleCase(first.kind),
        order: known?.order ?? 100,
        analyzerId: first.analyzerId,
        analyzerVersion: first.analyzerVersion,
        provenanceLabel: formatProvenance(first.provenance),
        createdUtc: ordered.reduce((latest, item) => String(item.createdUtc) > latest ? String(item.createdUtc) : latest, ''),
        count: ordered.length,
        visibleCount,
        remainingCount: ordered.length - visibleCount,
        rows: ordered.slice(0, visibleCount).map(evidenceRow),
      }
    })
    .sort((left, right) => left.order - right.order || left.label.localeCompare(right.label)
      || left.analyzerId.localeCompare(right.analyzerId))
}
