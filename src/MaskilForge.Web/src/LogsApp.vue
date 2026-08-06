<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { activityLog, formatLogEntries, type LogEntry, type LogLevel } from './logging'

const entries = ref<LogEntry[]>([])
const levelFilter = ref<LogLevel | 'all'>('all')
const copyStatus = ref('Copy logs')
const logEnd = ref<HTMLElement | null>(null)

const filteredEntries = computed(() => levelFilter.value === 'all'
  ? entries.value
  : entries.value.filter(entry => entry.level === levelFilter.value))

function refresh(scrollToEnd = true) {
  entries.value = activityLog.read()
  if (scrollToEnd) void nextTick(() => logEnd.value?.scrollIntoView({ block: 'end' }))
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

function clearLogs() {
  activityLog.clear()
  entries.value = []
}

let unsubscribe: (() => void) | undefined
onMounted(() => {
  refresh()
  unsubscribe = activityLog.subscribe(() => refresh())
})
onBeforeUnmount(() => unsubscribe?.())
</script>

<template>
  <main class="console-page">
    <header class="console-toolbar">
      <div>
        <p class="eyebrow">Developer activity</p>
        <h1>Maskil Forge Console</h1>
        <p>{{ filteredEntries.length }} visible · {{ entries.length }} total</p>
      </div>
      <div class="console-controls">
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
        <button class="danger clear-logs" @click="clearLogs">Clear logs</button>
        <a class="console-link" href="/">Back to editor</a>
      </div>
    </header>

    <section class="console-stream" aria-live="polite" aria-label="Application activity log">
      <p v-if="filteredEntries.length === 0" class="console-empty">No activity has been logged yet. Use the editor to create, load, save, or change a project.</p>
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
</template>
