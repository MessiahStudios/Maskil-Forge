<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { projectsApi, type Accidental, type ArrangementRole, type BeatPosition, type ChordQuality, type ChordSymbol, type HarmonyNoteSketch, type LyricLine, type LyricPhrase, type LyricTimelineMarker, type LyricTimelineView, type LyricWord, type MusicalKey, type NoteLetter, type ProjectResponse, type ProjectSummary, type ProsodicWeight, type ProsodyScore, type RecoverySummary, type RhythmCandidate, type ScaleMode, type SectionDensity, type SectionEnergy, type SectionKind, type SongGenre, type SongProject, type StressLevel, type TrashedProjectSummary, type VoiceLeadingReview } from './api'
import { activityLog } from './logging'
import { creatorDestination, creatorProgress, creatorStages } from './creatorJourney.js'
import type { RegisteredPitch } from './api'
import { ChordAudition } from './chordAudition'

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
const recoveryBlocked = ref(false)
let recoveryTimer: ReturnType<typeof setTimeout> | undefined
const project = computed(() => response.value?.project ?? null)
function projectSnapshot(value: SongProject) {
  const { lastModifiedUtc: _revision, ...creativeState } = value
  return JSON.stringify(creativeState)
}
const serializedProject = computed(() => project.value ? projectSnapshot(project.value) : '')
const isDirty = computed(() => Boolean(project.value) && serializedProject.value !== savedSnapshot.value)
const editorState = computed(() => isDirty.value ? 'Unsaved changes' : cleanLabel.value === 'saved' ? 'Saved' : 'No changes')
const meters = ['2/4', '3/4', '4/4', '5/4', '6/8', '7/8', '9/8', '12/8']
const genres: SongGenre[] = ['Unspecified', 'Pop', 'Rock', 'Folk', 'Country', 'RAndB', 'HipHop', 'Electronic', 'Cinematic', 'Alternative', 'Other']
const noteLetters: NoteLetter[] = ['C', 'D', 'E', 'F', 'G', 'A', 'B']
const accidentals: Accidental[] = ['Natural', 'Sharp', 'Flat']
const scaleModes: ScaleMode[] = ['Major', 'NaturalMinor']
const chordQualities: ChordQuality[] = ['Major', 'Minor', 'Diminished', 'Augmented', 'DominantSeventh']
const sectionEnergies: SectionEnergy[] = ['Intimate', 'Gentle', 'Building', 'Strong', 'Peak']
const sectionDensities: SectionDensity[] = ['Sparse', 'Light', 'Balanced', 'Full', 'Dense']
const arrangementRoles: Array<{ id: ArrangementRole; label: string; help: string }> = [
  { id: 'Foundation', label: 'Foundation', help: 'The grounding layer that makes the section feel settled.' },
  { id: 'Pulse', label: 'Pulse', help: 'A repeating motion that helps the section move.' },
  { id: 'Harmony', label: 'Harmony support', help: 'A layer that carries or colors the chords.' },
  { id: 'LowEndSupport', label: 'Low-end support', help: 'Weight beneath the song without naming a bass instrument.' },
  { id: 'Texture', label: 'Texture', help: 'Atmosphere, space, or sustained color.' },
  { id: 'Accent', label: 'Accents', help: 'Selective emphasis around important moments.' },
  { id: 'Transition', label: 'Transitions', help: 'Movement into or out of this section.' },
  { id: 'Countermelody', label: 'Countermelody', help: 'A supporting melodic response.' },
  { id: 'HookReinforcement', label: 'Hook reinforcement', help: 'Extra support for the section’s memorable idea.' },
]
const placementDrafts = reactive<Record<string, BeatPosition>>({})
const candidateLabelDrafts = reactive<Record<string, string>>({})
const harmonyCandidateLabelDrafts = reactive<Record<string, string>>({})
const prosodyScores = reactive<Record<string, ProsodyScore>>({})
const voiceLeadingReviews = reactive<Record<string, VoiceLeadingReview>>({})
const harmonyNoteSketches = reactive<Record<string, HarmonyNoteSketch>>({})
const chordAudition = new ChordAudition()
const auditionState = reactive({ sectionId: '', messageSectionId: '', message: '' })
const lyricTimeline = ref<LyricTimelineView | null>(null)
const timelineOverlayCandidateId = ref('')
const selectedTimelineMarkerKey = ref('')
let timelineRefreshToken = 0

function accept(next: ProjectResponse, message: string, markPersisted = false) {
  stopChordAudition()
  response.value = next
  Object.keys(placementDrafts).forEach(key => delete placementDrafts[key])
  Object.keys(harmonyCandidateLabelDrafts).forEach(key => delete harmonyCandidateLabelDrafts[key])
  Object.keys(prosodyScores).forEach(key => delete prosodyScores[key])
  Object.keys(voiceLeadingReviews).forEach(key => delete voiceLeadingReviews[key])
  Object.keys(harmonyNoteSketches).forEach(key => delete harmonyNoteSketches[key])
  projectId.value = next.project.id
  localStorage.setItem('maskilForge.projectId', next.project.id)
  status.value = message
  if (markPersisted) {
    savedSnapshot.value = projectSnapshot(next.project)
    persistedRevision.value = next.project.lastModifiedUtc
    recoveryBlocked.value = false
    cleanLabel.value = message.includes('saved') ? 'saved' : 'clean'
  }
  void refreshLyricTimeline()
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
    if (succeeded) { view.value = 'capture'; activeCreatorStage.value = 'idea' }
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
  if (succeeded) {
    view.value = project.value?.sections.length ? 'structure' : 'capture'
    activeCreatorStage.value = project.value?.sections.length ? 'shape' : 'idea'
  }
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
  activeCreatorStage.value = 'shape'
  status.value = 'Your original lyric draft remains preserved while you shape the song.'
  void refreshLyricTimeline()
}
function returnToDraft() { view.value = 'capture'; activeCreatorStage.value = 'words'; status.value = 'Raw lyric draft.'; lyricTimeline.value = null }
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
    recoveryBlocked.value = false
    savedSnapshot.value = ''
    timelineOverlayCandidateId.value = ''
    selectedTimelineMarkerKey.value = ''
    view.value = recovered.project.sections.length ? 'structure' : 'capture'
    status.value = 'Recovered unsaved work. Save the song when you are ready.'
    activityLog.write('success', 'recovery.restore', 'Unsaved work restored.', { projectId: id })
    void refreshLyricTimeline()
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
function editLyricLine(sectionId: string, lineId: string, text: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'edit-lyric-line', sectionId, lineId, text }),
    'Lyric words updated.',
    'lyrics.line.edit',
    { sectionId, lineId, wordCount: text.trim() ? text.trim().split(/\s+/u).length : 0 })
}
function syllableText(word: LyricWord) {
  return word.syllables.map(syllable => syllable.text).join(' | ')
}
function setWordSyllables(sectionId: string, lineId: string, wordId: string, event: Event) {
  if (!project.value) return
  const form = event.currentTarget as HTMLFormElement
  const input = form.elements.namedItem('syllables') as HTMLInputElement
  const value = input.value
  const syllables = value.trim()
    ? value.split(/\s*[|·]\s*/u).map(part => part.trim()).filter(Boolean)
    : []
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-word-syllables', sectionId, lineId, wordId, syllables,
    }),
    syllables.length ? 'Manual syllable boundaries saved.' : 'Manual syllable boundaries cleared.',
    'lyrics.syllables.manual',
    { sectionId, lineId, wordId, syllableCount: syllables.length })
}
function setSyllableStress(sectionId: string, lineId: string, wordId: string, syllableId: string, value: string) {
  if (!project.value) return
  const stressLevel = value ? value as StressLevel : null
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-syllable-stress', sectionId, lineId, wordId, syllableId, stressLevel,
    }),
    stressLevel ? `${stressLevel} stress saved as an artist decision.` : 'Stress mark cleared.',
    'lyrics.stress.manual',
    { sectionId, lineId, wordId, syllableId, stressLevel })
}
function phraseWords(line: LyricLine, phrase: LyricPhrase) {
  const wordById = new Map(line.words.map(word => [word.id, word]))
  return phrase.wordIds.map(id => wordById.get(id)).filter((word): word is LyricWord => Boolean(word))
}
function phraseSyllables(line: LyricLine, phrase: LyricPhrase) {
  return phraseWords(line, phrase).flatMap(word =>
    word.syllables.map(syllable => ({ word, syllable })))
}
function prosodicUnitFor(phrase: LyricPhrase, syllableId: string) {
  return phrase.prosody?.units.find(unit => unit.syllableId === syllableId)
}
function syllablePlacementFor(line: LyricLine, syllableId: string) {
  return line.syllablePlacements.find(item => item.syllableId === syllableId)
}
function placementDraft(line: LyricLine, syllableId: string) {
  if (!placementDrafts[syllableId]) {
    const existing = syllablePlacementFor(line, syllableId)?.position
    placementDrafts[syllableId] = existing ? { ...existing } : { bar: 1, beat: 1, tick: 0 }
  }
  return placementDrafts[syllableId]
}
function resolvedPlacement(sectionId: string, position: BeatPosition) {
  const sectionStart = placementFor(sectionId)?.start.bar ?? 1
  return `Song bar ${sectionStart + position.bar - 1}, beat ${position.beat}, tick ${position.tick}`
}
function setSyllablePlacement(sectionId: string, lineId: string, syllableId: string, beatPosition: BeatPosition | null) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-syllable-placement', sectionId, lineId, syllableId, beatPosition,
    }),
    beatPosition ? 'Syllable placed in musical time.' : 'Syllable placement cleared.',
    'lyrics.beat-map.manual',
    { sectionId, lineId, syllableId, bar: beatPosition?.bar ?? null, beat: beatPosition?.beat ?? null, tick: beatPosition?.tick ?? null })
}
function rhythmCandidatesFor(line: LyricLine, phraseId: string) {
  return line.rhythmCandidates.filter(candidate => candidate.phraseId === phraseId)
}
function suggestedCandidateLabel(line: LyricLine, phrase: LyricPhrase) {
  return `Option ${rhythmCandidatesFor(line, phrase.id).length + 1}`
}
async function captureRhythmCandidate(sectionId: string, line: LyricLine, phrase: LyricPhrase) {
  if (!project.value) return
  const candidateLabel = candidateLabelDrafts[phrase.id]?.trim() || suggestedCandidateLabel(line, phrase)
  const succeeded = await run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'capture-rhythm-candidate', sectionId, lineId: line.id, phraseId: phrase.id, candidateLabel,
    }),
    `${candidateLabel} saved for comparison.`,
    'lyrics.rhythm-candidate.capture',
    { sectionId, lineId: line.id, phraseId: phrase.id, candidateLabel })
  if (succeeded) candidateLabelDrafts[phrase.id] = `Option ${rhythmCandidatesFor(line, phrase.id).length + 2}`
}
function renameRhythmCandidate(sectionId: string, lineId: string, candidateId: string, candidateLabel: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'rename-rhythm-candidate', sectionId, lineId, rhythmCandidateId: candidateId, candidateLabel,
    }),
    'Rhythm option renamed.',
    'lyrics.rhythm-candidate.rename',
    { sectionId, lineId, candidateId })
}
function removeRhythmCandidate(sectionId: string, lineId: string, candidateId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'remove-rhythm-candidate', sectionId, lineId, rhythmCandidateId: candidateId,
    }),
    'Rhythm option removed.',
    'lyrics.rhythm-candidate.remove',
    { sectionId, lineId, candidateId })
}
function applyRhythmCandidate(sectionId: string, lineId: string, candidate: RhythmCandidate) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'apply-rhythm-candidate', sectionId, lineId, rhythmCandidateId: candidate.id,
    }),
    `${candidate.label} is now the active beat placement for this phrase.`,
    'lyrics.rhythm-candidate.apply',
    { sectionId, lineId, candidateId: candidate.id })
}
function breathPointFor(line: LyricLine, syllableId: string) {
  return line.breathPoints.find(item => item.afterSyllableId === syllableId)
}
function setBreathPoint(sectionId: string, lineId: string, syllableId: string, present: boolean) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-breath-point', sectionId, lineId, syllableId, breathPresent: present,
    }),
    present ? 'Breath after this syllable saved as an artist decision.' : 'Breath mark cleared.',
    'lyrics.breath.manual',
    { sectionId, lineId, syllableId, present })
}
function scoreKey(phraseId: string, rhythmCandidateId?: string | null) {
  return rhythmCandidateId ? `${phraseId}:${rhythmCandidateId}` : `${phraseId}:active`
}
function prosodyScoreFor(phraseId: string, rhythmCandidateId?: string | null) {
  return prosodyScores[scoreKey(phraseId, rhythmCandidateId)]
}
async function reviewProsody(
  sectionId: string,
  lineId: string,
  phraseId: string,
  rhythmCandidateId?: string,
) {
  if (!project.value) return
  busy.value = true
  const details = { sectionId, lineId, phraseId, rhythmCandidateId: rhythmCandidateId ?? null }
  activityLog.write('info', 'lyrics.prosody.score', 'Prosody review requested.', details)
  try {
    const score = await projectsApi.scoreProsody(
      project.value.id,
      project.value,
      sectionId,
      lineId,
      phraseId,
      rhythmCandidateId,
    )
    prosodyScores[scoreKey(phraseId, rhythmCandidateId)] = score
    status.value = score.findings.length
      ? `Prosody review: ${score.overall}/100 with ${score.findings.length} note${score.findings.length === 1 ? '' : 's'}.`
      : `Prosody review: ${score.overall}/100 with no issues flagged.`
    activityLog.write('success', 'lyrics.prosody.score', status.value, {
      ...details,
      overall: score.overall,
      findingCount: score.findings.length,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The request failed.'
    activityLog.write('error', 'lyrics.prosody.score', status.value, details)
  } finally {
    busy.value = false
  }
}
async function refreshLyricTimeline() {
  if (!project.value || view.value !== 'structure') {
    if (!project.value) lyricTimeline.value = null
    return
  }
  const token = ++timelineRefreshToken
  const overlay = timelineOverlayCandidateId.value || null
  try {
    const next = await projectsApi.lyricTimeline(project.value.id, project.value, overlay)
    if (token !== timelineRefreshToken) return
    lyricTimeline.value = next
  } catch (error) {
    if (token !== timelineRefreshToken) return
    activityLog.write(
      'error',
      'lyrics.timeline.view',
      error instanceof Error ? error.message : 'Lyric timeline refresh failed.',
      { projectId: project.value.id })
  }
}
function timelinePercent(tick: number) {
  const total = lyricTimeline.value?.totalTicks ?? 0
  if (total <= 0) return 0
  return Math.min(100, Math.max(0, (tick / total) * 100))
}
function timelineMarkerKey(marker: LyricTimelineMarker) {
  return `${marker.kind}:${marker.syllableId}:${marker.absoluteTick}:${marker.rhythmCandidateId ?? 'none'}`
}
function activeTimelineMarkers() {
  return lyricTimeline.value?.markers.filter(item => item.kind === 'ActivePlacement') ?? []
}
function overlayTimelineMarkers() {
  return lyricTimeline.value?.markers.filter(item => item.kind === 'RhythmCandidate') ?? []
}
function breathTimelineMarkers() {
  return lyricTimeline.value?.markers.filter(item => item.kind === 'BreathAfter') ?? []
}
function timelineBarTicks() {
  const viewModel = lyricTimeline.value
  if (!viewModel || viewModel.totalTicks <= 0) return []
  const ticksPerBar = viewModel.ticksPerBeat * viewModel.beatsPerBar
  const bars = Math.max(1, Math.round(viewModel.totalTicks / ticksPerBar))
  return Array.from({ length: bars + 1 }, (_, index) => ({
    bar: index + 1,
    percent: timelinePercent(index * ticksPerBar),
  }))
}
function overlayCandidateOptions() {
  if (!project.value) return [] as Array<{ id: string; label: string }>
  return project.value.sections.flatMap(section =>
    section.lyricLines.flatMap(line =>
      line.rhythmCandidates.map(candidate => ({
        id: candidate.id,
        label: `${section.title}: ${candidate.label}`,
      }))))
}
function selectedTimelineMarker() {
  return lyricTimeline.value?.markers.find(item => timelineMarkerKey(item) === selectedTimelineMarkerKey.value) ?? null
}
async function selectTimelineMarker(marker: LyricTimelineMarker) {
  selectedTimelineMarkerKey.value = timelineMarkerKey(marker)
  status.value = `${marker.syllableText} · song bar ${marker.songPosition.bar}, beat ${marker.songPosition.beat}, tick ${marker.songPosition.tick}`
  activityLog.write('info', 'lyrics.timeline.select', status.value, {
    syllableId: marker.syllableId,
    kind: marker.kind,
    absoluteTick: marker.absoluteTick,
  })
  await nextTick()
  const target = document.querySelector(`[data-syllable-id="${marker.syllableId}"]`) as HTMLElement | null
  target?.scrollIntoView({ behavior: 'smooth', block: 'center' })
}
function setTimelineOverlay(candidateId: string) {
  timelineOverlayCandidateId.value = candidateId
  void refreshLyricTimeline()
}
function lyricLineLock(lineId: string) {
  return project.value?.locks.find(item => item.scope === 'LyricLine' && item.lineId === lineId)
}
function phraseRhythmLock(lineId: string, phraseId: string) {
  return project.value?.locks.find(item => item.scope === 'PhraseRhythm' && item.lineId === lineId && item.phraseId === phraseId)
}
function lockLyricLine(lineId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'lock-lyric-line', lineId }),
    'Lyric line locked.',
    'lyrics.lock.line',
    { lineId })
}
function lockPhraseRhythm(lineId: string, phraseId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'lock-phrase-rhythm', lineId, phraseId }),
    'Phrase rhythm locked.',
    'lyrics.lock.phrase-rhythm',
    { lineId, phraseId })
}
function unlockCreativeLock(creativeLockId: string, message: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'unlock-creative-lock', creativeLockId }),
    message,
    'lyrics.lock.unlock',
    { creativeLockId })
}
function candidateEventLabel(line: LyricLine, candidateEvent: RhythmCandidate['events'][number]) {
  const syllable = line.words.flatMap(word => word.syllables.map(item => ({ word, syllable: item })))
    .find(item => item.syllable.id === candidateEvent.syllableId)
  const position = candidateEvent.beatPosition
  return `${syllable?.syllable.text ?? 'Syllable'} ${position.bar}:${position.beat}:${position.tick}`
}
function setProsodicWeight(sectionId: string, lineId: string, phraseId: string, syllableId: string, value: string) {
  if (!project.value) return
  const prosodicWeight = value ? value as ProsodicWeight : null
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-prosodic-weight', sectionId, lineId, phraseId, syllableId, prosodicWeight,
    }),
    prosodicWeight ? `${prosodicWeight} phrase weight saved as an artist decision.` : 'Phrase weight cleared.',
    'lyrics.prosody.manual',
    { sectionId, lineId, phraseId, syllableId, prosodicWeight })
}
function splitLyricPhrase(sectionId: string, lineId: string, wordId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'split-lyric-phrase', sectionId, lineId, wordId,
    }),
    'Phrase boundary added.',
    'lyrics.phrase.split',
    { sectionId, lineId, wordId })
}
function joinLyricPhrase(sectionId: string, lineId: string, phraseId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'join-lyric-phrase', sectionId, lineId, phraseId,
    }),
    'Phrases joined.',
    'lyrics.phrase.join',
    { sectionId, lineId, phraseId })
}
async function addLyricLine(sectionIndex: number, focus = false) {
  if (!project.value) return
  const section = project.value.sections[sectionIndex]
  const line = { id: crypto.randomUUID(), text: '', words: [], punctuation: [], phrases: [], syllablePlacements: [], rhythmCandidates: [], breathPoints: [] }
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
  const line = { id: crypto.randomUUID(), text: '', words: [], punctuation: [], phrases: [], syllablePlacements: [], rhythmCandidates: [], breathPoints: [] }
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
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-time-signature', numerator, denominator,
    }),
    'Time signature updated.',
    'timeline.meter',
    { numerator, denominator })
}
function setKey(partial: Partial<MusicalKey>) {
  if (!project.value) return
  const key: MusicalKey = {
    tonic: partial.tonic ?? project.value.key.tonic,
    accidental: partial.accidental ?? project.value.key.accidental,
    mode: partial.mode ?? project.value.key.mode,
  }
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-key', key }),
    `Key set to ${formatKey(key)}.`,
    'theory.key',
    { tonic: key.tonic, accidental: key.accidental, mode: key.mode })
}
function formatKey(key: MusicalKey) {
  const accidental = key.accidental === 'Sharp' ? '#' : key.accidental === 'Flat' ? 'b' : ''
  const mode = key.mode === 'Major' ? 'major' : 'natural minor'
  return `${key.tonic}${accidental} ${mode}`
}
function formatChord(chord: ChordSymbol) {
  const accidental = chord.accidental === 'Sharp' ? '#' : chord.accidental === 'Flat' ? 'b' : ''
  const quality = chord.quality === 'Major' ? ''
    : chord.quality === 'Minor' ? 'm'
      : chord.quality === 'Diminished' ? 'dim'
        : chord.quality === 'Augmented' ? 'aug'
          : '7'
  return `${chord.root}${accidental}${quality}`
}
function formatHarmonyCandidateEvent(item: SongProject['sections'][number]['harmonyCandidates'][number]['events'][number]) {
  const start = `${item.start.bar}:${item.start.beat}`
  const duration = item.durationBars === 1 ? '1 bar' : `${item.durationBars} bars`
  return `${formatChord(item.chord)} · ${start} · ${duration}`
}
function formatRegisteredPitch(pitch: RegisteredPitch) {
  const accidental = pitch.accidental === 'Sharp' ? '#' : pitch.accidental === 'Flat' ? 'b' : ''
  return `${pitch.letter}${accidental}${pitch.octave}`
}
function parseRegisteredPitch(token: string): RegisteredPitch {
  const match = /^([A-Ga-g])([#b]?)(-?\d)$/.exec(token.trim())
  if (!match) throw new Error(`Use a note such as C4 or Bb3. '${token}' is not recognized.`)
  return {
    letter: match[1].toUpperCase() as NoteLetter,
    accidental: match[2] === '#' ? 'Sharp' : match[2] === 'b' ? 'Flat' : 'Natural',
    octave: Number(match[3]),
  }
}
function setChordVoicing(sectionId: string, harmonyChordId: string, event: Event) {
  if (!project.value) return
  const form = event.currentTarget as HTMLFormElement
  const text = String(new FormData(form).get('voicing') ?? '').trim()
  const tokens = text ? text.split(/[\s,]+/) : []
  let registeredPitches: RegisteredPitch[]
  try {
    registeredPitches = tokens.map(parseRegisteredPitch)
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The voicing could not be read.'
    activityLog.write('warning', 'harmony.voicing.set', status.value, { sectionId, harmonyChordId })
    return
  }
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-chord-voicing', sectionId, harmonyChordId, registeredPitches, minimumMidiNote: 21, maximumMidiNote: 108 }),
    registeredPitches.length ? `Voicing set to ${tokens.join(' ')}.` : 'Chord voicing cleared.',
    'harmony.voicing.set',
    { sectionId, harmonyChordId, voiceCount: registeredPitches.length })
}
function addNoteEvent(event: Event) {
  if (!project.value) return
  const form = event.currentTarget as HTMLFormElement
  const data = new FormData(form)
  try {
    const notePitch = parseRegisteredPitch(String(data.get('pitch') ?? ''))
    const startTick = Number(data.get('startTick'))
    const durationTicks = Number(data.get('durationTicks'))
    const velocity = Number(data.get('velocity'))
    return run(
      () => projectsApi.command(project.value!.id, project.value!, { type: 'add-note-event', notePitch, startTick, durationTicks, velocity }),
      `${formatRegisteredPitch(notePitch)} added to the playable-note foundation.`,
      'midi.note.add',
      { pitch: formatRegisteredPitch(notePitch), startTick, durationTicks, velocity })
      ?.then(succeeded => { if (succeeded) form.reset() })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The note could not be read.'
    activityLog.write('warning', 'midi.note.add', status.value)
  }
}
function removeNoteEvent(noteEventId: string, pitch: RegisteredPitch) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'remove-note-event', noteEventId }),
    `${formatRegisteredPitch(pitch)} removed from the playable-note foundation.`,
    'midi.note.remove',
    { noteEventId, pitch: formatRegisteredPitch(pitch) })
}
async function exportMidi() {
  if (!project.value) return
  if (!project.value.noteEvents.length) {
    status.value = 'Your song does not contain playable notes yet. Create a harmony sketch first.'
    return
  }
  busy.value = true
  try {
    const blob = await projectsApi.exportMidi(project.value.id, project.value)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    const safeTitle = project.value.title.trim().replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '').toLowerCase() || 'song'
    link.href = url
    link.download = `${safeTitle}-maskil-forge.mid`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    status.value = 'MIDI exported successfully. Your musical sketch can now be opened in another music application.'
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'Unable to export MIDI. No changes were made to your project.'
  } finally {
    busy.value = false
  }
}
function chordLabel(sectionId: string, chordId: string) {
  const section = project.value?.sections.find(item => item.id === sectionId)
  const chord = section?.harmony.find(item => item.id === chordId)
  return chord ? formatChord(chord.chord) : 'Chord'
}
function motionExplanation(motion: string) {
  if (motion === 'Smooth') return 'Several notes can stay or move a short distance.'
  if (motion === 'Moderate') return 'The notes connect with a noticeable but manageable shift.'
  return 'The notes make a wider shift; this may sound bold rather than wrong.'
}
function stopChordAudition(message = '') {
  chordAudition.stop()
  const stoppedSectionId = auditionState.sectionId
  auditionState.sectionId = ''
  auditionState.messageSectionId = message ? stoppedSectionId : ''
  auditionState.message = message
}
async function hearProgression(sectionId: string) {
  if (!project.value) return
  const section = project.value.sections.find(item => item.id === sectionId)
  if (!section?.harmony.length) return
  const tempo = project.value.timeline.tempoMap.events[0].beatsPerMinute
  const meter = project.value.timeline.timeSignatureMap.events[0]
  auditionState.sectionId = sectionId
  auditionState.messageSectionId = sectionId
  auditionState.message = 'Preparing your progression…'
  try {
    const result = await chordAudition.play(section.harmony, {
      beatsPerMinute: tempo,
      beatsPerBar: meter.numerator,
      beatUnit: meter.denominator,
      ticksPerQuarterNote: project.value.timeline.ticksPerQuarterNote,
    }, () => stopChordAudition('Progression preview finished.'))
    auditionState.message = result.usedPreviewVoicings
      ? `Playing at ${tempo} BPM. Chords without your notes use temporary preview voicings.`
      : `Playing your registered voicings at ${tempo} BPM.`
  } catch (error) {
    stopChordAudition(error instanceof Error ? error.message : 'The progression could not be played.')
  }
}
async function reviewVoiceLeading(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'harmony.movement.review', 'Chord movement review requested.', { sectionId })
  try {
    const review = await projectsApi.reviewVoiceLeading(project.value.id, project.value, sectionId)
    voiceLeadingReviews[sectionId] = review
    status.value = `${review.smoothTransitionCount} of ${review.transitions.length} chord changes connect smoothly.`
    activityLog.write('success', 'harmony.movement.review', status.value, { sectionId, transitionCount: review.transitions.length })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The chord movement review failed.'
    activityLog.write('error', 'harmony.movement.review', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
async function prepareHarmonyNoteSketch(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'midi.sketch.prepare', 'Playable-note sketch requested.', { sectionId })
  try {
    const sketch = await projectsApi.harmonyNoteSketch(project.value.id, project.value, sectionId)
    harmonyNoteSketches[sectionId] = sketch
    status.value = `${sketch.events.length} playable notes prepared for review.`
    activityLog.write('success', 'midi.sketch.prepare', status.value, {
      sectionId, noteCount: sketch.events.length, usesPreviewVoicings: sketch.usesPreviewVoicings,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The playable-note sketch could not be prepared.'
    activityLog.write('error', 'midi.sketch.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function useHarmonyNoteSketch(sectionId: string) {
  if (!project.value) return
  const noteCount = harmonyNoteSketches[sectionId]?.events.length ?? 0
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-harmony-note-sketch', sectionId }),
    `${noteCount} playable notes added from this harmony sketch.`,
    'midi.sketch.use',
    { sectionId, noteCount })
}
function addHarmonyChord(sectionId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'add-harmony-chord',
      sectionId,
      chord: { root: project.value!.key.tonic, accidental: project.value!.key.accidental, quality: project.value!.key.mode === 'Major' ? 'Major' : 'Minor' },
      beatPosition: { bar: 1, beat: 1, tick: 0 },
      durationBars: 2,
    }),
    'Harmony chord added.',
    'harmony.add',
    { sectionId })
}
function updateHarmonyChord(
  sectionId: string,
  harmonyChordId: string,
  chord: ChordSymbol,
  start: BeatPosition,
  durationBars: number)
{
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-harmony-chord',
      sectionId,
      harmonyChordId,
      chord,
      beatPosition: start,
      durationBars,
    }),
    `Harmony updated to ${formatChord(chord)}.`,
    'harmony.set',
    { sectionId, harmonyChordId })
}
function removeHarmonyChord(sectionId: string, harmonyChordId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'remove-harmony-chord',
      sectionId,
      harmonyChordId,
    }),
    'Harmony chord removed.',
    'harmony.remove',
    { sectionId, harmonyChordId })
}
function captureHarmonyCandidate(sectionId: string) {
  if (!project.value) return
  const candidateLabel = harmonyCandidateLabelDrafts[sectionId]?.trim() || `Option ${(project.value.sections.find(item => item.id === sectionId)?.harmonyCandidates?.length ?? 0) + 1}`
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'capture-harmony-candidate', sectionId, candidateLabel }),
    `${candidateLabel} saved for harmony comparison.`,
    'harmony.candidate.capture',
    { sectionId, candidateLabel })
}
function renameHarmonyCandidate(sectionId: string, harmonyCandidateId: string, candidateLabel: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'rename-harmony-candidate', sectionId, harmonyCandidateId, candidateLabel }),
    'Harmony option renamed.',
    'harmony.candidate.rename',
    { sectionId, harmonyCandidateId })
}
function applyHarmonyCandidate(sectionId: string, harmonyCandidateId: string, label: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'apply-harmony-candidate', sectionId, harmonyCandidateId }),
    `${label} applied to the active progression.`,
    'harmony.candidate.apply',
    { sectionId, harmonyCandidateId })
}
function removeHarmonyCandidate(sectionId: string, harmonyCandidateId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'remove-harmony-candidate', sectionId, harmonyCandidateId }),
    'Harmony option removed.',
    'harmony.candidate.remove',
    { sectionId, harmonyCandidateId })
}
function meterValue(value: SongProject) { return `${value.timeline.timeSignatureMap.events[0].numerator}/${value.timeline.timeSignatureMap.events[0].denominator}` }
function placementFor(sectionId: string) { return project.value?.timeline.sectionPlacements.find(item => item.sectionId === sectionId) }
function arrangementFor(sectionId: string) { return project.value?.arrangement.find(item => item.sectionId === sectionId) }
function arrangementEnergy(sectionId: string) { return arrangementFor(sectionId)?.energy ?? 'Building' }
function arrangementDensity(sectionId: string) { return arrangementFor(sectionId)?.density ?? 'Balanced' }
function energyValue(energy: SectionEnergy) { return sectionEnergies.indexOf(energy) + 1 }
function setSectionArrangement(sectionId: string, energy: SectionEnergy, density: SectionDensity) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-section-arrangement', sectionId, sectionEnergy: energy, sectionDensity: density }),
    `Arrangement shape set to ${energy.toLowerCase()} energy with ${density.toLowerCase()} density.`,
    'arrangement.section.set',
    { sectionId, energy, density })
}
function sectionHasRole(sectionId: string, role: ArrangementRole) {
  return Boolean(project.value?.arrangementRoles.some(item => item.sectionId === sectionId && item.role === role))
}
function setSectionRole(sectionId: string, role: ArrangementRole, present: boolean) {
  if (!project.value) return
  const roleLabel = arrangementRoles.find(item => item.id === role)?.label ?? role
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-section-role', sectionId, arrangementRole: role, rolePresent: present }),
    present ? `${roleLabel} added to this section.` : `${roleLabel} removed from this section.`,
    'arrangement.role.set',
    { sectionId, role, present })
}
function label(kind: SectionKind) { return kind === 'PreChorus' ? 'Pre-Chorus' : kind }
type CreatorStage = 'idea' | 'words' | 'shape' | 'music' | 'harmony' | 'arrangement'
const activeCreatorStage = ref<CreatorStage>('idea')
const creatorCompletion = computed(() => creatorProgress(project.value))
function creatorStageState(stage: CreatorStage) { return creatorCompletion.value[stage] ? 'complete' : 'upcoming' }
async function goToCreatorStage(stage: CreatorStage) {
  const destination = creatorDestination(stage, Boolean(project.value?.sections.length))
  if (!destination) return
  activeCreatorStage.value = (destination.stage ?? stage) as CreatorStage
  view.value = destination.view
  if (destination.message) status.value = destination.message
  if (destination.view === 'capture') lyricTimeline.value = null
  await nextTick()
  const target = document.getElementById(destination.target)
  if (!target) return
  if (destination.open && target instanceof HTMLDetailsElement) target.open = true
  target.classList.remove('journey-focus')
  void target.getBoundingClientRect()
  target.classList.add('journey-focus')
  window.setTimeout(() => target.classList.remove('journey-focus'), 1_400)
  target.scrollIntoView({ behavior: 'smooth', block: 'center' })
  if (destination.focus && target instanceof HTMLElement) target.focus({ preventScroll: true })
  else if (target instanceof HTMLDetailsElement) target.querySelector<HTMLElement>('summary')?.focus({ preventScroll: true })
}
function undo() { if (project.value) return run(() => projectsApi.undo(project.value!.id, project.value!), 'Last edit undone.', 'history.undo') }
function redo() { if (project.value) return run(() => projectsApi.redo(project.value!.id, project.value!), 'Edit restored.', 'history.redo') }
function warnBeforeClose(event: BeforeUnloadEvent) { if (isDirty.value) event.preventDefault() }

async function saveRecoverySnapshot() {
  if (!project.value || !isDirty.value || !persistedRevision.value || busy.value || recoveryBlocked.value) return
  try {
    await projectsApi.saveRecovery(project.value, persistedRevision.value, sessionId)
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unsaved recovery snapshot failed.'
    status.value = message
    if (message.includes('another session') || message.includes('Reload it before saving')) {
      recoveryBlocked.value = true
      activityLog.write('error', 'recovery.snapshot', `${message} Automatic recovery paused until you reload.`, { projectId: project.value.id })
    } else {
      activityLog.write('error', 'recovery.snapshot', message, { projectId: project.value.id })
    }
  }
}

watch(serializedProject, () => {
  if (recoveryTimer) clearTimeout(recoveryTimer)
  if (isDirty.value && !recoveryBlocked.value) recoveryTimer = setTimeout(() => void saveRecoverySnapshot(), 1_000)
})

watch(
  () => [view.value, project.value?.id, project.value?.sections.length ?? 0] as const,
  ([nextView, nextProjectId], previous) => {
    if (previous && (nextView !== previous[0] || nextProjectId !== previous[1])) stopChordAudition()
    if (nextView === 'structure') void refreshLyricTimeline()
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
  stopChordAudition()
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

      <nav class="creator-journey" aria-label="Songwriting workspaces">
        <p><strong>Current workspace</strong><span>Move between creative areas without changing your song.</span></p>
        <ol>
          <li v-for="stage in creatorStages" :key="stage.id" :class="{ 'stage-active': activeCreatorStage === stage.id }">
            <button
              type="button"
              class="journey-step"
              :aria-current="activeCreatorStage === stage.id ? 'page' : undefined"
              @click="goToCreatorStage(stage.id)">
              <span class="journey-mark">{{ activeCreatorStage === stage.id ? '✦' : '·' }}</span>
              <span>{{ stage.label }}</span>
            </button>
          </li>
        </ol>
        <div class="journey-progress" aria-label="Song development progress">
          <strong>Your song journey</strong>
          <ul>
            <li v-for="stage in creatorStages" :key="`progress-${stage.id}`" :class="`progress-${creatorStageState(stage.id)}`">
              <span aria-hidden="true">{{ creatorStageState(stage.id) === 'complete' ? '✓' : '○' }}</span>
              {{ stage.id === 'idea' ? 'Song started' : stage.id === 'words' ? 'Lyrics started' : stage.id === 'shape' ? 'Structure' : stage.id === 'music' ? 'Music exploration' : stage.label }}
            </li>
          </ul>
        </div>
      </nav>

      <section v-if="view === 'capture'" class="capture-workspace" aria-labelledby="capture-title">
        <div class="capture-heading"><p class="eyebrow">Start with the words</p><h1 id="capture-title">Capture the idea</h1><p>Write lyrics, fragments, images, themes, or plain thoughts. You do not need to know the song structure yet.</p></div>
        <label class="raw-lyrics">Raw lyric draft<textarea id="raw-lyric-draft" v-model="project.rawLyricDraft" maxlength="100000" rows="18" autofocus placeholder="Write whatever is on your mind…&#10;&#10;A complete song is not required. Fragments are welcome." /></label>
        <div class="capture-actions"><button :disabled="busy || !isDirty" @click="saveDraft">Save draft</button><button class="secondary" :disabled="busy" @click="beginStructuring">Shape these words</button></div>
        <p class="preservation-note">Your raw draft remains preserved when you begin creating Verse, Chorus, and other sections.</p>
      </section>

      <template v-else>
      <div class="structure-nav"><button class="quiet" @click="returnToDraft">← Raw lyric draft</button></div>
      <details id="musical-refinement" class="disclosure-panel timeline-disclosure">
        <summary><span>Explore musical timing</span><small>Optional · See how placed words line up across the song.</small></summary>
      <section class="lyric-timeline" aria-labelledby="lyric-timeline-title">
        <div class="lyric-timeline-heading">
          <div>
            <p class="eyebrow">Musical fit</p>
            <h2 id="lyric-timeline-title">Lyric timeline</h2>
            <p>Placed syllables appear on the song timeline. Click a mark to jump to its lyric controls.</p>
          </div>
          <label class="timeline-overlay">
            <span>Compare rhythm option</span>
            <select
              :value="timelineOverlayCandidateId"
              :disabled="busy || overlayCandidateOptions().length === 0"
              @change="setTimelineOverlay(($event.target as HTMLSelectElement).value)">
              <option value="">Active placements only</option>
              <option v-for="option in overlayCandidateOptions()" :key="option.id" :value="option.id">{{ option.label }}</option>
            </select>
          </label>
        </div>
        <p v-if="!lyricTimeline && project.sections.length > 0" class="timeline-empty">Loading song timeline…</p>
        <p v-else-if="!lyricTimeline || lyricTimeline.sections.length === 0" class="timeline-empty">Add a section to see the song timeline.</p>
        <div v-else-if="activeTimelineMarkers().length === 0 && overlayTimelineMarkers().length === 0" class="timeline-empty">
          Place syllables in a phrase below to see them here.
          <div class="timeline-rail" aria-hidden="true">
            <div
              v-for="span in lyricTimeline.sections"
              :key="span.sectionId"
              class="timeline-section-span"
              :style="{ left: `${timelinePercent(span.startTick)}%`, width: `${timelinePercent(span.endTickExclusive - span.startTick)}%` }">
              <span>{{ span.title }}</span>
            </div>
          </div>
        </div>
        <div v-else class="timeline-stage">
          <div class="timeline-rail" role="img" :aria-label="`Song timeline with ${activeTimelineMarkers().length} placed syllable${activeTimelineMarkers().length === 1 ? '' : 's'}`">
            <div
              v-for="span in lyricTimeline.sections"
              :key="span.sectionId"
              class="timeline-section-span"
              :style="{ left: `${timelinePercent(span.startTick)}%`, width: `${timelinePercent(span.endTickExclusive - span.startTick)}%` }">
              <span>{{ span.title }}</span>
            </div>
            <span
              v-for="tick in timelineBarTicks()"
              :key="`bar-${tick.bar}`"
              class="timeline-bar-tick"
              :style="{ left: `${tick.percent}%` }"
              :data-bar="tick.bar"></span>
            <button
              v-for="marker in breathTimelineMarkers()"
              :key="timelineMarkerKey(marker)"
              type="button"
              class="timeline-marker breath"
              :class="{ selected: selectedTimelineMarkerKey === timelineMarkerKey(marker) }"
              :style="{ left: `${timelinePercent(marker.absoluteTick)}%` }"
              :title="`Breath after ${marker.syllableText}`"
              :aria-label="`Breath after ${marker.syllableText} near song bar ${marker.songPosition.bar}`"
              @click="selectTimelineMarker(marker)">
              <span class="sr-only">Breath after {{ marker.syllableText }}</span>
            </button>
            <button
              v-for="marker in overlayTimelineMarkers()"
              :key="timelineMarkerKey(marker)"
              type="button"
              class="timeline-marker candidate"
              :class="{ selected: selectedTimelineMarkerKey === timelineMarkerKey(marker) }"
              :style="{ left: `${timelinePercent(marker.absoluteTick)}%` }"
              :title="`${marker.syllableText} (option)`"
              :aria-label="`${marker.syllableText} from compared rhythm option at song bar ${marker.songPosition.bar}, beat ${marker.songPosition.beat}`"
              @click="selectTimelineMarker(marker)">
              {{ marker.syllableText }}
            </button>
            <button
              v-for="marker in activeTimelineMarkers()"
              :key="timelineMarkerKey(marker)"
              type="button"
              class="timeline-marker active"
              :class="{
                selected: selectedTimelineMarkerKey === timelineMarkerKey(marker),
                strong: marker.prosodicWeight === 'Strong' || marker.stressLevel === 'Primary' || marker.stressLevel === 'Emphasized',
              }"
              :style="{ left: `${timelinePercent(marker.absoluteTick)}%` }"
              :title="`${marker.wordText} · ${marker.syllableText}`"
              :aria-label="`${marker.syllableText} at song bar ${marker.songPosition.bar}, beat ${marker.songPosition.beat}, tick ${marker.songPosition.tick}`"
              @click="selectTimelineMarker(marker)">
              {{ marker.syllableText }}
            </button>
          </div>
          <p v-if="selectedTimelineMarker()" class="timeline-selection">
            <strong>{{ selectedTimelineMarker()!.syllableText }}</strong>
            in {{ selectedTimelineMarker()!.wordText }}
            · song bar {{ selectedTimelineMarker()!.songPosition.bar }}, beat {{ selectedTimelineMarker()!.songPosition.beat }}, tick {{ selectedTimelineMarker()!.songPosition.tick }}
            · section bar {{ selectedTimelineMarker()!.sectionRelative.bar }}
            <template v-if="selectedTimelineMarker()!.kind === 'BreathAfter'"> · breath mark</template>
            <template v-else-if="selectedTimelineMarker()!.kind === 'RhythmCandidate'"> · compared option</template>
            <template v-else> · active placement</template>
          </p>
        </div>
      </section>
      </details>
      <section id="song-structure" class="song-canvas" aria-label="Song structure">
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
            <details :id="index === 0 ? 'harmony-tools' : `harmony-tools-${section.id}`" class="disclosure-panel harmony-disclosure">
              <summary><span>Explore musical ideas</span><small>Optional · Add chords, compare options, and review how changes connect.</small></summary>
              <div class="harmony-editor" :aria-label="`Harmony for ${section.title}`">
              <div class="harmony-heading">
                <div>
                  <strong>Harmony</strong>
                  <small>Section chords in musical time. Roman analysis and generation come later.</small>
                </div>
                <button class="quiet" :disabled="busy" @click="addHarmonyChord(section.id)">+ Add chord</button>
              </div>
              <p v-if="!section.harmony?.length" class="harmony-empty">No chords yet. Add one to begin this section’s progression.</p>
              <div v-if="section.harmony?.length" class="chord-audition" :aria-label="`Hear ${section.title} progression`">
                <div>
                  <strong>Hear this progression</strong>
                  <small>Listen first, then explore why the chords feel connected. Uses your saved tempo and timing.</small>
                </div>
                <button v-if="auditionState.sectionId !== section.id" type="button" :disabled="busy" @click="hearProgression(section.id)">▶ Hear progression</button>
                <button v-else type="button" class="quiet" @click="stopChordAudition('Playback stopped.')">■ Stop</button>
                <p v-if="auditionState.messageSectionId === section.id" role="status">{{ auditionState.message }}</p>
              </div>
              <section v-if="section.harmony?.length" class="harmony-note-sketch" :aria-label="`Playable-note sketch for ${section.title}`">
                <div>
                  <strong>Turn this harmony into playable notes</strong>
                  <small>Preview the exact notes first. Nothing is added to your song until you choose “Use this sketch.”</small>
                </div>
                <button type="button" class="secondary" :disabled="busy" @click="prepareHarmonyNoteSketch(section.id)">
                  {{ harmonyNoteSketches[section.id] ? 'Refresh note sketch' : 'Prepare note sketch' }}
                </button>
                <div v-if="harmonyNoteSketches[section.id]" class="harmony-note-sketch-result">
                  <p>
                    <strong>{{ harmonyNoteSketches[section.id].events.length }} notes ready to review</strong>
                    <span v-if="harmonyNoteSketches[section.id].usesPreviewVoicings">Some chords use temporary preview voicings. Review the pitches before accepting.</span>
                    <span v-else>Uses your registered chord voicings.</span>
                  </p>
                  <ol>
                    <li v-for="(note, noteIndex) in harmonyNoteSketches[section.id].events" :key="`${note.startTick}:${formatRegisteredPitch(note.pitch)}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.usesPreviewVoicing ? 'Preview voicing' : 'Your voicing' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="useHarmonyNoteSketch(section.id)">Use this sketch</button>
                </div>
              </section>
              <div v-if="section.harmony?.length" class="harmony-list">
                <article v-for="item in section.harmony" :key="item.id" class="harmony-card">
                  <div class="harmony-symbol">
                    <strong>{{ formatChord(item.chord) }}</strong>
                    <small>{{ item.provenance }}</small>
                  </div>
                  <label>Root
                    <select
                      :value="item.chord.root"
                      :disabled="busy"
                      @change="updateHarmonyChord(section.id, item.id, { ...item.chord, root: ($event.target as HTMLSelectElement).value as NoteLetter }, item.start, item.durationBars)">
                      <option v-for="letter in noteLetters" :key="letter" :value="letter">{{ letter }}</option>
                    </select>
                  </label>
                  <label>Accidental
                    <select
                      :value="item.chord.accidental"
                      :disabled="busy"
                      @change="updateHarmonyChord(section.id, item.id, { ...item.chord, accidental: ($event.target as HTMLSelectElement).value as Accidental }, item.start, item.durationBars)">
                      <option v-for="accidental in accidentals" :key="accidental" :value="accidental">{{ accidental }}</option>
                    </select>
                  </label>
                  <label>Quality
                    <select
                      :value="item.chord.quality"
                      :disabled="busy"
                      @change="updateHarmonyChord(section.id, item.id, { ...item.chord, quality: ($event.target as HTMLSelectElement).value as ChordQuality }, item.start, item.durationBars)">
                      <option v-for="quality in chordQualities" :key="quality" :value="quality">{{ quality === 'DominantSeventh' ? 'Dominant 7' : quality }}</option>
                    </select>
                  </label>
                  <label>Bar
                    <input
                      type="number"
                      min="1"
                      :max="placementFor(section.id)?.durationBars ?? 1"
                      :value="item.start.bar"
                      :disabled="busy"
                      @change="updateHarmonyChord(section.id, item.id, item.chord, { ...item.start, bar: Number(($event.target as HTMLInputElement).value) }, item.durationBars)" />
                  </label>
                  <label>Beat
                    <input
                      type="number"
                      min="1"
                      :max="project.timeline.timeSignatureMap.events[0].numerator"
                      :value="item.start.beat"
                      :disabled="busy"
                      @change="updateHarmonyChord(section.id, item.id, item.chord, { ...item.start, beat: Number(($event.target as HTMLInputElement).value) }, item.durationBars)" />
                  </label>
                  <label>Length
                    <input
                      type="number"
                      min="1"
                      :max="placementFor(section.id)?.durationBars ?? 1"
                      :value="item.durationBars"
                      :disabled="busy"
                      @change="updateHarmonyChord(section.id, item.id, item.chord, item.start, Number(($event.target as HTMLInputElement).value))" />
                  </label>
                  <button class="danger" :disabled="busy" @click="removeHarmonyChord(section.id, item.id)">Remove</button>
                  <form class="voicing-editor" @submit.prevent="setChordVoicing(section.id, item.id, $event)">
                    <label>How the chord is played
                      <input name="voicing" :value="item.voicing?.voices.map(voice => formatRegisteredPitch(voice.pitch)).join(' ') ?? ''" placeholder="C3 G3 C4 E4" :disabled="busy" />
                    </label>
                    <button type="submit" class="quiet" :disabled="busy">Use these notes</button>
                    <small>Optional · Enter low-to-high notes with octaves. Leave empty to clear.</small>
                  </form>
                </article>
              </div>
              <details class="voice-leading-disclosure">
                <summary>Musical details</summary>
              <div class="voice-leading-review">
                <div>
                  <strong>How smoothly do these chords connect?</strong>
                  <small>This optional check follows registered notes when both chords have them. Otherwise, it compares shared chord tones and short moves. A wider move is a color choice, not an error.</small>
                </div>
                <button type="button" class="quiet" :disabled="busy || section.harmony.length < 2" @click="reviewVoiceLeading(section.id)">Check chord movement</button>
                <p v-if="section.harmony.length < 2" class="harmony-empty">Add at least two chords to review how they connect.</p>
                <div v-else-if="voiceLeadingReviews[section.id]" class="voice-leading-results">
                  <p><strong>{{ voiceLeadingReviews[section.id].smoothTransitionCount }} of {{ voiceLeadingReviews[section.id].transitions.length }}</strong> changes connect smoothly · average movement {{ voiceLeadingReviews[section.id].averageMotionSemitones }} semitones</p>
                  <article v-for="transition in voiceLeadingReviews[section.id].transitions" :key="`${transition.fromChordId}:${transition.toChordId}`" :class="`motion-${transition.motion.toLowerCase()}`">
                    <strong>{{ chordLabel(section.id, transition.fromChordId) }} → {{ chordLabel(section.id, transition.toChordId) }}</strong>
                    <span>{{ transition.motion }}</span>
                    <small>{{ transition.usesRegisteredVoices ? `Registered voices · largest move ${transition.maximumVoiceMovementSemitones} semitones.` : motionExplanation(transition.motion) }} {{ transition.commonToneCount }} shared {{ transition.commonToneCount === 1 ? 'note' : 'notes' }}.</small>
                    <ul v-if="transition.findings.length" class="voice-leading-findings">
                      <li v-for="(finding, findingIndex) in transition.findings" :key="`${finding.kind}:${findingIndex}`" :class="`finding-${finding.severity.toLowerCase()}`">{{ finding.message }}</li>
                    </ul>
                  </article>
                </div>
              </div>
              </details>
              <form class="harmony-candidate-capture" @submit.prevent="captureHarmonyCandidate(section.id)">
                <label>Option name
                  <input v-model="harmonyCandidateLabelDrafts[section.id]" maxlength="100" :placeholder="`Option ${(section.harmonyCandidates?.length ?? 0) + 1}`" :disabled="busy || !section.harmony?.length" />
                </label>
                <button type="submit" class="quiet" :disabled="busy || !section.harmony?.length">Save current progression</button>
              </form>
              <small class="harmony-candidate-help">Saved options preserve this progression for comparison. Using one replaces the active chords and can be undone.</small>
              <div v-if="section.harmonyCandidates?.length" class="harmony-candidates">
                <article v-for="candidate in section.harmonyCandidates" :key="candidate.id" class="harmony-candidate-card">
                  <div class="harmony-candidate-summary">
                    <label>Option name
                      <input :value="candidate.label" maxlength="100" :disabled="busy" @change="renameHarmonyCandidate(section.id, candidate.id, ($event.target as HTMLInputElement).value)" />
                    </label>
                    <small>{{ candidate.provenance }} · {{ candidate.events.length }} {{ candidate.events.length === 1 ? 'chord' : 'chords' }}</small>
                  </div>
                  <ol class="harmony-candidate-events">
                    <li v-for="item in candidate.events" :key="item.id">{{ formatHarmonyCandidateEvent(item) }}</li>
                  </ol>
                  <div class="harmony-candidate-actions">
                    <button type="button" :disabled="busy" @click="applyHarmonyCandidate(section.id, candidate.id, candidate.label)">Use this option</button>
                    <button type="button" class="danger" :disabled="busy" @click="removeHarmonyCandidate(section.id, candidate.id)">Remove option</button>
                  </div>
                </article>
              </div>
              </div>
            </details>
            <div class="lyrics-editor">
              <div class="lyrics-heading"><span>Lyrics</span><button class="quiet" :disabled="busy" @click="addLyricLine(index, true)">+ Add line</button></div>
              <div v-for="(line, lineIndex) in section.lyricLines" :key="line.id" class="lyric-line">
                <span class="lyric-line-number">{{ lineIndex + 1 }}</span>
                <input v-model="line.text" :data-line-id="line.id" maxlength="2000" :aria-label="`Lyric line ${lineIndex + 1}`" placeholder="Write a lyric line…" :disabled="busy || Boolean(lyricLineLock(line.id))" @change="editLyricLine(section.id, line.id, line.text)" @keydown.enter.prevent="addLineAfter(index, lineIndex)" @keydown.backspace="handleLineBackspace(index, lineIndex, line.text)" />
                <div class="lyric-line-actions">
                  <button v-if="lyricLineLock(line.id)" class="quiet" :disabled="busy" @click="unlockCreativeLock(lyricLineLock(line.id)!.id, 'Lyric line unlocked.')">Unlock line</button>
                  <button v-else class="quiet" :disabled="busy" @click="lockLyricLine(line.id)">Lock line</button>
                  <button class="quiet lyric-delete" :disabled="busy || Boolean(lyricLineLock(line.id))" @click="removeLyricLine(index, lineIndex)">Remove line</button>
                </div>
                <details v-if="line.words.length" class="disclosure-panel lyric-flow-tools">
                  <summary><span>Understand lyric flow</span><small>Optional · Syllables, emphasis, phrasing, breathing, and musical timing.</small></summary>
                <div v-if="line.words.length" class="lyric-words" :aria-label="`Artist-controlled syllables for lyric line ${lineIndex + 1}`">
                  <form v-for="word in line.words" :key="word.id" class="syllable-word" @submit.prevent="setWordSyllables(section.id, line.id, word.id, $event)">
                    <label :for="`syllables-${word.id}`" class="word-token">{{ word.text }}</label>
                    <input
                      :id="`syllables-${word.id}`"
                      name="syllables"
                      class="syllable-input"
                      :value="syllableText(word)"
                      :placeholder="word.text"
                      :aria-label="`Syllables for ${word.text}; separate boundaries with a vertical bar`"
                      :disabled="busy" />
                    <button class="quiet syllable-apply" type="submit" :disabled="busy">Use syllables</button>
                    <small v-if="word.syllables.length">{{ word.syllables[0].source }}</small>
                    <fieldset v-if="word.syllables.length" class="stress-controls">
                      <legend>Stress</legend>
                      <label v-for="syllable in word.syllables" :key="syllable.id" class="stress-syllable">
                        <span>{{ syllable.text }}</span>
                        <select
                          :value="syllable.stress?.level ?? ''"
                          :aria-label="`Stress for syllable ${syllable.text} in ${word.text}`"
                          :disabled="busy"
                          @change="setSyllableStress(section.id, line.id, word.id, syllable.id, ($event.target as HTMLSelectElement).value)">
                          <option value="">Unmarked</option>
                          <option value="None">No stress</option>
                          <option value="Secondary">Secondary</option>
                          <option value="Primary">Primary</option>
                          <option value="Emphasized">Emphasized</option>
                        </select>
                        <small>{{ syllable.stress?.provenance ?? 'Not marked' }}</small>
                      </label>
                    </fieldset>
                  </form>
                  <small class="syllable-help">Separate syllables with |, then mark the weight you intend to sing. Your corrections are authoritative.</small>
                </div>
                <div v-if="line.punctuation.length" class="punctuation-row" :aria-label="`Punctuation preserved for lyric line ${lineIndex + 1}`">
                  <small>Punctuation</small><span v-for="mark in line.punctuation" :key="mark.id" class="punctuation-token">{{ mark.text }}</span>
                </div>
                <div v-if="line.phrases.length" class="phrase-editor" :aria-label="`Phrase boundaries for lyric line ${lineIndex + 1}`">
                  <div class="phrase-heading"><strong>Phrases</strong><small>Break or join ideas without changing the lyric.</small></div>
                  <article v-for="phrase in line.phrases" :key="phrase.id" class="phrase-card">
                    <header>
                      <span>Phrase {{ phrase.position + 1 }}</span>
                      <small>{{ phrase.source }}</small>
                      <button v-if="phraseRhythmLock(line.id, phrase.id)" class="quiet" :disabled="busy" @click="unlockCreativeLock(phraseRhythmLock(line.id, phrase.id)!.id, 'Phrase rhythm unlocked.')">Unlock rhythm</button>
                      <button v-else class="quiet" :disabled="busy || Boolean(lyricLineLock(line.id))" @click="lockPhraseRhythm(line.id, phrase.id)">Lock rhythm</button>
                      <button v-if="phrase.position > 0" class="quiet phrase-join" :disabled="busy || Boolean(lyricLineLock(line.id))" @click="joinLyricPhrase(section.id, line.id, phrase.id)">Join with previous</button>
                    </header>
                    <div class="phrase-words">
                      <template v-for="(word, phraseWordIndex) in phraseWords(line, phrase)" :key="word.id">
                        <span class="phrase-word">{{ word.text }}</span>
                        <button v-if="phraseWordIndex < phrase.wordIds.length - 1" class="quiet phrase-break" :aria-label="`Break phrase after ${word.text}`" :disabled="busy" @click="splitLyricPhrase(section.id, line.id, word.id)">| Break</button>
                      </template>
                    </div>
                    <div v-if="phraseSyllables(line, phrase).length" class="prosody-editor">
                      <div class="prosody-heading"><strong>Shape the delivery</strong><small>Choose which syllables feel lighter or stronger, then place them in musical time if useful.</small></div>
                      <div v-for="entry in phraseSyllables(line, phrase)" :key="entry.syllable.id" class="prosodic-unit" :data-syllable-id="entry.syllable.id" :class="{ 'timeline-focus': selectedTimelineMarker()?.syllableId === entry.syllable.id }">
                        <span>{{ entry.syllable.text }} <small>{{ entry.word.text }}</small></span>
                        <select
                          :value="prosodicUnitFor(phrase, entry.syllable.id)?.weight ?? ''"
                          :aria-label="`Phrase weight for syllable ${entry.syllable.text} in ${entry.word.text}`"
                          :disabled="busy"
                          @change="setProsodicWeight(section.id, line.id, phrase.id, entry.syllable.id, ($event.target as HTMLSelectElement).value)">
                          <option value="">Unmapped</option>
                          <option value="Weak">Weak</option>
                          <option value="Neutral">Neutral</option>
                          <option value="Strong">Strong</option>
                        </select>
                        <small>{{ prosodicUnitFor(phrase, entry.syllable.id)?.provenance ?? 'Not mapped' }}</small>
                        <form class="beat-map-form" @submit.prevent="setSyllablePlacement(section.id, line.id, entry.syllable.id, { ...placementDraft(line, entry.syllable.id) })">
                          <strong>Musical placement</strong>
                          <label>Bar<input v-model.number="placementDraft(line, entry.syllable.id).bar" type="number" min="1" :max="placementFor(section.id)?.durationBars ?? 1" :aria-label="`Section-relative bar for syllable ${entry.syllable.text}`" :disabled="busy" /></label>
                          <label>Beat<input v-model.number="placementDraft(line, entry.syllable.id).beat" type="number" min="1" :max="project.timeline.timeSignatureMap.events[0].numerator" :aria-label="`Beat for syllable ${entry.syllable.text}`" :disabled="busy" /></label>
                          <label>Tick<input v-model.number="placementDraft(line, entry.syllable.id).tick" type="number" min="0" :max="project.timeline.ticksPerQuarterNote * 4 / project.timeline.timeSignatureMap.events[0].denominator - 1" :aria-label="`Tick for syllable ${entry.syllable.text}`" :disabled="busy" /></label>
                          <button type="submit" :disabled="busy">Place</button>
                          <button v-if="syllablePlacementFor(line, entry.syllable.id)" type="button" class="quiet clear-placement" :disabled="busy" @click="setSyllablePlacement(section.id, line.id, entry.syllable.id, null)">Clear</button>
                          <small v-if="syllablePlacementFor(line, entry.syllable.id)" class="placement-summary">Section bar {{ syllablePlacementFor(line, entry.syllable.id)!.position.bar }}, beat {{ syllablePlacementFor(line, entry.syllable.id)!.position.beat }}, tick {{ syllablePlacementFor(line, entry.syllable.id)!.position.tick }} · {{ resolvedPlacement(section.id, syllablePlacementFor(line, entry.syllable.id)!.position) }} · {{ syllablePlacementFor(line, entry.syllable.id)!.provenance }}</small>
                          <small v-else class="placement-summary">Not placed in musical time</small>
                        </form>
                        <label class="breath-toggle">
                          <input
                            type="checkbox"
                            :checked="Boolean(breathPointFor(line, entry.syllable.id))"
                            :aria-label="`Breath after syllable ${entry.syllable.text} in ${entry.word.text}`"
                            :disabled="busy"
                            @change="setBreathPoint(section.id, line.id, entry.syllable.id, ($event.target as HTMLInputElement).checked)" />
                          <span>Breath after</span>
                          <small>{{ breathPointFor(line, entry.syllable.id)?.provenance ?? 'Not marked' }}</small>
                        </label>
                      </div>
                    </div>
                    <small v-else class="prosody-empty">Add syllable boundaries above before describing phrase weight.</small>
                    <section v-if="phraseSyllables(line, phrase).length" class="rhythm-candidate-editor" :aria-label="`Rhythm options for phrase ${phrase.position + 1}`">
                      <div class="rhythm-candidate-heading">
                        <div><strong>Rhythm options</strong><small>Save the current beat placements as a possibility. Nothing is generated or accepted automatically.</small></div>
                        <form class="candidate-capture" @submit.prevent="captureRhythmCandidate(section.id, line, phrase)">
                          <label>Option name<input v-model="candidateLabelDrafts[phrase.id]" :placeholder="suggestedCandidateLabel(line, phrase)" maxlength="100" :aria-label="`Name for a new rhythm option for phrase ${phrase.position + 1}`" :disabled="busy" /></label>
                          <button type="submit" class="secondary" :disabled="busy">Save current placement</button>
                        </form>
                      </div>
                      <div class="prosody-score-panel">
                        <div class="prosody-score-heading">
                          <div><strong>How naturally do the words fit?</strong><small>An optional review of emphasis, breathing room, and crowded timing. The detailed scores do not change your song.</small></div>
                          <button type="button" class="secondary" :disabled="busy" @click="reviewProsody(section.id, line.id, phrase.id)">Check word flow</button>
                        </div>
                        <div v-if="prosodyScoreFor(phrase.id)" class="prosody-score-card" :aria-label="`Active placement score for phrase ${phrase.position + 1}`">
                          <p class="score-summary"><strong>{{ prosodyScoreFor(phrase.id)!.overall }}</strong>/100 · stress {{ prosodyScoreFor(phrase.id)!.stress }} · breath {{ prosodyScoreFor(phrase.id)!.breath }} · crowding {{ prosodyScoreFor(phrase.id)!.crowding }}</p>
                          <ul v-if="prosodyScoreFor(phrase.id)!.findings.length" class="score-findings">
                            <li v-for="(finding, findingIndex) in prosodyScoreFor(phrase.id)!.findings" :key="`${phrase.id}-active-${findingIndex}`">
                              <span class="finding-kind">{{ finding.kind }}</span>
                              <span>{{ finding.message }}</span>
                            </li>
                          </ul>
                          <p v-else class="candidate-empty">No stress, breath, or crowding issues flagged for the active placement.</p>
                        </div>
                      </div>
                      <p v-if="rhythmCandidatesFor(line, phrase.id).length === 0" class="candidate-empty">Place one or more syllables above, then save the timing as an option.</p>
                      <div v-else class="candidate-list">
                        <article v-for="candidate in rhythmCandidatesFor(line, phrase.id)" :key="candidate.id" class="candidate-card">
                          <label>Option name<input :value="candidate.label" maxlength="100" :aria-label="`Rename rhythm option ${candidate.label}`" :disabled="busy" @change="renameRhythmCandidate(section.id, line.id, candidate.id, ($event.target as HTMLInputElement).value)" /></label>
                          <div class="candidate-events" :aria-label="`${candidate.label} timing`">
                            <span v-for="candidateEvent in candidate.events" :key="candidateEvent.id">{{ candidateEventLabel(line, candidateEvent) }}</span>
                          </div>
                          <small>{{ candidate.provenance }} · {{ candidate.events.length }} timed {{ candidate.events.length === 1 ? 'syllable' : 'syllables' }}</small>
                          <div class="candidate-actions">
                            <button type="button" :disabled="busy" @click="applyRhythmCandidate(section.id, line.id, candidate)">Use this option</button>
                            <button type="button" class="secondary" :disabled="busy" @click="reviewProsody(section.id, line.id, phrase.id, candidate.id)">Check this option</button>
                            <button type="button" class="danger" :disabled="busy" @click="removeRhythmCandidate(section.id, line.id, candidate.id)">Remove option</button>
                          </div>
                          <div v-if="prosodyScoreFor(phrase.id, candidate.id)" class="prosody-score-card nested-score" :aria-label="`Score for ${candidate.label}`">
                            <p class="score-summary"><strong>{{ prosodyScoreFor(phrase.id, candidate.id)!.overall }}</strong>/100 · stress {{ prosodyScoreFor(phrase.id, candidate.id)!.stress }} · breath {{ prosodyScoreFor(phrase.id, candidate.id)!.breath }} · crowding {{ prosodyScoreFor(phrase.id, candidate.id)!.crowding }}</p>
                            <ul v-if="prosodyScoreFor(phrase.id, candidate.id)!.findings.length" class="score-findings">
                              <li v-for="(finding, findingIndex) in prosodyScoreFor(phrase.id, candidate.id)!.findings" :key="`${candidate.id}-${findingIndex}`">
                                <span class="finding-kind">{{ finding.kind }}</span>
                                <span>{{ finding.message }}</span>
                              </li>
                            </ul>
                            <p v-else class="candidate-empty">No issues flagged for this option.</p>
                          </div>
                        </article>
                      </div>
                    </section>
                  </article>
                </div>
                </details>
              </div>
            </div>
            <details class="developer-details"><summary>Developer details</summary><small>Section ID: {{ section.id }}</small><template v-for="line in section.lyricLines" :key="line.id"><small>Line ID: {{ line.id }}</small><template v-for="word in line.words" :key="word.id"><small>Word ID: {{ word.id }} · {{ word.text }}</small><small v-for="syllable in word.syllables" :key="syllable.id">Syllable ID: {{ syllable.id }} · {{ syllable.position }} · {{ syllable.source }} · {{ syllable.text }} · Stress: {{ syllable.stress ? `${syllable.stress.level} (${syllable.stress.provenance})` : 'Unmarked' }}</small></template><small v-for="mark in line.punctuation" :key="mark.id">Punctuation ID: {{ mark.id }} · {{ mark.start }} · {{ mark.text }}</small><template v-for="phrase in line.phrases" :key="phrase.id"><small>Phrase ID: {{ phrase.id }} · {{ phrase.position }} · {{ phrase.source }} · {{ phrase.wordIds.join(', ') }}</small><small v-if="phrase.prosody">Prosodic Pattern ID: {{ phrase.prosody.id }}</small><small v-for="unit in phrase.prosody?.units ?? []" :key="unit.id">Prosodic Unit ID: {{ unit.id }} · {{ unit.position }} · {{ unit.syllableId }} · {{ unit.weight }} · {{ unit.provenance }}</small></template><small v-for="placement in line.syllablePlacements" :key="placement.id">Syllable Placement ID: {{ placement.id }} · {{ placement.syllableId }} · {{ placement.position.bar }}:{{ placement.position.beat }}:{{ placement.position.tick }} · {{ placement.provenance }}</small><template v-for="candidate in line.rhythmCandidates" :key="candidate.id"><small>Rhythm Candidate ID: {{ candidate.id }} · {{ candidate.phraseId }} · {{ candidate.label }} · {{ candidate.provenance }}</small><small v-for="candidateEvent in candidate.events" :key="candidateEvent.id">Rhythm Event ID: {{ candidateEvent.id }} · {{ candidateEvent.position }} · {{ candidateEvent.syllableId }} · {{ candidateEvent.beatPosition.bar }}:{{ candidateEvent.beatPosition.beat }}:{{ candidateEvent.beatPosition.tick }}</small></template><small v-for="breath in line.breathPoints" :key="breath.id">Breath Point ID: {{ breath.id }} · after {{ breath.afterSyllableId }} · {{ breath.provenance }}</small></template></details>
          </li>
        </ol>
      </section>

      <section id="arrangement-blueprint" class="arrangement-blueprint" aria-labelledby="arrangement-title">
        <div class="arrangement-heading">
          <p class="eyebrow">Arrangement blueprint</p>
          <h2 id="arrangement-title">Shape the song’s energy</h2>
          <p>Describe how each section should feel before choosing instruments. These are creative intentions, not generated performances.</p>
        </div>
        <div v-if="project.sections.length" class="energy-curve" aria-label="Song energy curve">
          <article v-for="section in project.sections" :key="`energy-${section.id}`">
            <div class="energy-meter" :style="{ '--energy-level': energyValue(arrangementEnergy(section.id)) }" aria-hidden="true"><span /></div>
            <strong>{{ section.title }}</strong>
            <small>{{ arrangementFor(section.id) ? arrangementEnergy(section.id) : 'Not shaped yet' }}</small>
          </article>
        </div>
        <div v-if="project.sections.length" class="arrangement-sections">
          <article v-for="section in project.sections" :key="`arrangement-${section.id}`" class="arrangement-card">
            <div><strong>{{ section.title }}</strong><small>{{ label(section.kind) }} · {{ placementFor(section.id)?.durationBars ?? 0 }} bars</small></div>
            <label>Energy
              <select :value="arrangementEnergy(section.id)" :disabled="busy" @change="setSectionArrangement(section.id, ($event.target as HTMLSelectElement).value as SectionEnergy, arrangementDensity(section.id))">
                <option v-for="energy in sectionEnergies" :key="energy" :value="energy">{{ energy }}</option>
              </select>
            </label>
            <label>How much is happening
              <select :value="arrangementDensity(section.id)" :disabled="busy" @change="setSectionArrangement(section.id, arrangementEnergy(section.id), ($event.target as HTMLSelectElement).value as SectionDensity)">
                <option v-for="density in sectionDensities" :key="density" :value="density">{{ density }}</option>
              </select>
            </label>
            <small v-if="!arrangementFor(section.id)" class="arrangement-prompt">Choose either value to begin shaping this section.</small>
            <small v-else class="arrangement-saved">Saved as an artist decision.</small>
            <fieldset class="arrangement-role-picker">
              <legend>What does this section need?</legend>
              <p>Choose musical jobs now. Instruments can be selected later.</p>
              <button
                v-for="role in arrangementRoles"
                :key="role.id"
                type="button"
                class="role-choice"
                :class="{ selected: sectionHasRole(section.id, role.id) }"
                :aria-pressed="sectionHasRole(section.id, role.id)"
                :title="role.help"
                :disabled="busy"
                @click="setSectionRole(section.id, role.id, !sectionHasRole(section.id, role.id))">
                <span>{{ role.label }}</span>
                <small>{{ role.help }}</small>
              </button>
            </fieldset>
          </article>
        </div>
        <p v-else class="arrangement-empty">Add a Verse, Chorus, or another section above. Then you can describe how its energy should grow and what musical jobs it needs.</p>
        <p class="arrangement-boundary">Instrument choices and generated parts come in later slices.</p>
      </section>

      <section class="midi-export-panel" aria-labelledby="midi-export-title">
        <div>
          <span class="eyebrow">Take your sketch with you</span>
          <h2 id="midi-export-title">Export playable notes</h2>
          <p v-if="project.noteEvents.length">Your {{ project.noteEvents.length }} approved playable note{{ project.noteEvents.length === 1 ? '' : 's' }} can be opened in another music application. Maskil Forge will preserve {{ project.noteEvents.length === 1 ? 'its' : 'their' }} timing and dynamics without choosing an instrument.</p>
          <p v-else>Your song does not contain playable notes yet. Create and approve a harmony sketch first.</p>
        </div>
        <button type="button" :disabled="busy || !project.noteEvents.length" @click="exportMidi">Export MIDI</button>
      </section>

      <details class="disclosure-panel note-event-foundation">
        <summary><span>Inspect playable notes</span><small>Advanced · Review the exact events included in MIDI export.</small></summary>
        <div class="note-event-editor">
          <div>
            <strong>Playable-note foundation</strong>
            <p>These notes are explicit project data. Export translates them exactly; it does not select an instrument, generate a part, or start playback.</p>
          </div>
          <form class="note-event-form" @submit.prevent="addNoteEvent">
            <label>Pitch<input name="pitch" required placeholder="C4" :disabled="busy" /></label>
            <label>Start tick<input name="startTick" type="number" min="0" value="0" required :disabled="busy" /></label>
            <label>Duration<input name="durationTicks" type="number" min="1" :value="project.timeline.ticksPerQuarterNote" required :disabled="busy" /></label>
            <label>Velocity<input name="velocity" type="number" min="1" max="127" value="96" required :disabled="busy" /></label>
            <button type="submit" :disabled="busy">Add playable note</button>
          </form>
          <p v-if="!project.noteEvents.length" class="note-event-empty">No playable notes yet. Prepare a harmony note sketch above, or add notes here when you want precise control.</p>
          <ol v-else class="note-event-list">
            <li v-for="note in project.noteEvents" :key="note.id">
              <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
              <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks · velocity {{ note.velocity }}</span>
              <button type="button" class="danger" :disabled="busy" @click="removeNoteEvent(note.id, note.pitch)">Remove</button>
            </li>
          </ol>
        </div>
      </details>

      <details class="song-settings">
        <summary>Song settings</summary>
        <div class="settings-grid">
          <label>Artist<input v-model="project.artist" maxlength="200" placeholder="Artist or songwriter" /></label>
          <label>Genre<select v-model="project.genre"><option v-for="genre in genres" :key="genre" :value="genre">{{ genre === 'RAndB' ? 'R&B' : genre }}</option></select></label>
          <label>Tempo<input v-model.number="project.timeline.tempoMap.events[0].beatsPerMinute" type="number" min="20" max="300" /></label>
          <label>Time signature<select :value="meterValue(project)" @change="setMeter(($event.target as HTMLSelectElement).value)"><option v-for="meter in meters" :key="meter">{{ meter }}</option></select></label>
          <label>Key tonic<select :value="project.key.tonic" :disabled="busy" @change="setKey({ tonic: ($event.target as HTMLSelectElement).value as NoteLetter })"><option v-for="letter in noteLetters" :key="letter" :value="letter">{{ letter }}</option></select></label>
          <label>Accidental<select :value="project.key.accidental" :disabled="busy" @change="setKey({ accidental: ($event.target as HTMLSelectElement).value as Accidental })"><option v-for="accidental in accidentals" :key="accidental" :value="accidental">{{ accidental }}</option></select></label>
          <label>Mode<select :value="project.key.mode" :disabled="busy" @change="setKey({ mode: ($event.target as HTMLSelectElement).value as ScaleMode })"><option v-for="mode in scaleModes" :key="mode" :value="mode">{{ mode === 'NaturalMinor' ? 'Natural minor' : mode }}</option></select></label>
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
