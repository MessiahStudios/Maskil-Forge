import assert from 'node:assert/strict'
import test from 'node:test'
import { filterProjectLibrary, libraryRecentLimit, libraryResultStats, projectLibraryStage } from './libraryHygieneModel.js'

function project(id, title, artist, sectionCount = 0, hasRawLyrics = false) {
  return { id, title, artist, sectionCount, hasRawLyrics }
}

test('saved-library stage follows structured, raw draft, then empty-start precedence', () => {
  assert.equal(projectLibraryStage(project('1', 'Structured', '', 2, true)), 'structured')
  assert.equal(projectLibraryStage(project('2', 'Draft', '', 0, true)), 'raw')
  assert.equal(projectLibraryStage(project('3', 'Idea', '', 0, false)), 'empty')
})

test('saved-library search matches title or artist without changing result order', () => {
  const projects = [
    project('1', 'Night Signal', 'Mara Vale'),
    project('2', 'Morning Glass', 'Night Choir'),
    project('3', 'Low Tide', 'Mara Vale'),
  ]

  assert.deepEqual(filterProjectLibrary(projects, 'NIGHT').map(item => item.id), ['1', '2'])
  assert.deepEqual(filterProjectLibrary(projects, 'mara').map(item => item.id), ['1', '3'])
})

test('saved-library filters and recent-result collapse never delete or reorder songs', () => {
  const projects = Array.from({ length: libraryRecentLimit + 3 }, (_, index) =>
    project(String(index), `Idea ${index}`, '', index === 1 ? 1 : 0, index === 2))

  assert.deepEqual(filterProjectLibrary(projects, '', 'structured').map(item => item.id), ['1'])
  assert.deepEqual(filterProjectLibrary(projects, '', 'raw').map(item => item.id), ['2'])
  assert.equal(filterProjectLibrary(projects, '', 'empty').length, libraryRecentLimit + 1)
  assert.deepEqual(libraryResultStats(projects), {
    resultCount: libraryRecentLimit + 3,
    visibleCount: libraryRecentLimit,
    hiddenCount: 3,
  })
  assert.deepEqual(libraryResultStats(projects, true), {
    resultCount: libraryRecentLimit + 3,
    visibleCount: libraryRecentLimit + 3,
    hiddenCount: 0,
  })
})
