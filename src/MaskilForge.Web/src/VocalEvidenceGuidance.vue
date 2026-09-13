<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { projectsApi, type SongProject, type ProjectAsset, type VocalEvidenceGuidance, type VocalProcessingRole } from './api'

const props = defineProps<{ project: SongProject; asset: ProjectAsset; busy: boolean }>()
const emit = defineEmits<{ accept: [assetId: string, signature: string]; playing: [] }>()
const guidance = ref<VocalEvidenceGuidance | null>(null)
const loading = ref(false), error = ref(''), ready = ref(false)
const player = ref<HTMLAudioElement | null>(null)
let generation = 0
const names: Record<VocalProcessingRole, string> = { Cleanup: 'Cleanup', CorrectiveTone: 'Corrective tone', CharacterCompression: 'Character compression', Saturation: 'Saturation / color', TransparentDynamics: 'Transparent level control', SibilanceControl: 'Sibilance control', Space: 'Space' }
const order = (roles: VocalProcessingRole[]) => roles.length ? roles.map(role => names[role]).join(' → ') : 'None'
const seconds = (milliseconds: number) => (milliseconds / 1000).toFixed(2)
function discard() { generation++; player.value?.pause(); guidance.value = null; loading.value = false; error.value = ''; ready.value = false }
async function preview() {
  discard()
  if (props.busy) return
  const token = generation
  loading.value = true
  try {
    const result = await projectsApi.vocalEvidenceGuidance(props.project, props.asset.id)
    if (generation === token) guidance.value = result
  } catch (cause) { if (generation === token) error.value = cause instanceof Error ? cause.message : 'Guidance could not be prepared.' }
  finally { if (generation === token) loading.value = false }
}
function playing(event: Event) {
  emit('playing')
  document.querySelectorAll('audio').forEach(audio => { if (audio !== event.target) audio.pause() })
}
async function listen(milliseconds: number) {
  const audio = player.value
  if (!audio || !ready.value || props.busy) return
  try { audio.currentTime = milliseconds / 1000; await audio.play() }
  catch { error.value = 'The original could not be played from that frame. Try the recording controls.' }
}
watch(() => JSON.stringify(props.project), discard)
watch(() => props.busy, busy => { if (busy) player.value?.pause() })
onBeforeUnmount(discard)
</script>

<template>
  <details class="vocal-evidence-guidance" :aria-label="`Reviewed loudness guidance for ${asset.name}`" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) discard() }">
    <summary>Guidance from reviewed loudness</summary>
    <p>Analyze this take's loudness, then review its claims in “Inspect analyzer evidence.” Accurate claims and artist-corrected claims can support a production suggestion.</p>
    <div class="guidance-actions">
      <button type="button" :disabled="busy || loading" @click="preview">{{ loading ? 'Preparing guidance…' : 'Preview loudness guidance' }}</button>
      <button v-if="guidance || loading" type="button" class="quiet" :disabled="busy" @click="discard">Discard guidance</button>
    </div>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="guidance" class="guidance-review">
      <p>{{ guidance.evidence.length }} usable reviewed frames for this take.</p>
      <p v-if="guidance.spreadDecibels === null" role="status">Review at least three usable loudness frames above −60 dBFS to compare their levels. No job is suggested yet.</p>
      <p v-else>Reviewed frame RMS range: {{ guidance.spreadDecibels.toFixed(2) }} dB.</p>
      <template v-if="guidance.suggestLevelControl">
        <strong>Consider Transparent Level Control</strong>
        <p>The reviewed frames differ by at least 12 dB. Gentle level control may help quieter words remain audible. The variation may also be intentional phrasing; listen before deciding.</p>
        <p>Current job order: {{ order(guidance.currentRoles) }}</p>
        <p>Proposed job order: {{ order(guidance.proposedRoles) }}</p>
        <p v-if="guidance.hasChanges">Acceptance appends Transparent Level Control to your plan. Existing jobs keep their order. You can reorder them in the job editor.</p>
        <p v-else role="status">Transparent Level Control is already in your plan. No change is needed.</p>
      </template>
      <p v-else-if="guidance.spreadDecibels !== null" role="status">The reviewed range is below the 12 dB suggestion threshold. No additional job is proposed; this does not mean the take needs no other work.</p>
      <p>The 12 dB threshold is a starting rule, not a diagnosis. Only reviewed frames from the built-in loudness analyzer v1 are used. Very quiet frames, unsupported sources, and unreviewed claims are excluded. These frame measurements do not establish perceived loudness or a complete vocal dynamic range.</p>
      <template v-if="guidance.evidence.length">
        <label>Listen to the original<audio ref="player" controls preload="metadata" :src="projectsApi.originalVocalTakeUrl(project.id, asset.id)" :aria-label="`Guidance original for ${asset.name}`" @loadedmetadata="ready = true" @playing="playing" @error="ready = false; error = 'The original recording could not be loaded.'"></audio></label>
        <details open><summary>Evidence used in this review</summary>
          <ol class="guidance-evidence">
            <li v-for="frame in guidance.evidence" :key="frame.observationId">
              <strong>{{ seconds(frame.startMilliseconds) }}–{{ seconds(frame.startMilliseconds + frame.durationMilliseconds) }} s · {{ frame.rmsDbfs.toFixed(2) }} dBFS RMS</strong>
              <span>{{ frame.artistCorrected ? `Artist correction (analyzer originally ${frame.originalRmsDbfs.toFixed(2)} dBFS)` : 'Analyzer value marked accurate' }}</span>
              <small>{{ frame.analyzerId }} · {{ frame.analyzerVersion }} · {{ frame.provenance }} · Confidence: {{ frame.confidence ?? 'not supplied' }}</small>
              <small>Observation {{ frame.observationId }}</small>
              <button type="button" :disabled="busy || !ready" @click="listen(frame.startMilliseconds)">Listen from {{ seconds(frame.startMilliseconds) }} s</button>
            </li>
          </ol>
        </details>
      </template>
      <p>This is a plan preview. A Transparent Level Control processor is not available yet, so the player plays the original recording. Acceptance changes no audio or accepted take settings.</p>
      <button v-if="guidance.hasChanges" type="button" :disabled="busy" @click="emit('accept', asset.id, guidance.sourceSignature)">Add suggested level-control job</button>
      <p>Accepting a job is undoable. Use Save to keep it with the song.</p>
    </div>
  </details>
</template>

<style scoped>
.vocal-evidence-guidance { padding: .85rem; border: 1px solid #54614d; border-radius: .4rem; background: #10150f; }
summary { cursor: pointer; padding: .4rem 0; font-weight: 650; }
p, span, small { color: #abb4a4; line-height: 1.5; }
strong { color: #e6cf91; }
.guidance-actions { display: flex; flex-wrap: wrap; gap: .5rem; }
.guidance-review, label, .guidance-evidence li { display: grid; gap: .5rem; }
.guidance-evidence { padding-left: 1.3rem; max-height: 26rem; overflow-y: auto; }
.guidance-evidence li { margin: .6rem 0; padding: .5rem; background: #181e16; overflow-wrap: anywhere; }
audio { width: 100%; }
</style>
