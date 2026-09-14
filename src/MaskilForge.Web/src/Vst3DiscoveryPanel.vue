<script setup lang="ts">
import { onBeforeUnmount, ref } from 'vue'
import { projectsApi, type Vst3DiscoveryResult } from './api'

const result = ref<Vst3DiscoveryResult | null>(null)
const loading = ref(false), error = ref('')
let generation = 0
let controller: AbortController | null = null
const statuses: Record<string, string> = { Complete: 'Checked', Missing: 'Folder not installed', Partial: 'Partially checked', Unavailable: 'Folder could not be read', SkippedLink: 'Linked folder skipped' }
const issues: Record<string, string> = { EntryLimit: 'Folder entry limit reached', CandidateLimit: 'Candidate limit reached', DepthLimit: 'Deep folders skipped', LinkedEntry: 'Linked entries skipped', UnreadableEntry: 'Some entries could not be read' }
function clear() {
  generation++
  controller?.abort()
  controller = null
  result.value = null
  error.value = ''
  loading.value = false
}
async function scan() {
  clear()
  const token = generation
  controller = new AbortController()
  loading.value = true
  try {
    const inventory = await projectsApi.discoverVst3(controller.signal)
    if (token === generation) result.value = inventory
  } catch (cause) {
    if (token === generation) error.value = cause instanceof Error ? cause.message : 'Plugin folders could not be checked.'
  } finally {
    if (token === generation) { loading.value = false; controller = null }
  }
}
onBeforeUnmount(clear)
</script>

<template>
  <details class="vst3-discovery" aria-label="VST3 discovery on the Maskil host" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) clear() }">
    <summary>Find VST3 plugins on the host</summary>
    <p>Check the computer running the Maskil Forge project service. If you opened this page from another device, that device's plugins are outside this scan. A DAW does not need to be installed.</p>
    <p>Discovery lists .vst3 files and bundles in standard local folders. Plugin loading and playback are not available yet. Finding a candidate does not verify its compatibility, license, or the instruments and effects inside it.</p>
    <div class="discovery-actions">
      <button type="button" :disabled="loading" @click="scan">{{ loading ? 'Checking plugin folders…' : 'Check plugin folders' }}</button>
      <button v-if="loading || result" type="button" class="quiet" @click="clear">{{ loading ? 'Cancel scan' : 'Clear results' }}</button>
    </div>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="result" class="discovery-results">
      <p role="status">{{ result.locations.reduce((count, location) => count + location.candidates.length, 0) }} VST3 candidates found on the {{ result.platform }} host. Compatibility has not been checked.</p>
      <p>Checked {{ new Date(result.scannedUtc).toLocaleString() }}. Check again after installing or removing plugins.</p>
      <p v-if="!result.locations.length">Standard folders are not defined for this host platform yet.</p>
      <section v-for="location in result.locations" :key="location.name" :aria-label="`${location.name} plugin folder`">
        <h4>{{ location.name }} · {{ statuses[location.status] ?? location.status }}</h4>
        <p class="location-hint">{{ location.hint }}</p>
        <p v-if="location.issues.length">{{ location.issues.map(issue => issues[issue] ?? issue).join(' · ') }}. Results may be incomplete.</p>
        <ul v-if="location.candidates.length">
          <li v-for="candidate in location.candidates" :key="candidate.relativePath">
            <strong>{{ candidate.name }}</strong>
            <span>{{ candidate.kind === 'Bundle' ? 'Bundle' : 'File' }} · Unverified</span>
            <small>{{ candidate.relativePath }}</small>
          </li>
        </ul>
        <p v-else-if="location.status === 'Complete'">No .vst3 candidates in this folder.</p>
      </section>
      <p>Linked entries are skipped. Custom folders and the macOS network plugin location are outside this scan. Copies remain separate because plugin identities have not been inspected. Results are temporary and do not change your song or renderer.</p>
    </div>
  </details>
</template>

<style scoped>
.vst3-discovery { margin: 1rem 0; padding: .9rem; border: 1px solid #54614d; border-radius: .4rem; background: #10150f; }
summary { cursor: pointer; padding: .4rem 0; font-weight: 650; }
p, span, small { color: #abb4a4; line-height: 1.5; }
.discovery-actions { display: flex; flex-wrap: wrap; gap: .5rem; }
.discovery-results { max-height: 36rem; overflow-y: auto; }
section { border-top: 1px solid #54614d; margin-top: .8rem; }
li { display: grid; gap: .3rem; margin: .6rem 0; }
li, .location-hint { overflow-wrap: anywhere; }
strong { color: #e6cf91; }
</style>
