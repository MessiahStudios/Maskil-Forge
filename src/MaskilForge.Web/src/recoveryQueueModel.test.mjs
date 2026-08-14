import assert from 'node:assert/strict'
import test from 'node:test'
import { buildRecoveryQueue, recoveryQueueStats, recoveryRecentLimit, recoverySoftCap, recoveryStaleDays } from './recoveryQueueModel.js'

function summary(id, capturedAtUtc, title = id) {
  return {
    id,
    title,
    artist: '',
    capturedAtUtc,
    sectionCount: 0,
    lyricLineCount: 1,
    hasRawLyrics: true,
    sectionTitles: [],
  }
}

test('recovery queue counts a host and browser copy as one protected song', () => {
  const host = summary('song-1', '2026-08-01T00:00:00Z', 'Host title')
  const browser = summary('song-1', '2026-08-02T00:00:00Z', 'Browser title')
  const queue = buildRecoveryQueue([host], [browser], '2026-08-03T00:00:00Z')

  assert.equal(queue.length, 1)
  assert.equal(queue[0].title, 'Browser title')
  assert.equal(queue[0].sourceLabel, 'Local host + this browser')
  assert.equal(queue[0].hasHostSnapshot, true)
  assert.equal(queue[0].hasBrowserSnapshot, true)
})

test('recovery queue sorts unique songs newest first and marks thirty-day-old work stale', () => {
  const queue = buildRecoveryQueue([
    summary('old', '2026-07-01T00:00:00Z'),
    summary('recent', '2026-08-12T00:00:00Z'),
  ], [], '2026-08-13T00:00:00Z')

  assert.deepEqual(queue.map(item => item.id), ['recent', 'old'])
  assert.equal(queue[0].isStale, false)
  assert.equal(queue[1].ageDays, 43)
  assert.equal(queue[1].isStale, true)
  assert.equal(recoveryStaleDays, 30)
})

test('recovery queue statistics expose the five-item view and ten-item soft cap without deleting', () => {
  const queue = Array.from({ length: recoverySoftCap + 1 }, (_, index) => ({ isStale: index < 2 }))
  assert.deepEqual(recoveryQueueStats(queue), {
    uniqueCount: 11,
    hiddenCount: 11 - recoveryRecentLimit,
    staleCount: 2,
    overSoftCap: true,
  })
})
