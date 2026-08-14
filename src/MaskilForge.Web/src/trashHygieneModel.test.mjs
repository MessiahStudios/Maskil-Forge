import assert from 'node:assert/strict'
import test from 'node:test'
import { buildTrashQueue, filterTrashQueue, trashAgeLabel, trashOldDays, trashRecentLimit, trashResultStats } from './trashHygieneModel.js'

function project(id, title, artist, deletedAtUtc) {
  return { id, title, artist, deletedAtUtc }
}

test('Trash queue preserves repository order and labels age without removing anything', () => {
  const projects = [
    project('new', 'Night Signal', 'Mara Vale', '2026-08-13T12:00:00Z'),
    project('old', 'Morning Glass', '', '2026-07-01T00:00:00Z'),
  ]
  const queue = buildTrashQueue(projects, '2026-08-14T00:00:00Z')

  assert.deepEqual(queue.map(item => item.id), ['new', 'old'])
  assert.equal(queue[0].ageDays, 0)
  assert.equal(queue[0].isOld, false)
  assert.equal(queue[1].isOld, true)
  assert.equal(trashOldDays, 30)
})

test('Trash search matches title or artist without changing result order', () => {
  const queue = buildTrashQueue([
    project('1', 'Night Signal', 'Mara Vale', '2026-08-13T00:00:00Z'),
    project('2', 'Morning Glass', 'Night Choir', '2026-08-12T00:00:00Z'),
    project('3', 'Low Tide', 'Mara Vale', '2026-08-11T00:00:00Z'),
  ])

  assert.deepEqual(filterTrashQueue(queue, 'NIGHT').map(item => item.id), ['1', '2'])
  assert.deepEqual(filterTrashQueue(queue, 'mara').map(item => item.id), ['1', '3'])
})

test('Trash result collapse is presentation-only and age labels remain readable', () => {
  const queue = Array.from({ length: trashRecentLimit + 2 }, (_, index) => ({ isOld: index < 3 }))

  assert.deepEqual(trashResultStats(queue), {
    resultCount: trashRecentLimit + 2,
    visibleCount: trashRecentLimit,
    hiddenCount: 2,
    oldCount: 3,
  })
  assert.equal(trashResultStats(queue, true).visibleCount, trashRecentLimit + 2)
  assert.equal(trashAgeLabel(0), 'Today')
  assert.equal(trashAgeLabel(1), '1 day')
  assert.equal(trashAgeLabel(45), '45 days')
})
