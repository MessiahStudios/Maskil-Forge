import { previewEnvelopeForDuration, previewVoiceForInstrument } from './previewRendererModel.js'

export interface PreviewScheduledNote {
  midi: number
  startSeconds: number
  durationSeconds: number
  velocity: number
  instrumentProfileId?: string | null
}

export interface PreviewVoiceHandle {
  stop: () => void
  disconnect: () => void
}

export function scheduleBuiltInPreviewVoice(
  context: AudioContext,
  note: PreviewScheduledNote,
  startAt: number,
  baseLevel: number,
): PreviewVoiceHandle {
  const voice = previewVoiceForInstrument(note.instrumentProfileId)
  const envelope = previewEnvelopeForDuration(voice, note.durationSeconds)
  const noteStart = startAt + note.startSeconds
  const noteEnd = noteStart + note.durationSeconds
  const attackEnd = noteStart + envelope.attackSeconds
  const releaseStart = noteEnd - envelope.releaseSeconds
  const level = baseLevel * (0.45 + 0.55 * (note.velocity / 127))

  const filter = context.createBiquadFilter()
  const gain = context.createGain()
  const frequency = 440 * 2 ** ((note.midi - 69) / 12)
  const partials = voice.partials?.length
    ? voice.partials
    : [{ multiplier: 1, gain: 1, oscillatorType: voice.oscillatorType }]
  const partialGainTotal = partials.reduce((sum, partial) => sum + partial.gain, 0) || 1
  filter.type = 'lowpass'
  filter.frequency.setValueAtTime(voice.filterFrequencyHz, noteStart)
  filter.Q.setValueAtTime(voice.pitchDrop ? 0.5 : 0.8, noteStart)
  gain.gain.setValueAtTime(0, noteStart)
  gain.gain.linearRampToValueAtTime(level, attackEnd)
  if (releaseStart > attackEnd)
    gain.gain.linearRampToValueAtTime(level * voice.sustainLevel, releaseStart)
  gain.gain.linearRampToValueAtTime(0, noteEnd)
  filter.connect(gain).connect(context.destination)

  const oscillators = partials.map(partial => {
    const oscillator = context.createOscillator()
    const partialGain = context.createGain()
    const partialFrequency = Math.max(20, frequency * partial.multiplier)
    oscillator.type = partial.oscillatorType ?? voice.oscillatorType
    oscillator.frequency.setValueAtTime(
      voice.pitchDrop && partial.multiplier === 1 ? Math.max(90, partialFrequency * 2) : partialFrequency,
      noteStart,
    )
    if (voice.pitchDrop && partial.multiplier === 1)
      oscillator.frequency.exponentialRampToValueAtTime(Math.max(45, partialFrequency / 2), Math.min(noteEnd, noteStart + 0.08))
    partialGain.gain.setValueAtTime(partial.gain / partialGainTotal, noteStart)
    oscillator.connect(partialGain).connect(filter)
    oscillator.start(noteStart)
    oscillator.stop(noteEnd + 0.01)
    return { oscillator, partialGain }
  })

  return {
    stop: () => {
      for (const item of oscillators) {
        try { item.oscillator.stop() } catch { /* already stopped */ }
      }
    },
    disconnect: () => {
      for (const item of oscillators) {
        item.oscillator.disconnect()
        item.partialGain.disconnect()
      }
      filter.disconnect()
      gain.disconnect()
    },
  }
}
