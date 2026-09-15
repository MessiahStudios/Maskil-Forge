<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { projectsApi, type Vst3DiscoveryResult } from './api'
import { reviewPluginInventory, type ArchitectureFilter } from './pluginInventoryReview.js'

const result = ref<Vst3DiscoveryResult | null>(null)
const loading = ref(false), error = ref('')
const query = ref(''), architecture = ref<ArchitectureFilter>('all'), repeatedOnly = ref(false)
const inventory = computed(() => reviewPluginInventory(result.value, { query: query.value, architecture: architecture.value, repeatedOnly: repeatedOnly.value }))
const filtersActive = computed(() => Boolean(query.value.trim() || architecture.value !== 'all' || repeatedOnly.value))
function resetFilters() { query.value = ''; architecture.value = 'all'; repeatedOnly.value = false }
let generation = 0
let controller: AbortController | null = null
const statuses: Record<string, string> = { Complete: 'Checked', Missing: 'Folder not installed', Partial: 'Partially checked', Unavailable: 'Folder could not be read', SkippedLink: 'Linked folder skipped' }
const issues: Record<string, string> = { EntryLimit: 'Folder entry limit reached', CandidateLimit: 'Candidate limit reached', DepthLimit: 'Deep folders skipped', LinkedEntry: 'Linked entries skipped', UnreadableEntry: 'Some entries could not be read' }
const metadataStatuses: Record<string, string> = {
  Available: 'Reported metadata available', Missing: 'Optional metadata not supplied', NotApplicable: 'Metadata inspection is available for bundles only',
  LinkedPath: 'Metadata skipped because its path contains a link', Unreadable: 'Metadata could not be read',
  TooLarge: 'Metadata exceeds the 128 KiB limit', ScanLimit: 'Metadata read limit reached; details were not inspected',
  InvalidOrUnsupported: 'Metadata is invalid or uses an unsupported format. This does not establish that the plugin is broken.',
}
const binaryStatuses: Record<string, string> = { Recognized: 'Library header recognized', Invalid: 'Truncated or invalid header', Unsupported: 'Header format or layout not supported by this check', Unreadable: 'Header could not be read', LinkedPath: 'Linked binary path skipped' }
const binaryMatches: Record<string, string> = { Match: 'Header format and CPU match this host process', Different: 'Header format or CPU differs from this host process', Unknown: 'Host match is undetermined' }
function clear(reset = true) {
  generation++
  controller?.abort()
  controller = null
  result.value = null
  error.value = ''
  loading.value = false
  if (reset) resetFilters()
}
async function scan() {
  clear(false)
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
onBeforeUnmount(() => clear())
</script>

<template>
  <details class="vst3-discovery" aria-label="VST3 discovery on the Maskil host" @toggle="event => { if (!(event.target as HTMLDetailsElement).open) clear() }">
    <summary>Find VST3 plugins on the host</summary>
    <p>Check the computer running the Maskil Forge project service. If you opened this page from another device, that device's plugins are outside this scan. A DAW does not need to be installed.</p>
    <p>Discovery lists .vst3 files and bundles in standard local folders. Plugin loading and playback are not available yet. Finding a candidate does not verify its compatibility, license, or the instruments and effects inside it.</p>
    <p>When a bundle supplies an optional metadata file, its reported name, vendor, version, and classes appear below. These declarations have not been checked against the plugin's code.</p>
    <div class="discovery-actions">
      <button type="button" :disabled="loading" @click="scan">{{ loading ? 'Checking plugin folders…' : 'Check plugin folders' }}</button>
      <button v-if="loading || result" type="button" class="quiet" @click="clear()">{{ loading ? 'Cancel scan' : 'Clear results' }}</button>
    </div>
    <p v-if="error" role="alert">{{ error }}</p>
    <div v-if="result" class="discovery-results">
      <p role="status">Showing {{ inventory.visibleCount }} of {{ inventory.totalCount }} VST3 candidates found on the {{ result.platform }} host. Compatibility has not been checked.</p>
      <p>Checked {{ new Date(result.scannedUtc).toLocaleString() }}. Check again after installing or removing plugins.</p>
      <div class="inventory-filters" role="search" aria-label="Filter discovered plugins">
        <label>Search plugins<input v-model="query" type="search" maxlength="256" placeholder="Name, vendor, version, class, or location" /></label>
        <label>Binary architecture<select v-model="architecture">
          <option value="all">All results</option>
          <option value="match">At least one matching header</option>
          <option value="different">All inspected headers differ</option>
          <option value="undetermined">Undetermined or incomplete</option>
        </select></label>
        <label><input v-model="repeatedOnly" type="checkbox" /> Repeated reported class IDs only</label>
        <button v-if="filtersActive" type="button" class="quiet" @click="resetFilters">Reset filters</button>
      </div>
      <p v-if="!inventory.visibleCount && inventory.totalCount">No candidates match these filters. Folder status and repeated-ID findings below still describe the full scan.</p>
      <details v-if="inventory.repeatedClasses.length" class="plugin-metadata" aria-label="Repeated reported class IDs">
        <summary>{{ inventory.repeatedClasses.length }} class {{ inventory.repeatedClasses.length === 1 ? 'ID' : 'IDs' }} reported by multiple candidates</summary>
        <p>Across the full scan, including candidates hidden by filters. These unverified declarations may represent copies or versions; they do not prove a conflict or identify which installation should be used. Processor and controller IDs are both included.</p>
        <ul>
          <li v-for="group in inventory.repeatedClasses" :key="group.id">
            <strong>Reported class ID: {{ group.id }}</strong>
            <ul><li v-for="occurrence in group.occurrences" :key="occurrence.key">
              <span>{{ occurrence.location }} · {{ occurrence.path }}</span>
              <span>{{ occurrence.name }} · {{ occurrence.category }} · Version: {{ occurrence.version ?? 'Not reported' }}</span>
            </li></ul>
          </li>
        </ul>
      </details>
      <p v-if="!result.locations.length">Standard folders are not defined for this host platform yet.</p>
      <section v-for="location in inventory.locations" :key="location.name" :aria-label="`${location.name} plugin folder`">
        <h4>{{ location.name }} · {{ statuses[location.status] ?? location.status }}</h4>
        <p class="location-hint">{{ location.hint }}</p>
        <p v-if="location.issues.length">{{ location.issues.map(issue => issues[issue] ?? issue).join(' · ') }}. Results may be incomplete.</p>
        <ul v-if="location.rows.length">
          <li v-for="{ candidate, key, repeatedClassCount } in location.rows" :key="key">
            <strong>{{ candidate.name }}</strong>
            <span>{{ candidate.kind === 'Bundle' ? 'Bundle' : 'File' }} · Unverified</span>
            <small>{{ candidate.relativePath }}</small>
            <span v-if="repeatedClassCount">{{ repeatedClassCount }} reported class {{ repeatedClassCount === 1 ? 'ID also occurs' : 'IDs also occur' }} in other candidates. Review “Repeated reported class IDs” above.</span>
            <span>{{ metadataStatuses[candidate.metadata.status] ?? candidate.metadata.status }}</span>
            <details class="plugin-metadata" :aria-label="`Binary headers for ${candidate.relativePath}`">
              <summary>Check binary architecture</summary>
              <p>Running host: {{ candidate.binary.hostPlatform }} · {{ candidate.binary.hostArchitecture }} process</p>
              <p v-if="candidate.binary.status === 'ScanLimit'">Binary inspection limit reached. This candidate's headers were not read.</p>
              <p v-else-if="candidate.binary.status === 'NoConventionalBinary'">No binary found in the conventional layouts checked. Custom layouts and macOS executable names declared only in Info.plist are not resolved yet.</p>
              <ul v-else aria-label="Inspected binary headers">
                <li v-for="binary in candidate.binary.files" :key="binary.source">
                  <strong>{{ binary.source }}</strong>
                  <span>{{ binaryStatuses[binary.status] ?? binary.status }}</span>
                  <span v-if="binary.format">{{ binary.format }} · {{ binary.architectures.join(', ') || 'CPU not identified' }}</span>
                  <span>{{ binaryMatches[binary.hostMatch] ?? binary.hostMatch }}</span>
                </li>
              </ul>
              <p>This compares header format and CPU only. It does not validate the complete binary, VST entry points, OS version, dependencies, signatures, licensing, or playback. Emulation and hybrid Windows/Arm loading are not inferred. No plugin code was loaded.</p>
            </details>
            <details v-if="candidate.metadata.module" class="plugin-metadata" :aria-label="`Reported metadata for ${candidate.relativePath}`">
              <summary>Reported plugin details</summary>
              <dl>
                <dt>Module name</dt><dd>{{ candidate.metadata.module.name }}</dd>
                <dt>Module version</dt><dd>{{ candidate.metadata.module.version }}</dd>
                <dt>Factory vendor</dt><dd>{{ candidate.metadata.module.vendor }}</dd>
              </dl>
              <p>{{ candidate.metadata.module.classes.length }} reported classes. Modules can include processor and controller classes; this is not a count of playable instruments.</p>
              <ul aria-label="Reported plugin classes">
                <li v-for="pluginClass in candidate.metadata.module.classes" :key="pluginClass.id">
                  <strong>{{ pluginClass.name }}</strong>
                  <span>{{ pluginClass.category }}</span>
                  <span>Vendor: {{ pluginClass.vendor ?? 'Not reported' }} · Version: {{ pluginClass.version ?? 'Not reported' }}</span>
                  <span v-if="pluginClass.subCategories.length">Reported categories: {{ pluginClass.subCategories.join(' · ') }}</span>
                  <small>Class ID: {{ pluginClass.id }} · SDK: {{ pluginClass.sdkVersion ?? 'Not reported' }}</small>
                </li>
              </ul>
              <small>Metadata source: {{ candidate.metadata.source }}</small>
              <small>Metadata SHA-256: {{ candidate.metadata.sha256 }}</small>
              <p>The digest identifies the metadata bytes only. It does not verify the executable or its publisher.</p>
            </details>
          </li>
        </ul>
        <p v-else-if="location.totalCount">No candidates in this folder match the current filters.</p>
        <p v-else-if="location.status === 'Complete'">No .vst3 candidates in this folder.</p>
      </section>
      <p>Metadata inspection supports UTF-8 JSON with comments and trailing commas; other JSON5 syntax may be reported as unsupported. At most 64 manifest files are read per scan. Missing details do not prevent a candidate from being listed.</p>
      <p>Binary checks inspect bounded headers for at most 128 candidates per scan. Reported metadata and binary-header findings remain separate; neither selects a renderer.</p>
      <p>Linked entries are skipped. Custom folders and the macOS network plugin location are outside this scan. Copies remain separate because plugin identities have not been verified. Results are temporary and do not change your song or renderer.</p>
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
.plugin-metadata { padding: .6rem; border-left: 2px solid #54614d; }
.plugin-metadata small { display: block; }
dl { display: grid; grid-template-columns: minmax(6rem, 1fr) 2fr; gap: .4rem; }
dd { margin: 0; }
.inventory-filters { display: flex; flex-wrap: wrap; align-items: end; gap: .8rem; margin: 1rem 0; }
.inventory-filters label { display: grid; gap: .4rem; }
.inventory-filters label:has(input[type='checkbox']) { display: flex; align-items: center; }
.inventory-filters input[type='search'], .inventory-filters select { max-width: 100%; min-width: 13rem; }
</style>
