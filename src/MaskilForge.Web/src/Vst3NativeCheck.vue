<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { projectsApi, type Vst3DiscoveryResult, type Vst3NativeCheckResult, type Vst3NativeFactoryCheckResult } from './api'

type Candidate = Vst3DiscoveryResult['locations'][number]['candidates'][number]
const props = defineProps<{ location: string; candidate: Candidate }>()
const result = ref<Vst3NativeCheckResult | null>(null), factoryResult = ref<Vst3NativeFactoryCheckResult | null>(null), busy = ref(false), factoryBusy = ref(false), error = ref('')
let controller: AbortController | null = null
let generation = 0
const eligible = computed(() => {
  const declaration = props.candidate.binary.macExecutable
  return props.candidate.kind === 'Bundle' && declaration?.status === 'Available' && declaration.sha256 &&
    props.candidate.binary.files.some(file => file.source === `Contents/MacOS/${declaration.executable}` &&
      file.status === 'Recognized' && file.format === 'Mach-O' && file.hostMatch === 'Match')
})
const statuses: Record<string, string> = {
  Completed: 'Module load and entry/exit check completed', WorkerUnavailable: 'Native worker is unavailable on this host',
  Busy: 'Another native check is running. Try again when it finishes.', CandidateUnavailable: 'Candidate is no longer available. Check plugin folders again.',
  RescanRequired: 'The bundle declaration changed or cannot be used. Check plugin folders again.',
  HeaderNotMatched: 'The current executable header does not match this host', BinaryTooLarge: 'The binary exceeds the 512 MiB inspection limit',
  SourceChanged: 'The executable or declaration changed during the check. Discard this result and scan again.',
  TimedOut: 'The worker exceeded 10 seconds and was stopped', Cancelled: 'The check was cancelled',
  OutputLimit: 'The worker exceeded its output limit and was stopped', WorkerCrashed: 'The worker exited unexpectedly',
  InvalidWorkerOutput: 'The worker did not return a valid complete result', BundleOpenFailed: 'The worker could not open the bundle',
  ExecutableChanged: 'The native loader resolved a different executable', LoadFailed: 'macOS could not load the module',
  MissingEntryPoints: 'A required VST3 export was missing', EntryRejected: 'The module rejected bundle entry', ExitRejected: 'The module rejected bundle exit',
}
const factoryStatuses: Record<string, string> = { WorkerUnavailable: 'Native worker is unavailable on this host', Busy: 'Another native check is running', CandidateUnavailable: 'Candidate is no longer available', RescanRequired: 'The bundle changed; check plugin folders again', HeaderNotMatched: 'The current executable header does not match this host', TimedOut: 'Factory enumeration exceeded 10 seconds', Cancelled: 'Factory enumeration was cancelled', OutputLimit: 'Factory output exceeded its limit', WorkerCrashed: 'The factory worker exited unexpectedly', InvalidWorkerOutput: 'The factory worker did not return a valid result', FactoryUnavailable: 'The factory export returned no usable interface', FactoryClassLimit: 'The factory reported too many classes', FactoryClassReadFailed: 'A factory class could not be read' }
const stages: Record<string, string> = {
  NotStarted: 'Worker not started', WorkerStarted: 'Worker started', BundleOpened: 'Bundle opened', ModuleLoaded: 'Native module loaded',
  EntryPointsResolved: 'Required exports found', ModuleEntered: 'Bundle entry completed', ModuleExited: 'Bundle exit completed', ModuleUnloaded: 'Module cleanup completed',
}
function clear() {
  generation++
  controller?.abort()
  controller = null
  result.value = null
  factoryResult.value = null
  busy.value = false; factoryBusy.value = false
  error.value = ''
}
async function enumerateFactory() {
  clear()
  if (!eligible.value) return
  const token = generation; controller = new AbortController(); factoryBusy.value = true
  try {
    const outcome = await projectsApi.checkNativeVst3Factory(props.location, props.candidate.relativePath,
      props.candidate.binary.macExecutable!.sha256!, controller.signal)
    if (token === generation) factoryResult.value = outcome
  } catch (cause) { if (token === generation) error.value = cause instanceof Error ? cause.message : 'Factory enumeration failed.' }
  finally { if (token === generation) { factoryBusy.value = false; controller = null } }
}
async function check() {
  clear()
  if (!eligible.value) return
  const token = generation
  controller = new AbortController()
  busy.value = true
  try {
    const outcome = await projectsApi.checkNativeVst3(props.location, props.candidate.relativePath,
      props.candidate.binary.macExecutable!.sha256!, controller.signal)
    if (token === generation) result.value = outcome
  } catch (cause) {
    if (token === generation) error.value = cause instanceof Error ? cause.message : 'Native check failed.'
  } finally {
    if (token === generation) { busy.value = false; controller = null }
  }
}
onBeforeUnmount(clear)
</script>

<template>
  <details class="native-check" :aria-label="`Native module check for ${candidate.relativePath}`"
    @toggle="event => { if (!(event.target as HTMLDetailsElement).open) clear() }">
    <summary>Check native module loading</summary>
    <p>This runs the installed plugin's module code in a separate process on this Mac, with a 10-second limit. It calls bundle entry and exit and looks for the factory export. It does not create a processor or play audio.</p>
    <p>The separate process contains crashes; it is not a security sandbox and runs with your user account's access. Run checks only for plugins you trust.</p>
    <p v-if="!eligible">A readable XML bundle declaration and a matching macOS binary header are required. Check plugin folders again after changing an installation.</p>
    <button type="button" :disabled="busy || factoryBusy || !eligible" @click="check">{{ busy ? 'Checking native module…' : 'Run native module check' }}</button>
    <button type="button" :disabled="busy || factoryBusy || !eligible" @click="enumerateFactory">{{ factoryBusy ? 'Reading factory classes…' : 'Enumerate factory classes' }}</button>
    <button v-if="busy || factoryBusy" type="button" class="quiet" @click="() => { clear(); error = 'Check cancelled.' }">Cancel check</button>
    <button v-else-if="result || factoryResult" type="button" class="quiet" @click="clear">Clear native result</button>
    <p v-if="error" role="status">{{ error }}</p>
    <div v-if="result" role="status">
      <p>{{ statuses[result.status] ?? result.status }}</p>
      <p>Last completed stage: {{ stages[result.lastCompletedStage] ?? result.lastCompletedStage }}</p>
      <small>Checked {{ new Date(result.checkedUtc).toLocaleString() }}</small>
      <small v-if="result.binarySource">Executable: {{ result.binarySource }}</small>
      <small v-if="result.binarySha256">Executable SHA-256: {{ result.binarySha256 }}</small>
      <small v-if="result.plistSha256">Info.plist SHA-256: {{ result.plistSha256 }}</small>
      <p>These fingerprints identify the inspected executable and declaration only. Factory creation, classes, dependencies, licensing, processor behavior, and audio compatibility remain unverified.</p>
    </div>
    <div v-if="factoryResult" role="status">
      <p>{{ factoryResult.status === 'Completed' ? 'Factory class enumeration completed.' : (factoryStatuses[factoryResult.status] ?? factoryResult.status) }} Last completed stage: {{ stages[factoryResult.lastCompletedStage] ?? factoryResult.lastCompletedStage }}</p>
      <p v-if="factoryResult.status === 'Completed'">{{ factoryResult.classes.length }} classes were returned by the native factory. No component instance was created.</p>
      <ul v-if="factoryResult.status === 'Completed'" aria-label="Native factory classes"><li v-for="pluginClass in factoryResult.classes" :key="pluginClass.id"><strong>{{ pluginClass.name }}</strong><span>{{ pluginClass.category }} · Class ID: {{ pluginClass.id }}</span></li></ul>
      <p>Class declarations are runtime evidence from this executable. They do not prove processor behavior, audio compatibility, licensing, or a suitable production role.</p>
    </div>
  </details>
</template>

<style scoped>
.native-check { padding: .6rem; border-left: 2px solid #54614d; overflow-wrap: anywhere; }
summary { cursor: pointer; padding: .4rem 0; font-weight: 650; }
p, small { color: #abb4a4; line-height: 1.5; }
small { display: block; }
button { margin: .25rem .5rem .25rem 0; }
</style>
