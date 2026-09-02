import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatSoundBankSize,
  prepareSoundFontSchedule,
  soundBankKind,
  soundFontRendererId,
  soundFontRendererSummary,
  validateSoundBankFile,
} from './soundFontRendererModel.js'

test('the SoundFont renderer has a stable host identity and accepts supported bank files', () => {
  assert.equal(soundFontRendererId, 'maskil-soundfont-preview-v1')
  assert.equal(soundBankKind('Studio Bank.SF2'), '.sf2')
  assert.equal(soundBankKind('compressed.sf3'), '.sf3')
  assert.equal(soundBankKind('windows.dls'), '.dls')
  assert.equal(soundBankKind('song.mid'), null)
  assert.deepEqual(validateSoundBankFile({ name: 'empty.sf2', size: 0 }), {
    ok: false, kind: '.sf2', error: 'The selected sound bank is empty.',
  })
  assert.equal(validateSoundBankFile({ name: 'bank.sf2', size: 4_096 }).ok, true)
  assert.equal(formatSoundBankSize(3_145_728), '3.0 MB')
})

test('SoundFont scheduling uses inspectable zero-based GM channels and programs', () => {
  const notes = [
    { midi: 48, startSeconds: 0, durationSeconds: 1, velocity: 90, instrumentProfileId: 'cello', partId: 'a' },
    { midi: 36, startSeconds: 0, durationSeconds: 0.2, velocity: 110, instrumentProfileId: 'drum-kit', partId: 'b' },
    { midi: 60, startSeconds: 0.5, durationSeconds: 1, velocity: 80, instrumentProfileId: null, partId: 'c' },
  ]
  const prepared = prepareSoundFontSchedule(notes, {
    unassignedMidiChannel: 1,
    assignments: [
      { instrumentId: 'cello', midiChannel: 2 },
      { instrumentId: 'drum-kit', midiChannel: 10 },
    ],
  }, {
    assignments: [
      { instrumentId: 'cello', applicable: true, programName: 'Cello', midiProgram: 43 },
      { instrumentId: 'drum-kit', applicable: false, programName: null, midiProgram: null },
    ],
  })

  assert.deepEqual(prepared.map(note => ({
    partId: note.partId,
    channel: note.midiChannel,
    program: note.midiProgram,
    preset: note.soundFontPresetName,
  })), [
    { partId: 'a', channel: 1, program: 42, preset: 'Cello' },
    { partId: 'b', channel: 9, program: null, preset: 'Drum Kit' },
    { partId: 'c', channel: 0, program: 0, preset: 'Acoustic Grand Piano (fallback)' },
  ])
})

test('SoundFont scheduling preserves duplicate part ownership and summarizes audible presets', () => {
  const notes = [
    { midi: 60, startSeconds: 0, durationSeconds: 1, velocity: 90, instrumentProfileId: 'piano', partId: 'left' },
    { midi: 60, startSeconds: 0, durationSeconds: 1, velocity: 90, instrumentProfileId: 'piano', partId: 'right' },
  ]
  const prepared = prepareSoundFontSchedule(notes, {
    unassignedMidiChannel: 1,
    assignments: [{ instrumentId: 'piano', midiChannel: 4 }],
  }, {
    assignments: [{ instrumentId: 'piano', applicable: true, programName: 'Acoustic Grand Piano', midiProgram: 1 }],
  })
  assert.deepEqual(prepared.map(note => note.partId), ['left', 'right'])
  assert.equal(soundFontRendererSummary('My Bank.sf2', prepared),
    'Device-local SoundFont preview · My Bank.sf2 · Acoustic Grand Piano')
})
