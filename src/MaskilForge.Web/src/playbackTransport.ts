import type { ScheduledNote } from './partAudition'
import { peakPolyphony } from './partAuditionModel.js'

export interface TransportTiming {
  beatsPerMinute: number
  ticksPerQuarterNote: number
}

export interface TransportResult {
  noteCount: number
  durationSeconds: number
}

export class PlaybackTransport {
  private context: AudioContext | null = null
  private activeNodes: OscillatorNode[] = []
  private completionTimer: number | undefined
  private animationFrame: number | undefined
  private generation = 0
  private startedAt = 0
  private durationSeconds = 0

  get isPlaying() {
    return this.animationFrame !== undefined || this.activeNodes.length > 0
  }

  currentSeconds() {
    if (!this.context || !this.startedAt) return 0
    return Math.min(this.durationSeconds, Math.max(0, this.context.currentTime - this.startedAt))
  }

  async play(
    notes: ScheduledNote[],
    onPosition: (seconds: number) => void,
    onComplete: () => void,
  ): Promise<TransportResult> {
    this.stop()
    if (!window.AudioContext) throw new Error('Audio preview is not available in this browser.')
    if (!notes.length) throw new Error('Create a musical part before starting playback.')
    this.context ??= new AudioContext()
    if (this.context.state === 'suspended') await this.context.resume()
    if (this.context.state !== 'running') throw new Error('Your browser paused audio. Allow sound, then try again.')

    const generation = ++this.generation
    const startAt = this.context.currentTime + 0.05
    this.startedAt = startAt
    let endAt = startAt
    const baseLevel = 0.16 / Math.sqrt(Math.max(1, peakPolyphony(notes)))
    for (const note of notes) {
      const noteStart = startAt + note.startSeconds
      const noteEnd = noteStart + note.durationSeconds
      const attackSeconds = Math.min(0.02, note.durationSeconds / 3)
      const releaseSeconds = Math.min(0.05, note.durationSeconds / 3)
      endAt = Math.max(endAt, noteEnd)
      const level = baseLevel * (0.45 + 0.55 * (note.velocity / 127))
      const oscillator = this.context.createOscillator()
      const gain = this.context.createGain()
      oscillator.type = 'sine'
      oscillator.frequency.value = 440 * 2 ** ((note.midi - 69) / 12)
      gain.gain.setValueAtTime(0, noteStart)
      gain.gain.linearRampToValueAtTime(level, noteStart + attackSeconds)
      gain.gain.setValueAtTime(level, noteEnd - releaseSeconds)
      gain.gain.linearRampToValueAtTime(0, noteEnd)
      oscillator.connect(gain).connect(this.context.destination)
      oscillator.start(noteStart)
      oscillator.stop(noteEnd + 0.01)
      this.activeNodes.push(oscillator)
    }

    this.durationSeconds = endAt - startAt
    const tick = () => {
      if (generation !== this.generation) return
      onPosition(this.currentSeconds())
      this.animationFrame = window.requestAnimationFrame(tick)
    }
    this.animationFrame = window.requestAnimationFrame(tick)
    this.completionTimer = window.setTimeout(() => {
      if (generation !== this.generation) return
      onPosition(this.durationSeconds)
      if (this.animationFrame !== undefined) window.cancelAnimationFrame(this.animationFrame)
      this.animationFrame = undefined
      this.clearPlaybackNodes()
      onComplete()
    }, this.durationSeconds * 1000 + 100)
    return { noteCount: notes.length, durationSeconds: this.durationSeconds }
  }

  stop() {
    this.generation++
    if (this.completionTimer !== undefined) window.clearTimeout(this.completionTimer)
    this.completionTimer = undefined
    if (this.animationFrame !== undefined) window.cancelAnimationFrame(this.animationFrame)
    this.animationFrame = undefined
    this.clearPlaybackNodes()
    this.startedAt = 0
    this.durationSeconds = 0
  }

  private clearPlaybackNodes() {
    for (const node of this.activeNodes) {
      try { node.stop() } catch { /* already stopped */ }
      node.disconnect()
    }
    this.activeNodes = []
  }
}
