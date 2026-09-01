import { scheduleBuiltInPreviewVoice, type PreviewScheduledNote, type PreviewVoiceHandle } from './builtInPreviewRenderer'

export interface ScheduledNote extends PreviewScheduledNote {
  partId?: string
  partLabel?: string
}

export interface PartAuditionResult {
  noteCount: number
  durationSeconds: number
}

export class PartAudition {
  private context: AudioContext | null = null
  private activeVoices: PreviewVoiceHandle[] = []
  private completionTimer: number | undefined
  private generation = 0

  async play(notes: ScheduledNote[], onComplete: () => void): Promise<PartAuditionResult> {
    this.stop()
    if (!window.AudioContext) throw new Error('Audio preview is not available in this browser.')
    if (!notes.length) throw new Error('Create a musical part before hearing the assembled arrangement.')
    this.context ??= new AudioContext()
    if (this.context.state === 'suspended') await this.context.resume()
    if (this.context.state !== 'running') throw new Error('Your browser paused audio. Allow sound, then try again.')

    const generation = ++this.generation
    const startAt = this.context.currentTime + 0.05
    let endAt = startAt
    const baseLevel = 0.16 / Math.sqrt(notes.length)
    for (const note of notes) {
      const noteEnd = startAt + note.startSeconds + note.durationSeconds
      endAt = Math.max(endAt, noteEnd)
      this.activeVoices.push(scheduleBuiltInPreviewVoice(this.context, note, startAt, baseLevel))
    }

    const durationSeconds = endAt - startAt
    this.completionTimer = window.setTimeout(() => {
      if (generation !== this.generation) return
      this.clearPlaybackVoices(false)
      this.completionTimer = undefined
      onComplete()
    }, durationSeconds * 1000 + 100)
    return { noteCount: notes.length, durationSeconds }
  }

  stop() {
    this.generation++
    if (this.completionTimer !== undefined) window.clearTimeout(this.completionTimer)
    this.completionTimer = undefined
    this.clearPlaybackVoices(true)
  }

  private clearPlaybackVoices(stop: boolean) {
    for (const voice of this.activeVoices) {
      if (stop) voice.stop()
      voice.disconnect()
    }
    this.activeVoices = []
  }
}
