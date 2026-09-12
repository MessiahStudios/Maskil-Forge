<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { projectsApi, type SongProject, type VocalProfileProposal, type VocalProcessingRole } from './api'
import { vocalProductionIntentSummary } from './vocalProductionIntentModel.js'
import VocalLowCutPreview from './VocalLowCutPreview.vue'

const props = defineProps<{ project: SongProject; busy: boolean }>()
const emit = defineEmits<{ accept: [revision: string]; playing: [] }>()
const proposal = ref<VocalProfileProposal | null>(null)
const loading = ref(false)
const error = ref('')
const selectedTake = ref('')
let generation = 0
const asset = computed(() => props.project.assets.find(take => take.id === selectedTake.value))
const canPreview = computed(() => proposal.value?.jobs.some(job => job.canPreview))
const roleName = (role: VocalProcessingRole) => proposal.value?.jobs.find(job => job.role === role)?.name ?? ({
  Cleanup: 'Cleanup', CorrectiveTone: 'Corrective tone', CharacterCompression: 'Character compression',
  Saturation: 'Saturation / color', TransparentDynamics: 'Transparent level control', SibilanceControl: 'Sibilance control', Space: 'Space',
}[role])
const names = (roles: VocalProcessingRole[]) => roles.length ? roles.map(roleName).join(' → ') : 'None'

function discard() { generation++; proposal.value = null; loading.value = false; error.value = '' }
async function prepare() {
  discard()
  if (props.busy || !props.project.vocalProductionIntent) return
  const token = generation
  loading.value = true
  try {
    const result = await projectsApi.vocalProfileProposal(props.project)
    if (token !== generation) return
    proposal.value = result
    selectedTake.value = props.project.assets[0]?.id ?? ''
  } catch (cause) { if (token === generation) error.value = cause instanceof Error ? cause.message : 'Suggestions could not be prepared.' }
  finally { if (token === generation) loading.value = false }
}
watch(() => JSON.stringify(props.project), discard)
onBeforeUnmount(discard)
</script>

<template>
  <section class="vocal-profile-proposal" aria-labelledby="vocal-profile-proposal-title">
    <p class="eyebrow">From direction to production jobs</p>
    <h3 id="vocal-profile-proposal-title">Explore a plan for your vocal</h3>
    <p>Set or update your vocal direction above, then preview suggestions for that direction. Your written notes stay visible for your judgment; they are not automatically interpreted.</p>
    <strong>{{ vocalProductionIntentSummary(project.vocalProductionIntent) }}</strong>
    <p v-if="project.vocalProductionIntent?.artistNotes" class="artist-notes">Keep in mind: {{ project.vocalProductionIntent.artistNotes }}</p>
    <div class="proposal-actions">
      <button type="button" :disabled="busy || loading || !project.vocalProductionIntent" @click="prepare">{{ loading ? 'Preparing suggestions…' : 'Preview suggested jobs' }}</button>
      <button v-if="proposal || loading" type="button" class="quiet" :disabled="busy" @click="discard">Discard proposal</button>
    </div>
    <p v-if="!project.vocalProductionIntent">Choose and set a vocal direction to begin.</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="proposal" class="proposal-review">
      <p>These are starting suggestions from your chosen words. They do not diagnose your recording or guarantee a sound. Review each job against your performance.</p>
      <ol aria-label="Suggested vocal production jobs">
        <li v-for="job in proposal.jobs" :key="job.role">
          <strong>{{ job.name }}</strong>
          <ul><li v-for="reason in job.reasons" :key="reason">{{ reason }}</li></ul>
          <small>{{ job.canPreview ? 'An 80 Hz low-cut example is available below. It covers only part of this job.' : 'Plan only · Audio processing for this job is not available yet.' }}</small>
        </li>
      </ol>
      <details open>
        <summary>What accepting this plan changes</summary>
        <p>Current order: {{ names(proposal.currentRoles) }}</p>
        <p>Proposed order: {{ names(proposal.jobs.map(job => job.role)) }}</p>
        <p>Add: {{ names(proposal.addedRoles) }}</p>
        <p>Remove from the plan: {{ names(proposal.removedRoles) }}</p>
        <p v-if="proposal.orderChanges">The jobs kept from your current plan will also be reordered.</p>
        <p>Acceptance replaces the production-job plan and can be undone. Vocal direction, accepted take settings, and original recordings stay unchanged. You can revise the accepted jobs in the manual editor below.</p>
      </details>
      <div v-if="canPreview" class="proposal-audition">
        <h4>Try the available treatment</h4>
        <p>This comparison auditions only a low-cut example, not the complete proposed sound. Trying it does not add jobs or accept processing settings.</p>
        <template v-if="project.assets.length">
          <label>Take to compare<select v-model="selectedTake" :disabled="busy"><option v-for="take in project.assets" :key="take.id" :value="take.id">{{ take.name }}</option></select></label>
          <VocalLowCutPreview v-if="asset" :key="asset.id" :project-id="project.id" :asset="asset" :role-enabled="true" :busy="busy" preview-only @playing="emit('playing')" />
        </template>
        <p v-else>Save an original take to audition the available treatment. You can still review and accept a job plan now.</p>
      </div>
      <p v-else>No audio preview is available for these suggested jobs yet. This preview reviews the plan only.</p>
      <button type="button" :disabled="busy || !proposal.hasChanges" @click="emit('accept', proposal.sourceSignature)">Accept proposed jobs</button>
      <p v-if="!proposal.hasChanges" role="status">These jobs already match your current plan.</p>
      <p>Accepting jobs does not accept an audio treatment. After choosing jobs, compare and accept any take settings separately. Use Save to keep the plan with the song.</p>
    </div>
  </section>
</template>

<style scoped>
.vocal-profile-proposal { display: grid; gap: .75rem; padding: 1rem; border: 1px solid #54614d; border-radius: .45rem; background: #10150f; }
h3, h4, p { margin: 0; }
p, li, small { color: #abb4a4; line-height: 1.5; }
strong { color: #e6cf91; }
.artist-notes { white-space: pre-wrap; overflow-wrap: anywhere; }
.proposal-actions { display: flex; flex-wrap: wrap; gap: .5rem; }
.proposal-review, .proposal-audition { display: grid; gap: .75rem; }
.proposal-review > ol { margin: 0; padding-left: 1.4rem; }
.proposal-review > ol > li { margin: .5rem 0; padding: .6rem; background: #181e16; }
summary { cursor: pointer; font-weight: 650; padding: .6rem 0; }
details p { margin: .5rem 0; }
label { display: grid; gap: .4rem; }
select { max-width: 100%; }
</style>
