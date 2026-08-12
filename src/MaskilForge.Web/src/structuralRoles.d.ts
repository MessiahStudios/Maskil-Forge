import type { StructuralFunction } from './api'

export interface StructuralRolePresentation {
  id: StructuralFunction
  label: string
  help: string
}

export const structuralRoles: StructuralRolePresentation[]
export function structuralRole(id: StructuralFunction): StructuralRolePresentation
