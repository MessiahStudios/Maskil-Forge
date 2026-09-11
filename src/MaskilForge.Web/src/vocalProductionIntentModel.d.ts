import type { VocalProductionDescriptor, VocalProductionIntent } from './api'

export interface VocalProductionDescriptorOption {
  id: VocalProductionDescriptor
  label: string
  description: string
}

export const vocalProductionDescriptors: readonly VocalProductionDescriptorOption[]
export function vocalProductionIntentSummary(intent: VocalProductionIntent | null): string
