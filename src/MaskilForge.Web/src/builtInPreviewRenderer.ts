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

  const oscillator = context.createOscillator()
  const filter = context.createBiquadFilter()
  const gain = context.createGain()
  oscillator.type = voice.oscillatorType
  const frequency = 440 * 2 ** ((note.midi - 69) / 12)
  oscillator.frequency.setValueAtTime(voice.pitchDrop ? Math.max(90, frequency * 2) : frequency, noteStart)
  if (voice.pitchDrop)
    oscillator.frequency.exponentialRampToValueAtTime(Math.max(45, frequency / 2), Math.min(noteEnd, noteStart + 0.08))
  filter.type = 'lowpass'
  filter.frequency.setValueAtTime(voice.filterFrequencyHz, noteStart)
  filter.Q.setValueAtTime(voice.pitchDrop ? 0.5 : 0.8, noteStart)
  gain.gain.setValueAtTime(0, noteStart)
  gain.gain.linearRampToValueAtTime(level, attackEnd)
  if (releaseStart > attackEnd)
    gain.gain.linearRampToValueAtTime(level * voice.sustainLevel, releaseStart)
  gain.gain.linearRampToValueAtTime(0, noteEnd)
  oscillator.connect(filter).connect(gain).connect(context.destination)
  oscillator.start(noteStart)
  oscillator.stop(noteEnd + 0.01)

  return {
    stop: () => { try { oscillator.stop() } catch { /* already stopped */ } },
    disconnect: () => {
      oscillator.disconnect()
      filter.disconnect()
      gain.disconnect()
    },
  }
}
