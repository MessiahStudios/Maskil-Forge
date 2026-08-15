export const portableJsonMaxBytes: number
export const portablePackageMaxBytes: number
export const portablePackageContentType: string

export function isPortableProjectPackage(fileName: string, bytes?: ArrayLike<number> | null): boolean
export function portableImportLimit(isPackage: boolean): number
export function portableExportFileName(title: string, hasAssets: boolean): string
export function portableImportLimitMessage(isPackage: boolean): string
