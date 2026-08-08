export type SectionKind = 'Verse' | 'Chorus' | 'PreChorus' | 'Bridge' | 'Outro'
export type SongGenre = 'Unspecified' | 'Pop' | 'Rock' | 'Folk' | 'Country' | 'RAndB' | 'HipHop' | 'Electronic' | 'Cinematic' | 'Alternative' | 'Other'

export interface LyricLine {
  id: string
  text: string
  words: LyricWord[]
}

export interface LyricWord {
  id: string
  text: string
  start: number
  length: number
  syllables: Array<{ id: string; text: string }>
}

export interface SongSection {
  id: string
  kind: SectionKind
  title: string
  lyricLines: LyricLine[]
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
  targetIndex?: number
  durationBars?: number
  lyrics?: string[]
  lineId?: string
  wordId?: string
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
  undo: (id: string, project: SongProject) => request<ProjectResponse>(`/api/projects/${id}/undo`, { method: 'POST', body: JSON.stringify({ project }) }),
  redo: (id: string, project: SongProject) => request<ProjectResponse>(`/api/projects/${id}/redo`, { method: 'POST', body: JSON.stringify({ project }) }),
}
