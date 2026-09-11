<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import { projectsApi, type ProjectAsset, type VocalProcessingRecipe } from './api'
import { prepareVocalLowCut, type VocalComparison } from './vocalLowCut'

const props = defineProps<{ projectId: string; asset: ProjectAsset; roleEnabled: boolean; recipe?: VocalProcessingRecipe; busy: boolean }>()
const emit = defineEmits<{ accept: [assetId: string, sourceSha256: string]; clear: [assetId: string]; playing: [] }>()
const comparison = shallowRef<VocalComparison | null>(null)
const preparing = ref(false)
const message = ref('')
const heardOriginal = ref(false)
const heardProcessed = ref(false)
const originalPlayer = ref<HTMLAudioElement | null>(null)
const processedPlayer = ref<HTMLAudioElement | null>(null)
const accepted = computed(() => props.recipe?.sourceSha256 === props.asset.sha256)
let generation = 0
let controller: AbortController | null = null

function stop() { originalPlayer.value?.pause(); processedPlayer.value?.pause() }
function discard() {
  generation++
  controller?.abort()
  controller = null
  stop()
  comparison.value?.dispose()
  comparison.value = null
  preparing.value = false
  heardOriginal.value = false
  heardProcessed.value = false
  message.value = ''
}

async function prepare() {
  discard()
  if (!props.roleEnabled || props.busy) return
  const token = generation
  controller = new AbortController()
  preparing.value = true
  try {
    const result = await prepareVocalLowCut(projectsApi.originalVocalTakeUrl(props.projectId, props.asset.id), props.asset, controller.signal)
    if (generation !== token) { result.dispose(); return }
    comparison.value = result
    message.value = 'Comparison ready. Play each version before deciding.'
  } catch (error) {
    if (generation === token) message.value = error instanceof Error ? error.message : 'The take could not be decoded for processing.'
  } finally { if (generation === token) preparing.value = false }
}

function playing(version: 'original' | 'processed', event: Event) {
  emit('playing')
  document.querySelectorAll('audio').forEach(player => { if (player !== event.target) player.pause() })
  if (version === 'original') heardOriginal.value = true
  else heardProcessed.value = true
}

function accept() {
  if (!props.busy && props.roleEnabled && comparison.value && heardOriginal.value && heardProcessed.value)
    emit('accept', props.asset.id, props.asset.sha256)
}
watch([() => props.projectId, () => props.asset.id, () => props.asset.sha256, () => props.roleEnabled], discard)
watch(() => props.busy, value => { if (value) stop() })
onBeforeUnmount(discard)
</script>

<template>
  <details class="vocal-low-cut" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) discard() }">
    <summary>Compare a low-cut treatment <span v-if="accepted">· Accepted</span></summary>
    <p>Corrective Tone · Reduce low-frequency rumble with an 80 Hz low-cut filter. It can also thin a low voice, so compare it with your original performance.</p>
    <p>Only this treatment is previewed. The other production jobs remain plans.</p>
    <p v-if="!roleEnabled">Add Corrective Tone in “Choose and order production jobs” to try this treatment.</p>
    <p v-if="accepted">Low-cut settings are accepted for this take. Prepare a comparison to hear them again. Use Save to keep accepted settings with the song.</p>
    <details><summary>Processing settings</summary><p>High-pass biquad · 80 Hz · Q 0.7071 · 12 dB/octave · No added gain. These fixed settings are the first built-in processor.</p></details>
    <div class="low-cut-actions">
      <button type="button" :disabled="busy || preparing || !roleEnabled" @click="prepare">{{ preparing ? 'Preparing comparison…' : 'Prepare comparison' }}</button>
      <button v-if="preparing || comparison" type="button" class="quiet" :disabled="busy" @click="discard">Discard comparison</button>
      <button v-if="accepted" type="button" class="quiet" :disabled="busy || preparing" @click="emit('clear', asset.id)">Clear accepted low-cut</button>
    </div>
    <p v-if="message" role="status">{{ message }}</p>
    <div v-if="comparison" class="low-cut-comparison">
      <label>Original comparison<audio ref="originalPlayer" controls preload="metadata" :src="comparison.originalUrl" :aria-label="`Original comparison for ${asset.name}`" @playing="playing('original', $event)"></audio></label>
      <label>Low-cut preview<audio ref="processedPlayer" controls preload="metadata" :src="comparison.processedUrl" :aria-label="`Low-cut preview for ${asset.name}`" @playing="playing('processed', $event)"></audio></label>
      <p>Both comparisons start from the same decoded take at its original level. Playing one pauses the other. Playback and prepared audio stay in this tab.</p>
      <button v-if="!accepted" type="button" :disabled="busy || !heardOriginal || !heardProcessed" @click="accept">Accept low-cut settings</button>
      <p v-if="!accepted && (!heardOriginal || !heardProcessed)">Play both versions to enable acceptance.</p>
    </div>
    <p class="low-cut-boundary">Your saved original stays unchanged. Discard removes the temporary comparison; clearing accepted settings is undoable.</p>
  </details>
</template>

<style scoped>
.vocal-low-cut { padding: .85rem; border: 1px solid #54614d; border-radius: .4rem; background: #10150f; }
summary { cursor: pointer; font-weight: 650; padding: .4rem 0; }
p { color: #abb4a4; line-height: 1.5; font-size: .85rem; }
.low-cut-actions { display: flex; flex-wrap: wrap; gap: .5rem; margin: .7rem 0; }
.low-cut-comparison { display: grid; gap: .6rem; }
label { display: grid; gap: .4rem; color: #e6cf91; }
audio { width: 100%; }
.low-cut-boundary { border-top: 1px solid #394236; padding-top: .65rem; }
</style>
