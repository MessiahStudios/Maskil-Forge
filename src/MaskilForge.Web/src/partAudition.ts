export interface ScheduledNote {
  midi: number
  startSeconds: number
  durationSeconds: number
  velocity: number
}

export interface PartAuditionResult {
  noteCount: number
  durationSeconds: number
}

export class PartAudition {
  private context: AudioContext | null = null
  private activeNodes: OscillatorNode[] = []
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
      const noteStart = startAt + note.startSeconds
      const noteEnd = noteStart + note.durationSeconds
      endAt = Math.max(endAt, noteEnd)
      const level = baseLevel * (0.45 + 0.55 * (note.velocity / 127))
      const oscillator = this.context.createOscillator()
      const gain = this.context.createGain()
      oscillator.type = 'sine'
      oscillator.frequency.value = 440 * 2 ** ((note.midi - 69) / 12)
      gain.gain.setValueAtTime(0, noteStart)
      gain.gain.linearRampToValueAtTime(level, noteStart + 0.02)
      gain.gain.setValueAtTime(level, Math.max(noteStart + 0.02, noteEnd - 0.05))
      gain.gain.linearRampToValueAtTime(0, noteEnd)
      oscillator.connect(gain).connect(this.context.destination)
      oscillator.start(noteStart)
      oscillator.stop(noteEnd + 0.01)
      this.activeNodes.push(oscillator)
    }

    const durationSeconds = endAt - startAt
    this.completionTimer = window.setTimeout(() => {
      if (generation !== this.generation) return
      this.activeNodes = []
      this.completionTimer = undefined
      onComplete()
    }, durationSeconds * 1000 + 100)
    return { noteCount: notes.length, durationSeconds }
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
