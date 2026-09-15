import type { Vst3DiscoveryResult } from './api'

export type ArchitectureFilter = 'all' | 'match' | 'different' | 'undetermined'
type Candidate = Vst3DiscoveryResult['locations'][number]['candidates'][number]
export function candidateArchitecture(candidate: Candidate): Exclude<ArchitectureFilter, 'all'>
export function reviewPluginInventory(result: Vst3DiscoveryResult | null, filters?: {
  query?: string; architecture?: ArchitectureFilter; repeatedOnly?: boolean
}): {
  totalCount: number
  visibleCount: number
  locations: Array<Vst3DiscoveryResult['locations'][number] & {
    totalCount: number
    rows: Array<{ key: string; candidate: Candidate; architecture: Exclude<ArchitectureFilter, 'all'>; repeatedClassCount: number }>
  }>
  repeatedClasses: Array<{ id: string; occurrences: Array<{ key: string; location: string; path: string; name: string; category: string; version: string | null }> }>
}
