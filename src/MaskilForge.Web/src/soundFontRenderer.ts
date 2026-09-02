import { WorkletSynthesizer } from 'spessasynth_lib'
import type { PreviewScheduledNote } from './builtInPreviewRenderer'
import type { PreviewPlaybackHandle } from './previewPlayback'
import { validateSoundBankFile } from './soundFontRendererModel.js'

interface SoundFontPlaybackNote extends PreviewScheduledNote {
  midiChannel?: number
  midiProgram?: number | null
}

export interface LoadedSoundBank {
  name: string
  size: number
  kind: string
}

const workletPath = '/spessasynth_processor.min.js'
const soundBankId = 'maskil-user-sound-bank'

export class SoundFontRenderer {
  private context: AudioContext | null = null
  private synth: WorkletSynthesizer | null = null
  private activeStop: (() => void) | null = null
  private loadedBank: LoadedSoundBank | null = null

  get bank() { return this.loadedBank }
  get isLoaded() { return this.synth !== null && this.context !== null && this.loadedBank !== null }

  async load(file: File): Promise<LoadedSoundBank> {
    const validation = validateSoundBankFile(file)
    if (!validation.ok || !validation.kind) throw new Error(validation.error)
    if (!window.AudioContext || !window.AudioWorkletNode)
      throw new Error('SoundFont preview needs AudioWorklet support in this browser.')

    const soundBankBytes = await file.arrayBuffer()
    const context = new AudioContext()
    let synth: WorkletSynthesizer | null = null
    try {
      await context.audioWorklet.addModule(workletPath)
      synth = new WorkletSynthesizer(context)
      synth.connect(context.destination)
      await synth.isReady
      await synth.soundBankManager.addSoundBank(soundBankBytes, soundBankId)
      synth.setLogLevel(false, true, false)
    } catch (error) {
      synth?.destroy()
      await context.close().catch(() => undefined)
      throw new Error(error instanceof Error
        ? `The sound bank could not be loaded: ${error.message}`
        : 'The sound bank could not be loaded.')
    }

    await this.unload()
    this.context = context
    this.synth = synth
    this.loadedBank = { name: file.name, size: file.size, kind: validation.kind }
    return this.loadedBank
  }

  async play(notes: SoundFontPlaybackNote[]): Promise<PreviewPlaybackHandle> {
    const context = this.context
    const synth = this.synth
    if (!context || !synth || !this.loadedBank)
      throw new Error('Load a SoundFont or DLS bank before choosing SoundFont preview.')
    if (!notes.length) throw new Error('Create a musical part before starting playback.')
    this.stop()
    if (context.state === 'suspended') await context.resume()
    if (context.state !== 'running') throw new Error('Your browser paused audio. Allow sound, then try again.')

    const timers: number[] = []
    let stopped = false
    const channelPrograms = new Map<number, number>()
    for (const note of notes) {
      const channel = note.midiChannel ?? 0
      if (note.midiProgram != null) channelPrograms.set(channel, note.midiProgram)
    }
    for (const [channel, program] of channelPrograms) synth.programChange(channel, program)

    const later = (seconds: number, callback: () => void) => {
      const timer = window.setTimeout(() => { if (!stopped) callback() }, Math.max(0, seconds * 1_000))
      timers.push(timer)
    }
    for (const note of notes) {
      const channel = note.midiChannel ?? 0
      const midi = Math.max(0, Math.min(127, Math.round(note.midi)))
      const velocity = Math.max(1, Math.min(127, Math.round(note.velocity)))
      later(note.startSeconds, () => synth.noteOn(channel, midi, velocity))
      later(note.startSeconds + note.durationSeconds, () => synth.noteOff(channel, midi))
    }

    const durationSeconds = Math.max(...notes.map(note => note.startSeconds + note.durationSeconds))
    const stop = () => {
      if (stopped) return
      stopped = true
      timers.forEach(timer => window.clearTimeout(timer))
      synth.stopAll(true)
      if (this.activeStop === stop) this.activeStop = null
    }
    this.activeStop = stop
    return { durationSeconds, stop }
  }

  stop() {
    this.activeStop?.()
    this.activeStop = null
  }

  async unload() {
    this.stop()
    const context = this.context
    this.synth?.destroy()
    this.synth = null
    this.context = null
    this.loadedBank = null
    if (context && context.state !== 'closed') await context.close().catch(() => undefined)
  }
}
