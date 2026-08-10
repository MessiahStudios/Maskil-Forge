export type SectionKind = 'Verse' | 'Chorus' | 'PreChorus' | 'Bridge' | 'Outro'
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
  key: MusicalKey
}

export interface ProjectResponse {
  project: SongProject
  canUndo: boolean
  canRedo: boolean
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
  kind?: SectionKind
  title?: string
  numerator?: number
  denominator?: number
  targetIndex?: number
  durationBars?: number
  lyrics?: string[]
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
  notePitch?: RegisteredPitch
  startTick?: number
  durationTicks?: number
  velocity?: number
  text?: string
  syllables?: string[]
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

export const projectsApi = {
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
  undo: (id: string, project: SongProject) => request<ProjectResponse>(`/api/projects/${id}/undo`, { method: 'POST', body: JSON.stringify({ project }) }),
  redo: (id: string, project: SongProject) => request<ProjectResponse>(`/api/projects/${id}/redo`, { method: 'POST', body: JSON.stringify({ project }) }),
}
