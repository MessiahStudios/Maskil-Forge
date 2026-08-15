import assert from 'node:assert/strict'
import test from 'node:test'
import {
  microphonePreflightFailure,
  verifyMicrophoneInput,
  vocalCaptureSupport,
} from './vocalCapturePreflight.js'

test('vocal capture support requires a secure recording-capable browser', () => {
  assert.deepEqual(vocalCaptureSupport({ isSecureContext: false }), {
    supported: false,
    reason: 'Microphone access requires an installed app or a secure browser connection.',
  })

  assert.equal(vocalCaptureSupport({
    isSecureContext: true,
    navigator: { mediaDevices: { getUserMedia() {} } },
  }).supported, false)

  assert.equal(vocalCaptureSupport({
    isSecureContext: true,
    navigator: { mediaDevices: { getUserMedia() {} } },
    MediaRecorder: class {},
  }).supported, true)
})

test('microphone preflight stops every track without recording audio', async () => {
  const stopped = []
  const tracks = [
    { label: 'Studio microphone', readyState: 'live', stop: () => stopped.push('audio') },
    { label: 'Companion track', readyState: 'live', stop: () => stopped.push('companion') },
  ]

  const result = await verifyMicrophoneInput(async constraints => {
    assert.deepEqual(constraints, { audio: true })
    return {
      getAudioTracks: () => [tracks[0]],
      getTracks: () => tracks,
    }
  })

  assert.deepEqual(result, { label: 'Studio microphone', trackCount: 1 })
  assert.deepEqual(stopped, ['audio', 'companion'])
})

test('microphone preflight still closes tracks when no live input is available', async () => {
  let stopped = false
  await assert.rejects(
    verifyMicrophoneInput(async () => ({
      getAudioTracks: () => [{ label: '', readyState: 'ended', stop: () => { stopped = true } }],
      getTracks: () => [{ stop: () => { stopped = true } }],
    })),
    error => error.name === 'NotFoundError',
  )
  assert.equal(stopped, true)
})

test('microphone failures keep permission, hardware, and busy-device guidance distinct', () => {
  assert.match(microphonePreflightFailure({ name: 'NotAllowedError' }), /not granted/i)
  assert.match(microphonePreflightFailure({ name: 'NotFoundError' }), /No available microphone/i)
  assert.match(microphonePreflightFailure({ name: 'NotReadableError' }), /already in use/i)
  assert.match(microphonePreflightFailure(new Error('unknown')), /could not verify/i)
})
