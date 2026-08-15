import assert from 'node:assert/strict'
import test from 'node:test'
import { deviceLyricCaptureNotice, deviceLyricCaptureRecentLimit, deviceLyricCaptureResultStats, deviceLyricCaptureSnapshot, filterDeviceLyricCaptures, summarizeDeviceLyricCapture } from './deviceLyricCaptureModel.js'

function capture() {
  return {
    captureId: 'capture-1',
    title: 'Night Window',
    artist: 'Maskil Artist',
    genre: 'Alternative',
    description: 'A browser-owned thought.',
    rawLyricDraft: 'First line\n\nSecond line',
    createdAtUtc: '2026-08-14T03:00:00Z',
    savedAtUtc: '2026-08-14T03:05:00Z',
  }
}

test('device capture summary describes locally saved words', () => {
  assert.deepEqual(summarizeDeviceLyricCapture(capture()), {
    id: 'capture-1',
    title: 'Night Window',
    artist: 'Maskil Artist',
    genre: 'Alternative',
    savedAtUtc: '2026-08-14T03:05:00Z',
    lyricLineCount: 2,
  })
})

test('device capture summary gives a blank title an honest fallback', () => {
  const value = capture()
  value.title = '  '
  assert.equal(summarizeDeviceLyricCapture(value).title, 'Untitled capture')
})

test('capture dirty snapshot excludes storage timestamps and identity', () => {
  const original = capture()
  const savedSnapshot = deviceLyricCaptureSnapshot(original)
  original.savedAtUtc = '2026-08-14T04:00:00Z'
  original.captureId = 'capture-2'
  assert.equal(deviceLyricCaptureSnapshot(original), savedSnapshot)
  original.rawLyricDraft += '\nThird line'
  assert.notEqual(deviceLyricCaptureSnapshot(original), savedSnapshot)
})

test('device capture notice names browser-only ownership', () => {
  assert.match(deviceLyricCaptureNotice(0), /No browser-owned lyric captures/)
  assert.match(deviceLyricCaptureNotice(1), /1 browser-owned lyric capture is/)
  assert.match(deviceLyricCaptureNotice(2), /2 browser-owned lyric captures are/)
  assert.match(deviceLyricCaptureNotice(2), /only on this device/)
})

test('device capture search matches title or artist without changing newest-first order', () => {
  const captures = [
    { id: '1', title: 'Night Signal', artist: 'Mara Vale' },
    { id: '2', title: 'Morning Glass', artist: 'Night Choir' },
    { id: '3', title: 'Low Tide', artist: 'Mara Vale' },
  ]

  assert.deepEqual(filterDeviceLyricCaptures(captures, 'NIGHT').map(item => item.id), ['1', '2'])
  assert.deepEqual(filterDeviceLyricCaptures(captures, 'mara').map(item => item.id), ['1', '3'])
  assert.equal(filterDeviceLyricCaptures(captures, 'missing').length, 0)
})

test('device capture collapse is presentation-only and never caps stored work', () => {
  const captures = Array.from({ length: deviceLyricCaptureRecentLimit + 3 }, (_, index) => ({
    id: String(index), title: `Capture ${index}`, artist: '',
  }))

  assert.deepEqual(deviceLyricCaptureResultStats(captures), {
    resultCount: deviceLyricCaptureRecentLimit + 3,
    visibleCount: deviceLyricCaptureRecentLimit,
    hiddenCount: 3,
  })
  assert.deepEqual(deviceLyricCaptureResultStats(captures, true), {
    resultCount: deviceLyricCaptureRecentLimit + 3,
    visibleCount: deviceLyricCaptureRecentLimit + 3,
    hiddenCount: 0,
  })
  assert.equal(captures.length, deviceLyricCaptureRecentLimit + 3)
})
