import type { ScheduledNote } from './partAudition'
import { peakPolyphony } from './partAuditionModel.js'
import { scheduleBuiltInPreviewVoice, type PreviewVoiceHandle } from './builtInPreviewRenderer'
import type { ExternalPreviewRenderer, PreviewPlaybackHandle } from './previewPlayback'

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
  private activeVoices: PreviewVoiceHandle[] = []
  private externalPlayback: PreviewPlaybackHandle | null = null
  private completionTimer: number | undefined
  private animationFrame: number | undefined
  private generation = 0
  private startedAt = 0
  private durationSeconds = 0
  private wallClockPlayback = false

  get isPlaying() {
    return this.animationFrame !== undefined || this.activeVoices.length > 0 || this.externalPlayback !== null
  }

  currentSeconds() {
    if (!this.startedAt) return 0
    const now = this.wallClockPlayback ? performance.now() / 1_000 : this.context?.currentTime ?? 0
    return Math.min(this.durationSeconds, Math.max(0, now - this.startedAt))
  }

  async play(
    notes: ScheduledNote[],
    onPosition: (seconds: number) => void,
    onComplete: () => void,
    renderer?: ExternalPreviewRenderer,
  ): Promise<TransportResult> {
    this.stop()
    if (!notes.length) throw new Error('Create a musical part before starting playback.')

    const generation = ++this.generation
    if (renderer) {
      this.externalPlayback = await renderer.play(notes)
      this.wallClockPlayback = true
      this.startedAt = performance.now() / 1_000
      this.durationSeconds = this.externalPlayback.durationSeconds
    } else {
      if (!window.AudioContext) throw new Error('Audio preview is not available in this browser.')
      this.context ??= new AudioContext()
      if (this.context.state === 'suspended') await this.context.resume()
      if (this.context.state !== 'running') throw new Error('Your browser paused audio. Allow sound, then try again.')
      const startAt = this.context.currentTime + 0.05
      this.wallClockPlayback = false
      this.startedAt = startAt
      let endAt = startAt
      const baseLevel = 0.16 / Math.sqrt(Math.max(1, peakPolyphony(notes)))
      for (const note of notes) {
        const noteEnd = startAt + note.startSeconds + note.durationSeconds
        endAt = Math.max(endAt, noteEnd)
        this.activeVoices.push(scheduleBuiltInPreviewVoice(this.context, note, startAt, baseLevel))
      }
      this.durationSeconds = endAt - startAt
    }

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
      this.clearPlaybackVoices(false)
      this.clearExternalPlayback()
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
    this.clearPlaybackVoices(true)
    this.clearExternalPlayback()
    this.startedAt = 0
    this.durationSeconds = 0
    this.wallClockPlayback = false
  }

  private clearPlaybackVoices(stop: boolean) {
    for (const voice of this.activeVoices) {
      if (stop) voice.stop()
      voice.disconnect()
    }
    this.activeVoices = []
  }

  private clearExternalPlayback() {
    this.externalPlayback?.stop()
    this.externalPlayback = null
  }
}
