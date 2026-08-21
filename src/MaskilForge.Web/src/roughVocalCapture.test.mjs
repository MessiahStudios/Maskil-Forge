import assert from 'node:assert/strict'
import test from 'node:test'
import { beginRoughVocalCapture, formatRoughVocalBytes, formatRoughVocalDuration, preferredRoughVocalMediaType } from './roughVocalCapture.js'

test('rough vocal format selection prefers a compact browser-supported recording', () => {
  const MediaRecorderType = { isTypeSupported: type => type === 'audio/ogg;codecs=opus' }
  assert.equal(preferredRoughVocalMediaType(MediaRecorderType), 'audio/ogg;codecs=opus')
  assert.equal(formatRoughVocalDuration(61_000), '1m 1s')
  assert.equal(formatRoughVocalBytes(1_572_864), '1.5 MB')
})

test('rough vocal capture remains temporary until stop and closes every microphone track', async () => {
  const tracks = [{ readyState: 'live', stopped: false, stop() { this.stopped = true } }]
  const stream = { getAudioTracks: () => tracks, getTracks: () => tracks }
  class FakeMediaRecorder {
    static isTypeSupported(type) { return type === 'audio/webm;codecs=opus' }
    constructor() { this.mimeType = 'audio/webm;codecs=opus'; this.state = 'inactive' }
    start() { this.state = 'recording' }
    stop() {
      this.ondataavailable({ data: new Blob(['artist voice'], { type: this.mimeType }) })
      this.state = 'inactive'
      this.onstop()
    }
  }
  let now = 1_000
  const session = await beginRoughVocalCapture({
    navigator: { mediaDevices: { getUserMedia: async () => stream } },
    MediaRecorder: FakeMediaRecorder,
    performance: { now: () => (now += 1_500) },
  })

  const result = await session.stop()

  assert.equal(result.mediaType, 'audio/webm;codecs=opus')
  assert.equal(await result.blob.text(), 'artist voice')
  assert.equal(result.durationMs, 1_500)
  assert.equal(tracks[0].stopped, true)
})

test('discarding a rough vocal closes the live input without retaining audio', async () => {
  const track = { readyState: 'live', stopped: false, stop() { this.stopped = true } }
  class FakeMediaRecorder {
    static isTypeSupported() { return false }
    constructor() { this.mimeType = 'audio/webm'; this.state = 'inactive' }
    start() { this.state = 'recording' }
    stop() { this.state = 'inactive'; this.onstop() }
  }
  const session = await beginRoughVocalCapture({
    navigator: { mediaDevices: { getUserMedia: async () => ({ getAudioTracks: () => [track], getTracks: () => [track] }) } },
    MediaRecorder: FakeMediaRecorder,
  })

  session.discard()

  assert.equal(track.stopped, true)
})
