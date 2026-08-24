import type { PerformanceObservation, PerformanceObservationCorrection, PerformanceObservationReview, PerformanceObservationReviewVerdict } from './api'

export const performanceEvidencePageSize: 12

export interface PerformanceEvidenceCorrectionField {
  name: string
  unit: string
  value: number
  label: string
  min: string
  max: string
  step: string
}

export interface PerformanceEvidenceRow {
  id: string
  timeLabel: string
  measurementLabel: string
  confidenceLabel: string
  reviewVerdict: PerformanceObservationReviewVerdict | null
  reviewUpdatedUtc: string
  correctionLabel: string
  hasCorrection: boolean
  correctionFields: PerformanceEvidenceCorrectionField[]
}

export interface PerformanceEvidenceGroup {
  key: string
  kind: string
  label: string
  order: number
  analyzerId: string
  analyzerVersion: string
  provenanceLabel: string
  createdUtc: string
  count: number
  visibleCount: number
  remainingCount: number
  rows: PerformanceEvidenceRow[]
}

export function nextPerformanceEvidenceVisibleCount(currentCount: number | undefined, totalCount: number): number
export function buildPerformanceEvidenceGroups(
  observations: PerformanceObservation[] | null | undefined,
  sourceAssetId: string,
  visibleCounts?: Record<string, number>,
  reviews?: PerformanceObservationReview[],
  corrections?: PerformanceObservationCorrection[],
): PerformanceEvidenceGroup[]
