const maximumStemScalarCount = 80
const maximumStemUtf8Bytes = 160
const utf8 = new TextEncoder()
const windowsReservedStem = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/iu

function boundedStem(value) {
  let result = ''
  let scalarCount = 0
  let byteCount = 0
  for (const scalar of value) {
    const scalarBytes = utf8.encode(scalar).length
    if (scalarCount === maximumStemScalarCount || byteCount + scalarBytes > maximumStemUtf8Bytes) break
    result += scalar
    scalarCount++
    byteCount += scalarBytes
  }
  return result.replace(/-+$/gu, '')
}

export function safeExportStem(title) {
  const normalized = String(title ?? '')
    .trim()
    .toLowerCase()
    .normalize('NFC')
    .replace(/[\p{Cc}\p{Cf}]+/gu, '-')
    .replace(/[<>:"/\\|?*]+/gu, '-')
    .replace(/[\p{Z}\p{P}]+/gu, '-')
    .replace(/-+/gu, '-')
    .replace(/^-+|-+$/gu, '')
  const stem = boundedStem(normalized) || 'song'
  return windowsReservedStem.test(stem) ? `song-${stem}` : stem
}

export function midiExportFileName(title) {
  return `${safeExportStem(title)}-maskil-forge.mid`
}
