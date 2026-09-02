export const soundFontRendererId = 'maskil-soundfont-preview-v1'
export const soundFontRendererName = 'Device-local SoundFont preview'
export const acceptedSoundBankExtensions = ['.sf2', '.sf3', '.dls']

export function soundBankKind(fileName) {
  const normalized = String(fileName ?? '').trim().toLowerCase()
  return acceptedSoundBankExtensions.find(extension => normalized.endsWith(extension)) ?? null
}

export function validateSoundBankFile(file) {
  const kind = soundBankKind(file?.name)
  if (!kind) return { ok: false, kind: null, error: 'Choose an SF2, SF3, or DLS sound bank.' }
  if (!Number.isFinite(file?.size) || file.size <= 0)
    return { ok: false, kind, error: 'The selected sound bank is empty.' }
  return { ok: true, kind, error: '' }
}

export function formatSoundBankSize(size) {
  if (size < 1_024) return `${size} B`
  if (size < 1_048_576) return `${(size / 1_024).toFixed(1)} KB`
  return `${(size / 1_048_576).toFixed(1)} MB`
}

/**
 * Add renderer-only GM channel and preset choices to scheduled Song Graph
 * notes. Musician-facing API values are one-based; the synth receives zero-
 * based values. Missing and future instruments use GM Acoustic Grand Piano on
 * the unassigned channel without changing the stored part.
 */
export function prepareSoundFontSchedule(notes, channelMap, programMap) {
  const channels = new Map((channelMap?.assignments ?? []).map(item => [item.instrumentId, item.midiChannel]))
  const programs = new Map((programMap?.assignments ?? []).map(item => [item.instrumentId, item]))
  const unassignedChannel = Math.max(0, (channelMap?.unassignedMidiChannel ?? 1) - 1)

  return notes.map(note => {
    const instrumentId = note.instrumentProfileId ?? null
    const channel = instrumentId && channels.has(instrumentId)
      ? Math.max(0, channels.get(instrumentId) - 1)
      : unassignedChannel
    const assignment = instrumentId ? programs.get(instrumentId) : null
    const isDrumKit = instrumentId === 'drum-kit'
    const midiProgram = !isDrumKit && assignment?.applicable && assignment.midiProgram != null
      ? Math.max(0, assignment.midiProgram - 1)
      : isDrumKit ? null : 0
    const presetName = isDrumKit
      ? 'Drum Kit'
      : assignment?.applicable && assignment.programName
        ? assignment.programName
        : 'Acoustic Grand Piano (fallback)'
    return { ...note, midiChannel: channel, midiProgram, soundFontPresetName: presetName }
  })
}

export function soundFontRendererSummary(bankName, notes) {
  const presets = [...new Set(notes.map(note => note.soundFontPresetName).filter(Boolean))]
  const presetCopy = presets.length ? presets.join(', ') : 'no scheduled presets'
  return `${soundFontRendererName} · ${bankName} · ${presetCopy}`
}
