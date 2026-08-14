import type { BrowserProjectRecord } from './browserRecovery'

export interface BrowserProjectSummary {
  id: string
  title: string
  artist: string
  genre: string
  savedAtUtc: string
  lastModifiedUtc: string
  sectionCount: number
  lyricLineCount: number
  hasRawLyrics: boolean
  sectionTitles: string[]
}

export function summarizeBrowserProject(record: BrowserProjectRecord): BrowserProjectSummary
export function browserProjectNotice(count: number): string
