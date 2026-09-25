<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import { projectsApi, type ProjectAsset, type VocalProcessingRecipe } from './api'
import { prepareVocalLevelControl, vocalLevelControl, type VocalLevelComparison } from './vocalLevelControl'

const props = defineProps<{ projectId: string; asset: ProjectAsset; roleEnabled: boolean; recipe?: VocalProcessingRecipe; busy: boolean }>()
const emit = defineEmits<{ accept: [assetId: string, sourceSha256: string]; clear: [assetId: string]; playing: [] }>()
const comparison = shallowRef<VocalLevelComparison | null>(null)
const preparing = ref(false)
const message = ref('')
const heardOriginal = ref(false)
const heardProcessed = ref(false)
const originalPlayer = ref<HTMLAudioElement | null>(null)
const processedPlayer = ref<HTMLAudioElement | null>(null)
const accepted = computed(() => props.recipe?.processorId === vocalLevelControl.processorId && props.recipe.sourceSha256 === props.asset.sha256)
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
    const result = await prepareVocalLevelControl(projectsApi.originalVocalTakeUrl(props.projectId, props.asset.id), props.asset, controller.signal)
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
  <details class="vocal-level-control" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) discard() }">
    <summary>Compare level control <span v-if="accepted">· Accepted</span></summary>
    <p>Transparent Level Control · Ease peaks above −18 dBFS with a 2:1 ratio, 20 ms attack, and 120 ms release. Quiet phrases are not boosted, and no makeup gain or limiter is applied.</p>
    <p>Only this treatment is previewed, from the original take. It does not include an accepted low-cut or any other production job.</p>
    <p v-if="!roleEnabled">Add Transparent Level Control in “Choose and order production jobs” to try this treatment.</p>
    <p v-if="accepted">Accepted starting point: −18 dBFS threshold · 2:1 · 20 ms attack · 120 ms release. Prepare a comparison to hear it again. Use Save to keep accepted settings with the song.</p>
    <div class="level-actions">
      <button type="button" :disabled="busy || preparing || !roleEnabled" @click="prepare">{{ preparing ? 'Preparing comparison…' : 'Prepare comparison' }}</button>
      <button v-if="preparing || comparison" type="button" class="quiet" :disabled="busy" @click="discard">Discard comparison</button>
      <button v-if="accepted" type="button" class="quiet" :disabled="busy || preparing" @click="emit('clear', asset.id)">Clear accepted level control</button>
    </div>
    <p v-if="message" role="status">{{ message }}</p>
    <div v-if="comparison" class="level-comparison">
      <label>Original comparison<audio ref="originalPlayer" controls preload="metadata" :src="comparison.originalUrl" :aria-label="`Original comparison for ${asset.name}`" @playing="playing('original', $event)"></audio></label>
      <label>Level-control preview<audio ref="processedPlayer" controls preload="metadata" :src="comparison.processedUrl" :aria-label="`Level-control preview for ${asset.name}`" @playing="playing('processed', $event)"></audio></label>
      <p>Both comparisons start from the same decoded take. Louder moments above the threshold are eased. Playing one pauses the other. Playback and prepared audio stay in this tab.</p>
      <button v-if="!accepted" type="button" :disabled="busy || !heardOriginal || !heardProcessed" @click="accept">Accept level-control settings</button>
      <p v-if="!accepted && (!heardOriginal || !heardProcessed)">Play both versions to enable acceptance.</p>
    </div>
    <p class="level-boundary">Your saved original stays unchanged. Discard removes the temporary comparison; clearing accepted settings is undoable. There are no advanced controls for this starting point.</p>
  </details>
</template>

<style scoped>
.vocal-level-control { padding: .85rem; border: 1px solid #54614d; border-radius: .4rem; background: #10150f; }
summary { cursor: pointer; font-weight: 650; padding: .4rem 0; }
p { color: #abb4a4; line-height: 1.5; font-size: .85rem; }
.level-actions { display: flex; flex-wrap: wrap; gap: .5rem; margin: .7rem 0; }
.level-comparison { display: grid; gap: .6rem; }
label { display: grid; gap: .4rem; color: #e6cf91; }
audio { width: 100%; }
.level-boundary { border-top: 1px solid #394236; padding-top: .65rem; }
</style>
