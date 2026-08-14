import assert from 'node:assert/strict'
import test from 'node:test'
import { browserProjectNotice, summarizeBrowserProject } from './browserProjectModel.js'

function record() {
  return {
    projectId: 'project-1',
    savedAtUtc: '2026-08-14T05:00:00Z',
    project: {
      title: 'Saved song',
      artist: 'Maskil Artist',
      genre: 'Alternative',
      lastModifiedUtc: '2026-08-14T04:59:00Z',
      rawLyricDraft: '[Verse]\nFirst line',
      sections: [
        { title: 'Verse', lyricLines: [{ text: 'First line' }, { text: ' ' }] },
        { title: 'Chorus', lyricLines: [{ text: 'Sing it back' }] },
      ],
    },
  }
}

test('saved project summary describes the exact cached song anatomy', () => {
  assert.deepEqual(summarizeBrowserProject(record()), {
    id: 'project-1',
    title: 'Saved song',
    artist: 'Maskil Artist',
    genre: 'Alternative',
    savedAtUtc: '2026-08-14T05:00:00Z',
    lastModifiedUtc: '2026-08-14T04:59:00Z',
    sectionCount: 2,
    lyricLineCount: 2,
    hasRawLyrics: true,
    sectionTitles: ['Verse', 'Chorus'],
  })
})

test('saved project summary counts an unstructured raw draft', () => {
  const rawRecord = record()
  rawRecord.project.sections = []
  rawRecord.project.rawLyricDraft = 'First thought\n\nSecond thought\nThird thought'
  assert.equal(summarizeBrowserProject(rawRecord).lyricLineCount, 3)
})

test('saved project notice names view-only device availability honestly', () => {
  assert.match(browserProjectNotice(0), /No saved song snapshots/)
  assert.match(browserProjectNotice(1), /1 explicitly saved song snapshot is/)
  assert.match(browserProjectNotice(2), /2 explicitly saved song snapshots are/)
  assert.match(browserProjectNotice(2), /view-only/)
})
