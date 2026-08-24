import assert from 'node:assert/strict'
import test from 'node:test'
import {
  buildPerformanceEvidenceGroups,
  nextPerformanceEvidenceVisibleCount,
  performanceEvidencePageSize,
} from './performanceEvidenceInspector.js'

function observation(overrides = {}) {
  return {
    id: crypto.randomUUID(),
    sourceAssetId: 'take-a',
    kind: 'pitch.frame',
    startMilliseconds: 200,
    durationMilliseconds: 80,
    measurements: [{ name: 'frequencyHertz', value: 440.25, unit: 'hertz' }],
    confidence: .82,
    analyzerId: 'maskil.browser.pitch-acf',
    analyzerVersion: '1.0.0',
    provenance: 'DeterministicAnalyzer',
    createdUtc: '2026-08-22T12:00:00Z',
    ...overrides,
  }
}

test('evidence inspector filters one source and orders known analyzer groups', () => {
  const groups = buildPerformanceEvidenceGroups([
    observation({ kind: 'onset.event', analyzerId: 'maskil.browser.onset-energy', measurements: [{ name: 'strength', value: .71, unit: 'normalized' }] }),
    observation({ sourceAssetId: 'take-b' }),
    observation({ kind: 'loudness.frame', analyzerId: 'maskil.browser.loudness', measurements: [{ name: 'rmsDbfs', value: -18.2, unit: 'dBFS' }] }),
    observation(),
  ], 'take-a')

  assert.deepEqual(groups.map(group => group.label), ['Loudness frames', 'Pitch frames', 'Onset candidates'])
  assert.equal(groups.reduce((count, group) => count + group.count, 0), 3)
})

test('rows are chronological and format time, measurements, and confidence for review', () => {
  const groups = buildPerformanceEvidenceGroups([
    observation({ id: 'later', startMilliseconds: 400, confidence: null }),
    observation({ id: 'earlier', startMilliseconds: 0, measurements: [{ name: 'frequencyHertz', value: 220, unit: 'hertz' }] }),
  ], 'take-a')

  assert.deepEqual(groups[0].rows, [
    { id: 'earlier', timeLabel: '0.000s–0.080s', measurementLabel: '220.0 Hz', confidenceLabel: 'Confidence 82%', reviewVerdict: null, reviewUpdatedUtc: '', correctionLabel: '', hasCorrection: false, correctionFields: [], canPromote: false, hasGesture: false, gestureLabel: '' },
    { id: 'later', timeLabel: '0.400s–0.480s', measurementLabel: '440.3 Hz', confidenceLabel: 'Confidence not reported', reviewVerdict: null, reviewUpdatedUtc: '', correctionLabel: '', hasCorrection: false, correctionFields: [], canPromote: false, hasGesture: false, gestureLabel: '' },
  ])
})

test('artist verdicts decorate matching claims without changing analyzer evidence', () => {
  const source = [observation({ id: 'reviewed' }), observation({ id: 'unreviewed', startMilliseconds: 400 })]
  const snapshot = structuredClone(source)
  const groups = buildPerformanceEvidenceGroups(source, 'take-a', {}, [{
    id: 'review-1',
    observationId: 'reviewed',
    verdict: 'Accurate',
    createdUtc: '2026-08-22T12:01:00Z',
    updatedUtc: '2026-08-22T12:02:00Z',
  }])

  assert.equal(groups[0].rows[0].reviewVerdict, 'Accurate')
  assert.equal(groups[0].rows[0].reviewUpdatedUtc, '2026-08-22T12:02:00Z')
  assert.equal(groups[0].rows[1].reviewVerdict, null)
  assert.equal(groups[0].rows[0].hasCorrection, false)
  assert.equal(groups[0].rows[0].canPromote, true)
  assert.equal(groups[0].rows[0].hasGesture, false)
  assert.deepEqual(source, snapshot)
})

test('inaccurate claims expose a correction form without rewriting analyzer measurements', () => {
  const source = [observation({ id: 'corrected', measurements: [{ name: 'frequencyHertz', value: 440.25, unit: 'hertz' }] })]
  const snapshot = structuredClone(source)
  const groups = buildPerformanceEvidenceGroups(source, 'take-a', {}, [{
    id: 'review-1',
    observationId: 'corrected',
    verdict: 'Inaccurate',
    createdUtc: '2026-08-23T12:01:00Z',
    updatedUtc: '2026-08-23T12:02:00Z',
  }], [{
    id: 'correction-1',
    observationId: 'corrected',
    measurements: [{ name: 'frequencyHertz', value: 196.2, unit: 'hertz' }],
    createdUtc: '2026-08-23T12:03:00Z',
    updatedUtc: '2026-08-23T12:04:00Z',
  }])

  assert.equal(groups[0].rows[0].measurementLabel, '440.3 Hz')
  assert.equal(groups[0].rows[0].reviewVerdict, 'Inaccurate')
  assert.equal(groups[0].rows[0].hasCorrection, true)
  assert.equal(groups[0].rows[0].correctionLabel, 'Artist correction · 196.2 Hz')
  assert.equal(groups[0].rows[0].canPromote, true)
  assert.equal(groups[0].rows[0].hasGesture, false)
  assert.deepEqual(groups[0].rows[0].correctionFields, [{
    name: 'frequencyHertz',
    unit: 'hertz',
    value: 196.2,
    label: 'Frequency Hz',
    min: '65',
    max: '1000',
    step: '0.1',
  }])
  assert.deepEqual(source, snapshot)
})

test('eligible claims can promote a gesture snapshot without rewriting analyzer measurements', () => {
  const source = [
    observation({ id: 'accurate', measurements: [{ name: 'frequencyHertz', value: 440.25, unit: 'hertz' }] }),
    observation({ id: 'inaccurate', startMilliseconds: 400, measurements: [{ name: 'frequencyHertz', value: 440.25, unit: 'hertz' }] }),
  ]
  const snapshot = structuredClone(source)
  const groups = buildPerformanceEvidenceGroups(source, 'take-a', {}, [
    { id: 'review-accurate', observationId: 'accurate', verdict: 'Accurate', createdUtc: '2026-08-23T12:01:00Z', updatedUtc: '2026-08-23T12:02:00Z' },
    { id: 'review-inaccurate', observationId: 'inaccurate', verdict: 'Inaccurate', createdUtc: '2026-08-23T12:01:00Z', updatedUtc: '2026-08-23T12:02:00Z' },
  ], [
    { id: 'correction-1', observationId: 'inaccurate', measurements: [{ name: 'frequencyHertz', value: 196.2, unit: 'hertz' }], createdUtc: '2026-08-23T12:03:00Z', updatedUtc: '2026-08-23T12:04:00Z' },
  ], [
    { id: 'gesture-1', observationId: 'accurate', measurements: [{ name: 'frequencyHertz', value: 440.25, unit: 'hertz' }], createdUtc: '2026-08-23T12:05:00Z', updatedUtc: '2026-08-23T12:06:00Z' },
  ])

  assert.equal(groups[0].rows[0].measurementLabel, '440.3 Hz')
  assert.equal(groups[0].rows[0].canPromote, true)
  assert.equal(groups[0].rows[0].hasGesture, true)
  assert.equal(groups[0].rows[0].gestureLabel, 'Artist gesture · 440.3 Hz')
  assert.equal(groups[0].rows[1].canPromote, true)
  assert.equal(groups[0].rows[1].hasGesture, false)
  assert.equal(groups[0].rows[1].gestureLabel, '')
  assert.deepEqual(source, snapshot)
})

test('unreviewed and uncorrected inaccurate claims cannot promote a gesture', () => {
  const groups = buildPerformanceEvidenceGroups([
    observation({ id: 'unreviewed' }),
    observation({ id: 'inaccurate', startMilliseconds: 400 }),
  ], 'take-a', {}, [{
    id: 'review-1',
    observationId: 'inaccurate',
    verdict: 'Inaccurate',
    createdUtc: '2026-08-23T12:01:00Z',
    updatedUtc: '2026-08-23T12:02:00Z',
  }])

  assert.equal(groups[0].rows[0].canPromote, false)
  assert.equal(groups[0].rows[1].canPromote, false)
})

test('loudness and onset evidence use compact human-readable measurements', () => {
  const groups = buildPerformanceEvidenceGroups([
    observation({ kind: 'loudness.frame', analyzerId: 'maskil.browser.loudness', measurements: [
      { name: 'rmsDbfs', value: -18.25, unit: 'dBFS' },
      { name: 'peakDbfs', value: -4.04, unit: 'dBFS' },
    ], confidence: null }),
    observation({ kind: 'onset.event', analyzerId: 'maskil.browser.onset-energy', measurements: [{ name: 'strength', value: .707, unit: 'normalized' }] }),
  ], 'take-a')

  assert.equal(groups[0].rows[0].measurementLabel, 'RMS −18.3 dBFS · peak −4.0 dBFS')
  assert.equal(groups[1].rows[0].measurementLabel, 'strength 71%')
})

test('large reports reveal one bounded page at a time', () => {
  const observations = Array.from({ length: 29 }, (_, index) => observation({ id: String(index), startMilliseconds: index * 200 }))
  const first = buildPerformanceEvidenceGroups(observations, 'take-a')[0]
  assert.equal(first.rows.length, performanceEvidencePageSize)
  assert.equal(first.remainingCount, 17)

  const nextCount = nextPerformanceEvidenceVisibleCount(first.visibleCount, first.count)
  const second = buildPerformanceEvidenceGroups(observations, 'take-a', { [first.key]: nextCount })[0]
  assert.equal(second.rows.length, 24)
  assert.equal(nextPerformanceEvidenceVisibleCount(second.visibleCount, second.count), 29)
})

test('extensible unknown evidence remains visible without mutating input', () => {
  const source = [observation({
    kind: 'spectral.centroid',
    analyzerId: 'future.analyzer',
    analyzerVersion: '2.1.0',
    provenance: 'ImportedAnalyzer',
    measurements: [{ name: 'centroidHertz', value: 1234.5678, unit: 'hertz' }],
  })]
  const snapshot = structuredClone(source)
  const group = buildPerformanceEvidenceGroups(source, 'take-a')[0]

  assert.equal(group.label, 'Spectral Centroid')
  assert.equal(group.provenanceLabel, 'Imported analyzer')
  assert.equal(group.rows[0].measurementLabel, 'Centroid Hertz 1234.568 hertz')
  assert.deepEqual(source, snapshot)
})
