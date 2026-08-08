<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { projectsApi, type ProjectResponse, type ProjectSummary, type RecoverySummary, type SectionKind, type SongGenre, type SongProject, type TrashedProjectSummary } from './api'
import { activityLog } from './logging'

const response = ref<ProjectResponse | null>(null)
const projectId = ref(localStorage.getItem('maskilForge.projectId') ?? '')
const view = ref<'home' | 'recovery' | 'trash' | 'capture' | 'structure'>('home')
const projects = ref<ProjectSummary[]>([])
const recoverySnapshots = ref<RecoverySummary[]>([])
const trashedProjects = ref<TrashedProjectSummary[]>([])
const libraryBusy = ref(true)
const status = ref('Begin a new song or open an existing project.')
const busy = ref(false)
const savedSnapshot = ref('')
const cleanLabel = ref<'clean' | 'saved'>('clean')
const confirmationOpen = ref(false)
const deleteConfirmationOpen = ref(false)
const deleteTarget = ref<{ id: string; title: string } | null>(null)
const permanentDeleteTarget = ref<{ id: string; title: string } | null>(null)
const pendingAction = ref<'load' | 'new' | 'home'>('load')
const pendingLoadId = ref('')
const sessionId = crypto.randomUUID()
const persistedRevision = ref('')
let recoveryTimer: ReturnType<typeof setTimeout> | undefined
const project = computed(() => response.value?.project ?? null)
const serializedProject = computed(() => project.value ? JSON.stringify(project.value) : '')
const isDirty = computed(() => Boolean(project.value) && serializedProject.value !== savedSnapshot.value)
const editorState = computed(() => isDirty.value ? 'Unsaved changes' : cleanLabel.value === 'saved' ? 'Saved' : 'No changes')
const meters = ['2/4', '3/4', '4/4', '5/4', '6/8', '7/8', '9/8', '12/8']
const genres: SongGenre[] = ['Unspecified', 'Pop', 'Rock', 'Folk', 'Country', 'RAndB', 'HipHop', 'Electronic', 'Cinematic', 'Alternative', 'Other']

function accept(next: ProjectResponse, message: string, markPersisted = false) {
  response.value = next
  projectId.value = next.project.id
  localStorage.setItem('maskilForge.projectId', next.project.id)
  status.value = message
  if (markPersisted) {
    savedSnapshot.value = JSON.stringify(next.project)
    persistedRevision.value = next.project.lastModifiedUtc
    cleanLabel.value = message.includes('saved') ? 'saved' : 'clean'
  }
}

async function run(action: () => Promise<ProjectResponse>, message: string, logAction: string, details?: Record<string, string | number | boolean | null>, markPersisted = false) {
  busy.value = true
  activityLog.write('info', logAction, 'Action requested.', details)
  try {
    accept(await action(), message, markPersisted)
    activityLog.write('success', logAction, message, { projectId: projectId.value, ...details })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The request failed.'
    activityLog.write('error', logAction, status.value, details)
    return false
  } finally {
    busy.value = false
  }
  return true
}

function requestNewProject() {
  if (isDirty.value) return openConfirmation('new')
  return createProject()
}
function createProject() {
  confirmationOpen.value = false
  return run(() => projectsApi.create('Untitled Song'), 'Capture the idea. Structure can come later.', 'project.create', undefined, true).then(succeeded => {
    if (succeeded) view.value = 'capture'
    return succeeded
  })
}
function requestLoad() {
  if (!projectId.value.trim()) {
    status.value = 'Enter a project ID to open.'
    activityLog.write('warning', 'project.load', status.value)
    return
  }
  pendingLoadId.value = projectId.value.trim()
  if (isDirty.value) return openConfirmation('load')
  return performLoad()
}
function openConfirmation(action: 'load' | 'new' | 'home') {
  pendingAction.value = action
  confirmationOpen.value = true
  activityLog.write('warning', `project.${action}`, 'Action paused because unsaved changes were detected.')
}
async function performLoad() {
  confirmationOpen.value = false
  const succeeded = await run(() => projectsApi.load(pendingLoadId.value), 'Song opened.', 'project.load', { projectId: pendingLoadId.value }, true)
  if (succeeded) view.value = project.value?.sections.length ? 'structure' : 'capture'
  return succeeded
}
async function continuePendingAction() {
  if (pendingAction.value === 'new') await createProject()
  else if (pendingAction.value === 'load') await performLoad()
  else await goHome()
}
async function saveBeforeContinuing() {
  if (await saveProject()) await continuePendingAction()
}
async function discardAndContinue() {
  activityLog.write('warning', `project.${pendingAction.value}`, 'Unsaved editor changes discarded by user.')
  if (project.value) await projectsApi.discardRecovery(project.value.id).catch(() => undefined)
  return await continuePendingAction()
}
function cancelConfirmation() {
  confirmationOpen.value = false
  status.value = 'Cancelled. Your unsaved changes remain in the editor.'
}
async function saveProject() {
  if (!project.value || !persistedRevision.value) return
  const succeeded = await run(() => projectsApi.save(project.value!, persistedRevision.value), 'Song saved.', 'project.save', { projectId: project.value.id, sectionCount: project.value.sections.length }, true)
  if (succeeded) await refreshRecovery()
  return succeeded
}
async function saveDraft() {
  const succeeded = await saveProject()
  if (succeeded) await refreshLibrary()
}
async function beginStructuring() {
  if (!project.value) return
  if (isDirty.value && !(await saveProject())) return
  view.value = 'structure'
  status.value = 'Your original lyric draft remains preserved while you shape the song.'
}
function returnToDraft() { view.value = 'capture'; status.value = 'Raw lyric draft.' }
function requestHome() { if (isDirty.value) return openConfirmation('home'); return goHome() }
async function goHome() { confirmationOpen.value = false; view.value = 'home'; await Promise.all([refreshLibrary(), refreshRecovery()]) }
function openSummary(id: string) { projectId.value = id; return requestLoad() }
function requestDelete(id: string, title: string) {
  deleteTarget.value = { id, title }
  deleteConfirmationOpen.value = true
  activityLog.write('warning', 'project.delete', 'Delete confirmation requested.', { projectId: id })
}
function cancelDelete() {
  deleteConfirmationOpen.value = false
  deleteTarget.value = null
  activityLog.write('info', 'project.delete', 'Song deletion cancelled.')
}
async function confirmDelete() {
  if (!deleteTarget.value) return
  const target = deleteTarget.value
  busy.value = true
  try {
    await projectsApi.delete(target.id)
    activityLog.write('success', 'project.delete', 'Song moved to Trash.', { projectId: target.id, title: target.title })
    deleteConfirmationOpen.value = false
    deleteTarget.value = null
    if (project.value?.id === target.id) {
      response.value = null
      savedSnapshot.value = ''
      localStorage.removeItem('maskilForge.projectId')
      view.value = 'home'
    }
    status.value = `“${target.title}” was moved to Trash.`
    await Promise.all([refreshLibrary(), refreshRecovery()])
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The song could not be deleted.'
    activityLog.write('error', 'project.delete', status.value, { projectId: target.id })
  } finally { busy.value = false }
}
async function openTrash() {
  view.value = 'trash'
  libraryBusy.value = true
  try { trashedProjects.value = await projectsApi.listTrash() }
  catch (error) { status.value = error instanceof Error ? error.message : 'Could not load Trash.' }
  finally { libraryBusy.value = false }
}
async function refreshRecovery() {
  try { recoverySnapshots.value = await projectsApi.listRecovery() }
  catch (error) { status.value = error instanceof Error ? error.message : 'Could not check recovery snapshots.' }
}
async function openRecovery() {
  view.value = 'recovery'
  await refreshRecovery()
}
async function restoreRecovery(id: string) {
  busy.value = true
  try {
    const recovered = await projectsApi.loadRecovery(id)
    response.value = { project: recovered.project, canUndo: false, canRedo: false }
    projectId.value = recovered.project.id
    localStorage.setItem('maskilForge.projectId', recovered.project.id)
    persistedRevision.value = recovered.baseProjectLastModifiedUtc
    savedSnapshot.value = ''
    view.value = recovered.project.sections.length ? 'structure' : 'capture'
    status.value = 'Recovered unsaved work. Save the song when you are ready.'
    activityLog.write('success', 'recovery.restore', 'Unsaved work restored.', { projectId: id })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The recovery snapshot could not be restored.'
  } finally { busy.value = false }
}
async function discardRecovery(id: string) {
  busy.value = true
  try {
    await projectsApi.discardRecovery(id)
    activityLog.write('info', 'recovery.discard', 'Recovery snapshot discarded.', { projectId: id })
    await refreshRecovery()
    if (recoverySnapshots.value.length === 0) await goHome()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The recovery snapshot could not be discarded.'
  } finally { busy.value = false }
}
async function restoreSong(id: string, title: string) {
  busy.value = true
  try {
    await projectsApi.restore(id)
    status.value = `“${title}” was restored to your song library.`
    activityLog.write('success', 'project.restore', 'Song restored from Trash.', { projectId: id, title })
    await openTrash()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The song could not be restored.'
  } finally { busy.value = false }
}
function requestPermanentDelete(id: string, title: string) { permanentDeleteTarget.value = { id, title } }
function cancelPermanentDelete() { permanentDeleteTarget.value = null }
async function confirmPermanentDelete() {
  if (!permanentDeleteTarget.value) return
  const target = permanentDeleteTarget.value
  busy.value = true
  try {
    await projectsApi.permanentlyDelete(target.id)
    permanentDeleteTarget.value = null
    status.value = `“${target.title}” was permanently deleted.`
    activityLog.write('success', 'project.permanent-delete', 'Song permanently deleted.', { projectId: target.id, title: target.title })
    await openTrash()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The song could not be permanently deleted.'
  } finally { busy.value = false }
}
async function refreshLibrary() {
  libraryBusy.value = true
  try { projects.value = await projectsApi.list() }
  catch (error) { status.value = error instanceof Error ? error.message : 'Could not load the song library.' }
  finally { libraryBusy.value = false }
}
function formatModified(value: string) { return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) }
async function addSection(kind: SectionKind) {
  if (!project.value) return
  const existingIds = new Set(project.value.sections.map(section => section.id))
  const succeeded = await run(() => projectsApi.command(project.value!.id, project.value!, { type: 'add-section', kind }), `${label(kind)} added. Start writing.`, 'section.add', { kind })
  if (!succeeded || !project.value) return
  const sectionIndex = project.value.sections.findIndex(section => !existingIds.has(section.id))
  if (sectionIndex >= 0) await addLyricLine(sectionIndex, true)
}
function renameSection(sectionId: string, title: string) {
  if (!project.value) return
  return run(() => projectsApi.command(project.value!.id, project.value!, { type: 'rename-section', sectionId, title }), 'Section renamed.', 'section.rename', { sectionId, title })
}
function moveSection(sectionId: string, targetIndex: number) {
  if (!project.value) return
  return run(() => projectsApi.command(project.value!.id, project.value!, { type: 'move-section', sectionId, targetIndex }), 'Section order updated.', 'section.move', { sectionId, targetIndex })
}
function setSectionDuration(sectionId: string, durationBars: number) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-section-duration', sectionId, durationBars }),
    'Section length updated.',
    'section.duration',
    { sectionId, durationBars })
}
function removeSection(sectionId: string) {
  if (!project.value) return
  return run(() => projectsApi.command(project.value!.id, project.value!, { type: 'remove-section', sectionId }), 'Section removed.', 'section.remove', { sectionId })
}
async function addLyricLine(sectionIndex: number, focus = false) {
  if (!project.value) return
  const section = project.value.sections[sectionIndex]
  const line = { id: crypto.randomUUID(), text: '' }
  section.lyricLines.push(line)
  activityLog.write('info', 'lyrics.line.add', 'Lyric line added locally.', { sectionId: section.id, lineId: line.id })
  if (focus) {
    await nextTick()
    document.querySelector<HTMLInputElement>(`[data-line-id="${line.id}"]`)?.focus()
  }
}
async function addLineAfter(sectionIndex: number, lineIndex: number) {
  if (!project.value) return
  const section = project.value.sections[sectionIndex]
  const line = { id: crypto.randomUUID(), text: '' }
  section.lyricLines.splice(lineIndex + 1, 0, line)
  await nextTick()
  document.querySelector<HTMLInputElement>(`[data-line-id="${line.id}"]`)?.focus()
}
function removeLyricLine(sectionIndex: number, lineIndex: number) {
  if (!project.value) return
  const section = project.value.sections[sectionIndex]
  const [line] = section.lyricLines.splice(lineIndex, 1)
  activityLog.write('info', 'lyrics.line.remove', 'Lyric line removed locally.', { sectionId: section.id, lineId: line.id })
}
async function handleLineBackspace(sectionIndex: number, lineIndex: number, lineText: string) {
  if (lineText || lineIndex === 0 || !project.value) return
  const previousId = project.value.sections[sectionIndex].lyricLines[lineIndex - 1].id
  removeLyricLine(sectionIndex, lineIndex)
  await nextTick()
  document.querySelector<HTMLInputElement>(`[data-line-id="${previousId}"]`)?.focus()
}
function setMeter(value: string) {
  if (!project.value) return
  const [numerator, denominator] = value.split('/').map(Number)
  project.value.timeline.timeSignatureMap.events[0].numerator = numerator
  project.value.timeline.timeSignatureMap.events[0].denominator = denominator
}
function meterValue(value: SongProject) { return `${value.timeline.timeSignatureMap.events[0].numerator}/${value.timeline.timeSignatureMap.events[0].denominator}` }
function placementFor(sectionId: string) { return project.value?.timeline.sectionPlacements.find(item => item.sectionId === sectionId) }
function label(kind: SectionKind) { return kind === 'PreChorus' ? 'Pre-Chorus' : kind }
function undo() { if (project.value) return run(() => projectsApi.undo(project.value!.id, project.value!), 'Last section operation undone.', 'history.undo') }
function redo() { if (project.value) return run(() => projectsApi.redo(project.value!.id, project.value!), 'Section operation restored.', 'history.redo') }
function warnBeforeClose(event: BeforeUnloadEvent) { if (isDirty.value) event.preventDefault() }

async function saveRecoverySnapshot() {
  if (!project.value || !isDirty.value || !persistedRevision.value || busy.value) return
  try {
    await projectsApi.saveRecovery(project.value, persistedRevision.value, sessionId)
    activityLog.write('info', 'recovery.snapshot', 'Unsaved work protected.', { projectId: project.value.id })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'Unsaved recovery snapshot failed.'
    activityLog.write('error', 'recovery.snapshot', status.value, { projectId: project.value.id })
  }
}

watch(serializedProject, () => {
  if (recoveryTimer) clearTimeout(recoveryTimer)
  if (isDirty.value) recoveryTimer = setTimeout(() => void saveRecoverySnapshot(), 1_000)
})

onMounted(async () => {
  window.addEventListener('beforeunload', warnBeforeClose)
  await Promise.all([refreshLibrary(), refreshRecovery()])
  if (recoverySnapshots.value.length > 0) {
    view.value = 'recovery'
    status.value = `${recoverySnapshots.value.length} protected editor snapshot${recoverySnapshots.value.length === 1 ? '' : 's'} found.`
  }
})
onBeforeUnmount(() => {
  window.removeEventListener('beforeunload', warnBeforeClose)
  if (recoveryTimer) clearTimeout(recoveryTimer)
})
</script>

<template>
  <main :class="{ 'has-project': view !== 'home' }">
    <header v-if="view === 'home'" class="welcome library-home">
      <p class="eyebrow">Your songwriting workspace</p>
      <h1>Maskil Forge</h1>
      <p class="tagline">Understand the words. Forge the music.</p>
      <div class="welcome-actions">
        <button @click="requestNewProject">Begin a new song</button>
        <details class="open-project">
          <summary>Open an existing song</summary>
          <label>Project ID<input v-model="projectId" placeholder="Paste project ID" /></label>
          <button class="secondary" :disabled="busy" @click="requestLoad">Open song</button>
        </details>
      </div>
      <section class="project-library" aria-labelledby="library-title">
        <div class="library-heading"><div><p class="eyebrow">Song library</p><h2 id="library-title">Continue your work</h2></div><div class="library-actions"><button v-if="recoverySnapshots.length" class="recovery-button" @click="openRecovery">Recovery ({{ recoverySnapshots.length }})</button><button class="quiet" @click="openTrash">Trash</button><button class="quiet" :disabled="libraryBusy" @click="refreshLibrary">Refresh</button></div></div>
        <p v-if="libraryBusy" class="library-message">Finding your saved songs…</p>
        <p v-else-if="projects.length === 0" class="library-message">No saved songs yet. Begin with any idea, even if it has no structure.</p>
        <div v-else class="project-grid">
          <article v-for="summary in projects" :key="summary.id" class="project-card">
            <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
            <dl><div><dt>Stage</dt><dd>{{ summary.sectionCount ? `${summary.sectionCount} structured section${summary.sectionCount === 1 ? '' : 's'}` : summary.hasRawLyrics ? 'Raw lyric draft' : 'New idea' }}</dd></div><div><dt>Modified</dt><dd>{{ formatModified(summary.lastModifiedUtc) }}</dd></div></dl>
            <div class="card-actions"><button class="secondary" @click="openSummary(summary.id)">Continue song</button><details class="card-menu"><summary>More actions</summary><button class="danger" @click="requestDelete(summary.id, summary.title)">Delete song</button></details></div>
          </article>
        </div>
      </section>
    </header>

    <section v-else-if="view === 'recovery'" class="trash-view recovery-view" aria-labelledby="recovery-title">
      <button class="quiet" @click="goHome">← Song library</button>
      <div class="trash-heading"><p class="eyebrow">Protected work</p><h1 id="recovery-title">Recover unsaved songs</h1><p>Maskil Forge found editor snapshots newer than an explicit save. Restore one to inspect it, or discard it without changing the saved song.</p></div>
      <p class="status" role="status">{{ status }}</p>
      <p v-if="recoverySnapshots.length === 0" class="library-message">No unsaved recovery snapshots remain.</p>
      <div v-else class="project-grid">
        <article v-for="summary in recoverySnapshots" :key="summary.id" class="project-card recovery-card">
          <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
          <dl><div><dt>Protected</dt><dd>{{ formatModified(summary.capturedAtUtc) }}</dd></div></dl>
          <div class="card-actions"><button :disabled="busy" @click="restoreRecovery(summary.id)">Restore unsaved work</button><button class="danger" :disabled="busy" @click="discardRecovery(summary.id)">Discard snapshot</button></div>
        </article>
      </div>
    </section>

    <section v-else-if="view === 'trash'" class="trash-view" aria-labelledby="trash-title">
      <button class="quiet" @click="goHome">← Song library</button>
      <div class="trash-heading"><p class="eyebrow">Recovery</p><h1 id="trash-title">Trash</h1><p>Restore a song to your library or permanently delete it. Permanent deletion cannot be undone.</p></div>
      <p class="status" role="status">{{ status }}</p>
      <p v-if="libraryBusy" class="library-message">Opening Trash…</p>
      <p v-else-if="trashedProjects.length === 0" class="library-message">Trash is empty.</p>
      <div v-else class="project-grid">
        <article v-for="summary in trashedProjects" :key="summary.id" class="project-card trash-card">
          <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
          <dl><div><dt>Deleted</dt><dd>{{ formatModified(summary.deletedAtUtc) }}</dd></div></dl>
          <div class="card-actions"><button class="secondary" :disabled="busy" @click="restoreSong(summary.id, summary.title)">Restore song</button><button class="danger" :disabled="busy" @click="requestPermanentDelete(summary.id, summary.title)">Permanently delete</button></div>
        </article>
      </div>
    </section>

    <template v-else-if="project">
      <header class="project-bar">
        <a class="wordmark" href="#" aria-label="Maskil Forge home" @click.prevent="requestHome">Maskil Forge</a>
        <label class="title-field"><span>Song title</span><input v-model="project.title" maxlength="200" /></label>
        <span class="editor-state" :class="{ modified: isDirty, saved: !isDirty }">{{ editorState }}</span>
        <nav class="project-actions" aria-label="Project actions">
          <button class="secondary" :disabled="busy || !response?.canUndo" @click="undo">Undo</button>
          <button class="secondary" :disabled="busy || !response?.canRedo" @click="redo">Redo</button>
          <button :disabled="busy || !isDirty" @click="saveProject">Save</button>
        </nav>
        <details class="project-menu">
          <summary>Project</summary>
          <div class="project-menu-panel">
            <button class="secondary" @click="requestNewProject">New song</button>
            <button class="secondary" @click="requestHome">Song library</button>
            <button class="danger" @click="requestDelete(project.id, project.title)">Delete this song</button>
            <label>Open by project ID<input v-model="projectId" placeholder="Project UUID" /></label>
            <button class="secondary" :disabled="busy" @click="requestLoad">Open song</button>
            <a href="/logs.html" target="_blank">Activity console ↗</a>
          </div>
        </details>
      </header>

      <p class="status" role="status">{{ status }}</p>

      <section v-if="view === 'capture'" class="capture-workspace" aria-labelledby="capture-title">
        <div class="capture-heading"><p class="eyebrow">Start with the words</p><h1 id="capture-title">Capture the idea</h1><p>Write lyrics, fragments, images, themes, or plain thoughts. You do not need to know the song structure yet.</p></div>
        <label class="raw-lyrics">Raw lyric draft<textarea v-model="project.rawLyricDraft" maxlength="100000" rows="18" autofocus placeholder="Write whatever is on your mind…&#10;&#10;A complete song is not required. Fragments are welcome." /></label>
        <div class="capture-actions"><button :disabled="busy || !isDirty" @click="saveDraft">Save draft</button><button class="secondary" :disabled="busy" @click="beginStructuring">Begin structuring</button></div>
        <p class="preservation-note">Your raw draft remains preserved when you begin creating Verse, Chorus, and other sections.</p>
      </section>

      <template v-else>
      <div class="structure-nav"><button class="quiet" @click="returnToDraft">← Raw lyric draft</button></div>
      <section class="song-canvas" aria-label="Song structure">
        <div class="canvas-heading">
          <div><p class="eyebrow">Song structure</p><h1>Shape the song</h1></div>
          <div class="section-toolbar" aria-label="Add song section">
            <button v-for="kind in (['Verse','Chorus','PreChorus','Bridge','Outro'] as SectionKind[])" :key="kind" class="secondary add-section" :disabled="busy" @click="addSection(kind)">+ {{ label(kind) }}</button>
          </div>
        </div>

        <p v-if="project.sections.length === 0" class="empty-song">Choose a section above and start writing your first line.</p>
        <ol class="sections">
          <li v-for="(section, index) in project.sections" :key="section.id" class="section-card">
            <div class="section-heading">
              <span class="section-number">{{ String(index + 1).padStart(2, '0') }}</span>
              <div class="section-identity">
                <span>{{ label(section.kind) }}</span>
                <label><span class="sr-only">Section title</span><input :value="section.title" maxlength="100" @change="renameSection(section.id, ($event.target as HTMLInputElement).value)" /></label>
                <div v-if="placementFor(section.id)" class="section-position">
                  <span>Bars {{ placementFor(section.id)!.start.bar }}–{{ placementFor(section.id)!.start.bar + placementFor(section.id)!.durationBars - 1 }}</span>
                  <label>Length <input :value="placementFor(section.id)!.durationBars" type="number" min="1" max="128" :aria-label="`${section.title} length in bars`" @change="setSectionDuration(section.id, Number(($event.target as HTMLInputElement).value))" /> bars</label>
                </div>
              </div>
              <div class="section-actions">
                <button class="quiet" :disabled="busy || index === 0" @click="moveSection(section.id, index - 1)">↑ <span>Move up</span></button>
                <button class="quiet" :disabled="busy || index === project.sections.length - 1" @click="moveSection(section.id, index + 1)">↓ <span>Move down</span></button>
                <button class="danger" :disabled="busy" @click="removeSection(section.id)">Delete section</button>
              </div>
            </div>
            <div class="lyrics-editor">
              <div class="lyrics-heading"><span>Lyrics</span><button class="quiet" :disabled="busy" @click="addLyricLine(index, true)">+ Add line</button></div>
              <div v-for="(line, lineIndex) in section.lyricLines" :key="line.id" class="lyric-line">
                <span>{{ lineIndex + 1 }}</span>
                <input v-model="line.text" :data-line-id="line.id" maxlength="2000" :aria-label="`Lyric line ${lineIndex + 1}`" placeholder="Write a lyric line…" @keydown.enter.prevent="addLineAfter(index, lineIndex)" @keydown.backspace="handleLineBackspace(index, lineIndex, line.text)" />
                <button class="quiet lyric-delete" :disabled="busy" @click="removeLyricLine(index, lineIndex)">Remove line</button>
              </div>
            </div>
            <details class="developer-details"><summary>Developer details</summary><small>Section ID: {{ section.id }}</small><small v-for="line in section.lyricLines" :key="line.id">Line ID: {{ line.id }}</small></details>
          </li>
        </ol>
      </section>

      <details class="song-settings">
        <summary>Song settings</summary>
        <div class="settings-grid">
          <label>Artist<input v-model="project.artist" maxlength="200" placeholder="Artist or songwriter" /></label>
          <label>Genre<select v-model="project.genre"><option v-for="genre in genres" :key="genre" :value="genre">{{ genre === 'RAndB' ? 'R&B' : genre }}</option></select></label>
          <label>Tempo<input v-model.number="project.timeline.tempoMap.events[0].beatsPerMinute" type="number" min="20" max="300" /></label>
          <label>Time signature<select :value="meterValue(project)" @change="setMeter(($event.target as HTMLSelectElement).value)"><option v-for="meter in meters" :key="meter">{{ meter }}</option></select></label>
          <label class="description-field">Description<textarea v-model="project.description" maxlength="2000" rows="3" placeholder="Song concept or creative context" /></label>
        </div>
      </details>
      </template>
    </template>

    <div v-if="confirmationOpen" class="modal-backdrop" role="presentation" @click.self="cancelConfirmation">
      <section class="load-dialog" role="dialog" aria-modal="true" aria-labelledby="confirmation-title">
        <p class="eyebrow">Protect your work</p>
        <h2 id="confirmation-title">Unsaved changes detected</h2>
        <p>Save your current song before {{ pendingAction === 'new' ? 'beginning a new one' : pendingAction === 'load' ? 'opening another one' : 'returning to the song library' }}?</p>
        <div class="dialog-actions">
          <button :disabled="busy" @click="saveBeforeContinuing">Save first</button>
          <button class="danger" :disabled="busy" @click="discardAndContinue">Discard changes</button>
          <button class="secondary" :disabled="busy" @click="cancelConfirmation">Cancel</button>
        </div>
      </section>
    </div>

    <div v-if="deleteConfirmationOpen && deleteTarget" class="modal-backdrop" role="presentation" @click.self="cancelDelete">
      <section class="load-dialog delete-dialog" role="alertdialog" aria-modal="true" aria-labelledby="delete-title" aria-describedby="delete-description">
        <p class="eyebrow">Final confirmation</p>
        <h2 id="delete-title">Are you sure?</h2>
        <p id="delete-description">Delete “{{ deleteTarget.title }}”? It will be removed from your song library and moved to Trash.</p>
        <div class="dialog-actions">
          <button class="secondary" :disabled="busy" autofocus @click="cancelDelete">Cancel</button>
          <button class="danger" :disabled="busy" @click="confirmDelete">Yes, delete song</button>
        </div>
      </section>
    </div>

    <div v-if="permanentDeleteTarget" class="modal-backdrop" role="presentation" @click.self="cancelPermanentDelete">
      <section class="load-dialog delete-dialog" role="alertdialog" aria-modal="true" aria-labelledby="permanent-delete-title" aria-describedby="permanent-delete-description">
        <p class="eyebrow">Cannot be undone</p>
        <h2 id="permanent-delete-title">Permanently delete this song?</h2>
        <p id="permanent-delete-description">“{{ permanentDeleteTarget.title }}” and its saved project data will be erased forever. Are you absolutely sure?</p>
        <div class="dialog-actions">
          <button class="secondary" :disabled="busy" autofocus @click="cancelPermanentDelete">Cancel</button>
          <button class="danger" :disabled="busy" @click="confirmPermanentDelete">Yes, permanently delete</button>
        </div>
      </section>
    </div>
  </main>
</template>
