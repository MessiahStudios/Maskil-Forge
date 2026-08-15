export function vocalCaptureSupport(environment = globalThis) {
  if (!environment?.isSecureContext) {
    return {
      supported: false,
      reason: 'Microphone access requires an installed app or a secure browser connection.',
    }
  }

  if (typeof environment?.navigator?.mediaDevices?.getUserMedia !== 'function') {
    return {
      supported: false,
      reason: 'This browser does not expose microphone capture to Maskil Forge.',
    }
  }

  if (typeof environment?.MediaRecorder !== 'function') {
    return {
      supported: false,
      reason: 'This browser can open a microphone but cannot create a rough vocal take.',
    }
  }

  return {
    supported: true,
    reason: 'This browser can request a microphone and record a rough vocal take.',
  }
}

export async function verifyMicrophoneInput(getUserMedia) {
  const stream = await getUserMedia({ audio: true })
  const tracks = typeof stream?.getAudioTracks === 'function' ? stream.getAudioTracks() : []

  try {
    if (!tracks.length || tracks.every(track => track.readyState === 'ended')) {
      throw Object.assign(new Error('No live microphone input became available.'), { name: 'NotFoundError' })
    }

    return {
      label: tracks[0].label?.trim() || 'Available microphone',
      trackCount: tracks.length,
    }
  } finally {
    if (typeof stream?.getTracks === 'function') {
      for (const track of stream.getTracks()) track.stop()
    }
  }
}

export function microphonePreflightFailure(error) {
  if (error?.name === 'NotAllowedError' || error?.name === 'SecurityError') {
    return 'Microphone access was not granted. Nothing was recorded or saved.'
  }
  if (error?.name === 'NotFoundError' || error?.name === 'DevicesNotFoundError') {
    return 'No available microphone was found. Nothing was recorded or saved.'
  }
  if (error?.name === 'NotReadableError' || error?.name === 'TrackStartError') {
    return 'The microphone is unavailable or already in use by another application.'
  }
  return 'Maskil Forge could not verify this microphone. Nothing was recorded or saved.'
}
