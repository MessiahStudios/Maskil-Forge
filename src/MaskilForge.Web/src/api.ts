export type SectionKind = 'Intro' | 'Verse' | 'Chorus' | 'PreChorus' | 'Bridge' | 'Outro'
export type SectionDelivery = 'Sung' | 'TalkSung' | 'Spoken' | 'Whispered'
export type StructuralFunction = 'Unspecified' | 'Setup' | 'Development' | 'Lift' | 'Payoff' | 'Contrast' | 'Transition' | 'Resolution'
export type SongGenre = 'Unspecified' | 'Pop' | 'Rock' | 'Folk' | 'Country' | 'RAndB' | 'HipHop' | 'Electronic' | 'Cinematic' | 'Alternative' | 'Other'
export type SyllableSource = 'Manual' | 'Analyzer' | 'Imported'
export type PhraseSource = 'Default' | 'Manual' | 'Analyzer' | 'Imported'
export type StressLevel = 'None' | 'Secondary' | 'Primary' | 'Emphasized'
export type StressProvenance = 'Manual' | 'Analyzer' | 'Imported'
export type ProsodicWeight = 'Weak' | 'Neutral' | 'Strong'
export type ProsodyProvenance = 'Manual' | 'Analyzer' | 'Imported'
export type PlacementProvenance = 'Manual' | 'Analyzer' | 'Imported'
export type RhythmCandidateProvenance = 'Manual' | 'Analyzer' | 'Imported'
export type BreathProvenance = 'Manual' | 'Analyzer' | 'Imported'
export type NoteLetter = 'C' | 'D' | 'E' | 'F' | 'G' | 'A' | 'B'
export type Accidental = 'Natural' | 'Sharp' | 'Flat'
export type ScaleMode = 'Major' | 'NaturalMinor'
export type ChordQuality = 'Major' | 'Minor' | 'Diminished' | 'Augmented' | 'DominantSeventh'
export type HarmonyProvenance = 'Manual' | 'Analyzer' | 'Imported'
export type SectionEnergy = 'Intimate' | 'Gentle' | 'Building' | 'Strong' | 'Peak'
export type SectionDensity = 'Sparse' | 'Light' | 'Balanced' | 'Full' | 'Dense'
export type ArrangementProvenance = 'Manual' | 'Analyzer' | 'Imported'
export type ArrangementRole = 'Foundation' | 'Pulse' | 'Harmony' | 'LowEndSupport' | 'Texture' | 'Accent' | 'Transition' | 'Countermelody' | 'HookReinforcement'

export interface SectionArrangement {
  id: string
  sectionId: string
  energy: SectionEnergy
  density: SectionDensity
  provenance: ArrangementProvenance
}

export interface SectionRoleAssignment {
  id: string
  sectionId: string
  role: ArrangementRole
  provenance: ArrangementProvenance
}

export interface MusicalPart {
  id: string
  sectionId: string
  role: ArrangementRole
  label: string
  noteEventIds: string[]
  provenance: ArrangementProvenance
}

export interface MusicalKey {
  tonic: NoteLetter
  accidental: Accidental
  mode: ScaleMode
}

export interface ChordSymbol {
  root: NoteLetter
  accidental: Accidental
  quality: ChordQuality
}

export interface HarmonyChord {
  id: string
  chord: ChordSymbol
  start: BeatPosition
  durationBars: number
  provenance: HarmonyProvenance
  voicing: ChordVoicing | null
}

export interface RegisteredPitch { letter: NoteLetter; accidental: Accidental; octave: number }
export interface NoteEvent { id: string; pitch: RegisteredPitch; startTick: number; durationTicks: number; velocity: number }
export interface HarmonyNoteSketchEvent extends Omit<NoteEvent, 'id'> { usesPreviewVoicing: boolean }
export interface HarmonyNoteSketch { sectionId: string; events: HarmonyNoteSketchEvent[]; usesPreviewVoicings: boolean }
export interface LowEndSupportProposalEvent extends Omit<NoteEvent, 'id'> { sourceNoteEventId: string; existingNoteEventId: string | null }
export interface LowEndSupportProposal { sectionId: string; partLabel: string; events: LowEndSupportProposalEvent[]; reusedNoteCount: number }
export interface PulseProposalEvent extends Omit<NoteEvent, 'id'> { sourceNoteEventId: string; existingNoteEventId: string | null }
export interface PulseProposal { sectionId: string; partLabel: string; events: PulseProposalEvent[]; reusedNoteCount: number }
export interface HarmonySupportProposalEvent extends Omit<NoteEvent, 'id'> { usesPreviewVoicing: boolean; existingNoteEventId: string | null }
export interface HarmonySupportProposal { sectionId: string; partLabel: string; events: HarmonySupportProposalEvent[]; reusedNoteCount: number; usesPreviewVoicings: boolean }
export interface TextureProposalEvent extends Omit<NoteEvent, 'id'> { usesPreviewVoicing: boolean; existingNoteEventId: string | null }
export interface TextureProposal { sectionId: string; partLabel: string; events: TextureProposalEvent[]; reusedNoteCount: number; usesPreviewVoicings: boolean }
export interface HookReinforcementProposalEvent extends Omit<NoteEvent, 'id'> { sourceNoteEventId: string; existingNoteEventId: string | null }
export interface HookReinforcementProposal { sectionId: string; partLabel: string; events: HookReinforcementProposalEvent[]; reusedNoteCount: number }
export interface CountermelodyProposalEvent extends Omit<NoteEvent, 'id'> { sourceNoteEventId: string; existingNoteEventId: string | null }
export interface CountermelodyProposal { sectionId: string; partLabel: string; events: CountermelodyProposalEvent[]; reusedNoteCount: number }
export interface AccentProposalEvent extends Omit<NoteEvent, 'id'> { sourceNoteEventId: string; existingNoteEventId: string | null }
export interface AccentProposal { sectionId: string; partLabel: string; events: AccentProposalEvent[]; reusedNoteCount: number }
export interface ChordVoice { id: string; position: number; pitch: RegisteredPitch; provenance: HarmonyProvenance }
export interface ChordVoicing { id: string; minimumMidiNote: number; maximumMidiNote: number; voices: ChordVoice[] }

export interface HarmonyCandidateEvent {
  id: string
  position: number
  chord: ChordSymbol
  start: BeatPosition
  durationBars: number
}

export interface HarmonyCandidate {
  id: string
  label: string
  provenance: HarmonyProvenance
  events: HarmonyCandidateEvent[]
}

export type VoiceLeadingMotion = 'Smooth' | 'Moderate' | 'Wide'
export type VoiceLeadingFindingKind = 'RetainedVoice' | 'WideLeap' | 'WideSpacing' | 'ParallelPerfectInterval' | 'VoiceCountChange'
export type VoiceLeadingFindingSeverity = 'Info' | 'Warning'

export interface VoiceLeadingFinding {
  kind: VoiceLeadingFindingKind
  severity: VoiceLeadingFindingSeverity
  message: string
  fromVoicePosition: number | null
  toVoicePosition: number | null
}

export interface VoiceLeadingTransition {
  fromChordId: string
  toChordId: string
  commonToneCount: number
  averageNearestMotionSemitones: number
  rootMotionSemitones: number
  motion: VoiceLeadingMotion
  usesRegisteredVoices: boolean
  maximumVoiceMovementSemitones: number
  findings: VoiceLeadingFinding[]
}

export interface VoiceLeadingReview {
  sectionId: string
  transitions: VoiceLeadingTransition[]
  smoothTransitionCount: number
  averageMotionSemitones: number
}

export interface LyricLine {
  id: string
  text: string
  words: LyricWord[]
  punctuation: LyricPunctuation[]
  phrases: LyricPhrase[]
  syllablePlacements: SyllablePlacement[]
  rhythmCandidates: RhythmCandidate[]
  breathPoints: BreathPoint[]
}

export interface LyricWord {
  id: string
  text: string
  start: number
  length: number
  syllables: LyricSyllable[]
}

export interface LyricSyllable {
  id: string
  text: string
  position: number
  source: SyllableSource
  stress: StressMark | null
}

export interface StressMark {
  level: StressLevel
  provenance: StressProvenance
}

export interface LyricPunctuation {
  id: string
  text: string
  start: number
  length: number
}

export interface LyricPhrase {
  id: string
  position: number
  wordIds: string[]
  source: PhraseSource
  prosody: ProsodicPattern | null
}

export interface ProsodicPattern {
  id: string
  units: ProsodicUnit[]
}

export interface ProsodicUnit {
  id: string
  syllableId: string
  position: number
  weight: ProsodicWeight
  provenance: ProsodyProvenance
}

export interface BeatPosition {
  bar: number
  beat: number
  tick: number
}

export interface SyllablePlacement {
  id: string
  syllableId: string
  position: BeatPosition
  provenance: PlacementProvenance
}

export interface RhythmCandidateEvent {
  id: string
  syllableId: string
  position: number
  beatPosition: BeatPosition
}

export interface RhythmCandidate {
  id: string
  phraseId: string
  label: string
  provenance: RhythmCandidateProvenance
  events: RhythmCandidateEvent[]
}

export interface BreathPoint {
  id: string
  afterSyllableId: string
  provenance: BreathProvenance
}

export type ProsodyFindingKind = 'StressConflict' | 'BreathIssue' | 'Crowding'
export type ProsodyFindingSeverity = 'Info' | 'Warning'

export interface ProsodyFinding {
  kind: ProsodyFindingKind
  severity: ProsodyFindingSeverity
  message: string
  syllableId: string | null
  relatedSyllableId: string | null
}

export interface ProsodyScore {
  phraseId: string
  rhythmCandidateId: string | null
  overall: number
  stress: number
  breath: number
  crowding: number
  findings: ProsodyFinding[]
}

export type LyricTimelineMarkerKind = 'ActivePlacement' | 'RhythmCandidate' | 'BreathAfter'

export interface LyricTimelineSectionSpan {
  sectionId: string
  kind: SectionKind
  title: string
  start: BeatPosition
  durationBars: number
  startTick: number
  endTickExclusive: number
}

export interface LyricTimelineMarker {
  kind: LyricTimelineMarkerKind
  sectionId: string
  lineId: string
  phraseId: string | null
  syllableId: string
  placementId: string | null
  rhythmCandidateId: string | null
  syllableText: string
  wordText: string
  sectionRelative: BeatPosition
  songPosition: BeatPosition
  absoluteTick: number
  stressLevel: StressLevel | null
  prosodicWeight: ProsodicWeight | null
  hasBreathAfter: boolean
}

export interface LyricTimelineView {
  totalTicks: number
  ticksPerBeat: number
  beatsPerBar: number
  sections: LyricTimelineSectionSpan[]
  markers: LyricTimelineMarker[]
}

export type CreativeLockScope = 'LyricLine' | 'PhraseRhythm'
export type LockProvenance = 'Manual' | 'Analyzer' | 'Imported'

export interface CreativeLock {
  id: string
  scope: CreativeLockScope
  lineId: string
  phraseId: string | null
  provenance: LockProvenance
}

export interface SongSection {
  id: string
  kind: SectionKind
  title: string
  lyricLines: LyricLine[]
  harmony: HarmonyChord[]
  harmonyCandidates: HarmonyCandidate[]
  delivery: SectionDelivery
  performanceNotes: string
  structuralFunction: StructuralFunction
}

export interface SongProject {
  id: string
  schemaVersion: number
  title: string
  artist: string
  genre: SongGenre
  description: string
  rawLyricDraft: string
  lastModifiedUtc: string
  timeline: {
    ticksPerQuarterNote: number
    tempoMap: { events: Array<{ beat: number; beatsPerMinute: number }> }
    timeSignatureMap: { events: Array<{ beat: number; numerator: number; denominator: number }> }
    sectionPlacements: Array<{
      sectionId: string
      start: { bar: number; beat: number; tick: number }
      durationBars: number
    }>
  }
  sections: SongSection[]
  tracks: unknown[]
  locks: CreativeLock[]
  arrangement: SectionArrangement[]
  arrangementRoles: SectionRoleAssignment[]
  noteEvents: NoteEvent[]
  musicalParts: MusicalPart[]
  key: MusicalKey
}

export interface ProjectResponse {
  project: SongProject
  canUndo: boolean
  canRedo: boolean
}

export interface WorkspaceHealth {
  status: 'ready'
  persistence: 'local-host'
  schemaVersion: number
  webClientHosted: boolean
}

export interface PortableProjectImportPreview {
  id: string
  title: string
  artist: string
  genre: SongGenre
  sourceSchemaVersion: number
  currentSchemaVersion: number
  sectionCount: number
  lyricLineCount: number
  hasRawLyrics: boolean
  sectionTitles: string[]
  identityConflict: boolean
}

export interface ProjectSummary {
  id: string
  title: string
  artist: string
  genre: SongGenre
  lastModifiedUtc: string
  sectionCount: number
  hasRawLyrics: boolean
}

export interface TrashedProjectSummary {
  id: string
  title: string
  artist: string
  deletedAtUtc: string
}

export interface RecoverySummary {
  id: string
  title: string
  artist: string
  capturedAtUtc: string
  sectionCount: number
  lyricLineCount: number
  hasRawLyrics: boolean
  sectionTitles: string[]
}

export interface RecoveryProjectResponse {
  project: SongProject
  capturedAtUtc: string
  baseProjectLastModifiedUtc: string
}

export interface ProjectCommand {
  type: string
  project?: SongProject
  sectionId?: string
  sourceSectionId?: string
  kind?: SectionKind
  sectionDelivery?: SectionDelivery
  structuralFunction?: StructuralFunction
  title?: string
  performanceNotes?: string
  numerator?: number
  denominator?: number
  targetIndex?: number
  durationBars?: number
  lyrics?: string[]
  proposedSections?: ProposedSongSection[]
  lineId?: string
  wordId?: string
  syllableId?: string
  phraseId?: string
  stressLevel?: StressLevel | null
  prosodicWeight?: ProsodicWeight | null
  beatPosition?: BeatPosition | null
  rhythmCandidateId?: string
  candidateLabel?: string
  breathPresent?: boolean
  creativeLockId?: string
  key?: MusicalKey
  chord?: ChordSymbol
  harmonyChordId?: string
  harmonyCandidateId?: string
  registeredPitches?: RegisteredPitch[] | null
  minimumMidiNote?: number
  maximumMidiNote?: number
  sectionEnergy?: SectionEnergy
  sectionDensity?: SectionDensity
  arrangementRole?: ArrangementRole
  rolePresent?: boolean
  noteEventId?: string
  musicalPartId?: string
  partLabel?: string
  noteEventIds?: string[]
  notePitch?: RegisteredPitch
  startTick?: number
  durationTicks?: number
  velocity?: number
  text?: string
  syllables?: string[]
}

export interface ProposedSongSection {
  kind: SectionKind
  title: string
  delivery: SectionDelivery
  performanceNotes: string
  lyrics: string[]
  structuralFunction: StructuralFunction
}

export interface LyricSheetStructurePreview {
  sections: ProposedSongSection[]
  unassignedLines: string[]
  unrecognizedHeadings: string[]
  unrecognizedSections: Array<{
    heading: string
    delivery: SectionDelivery
    performanceNotes: string
    lyrics: string[]
    insertionIndex: number
    resolutionKind?: SectionKind
  }>
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const error = (await response.json().catch(() => null)) as {
      error?: string
      recoveryCopyFileName?: string
    } | null
    const recoveryDetail = error?.recoveryCopyFileName
      ? ` Recovery file: ${error.recoveryCopyFileName}`
      : ''
    throw new Error(`${error?.error ?? `Request failed with status ${response.status}.`}${recoveryDetail}`)
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

async function requestBlob(url: string, init?: RequestInit): Promise<Blob> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const error = await response.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `Request failed with status ${response.status}.`)
  }
  return response.blob()
}

export const projectsApi = {
  health: () => request<WorkspaceHealth>('/api/health'),
  list: () => request<ProjectSummary[]>('/api/projects'),
  listRecovery: () => request<RecoverySummary[]>('/api/recovery'),
  loadRecovery: (id: string) => request<RecoveryProjectResponse>(`/api/recovery/${id}`),
  saveRecovery: (project: SongProject, baseProjectLastModifiedUtc: string, sessionId: string) =>
    request<void>(`/api/projects/${project.id}/recovery`, {
      method: 'PUT', body: JSON.stringify({ project, baseProjectLastModifiedUtc, sessionId }),
    }),
  discardRecovery: (id: string) => request<void>(`/api/recovery/${id}`, { method: 'DELETE' }),
  delete: (id: string) => request<void>(`/api/projects/${id}`, { method: 'DELETE' }),
  listTrash: () => request<TrashedProjectSummary[]>('/api/trash'),
  restore: (id: string) => request<void>(`/api/trash/${id}/restore`, { method: 'POST' }),
  permanentlyDelete: (id: string) => request<void>(`/api/trash/${id}`, { method: 'DELETE' }),
  create: (title: string) => request<ProjectResponse>('/api/projects', {
    method: 'POST', body: JSON.stringify({ title }),
  }),
  duplicate: (id: string) => request<ProjectResponse>(`/api/projects/${id}/duplicate`, { method: 'POST' }),
  previewStructure: (text: string) => request<LyricSheetStructurePreview>('/api/structure-preview', {
    method: 'POST', body: JSON.stringify({ text }),
  }),
  load: (id: string) => request<ProjectResponse>(`/api/projects/${id}`),
  save: (project: SongProject, baseProjectLastModifiedUtc: string) => request<ProjectResponse>(`/api/projects/${project.id}`, {
    method: 'PUT', body: JSON.stringify({ project, baseProjectLastModifiedUtc }),
  }),
  command: (id: string, project: SongProject, command: ProjectCommand) => request<ProjectResponse>(`/api/projects/${id}/commands`, {
    method: 'POST', body: JSON.stringify({ ...command, project }),
  }),
  scoreProsody: (
    id: string,
    project: SongProject,
    sectionId: string,
    lineId: string,
    phraseId: string,
    rhythmCandidateId?: string,
  ) => request<ProsodyScore>(`/api/projects/${id}/prosody-score`, {
    method: 'POST',
    body: JSON.stringify({ project, sectionId, lineId, phraseId, rhythmCandidateId: rhythmCandidateId ?? null }),
  }),
  lyricTimeline: (
    id: string,
    project: SongProject,
    rhythmCandidateId?: string | null,
  ) => request<LyricTimelineView>(`/api/projects/${id}/lyric-timeline`, {
    method: 'POST',
    body: JSON.stringify({ project, rhythmCandidateId: rhythmCandidateId ?? null }),
  }),
  reviewVoiceLeading: (id: string, project: SongProject, sectionId: string) =>
    request<VoiceLeadingReview>(`/api/projects/${id}/voice-leading-review`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  harmonyNoteSketch: (id: string, project: SongProject, sectionId: string) =>
    request<HarmonyNoteSketch>(`/api/projects/${id}/harmony-note-sketch`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  lowEndSupportProposal: (id: string, project: SongProject, sectionId: string) =>
    request<LowEndSupportProposal>(`/api/projects/${id}/low-end-support-proposal`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  pulseProposal: (id: string, project: SongProject, sectionId: string) =>
    request<PulseProposal>(`/api/projects/${id}/pulse-proposal`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  harmonySupportProposal: (id: string, project: SongProject, sectionId: string) =>
    request<HarmonySupportProposal>(`/api/projects/${id}/harmony-support-proposal`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  textureProposal: (id: string, project: SongProject, sectionId: string) =>
    request<TextureProposal>(`/api/projects/${id}/texture-proposal`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  hookReinforcementProposal: (id: string, project: SongProject, sectionId: string) =>
    request<HookReinforcementProposal>(`/api/projects/${id}/hook-reinforcement-proposal`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  countermelodyProposal: (id: string, project: SongProject, sectionId: string) =>
    request<CountermelodyProposal>(`/api/projects/${id}/countermelody-proposal`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  accentProposal: (id: string, project: SongProject, sectionId: string) =>
    request<AccentProposal>(`/api/projects/${id}/accent-proposal`, {
      method: 'POST', body: JSON.stringify({ project, sectionId }),
    }),
  exportMidi: (id: string, project: SongProject) =>
    requestBlob(`/api/projects/${id}/midi-export`, {
      method: 'POST', body: JSON.stringify({ project }),
    }),
  exportPortableProject: (id: string, project: SongProject) =>
    requestBlob(`/api/projects/${id}/portable-export`, {
      method: 'POST', body: JSON.stringify({ project }),
    }),
  previewPortableProject: (projectJson: string) => request<PortableProjectImportPreview>('/api/projects/import-preview', {
    method: 'POST', body: JSON.stringify({ projectJson }),
  }),
  importPortableProject: (projectJson: string, importAsCopy: boolean) => request<ProjectResponse>('/api/projects/import', {
    method: 'POST', body: JSON.stringify({ projectJson, importAsCopy }),
  }),
  undo: (id: string, project: SongProject) => request<ProjectResponse>(`/api/projects/${id}/undo`, { method: 'POST', body: JSON.stringify({ project }) }),
  redo: (id: string, project: SongProject) => request<ProjectResponse>(`/api/projects/${id}/redo`, { method: 'POST', body: JSON.stringify({ project }) }),
}
