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
  cello: { name: 'Cello', oscillatorType: 'sawtooth', attackSeconds: 0.04, releaseSeconds: 0.10, sustainLevel: 0.72, filterFrequencyHz: 1_800 },
  'acoustic-guitar': { name: 'Acoustic Guitar', oscillatorType: 'triangle', attackSeconds: 0.006, releaseSeconds: 0.12, sustainLevel: 0.35, filterFrequencyHz: 4_200 },
  piano: { name: 'Piano', oscillatorType: 'triangle', attackSeconds: 0.005, releaseSeconds: 0.18, sustainLevel: 0.28, filterFrequencyHz: 6_000 },
  'electric-bass': { name: 'Electric Bass', oscillatorType: 'square', attackSeconds: 0.01, releaseSeconds: 0.08, sustainLevel: 0.65, filterFrequencyHz: 900 },
  'drum-kit': { name: 'Drum Kit', oscillatorType: 'sine', attackSeconds: 0.002, releaseSeconds: 0.08, sustainLevel: 0, filterFrequencyHz: 1_400, pitchDrop: true },
  violin: { name: 'Violin', oscillatorType: 'sawtooth', attackSeconds: 0.03, releaseSeconds: 0.08, sustainLevel: 0.72, filterFrequencyHz: 3_200 },
  flute: { name: 'Flute', oscillatorType: 'sine', attackSeconds: 0.04, releaseSeconds: 0.06, sustainLevel: 0.78, filterFrequencyHz: 5_200 },
  clarinet: { name: 'Clarinet', oscillatorType: 'square', attackSeconds: 0.025, releaseSeconds: 0.07, sustainLevel: 0.70, filterFrequencyHz: 2_400 },
  trumpet: { name: 'Trumpet', oscillatorType: 'sawtooth', attackSeconds: 0.018, releaseSeconds: 0.06, sustainLevel: 0.72, filterFrequencyHz: 4_500 },
  'synth-pad': { name: 'Synth Pad', oscillatorType: 'triangle', attackSeconds: 0.12, releaseSeconds: 0.20, sustainLevel: 0.82, filterFrequencyHz: 2_400 },
  'synth-lead': { name: 'Synth Lead', oscillatorType: 'sawtooth', attackSeconds: 0.012, releaseSeconds: 0.05, sustainLevel: 0.78, filterFrequencyHz: 6_000 },
  'electric-guitar': { name: 'Electric Guitar', oscillatorType: 'square', attackSeconds: 0.008, releaseSeconds: 0.08, sustainLevel: 0.55, filterFrequencyHz: 3_500 },
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
