export function vocalCountInOffsets(beatsPerMinute, beatsPerBar) {
  const tempo = Number(beatsPerMinute)
  if (!Number.isFinite(tempo) || tempo < 20 || tempo > 300)
    throw new Error('Count-in uses the song tempo, from 20 to 300 BPM.')
  if (!Number.isInteger(beatsPerBar) || beatsPerBar < 1 || beatsPerBar > 32)
    throw new Error('Count-in uses one bar of the song meter.')
  const interval = 60_000 / tempo
  return Array.from({ length: beatsPerBar }, (_, index) => Math.round(index * interval))
}

export function vocalCountInDurationMs(beatsPerMinute, beatsPerBar) {
  const offsets = vocalCountInOffsets(beatsPerMinute, beatsPerBar)
  const interval = offsets.length > 1 ? offsets[1] - offsets[0] : Math.round(60_000 / Number(beatsPerMinute))
  return offsets[offsets.length - 1] + interval
}

export async function playVocalCountIn({ beatsPerMinute, beatsPerBar, signal, environment = globalThis }) {
  const offsets = vocalCountInOffsets(beatsPerMinute, beatsPerBar)
  const durationMs = vocalCountInDurationMs(beatsPerMinute, beatsPerBar)
  const AudioContextType = environment.AudioContext ?? environment.webkitAudioContext
  if (!AudioContextType) throw new Error('This browser cannot play a count-in.')
  if (signal?.aborted) throw new Error('Count-in cancelled.')
  const context = new AudioContextType()
  const setTimer = environment.setTimeout?.bind(environment) ?? setTimeout
  const clearTimer = environment.clearTimeout?.bind(environment) ?? clearTimeout
  try {
    const start = context.currentTime + 0.05
    offsets.forEach((offset, index) => {
      const oscillator = context.createOscillator()
      const gain = context.createGain()
      oscillator.frequency.value = index === offsets.length - 1 ? 880 : 660
      oscillator.connect(gain)
      gain.connect(context.destination)
      const at = start + offset / 1000
      gain.gain.setValueAtTime(0.0001, at)
      gain.gain.exponentialRampToValueAtTime(0.2, at + 0.005)
      gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.07)
      oscillator.start(at)
      oscillator.stop(at + 0.08)
    })
    await new Promise((resolve, reject) => {
      const timer = setTimer(resolve, durationMs)
      signal?.addEventListener('abort', () => {
        clearTimer(timer)
        reject(new Error('Count-in cancelled.'))
      }, { once: true })
    })
  } finally {
    await context.close().catch(() => undefined)
  }
}
