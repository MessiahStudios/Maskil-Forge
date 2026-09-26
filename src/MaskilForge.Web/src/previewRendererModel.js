export const builtInPreviewRendererId = 'maskil-browser-preview-v1'
export const builtInPreviewRendererName = 'Built-in instrument preview'

const neutralVoice = {
  instrumentProfileId: null,
  name: 'Neutral',
  oscillatorType: 'sine',
  attackSeconds: 0.012,
  releaseSeconds: 0.06,
  sustainLevel: 0.82,
  filterFrequencyHz: 6_000,
  pitchDrop: false,
}

const voices = {
  cello: { name: 'Cello', oscillatorType: 'sine', attackSeconds: 0.05, releaseSeconds: 0.14, sustainLevel: 0.7, filterFrequencyHz: 1_600, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'sine' }, { multiplier: 2, gain: 0.22, oscillatorType: 'sawtooth' }, { multiplier: 3, gain: 0.08, oscillatorType: 'sine' }] },
  'acoustic-guitar': { name: 'Acoustic Guitar', oscillatorType: 'triangle', attackSeconds: 0.004, releaseSeconds: 0.22, sustainLevel: 0.16, filterFrequencyHz: 3_200, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'triangle' }, { multiplier: 2, gain: 0.2, oscillatorType: 'sine' }, { multiplier: 3, gain: 0.07, oscillatorType: 'sine' }] },
  piano: { name: 'Piano', oscillatorType: 'sine', attackSeconds: 0.003, releaseSeconds: 0.45, sustainLevel: 0.1, filterFrequencyHz: 4_200, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'sine' }, { multiplier: 2, gain: 0.26, oscillatorType: 'sine' }, { multiplier: 4, gain: 0.07, oscillatorType: 'triangle' }] },
  'electric-bass': { name: 'Electric Bass', oscillatorType: 'sine', attackSeconds: 0.012, releaseSeconds: 0.1, sustainLevel: 0.62, filterFrequencyHz: 700, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'sine' }, { multiplier: 2, gain: 0.14, oscillatorType: 'triangle' }] },
  'drum-kit': { name: 'Drum Kit', oscillatorType: 'sine', attackSeconds: 0.002, releaseSeconds: 0.08, sustainLevel: 0, filterFrequencyHz: 1_400, pitchDrop: true },
  violin: { name: 'Violin', oscillatorType: 'sine', attackSeconds: 0.04, releaseSeconds: 0.1, sustainLevel: 0.68, filterFrequencyHz: 2_800, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'sine' }, { multiplier: 2, gain: 0.28, oscillatorType: 'sawtooth' }, { multiplier: 4, gain: 0.06, oscillatorType: 'sine' }] },
  flute: { name: 'Flute', oscillatorType: 'sine', attackSeconds: 0.06, releaseSeconds: 0.08, sustainLevel: 0.8, filterFrequencyHz: 4_800, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'sine' }, { multiplier: 2, gain: 0.06, oscillatorType: 'sine' }] },
  clarinet: { name: 'Clarinet', oscillatorType: 'sine', attackSeconds: 0.03, releaseSeconds: 0.09, sustainLevel: 0.66, filterFrequencyHz: 2_200, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'sine' }, { multiplier: 3, gain: 0.24, oscillatorType: 'sine' }, { multiplier: 5, gain: 0.08, oscillatorType: 'sine' }] },
  trumpet: { name: 'Trumpet', oscillatorType: 'sawtooth', attackSeconds: 0.02, releaseSeconds: 0.08, sustainLevel: 0.7, filterFrequencyHz: 2_600, partials: [{ multiplier: 1, gain: 0.7, oscillatorType: 'sawtooth' }, { multiplier: 2, gain: 0.3, oscillatorType: 'sine' }, { multiplier: 3, gain: 0.12, oscillatorType: 'sine' }] },
  'synth-pad': { name: 'Synth Pad', oscillatorType: 'triangle', attackSeconds: 0.16, releaseSeconds: 0.28, sustainLevel: 0.84, filterFrequencyHz: 1_800, partials: [{ multiplier: 1, gain: 1, oscillatorType: 'triangle' }, { multiplier: 2, gain: 0.35, oscillatorType: 'sine' }] },
  'synth-lead': { name: 'Synth Lead', oscillatorType: 'sawtooth', attackSeconds: 0.01, releaseSeconds: 0.06, sustainLevel: 0.74, filterFrequencyHz: 3_400, partials: [{ multiplier: 1, gain: 0.65, oscillatorType: 'sawtooth' }, { multiplier: 2, gain: 0.2, oscillatorType: 'square' }] },
  'electric-guitar': { name: 'Electric Guitar', oscillatorType: 'sawtooth', attackSeconds: 0.006, releaseSeconds: 0.16, sustainLevel: 0.32, filterFrequencyHz: 2_400, partials: [{ multiplier: 1, gain: 0.75, oscillatorType: 'sawtooth' }, { multiplier: 2, gain: 0.22, oscillatorType: 'square' }, { multiplier: 3, gain: 0.08, oscillatorType: 'sine' }] },
}

/** Renderer-only voice choice. It never changes or becomes instrument identity. */
export function previewVoiceForInstrument(instrumentProfileId) {
  const selected = instrumentProfileId ? voices[instrumentProfileId] : null
  return selected
    ? { ...selected, instrumentProfileId, pitchDrop: selected.pitchDrop ?? false }
    : { ...neutralVoice }
}

export function previewEnvelopeForDuration(voice, durationSeconds) {
  const safeDuration = Math.max(0.01, durationSeconds)
  const attackSeconds = Math.min(voice.attackSeconds, safeDuration / 3)
  const releaseSeconds = Math.min(voice.releaseSeconds, safeDuration / 3)
  return {
    attackSeconds,
    releaseSeconds,
    sustainSeconds: Math.max(0, safeDuration - attackSeconds - releaseSeconds),
  }
}

export function previewRendererSummary(notes) {
  const names = [...new Set(notes.map(note => previewVoiceForInstrument(note.instrumentProfileId).name))]
  if (names.length === 0) return `${builtInPreviewRendererName} · no scheduled voices`
  return `${builtInPreviewRendererName} · ${names.join(', ')}`
}
