<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import { projectsApi, type ProjectAsset, type VocalProcessingRecipe, type VocalProcessingRole } from './api'
import { acceptedVocalChainSteps, describeAcceptedVocalChain, prepareAcceptedVocalChain, type VocalChainComparison } from './vocalChainPreview'

const props = defineProps<{ projectId: string; asset: ProjectAsset; busy: boolean; chainRoles: VocalProcessingRole[]; recipes: VocalProcessingRecipe[] }>()
const emit = defineEmits<{ playing: [] }>()
const comparison = shallowRef<VocalChainComparison | null>(null)
const preparing = ref(false)
const message = ref('')
const originalPlayer = ref<HTMLAudioElement | null>(null)
const processedPlayer = ref<HTMLAudioElement | null>(null)
const steps = computed(() => acceptedVocalChainSteps(props.chainRoles, props.recipes, props.asset))
const description = computed(() => describeAcceptedVocalChain(steps.value))
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
  message.value = ''
}

async function prepare() {
  discard()
  if (props.busy || !steps.value.length) return
  const token = generation
  const rendered = steps.value
  controller = new AbortController()
  preparing.value = true
  try {
    const result = await prepareAcceptedVocalChain(projectsApi.originalVocalTakeUrl(props.projectId, props.asset.id), props.asset, rendered, controller.signal)
    if (generation !== token) { result.dispose(); return }
    comparison.value = result
    message.value = 'Comparison ready.'
  } catch (error) {
    if (generation === token) message.value = error instanceof Error ? error.message : 'The take could not be decoded for processing.'
  } finally { if (generation === token) preparing.value = false }
}

function playing(event: Event) {
  emit('playing')
  document.querySelectorAll('audio').forEach(player => { if (player !== event.target) player.pause() })
}

watch(() => JSON.stringify(steps.value), discard)
watch(() => props.busy, value => { if (value) stop() })
onBeforeUnmount(discard)
</script>

<template>
  <details class="vocal-chain" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) discard() }">
    <summary>Hear accepted treatments</summary>
    <p>This plays the original take through the low-cut and level-control settings already accepted for it, in the current production-job order. Jobs without a processor stay in the plan and are not heard.</p>
    <p>{{ description }}</p>
    <div class="chain-actions">
      <button type="button" :disabled="busy || preparing || !steps.length" @click="prepare">{{ preparing ? 'Preparing comparison…' : 'Prepare comparison' }}</button>
      <button v-if="preparing || comparison" type="button" class="quiet" :disabled="busy" @click="discard">Discard comparison</button>
    </div>
    <p v-if="message" role="status">{{ message }}</p>
    <div v-if="comparison" class="chain-comparison">
      <label>Original comparison<audio ref="originalPlayer" controls preload="metadata" :src="comparison.originalUrl" :aria-label="`Original chain comparison for ${asset.name}`" @playing="playing"></audio></label>
      <label>Accepted treatments<audio ref="processedPlayer" controls preload="metadata" :src="comparison.processedUrl" :aria-label="`Accepted treatments for ${asset.name}`" @playing="playing"></audio></label>
      <p>Both comparisons start from the saved original. Playing one pauses the other. This does not change accepted settings or store a new recording.</p>
    </div>
  </details>
</template>

<style scoped>
.vocal-chain { padding: .85rem; border: 1px solid #54614d; border-radius: .4rem; background: #10150f; }
summary { cursor: pointer; font-weight: 650; padding: .4rem 0; }
p { color: #abb4a4; line-height: 1.5; font-size: .85rem; }
.chain-actions { display: flex; flex-wrap: wrap; gap: .5rem; margin: .7rem 0; }
.chain-comparison { display: grid; gap: .6rem; }
label { display: grid; gap: .4rem; color: #e6cf91; }
audio { width: 100%; }
</style>
