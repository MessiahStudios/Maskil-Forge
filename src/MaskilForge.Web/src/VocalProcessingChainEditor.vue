<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { projectsApi, type VocalProcessingChain, type VocalProcessingRole, type VocalProcessingRoleCatalog } from './api'

const props = defineProps<{ projectId: string; chain: VocalProcessingChain | null; busy: boolean }>()
const emit = defineEmits<{ set: [roles: VocalProcessingRole[]]; clear: [] }>()
const catalog = ref<VocalProcessingRoleCatalog | null>(null)
const loading = ref(false)
const error = ref('')
const draft = ref<VocalProcessingRole[]>([])
watch([() => props.projectId, () => props.chain?.updatedUtc], () => {
  draft.value = [...(props.chain?.roles ?? [])]
}, { immediate: true })
const changed = computed(() => draft.value.join('|') !== (props.chain?.roles ?? []).join('|'))
const roleName = (id: VocalProcessingRole) => catalog.value?.roles.find(role => role.id === id)?.name ?? id

async function loadCatalog() {
  loading.value = true
  error.value = ''
  try { catalog.value = await projectsApi.vocalProcessingRoles() }
  catch { error.value = 'Production jobs could not be loaded. Reconnect to the local host and try again.' }
  finally { loading.value = false }
}

function move(index: number, offset: number) {
  const destination = index + offset
  if (props.busy || destination < 0 || destination >= draft.value.length) return
  const next = [...draft.value]
  const [role] = next.splice(index, 1)
  if (role) next.splice(destination, 0, role)
  draft.value = next
}

onMounted(loadCatalog)
</script>

<template>
  <section class="vocal-processing-chain" aria-labelledby="vocal-processing-chain-title">
    <p class="eyebrow">Vocal production plan</p>
    <h3 id="vocal-processing-chain-title">The jobs behind your vocal sound</h3>
    <p>Choose the jobs you want to plan, then put them in order. Your vocal direction stays separate, so revising it keeps these choices intact.</p>
    <p v-if="!chain" class="chain-summary">No production jobs chosen yet.</p>
    <div v-else class="chain-summary">
      <strong>Current production order</strong>
      <ol aria-label="Current vocal production order">
        <li v-for="role in chain.roles" :key="role">{{ roleName(role) }}</li>
      </ol>
    </div>
    <details class="chain-edit">
      <summary>Choose and order production jobs</summary>
      <p v-if="loading" role="status">Loading production jobs…</p>
      <p v-if="error" role="alert">{{ error }} <button type="button" :disabled="loading || busy" @click="loadCatalog">Retry</button></p>
      <form v-if="catalog" @submit.prevent="emit('set', [...draft])">
        <fieldset :disabled="busy" class="chain-role-choices">
          <legend>Choose the jobs you want to plan</legend>
          <label v-for="role in catalog.roles" :key="role.id">
            <input v-model="draft" type="checkbox" :value="role.id">
            <span><strong>{{ role.name }}</strong><small>{{ role.purpose }}</small></span>
          </label>
        </fieldset>
        <div v-if="draft.length" class="chain-draft-order">
          <strong>Planned order</strong>
          <ol aria-label="Draft vocal production order">
            <li v-for="(role, index) in draft" :key="role">
              <span>{{ index + 1 }}. {{ roleName(role) }}</span>
              <div>
                <button type="button" :disabled="busy || index === 0" :aria-label="`Move ${roleName(role)} earlier`" @click="move(index, -1)">↑ Earlier</button>
                <button type="button" :disabled="busy || index === draft.length - 1" :aria-label="`Move ${roleName(role)} later`" @click="move(index, 1)">↓ Later</button>
              </div>
            </li>
          </ol>
        </div>
        <p v-if="changed" role="status">Job choices have not been applied to the song yet.</p>
        <div class="chain-actions">
          <button type="submit" :disabled="busy || !draft.length || !changed">{{ chain ? 'Update production jobs' : 'Set production jobs' }}</button>
          <button v-if="changed" type="button" class="quiet" :disabled="busy" @click="draft = [...(chain?.roles ?? [])]">Reset choices</button>
          <button v-if="chain" type="button" class="quiet" :disabled="busy" @click="emit('clear')">Clear production jobs</button>
        </div>
        <details class="chain-techniques">
          <summary>About these production roles</summary>
          <dl><template v-for="role in catalog.roles" :key="role.id"><dt>{{ role.name }}</dt><dd>{{ role.technique }}</dd></template></dl>
          <p>Character compression shapes energy and color. Transparent level control keeps levels consistent. Space describes the vocal's depth; song-level routing is separate.</p>
        </details>
      </form>
    </details>
    <p class="chain-boundary">This is a plan for later production. Setting jobs does not change your recordings. Use Save to keep the plan with your song.</p>
  </section>
</template>

<style scoped>
.vocal-processing-chain { display: grid; gap: .75rem; padding: 1rem; background: #10150f; border: 1px solid #54614d; border-radius: .45rem; }
h3, p { margin: 0; }
p, small { color: #abb4a4; line-height: 1.5; }
.chain-summary { color: #e6cf91; }
.chain-summary ol { margin: .4rem 0 0; padding-left: 1.4rem; }
summary { cursor: pointer; padding: .65rem 0; font-weight: 650; }
form { display: grid; gap: .85rem; }
.chain-role-choices { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem; margin: 0; padding: .75rem; border: 1px solid #495345; }
.chain-role-choices label { display: flex; gap: .5rem; align-items: flex-start; padding: .65rem; background: #181e16; border: 1px solid #3d4939; border-radius: .3rem; cursor: pointer; }
.chain-role-choices label:has(input:checked) { background: #293023; border-color: #a78c52; }
.chain-role-choices input { margin-top: .2rem; accent-color: #b99b56; }
.chain-role-choices span { display: grid; gap: .2rem; }
.chain-draft-order ol { display: grid; gap: .4rem; padding: 0; list-style: none; }
.chain-draft-order li { display: flex; align-items: center; justify-content: space-between; gap: .5rem; padding: .5rem; background: #181e16; }
.chain-draft-order li > div, .chain-actions { display: flex; flex-wrap: wrap; gap: .4rem; }
.chain-techniques { border-top: 1px solid #394236; }
.chain-techniques dl { display: grid; grid-template-columns: 1fr 1fr; gap: .5rem; }
.chain-techniques dd { margin: 0; color: #abb4a4; }
.chain-boundary { border-top: 1px solid #394236; padding-top: .7rem; font-size: .8rem; }
@media (max-width: 900px) {
  .chain-role-choices { grid-template-columns: 1fr; }
  .chain-draft-order li { align-items: flex-start; flex-direction: column; }
}
</style>
