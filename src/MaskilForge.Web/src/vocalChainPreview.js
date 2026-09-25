import { encodePreviewWav, renderVocalLowCut, validateLowCutSettings } from './vocalLowCut.js'
import { renderVocalLevelControl } from './vocalLevelControl.js'
import { renderVocalSaturation } from './vocalSaturation.js'
import { renderVocalCharacterCompression } from './vocalCharacterCompression.js'
import { renderVocalCleanup } from './vocalCleanup.js'
import { renderVocalSibilance } from './vocalSibilance.js'
import { renderVocalSpace } from './vocalSpace.js'

const names = Object.freeze({ CorrectiveTone: 'Low-cut', TransparentDynamics: 'Level control', Saturation: 'Saturation', CharacterCompression: 'Character compression', Cleanup: 'Cleanup', SibilanceControl: 'Sibilance control', Space: 'Space' })

function asBuffer(channels, sampleRate) {
  return { sampleRate, length: channels[0].length, numberOfChannels: channels.length, getChannelData: index => channels[index] }
}

export function acceptedVocalChainSteps(chainRoles, recipes, asset) {
  const steps = []
  const seen = new Set()
  for (const role of chainRoles ?? []) {
    if (seen.has(role)) continue
    seen.add(role)
    const recipe = (recipes ?? []).find(item => item?.assetId === asset?.id && item.role === role && item.sourceSha256 === asset?.sha256)
    if (!recipe) continue
    if (role === 'CorrectiveTone') steps.push(Object.freeze({ role, cutoffHertz: recipe.cutoffHertz, q: recipe.q }))
    else if (role === 'TransparentDynamics' || role === 'Saturation' || role === 'CharacterCompression' || role === 'Cleanup' || role === 'SibilanceControl' || role === 'Space') steps.push(Object.freeze({ role }))
  }
  return Object.freeze(steps)
}

export function describeAcceptedVocalChain(steps) {
  if (!steps?.length) return 'Accept a built-in vocal treatment on this take to hear it here.'
  return steps.map(step => names[step.role]).join(' → ')
}

export function renderAcceptedVocalChain(buffer, steps) {
  if (!Array.isArray(steps) || steps.length < 1 || steps.length > 7)
    throw new Error('Hear the accepted built-in vocal treatments in plan order.')
  const seen = new Set()
  let current = buffer
  let channels = null
  for (const step of steps) {
    if (!step || seen.has(step.role)) throw new Error('Each accepted treatment is heard once.')
    seen.add(step.role)
    if (step.role === 'CorrectiveTone') channels = renderVocalLowCut(current, validateLowCutSettings(step))
    else if (step.role === 'TransparentDynamics') channels = renderVocalLevelControl(current)
    else if (step.role === 'Saturation') channels = renderVocalSaturation(current)
    else if (step.role === 'CharacterCompression') channels = renderVocalCharacterCompression(current)
    else if (step.role === 'Cleanup') channels = renderVocalCleanup(current)
    else if (step.role === 'SibilanceControl') channels = renderVocalSibilance(current)
    else if (step.role === 'Space') channels = renderVocalSpace(current)
    else throw new Error('This preview only includes the accepted built-in vocal treatments.')
    current = asBuffer(channels, buffer.sampleRate)
  }
  return channels
}

export async function prepareAcceptedVocalChain(url, asset, steps, signal, environment = globalThis) {
  const renderedSteps = Object.freeze(steps.map(step => Object.freeze({ ...step })))
  renderAcceptedVocalChain({ sampleRate: 48000, length: 1, numberOfChannels: 1, getChannelData: () => new Float32Array([0]) }, renderedSteps)
  const AudioContextType = environment.AudioContext ?? environment.webkitAudioContext
  if (!AudioContextType || !environment.crypto?.subtle) throw new Error('This browser cannot prepare verified vocal processing previews.')
  if (!Number.isInteger(asset.byteLength) || asset.byteLength < 1 || asset.byteLength > 25 * 1024 * 1024)
    throw new Error('The source take is outside the preview size limit.')
  let context, originalUrl, processedUrl
  const abort = () => { if (signal?.aborted) throw new Error('Preview cancelled.') }
  try {
    abort()
    const response = await environment.fetch(url, { cache: 'no-store', signal })
    if (!response.ok) throw new Error(`The original take could not be loaded (${response.status}).`)
    const bytes = await response.arrayBuffer()
    if (bytes.byteLength !== asset.byteLength) throw new Error('The source take length no longer matches this project.')
    const digest = Array.from(new Uint8Array(await environment.crypto.subtle.digest('SHA-256', bytes)), byte => byte.toString(16).padStart(2, '0')).join('')
    if (digest !== asset.sha256) throw new Error('The source take digest no longer matches this project.')
    abort()
    context = new AudioContextType({ sampleRate: 48000 })
    const buffer = await context.decodeAudioData(bytes)
    abort()
    const processed = renderAcceptedVocalChain(buffer, renderedSteps)
    const original = Array.from({ length: buffer.numberOfChannels }, (_, index) => buffer.getChannelData(index))
    originalUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(original, buffer.sampleRate)], { type: 'audio/wav' }))
    processedUrl = environment.URL.createObjectURL(new environment.Blob([encodePreviewWav(processed, buffer.sampleRate)], { type: 'audio/wav' }))
    abort()
    let disposed = false
    return { originalUrl, processedUrl, steps: renderedSteps, dispose() {
      if (disposed) return
      disposed = true
      environment.URL.revokeObjectURL(originalUrl)
      environment.URL.revokeObjectURL(processedUrl)
    } }
  } catch (error) {
    if (originalUrl) environment.URL.revokeObjectURL(originalUrl)
    if (processedUrl) environment.URL.revokeObjectURL(processedUrl)
    throw error
  } finally { if (context) await context.close().catch(() => undefined) }
}
