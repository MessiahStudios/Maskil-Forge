export type SectionKind = 'Verse' | 'Chorus' | 'PreChorus' | 'Bridge' | 'Outro'
export type SongGenre = 'Unspecified' | 'Pop' | 'Rock' | 'Folk' | 'Country' | 'RAndB' | 'HipHop' | 'Electronic' | 'Cinematic' | 'Alternative' | 'Other'

export interface LyricLine {
  id: string
  text: string
}

export interface SongSection {
  id: string
  kind: SectionKind
  title: string
  lyricLines: LyricLine[]
}

export interface SongProject {
  id: string
  schemaVersion: { value: number }
  title: string
  artist: string
  genre: SongGenre
  description: string
  tempo: { beat: number; beatsPerMinute: number }
  timeSignature: { beat: number; numerator: number; denominator: number }
  sections: SongSection[]
  tracks: unknown[]
}

export interface ProjectResponse {
  project: SongProject
  canUndo: boolean
  canRedo: boolean
}

export interface ProjectCommand {
  type: string
  sectionId?: string
  kind?: SectionKind
  title?: string
  targetIndex?: number
  lyrics?: string[]
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const error = (await response.json().catch(() => null)) as { error?: string } | null
    throw new Error(error?.error ?? `Request failed with status ${response.status}.`)
  }
  return response.json() as Promise<T>
}

export const projectsApi = {
  create: (title: string) => request<ProjectResponse>('/api/projects', {
    method: 'POST', body: JSON.stringify({ title }),
  }),
  load: (id: string) => request<ProjectResponse>(`/api/projects/${id}`),
  save: (project: SongProject) => request<ProjectResponse>(`/api/projects/${project.id}`, {
    method: 'PUT', body: JSON.stringify({ project }),
  }),
  command: (id: string, command: ProjectCommand) => request<ProjectResponse>(`/api/projects/${id}/commands`, {
    method: 'POST', body: JSON.stringify(command),
  }),
  undo: (id: string) => request<ProjectResponse>(`/api/projects/${id}/undo`, { method: 'POST' }),
  redo: (id: string) => request<ProjectResponse>(`/api/projects/${id}/redo`, { method: 'POST' }),
}
