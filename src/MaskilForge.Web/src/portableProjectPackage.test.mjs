import assert from 'node:assert/strict'
import test from 'node:test'
import {
  isPortableProjectPackage,
  portableExportFileName,
  portableImportLimit,
  portableImportLimitMessage,
  portableJsonMaxBytes,
  portablePackageMaxBytes,
} from './portableProjectPackage.js'

test('package detection prefers the .maskil extension and zip signature', () => {
  assert.equal(isPortableProjectPackage('hallway.maskil.json'), false)
  assert.equal(isPortableProjectPackage('hallway.json'), false)
  assert.equal(isPortableProjectPackage('hallway.maskil'), true)
  assert.equal(isPortableProjectPackage('untitled', [0x50, 0x4b, 0x03, 0x04]), true)
  assert.equal(isPortableProjectPackage('untitled', [0x7b, 0x0a]), false)
})

test('export names and import limits distinguish json documents from asset packages', () => {
  assert.equal(portableExportFileName('Hallway Light', false), 'hallway-light.maskil.json')
  assert.equal(portableExportFileName('Hallway Light', true), 'hallway-light.maskil')
  assert.equal(portableExportFileName('Canción 夜', false), 'canción-夜.maskil.json')
  assert.equal(portableExportFileName('Canción 夜', true), 'canción-夜.maskil')
  assert.equal(portableImportLimit(false), portableJsonMaxBytes)
  assert.equal(portableImportLimit(true), portablePackageMaxBytes)
  assert.match(portableImportLimitMessage(true), /25 MB/)
  assert.match(portableImportLimitMessage(false), /10 MB/)
})
