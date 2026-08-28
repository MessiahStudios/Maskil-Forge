import assert from 'node:assert/strict'
import test from 'node:test'
import { midiExportFileName, safeExportStem } from './exportFileName.js'

test('export filenames preserve normalized Unicode across music applications', () => {
  assert.equal(safeExportStem('DAW Smoke — Cancio\u0301n 夜'), 'daw-smoke-canción-夜')
  assert.equal(midiExportFileName('DAW Smoke — Canción 夜'), 'daw-smoke-canción-夜-maskil-forge.mid')
  assert.equal(midiExportFileName('Song 🎵'), 'song-🎵-maskil-forge.mid')
})

test('export filename stems stay path-safe and usable on Windows', () => {
  assert.equal(safeExportStem('  Demo: Verse/Chorus? *Final*  '), 'demo-verse-chorus-final')
  assert.equal(safeExportStem('CON'), 'song-con')
  assert.equal(safeExportStem('  <>:"/\\|?*  '), 'song')
  assert.equal(safeExportStem('Line\u0000Break'), 'line-break')
})

test('export filename stems are bounded by scalar and UTF-8 size', () => {
  assert.equal(Array.from(safeExportStem('a'.repeat(100))).length, 80)
  assert.equal(Array.from(safeExportStem('🎵'.repeat(100))).length, 40)
})
