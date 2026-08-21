export const roughVocalMaximumDurationMs = 60_000
export const roughVocalMaximumByteLength = 25 * 1024 * 1024

const preferredMediaTypes = [
  'audio/webm;codecs=opus',
  'audio/ogg;codecs=opus',
  'audio/mp4',
]

export function preferredRoughVocalMediaType(MediaRecorderType) {
  if (typeof MediaRecorderType?.isTypeSupported !== 'function') return ''
  return preferredMediaTypes.find(type => MediaRecorderType.isTypeSupported(type)) ?? ''
}

export function formatRoughVocalDuration(durationMs) {
  const seconds = Math.max(1, Math.round(durationMs / 1_000))
  return seconds < 60 ? `${seconds}s` : `${Math.floor(seconds / 60)}m ${seconds % 60}s`
}

export function formatRoughVocalBytes(byteLength) {
  if (byteLength < 1_024 * 1_024) return `${Math.max(1, Math.round(byteLength / 1_024))} KB`
  return `${(byteLength / 1_024 / 1_024).toFixed(1)} MB`
}

export async function beginRoughVocalCapture(environment = globalThis) {
  const stream = await environment.navigator.mediaDevices.getUserMedia({
    audio: { echoCancellation: true, noiseSuppression: true },
  })
  const tracks = typeof stream?.getAudioTracks === 'function' ? stream.getAudioTracks() : []
  if (!tracks.length || tracks.every(track => track.readyState === 'ended')) {
    stream?.getTracks?.().forEach(track => track.stop())
    throw Object.assign(new Error('No live microphone input became available.'), { name: 'NotFoundError' })
  }

  const MediaRecorderType = environment.MediaRecorder
  const requestedMediaType = preferredRoughVocalMediaType(MediaRecorderType)
  let recorder
  try {
    recorder = requestedMediaType
      ? new MediaRecorderType(stream, { mimeType: requestedMediaType })
      : new MediaRecorderType(stream)
  } catch (error) {
    stream.getTracks().forEach(track => track.stop())
    throw error
  }

  const chunks = []
  const startedAt = environment.performance?.now?.() ?? Date.now()
  let discarded = false
  let settled = false
  let resolveCompleted
  let rejectCompleted
  const completed = new Promise((resolve, reject) => {
    resolveCompleted = resolve
    rejectCompleted = reject
  })
  const closeTracks = () => stream.getTracks().forEach(track => track.stop())

  recorder.ondataavailable = event => {
    if (!discarded && event.data?.size > 0) chunks.push(event.data)
  }
  recorder.onerror = event => {
    if (settled) return
    settled = true
    closeTracks()
    rejectCompleted(event.error ?? new Error('The browser could not finish this rough vocal take.'))
  }
  recorder.onstop = () => {
    if (settled) return
    settled = true
    closeTracks()
    const endedAt = environment.performance?.now?.() ?? Date.now()
    const mediaType = recorder.mimeType || requestedMediaType || chunks[0]?.type || 'audio/webm'
    resolveCompleted({
      blob: new Blob(discarded ? [] : chunks, { type: mediaType }),
      durationMs: Math.max(1, endedAt - startedAt),
      mediaType,
    })
  }
  recorder.start(1_000)

  return {
    mediaType: recorder.mimeType || requestedMediaType || 'audio/webm',
    stop() {
      if (recorder.state !== 'inactive') recorder.stop()
      return completed
    },
    discard() {
      discarded = true
      if (recorder.state !== 'inactive') recorder.stop()
      else closeTracks()
    },
  }
}
