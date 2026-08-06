<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { projectsApi, type ProjectResponse, type SectionKind, type SongProject } from './api'
import { activityLog } from './logging'

const response = ref<ProjectResponse | null>(null)
const projectId = ref(localStorage.getItem('maskilForge.projectId') ?? '')
const status = ref('Create a project or load one by ID.')
const busy = ref(false)
const project = computed(() => response.value?.project ?? null)
const meters = ['2/4', '3/4', '4/4', '5/4', '6/8', '7/8', '9/8', '12/8']

function accept(next: ProjectResponse, message: string) {
  response.value = next
  projectId.value = next.project.id
  localStorage.setItem('maskilForge.projectId', next.project.id)
  status.value = message
}

async function run(action: () => Promise<ProjectResponse>, message: string, logAction: string, details?: Record<string, string | number | boolean | null>) {
  busy.value = true
  activityLog.write('info', logAction, 'Action requested.', details)
  try {
    accept(await action(), message)
    activityLog.write('success', logAction, message, { projectId: projectId.value, ...details })
  }
  catch (error) {
    status.value = error instanceof Error ? error.message : 'The request failed.'
    activityLog.write('error', logAction, status.value, details)
  }
  finally { busy.value = false }
}

function createProject() { return run(() => projectsApi.create('Untitled Song'), 'Project created and saved.', 'project.create') }
function loadProject() {
  if (!projectId.value.trim()) { status.value = 'Enter a project ID to load.'; activityLog.write('warning', 'project.load', status.value); return }
  return run(() => projectsApi.load(projectId.value.trim()), 'Project loaded from JSON.', 'project.load', { projectId: projectId.value.trim() })
}
function saveProject() {
  if (!project.value) return
  return run(() => projectsApi.save(project.value!), 'Project saved to JSON.', 'project.save', { projectId: project.value.id, sectionCount: project.value.sections.length })
}
function addSection(kind: SectionKind) {
  if (!project.value) return
  return run(() => projectsApi.command(project.value!.id, { type: 'add-section', kind }), `${label(kind)} added.`, 'section.add', { kind })
}
function renameSection(sectionId: string, title: string) {
  if (!project.value) return
  return run(() => projectsApi.command(project.value!.id, { type: 'rename-section', sectionId, title }), 'Section renamed.', 'section.rename', { sectionId, title })
}
function moveSection(sectionId: string, targetIndex: number) {
  if (!project.value) return
  return run(() => projectsApi.command(project.value!.id, { type: 'move-section', sectionId, targetIndex }), 'Section order updated.', 'section.move', { sectionId, targetIndex })
}
function removeSection(sectionId: string) {
  if (!project.value) return
  return run(() => projectsApi.command(project.value!.id, { type: 'remove-section', sectionId }), 'Section removed.', 'section.remove', { sectionId })
}
function updateLyrics(index: number, text: string) {
  if (!project.value) return
  project.value.sections[index].lyricLines = text.split('\n').map((line, lineIndex) => ({
    id: project.value!.sections[index].lyricLines[lineIndex]?.id ?? crypto.randomUUID(), text: line,
  }))
}
function lyricText(index: number) { return project.value?.sections[index].lyricLines.map(line => line.text).join('\n') ?? '' }
function setMeter(value: string) {
  if (!project.value) return
  const [numerator, denominator] = value.split('/').map(Number)
  project.value.timeSignature.numerator = numerator
  project.value.timeSignature.denominator = denominator
}
function meterValue(project: SongProject) { return `${project.timeSignature.numerator}/${project.timeSignature.denominator}` }
function label(kind: SectionKind) { return kind === 'PreChorus' ? 'Pre-Chorus' : kind }
function undo() { if (project.value) return run(() => projectsApi.undo(project.value!.id), 'Last section operation undone.', 'history.undo') }
function redo() { if (project.value) return run(() => projectsApi.redo(project.value!.id), 'Section operation restored.', 'history.redo') }

onMounted(() => { if (projectId.value) void loadProject() })
</script>

<template>
  <main>
    <header class="hero">
      <div>
        <p class="eyebrow">Song Graph foundation</p>
        <h1>Maskil Forge</h1>
        <p>Understand the words. Forge the music.</p>
        <a class="activity-link" href="/logs.html" target="_blank">Open activity console ↗</a>
      </div>
      <div class="load-panel">
        <label>Project ID<input v-model="projectId" placeholder="Project UUID" /></label>
        <div class="button-row"><button :disabled="busy" @click="createProject">New project</button><button class="secondary" :disabled="busy" @click="loadProject">Load</button></div>
      </div>
    </header>

    <p class="status" role="status">{{ status }}</p>

    <section v-if="project" class="workspace">
      <div class="project-controls">
        <label>Project title<input v-model="project.title" maxlength="200" /></label>
        <label>Tempo<input v-model.number="project.tempo.beatsPerMinute" type="number" min="20" max="300" /></label>
        <label>Time signature<select :value="meterValue(project)" @change="setMeter(($event.target as HTMLSelectElement).value)"><option v-for="meter in meters" :key="meter">{{ meter }}</option></select></label>
        <div class="button-row actions"><button :disabled="busy || !response?.canUndo" @click="undo">Undo</button><button :disabled="busy || !response?.canRedo" @click="redo">Redo</button><button :disabled="busy" @click="saveProject">Save</button></div>
      </div>

      <div class="section-toolbar" aria-label="Add song section">
        <span>Add section</span>
        <button v-for="kind in (['Verse','Chorus','PreChorus','Bridge','Outro'] as SectionKind[])" :key="kind" :disabled="busy" @click="addSection(kind)">+ {{ label(kind) }}</button>
      </div>

      <ol class="sections">
        <li v-for="(section, index) in project.sections" :key="section.id" class="section-card">
          <div class="section-heading">
            <span class="section-number">{{ String(index + 1).padStart(2, '0') }}</span>
            <div class="section-identity"><span>{{ label(section.kind) }}</span><input :value="section.title" maxlength="100" @change="renameSection(section.id, ($event.target as HTMLInputElement).value)" /></div>
            <div class="section-actions"><button :disabled="busy || index === 0" title="Move up" @click="moveSection(section.id, index - 1)">↑</button><button :disabled="busy || index === project.sections.length - 1" title="Move down" @click="moveSection(section.id, index + 1)">↓</button><button class="danger" :disabled="busy" title="Delete section" @click="removeSection(section.id)">Delete</button></div>
          </div>
          <label>Lyrics<textarea :value="lyricText(index)" rows="5" placeholder="One lyric line per row" @input="updateLyrics(index, ($event.target as HTMLTextAreaElement).value)" /></label>
          <small>Section ID: {{ section.id }}</small>
        </li>
      </ol>
    </section>
  </main>
</template>
