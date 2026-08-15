export const portableJsonMaxBytes = 10 * 1024 * 1024
export const portablePackageMaxBytes = 25 * 1024 * 1024
export const portablePackageContentType = 'application/vnd.maskil-forge.project+zip'

export function isPortableProjectPackage(fileName, bytes) {
  const name = String(fileName ?? '').toLowerCase()
  if (name.endsWith('.maskil.json') || name.endsWith('.json')) return false
  if (name.endsWith('.maskil')) return true
  return Boolean(bytes && bytes.length >= 2 && bytes[0] === 0x50 && bytes[1] === 0x4b)
}

export function portableImportLimit(isPackage) {
  return isPackage ? portablePackageMaxBytes : portableJsonMaxBytes
}

export function portableExportFileName(title, hasAssets) {
  const safeTitle = String(title ?? '').trim().replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '').toLowerCase() || 'song'
  return hasAssets ? `${safeTitle}.maskil` : `${safeTitle}.maskil.json`
}

export function portableImportLimitMessage(isPackage) {
  return isPackage
    ? 'Asset-owning project packages cannot exceed 25 MB.'
    : 'Portable project files cannot exceed 10 MB.'
}
