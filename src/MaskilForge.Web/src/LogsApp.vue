<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { activityLog, formatLogEntries, type LogEntry, type LogLevel } from './logging'
import { remoteActivityLogSessionLabel, remoteActivityLogSessionOptions } from './remoteActivityLogModel.js'
import { listRemoteActivityLogSessions, readRemoteActivityLogSession, removeRemoteActivityLogSession, type RemoteActivityLogSessionSummary } from './remoteActivityLogs'

const entries = ref<LogEntry[]>([])
const levelFilter = ref<LogLevel | 'all'>('all')
const copyStatus = ref('Copy logs')
const logEnd = ref<HTMLElement | null>(null)
const source = ref('local')
const remoteSessions = ref<RemoteActivityLogSessionSummary[]>([])
const remoteStatus = ref('Checking for remote device sessions…')
const clearStatus = ref('')
const clearBusy = ref(false)
const removeRemoteOpen = ref(false)
const removeRemoteCancelButton = ref<HTMLButtonElement | null>(null)
let remotePollBusy = false
let remoteLogVersion = 0

const filteredEntries = computed(() => levelFilter.value === 'all'
  ? entries.value
  : entries.value.filter(entry => entry.level === levelFilter.value))
const selectedRemoteSession = computed(() => remoteSessions.value.find(session => session.sessionId === source.value) ?? null)
const remoteSessionOptions = computed(() => remoteActivityLogSessionOptions(
  remoteSessions.value,
  value => new Date(value).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', second: '2-digit' }),
))
const sourceDescription = computed(() => {
  if (source.value === 'local') return 'Entries stored in this browser.'
  if (!selectedRemoteSession.value) return remoteStatus.value
  const lastSeen = new Date(selectedRemoteSession.value.lastSeenUtc).toLocaleTimeString()
  return `${remoteActivityLogSessionLabel(selectedRemoteSession.value)} · last event ${lastSeen}. In-memory development telemetry only.`
})

function refreshLocal(scrollToEnd = true) {
  if (source.value !== 'local') return
  entries.value = activityLog.read()
  if (scrollToEnd) void nextTick(() => logEnd.value?.scrollIntoView({ block: 'end' }))
}

async function pollRemoteSessions(selectPhone = false) {
  if (remotePollBusy) return
  remotePollBusy = true
  const logVersion = remoteLogVersion
  try {
    remoteSessions.value = await listRemoteActivityLogSessions()
    remoteStatus.value = remoteSessions.value.length
      ? `${remoteSessions.value.length} remote development session${remoteSessions.value.length === 1 ? '' : 's'} available.`
      : 'Waiting for an editor browser to connect and write its first activity event.'
    if (selectPhone && source.value === 'local') {
      const preferred = remoteSessions.value.find(session => session.deviceKind === 'phone') ?? remoteSessions.value[0]
      if (preferred) source.value = preferred.sessionId
    }
    if (source.value !== 'local') {
      const session = await readRemoteActivityLogSession(source.value)
      if (logVersion !== remoteLogVersion) return
      entries.value = session.entries
      await nextTick(() => logEnd.value?.scrollIntoView({ block: 'end' }))
    }
  } catch (error) {
    if (logVersion !== remoteLogVersion) return
    remoteSessions.value = []
    remoteStatus.value = error instanceof Error ? error.message : 'Remote device logs are unavailable.'
    if (source.value !== 'local') entries.value = []
  } finally {
    remotePollBusy = false
  }
}

async function copyLogs() {
  const text = formatLogEntries(filteredEntries.value)
  if (!text) { copyStatus.value = 'No logs to copy'; return }
  try {
    await navigator.clipboard.writeText(text)
    copyStatus.value = `Copied ${filteredEntries.value.length} logs`
  } catch {
    copyStatus.value = 'Copy failed'
  }
  window.setTimeout(() => { copyStatus.value = 'Copy logs' }, 1_800)
}

async function clearLogs() {
  if (source.value !== 'local' || clearBusy.value || entries.value.length === 0) return
  clearBusy.value = true
  clearStatus.value = ''
  try {
    activityLog.clear()
    entries.value = []
    clearStatus.value = 'Logs cleared'
  } catch (error) {
    clearStatus.value = error instanceof Error ? error.message : 'Logs could not be cleared.'
  } finally {
    clearBusy.value = false
    window.setTimeout(() => { clearStatus.value = '' }, 2_500)
  }
}

function requestRemoveRemoteSession() {
  if (!selectedRemoteSession.value || clearBusy.value) return
  removeRemoteOpen.value = true
  void nextTick(() => removeRemoteCancelButton.value?.focus())
}

function cancelRemoveRemoteSession() {
  removeRemoteOpen.value = false
}

async function confirmRemoveRemoteSession() {
  const target = selectedRemoteSession.value
  if (!target || clearBusy.value) { removeRemoteOpen.value = false; return }
  clearBusy.value = true
  clearStatus.value = ''
  try {
    await removeRemoteActivityLogSession(target.sessionId)
    remoteLogVersion += 1
    const remaining = remoteSessions.value.filter(session => session.sessionId !== target.sessionId)
    remoteSessions.value = remaining
    removeRemoteOpen.value = false
    entries.value = []
    source.value = remaining[0]?.sessionId ?? 'local'
    clearStatus.value = 'Session and logs removed'
  } catch (error) {
    clearStatus.value = error instanceof Error ? error.message : 'Remote device session could not be removed.'
  } finally {
    clearBusy.value = false
    window.setTimeout(() => { clearStatus.value = '' }, 2_500)
  }
}

let unsubscribe: (() => void) | undefined
let remotePollTimer: number | undefined
onMounted(() => {
  refreshLocal()
  unsubscribe = activityLog.subscribe(() => refreshLocal())
  void pollRemoteSessions(true)
  remotePollTimer = window.setInterval(() => void pollRemoteSessions(), 1_000)
})
watch(source, next => {
  if (next === 'local') refreshLocal()
  else void pollRemoteSessions()
})
onBeforeUnmount(() => {
  unsubscribe?.()
  if (remotePollTimer !== undefined) window.clearInterval(remotePollTimer)
})
</script>

<template>
  <main class="console-page">
    <header class="console-toolbar">
      <div>
        <p class="eyebrow">Developer activity</p>
        <h1>Maskil Forge Console</h1>
        <p>{{ filteredEntries.length }} visible · {{ entries.length }} total</p>
        <small class="console-source-detail">{{ sourceDescription }}</small>
      </div>
      <div class="console-controls">
        <label>Source
          <select v-model="source">
            <option value="local">This browser</option>
            <option v-for="option in remoteSessionOptions" :key="option.sessionId" :value="option.sessionId">
              {{ option.label }}
            </option>
          </select>
        </label>
        <label>Level
          <select v-model="levelFilter">
            <option value="all">All levels</option>
            <option value="info">Info</option>
            <option value="success">Success</option>
            <option value="warning">Warnings</option>
            <option value="error">Errors</option>
          </select>
        </label>
        <button class="secondary" @click="copyLogs">{{ copyStatus }}</button>
        <button v-if="source === 'local'" class="danger clear-logs" :disabled="clearBusy || entries.length === 0" @click="clearLogs">{{ clearBusy ? 'Clearing…' : 'Clear logs' }}</button>
        <button v-else class="danger clear-logs" :disabled="clearBusy || !selectedRemoteSession" @click="requestRemoveRemoteSession">Remove session</button>
        <a class="console-link" href="/">Back to editor</a>
        <span class="console-operation-status" role="status">{{ clearStatus }}</span>
      </div>
    </header>

    <section class="console-stream" aria-live="polite" aria-label="Application activity log">
      <p v-if="filteredEntries.length === 0" class="console-empty">{{ source === 'local' ? 'No activity has been logged yet. Use the editor to create, load, save, or change a project.' : selectedRemoteSession ? 'No logs in this session. New device activity will appear here.' : remoteStatus }}</p>
      <article v-for="entry in filteredEntries" :key="entry.id" class="log-entry" :class="`level-${entry.level}`">
        <time :datetime="entry.timestamp">{{ new Date(entry.timestamp).toLocaleString() }}</time>
        <strong>{{ entry.action }}</strong>
        <span class="level-badge">{{ entry.level }}</span>
        <p>{{ entry.message }}</p>
        <pre v-if="entry.details">{{ JSON.stringify(entry.details, null, 2) }}</pre>
      </article>
      <div ref="logEnd" />
    </section>
  </main>

  <div v-if="removeRemoteOpen" class="modal-backdrop" role="presentation" @click.self="cancelRemoveRemoteSession">
    <section class="load-dialog delete-dialog" role="alertdialog" aria-modal="true" aria-labelledby="remove-remote-session-title" aria-describedby="remove-remote-session-description">
      <p class="eyebrow">Transient telemetry</p>
      <h2 id="remove-remote-session-title">Remove this device session?</h2>
      <p id="remove-remote-session-description">Its development logs will be deleted from Katana. If the browser is still active, its next Forge action will register a fresh session.</p>
      <p v-if="selectedRemoteSession"><strong>{{ remoteActivityLogSessionLabel(selectedRemoteSession) }}</strong> · last event {{ new Date(selectedRemoteSession.lastSeenUtc).toLocaleTimeString() }}</p>
      <div class="dialog-actions">
        <button ref="removeRemoteCancelButton" class="secondary" :disabled="clearBusy" @click="cancelRemoveRemoteSession">Keep session</button>
        <button class="danger" :disabled="clearBusy" @click="confirmRemoveRemoteSession">{{ clearBusy ? 'Removing…' : 'Remove session and logs' }}</button>
      </div>
    </section>
  </div>
</template>
