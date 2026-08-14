import assert from 'node:assert/strict'
import test from 'node:test'
import { browserRecoveryNotice, summarizeBrowserRecovery } from './browserRecoveryModel.js'

function record() {
  return {
    projectId: 'project-1',
    capturedAtUtc: '2026-08-14T04:00:00Z',
    baseProjectLastModifiedUtc: '2026-08-14T03:00:00Z',
    sessionId: 'session-1',
    project: {
      title: 'Protected song',
      artist: 'Maskil Artist',
      rawLyricDraft: 'A raw line',
      sections: [
        { title: 'Verse', lyricLines: [{ text: 'First line' }, { text: ' ' }] },
        { title: 'Chorus', lyricLines: [{ text: 'Sing it back' }] },
      ],
    },
  }
}

test('browser recovery summary describes protected creative contents', () => {
  assert.deepEqual(summarizeBrowserRecovery(record()), {
    id: 'project-1',
    title: 'Protected song',
    artist: 'Maskil Artist',
    capturedAtUtc: '2026-08-14T04:00:00Z',
    sectionCount: 2,
    lyricLineCount: 2,
    hasRawLyrics: true,
    sectionTitles: ['Verse', 'Chorus'],
  })
})

test('browser recovery notice keeps offline and reconnect states honest', () => {
  assert.equal(browserRecoveryNotice(0, false), '')
  assert.match(browserRecoveryNotice(1, false), /protected on this device/)
  assert.match(browserRecoveryNotice(2, true), /ready to return to the local project service/)
})

test('browser recovery summary counts lines in an unstructured raw draft', () => {
  const rawRecord = record()
  rawRecord.project.sections = []
  rawRecord.project.rawLyricDraft = 'First thought\n\nSecond thought\nThird thought'
  assert.equal(summarizeBrowserRecovery(rawRecord).lyricLineCount, 3)
})
