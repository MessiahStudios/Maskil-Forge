<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import { projectsApi, type ProjectAsset, type VocalProcessingRecipe } from './api'
import { prepareVocalCleanup, vocalCleanup, type VocalCleanupComparison } from './vocalCleanup'

const props = defineProps<{ projectId: string; asset: ProjectAsset; roleEnabled: boolean; recipe?: VocalProcessingRecipe; busy: boolean }>()
const emit = defineEmits<{ accept: [assetId: string, sourceSha256: string]; clear: [assetId: string]; playing: [] }>()
const comparison = shallowRef<VocalCleanupComparison | null>(null)
const preparing = ref(false)
const message = ref('')
const heardOriginal = ref(false)
const heardProcessed = ref(false)
const originalPlayer = ref<HTMLAudioElement | null>(null)
const processedPlayer = ref<HTMLAudioElement | null>(null)
const accepted = computed(() => props.recipe?.processorId === vocalCleanup.processorId && props.recipe.sourceSha256 === props.asset.sha256)
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
    const result = await prepareVocalCleanup(projectsApi.originalVocalTakeUrl(props.projectId, props.asset.id), props.asset, controller.signal)
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
  if (!props.busy && props.roleEnabled && !accepted.value && comparison.value && heardOriginal.value && heardProcessed.value)
    emit('accept', props.asset.id, props.asset.sha256)
}
watch([() => props.projectId, () => props.asset.id, () => props.asset.sha256, () => props.roleEnabled, () => props.recipe], discard)
watch(() => props.busy, value => { if (value) stop() })
onBeforeUnmount(discard)
</script>

<template>
  <details class="vocal-cleanup" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) discard() }">
    <summary>Compare cleanup <span v-if="accepted">· Accepted</span></summary>
    <p>Cleanup · Gaps below −40 dBFS ease toward one quarter of their level over 200 ms, and a sung level returns over 10 ms. The gaps are lowered, not silenced, and sung levels are not boosted.</p>
    <p>Only this treatment is previewed, from the original take. It does not include an accepted low-cut, level control, saturation, character compression, or any other production job.</p>
    <p v-if="!roleEnabled">Add Cleanup in “Choose and order production jobs” to try this treatment.</p>
    <p v-if="accepted">Accepted starting point: −40 dBFS threshold · quiet gaps to one quarter · 10 ms return · 200 ms close. Prepare a comparison to hear it again. Use Save to keep accepted settings with the song.</p>
    <div class="cleanup-actions">
      <button type="button" :disabled="busy || preparing || !roleEnabled" @click="prepare">{{ preparing ? 'Preparing comparison…' : 'Prepare comparison' }}</button>
      <button v-if="preparing || comparison" type="button" class="quiet" :disabled="busy" @click="discard">Discard comparison</button>
      <button v-if="accepted" type="button" class="quiet" :disabled="busy || preparing" @click="emit('clear', asset.id)">Clear accepted cleanup</button>
    </div>
    <p v-if="message" role="status">{{ message }}</p>
    <div v-if="comparison" class="cleanup-comparison">
      <label>Original comparison<audio ref="originalPlayer" controls preload="metadata" :src="comparison.originalUrl" :aria-label="`Original comparison for ${asset.name}`" @playing="playing('original', $event)"></audio></label>
      <label>Cleanup preview<audio ref="processedPlayer" controls preload="metadata" :src="comparison.processedUrl" :aria-label="`Cleanup preview for ${asset.name}`" @playing="playing('processed', $event)"></audio></label>
      <p>Both comparisons start from the same decoded take. Playing one pauses the other. Playback and prepared audio stay in this tab.</p>
      <button v-if="!accepted" type="button" :disabled="busy || !heardOriginal || !heardProcessed" @click="accept">Accept cleanup settings</button>
      <p v-if="!accepted && (!heardOriginal || !heardProcessed)">Play both versions to enable acceptance.</p>
    </div>
    <p class="cleanup-boundary">Your saved original stays unchanged. Discard removes the temporary comparison; clearing accepted settings is undoable. There are no advanced controls for this starting point.</p>
  </details>
</template>

<style scoped>
.vocal-cleanup { padding: .85rem; border: 1px solid #54614d; border-radius: .4rem; background: #10150f; }
summary { cursor: pointer; font-weight: 650; padding: .4rem 0; }
p { color: #abb4a4; line-height: 1.5; font-size: .85rem; }
.cleanup-actions { display: flex; flex-wrap: wrap; gap: .5rem; margin: .7rem 0; }
.cleanup-comparison { display: grid; gap: .6rem; }
label { display: grid; gap: .4rem; color: #e6cf91; }
audio { width: 100%; }
.cleanup-boundary { border-top: 1px solid #394236; padding-top: .65rem; }
</style>
