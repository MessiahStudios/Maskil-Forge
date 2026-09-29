<script setup lang="ts">
import { onBeforeUnmount, ref } from 'vue'
import { projectsApi, type Vst3DiscoveryResult, type Vst3PreviewResult } from './api'

type Candidate = Vst3DiscoveryResult['locations'][number]['candidates'][number]
const props = defineProps<{ location: string; candidate: Candidate; classId: string; className: string }>()
const busy = ref(false), error = ref(''), preview = ref<Vst3PreviewResult | null>(null), playing = ref(false)
let controller: AbortController | null = null
let audio: HTMLAudioElement | null = null
let audioUrl = ''
const statuses: Record<string, string> = {
  WorkerUnavailable: 'The native worker is unavailable on this Mac',
  Busy: 'Another plugin load is running. Try again when it finishes.',
  CandidateUnavailable: 'This plugin is no longer available. Check the folders again.',
  RescanRequired: 'The plugin declaration changed. Check the folders again.',
  HeaderNotMatched: 'The executable header does not match this Mac',
  InvalidClassId: 'The reported class cannot be loaded',
  TimedOut: 'Loading exceeded 30 seconds and was stopped',
  Cancelled: 'Loading was cancelled',
  WorkerCrashed: 'The plugin exited unexpectedly',
  InvalidWorkerOutput: 'The plugin did not return a playable preview',
  ProcessorUnavailable: 'This class did not provide an audio processor',
  ProcessingSetupFailed: 'The processor did not accept the host setup',
  ProcessingFailed: 'The processor rejected the preview',
  PreviewWriteFailed: 'The preview audio could not be saved',
  ComponentClassUnavailable: 'The factory no longer returns this class',
  ComponentClassUnsupported: 'This class is not an audio module',
}
function stop() {
  audio?.pause()
  if (audioUrl) URL.revokeObjectURL(audioUrl)
  audio = null
  audioUrl = ''
  playing.value = false
}
function clear() {
  controller?.abort()
  controller = null
  busy.value = false
  error.value = ''
  preview.value = null
  stop()
}
async function load() {
  const declaration = props.candidate.binary.macExecutable
  if (!declaration?.sha256 || props.candidate.format === 'VST') return
  clear()
  const token = controller = new AbortController()
  busy.value = true
  try {
    const outcome = await projectsApi.renderVst3Preview(props.location, props.candidate.relativePath, declaration.sha256, props.classId, token.signal)
    if (controller === token) preview.value = outcome
  } catch (cause) {
    if (controller === token && !(cause instanceof DOMException && cause.name === 'AbortError'))
      error.value = cause instanceof Error ? cause.message : 'The plugin could not be loaded.'
  } finally {
    if (controller === token) { busy.value = false; controller = null }
  }
}
function hear() {
  const encoded = preview.value?.audioWavBase64
  if (!encoded) return
  stop()
  const bytes = Uint8Array.from(atob(encoded), character => character.charCodeAt(0))
  audioUrl = URL.createObjectURL(new Blob([bytes], { type: 'audio/wav' }))
  audio = new Audio(audioUrl)
  playing.value = true
  audio.addEventListener('ended', () => { playing.value = false })
  void audio.play().catch(() => { playing.value = false; error.value = 'The browser did not start the preview.' })
}
onBeforeUnmount(() => clear())
</script>

<template>
  <div class="plugin-load">
    <button type="button" :disabled="busy" @click="load">{{ busy ? 'Loading plugin…' : `Load ${className}` }}</button>
    <button v-if="busy" type="button" class="quiet" @click="clear">Cancel load</button>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="preview" role="status">
      <p v-if="preview.status === 'Completed'">{{ className }} loaded and rendered one second at {{ preview.sampleRate }} Hz. Peak level {{ preview.peak }}. This preview is temporary and does not change the song.</p>
      <p v-else>{{ statuses[preview.status] ?? preview.status }} Last completed stage: {{ preview.lastCompletedStage }}.</p>
      <p v-if="preview.status === 'Completed' && preview.peak === 0">The processor ran and produced silence. Its sample library, preset, or editor may still be required before it has a sound.</p>
      <button v-if="preview.audioWavBase64" type="button" @click="hear">{{ playing ? 'Playing preview…' : 'Hear the loaded plugin' }}</button>
    </div>
  </div>
</template>

<style scoped>
.plugin-load { display: grid; gap: .3rem; }
button { margin: .25rem .5rem .25rem 0; }
p { color: #abb4a4; line-height: 1.5; }
</style>
