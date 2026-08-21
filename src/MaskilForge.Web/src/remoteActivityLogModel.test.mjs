import assert from 'node:assert/strict'
import test from 'node:test'
import { activityLogDeviceKind, remoteActivityLogSessionLabel, remoteActivityLogSessionOptions } from './remoteActivityLogModel.js'

test('remote activity logging identifies coarse phone and tablet sessions without mislabeling desktop', () => {
  assert.equal(activityLogDeviceKind(390, 844, true), 'phone')
  assert.equal(activityLogDeviceKind(844, 390, true), 'phone')
  assert.equal(activityLogDeviceKind(820, 1180, true), 'tablet')
  assert.equal(activityLogDeviceKind(540, 900, false), 'desktop')
})

test('remote activity session labels expose only transient display context', () => {
  assert.equal(remoteActivityLogSessionLabel({
    deviceKind: 'phone',
    viewportWidth: 390,
    viewportHeight: 844,
    standalone: true,
  }), 'Phone · 390×844 · installed')
})

test('remote activity session options distinguish repeated device contexts by recency', () => {
  const sessions = [
    { sessionId: 'new', deviceKind: 'phone', viewportWidth: 384, viewportHeight: 794, standalone: false, lastSeenUtc: 'new-time' },
    { sessionId: 'old', deviceKind: 'phone', viewportWidth: 384, viewportHeight: 794, standalone: false, lastSeenUtc: 'old-time' },
    { sessionId: 'tablet', deviceKind: 'tablet', viewportWidth: 820, viewportHeight: 1180, standalone: true, lastSeenUtc: 'tablet-time' },
  ]

  assert.deepEqual(remoteActivityLogSessionOptions(sessions, value => value), [
    { sessionId: 'new', label: 'Phone · 384×794 · browser · latest new-time' },
    { sessionId: 'old', label: 'Phone · 384×794 · browser · earlier old-time' },
    { sessionId: 'tablet', label: 'Tablet · 820×1180 · installed · last active tablet-time' },
  ])
})
