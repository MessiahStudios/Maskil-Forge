export const soundFontRendererId: 'maskil-soundfont-preview-v1'
export const soundFontRendererName: 'Device-local SoundFont preview'
export const acceptedSoundBankExtensions: readonly ['.sf2', '.sf3', '.dls']

export interface SoundBankFileDescriptor { name: string; size: number }
export interface SoundBankValidation { ok: boolean; kind: string | null; error: string }
export interface SoundFontChannelMap {
  unassignedMidiChannel: number
  assignments: Array<{ instrumentId: string; midiChannel: number }>
}
export interface SoundFontProgramMap {
  assignments: Array<{
    instrumentId: string
    applicable: boolean
    programName: string | null
    midiProgram: number | null
  }>
}
export interface SoundFontScheduledNote {
  midi: number
  startSeconds: number
  durationSeconds: number
  velocity: number
  instrumentProfileId?: string | null
  partId?: string
  partLabel?: string
  midiChannel: number
  midiProgram: number | null
  soundFontPresetName: string
}

export function soundBankKind(fileName: string): string | null
export function validateSoundBankFile(file: SoundBankFileDescriptor): SoundBankValidation
export function formatSoundBankSize(size: number): string
export function prepareSoundFontSchedule<T extends {
  midi: number
  startSeconds: number
  durationSeconds: number
  velocity: number
  instrumentProfileId?: string | null
}>(notes: T[], channelMap?: SoundFontChannelMap | null, programMap?: SoundFontProgramMap | null): Array<T & SoundFontScheduledNote>
export function soundFontRendererSummary(bankName: string, notes: Array<{ soundFontPresetName?: string }>): string
