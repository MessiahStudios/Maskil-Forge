<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { projectsApi, type ProjectAsset, type SongProject, type VocalMelodyCheck, type VocalMelodyRelation } from './api'

const props = defineProps<{ project: SongProject; asset: ProjectAsset; busy: boolean }>()
const emit = defineEmits<{ playing: [] }>()
const check = ref<VocalMelodyCheck | null>(null)
const loading = ref(false)
const error = ref('')
const ready = ref(false)
const player = ref<HTMLAudioElement | null>(null)
let generation = 0
const relationLabel: Record<VocalMelodyRelation, string> = { With: 'With the written note', Above: 'Above the written note', Below: 'Below the written note' }
const seconds = (milliseconds: number) => (milliseconds / 1000).toFixed(2)

function discard() {
  generation++
  player.value?.pause()
  check.value = null
  loading.value = false
  error.value = ''
  ready.value = false
}

async function preview() {
  discard()
  if (props.busy) return
  const token = generation
  loading.value = true
  try {
    const result = await projectsApi.vocalMelodyCheck(props.project, props.asset.id)
    if (generation === token) check.value = result
  } catch (cause) {
    if (generation === token) error.value = cause instanceof Error ? cause.message : 'The melody check could not be prepared.'
  } finally {
    if (generation === token) loading.value = false
  }
}

function playing(event: Event) {
  emit('playing')
  document.querySelectorAll('audio').forEach(audio => { if (audio !== event.target) audio.pause() })
}

async function listen(milliseconds: number) {
  const audio = player.value
  if (!audio || !ready.value || props.busy) return
  try {
    audio.currentTime = milliseconds / 1000
    await audio.play()
  } catch {
    error.value = 'The original could not be played from that moment. Try the recording controls.'
  }
}

watch(() => JSON.stringify(props.project), discard)
watch(() => props.busy, busy => { if (busy) player.value?.pause() })
onBeforeUnmount(discard)
</script>

<template>
  <details class="vocal-melody-check" :aria-label="`Melody check for ${asset.name}`" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) discard() }">
    <summary>Check against the written notes</summary>
    <p>This compares saved pitch frames from this take with the notes written in the song. The same note in another octave counts as a match. Nothing is tuned, and the recording stays unchanged.</p>
    <div class="melody-actions">
      <button type="button" :disabled="busy || loading" @click="preview">{{ loading ? 'Checking melody…' : 'Check melody' }}</button>
      <button v-if="check || loading" type="button" class="quiet" :disabled="busy" @click="discard">Discard check</button>
    </div>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="check" class="melody-review">
      <p role="status">{{ check.summary }}</p>
      <p>A reviewed accurate frame uses its saved pitch. A frame marked inaccurate uses the stored correction when one exists. The original measurement stays on the take. A moment between written notes is reported and not scored.</p>
      <template v-if="check.examples.length">
        <label>Listen to the original<audio ref="player" controls preload="metadata" :src="projectsApi.originalVocalTakeUrl(project.id, asset.id)" :aria-label="`Melody check original for ${asset.name}`" @loadedmetadata="ready = true" @playing="playing" @error="ready = false; error = 'The original recording could not be loaded.'"></audio></label>
        <ol>
          <li v-for="moment in check.examples" :key="`${moment.startMilliseconds}-${moment.sungMidi}`">
            <strong>{{ seconds(moment.startMilliseconds) }} s · {{ relationLabel[moment.relation] }}</strong>
            <small v-if="moment.artistCorrected">Uses the stored pitch correction. The original measurement stays on the take.</small>
            <button type="button" :disabled="busy || !ready" @click="listen(moment.startMilliseconds)">Listen from {{ seconds(moment.startMilliseconds) }} s</button>
          </li>
        </ol>
      </template>
    </div>
    <p class="melody-boundary">This check is not saved. Closing it removes the temporary result. The lead vocal and the original bytes stay as they are.</p>
  </details>
</template>

<style scoped>
.vocal-melody-check { padding: .85rem; border: 1px solid #54614d; border-radius: .4rem; background: #10150f; }
summary { cursor: pointer; font-weight: 650; padding: .4rem 0; }
p, li { color: #abb4a4; line-height: 1.5; font-size: .85rem; }
.melody-actions, .melody-review { display: grid; gap: .6rem; margin: .7rem 0; }
.melody-actions { display: flex; flex-wrap: wrap; }
label { display: grid; gap: .4rem; color: #e6cf91; }
audio { width: 100%; }
ol { display: grid; gap: .45rem; margin: 0; padding-left: 1.1rem; }
.melody-boundary { border-top: 1px solid #394236; padding-top: .65rem; }
</style>
