import type { HarmonyChord } from './api'
import { positionInQuarterNotes, previewMidiNotes } from './chordAuditionModel.js'

export interface AuditionTiming {
  beatsPerMinute: number
  beatsPerBar: number
  beatUnit: number
  ticksPerQuarterNote: number
}

export interface AuditionResult {
  usedPreviewVoicings: boolean
  durationSeconds: number
}

export class ChordAudition {
  private context: AudioContext | null = null
  private activeNodes: OscillatorNode[] = []
  private completionTimer: number | undefined
  private generation = 0

  async play(chords: HarmonyChord[], timing: AuditionTiming, onComplete: () => void): Promise<AuditionResult> {
    this.stop()
    if (!window.AudioContext) throw new Error('Audio preview is not available in this browser.')
    if (!chords.length) throw new Error('Add a chord before hearing this progression.')
    this.context ??= new AudioContext()
    if (this.context.state === 'suspended') await this.context.resume()
    if (this.context.state !== 'running') throw new Error('Your browser paused audio. Allow sound, then try again.')

    const generation = ++this.generation
    const secondsPerQuarter = 60 / timing.beatsPerMinute
    const startAt = this.context.currentTime + 0.05
    let endAt = startAt
    let usedPreviewVoicings = false
    for (const chord of [...chords].sort((left, right) => positionInQuarterNotes(left, timing) - positionInQuarterNotes(right, timing))) {
      const chordStart = startAt + positionInQuarterNotes(chord, timing) * secondsPerQuarter
      const chordDuration = chord.durationBars * timing.beatsPerBar * (4 / timing.beatUnit) * secondsPerQuarter
      const chordEnd = chordStart + chordDuration
      endAt = Math.max(endAt, chordEnd)
      usedPreviewVoicings ||= !chord.voicing?.voices.length
      const notes = previewMidiNotes(chord)
      const level = 0.18 / Math.sqrt(notes.length)
      for (const midi of notes) {
        const oscillator = this.context.createOscillator()
        const gain = this.context.createGain()
        oscillator.type = 'sine'
        oscillator.frequency.value = 440 * 2 ** ((midi - 69) / 12)
        gain.gain.setValueAtTime(0, chordStart)
        gain.gain.linearRampToValueAtTime(level, chordStart + 0.025)
        gain.gain.setValueAtTime(level, Math.max(chordStart + 0.025, chordEnd - 0.06))
        gain.gain.linearRampToValueAtTime(0, chordEnd)
        oscillator.connect(gain).connect(this.context.destination)
        oscillator.start(chordStart)
        oscillator.stop(chordEnd + 0.01)
        this.activeNodes.push(oscillator)
      }
    }
    const durationSeconds = endAt - startAt
    this.completionTimer = window.setTimeout(() => {
      if (generation !== this.generation) return
      this.activeNodes = []
      this.completionTimer = undefined
      onComplete()
    }, durationSeconds * 1000 + 100)
    return { usedPreviewVoicings, durationSeconds }
  }

  stop() {
    this.generation++
    if (this.completionTimer !== undefined) window.clearTimeout(this.completionTimer)
    this.completionTimer = undefined
    for (const node of this.activeNodes) {
      try { node.stop() } catch { /* already stopped */ }
      node.disconnect()
    }
    this.activeNodes = []
  }
}
