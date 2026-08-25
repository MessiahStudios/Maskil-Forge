<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { projectsApi, type AccentProposal, type Accidental, type ArrangementRole, type BeatPosition, type ChordQuality, type ChordSymbol, type CountermelodyProposal, type DrumKitGeneralMidiMap, type HarmonyNoteSketch, type HarmonySupportProposal, type HookReinforcementProposal, type InstrumentArticulation, type InstrumentArticulationMapSet, type InstrumentExpressiveQuality, type InstrumentGesturePerformance, type InstrumentMidiChannelMapSet, type InstrumentProfile, type InstrumentProfileCatalog, type InstrumentRecommendationSet, type InstrumentPerformanceRetargetSet, type InstrumentRangeReviewSet, type LoudnessGestureExpressionSketch, type LoudnessGestureNoteSketch, type LowEndSupportProposal, type LyricLine, type LyricPhrase, type LyricSheetStructurePreview, type LyricTimelineMarker, type LyricTimelineView, type LyricWord, type MusicalKey, type NoteLetter, type OnsetGestureNoteSketch, type PerformanceObservationReviewVerdict, type PitchGestureNoteSketch, type PortableProjectImportPreview, type ProjectAsset, type ProjectResponse, type ProjectSummary, type ProposedSongSection, type ProsodicWeight, type ProsodyScore, type PulseProposal, type RangeCollisionKind, type RecoverySummary, type RhythmCandidate, type ScaleMode, type SectionDelivery, type SectionDensity, type SectionEnergy, type SectionKind, type SongGenre, type SongProject, type StressLevel, type StructuralFunction, type TextureProposal, type TrashedProjectSummary, type VoiceLeadingReview, type WorkspaceHealth } from './api'
import { activityLog } from './logging'
import { creatorDestination, creatorProgress, creatorStages, type CreatorStage as DesktopCreatorStage } from './creatorJourney.js'
import { demoReadiness, firstWritableEmptyLyricLine, matchingLyricSheetPreview } from './demoReadiness.js'
import { phoneCaptureReadiness, phoneCreatorStages, phoneDestination, phoneEditorChrome, phoneJourneyProgress, phoneLayoutMaxWidth, phoneShowsSongOutline, remapDesktopStageForPhone, remapPhoneStageForDesktop, type CreatorJourneyStage } from './phoneJourneyModel.js'
import { noteOwners, noteRemovalGuidance } from './noteOwnership.js'
import { adjacentSectionId, songOutline, structuralRoleReview } from './songOutline.js'
import { structuralRole, structuralRoles } from './structuralRoles.js'
import { chordToneNames, voicingIssues } from './voicingValidation.js'
import type { RegisteredPitch } from './api'
import { ChordAudition } from './chordAudition'
import { PartAudition } from './partAudition'
import { assemblePartNotes, formatTransportPosition, musicalPositionFromTicks, scheduleAbsoluteNotes, scheduleAssembledNotes, tickFromSeconds } from './partAuditionModel.js'
import { PlaybackTransport } from './playbackTransport'
import { activateApplicationShellUpdate, isStandaloneApplication, registerApplicationShell, type InstallPromptEvent } from './pwa'
import { cacheBrowserProject, discardBrowserProject, discardBrowserRecovery, discardDeviceLyricCapture, listBrowserProjects, listBrowserRecoveries, listDeviceLyricCaptures, loadBrowserProject, loadBrowserRecovery, loadDeviceLyricCapture, protectBrowserRecovery, saveDeviceLyricCapture, type BrowserProjectRecord, type BrowserRecoveryRecord, type DeviceLyricCaptureRecord } from './browserRecovery'
import { browserRecoveryNotice, summarizeBrowserRecovery } from './browserRecoveryModel.js'
import { browserProjectNotice, summarizeBrowserProject } from './browserProjectModel.js'
import { deviceLyricCaptureNotice, deviceLyricCaptureRecentLimit, deviceLyricCaptureResultStats, deviceLyricCaptureSnapshot, filterDeviceLyricCaptures, summarizeDeviceLyricCapture } from './deviceLyricCaptureModel.js'
import { buildRecoveryQueue, recoveryQueueStats, recoveryRecentLimit, recoverySoftCap, recoveryStaleDays, type RecoveryQueueItem } from './recoveryQueueModel.js'
import { filterProjectLibrary, libraryRecentLimit, libraryResultStats, projectLibraryStage, type ProjectLibraryStage } from './libraryHygieneModel.js'
import { buildTrashQueue, filterTrashQueue, trashAgeLabel, trashOldDays, trashRecentLimit, trashResultStats } from './trashHygieneModel.js'
import { microphonePreflightFailure, verifyMicrophoneInput, vocalCaptureSupport } from './vocalCapturePreflight.js'
import { isPortableProjectPackage, portableExportFileName, portableImportLimit, portableImportLimitMessage } from './portableProjectPackage.js'
import { beginRoughVocalCapture, formatRoughVocalBytes, formatRoughVocalDuration, roughVocalMaximumByteLength, roughVocalMaximumDurationMs, type CapturedRoughVocal, type RoughVocalCaptureSession } from './roughVocalCapture.js'
import { analyzeSavedVocalTake, loudnessAnalyzerId, loudnessObservationKind } from './loudnessAnalysis.js'
import { analyzeSavedVocalTakePitch, pitchAnalyzerId, pitchObservationKind } from './pitchAnalysis.js'
import { analyzeSavedVocalTakeOnsets, onsetAnalyzerId, onsetObservationKind } from './onsetAnalysis.js'
import { buildPerformanceEvidenceGroups, nextPerformanceEvidenceVisibleCount } from './performanceEvidenceInspector.js'

const response = ref<ProjectResponse | null>(null)
const projectId = ref(localStorage.getItem('maskilForge.projectId') ?? '')
const view = ref<'home' | 'device-capture' | 'offline-review' | 'recovery' | 'trash' | 'capture' | 'structure'>('home')
const projects = ref<ProjectSummary[]>([])
const recoverySnapshots = ref<RecoverySummary[]>([])
const browserRecoveries = ref<BrowserRecoveryRecord[]>([])
const browserProjects = ref<BrowserProjectRecord[]>([])
const deviceLyricCaptures = ref<DeviceLyricCaptureRecord[]>([])
const activeDeviceLyricCapture = ref<DeviceLyricCaptureRecord | null>(null)
const deviceLyricCaptureSavedSnapshot = ref('')
const deviceLyricCaptureBusy = ref(false)
const deviceLyricCaptureDeleteTarget = ref<{ id: string; title: string } | null>(null)
const deviceLyricCaptureQuery = ref('')
const showAllDeviceLyricCaptureResults = ref(false)
const deviceLyricCaptureCleanupMode = ref(false)
const selectedDeviceLyricCaptureIds = ref<string[]>([])
const bulkDeviceLyricCaptureDeleteOpen = ref(false)
const bulkDeviceLyricCaptureDeleteCancelButton = ref<HTMLButtonElement | null>(null)
const roughVocalSupport = vocalCaptureSupport(window)
const microphonePreflightState = ref<'idle' | 'checking' | 'ready' | 'failed'>('idle')
const microphonePreflightLabel = ref('')
const microphonePreflightMessage = ref('')
const roughVocalCaptureState = ref<'idle' | 'requesting' | 'recording' | 'review' | 'saving' | 'saved' | 'failed'>('idle')
const roughVocalCaptureMessage = ref('')
const pendingRoughVocal = ref<(CapturedRoughVocal & { projectId: string; url: string }) | null>(null)
const roughVocalRemovalTarget = ref<{ asset: ProjectAsset; takeNumber: number } | null>(null)
const roughVocalRemovalCancelButton = ref<HTMLButtonElement | null>(null)
const roughVocalRenameTarget = ref<ProjectAsset | null>(null)
const roughVocalRenameName = ref('')
const roughVocalRenameInput = ref<HTMLInputElement | null>(null)
const loudnessAnalysisAssetId = ref('')
const loudnessAnalysisMessages = reactive<Record<string, string>>({})
const pitchAnalysisAssetId = ref('')
const pitchAnalysisMessages = reactive<Record<string, string>>({})
const onsetAnalysisAssetId = ref('')
const onsetAnalysisMessages = reactive<Record<string, string>>({})
const performanceEvidenceVisibility = reactive<Record<string, Record<string, number>>>({})
const performanceReviewMessages = reactive<Record<string, string>>({})
const observationCorrectionDrafts = reactive<Record<string, Record<string, string>>>({})
let roughVocalCaptureSession: RoughVocalCaptureSession | null = null
let roughVocalAutoStopTimer: number | undefined
const offlineReviewProject = ref<BrowserProjectRecord | null>(null)
const trashedProjects = ref<TrashedProjectSummary[]>([])
const libraryBusy = ref(true)
const workspaceConnection = ref<'checking' | 'ready' | 'unavailable'>('checking')
const workspaceHealth = ref<WorkspaceHealth | null>(null)
const instrumentProfiles = ref<InstrumentProfileCatalog | null>(null)
const instrumentQualityFilter = ref<InstrumentExpressiveQuality | ''>('')
const instrumentRecommendations = ref<InstrumentRecommendationSet | null>(null)
const instrumentRangeReviews = ref<InstrumentRangeReviewSet | null>(null)
const instrumentArticulationMaps = ref<InstrumentArticulationMapSet | null>(null)
const drumKitGmMap = ref<DrumKitGeneralMidiMap | null>(null)
const instrumentMidiChannels = ref<InstrumentMidiChannelMapSet | null>(null)
let instrumentRecommendationToken = 0
let instrumentRangeReviewToken = 0
const workspaceCheckBusy = ref(false)
const installPrompt = ref<InstallPromptEvent | null>(null)
const applicationInstalled = ref(isStandaloneApplication())
const shellUpdateRegistration = ref<ServiceWorkerRegistration | null>(null)
const status = ref('Begin a new song or open an existing project.')
const busy = ref(false)
const structurePreview = ref<LyricSheetStructurePreview | null>(null)
const previewedLyricSheet = ref('')
const savedSnapshot = ref('')
const cleanLabel = ref<'clean' | 'saved'>('clean')
const confirmationOpen = ref(false)
const deleteConfirmationOpen = ref(false)
const deleteTarget = ref<{ id: string; title: string } | null>(null)
const permanentDeleteTarget = ref<{ id: string; title: string } | null>(null)
const recoveryDiscardTarget = ref<RecoveryQueueItem | null>(null)
const recoveryDiscardCancelButton = ref<HTMLButtonElement | null>(null)
const staleRecoveryCleanupOpen = ref(false)
const staleRecoveryCleanupCancelButton = ref<HTMLButtonElement | null>(null)
const showAllRecoveries = ref(false)
const libraryQuery = ref('')
const libraryStageFilter = ref<ProjectLibraryStage>('all')
const showAllLibraryResults = ref(false)
const libraryCleanupMode = ref(false)
const selectedLibraryProjectIds = ref<string[]>([])
const bulkTrashOpen = ref(false)
const bulkTrashCancelButton = ref<HTMLButtonElement | null>(null)
const trashQuery = ref('')
const showAllTrashResults = ref(false)
const trashSelectionMode = ref(false)
const selectedTrashProjectIds = ref<string[]>([])
const bulkRestoreOpen = ref(false)
const bulkRestoreCancelButton = ref<HTMLButtonElement | null>(null)
const bulkPermanentDeleteOpen = ref(false)
const bulkPermanentDeleteCancelButton = ref<HTMLButtonElement | null>(null)
const portableImportInput = ref<HTMLInputElement | null>(null)
let recoveryDiscardReturnFocus: HTMLElement | null = null
const firstPartConfirmation = ref<{ label: string; proceed: () => void } | null>(null)
const pendingAction = ref<'load' | 'new' | 'home' | 'import'>('load')
const pendingLoadId = ref('')
const pendingPortableImport = ref<{ fileName: string; projectJson?: string; packageBytes?: Blob; importAsCopy: boolean } | null>(null)
const portableImportPreview = ref<PortableProjectImportPreview | null>(null)
const sessionId = crypto.randomUUID()
const persistedRevision = ref('')
const recoveryBlocked = ref(false)
const browserRecoverySyncBusy = ref(false)
const browserRecoveryNeedsReview = ref(false)
let recoveryTimer: ReturnType<typeof setTimeout> | undefined
let deviceLyricCaptureTimer: ReturnType<typeof setTimeout> | undefined
const project = computed(() => response.value?.project ?? null)
const structureLocked = computed(() => Boolean(project.value?.musicalParts.length))
const workspaceConnectionTitle = computed(() => workspaceConnection.value === 'ready'
  ? 'Local workspace connected'
  : workspaceConnection.value === 'unavailable'
    ? 'Local workspace unavailable'
    : 'Checking local workspace')
const workspaceConnectionDetail = computed(() => workspaceConnection.value === 'ready'
  ? `Project schema ${workspaceHealth.value?.schemaVersion ?? '—'} · Songs are stored by this Maskil Forge host. Portable export moves them between devices.`
  : workspaceConnection.value === 'unavailable'
    ? 'Host-owned songs are paused until the project service reconnects. Browser-owned lyric captures remain editable on this device, and explicitly cached saves remain view only.'
    : 'Confirming that the project service and local song storage are available.')
const applicationShellDetail = computed(() => shellUpdateRegistration.value
  ? 'A newer interface is ready. Applying it will reload this page without changing project data.'
  : applicationInstalled.value
    ? 'Installed shell available. Browser-owned lyric captures can be edited offline; host-owned songs still require the connected workspace.'
    : installPrompt.value
      ? 'Installation is available on this device. Offline work is currently limited to browser-owned raw lyric captures.'
      : '')
function projectSnapshot(value: SongProject) {
  const { lastModifiedUtc: _revision, ...creativeState } = value
  return JSON.stringify(creativeState)
}
const serializedProject = computed(() => project.value ? projectSnapshot(project.value) : '')
const isDirty = computed(() => Boolean(project.value) && serializedProject.value !== savedSnapshot.value)
const performanceEvidenceByAsset = computed(() => Object.fromEntries((project.value?.assets ?? []).map(asset => [
  asset.id,
  buildPerformanceEvidenceGroups(
    project.value?.performanceObservations,
    asset.id,
    performanceEvidenceVisibility[asset.id] ?? {},
    project.value?.performanceObservationReviews,
    project.value?.performanceObservationCorrections,
    project.value?.performanceObservationGestures,
  ),
])))
const browserRecoverySummaries = computed(() => browserRecoveries.value.map(summarizeBrowserRecovery))
const browserProjectSummaries = computed(() => browserProjects.value.map(summarizeBrowserProject))
const browserProjectDetail = computed(() => browserProjectNotice(browserProjects.value.length))
const deviceLyricCaptureSummaries = computed(() => deviceLyricCaptures.value.map(summarizeDeviceLyricCapture))
const deviceLyricCaptureDetail = computed(() => deviceLyricCaptureNotice(deviceLyricCaptures.value.length))
const filteredDeviceLyricCaptures = computed(() => filterDeviceLyricCaptures(deviceLyricCaptureSummaries.value, deviceLyricCaptureQuery.value))
const deviceLyricCaptureResults = computed(() => deviceLyricCaptureResultStats(filteredDeviceLyricCaptures.value, showAllDeviceLyricCaptureResults.value))
const visibleDeviceLyricCaptures = computed(() => filteredDeviceLyricCaptures.value.slice(0, deviceLyricCaptureResults.value.visibleCount))
const selectedDeviceLyricCaptures = computed(() => {
  const selectedIds = new Set(selectedDeviceLyricCaptureIds.value)
  return deviceLyricCaptureSummaries.value.filter(summary => selectedIds.has(summary.id))
})
const serializedDeviceLyricCapture = computed(() => activeDeviceLyricCapture.value ? deviceLyricCaptureSnapshot(activeDeviceLyricCapture.value) : '')
const deviceLyricCaptureDirty = computed(() => Boolean(activeDeviceLyricCapture.value) && serializedDeviceLyricCapture.value !== deviceLyricCaptureSavedSnapshot.value)
const recoveryQueue = computed(() => buildRecoveryQueue(recoverySnapshots.value, browserRecoverySummaries.value))
const recoveryHygiene = computed(() => recoveryQueueStats(recoveryQueue.value))
const recoveryCount = computed(() => recoveryHygiene.value.uniqueCount)
const visibleRecoveryQueue = computed(() => showAllRecoveries.value ? recoveryQueue.value : recoveryQueue.value.slice(0, recoveryRecentLimit))
const staleRecoveryQueue = computed(() => recoveryQueue.value.filter(snapshot => snapshot.isStale))
const filteredLibraryProjects = computed(() => filterProjectLibrary(projects.value, libraryQuery.value, libraryStageFilter.value))
const libraryResults = computed(() => libraryResultStats(filteredLibraryProjects.value, showAllLibraryResults.value))
const visibleLibraryProjects = computed(() => filteredLibraryProjects.value.slice(0, libraryResults.value.visibleCount))
const selectedLibraryProjects = computed(() => {
  const selectedIds = new Set(selectedLibraryProjectIds.value)
  return projects.value.filter(summary => selectedIds.has(summary.id) && projectLibraryStage(summary) === 'empty')
})
const trashQueue = computed(() => buildTrashQueue(trashedProjects.value))
const filteredTrashQueue = computed(() => filterTrashQueue(trashQueue.value, trashQuery.value))
const trashResults = computed(() => trashResultStats(filteredTrashQueue.value, showAllTrashResults.value))
const visibleTrashQueue = computed(() => filteredTrashQueue.value.slice(0, trashResults.value.visibleCount))
const selectedTrashProjects = computed(() => {
  const selectedIds = new Set(selectedTrashProjectIds.value)
  return trashQueue.value.filter(summary => selectedIds.has(summary.id))
})
const browserRecoveryDetail = computed(() => browserRecoveries.value.length && browserRecoveryNeedsReview.value
  ? 'Browser-protected work needs review before it can return to the local project service.'
  : browserRecoveryNotice(browserRecoveries.value.length, workspaceConnection.value === 'ready'))
const currentProjectBrowserProtected = computed(() => Boolean(project.value && browserRecoveries.value.some(snapshot => snapshot.projectId === project.value?.id)))
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
const instrumentExpressiveQualities: InstrumentExpressiveQuality[] = ['Warm', 'Bright', 'Intimate', 'Sustained', 'Percussive', 'Agile']
const placementDrafts = reactive<Record<string, BeatPosition>>({})
const candidateLabelDrafts = reactive<Record<string, string>>({})
const harmonyCandidateLabelDrafts = reactive<Record<string, string>>({})
const foundationSourceDrafts = reactive<Record<string, string>>({})
const structuralRoleDrafts = reactive<Record<string, StructuralFunction>>({})
const voicingDrafts = reactive<Record<string, string>>({})
const prosodyScores = reactive<Record<string, ProsodyScore>>({})
const voiceLeadingReviews = reactive<Record<string, VoiceLeadingReview>>({})
const harmonyNoteSketches = reactive<Record<string, HarmonyNoteSketch>>({})
const pitchGestureNoteSketches = reactive<Record<string, PitchGestureNoteSketch>>({})
const onsetGestureNoteSketches = reactive<Record<string, OnsetGestureNoteSketch>>({})
const loudnessGestureNoteSketches = reactive<Record<string, LoudnessGestureNoteSketch>>({})
const loudnessGestureExpressionSketches = reactive<Record<string, LoudnessGestureExpressionSketch>>({})
const instrumentPerformanceSketches = reactive<Record<string, InstrumentPerformanceRetargetSet>>({})
const instrumentSketchPartIds = reactive<Record<string, string>>({})
const lowEndSupportProposals = reactive<Record<string, LowEndSupportProposal>>({})
const pulseProposals = reactive<Record<string, PulseProposal>>({})
const harmonySupportProposals = reactive<Record<string, HarmonySupportProposal>>({})
const textureProposals = reactive<Record<string, TextureProposal>>({})
const hookReinforcementProposals = reactive<Record<string, HookReinforcementProposal>>({})
const countermelodyProposals = reactive<Record<string, CountermelodyProposal>>({})
const accentProposals = reactive<Record<string, AccentProposal>>({})
const chordAudition = new ChordAudition()
const partAudition = new PartAudition()
const playbackTransport = new PlaybackTransport()
const auditionState = reactive({ sectionId: '', messageSectionId: '', message: '' })
const partAuditionState = reactive({ sectionId: '', messageSectionId: '', message: '' })
const transportState = reactive({ playing: false, positionLabel: 'Bar 1 · Beat 1', message: '', noteCount: 0 })
const lyricTimeline = ref<LyricTimelineView | null>(null)
const timelineOverlayCandidateId = ref('')
const selectedTimelineMarkerKey = ref('')
let timelineRefreshToken = 0

function accept(next: ProjectResponse, message: string, markPersisted = false) {
  if (pendingRoughVocal.value && pendingRoughVocal.value.projectId !== next.project.id)
    discardPendingRoughVocal(false)
  stopChordAudition()
  stopPartAudition()
  stopTransport()
  response.value = next
  Object.keys(placementDrafts).forEach(key => delete placementDrafts[key])
  Object.keys(harmonyCandidateLabelDrafts).forEach(key => delete harmonyCandidateLabelDrafts[key])
  Object.keys(prosodyScores).forEach(key => delete prosodyScores[key])
  Object.keys(voiceLeadingReviews).forEach(key => delete voiceLeadingReviews[key])
  Object.keys(harmonyNoteSketches).forEach(key => delete harmonyNoteSketches[key])
  Object.keys(lowEndSupportProposals).forEach(key => delete lowEndSupportProposals[key])
  Object.keys(pulseProposals).forEach(key => delete pulseProposals[key])
  Object.keys(harmonySupportProposals).forEach(key => delete harmonySupportProposals[key])
  Object.keys(textureProposals).forEach(key => delete textureProposals[key])
  Object.keys(hookReinforcementProposals).forEach(key => delete hookReinforcementProposals[key])
  Object.keys(countermelodyProposals).forEach(key => delete countermelodyProposals[key])
  Object.keys(accentProposals).forEach(key => delete accentProposals[key])
  projectId.value = next.project.id
  localStorage.setItem('maskilForge.projectId', next.project.id)
  status.value = message
  if (markPersisted) {
    savedSnapshot.value = projectSnapshot(next.project)
    persistedRevision.value = next.project.lastModifiedUtc
    recoveryBlocked.value = false
    cleanLabel.value = message.includes('saved') ? 'saved' : 'clean'
    const record: BrowserProjectRecord = {
      projectId: next.project.id,
      project: structuredClone(next.project),
      savedAtUtc: new Date().toISOString(),
    }
    void cacheBrowserProject(record)
      .then(refreshBrowserProjects)
      .catch(error => activityLog.write('warning', 'delivery.offline-review', error instanceof Error ? error.message : 'The saved song could not be cached for offline review.', { projectId: next.project.id }))
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
function openConfirmation(action: 'load' | 'new' | 'home' | 'import') {
  pendingAction.value = action
  confirmationOpen.value = true
  activityLog.write('warning', pendingActionLogName(action), 'Action paused because unsaved changes were detected.')
}
function pendingActionLogName(action: 'load' | 'new' | 'home' | 'import') {
  return action === 'import' ? 'project.portable-import' : `project.${action}`
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
  else if (pendingAction.value === 'import') await performPortableImport()
  else await goHome()
}
async function saveBeforeContinuing() {
  if (await saveProject()) await continuePendingAction()
}
async function discardAndContinue() {
  activityLog.write('warning', pendingActionLogName(pendingAction.value), 'Unsaved editor changes discarded by user.')
  if (project.value) await projectsApi.discardRecovery(project.value.id).catch(() => undefined)
  return await continuePendingAction()
}
function cancelConfirmation() {
  confirmationOpen.value = false
  if (pendingAction.value === 'import') clearPendingPortableImport()
  status.value = 'Cancelled. Your unsaved changes remain in the editor.'
}

function requestPortableImport() {
  if (busy.value) return
  if (portableImportInput.value) portableImportInput.value.value = ''
  portableImportInput.value?.click()
}

async function selectPortableImport(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  const bytes = new Uint8Array(await file.arrayBuffer())
  const isPackage = isPortableProjectPackage(file.name, bytes)
  if (file.size > portableImportLimit(isPackage)) {
    status.value = portableImportLimitMessage(isPackage)
    activityLog.write('warning', 'project.portable-import', status.value, { fileName: file.name, fileSize: file.size, isPackage })
    clearPendingPortableImport()
    return
  }
  try {
    pendingPortableImport.value = isPackage
      ? { fileName: file.name, packageBytes: file, importAsCopy: false }
      : { fileName: file.name, projectJson: new TextDecoder().decode(bytes), importAsCopy: false }
  } catch {
    status.value = 'The selected project file could not be read. Nothing was imported.'
    activityLog.write('error', 'project.portable-import', status.value, { fileName: file.name })
    clearPendingPortableImport()
    return
  }
  busy.value = true
  activityLog.write('info', 'project.portable-import.preview', 'Project file preview requested.', { fileName: file.name, fileSize: file.size, isPackage })
  try {
    const pending = pendingPortableImport.value
    if (!pending) return
    portableImportPreview.value = pending.packageBytes
      ? await projectsApi.previewPortablePackage(pending.packageBytes)
      : await projectsApi.previewPortableProject(pending.projectJson ?? '')
    status.value = portableImportPreview.value.identityConflict
      ? 'This project identity is already stored on this device. Review the safe copy option.'
      : 'Project file validated. Review it before importing.'
    activityLog.write('success', 'project.portable-import.preview', status.value, {
      fileName: file.name,
      projectId: portableImportPreview.value.id,
      sourceSchemaVersion: portableImportPreview.value.sourceSchemaVersion,
      identityConflict: portableImportPreview.value.identityConflict,
      originalVocalCount: portableImportPreview.value.originalVocalCount,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The project file could not be validated. Nothing was imported.'
    activityLog.write('error', 'project.portable-import.preview', status.value, { fileName: file.name })
    clearPendingPortableImport()
  } finally {
    busy.value = false
  }
}

function choosePortableImport(importAsCopy: boolean) {
  const pending = pendingPortableImport.value
  const preview = portableImportPreview.value
  if (!pending || !preview || (preview.identityConflict && !importAsCopy)) return
  pending.importAsCopy = importAsCopy
  portableImportPreview.value = null
  if (isDirty.value) return openConfirmation('import')
  return performPortableImport()
}

function cancelPortableImportPreview() {
  status.value = 'Import cancelled. No project data was changed.'
  activityLog.write('info', 'project.portable-import.preview', status.value, { fileName: pendingPortableImport.value?.fileName ?? null })
  clearPendingPortableImport()
}

async function performPortableImport() {
  const pending = pendingPortableImport.value
  if (!pending) return false
  confirmationOpen.value = false
  const succeeded = await run(
    () => pending.packageBytes
      ? projectsApi.importPortablePackage(pending.packageBytes, pending.importAsCopy)
      : projectsApi.importPortableProject(pending.projectJson ?? '', pending.importAsCopy),
    pending.importAsCopy ? 'Portable project imported as a new copy.' : 'Portable project imported and saved to your song library.',
    'project.portable-import',
    { fileName: pending.fileName, importAsCopy: pending.importAsCopy },
    true,
  )
  clearPendingPortableImport()
  if (succeeded) {
    view.value = project.value?.sections.length ? 'structure' : 'capture'
    activeCreatorStage.value = project.value?.sections.length ? 'shape' : 'idea'
    await Promise.all([refreshLibrary(), refreshRecovery()])
  }
  return succeeded
}

function clearPendingPortableImport() {
  pendingPortableImport.value = null
  portableImportPreview.value = null
  if (portableImportInput.value) portableImportInput.value.value = ''
}
async function saveProject() {
  if (!project.value || !persistedRevision.value) return
  const savingProjectId = project.value.id
  const succeeded = await run(() => projectsApi.save(project.value!, persistedRevision.value), 'Song saved.', 'project.save', { projectId: project.value.id, sectionCount: project.value.sections.length }, true)
  if (succeeded) {
    await discardBrowserRecovery(savingProjectId).catch(error => {
      activityLog.write('warning', 'recovery.browser', error instanceof Error ? error.message : 'A completed browser recovery snapshot could not be cleared.', { projectId: savingProjectId })
    })
    await Promise.all([refreshRecovery(), refreshBrowserRecovery()])
  }
  return succeeded
}
async function saveDraft() {
  const succeeded = await saveProject()
  if (succeeded) await refreshLibrary()
}
function beginDeviceLyricCapture() {
  const now = new Date().toISOString()
  activeDeviceLyricCapture.value = {
    captureId: crypto.randomUUID(),
    title: 'Untitled capture',
    artist: '',
    genre: 'Unspecified',
    description: '',
    rawLyricDraft: '',
    createdAtUtc: now,
    savedAtUtc: now,
  }
  deviceLyricCaptureSavedSnapshot.value = deviceLyricCaptureSnapshot(activeDeviceLyricCapture.value)
  view.value = 'device-capture'
  status.value = 'Write freely. This capture will stay in this browser until you add it to the connected song library.'
  activityLog.write('info', 'delivery.device-capture', 'Browser-owned lyric capture started.', { captureId: activeDeviceLyricCapture.value.captureId })
  void persistDeviceLyricCapture()
}
async function openDeviceLyricCapture(id: string) {
  try {
    const capture = await loadDeviceLyricCapture(id)
    if (!capture) throw new Error('That device lyric capture is no longer available in this browser.')
    activeDeviceLyricCapture.value = structuredClone(capture)
    deviceLyricCaptureSavedSnapshot.value = deviceLyricCaptureSnapshot(capture)
    view.value = 'device-capture'
    status.value = 'Browser-owned lyric capture opened.'
    activityLog.write('info', 'delivery.device-capture', 'Browser-owned lyric capture opened.', { captureId: id })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The device lyric capture could not be opened.'
    await refreshDeviceLyricCaptures()
  }
}
async function persistDeviceLyricCapture() {
  const current = activeDeviceLyricCapture.value
  if (!current) return false
  if (deviceLyricCaptureBusy.value) {
    if (deviceLyricCaptureTimer) clearTimeout(deviceLyricCaptureTimer)
    deviceLyricCaptureTimer = setTimeout(() => void persistDeviceLyricCapture(), 150)
    return false
  }
  const saved: DeviceLyricCaptureRecord = {
    ...(JSON.parse(JSON.stringify(current)) as DeviceLyricCaptureRecord),
    savedAtUtc: new Date().toISOString(),
  }
  let savedSuccessfully = false
  deviceLyricCaptureBusy.value = true
  try {
    await saveDeviceLyricCapture(saved)
    if (activeDeviceLyricCapture.value?.captureId === saved.captureId) {
      activeDeviceLyricCapture.value.savedAtUtc = saved.savedAtUtc
      deviceLyricCaptureSavedSnapshot.value = deviceLyricCaptureSnapshot(saved)
    }
    await refreshDeviceLyricCaptures()
    status.value = 'Saved in this browser on this device.'
    activityLog.write('success', 'delivery.device-capture.save', 'Browser-owned lyric capture saved.', { captureId: saved.captureId })
    savedSuccessfully = true
    return true
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'This browser could not save the lyric capture.'
    activityLog.write('error', 'delivery.device-capture.save', status.value, { captureId: saved.captureId })
    return false
  } finally {
    deviceLyricCaptureBusy.value = false
    if (savedSuccessfully && activeDeviceLyricCapture.value?.captureId === saved.captureId && deviceLyricCaptureDirty.value) {
      if (deviceLyricCaptureTimer) clearTimeout(deviceLyricCaptureTimer)
      deviceLyricCaptureTimer = setTimeout(() => void persistDeviceLyricCapture(), 150)
    }
  }
}
async function closeDeviceLyricCapture() {
  if (deviceLyricCaptureDirty.value && !(await persistDeviceLyricCapture())) return
  activeDeviceLyricCapture.value = null
  deviceLyricCaptureSavedSnapshot.value = ''
  view.value = 'home'
  status.value = deviceLyricCaptureDetail.value
  await refreshDeviceLyricCaptures()
}
async function addDeviceLyricCaptureToLibrary() {
  const capture = activeDeviceLyricCapture.value
  if (!capture || workspaceConnection.value !== 'ready' || !capture.title.trim() || !capture.rawLyricDraft.trim()) return
  if (deviceLyricCaptureDirty.value && !(await persistDeviceLyricCapture())) return
  deviceLyricCaptureBusy.value = true
  activityLog.write('info', 'delivery.device-capture.handoff', 'Device lyric capture handoff requested.', { captureId: capture.captureId })
  try {
    const created = await projectsApi.createFromDeviceLyricCapture({
      title: capture.title,
      artist: capture.artist,
      genre: capture.genre,
      description: capture.description,
      rawLyricDraft: capture.rawLyricDraft,
    })
    accept(created, 'Device lyric capture added to the connected song library.', true)
    await discardDeviceLyricCapture(capture.captureId)
    activeDeviceLyricCapture.value = null
    deviceLyricCaptureSavedSnapshot.value = ''
    view.value = 'capture'
    activeCreatorStage.value = 'words'
    await Promise.all([refreshDeviceLyricCaptures(), refreshLibrary()])
    activityLog.write('success', 'delivery.device-capture.handoff', 'Device lyric capture became a new saved song.', { captureId: capture.captureId, projectId: created.project.id })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The device lyric capture could not be added to the song library.'
    activityLog.write('error', 'delivery.device-capture.handoff', status.value, { captureId: capture.captureId })
  } finally {
    deviceLyricCaptureBusy.value = false
  }
}
function requestDeviceLyricCaptureDelete(id: string, title: string) {
  deviceLyricCaptureDeleteTarget.value = { id, title }
  activityLog.write('warning', 'delivery.device-capture.delete', 'Device lyric capture deletion confirmation requested.', { captureId: id })
}
function cancelDeviceLyricCaptureDelete() {
  deviceLyricCaptureDeleteTarget.value = null
}
async function confirmDeviceLyricCaptureDelete() {
  const target = deviceLyricCaptureDeleteTarget.value
  if (!target) return
  deviceLyricCaptureBusy.value = true
  try {
    await discardDeviceLyricCapture(target.id)
    if (activeDeviceLyricCapture.value?.captureId === target.id) {
      activeDeviceLyricCapture.value = null
      deviceLyricCaptureSavedSnapshot.value = ''
      view.value = 'home'
    }
    deviceLyricCaptureDeleteTarget.value = null
    await refreshDeviceLyricCaptures()
    status.value = `“${target.title}” was permanently removed from this browser.`
    activityLog.write('success', 'delivery.device-capture.delete', status.value, { captureId: target.id })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The device lyric capture could not be deleted.'
    activityLog.write('error', 'delivery.device-capture.delete', status.value, { captureId: target.id })
  } finally {
    deviceLyricCaptureBusy.value = false
  }
}
function beginDeviceLyricCaptureCleanup() {
  deviceLyricCaptureCleanupMode.value = true
  deviceLyricCaptureQuery.value = ''
  showAllDeviceLyricCaptureResults.value = false
  selectedDeviceLyricCaptureIds.value = []
  status.value = 'Review browser-owned captures. Nothing is selected or removed automatically.'
}
function finishDeviceLyricCaptureCleanup() {
  deviceLyricCaptureCleanupMode.value = false
  deviceLyricCaptureQuery.value = ''
  showAllDeviceLyricCaptureResults.value = false
  selectedDeviceLyricCaptureIds.value = []
  status.value = 'Song library.'
}
function setDeviceLyricCaptureSelected(id: string, event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  const selected = new Set(selectedDeviceLyricCaptureIds.value)
  if (checked) selected.add(id)
  else selected.delete(id)
  selectedDeviceLyricCaptureIds.value = [...selected]
}
function selectVisibleDeviceLyricCaptures() {
  const selected = new Set(selectedDeviceLyricCaptureIds.value)
  visibleDeviceLyricCaptures.value.forEach(summary => selected.add(summary.id))
  selectedDeviceLyricCaptureIds.value = [...selected]
}
function clearDeviceLyricCaptureSelection() { selectedDeviceLyricCaptureIds.value = [] }
async function requestBulkDeviceLyricCaptureDelete() {
  if (selectedDeviceLyricCaptures.value.length === 0) return
  bulkDeviceLyricCaptureDeleteOpen.value = true
  activityLog.write('warning', 'delivery.device-capture.bulk-delete', 'Multi-capture permanent deletion confirmation requested.', { count: selectedDeviceLyricCaptures.value.length })
  await nextTick()
  bulkDeviceLyricCaptureDeleteCancelButton.value?.focus()
}
function cancelBulkDeviceLyricCaptureDelete() {
  bulkDeviceLyricCaptureDeleteOpen.value = false
  activityLog.write('info', 'delivery.device-capture.bulk-delete', 'Multi-capture permanent deletion cancelled.')
}
async function confirmBulkDeviceLyricCaptureDelete() {
  const targets = [...selectedDeviceLyricCaptures.value]
  if (targets.length === 0) { bulkDeviceLyricCaptureDeleteOpen.value = false; return }
  deviceLyricCaptureBusy.value = true
  let deleted = 0
  const failedIds: string[] = []
  for (const target of targets) {
    try {
      await discardDeviceLyricCapture(target.id)
      deleted++
    } catch {
      failedIds.push(target.id)
    }
  }
  bulkDeviceLyricCaptureDeleteOpen.value = false
  selectedDeviceLyricCaptureIds.value = failedIds
  await refreshDeviceLyricCaptures()
  if (failedIds.length) {
    status.value = `${deleted} device capture${deleted === 1 ? ' was' : 's were'} permanently deleted. ${failedIds.length} could not be deleted and remain selected.`
    activityLog.write('warning', 'delivery.device-capture.bulk-delete', 'Multi-capture deletion completed with retained failures.', { deleted, failed: failedIds.length })
  } else {
    status.value = `${deleted} browser-owned capture${deleted === 1 ? ' was' : 's were'} permanently deleted from this device.`
    activityLog.write('success', 'delivery.device-capture.bulk-delete', 'Selected browser-owned captures permanently deleted.', { deleted })
  }
  deviceLyricCaptureBusy.value = false
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
async function goHome() { confirmationOpen.value = false; offlineReviewProject.value = null; view.value = 'home'; await Promise.all([refreshLibrary(), refreshRecovery(), refreshBrowserProjects()]) }
function leaveProtectedOfflineEditor() {
  response.value = null
  savedSnapshot.value = ''
  persistedRevision.value = ''
  confirmationOpen.value = false
  view.value = 'home'
  status.value = browserRecoveryDetail.value
}
function openSummary(id: string) { projectId.value = id; return requestLoad() }
async function openOfflineReview(id: string) {
  try {
    const record = await loadBrowserProject(id)
    if (!record) throw new Error('That saved browser snapshot is no longer available on this device.')
    offlineReviewProject.value = record
    view.value = 'offline-review'
    status.value = `View-only saved snapshot opened for “${record.project.title}”.`
    activityLog.write('info', 'delivery.offline-review', 'Saved browser snapshot opened for view-only review.', { projectId: id })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The saved browser snapshot could not be opened.'
    await refreshBrowserProjects()
  }
}
function closeOfflineReview() {
  offlineReviewProject.value = null
  view.value = 'home'
  status.value = workspaceConnection.value === 'ready' ? 'Song library.' : browserProjectDetail.value
}
async function openOfflineReviewEditable() {
  const id = offlineReviewProject.value?.projectId
  if (!id || workspaceConnection.value !== 'ready') return
  projectId.value = id
  pendingLoadId.value = id
  await performLoad()
  if (view.value !== 'offline-review') offlineReviewProject.value = null
}
function closeCardMenu(event?: Event) {
  const trigger = event?.currentTarget as HTMLElement | undefined
  trigger?.closest('details')?.removeAttribute('open')
}
async function duplicateSong(id: string, title: string, event: Event) {
  closeCardMenu(event)
  busy.value = true
  activityLog.write('info', 'project.duplicate', 'Saved-song duplication requested.', { projectId: id, title })
  try {
    const duplicated = await projectsApi.duplicate(id)
    status.value = `“${duplicated.project.title}” is ready in your song library.`
    activityLog.write('success', 'project.duplicate', 'Independent song copy created.', {
      sourceProjectId: id,
      projectId: duplicated.project.id,
      title: duplicated.project.title,
    })
    await refreshLibrary()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The saved song could not be duplicated.'
    activityLog.write('error', 'project.duplicate', status.value, { projectId: id, title })
  } finally { busy.value = false }
}
function requestDelete(id: string, title: string, event?: Event) {
  closeCardMenu(event)
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
    await Promise.all([
      discardBrowserRecovery(target.id).catch(() => undefined),
      discardBrowserProject(target.id).catch(() => undefined),
    ])
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
    await Promise.all([refreshLibrary(), refreshRecovery(), refreshBrowserProjects()])
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The song could not be deleted.'
    activityLog.write('error', 'project.delete', status.value, { projectId: target.id })
  } finally { busy.value = false }
}
async function openTrash() {
  view.value = 'trash'
  trashQuery.value = ''
  showAllTrashResults.value = false
  trashSelectionMode.value = false
  selectedTrashProjectIds.value = []
  await refreshTrash()
}
async function refreshTrash() {
  libraryBusy.value = true
  try { trashedProjects.value = await projectsApi.listTrash() }
  catch (error) { status.value = error instanceof Error ? error.message : 'Could not load Trash.' }
  finally { libraryBusy.value = false }
}
async function refreshRecovery() {
  try { recoverySnapshots.value = await projectsApi.listRecovery() }
  catch (error) { status.value = error instanceof Error ? error.message : 'Could not check recovery snapshots.' }
}
async function refreshBrowserRecovery() {
  try {
    browserRecoveries.value = await listBrowserRecoveries()
  } catch (error) {
    activityLog.write('error', 'recovery.browser', error instanceof Error ? error.message : 'Browser recovery storage could not be read.')
  }
}
async function refreshBrowserProjects() {
  try {
    browserProjects.value = await listBrowserProjects()
  } catch (error) {
    activityLog.write('warning', 'delivery.offline-review', error instanceof Error ? error.message : 'Saved browser snapshots could not be read.')
  }
}
async function refreshDeviceLyricCaptures() {
  try {
    deviceLyricCaptures.value = await listDeviceLyricCaptures()
  } catch (error) {
    activityLog.write('warning', 'delivery.device-capture', error instanceof Error ? error.message : 'Device lyric captures could not be read.')
  }
}

async function checkRoughVocalMicrophone() {
  if (!roughVocalSupport.supported) {
    microphonePreflightState.value = 'failed'
    microphonePreflightMessage.value = roughVocalSupport.reason
    status.value = roughVocalSupport.reason
    return
  }

  microphonePreflightState.value = 'checking'
  microphonePreflightLabel.value = ''
  microphonePreflightMessage.value = 'Waiting for this browser to confirm microphone access…'
  activityLog.write('info', 'vocal.preflight', 'Microphone readiness check requested. No audio will be recorded or saved.')

  try {
    const result = await verifyMicrophoneInput(navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices))
    microphonePreflightState.value = 'ready'
    microphonePreflightLabel.value = result.label
    microphonePreflightMessage.value = 'Microphone access is ready. The test stream is closed and no audio was recorded or saved.'
    status.value = 'Microphone ready for a future rough vocal take. No audio was recorded or saved.'
    activityLog.write('success', 'vocal.preflight', 'Microphone readiness confirmed and the test stream was closed.', { trackCount: result.trackCount })
  } catch (error) {
    microphonePreflightState.value = 'failed'
    microphonePreflightMessage.value = microphonePreflightFailure(error)
    status.value = microphonePreflightMessage.value
    activityLog.write('warning', 'vocal.preflight', microphonePreflightMessage.value, {
      reason: error instanceof DOMException || error instanceof Error ? error.name : 'UnknownError',
    })
  }
}

function clearRoughVocalAutoStop() {
  if (roughVocalAutoStopTimer !== undefined) window.clearTimeout(roughVocalAutoStopTimer)
  roughVocalAutoStopTimer = undefined
}

function releasePendingRoughVocal() {
  if (pendingRoughVocal.value?.url) URL.revokeObjectURL(pendingRoughVocal.value.url)
  pendingRoughVocal.value = null
}

function discardPendingRoughVocal(report = true) {
  clearRoughVocalAutoStop()
  roughVocalCaptureSession?.discard()
  roughVocalCaptureSession = null
  releasePendingRoughVocal()
  roughVocalCaptureState.value = 'idle'
  roughVocalCaptureMessage.value = report ? 'Temporary take discarded. No audio was uploaded or saved.' : ''
  if (report) activityLog.write('info', 'vocal.capture-discard', roughVocalCaptureMessage.value)
}

async function startRoughVocalRecording() {
  if (!project.value || roughVocalCaptureState.value === 'requesting' || roughVocalCaptureState.value === 'recording') return
  if (workspaceConnection.value !== 'ready') {
    roughVocalCaptureState.value = 'failed'
    roughVocalCaptureMessage.value = 'Reconnect to the Maskil Forge host before recording a take for this song.'
    return
  }
  if (isDirty.value) {
    roughVocalCaptureState.value = 'failed'
    roughVocalCaptureMessage.value = 'Save the current words and structure before recording a take for this version.'
    return
  }

  discardPendingRoughVocal(false)
  roughVocalCaptureState.value = 'requesting'
  roughVocalCaptureMessage.value = 'Waiting for microphone access…'
  activityLog.write('info', 'vocal.capture-start', 'Rough vocal recording requested. Audio remains in browser memory until reviewed and explicitly saved.', { projectId: project.value.id })
  try {
    roughVocalCaptureSession = await beginRoughVocalCapture(window)
    roughVocalCaptureState.value = 'recording'
    roughVocalCaptureMessage.value = 'Recording now. Stop when the rough performance is complete; recording stops automatically after one minute.'
    activityLog.write('success', 'vocal.capture-start', 'Rough vocal recording started.', {
      projectId: project.value.id,
      mediaType: roughVocalCaptureSession.mediaType,
    })
    roughVocalAutoStopTimer = window.setTimeout(() => void stopRoughVocalRecording(true), roughVocalMaximumDurationMs)
  } catch (error) {
    roughVocalCaptureState.value = 'failed'
    roughVocalCaptureMessage.value = microphonePreflightFailure(error)
    activityLog.write('warning', 'vocal.capture-start', roughVocalCaptureMessage.value, {
      projectId: project.value.id,
      reason: error instanceof DOMException || error instanceof Error ? error.name : 'UnknownError',
    })
  }
}

async function stopRoughVocalRecording(automatic = false) {
  const session = roughVocalCaptureSession
  const captureProjectId = project.value?.id
  if (!session || roughVocalCaptureState.value !== 'recording' || !captureProjectId) return
  clearRoughVocalAutoStop()
  roughVocalCaptureSession = null
  try {
    const capture = await session.stop()
    if (capture.blob.size === 0) throw new Error('The browser returned an empty recording.')
    if (capture.blob.size > roughVocalMaximumByteLength) throw new Error('This rough vocal take exceeds the 25 MB save limit. Record a shorter take.')
    const url = URL.createObjectURL(capture.blob)
    pendingRoughVocal.value = { ...capture, projectId: captureProjectId, url }
    roughVocalCaptureState.value = 'review'
    roughVocalCaptureMessage.value = `${automatic ? 'One-minute limit reached. ' : ''}Listen before deciding whether to save this ${formatRoughVocalDuration(capture.durationMs)} take.`
    activityLog.write('success', 'vocal.capture-stop', 'Rough vocal recording stopped and is waiting for artist review.', {
      projectId: captureProjectId,
      durationMs: Math.round(capture.durationMs),
      byteLength: capture.blob.size,
      mediaType: capture.mediaType,
    })
  } catch (error) {
    roughVocalCaptureState.value = 'failed'
    roughVocalCaptureMessage.value = error instanceof Error ? error.message : 'The browser could not finish this rough vocal take.'
    activityLog.write('error', 'vocal.capture-stop', roughVocalCaptureMessage.value, { projectId: captureProjectId })
  }
}

async function savePendingRoughVocal() {
  const capture = pendingRoughVocal.value
  if (!capture || !project.value || capture.projectId !== project.value.id || !persistedRevision.value || isDirty.value) return
  roughVocalCaptureState.value = 'saving'
  roughVocalCaptureMessage.value = 'Saving the reviewed take into this song’s verified local assets…'
  activityLog.write('info', 'vocal.capture-save', 'Artist approved rough vocal upload.', {
    projectId: project.value.id,
    durationMs: Math.round(capture.durationMs),
    byteLength: capture.blob.size,
  })
  try {
    const next = await projectsApi.saveOriginalVocalTake(project.value.id, persistedRevision.value, capture.blob)
    accept(next, 'Rough vocal take saved with this song.', true)
    releasePendingRoughVocal()
    roughVocalCaptureState.value = 'saved'
    roughVocalCaptureMessage.value = 'Take saved. Its bytes are now covered by backup, Trash, recovery, and portable .maskil export.'
    activityLog.write('success', 'vocal.capture-save', roughVocalCaptureMessage.value, {
      projectId: next.project.id,
      originalVocalCount: next.project.assets.length,
    })
  } catch (error) {
    roughVocalCaptureState.value = 'review'
    roughVocalCaptureMessage.value = error instanceof Error ? `${error.message} The reviewed recording remains in this tab so you can retry.` : 'The take could not be saved. The reviewed recording remains in this tab so you can retry.'
    activityLog.write('error', 'vocal.capture-save', roughVocalCaptureMessage.value, { projectId: project.value.id })
  }
}

function logRoughVocalPlayback(source: 'temporary' | 'saved', assetId?: string) {
  activityLog.write('info', 'vocal.capture-playback', source === 'temporary' ? 'Artist played the temporary rough vocal review.' : 'Artist played a saved rough vocal take.', {
    projectId: project.value?.id ?? '',
    source,
    ...(assetId ? { assetId } : {}),
  })
}

async function requestRemoveSavedRoughVocal(asset: ProjectAsset, takeNumber: number) {
  if (!project.value || isDirty.value || busy.value) return
  roughVocalRemovalTarget.value = { asset, takeNumber }
  activityLog.write('warning', 'vocal.take-remove', 'Saved rough vocal removal confirmation requested.', {
    projectId: project.value.id,
    assetId: asset.id,
    byteLength: asset.byteLength,
  })
  await nextTick()
  roughVocalRemovalCancelButton.value?.focus()
}

function cancelRemoveSavedRoughVocal() {
  roughVocalRemovalTarget.value = null
}

async function requestRenameSavedRoughVocal(asset: ProjectAsset) {
  if (!project.value || isDirty.value || busy.value) return
  roughVocalRenameTarget.value = asset
  roughVocalRenameName.value = asset.name
  activityLog.write('info', 'vocal.take-rename', 'Saved rough vocal rename opened.', {
    projectId: project.value.id,
    assetId: asset.id,
  })
  await nextTick()
  roughVocalRenameInput.value?.focus()
  roughVocalRenameInput.value?.select()
}

function cancelRenameSavedRoughVocal() {
  roughVocalRenameTarget.value = null
  roughVocalRenameName.value = ''
}

async function confirmRenameSavedRoughVocal() {
  const target = roughVocalRenameTarget.value
  const name = roughVocalRenameName.value.trim()
  if (!target || !project.value || !persistedRevision.value || isDirty.value || !name || name.length > 80) return
  busy.value = true
  try {
    const next = await projectsApi.renameOriginalVocalTake(project.value.id, target.id, name, persistedRevision.value)
    accept(next, 'Saved rough vocal take renamed.', true)
    cancelRenameSavedRoughVocal()
    activityLog.write('success', 'vocal.take-rename', 'Saved rough vocal rename committed.', {
      projectId: next.project.id,
      assetId: target.id,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The saved rough vocal take could not be renamed.'
    activityLog.write('error', 'vocal.take-rename', status.value, {
      projectId: project.value.id,
      assetId: target.id,
    })
  } finally {
    busy.value = false
  }
}

async function confirmRemoveSavedRoughVocal() {
  const target = roughVocalRemovalTarget.value
  if (!target || !project.value || !persistedRevision.value || isDirty.value) return
  busy.value = true
  try {
    const next = await projectsApi.removeOriginalVocalTake(project.value.id, target.asset.id, persistedRevision.value)
    accept(next, `Take ${target.takeNumber} removed from this saved song.`, true)
    roughVocalRemovalTarget.value = null
    roughVocalCaptureMessage.value = 'Saved take removed from the current song and future exports. The previous version remains protected by the host’s local safety backup.'
    activityLog.write('success', 'vocal.take-remove', roughVocalCaptureMessage.value, {
      projectId: next.project.id,
      assetId: target.asset.id,
      originalVocalCount: next.project.assets.length,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The saved rough vocal take could not be removed.'
    activityLog.write('error', 'vocal.take-remove', status.value, {
      projectId: project.value.id,
      assetId: target.asset.id,
    })
  } finally {
    busy.value = false
  }
}

function loudnessObservationSummary(assetId: string) {
  const frames = project.value?.performanceObservations
    .filter(observation => observation.sourceAssetId === assetId
      && observation.analyzerId === loudnessAnalyzerId
      && observation.kind === loudnessObservationKind)
    .sort((left, right) => left.startMilliseconds - right.startMilliseconds) ?? []
  if (!frames.length) return ''
  const strongestPeak = Math.max(...frames.map(frame => frame.measurements.find(item => item.name === 'peakDbfs')?.value ?? -120))
  const analyzedDuration = frames.at(-1)!.startMilliseconds + frames.at(-1)!.durationMilliseconds
  return `${frames.length} loudness frame${frames.length === 1 ? '' : 's'} across ${formatRoughVocalDuration(analyzedDuration)} · strongest peak ${strongestPeak.toFixed(1)} dBFS`
}

function pitchObservationSummary(assetId: string) {
  const frequencies = project.value?.performanceObservations
    .filter(observation => observation.sourceAssetId === assetId
      && observation.analyzerId === pitchAnalyzerId
      && observation.kind === pitchObservationKind)
    .map(observation => observation.measurements.find(item => item.name === 'frequencyHertz')?.value)
    .filter((value): value is number => value !== undefined)
    .sort((left, right) => left - right) ?? []
  if (!frequencies.length) return ''
  const middle = Math.floor(frequencies.length / 2)
  const median = frequencies.length % 2
    ? frequencies[middle]
    : (frequencies[middle - 1] + frequencies[middle]) / 2
  return `${frequencies.length} confident voiced pitch frame${frequencies.length === 1 ? '' : 's'} · median ${median.toFixed(1)} Hz · evidence only`
}

function onsetObservationSummary(assetId: string) {
  const events = project.value?.performanceObservations
    .filter(observation => observation.sourceAssetId === assetId
      && observation.analyzerId === onsetAnalyzerId
      && observation.kind === onsetObservationKind)
    .sort((left, right) => left.startMilliseconds - right.startMilliseconds) ?? []
  if (!events.length) return ''
  const firstSeconds = (events[0].startMilliseconds / 1000).toFixed(2).replace(/\.?0+$/, '')
  return `${events.length} confident onset candidate${events.length === 1 ? '' : 's'} · first near ${firstSeconds}s · evidence only`
}

function performanceEvidenceGroups(assetId: string) {
  return performanceEvidenceByAsset.value[assetId] ?? []
}

function performanceEvidenceCount(assetId: string) {
  return performanceEvidenceGroups(assetId).reduce((count, group) => count + group.count, 0)
}

function pitchGestureCountForAsset(assetId: string) {
  const current = project.value
  if (!current) return 0
  const observationIds = new Set(
    current.performanceObservations
      .filter(observation => observation.sourceAssetId === assetId)
      .map(observation => observation.id),
  )
  return current.performanceObservationGestures.filter(gesture =>
    observationIds.has(gesture.observationId)
    && gesture.measurements.some(item => item.name === 'frequencyHertz')).length
}

const pitchGestureTakes = computed(() =>
  (project.value?.assets ?? []).filter(asset => pitchGestureCountForAsset(asset.id) > 0))

function onsetGestureCountForAsset(assetId: string) {
  const current = project.value
  if (!current) return 0
  const observationIds = new Set(
    current.performanceObservations
      .filter(observation => observation.sourceAssetId === assetId && observation.kind === onsetObservationKind)
      .map(observation => observation.id),
  )
  return current.performanceObservationGestures.filter(gesture => observationIds.has(gesture.observationId)).length
}

const onsetGestureTakes = computed(() =>
  (project.value?.assets ?? []).filter(asset => onsetGestureCountForAsset(asset.id) > 0))

function loudnessGestureCountForAsset(assetId: string) {
  const current = project.value
  if (!current) return 0
  const observationIds = new Set(
    current.performanceObservations
      .filter(observation => observation.sourceAssetId === assetId && observation.kind === loudnessObservationKind)
      .map(observation => observation.id),
  )
  return current.performanceObservationGestures.filter(gesture => observationIds.has(gesture.observationId)).length
}

const loudnessGestureTakes = computed(() =>
  (project.value?.assets ?? []).filter(asset => loudnessGestureCountForAsset(asset.id) > 0))

const instrumentRetargetTakes = computed(() =>
  (project.value?.assets ?? []).filter(asset =>
    pitchGestureCountForAsset(asset.id) > 0
    || loudnessGestureCountForAsset(asset.id) > 0
    || onsetGestureCountForAsset(asset.id) > 0))


function showMorePerformanceEvidence(assetId: string, groupKey: string, totalCount: number) {
  const visibility = performanceEvidenceVisibility[assetId] ??= {}
  visibility[groupKey] = nextPerformanceEvidenceVisibleCount(visibility[groupKey], totalCount)
}

async function reviewPerformanceObservation(
  assetId: string,
  observationId: string,
  verdict: PerformanceObservationReviewVerdict | null,
) {
  if (!project.value || !persistedRevision.value || isDirty.value || workspaceConnection.value !== 'ready') return
  const reviewProjectId = project.value.id
  busy.value = true
  performanceReviewMessages[assetId] = verdict === null
    ? 'Clearing the artist verdict…'
    : `Marking this analyzer claim ${verdict === 'Accurate' ? 'accurate' : 'inaccurate'}…`
  try {
    const next = await projectsApi.reviewPerformanceObservation(
      reviewProjectId,
      observationId,
      persistedRevision.value,
      verdict,
    )
    if (project.value?.id === reviewProjectId) {
      const message = verdict === null
        ? 'Analyzer claim returned to unreviewed.'
        : `Analyzer claim marked ${verdict === 'Accurate' ? 'accurate' : 'inaccurate'} by the artist.`
      accept(next, message, true)
      performanceReviewMessages[assetId] = verdict === null
        ? `${message} Any stored correction was removed with the verdict.`
        : `${message} ${verdict === 'Inaccurate' ? 'You can now store a separate correction without changing analyzer evidence.' : 'This verdict does not create or correct musical material.'}`
    }
    if (verdict !== 'Inaccurate') delete observationCorrectionDrafts[observationId]
    activityLog.write('success', 'vocal.observation-review', verdict === null
      ? 'Artist review cleared from analyzer evidence.'
      : 'Artist verdict saved for analyzer evidence.', {
      projectId: reviewProjectId,
      assetId,
      observationId,
      verdict: verdict ?? 'Unreviewed',
    })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The analyzer claim could not be reviewed.'
    status.value = message
    performanceReviewMessages[assetId] = message
    activityLog.write('error', 'vocal.observation-review', message, {
      projectId: reviewProjectId,
      assetId,
      observationId,
    })
  } finally {
    busy.value = false
  }
}

function correctionDraftValue(rowId: string, field: { name: string; value: number }) {
  return observationCorrectionDrafts[rowId]?.[field.name] ?? String(field.value)
}

function setCorrectionDraft(rowId: string, name: string, value: string) {
  observationCorrectionDrafts[rowId] ??= {}
  observationCorrectionDrafts[rowId][name] = value
}

async function savePerformanceObservationCorrection(
  assetId: string,
  row: { id: string; correctionFields: Array<{ name: string; unit: string; value: number }> },
) {
  if (!project.value || !persistedRevision.value || isDirty.value || workspaceConnection.value !== 'ready') return
  const correctionProjectId = project.value.id
  const measurements = row.correctionFields.map(field => ({
    name: field.name,
    unit: field.unit,
    value: Number(correctionDraftValue(row.id, field)),
  }))
  if (measurements.some(item => !Number.isFinite(item.value))) {
    performanceReviewMessages[assetId] = 'A correction needs a finite number for every measurement.'
    return
  }
  busy.value = true
  performanceReviewMessages[assetId] = 'Saving the artist correction…'
  try {
    const next = await projectsApi.correctPerformanceObservation(
      correctionProjectId,
      row.id,
      persistedRevision.value,
      measurements,
    )
    delete observationCorrectionDrafts[row.id]
    if (project.value?.id === correctionProjectId) {
      const message = 'Artist correction saved beside the original analyzer claim.'
      accept(next, message, true)
      performanceReviewMessages[assetId] = `${message} Analyzer evidence was not rewritten.`
    }
    activityLog.write('success', 'vocal.observation-correction', 'Artist correction saved for analyzer evidence.', {
      projectId: correctionProjectId,
      assetId,
      observationId: row.id,
      outcome: 'saved',
    })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The analyzer claim could not be corrected.'
    status.value = message
    performanceReviewMessages[assetId] = message
    activityLog.write('error', 'vocal.observation-correction', message, {
      projectId: correctionProjectId,
      assetId,
      observationId: row.id,
    })
  } finally {
    busy.value = false
  }
}

async function clearPerformanceObservationCorrection(assetId: string, observationId: string) {
  if (!project.value || !persistedRevision.value || isDirty.value || workspaceConnection.value !== 'ready') return
  const correctionProjectId = project.value.id
  busy.value = true
  performanceReviewMessages[assetId] = 'Removing the artist correction…'
  try {
    const next = await projectsApi.correctPerformanceObservation(
      correctionProjectId,
      observationId,
      persistedRevision.value,
      null,
    )
    delete observationCorrectionDrafts[observationId]
    if (project.value?.id === correctionProjectId) {
      const message = 'Artist correction removed. Analyzer evidence is unchanged.'
      accept(next, message, true)
      performanceReviewMessages[assetId] = message
    }
    activityLog.write('success', 'vocal.observation-correction', 'Artist correction cleared from analyzer evidence.', {
      projectId: correctionProjectId,
      assetId,
      observationId,
      outcome: 'cleared',
    })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The artist correction could not be removed.'
    status.value = message
    performanceReviewMessages[assetId] = message
    activityLog.write('error', 'vocal.observation-correction', message, {
      projectId: correctionProjectId,
      assetId,
      observationId,
    })
  } finally {
    busy.value = false
  }
}

async function setPerformanceObservationGesture(assetId: string, observationId: string, promoted: true | null) {
  if (!project.value || !persistedRevision.value || isDirty.value || workspaceConnection.value !== 'ready') return
  const gestureProjectId = project.value.id
  busy.value = true
  performanceReviewMessages[assetId] = promoted
    ? 'Promoting approved measurements into an artist gesture…'
    : 'Removing the artist gesture…'
  try {
    const next = await projectsApi.promotePerformanceObservation(
      gestureProjectId,
      observationId,
      persistedRevision.value,
      promoted,
    )
    if (project.value?.id === gestureProjectId) {
      const message = promoted
        ? 'Artist gesture saved from the approved measurements. Analyzer evidence was not rewritten.'
        : 'Artist gesture removed. Analyzer evidence is unchanged.'
      accept(next, message, true)
      performanceReviewMessages[assetId] = message
    }
    activityLog.write('success', 'vocal.observation-gesture', promoted
      ? 'Artist gesture promoted from approved analyzer evidence.'
      : 'Artist gesture cleared from analyzer evidence.', {
      projectId: gestureProjectId,
      assetId,
      observationId,
      outcome: promoted ? 'promoted' : 'cleared',
    })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The artist gesture could not be updated.'
    status.value = message
    performanceReviewMessages[assetId] = message
    activityLog.write('error', 'vocal.observation-gesture', message, {
      projectId: gestureProjectId,
      assetId,
      observationId,
    })
  } finally {
    busy.value = false
  }
}

async function analyzeSavedRoughVocal(asset: ProjectAsset) {
  if (!project.value || !persistedRevision.value || isDirty.value || workspaceConnection.value !== 'ready') return
  const analysisProjectId = project.value.id
  loudnessAnalysisAssetId.value = asset.id
  loudnessAnalysisMessages[asset.id] = 'Decoding the saved take on this device…'
  busy.value = true
  activityLog.write('info', 'vocal.loudness-analysis', 'Artist requested deterministic loudness evidence for a saved take.', {
    projectId: analysisProjectId,
    assetId: asset.id,
  })
  try {
    const frames = await analyzeSavedVocalTake(projectsApi.originalVocalTakeUrl(analysisProjectId, asset.id), window)
    if (project.value?.id !== analysisProjectId)
      throw new Error('Loudness analysis stopped because another song is now open. No evidence was saved.')
    loudnessAnalysisMessages[asset.id] = `Saving ${frames.length} bounded loudness frames without changing the recording…`
    const next = await projectsApi.saveLoudnessAnalysis(analysisProjectId, asset.id, persistedRevision.value, frames)
    if (project.value?.id === analysisProjectId)
      accept(next, `Loudness evidence saved for ${asset.name}.`, true)
    loudnessAnalysisMessages[asset.id] = 'Loudness evidence saved. Rerunning replaces only this analyzer’s earlier frames.'
    activityLog.write('success', 'vocal.loudness-analysis', 'Deterministic loudness evidence saved.', {
      projectId: analysisProjectId,
      assetId: asset.id,
      frameCount: frames.length,
      analyzerId: loudnessAnalyzerId,
    })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The saved take could not be analyzed.'
    status.value = message
    loudnessAnalysisMessages[asset.id] = message
    activityLog.write('error', 'vocal.loudness-analysis', message, {
      projectId: analysisProjectId,
      assetId: asset.id,
      analyzerId: loudnessAnalyzerId,
    })
  } finally {
    loudnessAnalysisAssetId.value = ''
    busy.value = false
  }
}

async function analyzeSavedRoughVocalPitch(asset: ProjectAsset) {
  if (!project.value || !persistedRevision.value || isDirty.value || workspaceConnection.value !== 'ready') return
  const analysisProjectId = project.value.id
  pitchAnalysisAssetId.value = asset.id
  pitchAnalysisMessages[asset.id] = 'Listening for confident voiced frequency on this device…'
  busy.value = true
  activityLog.write('info', 'vocal.pitch-analysis', 'Artist requested deterministic pitch evidence for a saved take.', {
    projectId: analysisProjectId,
    assetId: asset.id,
  })
  try {
    const frames = await analyzeSavedVocalTakePitch(projectsApi.originalVocalTakeUrl(analysisProjectId, asset.id), window)
    if (project.value?.id !== analysisProjectId)
      throw new Error('Pitch analysis stopped because another song is now open. No evidence was saved.')
    pitchAnalysisMessages[asset.id] = frames.length
      ? `Saving ${frames.length} confidence-gated pitch frames without creating notes…`
      : 'No confident voiced pitch was found. Clearing this analyzer’s earlier frames…'
    const next = await projectsApi.savePitchAnalysis(analysisProjectId, asset.id, persistedRevision.value, frames)
    const message = frames.length
      ? `Pitch evidence saved for ${asset.name}.`
      : `No confident voiced pitch found in ${asset.name}; earlier pitch evidence was cleared.`
    if (project.value?.id === analysisProjectId) accept(next, message, true)
    pitchAnalysisMessages[asset.id] = frames.length
      ? 'Pitch evidence saved. It remains separate from notes and rerunning replaces only these frames.'
      : 'No confident voiced pitch was claimed. Earlier frames from this analyzer were cleared.'
    activityLog.write('success', 'vocal.pitch-analysis', frames.length ? 'Deterministic pitch evidence saved.' : 'Pitch analysis completed without a voiced claim.', {
      projectId: analysisProjectId,
      assetId: asset.id,
      frameCount: frames.length,
      analyzerId: pitchAnalyzerId,
    })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The saved take could not be analyzed for pitch.'
    status.value = message
    pitchAnalysisMessages[asset.id] = message
    activityLog.write('error', 'vocal.pitch-analysis', message, {
      projectId: analysisProjectId,
      assetId: asset.id,
      analyzerId: pitchAnalyzerId,
    })
  } finally {
    pitchAnalysisAssetId.value = ''
    busy.value = false
  }
}

async function analyzeSavedRoughVocalOnsets(asset: ProjectAsset) {
  if (!project.value || !persistedRevision.value || isDirty.value || workspaceConnection.value !== 'ready') return
  const analysisProjectId = project.value.id
  onsetAnalysisAssetId.value = asset.id
  onsetAnalysisMessages[asset.id] = 'Listening for confident energy rises on this device…'
  busy.value = true
  activityLog.write('info', 'vocal.onset-analysis', 'Artist requested deterministic onset evidence for a saved take.', {
    projectId: analysisProjectId,
    assetId: asset.id,
  })
  try {
    const events = await analyzeSavedVocalTakeOnsets(projectsApi.originalVocalTakeUrl(analysisProjectId, asset.id), window)
    if (project.value?.id !== analysisProjectId)
      throw new Error('Onset analysis stopped because another song is now open. No evidence was saved.')
    onsetAnalysisMessages[asset.id] = events.length
      ? `Saving ${events.length} confidence-gated onset candidates without changing timing…`
      : 'No confident onset was found. Clearing this analyzer’s earlier candidates…'
    const next = await projectsApi.saveOnsetAnalysis(analysisProjectId, asset.id, persistedRevision.value, events)
    const message = events.length
      ? `Onset evidence saved for ${asset.name}.`
      : `No confident onset found in ${asset.name}; earlier onset evidence was cleared.`
    if (project.value?.id === analysisProjectId) accept(next, message, true)
    onsetAnalysisMessages[asset.id] = events.length
      ? 'Onset evidence saved. It does not set tempo or timing, and rerunning replaces only these candidates.'
      : 'No confident onset was claimed. Earlier candidates from this analyzer were cleared.'
    activityLog.write('success', 'vocal.onset-analysis', events.length ? 'Deterministic onset evidence saved.' : 'Onset analysis completed without a candidate claim.', {
      projectId: analysisProjectId,
      assetId: asset.id,
      eventCount: events.length,
      analyzerId: onsetAnalyzerId,
    })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The saved take could not be analyzed for onsets.'
    status.value = message
    onsetAnalysisMessages[asset.id] = message
    activityLog.write('error', 'vocal.onset-analysis', message, {
      projectId: analysisProjectId,
      assetId: asset.id,
      analyzerId: onsetAnalyzerId,
    })
  } finally {
    onsetAnalysisAssetId.value = ''
    busy.value = false
  }
}
async function syncBrowserRecovery() {
  if (browserRecoverySyncBusy.value || workspaceConnection.value !== 'ready' || browserRecoveries.value.length === 0) return
  browserRecoverySyncBusy.value = true
  let synchronized = 0
  let failed = 0
  for (const snapshot of [...browserRecoveries.value]) {
    try {
      await projectsApi.saveRecovery(snapshot.project, snapshot.baseProjectLastModifiedUtc, snapshot.sessionId)
      await discardBrowserRecovery(snapshot.projectId)
      synchronized++
      activityLog.write('success', 'recovery.browser-sync', 'Browser-protected work returned to the local project service.', { projectId: snapshot.projectId })
    } catch (error) {
      failed++
      activityLog.write('warning', 'recovery.browser-sync', error instanceof Error ? error.message : 'Browser-protected work could not return to the local project service.', { projectId: snapshot.projectId })
    }
  }
  await refreshBrowserRecovery()
  browserRecoveryNeedsReview.value = failed > 0 && browserRecoveries.value.length > 0
  if (synchronized) await refreshRecovery()
  if (browserRecoveries.value.length) status.value = browserRecoveryDetail.value
  else if (synchronized) status.value = 'Browser-protected work returned to Recovery. Review it before saving.'
  browserRecoverySyncBusy.value = false
}
async function openRecovery() {
  view.value = 'recovery'
  showAllRecoveries.value = false
  await Promise.all([refreshRecovery(), refreshBrowserRecovery()])
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
    activeCreatorStage.value = recovered.project.sections.length ? 'shape' : 'idea'
    status.value = 'Recovered unsaved work. Save the song when you are ready.'
    activityLog.write('success', 'recovery.restore', 'Unsaved work restored.', { projectId: id })
    void refreshLyricTimeline()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The recovery snapshot could not be restored.'
  } finally { busy.value = false }
}
async function restoreBrowserProtectedWork(id: string) {
  if (workspaceConnection.value !== 'ready') {
    status.value = 'Reconnect the local project service before restoring browser-protected work.'
    return
  }
  busy.value = true
  try {
    const recovered = await loadBrowserRecovery(id)
    if (!recovered) throw new Error('That browser recovery snapshot is no longer available.')
    response.value = { project: recovered.project, canUndo: false, canRedo: false }
    projectId.value = recovered.projectId
    localStorage.setItem('maskilForge.projectId', recovered.projectId)
    persistedRevision.value = recovered.baseProjectLastModifiedUtc
    recoveryBlocked.value = false
    savedSnapshot.value = ''
    timelineOverlayCandidateId.value = ''
    selectedTimelineMarkerKey.value = ''
    view.value = recovered.project.sections.length ? 'structure' : 'capture'
    activeCreatorStage.value = recovered.project.sections.length ? 'shape' : 'idea'
    status.value = 'Browser-protected work restored for review. Saving will proceed only if the saved song revision still matches.'
    activityLog.write('success', 'recovery.browser-restore', 'Browser-protected work restored for review.', { projectId: id })
    void refreshLyricTimeline()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'Browser-protected work could not be restored.'
    activityLog.write('error', 'recovery.browser-restore', status.value, { projectId: id })
  } finally { busy.value = false }
}
async function requestRecoveryDiscard(summary: RecoveryQueueItem, event: Event) {
  recoveryDiscardReturnFocus = event.currentTarget as HTMLElement
  recoveryDiscardTarget.value = summary
  activityLog.write('warning', 'recovery.discard', 'Recovery discard confirmation requested.', { projectId: summary.id, title: summary.title, source: summary.sourceLabel })
  await nextTick()
  recoveryDiscardCancelButton.value?.focus()
}
async function cancelRecoveryDiscard() {
  recoveryDiscardTarget.value = null
  activityLog.write('info', 'recovery.discard', 'Recovery discard cancelled.')
  await nextTick()
  recoveryDiscardReturnFocus?.focus()
  recoveryDiscardReturnFocus = null
}
async function confirmRecoveryDiscard() {
  if (!recoveryDiscardTarget.value) return
  const target = recoveryDiscardTarget.value
  busy.value = true
  try {
    await discardRecoverySources(target)
    recoveryDiscardTarget.value = null
    recoveryDiscardReturnFocus = null
    status.value = `The protected “${target.title}” work was discarded from ${target.sourceLabel.toLowerCase()}.`
    activityLog.write('info', 'recovery.discard', 'Recovery work discarded.', { projectId: target.id, title: target.title, source: target.sourceLabel })
    await Promise.all([refreshRecovery(), refreshBrowserRecovery()])
    if (recoveryCount.value === 0) await goHome()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The protected work could not be discarded.'
  } finally { busy.value = false }
}
async function discardRecoverySources(target: RecoveryQueueItem) {
  const removals: Promise<unknown>[] = []
  if (target.hasHostSnapshot) removals.push(projectsApi.discardRecovery(target.id))
  if (target.hasBrowserSnapshot) removals.push(discardBrowserRecovery(target.id))
  await Promise.all(removals)
}
async function requestStaleRecoveryCleanup() {
  staleRecoveryCleanupOpen.value = true
  activityLog.write('warning', 'recovery.stale-cleanup', 'Stale recovery cleanup confirmation requested.', { count: staleRecoveryQueue.value.length, staleDays: recoveryStaleDays })
  await nextTick()
  staleRecoveryCleanupCancelButton.value?.focus()
}
function cancelStaleRecoveryCleanup() {
  staleRecoveryCleanupOpen.value = false
  activityLog.write('info', 'recovery.stale-cleanup', 'Stale recovery cleanup cancelled.')
}
async function confirmStaleRecoveryCleanup() {
  const targets = [...staleRecoveryQueue.value]
  if (targets.length === 0) { staleRecoveryCleanupOpen.value = false; return }
  busy.value = true
  let discarded = 0
  const failedTitles: string[] = []
  for (const target of targets) {
    try {
      await discardRecoverySources(target)
      discarded++
    } catch {
      failedTitles.push(target.title)
    }
  }
  staleRecoveryCleanupOpen.value = false
  await Promise.all([refreshRecovery(), refreshBrowserRecovery()])
  if (failedTitles.length) {
    status.value = `${discarded} stale recover${discarded === 1 ? 'y was' : 'ies were'} discarded. ${failedTitles.length} could not be removed and remain available.`
    activityLog.write('warning', 'recovery.stale-cleanup', 'Stale recovery cleanup completed with retained failures.', { discarded, failed: failedTitles.length })
  } else {
    status.value = `${discarded} stale recover${discarded === 1 ? 'y was' : 'ies were'} explicitly discarded.`
    activityLog.write('info', 'recovery.stale-cleanup', 'Stale recovery cleanup completed.', { discarded })
  }
  if (recoveryCount.value === 0) await goHome()
  busy.value = false
}
async function restoreSong(id: string, title: string) {
  busy.value = true
  try {
    await projectsApi.restore(id)
    status.value = `“${title}” was restored to your song library.`
    activityLog.write('success', 'project.restore', 'Song restored from Trash.', { projectId: id, title })
    await Promise.all([refreshTrash(), refreshLibrary()])
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
    await Promise.all([
      discardBrowserRecovery(target.id).catch(() => undefined),
      discardBrowserProject(target.id).catch(() => undefined),
    ])
    permanentDeleteTarget.value = null
    status.value = `“${target.title}” was permanently deleted.`
    activityLog.write('success', 'project.permanent-delete', 'Song permanently deleted.', { projectId: target.id, title: target.title })
    await refreshTrash()
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The song could not be permanently deleted.'
  } finally { busy.value = false }
}
function beginTrashSelection() {
  trashSelectionMode.value = true
  selectedTrashProjectIds.value = []
  status.value = 'Select Trash items to restore or review for permanent deletion. Nothing is selected automatically.'
}
function finishTrashSelection() {
  trashSelectionMode.value = false
  selectedTrashProjectIds.value = []
  status.value = 'Trash.'
}
function setTrashProjectSelected(id: string, event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  const selected = new Set(selectedTrashProjectIds.value)
  if (checked) selected.add(id)
  else selected.delete(id)
  selectedTrashProjectIds.value = [...selected]
}
function selectVisibleTrashProjects() {
  const selected = new Set(selectedTrashProjectIds.value)
  visibleTrashQueue.value.forEach(summary => selected.add(summary.id))
  selectedTrashProjectIds.value = [...selected]
}
function clearTrashSelection() { selectedTrashProjectIds.value = [] }
async function requestBulkRestore() {
  if (selectedTrashProjects.value.length === 0) return
  bulkRestoreOpen.value = true
  activityLog.write('info', 'trash.bulk-restore', 'Multi-song restore confirmation requested.', { count: selectedTrashProjects.value.length })
  await nextTick()
  bulkRestoreCancelButton.value?.focus()
}
function cancelBulkRestore() {
  bulkRestoreOpen.value = false
  activityLog.write('info', 'trash.bulk-restore', 'Multi-song restore cancelled.')
}
async function confirmBulkRestore() {
  const targets = [...selectedTrashProjects.value]
  if (targets.length === 0) { bulkRestoreOpen.value = false; return }
  busy.value = true
  let restored = 0
  const failedIds: string[] = []
  for (const target of targets) {
    try {
      await projectsApi.restore(target.id)
      restored++
    } catch {
      failedIds.push(target.id)
    }
  }
  bulkRestoreOpen.value = false
  selectedTrashProjectIds.value = failedIds
  await Promise.all([refreshTrash(), refreshLibrary()])
  if (failedIds.length) {
    status.value = `${restored} song${restored === 1 ? ' was' : 's were'} restored. ${failedIds.length} could not be restored and remain selected.`
    activityLog.write('warning', 'trash.bulk-restore', 'Multi-song restore completed with retained failures.', { restored, failed: failedIds.length })
  } else {
    status.value = `${restored} song${restored === 1 ? ' was' : 's were'} restored to your library.`
    activityLog.write('success', 'trash.bulk-restore', 'Selected songs restored from Trash.', { restored })
  }
  busy.value = false
}
async function requestBulkPermanentDelete() {
  if (selectedTrashProjects.value.length === 0) return
  bulkPermanentDeleteOpen.value = true
  activityLog.write('warning', 'trash.bulk-permanent-delete', 'Multi-song permanent deletion confirmation requested.', { count: selectedTrashProjects.value.length })
  await nextTick()
  bulkPermanentDeleteCancelButton.value?.focus()
}
function cancelBulkPermanentDelete() {
  bulkPermanentDeleteOpen.value = false
  activityLog.write('info', 'trash.bulk-permanent-delete', 'Multi-song permanent deletion cancelled.')
}
async function confirmBulkPermanentDelete() {
  const targets = [...selectedTrashProjects.value]
  if (targets.length === 0) { bulkPermanentDeleteOpen.value = false; return }
  busy.value = true
  let deleted = 0
  const failedIds: string[] = []
  for (const target of targets) {
    try {
      await projectsApi.permanentlyDelete(target.id)
      await Promise.all([
        discardBrowserRecovery(target.id).catch(() => undefined),
        discardBrowserProject(target.id).catch(() => undefined),
      ])
      deleted++
    } catch {
      failedIds.push(target.id)
    }
  }
  bulkPermanentDeleteOpen.value = false
  selectedTrashProjectIds.value = failedIds
  await Promise.all([refreshTrash(), refreshRecovery(), refreshBrowserProjects()])
  if (failedIds.length) {
    status.value = `${deleted} song${deleted === 1 ? ' was' : 's were'} permanently deleted. ${failedIds.length} could not be deleted and remain selected.`
    activityLog.write('warning', 'trash.bulk-permanent-delete', 'Multi-song permanent deletion completed with retained failures.', { deleted, failed: failedIds.length })
  } else {
    status.value = `${deleted} song${deleted === 1 ? ' was' : 's were'} permanently deleted.`
    activityLog.write('success', 'trash.bulk-permanent-delete', 'Selected Trash items permanently deleted.', { deleted })
  }
  busy.value = false
}
function beginLibraryCleanup() {
  libraryCleanupMode.value = true
  libraryQuery.value = ''
  libraryStageFilter.value = 'empty'
  showAllLibraryResults.value = false
  selectedLibraryProjectIds.value = []
  status.value = 'Review empty starts. Nothing is selected or removed automatically.'
}
function finishLibraryCleanup() {
  libraryCleanupMode.value = false
  libraryQuery.value = ''
  libraryStageFilter.value = 'all'
  selectedLibraryProjectIds.value = []
  status.value = 'Song library.'
}
function setLibraryProjectSelected(id: string, event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  const selected = new Set(selectedLibraryProjectIds.value)
  if (checked) selected.add(id)
  else selected.delete(id)
  selectedLibraryProjectIds.value = [...selected]
}
function selectVisibleEmptyStarts() {
  const selected = new Set(selectedLibraryProjectIds.value)
  visibleLibraryProjects.value
    .filter(summary => projectLibraryStage(summary) === 'empty')
    .forEach(summary => selected.add(summary.id))
  selectedLibraryProjectIds.value = [...selected]
}
function clearLibrarySelection() { selectedLibraryProjectIds.value = [] }
async function requestBulkTrash() {
  if (selectedLibraryProjects.value.length === 0) return
  bulkTrashOpen.value = true
  activityLog.write('warning', 'project.bulk-delete', 'Empty-start cleanup confirmation requested.', { count: selectedLibraryProjects.value.length })
  await nextTick()
  bulkTrashCancelButton.value?.focus()
}
function cancelBulkTrash() {
  bulkTrashOpen.value = false
  activityLog.write('info', 'project.bulk-delete', 'Empty-start cleanup cancelled.')
}
async function confirmBulkTrash() {
  const targets = [...selectedLibraryProjects.value]
  if (targets.length === 0) { bulkTrashOpen.value = false; return }
  busy.value = true
  let moved = 0
  const failedIds: string[] = []
  for (const target of targets) {
    try {
      await projectsApi.delete(target.id)
      await Promise.all([
        discardBrowserRecovery(target.id).catch(() => undefined),
        discardBrowserProject(target.id).catch(() => undefined),
      ])
      moved++
      if (project.value?.id === target.id) {
        response.value = null
        savedSnapshot.value = ''
        localStorage.removeItem('maskilForge.projectId')
      }
    } catch {
      failedIds.push(target.id)
    }
  }
  bulkTrashOpen.value = false
  selectedLibraryProjectIds.value = failedIds
  await Promise.all([refreshLibrary(), refreshRecovery(), refreshBrowserProjects()])
  if (failedIds.length) {
    status.value = `${moved} empty start${moved === 1 ? '' : 's'} moved to Trash. ${failedIds.length} could not be moved and remain selected.`
    activityLog.write('warning', 'project.bulk-delete', 'Empty-start cleanup completed with retained failures.', { moved, failed: failedIds.length })
  } else {
    status.value = `${moved} empty start${moved === 1 ? '' : 's'} moved to Trash. You can restore ${moved === 1 ? 'it' : 'them'} there.`
    activityLog.write('success', 'project.bulk-delete', 'Selected empty starts moved to Trash.', { moved })
  }
  busy.value = false
}
function requestFirstPartCommit(label: string, proceed: () => void) {
  if (project.value?.musicalParts.length) { proceed(); return }
  firstPartConfirmation.value = { label, proceed }
}
function cancelFirstPartCommit() {
  firstPartConfirmation.value = null
  status.value = 'Part creation cancelled. The song structure remains fully editable.'
}
function confirmFirstPartCommit() {
  const target = firstPartConfirmation.value
  firstPartConfirmation.value = null
  target?.proceed()
}
async function refreshLibrary() {
  libraryBusy.value = true
  try {
    projects.value = await projectsApi.list()
    const hostProjectIds = new Set(projects.value.map(summary => summary.id))
    const orphanedBrowserProjects = browserProjects.value.filter(record => !hostProjectIds.has(record.projectId))
    if (orphanedBrowserProjects.length) {
      await Promise.all(orphanedBrowserProjects.map(record => discardBrowserProject(record.projectId)))
      await refreshBrowserProjects()
      activityLog.write('info', 'delivery.offline-review', 'Browser review snapshots no longer present in the connected song library were removed.', { count: orphanedBrowserProjects.length })
    }
  }
  catch (error) { status.value = error instanceof Error ? error.message : 'Could not load the song library.' }
  finally { libraryBusy.value = false }
}
async function refreshWorkspaceHealth() {
  if (workspaceCheckBusy.value) return
  workspaceCheckBusy.value = true
  const previousConnection = workspaceConnection.value
  try {
    workspaceHealth.value = await projectsApi.health()
    activityLog.configureRemote(workspaceHealth.value.remoteActivityLoggingEnabled)
    workspaceConnection.value = 'ready'
    if (previousConnection === 'unavailable') {
      activityLog.write('success', 'delivery.workspace', 'Local project service reconnected.', {
        schemaVersion: workspaceHealth.value.schemaVersion,
        webClientHosted: workspaceHealth.value.webClientHosted,
      })
    }
    try {
      instrumentProfiles.value = await projectsApi.instrumentProfiles()
      activityLog.write('info', 'instrument-knowledge.load', 'Instrument profiles loaded.', {
        version: instrumentProfiles.value.version,
        instrumentCount: instrumentProfiles.value.instruments.length,
      })
    } catch (error) {
      instrumentProfiles.value = null
      activityLog.write('warning', 'instrument-knowledge.load', error instanceof Error ? error.message : 'Instrument profiles could not be loaded.')
    }
    try {
      instrumentArticulationMaps.value = await projectsApi.instrumentArticulationMaps()
      const mappings = instrumentArticulationMaps.value.maps.flatMap(item => item.mappings)
      activityLog.write('info', 'instrument-articulation-map.load', 'Instrument articulation maps loaded.', {
        instrumentCount: instrumentArticulationMaps.value.maps.length,
        mappedCount: mappings.filter(item => item.applicable).length,
        notApplicableCount: mappings.filter(item => !item.applicable).length,
      })
    } catch (error) {
      instrumentArticulationMaps.value = null
      activityLog.write('warning', 'instrument-articulation-map.load', error instanceof Error ? error.message : 'Instrument articulation maps could not be loaded.')
    }
    try {
      drumKitGmMap.value = await projectsApi.drumKitGmMap()
      activityLog.write('info', 'drum-kit-gm-map.load', 'Drum-kit General MIDI map loaded.', {
        piece: drumKitGmMap.value.hit.name,
        pitch: formatRegisteredPitch(drumKitGmMap.value.hit.pitch),
      })
    } catch (error) {
      drumKitGmMap.value = null
      activityLog.write('warning', 'drum-kit-gm-map.load', error instanceof Error ? error.message : 'Drum-kit General MIDI map could not be loaded.')
    }
    try {
      instrumentMidiChannels.value = await projectsApi.instrumentMidiChannels()
      activityLog.write('info', 'instrument-midi-channels.load', 'Instrument MIDI channels loaded.', {
        unassignedMidiChannel: instrumentMidiChannels.value.unassignedMidiChannel,
        assignmentCount: instrumentMidiChannels.value.assignments.length,
      })
    } catch (error) {
      instrumentMidiChannels.value = null
      activityLog.write('warning', 'instrument-midi-channels.load', error instanceof Error ? error.message : 'Instrument MIDI channels could not be loaded.')
    }
    await refreshBrowserRecovery()
    await syncBrowserRecovery()
  } catch {
    activityLog.configureRemote(false)
    workspaceHealth.value = null
    instrumentProfiles.value = null
    instrumentRangeReviews.value = null
    instrumentArticulationMaps.value = null
    drumKitGmMap.value = null
    instrumentMidiChannels.value = null
    workspaceConnection.value = 'unavailable'
    if (previousConnection !== 'unavailable') {
      activityLog.write('warning', 'delivery.workspace', 'Local project service is unavailable. Host-owned editing is paused; browser-owned lyric capture remains available.')
    }
  } finally {
    workspaceCheckBusy.value = false
  }
}
function handleConnectivityChange() { void refreshWorkspaceHealth() }
function captureInstallPrompt(event: Event) {
  event.preventDefault()
  installPrompt.value = event as InstallPromptEvent
}
async function installApplication() {
  const prompt = installPrompt.value
  if (!prompt) return
  await prompt.prompt()
  const choice = await prompt.userChoice
  activityLog.write(choice.outcome === 'accepted' ? 'success' : 'info', 'delivery.install', choice.outcome === 'accepted'
    ? 'Application installation accepted.'
    : 'Application installation dismissed.', { platform: choice.platform })
  installPrompt.value = null
}
function markApplicationInstalled() {
  applicationInstalled.value = true
  installPrompt.value = null
  activityLog.write('success', 'delivery.install', 'Maskil Forge application installed.')
}
function applyApplicationShellUpdate() {
  const registration = shellUpdateRegistration.value
  if (!registration) return
  let reloadPending = true
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (!reloadPending) return
    reloadPending = false
    window.location.reload()
  }, { once: true })
  activateApplicationShellUpdate(registration)
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
async function addSectionFromMenu(kind: SectionKind) {
  await addSection(kind)
  await nextTick()
  const details = document.getElementById('section-toolbar')
  if (details instanceof HTMLDetailsElement) details.open = false
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
async function previewPastedStructure() {
  if (!project.value?.rawLyricDraft.trim()) {
    status.value = 'Paste a lyric sheet before previewing its structure.'
    return
  }
  busy.value = true
  try {
    structurePreview.value = await projectsApi.previewStructure(project.value.rawLyricDraft)
    previewedLyricSheet.value = project.value.rawLyricDraft
    status.value = structurePreview.value.sections.length
      ? `${structurePreview.value.sections.length} sections detected. Review them before creating anything.`
      : 'No familiar section headings were detected. Keep the text as a raw draft or add bracketed headings such as [Verse 1].'
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The lyric sheet could not be analyzed.'
  } finally { busy.value = false }
}
function removeProposedSection(index: number) { structurePreview.value?.sections.splice(index, 1) }
function resolveUnrecognizedSection(index: number) {
  const preview = structurePreview.value
  const unresolved = preview?.unrecognizedSections[index]
  if (!preview || !unresolved?.resolutionKind) return
  const insertionIndex = Math.min(unresolved.insertionIndex, preview.sections.length)
  preview.sections.splice(insertionIndex, 0, {
    kind: unresolved.resolutionKind,
    title: unresolved.heading,
    delivery: unresolved.delivery,
    performanceNotes: unresolved.performanceNotes,
    lyrics: [...unresolved.lyrics],
    structuralFunction: 'Unspecified',
  })
  preview.unrecognizedSections.forEach((item, itemIndex) => {
    if (itemIndex !== index && item.insertionIndex >= insertionIndex) item.insertionIndex += 1
  })
  preview.unrecognizedSections.splice(index, 1)
  preview.unrecognizedHeadings.splice(index, 1)
  status.value = `${unresolved.heading} added to the proposal as ${label(unresolved.resolutionKind)}. Review it before creating sections.`
}
function moveProposedSection(index: number, offset: number) {
  const sections = structurePreview.value?.sections
  if (!sections) return
  const target = index + offset
  if (target < 0 || target >= sections.length) return
  const [section] = sections.splice(index, 1)
  sections.splice(target, 0, section)
}
async function acceptStructurePreview() {
  if (!project.value || !structurePreview.value?.sections.length) return
  if (project.value.rawLyricDraft !== previewedLyricSheet.value) {
    status.value = 'The lyric sheet changed after this preview. Preview it again before creating sections.'
    return
  }
  if (isDirty.value && !(await saveProject())) return
  const proposedSections = structurePreview.value.sections.map(section => ({ ...section, lyrics: [...section.lyrics] }))
  const succeeded = await run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'import-song-structure', proposedSections }),
    `${proposedSections.length} sections created. Your original lyric sheet remains preserved.`,
    'section.import',
    { sectionCount: proposedSections.length })
  if (succeeded) {
    structurePreview.value = null
    view.value = 'structure'
    activeCreatorStage.value = 'shape'
  }
}
function cancelStructurePreview() { structurePreview.value = null; previewedLyricSheet.value = ''; status.value = 'Your lyric sheet remains unchanged.' }
function duplicateSection(sectionId: string, title: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'duplicate-section', sectionId }),
    `${title} duplicated. Review its timing before building musical parts.`,
    'section.duplicate',
    { sectionId })
}
function reusableFoundationSources(targetSectionId: string) {
  if (!project.value) return []
  return project.value.sections.filter(section => section.id !== targetSectionId && (
    section.harmony.length > 0
    || project.value!.arrangement.some(item => item.sectionId === section.id)
    || project.value!.arrangementRoles.some(item => item.sectionId === section.id)))
}
function reuseSectionFoundation(targetSectionId: string) {
  if (!project.value) return
  const sourceSectionId = foundationSourceDrafts[targetSectionId]
  const source = project.value.sections.find(section => section.id === sourceSectionId)
  const target = project.value.sections.find(section => section.id === targetSectionId)
  if (!source || !target) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'reuse-section-foundation', sectionId: targetSectionId, sourceSectionId }),
    `${target.title} now starts from ${source.title}'s harmony and arrangement foundation. Lyrics and performance intent were not changed.`,
    'section.foundation.reuse',
    { sourceSectionId, targetSectionId })
}
async function setSectionIntent(sectionId: string, event: Event) {
  if (!project.value) return
  const form = event.currentTarget as HTMLFormElement
  const structuralFunction = (form.elements.namedItem('structuralFunction') as HTMLSelectElement).value as StructuralFunction
  const delivery = (form.elements.namedItem('delivery') as HTMLSelectElement).value as SectionDelivery
  const performanceNotes = (form.elements.namedItem('performanceNotes') as HTMLTextAreaElement).value
  const succeeded = await run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-section-intent', sectionId, structuralFunction, sectionDelivery: delivery, performanceNotes }),
    'Section role and performance intent saved.',
    'section.intent',
    { sectionId, structuralFunction, delivery })
  if (succeeded) delete structuralRoleDrafts[sectionId]
  if (!succeeded || !roleReviewActive.value || structuralFunction === 'Unspecified') return succeeded
  if (roleReview.value.nextSectionId) await reviewNextOpenRole()
  else {
    roleReviewActive.value = false
    status.value = 'Functional arc reviewed. Every section now has an artist-authored role.'
  }
  return succeeded
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
function writableEmptyLyric(section: { lyricLines: Array<{ id: string; text: string }> }) {
  return firstWritableEmptyLyricLine(section, project.value?.locks.filter(item => item.scope === 'LyricLine').map(item => item.lineId) ?? [])
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
function instrumentProfileName(id: string | null | undefined) {
  if (!id) return 'Not assigned'
  return instrumentProfiles.value?.instruments.find(item => item.id === id)?.name ?? id
}
function selectedInstrumentProfileId(data: FormData) {
  const value = String(data.get('instrumentProfileId') ?? '').trim()
  return value === '' ? null : value
}
function instrumentArticulationLabel(articulation: InstrumentArticulation) {
  if (articulation === 'BowExpression') return 'Bow expression'
  if (articulation === 'HammerOn') return 'Hammer-on'
  if (articulation === 'PalmMute') return 'Palm mute'
  return articulation
}
function instrumentGestureLabel(gesture: string) {
  return gesture
}
function gesturePerformanceCopy(performance: InstrumentGesturePerformance) {
  if (!performance.applicable || !performance.articulation) {
    return `${instrumentGestureLabel(performance.gesture)} does not apply.`
  }
  return `${instrumentGestureLabel(performance.gesture)} → ${instrumentArticulationLabel(performance.articulation)}`
}
function slideRangeCopy(kind: RangeCollisionKind | null) {
  if (kind === 'Below') return 'below this instrument’s range'
  if (kind === 'Above') return 'above this instrument’s range'
  return 'in range'
}
function articulationMapForInstrument(instrumentId: string) {
  return instrumentArticulationMaps.value?.maps.find(item => item.instrumentId === instrumentId) ?? null
}
function midiChannelLabel(instrumentId: string) {
  const assignment = instrumentMidiChannels.value?.assignments.find(item => item.instrumentId === instrumentId)
  return assignment ? `MIDI channel ${assignment.midiChannel}` : ''
}
function gestureMapCopy(instrumentId: string) {
  const map = articulationMapForInstrument(instrumentId)
  if (!map) return []
  const lines = map.mappings.map(mapping => mapping.applicable && mapping.articulation
    ? `${instrumentGestureLabel(mapping.gesture)} → ${instrumentArticulationLabel(mapping.articulation)}`
    : `${instrumentGestureLabel(mapping.gesture)} does not apply.`)
  if (instrumentId === 'drum-kit' && drumKitGmMap.value) {
    lines.push(`GM percussion → ${drumKitGmMap.value.hit.name} (${formatRegisteredPitch(drumKitGmMap.value.hit.pitch)})`)
  }
  return lines
}
function instrumentQualityLabel(quality: InstrumentExpressiveQuality) {
  return quality
}
function instrumentRoleLabel(role: ArrangementRole) {
  return arrangementRoles.find(item => item.id === role)?.label ?? role
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
function setChordVoicing(sectionId: string, harmonyChordId: string, chord: ChordSymbol, event: Event) {
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
  const issues = voicingIssues(chord, registeredPitches, 21, 108)
  if (issues.length) {
    status.value = `${issues.join(' ')} Chord tones: ${chordToneNames(chord).join(', ')}.`
    activityLog.write('warning', 'harmony.voicing.set', status.value, { sectionId, harmonyChordId })
    return
  }
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-chord-voicing', sectionId, harmonyChordId, registeredPitches, minimumMidiNote: 21, maximumMidiNote: 108 }),
    registeredPitches.length ? `Voicing set to ${tokens.join(' ')}.` : 'Chord voicing cleared.',
    'harmony.voicing.set',
    { sectionId, harmonyChordId, voiceCount: registeredPitches.length })
    ?.then(succeeded => { if (succeeded) delete voicingDrafts[harmonyChordId] })
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
function setNoteEvent(noteEventId: string, event: Event) {
  if (!project.value) return
  const form = event.currentTarget as HTMLFormElement
  const data = new FormData(form)
  try {
    const notePitch = parseRegisteredPitch(String(data.get('pitch') ?? ''))
    const startTick = Number(data.get('startTick'))
    const durationTicks = Number(data.get('durationTicks'))
    const velocity = Number(data.get('velocity'))
    return run(
      () => projectsApi.command(project.value!.id, project.value!, { type: 'set-note-event', noteEventId, notePitch, startTick, durationTicks, velocity }),
      `${formatRegisteredPitch(notePitch)} updated. Hear the song again to review the change.`,
      'midi.note.set',
      { noteEventId, pitch: formatRegisteredPitch(notePitch), startTick, durationTicks, velocity })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The note could not be read.'
    activityLog.write('warning', 'midi.note.set', status.value, { noteEventId })
  }
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
async function exportPortableProject() {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'project.portable-export', 'Portable project export requested.', { projectId: project.value.id })
  try {
    const blob = await projectsApi.exportPortableProject(project.value.id, project.value)
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    const hasAssets = project.value.assets.length > 0
    link.href = url
    link.download = portableExportFileName(project.value.title, hasAssets)
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    status.value = hasAssets
      ? 'Asset-owning project package exported. Original vocal bytes travel with the Song Graph and stay verified by length and SHA-256.'
      : 'Portable project exported. This versioned Song Graph can be stored or moved without an account.'
    activityLog.write('success', 'project.portable-export', 'Portable project exported.', { projectId: project.value.id, fileName: link.download })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'Unable to export the portable project. No project data was changed.'
    activityLog.write('error', 'project.portable-export', status.value, { projectId: project.value.id })
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
function stopPartAudition(message = '') {
  partAudition.stop()
  const stoppedSectionId = partAuditionState.sectionId
  partAuditionState.sectionId = ''
  partAuditionState.messageSectionId = message ? stoppedSectionId : ''
  partAuditionState.message = message
}
function stopTransport(message = '') {
  playbackTransport.stop()
  transportState.playing = false
  transportState.positionLabel = 'Bar 1 · Beat 1'
  transportState.noteCount = 0
  transportState.message = message
}
function updateTransportPosition(seconds: number) {
  if (!project.value) return
  const tempo = project.value.timeline.tempoMap.events[0].beatsPerMinute
  const meter = project.value.timeline.timeSignatureMap.events[0]
  const tick = tickFromSeconds(seconds, {
    beatsPerMinute: tempo,
    ticksPerQuarterNote: project.value.timeline.ticksPerQuarterNote,
  })
  transportState.positionLabel = formatTransportPosition(musicalPositionFromTicks(tick, {
    beatsPerBar: meter.numerator,
    beatUnit: meter.denominator,
    ticksPerQuarterNote: project.value.timeline.ticksPerQuarterNote,
  }))
}
async function hearProgression(sectionId: string) {
  if (!project.value) return
  const section = project.value.sections.find(item => item.id === sectionId)
  if (!section?.harmony.length) return
  stopPartAudition()
  stopTransport()
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
async function hearAssembledParts(sectionId: string) {
  if (!project.value) return
  const parts = partsForSection(sectionId)
  if (!parts.length) return
  stopChordAudition()
  stopTransport()
  const tempo = project.value.timeline.tempoMap.events[0].beatsPerMinute
  const notes = assemblePartNotes(project.value.musicalParts, project.value.noteEvents, sectionId)
  partAuditionState.sectionId = sectionId
  partAuditionState.messageSectionId = sectionId
  partAuditionState.message = 'Preparing your assembled parts…'
  try {
    const scheduled = scheduleAssembledNotes(notes, {
      beatsPerMinute: tempo,
      ticksPerQuarterNote: project.value.timeline.ticksPerQuarterNote,
    })
    const result = await partAudition.play(scheduled, () => stopPartAudition('Assembled-part preview finished.'))
    partAuditionState.message = `Playing ${result.noteCount} assembled note${result.noteCount === 1 ? '' : 's'} at ${tempo} BPM.`
    activityLog.write('success', 'arrangement.parts.hear', partAuditionState.message, { sectionId, noteCount: result.noteCount, partCount: parts.length })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'The assembled parts could not be played.'
    stopPartAudition(message)
    activityLog.write('error', 'arrangement.parts.hear', message, { sectionId })
  }
}
async function startTransport() {
  if (!project.value) return
  const parts = project.value.musicalParts
  if (!parts.length) return
  stopChordAudition()
  stopPartAudition()
  const tempo = project.value.timeline.tempoMap.events[0].beatsPerMinute
  const notes = assemblePartNotes(parts, project.value.noteEvents)
  transportState.playing = true
  transportState.message = 'Preparing song playback…'
  transportState.positionLabel = 'Bar 1 · Beat 1'
  try {
    const scheduled = scheduleAbsoluteNotes(notes, {
      beatsPerMinute: tempo,
      ticksPerQuarterNote: project.value.timeline.ticksPerQuarterNote,
    })
    const result = await playbackTransport.play(
      scheduled,
      seconds => updateTransportPosition(seconds),
      () => {
        transportState.playing = false
        transportState.message = 'Playback finished.'
        activityLog.write('success', 'transport.play', 'Song playback finished.', { noteCount: result.noteCount })
      })
    transportState.noteCount = result.noteCount
    transportState.message = `Playing ${result.noteCount} assembled note${result.noteCount === 1 ? '' : 's'} across the song at ${tempo} BPM.`
    activityLog.write('success', 'transport.play', transportState.message, { noteCount: result.noteCount, partCount: parts.length })
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Song playback could not start.'
    stopTransport(message)
    activityLog.write('error', 'transport.play', message)
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
async function preparePitchGestureNoteSketch(assetId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'midi.pitch-gesture.prepare', 'Pitch-gesture note sketch requested.', { assetId })
  try {
    const sketch = await projectsApi.pitchGestureNoteSketch(project.value.id, project.value, assetId)
    pitchGestureNoteSketches[assetId] = sketch
    status.value = `${sketch.events.length} playable notes prepared from pitch gestures.`
    activityLog.write('success', 'midi.pitch-gesture.prepare', status.value, {
      assetId, noteCount: sketch.events.length,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The pitch-gesture note sketch could not be prepared.'
    activityLog.write('error', 'midi.pitch-gesture.prepare', status.value, { assetId })
  } finally {
    busy.value = false
  }
}
function usePitchGestureNoteSketch(assetId: string) {
  if (!project.value) return
  const noteCount = pitchGestureNoteSketches[assetId]?.events.length ?? 0
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-pitch-gesture-note-sketch', assetId }),
    `${noteCount} playable notes added from this pitch-gesture sketch.`,
    'midi.pitch-gesture.use',
    { assetId, noteCount })
}
async function prepareOnsetGestureNoteSketch(assetId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'midi.onset-gesture.prepare', 'Onset-gesture note sketch requested.', { assetId })
  try {
    const sketch = await projectsApi.onsetGestureNoteSketch(project.value.id, project.value, assetId)
    onsetGestureNoteSketches[assetId] = sketch
    status.value = `${sketch.events.length} playable notes prepared from onset gestures.`
    activityLog.write('success', 'midi.onset-gesture.prepare', status.value, {
      assetId, noteCount: sketch.events.length,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The onset-gesture note sketch could not be prepared.'
    activityLog.write('error', 'midi.onset-gesture.prepare', status.value, { assetId })
  } finally {
    busy.value = false
  }
}
function useOnsetGestureNoteSketch(assetId: string) {
  if (!project.value) return
  const noteCount = onsetGestureNoteSketches[assetId]?.events.length ?? 0
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-onset-gesture-note-sketch', assetId }),
    `${noteCount} playable notes added from this onset-gesture sketch.`,
    'midi.onset-gesture.use',
    { assetId, noteCount })
}
async function prepareLoudnessGestureNoteSketch(assetId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'midi.loudness-gesture.prepare', 'Loudness-gesture note sketch requested.', { assetId })
  try {
    const sketch = await projectsApi.loudnessGestureNoteSketch(project.value.id, project.value, assetId)
    loudnessGestureNoteSketches[assetId] = sketch
    status.value = `${sketch.events.length} playable notes prepared from loudness gestures.`
    activityLog.write('success', 'midi.loudness-gesture.prepare', status.value, {
      assetId, noteCount: sketch.events.length,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The loudness-gesture note sketch could not be prepared.'
    activityLog.write('error', 'midi.loudness-gesture.prepare', status.value, { assetId })
  } finally {
    busy.value = false
  }
}
function useLoudnessGestureNoteSketch(assetId: string) {
  if (!project.value) return
  const noteCount = loudnessGestureNoteSketches[assetId]?.events.length ?? 0
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-loudness-gesture-note-sketch', assetId }),
    `${noteCount} playable notes added from this loudness-gesture sketch.`,
    'midi.loudness-gesture.use',
    { assetId, noteCount })
}
async function prepareLoudnessGestureExpressionSketch(assetId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'midi.loudness-expression.prepare', 'Loudness-gesture expression sketch requested.', { assetId })
  try {
    const sketch = await projectsApi.loudnessGestureExpressionSketch(project.value.id, project.value, assetId)
    loudnessGestureExpressionSketches[assetId] = sketch
    status.value = `${sketch.points.length} dynamics points prepared from loudness gestures.`
    activityLog.write('success', 'midi.loudness-expression.prepare', status.value, {
      assetId, pointCount: sketch.points.length,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The loudness-gesture expression sketch could not be prepared.'
    activityLog.write('error', 'midi.loudness-expression.prepare', status.value, { assetId })
  } finally {
    busy.value = false
  }
}
function useLoudnessGestureExpressionSketch(assetId: string) {
  if (!project.value) return
  const pointCount = loudnessGestureExpressionSketches[assetId]?.points.length ?? 0
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-loudness-gesture-expression-sketch', assetId }),
    `${pointCount} dynamics points added from this loudness-gesture sketch.`,
    'midi.loudness-expression.use',
    { assetId, pointCount })
}
async function prepareInstrumentPerformanceSketch(assetId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'midi.instrument-retarget.prepare', 'Catalog instrument retarget requested.', { assetId })
  try {
    const sketch = await projectsApi.instrumentPerformanceSketch(project.value.id, project.value, assetId)
    instrumentPerformanceSketches[assetId] = sketch
    const outOfRangeCount = sketch.targets.reduce(
      (sum, target) => sum + target.slide.events.filter(item => item.rangeKind).length,
      0)
    status.value = `Catalog instrument retarget prepared from this take.`
    activityLog.write('success', 'midi.instrument-retarget.prepare', status.value, {
      assetId,
      targetCount: sketch.targets.length,
      outOfRangeCount,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The instrument retarget could not be prepared.'
    activityLog.write('error', 'midi.instrument-retarget.prepare', status.value, { assetId })
  } finally {
    busy.value = false
  }
}
function matchingInstrumentParts(instrumentId: string) {
  return (project.value?.musicalParts ?? []).filter(part => part.instrumentProfileId === instrumentId)
}
function instrumentSketchPartKey(assetId: string, instrumentId: string) {
  return `${assetId}:${instrumentId}`
}
function selectedInstrumentSketchPartId(assetId: string, instrumentId: string) {
  const parts = matchingInstrumentParts(instrumentId)
  if (parts.length === 1) return parts[0].id
  const chosen = instrumentSketchPartIds[instrumentSketchPartKey(assetId, instrumentId)]
  return parts.some(part => part.id === chosen) ? chosen : ''
}
function instrumentSketchHasPersistableEvents(target: InstrumentPerformanceRetargetSet['targets'][number]) {
  return target.swell.events.length > 0
    || target.slide.events.some(item => !item.rangeKind)
    || target.hit.events.length > 0
}
function instrumentSketchAcceptCopy(target: InstrumentPerformanceRetargetSet['targets'][number]) {
  if (!target.swell.applicable && !target.slide.applicable && !target.hit.applicable) {
    return `${target.instrumentName} does not apply swell, slide, or hit. Nothing is stored.`
  }
  if (!instrumentSketchHasPersistableEvents(target)) {
    return `${target.instrumentName} has no in-range slides, swells, or hits to store.`
  }
  const parts: string[] = []
  if (target.slide.applicable) parts.push('adds in-range slides to the named part')
  if (target.swell.applicable) parts.push('stores swells as a dynamics curve')
  if (target.hit.applicable) parts.push('adds hits to the named part')
  const lead = parts.join(' and ')
  const sentence = `${lead.charAt(0).toUpperCase()}${lead.slice(1)}.`
  if (target.slide.applicable) {
    return `${sentence} Out-of-range slides are skipped, not moved. MIDI does not choose an instrument.`
  }
  return `${sentence} MIDI does not choose an instrument.`
}
function useInstrumentPerformanceSketch(assetId: string, instrumentId: string) {
  if (!project.value) return
  const musicalPartId = selectedInstrumentSketchPartId(assetId, instrumentId)
  if (!musicalPartId) return
  const sketch = instrumentPerformanceSketches[assetId]
  const target = sketch?.targets.find(item => item.instrumentId === instrumentId)
  const inRangeCount = target?.slide.events.filter(item => !item.rangeKind).length ?? 0
  const swellCount = target?.swell.events.length ?? 0
  const hitCount = target?.hit.events.length ?? 0
  const partLabel = project.value.musicalParts.find(part => part.id === musicalPartId)?.label ?? 'the named part'
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'use-instrument-performance-sketch',
      assetId,
      instrumentProfileId: instrumentId,
      musicalPartId,
    }),
    `${target?.instrumentName ?? 'Instrument'} sketch stored on ${partLabel}.`,
    'midi.instrument-retarget.use',
    { assetId, instrumentId, musicalPartId, inRangeCount, swellCount, hitCount })
}
function removeExpressionCurve(expressionCurveId: string, name: string) {
  if (!project.value) return
  const pointCount = project.value.expressionCurves?.find(item => item.id === expressionCurveId)?.points.length ?? 0
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'remove-expression-curve', expressionCurveId }),
    `${name} removed from the song.`,
    'midi.expression-curve.remove',
    { expressionCurveId, pointCount })
}
function vocalTakePlacement(assetId: string) {
  return project.value?.vocalTakePlacements?.find(item => item.assetId === assetId) ?? null
}
function vocalTakePlacementLabel(assetId: string) {
  const placement = vocalTakePlacement(assetId)
  if (!placement) return 'Unplaced · song tick 0'
  return `Bar ${placement.start.bar} · beat ${placement.start.beat} · tick ${placement.start.tick}`
}
function setVocalTakePlacement(assetId: string, event: Event) {
  if (!project.value) return
  const form = event.target as HTMLFormElement
  const bar = Number((form.elements.namedItem('bar') as HTMLInputElement).value)
  const beat = Number((form.elements.namedItem('beat') as HTMLInputElement).value)
  const tick = Number((form.elements.namedItem('tick') as HTMLInputElement).value)
  return run(
    () => projectsApi.command(project.value!.id, project.value!, {
      type: 'set-vocal-take-placement',
      assetId,
      start: { bar, beat, tick },
    }),
    `Take placed at bar ${bar}.`,
    'vocal-take.place',
    { assetId, bar, beat, tick },
  ).then(async succeeded => {
    if (!succeeded) return
    if (pitchGestureNoteSketches[assetId]) await preparePitchGestureNoteSketch(assetId)
    if (onsetGestureNoteSketches[assetId]) await prepareOnsetGestureNoteSketch(assetId)
    if (loudnessGestureNoteSketches[assetId]) await prepareLoudnessGestureNoteSketch(assetId)
    if (loudnessGestureExpressionSketches[assetId]) await prepareLoudnessGestureExpressionSketch(assetId)
    if (instrumentPerformanceSketches[assetId]) return prepareInstrumentPerformanceSketch(assetId)
  })
}
function clearVocalTakePlacement(assetId: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'clear-vocal-take-placement', assetId }),
    'Take placement cleared. Sketch timing returns to song tick 0.',
    'vocal-take.place.clear',
    { assetId },
  ).then(async succeeded => {
    if (!succeeded) return
    if (pitchGestureNoteSketches[assetId]) await preparePitchGestureNoteSketch(assetId)
    if (onsetGestureNoteSketches[assetId]) await prepareOnsetGestureNoteSketch(assetId)
    if (loudnessGestureNoteSketches[assetId]) await prepareLoudnessGestureNoteSketch(assetId)
    if (loudnessGestureExpressionSketches[assetId]) await prepareLoudnessGestureExpressionSketch(assetId)
    if (instrumentPerformanceSketches[assetId]) return prepareInstrumentPerformanceSketch(assetId)
  })
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
function assignedRolesForSection(sectionId: string) {
  return arrangementRoles.filter(role => sectionHasRole(sectionId, role.id))
}
const assignedArrangementRoleIds = computed(() => {
  if (!project.value) return [] as ArrangementRole[]
  const seen = new Set<ArrangementRole>()
  const ordered: ArrangementRole[] = []
  for (const section of project.value.sections) {
    for (const role of assignedRolesForSection(section.id)) {
      if (seen.has(role.id)) continue
      seen.add(role.id)
      ordered.push(role.id)
    }
  }
  return ordered
})
function recommendedInstrumentsForRole(role: ArrangementRole): InstrumentProfile[] {
  return instrumentRecommendations.value?.recommendations.find(item => item.role === role)?.instruments ?? []
}
async function refreshInstrumentRecommendations() {
  const token = ++instrumentRecommendationToken
  if (workspaceConnection.value !== 'ready' || assignedArrangementRoleIds.value.length === 0) {
    if (token === instrumentRecommendationToken) instrumentRecommendations.value = null
    return
  }
  try {
    const quality = instrumentQualityFilter.value || null
    const set = await projectsApi.recommendInstruments(assignedArrangementRoleIds.value, quality)
    if (token !== instrumentRecommendationToken) return
    instrumentRecommendations.value = set
    activityLog.write('info', 'instrument-recommendation.prepare', 'Instrument recommendations prepared.', {
      roles: assignedArrangementRoleIds.value.join(','),
      quality,
      matchCount: set.recommendations.reduce((sum, item) => sum + item.instruments.length, 0),
    })
  } catch (error) {
    if (token !== instrumentRecommendationToken) return
    instrumentRecommendations.value = null
    activityLog.write('warning', 'instrument-recommendation.prepare', error instanceof Error ? error.message : 'Instrument recommendations could not be prepared.')
  }
}
async function refreshInstrumentRangeReviews() {
  const token = ++instrumentRangeReviewToken
  const notes = project.value?.noteEvents ?? []
  if (workspaceConnection.value !== 'ready' || notes.length === 0) {
    if (token === instrumentRangeReviewToken) instrumentRangeReviews.value = null
    return
  }
  try {
    const set = await projectsApi.reviewInstrumentRanges(notes.map(note => ({ id: note.id, pitch: note.pitch })))
    if (token !== instrumentRangeReviewToken) return
    instrumentRangeReviews.value = set
    activityLog.write('info', 'instrument-range-review.prepare', 'Instrument range review prepared.', {
      noteCount: notes.length,
      outOfRangeCount: set.reviews.reduce((sum, item) => sum + item.outOfRange.length, 0),
    })
  } catch (error) {
    if (token !== instrumentRangeReviewToken) return
    instrumentRangeReviews.value = null
    activityLog.write('warning', 'instrument-range-review.prepare', error instanceof Error ? error.message : 'Instrument range review could not be prepared.')
  }
}
function rangeFitForInstrument(sectionId: string, instrument: InstrumentProfile) {
  const notes = notesForSection(sectionId)
  if (notes.length === 0) return 'Add notes to check range.'
  const review = instrumentRangeReviews.value?.reviews.find(item => item.instrumentId === instrument.id)
  if (!review) return ''
  if (!review.applicable) return 'Unpitched. Range does not apply.'
  const sectionNoteIds = new Set(notes.map(note => note.id))
  const collisions = review.outOfRange.filter(item => sectionNoteIds.has(item.noteEventId))
  if (collisions.length === 0) return 'All section notes fit this range.'
  return collisions
    .map(item => `${formatRegisteredPitch(item.pitch)} ${item.kind === 'Below' ? 'below' : 'above'}`)
    .join(', ')
}
function notesForSection(sectionId: string) {
  if (!project.value) return []
  const placement = placementFor(sectionId)
  if (!placement) return []
  const meter = project.value.timeline.timeSignatureMap.events[0]
  const ticksPerBeat = project.value.timeline.ticksPerQuarterNote * 4 / meter.denominator
  const ticksPerBar = meter.numerator * ticksPerBeat
  const startTick = (placement.start.bar - 1) * ticksPerBar
  const endTick = startTick + placement.durationBars * ticksPerBar
  return project.value.noteEvents.filter(note => note.startTick >= startTick && note.startTick < endTick)
}
function partsForSection(sectionId: string) {
  return project.value?.musicalParts.filter(part => part.sectionId === sectionId) ?? []
}
function hasPartForRole(sectionId: string, role: ArrangementRole) {
  return partsForSection(sectionId).some(part => part.role === role)
}
async function prepareLowEndSupportProposal(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'arrangement.low_end.prepare', 'Low-end support idea requested.', { sectionId })
  try {
    const proposal = await projectsApi.lowEndSupportProposal(project.value.id, project.value, sectionId)
    lowEndSupportProposals[sectionId] = proposal
    status.value = `${proposal.events.length} low-end note${proposal.events.length === 1 ? '' : 's'} prepared for review.`
    activityLog.write('success', 'arrangement.low_end.prepare', status.value, { sectionId, noteCount: proposal.events.length, reusedNoteCount: proposal.reusedNoteCount })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The low-end support idea could not be prepared.'
    activityLog.write('error', 'arrangement.low_end.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function useLowEndSupportProposal(sectionId: string) {
  if (!project.value) return
  const proposal = lowEndSupportProposals[sectionId]
  return requestFirstPartCommit(proposal?.partLabel ?? 'Low-end support', () => void run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-low-end-support-proposal', sectionId }),
    `${proposal?.partLabel ?? 'Low-end support'} added as an editable musical part.`,
    'arrangement.low_end.use',
    { sectionId, noteCount: proposal?.events.length ?? 0 }))
}
async function preparePulseProposal(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'arrangement.pulse.prepare', 'Pulse idea requested.', { sectionId })
  try {
    const proposal = await projectsApi.pulseProposal(project.value.id, project.value, sectionId)
    pulseProposals[sectionId] = proposal
    status.value = `${proposal.events.length} pulse note${proposal.events.length === 1 ? '' : 's'} prepared for review.`
    activityLog.write('success', 'arrangement.pulse.prepare', status.value, { sectionId, noteCount: proposal.events.length, reusedNoteCount: proposal.reusedNoteCount })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The pulse idea could not be prepared.'
    activityLog.write('error', 'arrangement.pulse.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function usePulseProposal(sectionId: string) {
  if (!project.value) return
  const proposal = pulseProposals[sectionId]
  return requestFirstPartCommit(proposal?.partLabel ?? 'Pulse', () => void run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-pulse-proposal', sectionId }),
    `${proposal?.partLabel ?? 'Pulse'} added as an editable musical part.`,
    'arrangement.pulse.use',
    { sectionId, noteCount: proposal?.events.length ?? 0 }))
}
async function prepareHarmonySupportProposal(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'arrangement.harmony_support.prepare', 'Harmony support idea requested.', { sectionId })
  try {
    const proposal = await projectsApi.harmonySupportProposal(project.value.id, project.value, sectionId)
    harmonySupportProposals[sectionId] = proposal
    status.value = `${proposal.events.length} harmony note${proposal.events.length === 1 ? '' : 's'} prepared for review.`
    activityLog.write('success', 'arrangement.harmony_support.prepare', status.value, {
      sectionId,
      noteCount: proposal.events.length,
      reusedNoteCount: proposal.reusedNoteCount,
      usesPreviewVoicings: proposal.usesPreviewVoicings,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The harmony support idea could not be prepared.'
    activityLog.write('error', 'arrangement.harmony_support.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function useHarmonySupportProposal(sectionId: string) {
  if (!project.value) return
  const proposal = harmonySupportProposals[sectionId]
  return requestFirstPartCommit(proposal?.partLabel ?? 'Harmony support', () => void run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-harmony-support-proposal', sectionId }),
    `${proposal?.partLabel ?? 'Harmony support'} added as an editable musical part.`,
    'arrangement.harmony_support.use',
    { sectionId, noteCount: proposal?.events.length ?? 0 }))
}
async function prepareTextureProposal(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'arrangement.texture.prepare', 'Texture idea requested.', { sectionId })
  try {
    const proposal = await projectsApi.textureProposal(project.value.id, project.value, sectionId)
    textureProposals[sectionId] = proposal
    status.value = `${proposal.events.length} texture note${proposal.events.length === 1 ? '' : 's'} prepared for review.`
    activityLog.write('success', 'arrangement.texture.prepare', status.value, {
      sectionId,
      noteCount: proposal.events.length,
      reusedNoteCount: proposal.reusedNoteCount,
      usesPreviewVoicings: proposal.usesPreviewVoicings,
    })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The texture idea could not be prepared.'
    activityLog.write('error', 'arrangement.texture.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function useTextureProposal(sectionId: string) {
  if (!project.value) return
  const proposal = textureProposals[sectionId]
  return requestFirstPartCommit(proposal?.partLabel ?? 'Texture', () => void run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-texture-proposal', sectionId }),
    `${proposal?.partLabel ?? 'Texture'} added as an editable musical part.`,
    'arrangement.texture.use',
    { sectionId, noteCount: proposal?.events.length ?? 0 }))
}
async function prepareHookReinforcementProposal(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'arrangement.hook.prepare', 'Hook reinforcement idea requested.', { sectionId })
  try {
    const proposal = await projectsApi.hookReinforcementProposal(project.value.id, project.value, sectionId)
    hookReinforcementProposals[sectionId] = proposal
    status.value = `${proposal.events.length} hook note${proposal.events.length === 1 ? '' : 's'} prepared for review.`
    activityLog.write('success', 'arrangement.hook.prepare', status.value, { sectionId, noteCount: proposal.events.length, reusedNoteCount: proposal.reusedNoteCount })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The hook reinforcement idea could not be prepared.'
    activityLog.write('error', 'arrangement.hook.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function useHookReinforcementProposal(sectionId: string) {
  if (!project.value) return
  const proposal = hookReinforcementProposals[sectionId]
  return requestFirstPartCommit(proposal?.partLabel ?? 'Hook reinforcement', () => void run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-hook-reinforcement-proposal', sectionId }),
    `${proposal?.partLabel ?? 'Hook reinforcement'} added as an editable musical part.`,
    'arrangement.hook.use',
    { sectionId, noteCount: proposal?.events.length ?? 0 }))
}
async function prepareCountermelodyProposal(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'arrangement.countermelody.prepare', 'Countermelody idea requested.', { sectionId })
  try {
    const proposal = await projectsApi.countermelodyProposal(project.value.id, project.value, sectionId)
    countermelodyProposals[sectionId] = proposal
    status.value = `${proposal.events.length} countermelody note${proposal.events.length === 1 ? '' : 's'} prepared for review.`
    activityLog.write('success', 'arrangement.countermelody.prepare', status.value, { sectionId, noteCount: proposal.events.length, reusedNoteCount: proposal.reusedNoteCount })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The countermelody idea could not be prepared.'
    activityLog.write('error', 'arrangement.countermelody.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function useCountermelodyProposal(sectionId: string) {
  if (!project.value) return
  const proposal = countermelodyProposals[sectionId]
  return requestFirstPartCommit(proposal?.partLabel ?? 'Countermelody', () => void run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-countermelody-proposal', sectionId }),
    `${proposal?.partLabel ?? 'Countermelody'} added as an editable musical part.`,
    'arrangement.countermelody.use',
    { sectionId, noteCount: proposal?.events.length ?? 0 }))
}
async function prepareAccentProposal(sectionId: string) {
  if (!project.value) return
  busy.value = true
  activityLog.write('info', 'arrangement.accent.prepare', 'Accent idea requested.', { sectionId })
  try {
    const proposal = await projectsApi.accentProposal(project.value.id, project.value, sectionId)
    accentProposals[sectionId] = proposal
    status.value = `${proposal.events.length} accent note${proposal.events.length === 1 ? '' : 's'} prepared for review.`
    activityLog.write('success', 'arrangement.accent.prepare', status.value, { sectionId, noteCount: proposal.events.length, reusedNoteCount: proposal.reusedNoteCount })
  } catch (error) {
    status.value = error instanceof Error ? error.message : 'The accent idea could not be prepared.'
    activityLog.write('error', 'arrangement.accent.prepare', status.value, { sectionId })
  } finally {
    busy.value = false
  }
}
function useAccentProposal(sectionId: string) {
  if (!project.value) return
  const proposal = accentProposals[sectionId]
  return requestFirstPartCommit(proposal?.partLabel ?? 'Accents', () => void run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'use-accent-proposal', sectionId }),
    `${proposal?.partLabel ?? 'Accents'} added as an editable musical part.`,
    'arrangement.accent.use',
    { sectionId, noteCount: proposal?.events.length ?? 0 }))
}
function addMusicalPart(sectionId: string, event: Event) {
  if (!project.value) return
  const form = event.currentTarget as HTMLFormElement
  const data = new FormData(form)
  const arrangementRole = String(data.get('role')) as ArrangementRole
  const partLabel = String(data.get('label') ?? '').trim()
  const noteEventIds = data.getAll('noteEventIds').map(String)
  const instrumentProfileId = selectedInstrumentProfileId(data)
  return requestFirstPartCommit(partLabel, () => void run(
      () => projectsApi.command(project.value!.id, project.value!, { type: 'add-musical-part', sectionId, arrangementRole, partLabel, noteEventIds, instrumentProfileId }),
      `${partLabel} now connects ${noteEventIds.length} approved note${noteEventIds.length === 1 ? '' : 's'} to the ${arrangementRoles.find(item => item.id === arrangementRole)?.label ?? arrangementRole} role${instrumentProfileId ? ` on ${instrumentProfileName(instrumentProfileId)}` : ''}.`,
      'arrangement.part.add',
      { sectionId, arrangementRole, noteCount: noteEventIds.length, instrumentProfileId })
    .then(succeeded => { if (succeeded) form.reset() }))
}
function removeMusicalPart(musicalPartId: string, label: string) {
  if (!project.value) return
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'remove-musical-part', musicalPartId }),
    `${label} removed. Its playable notes remain in the song.`,
    'arrangement.part.remove',
    { musicalPartId })
}
function setMusicalPart(musicalPartId: string, event: Event) {
  if (!project.value) return
  const form = event.currentTarget as HTMLFormElement
  const data = new FormData(form)
  const partLabel = String(data.get('label') ?? '').trim()
  const noteEventIds = data.getAll('noteEventIds').map(String)
  const instrumentProfileId = selectedInstrumentProfileId(data)
  return run(
    () => projectsApi.command(project.value!.id, project.value!, { type: 'set-musical-part', musicalPartId, partLabel, noteEventIds, instrumentProfileId }),
    `${partLabel} updated${instrumentProfileId ? ` on ${instrumentProfileName(instrumentProfileId)}` : ''}. Hear the section or song again to review the change.`,
    'arrangement.part.set',
    { musicalPartId, noteCount: noteEventIds.length, instrumentProfileId })
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
function deliveryLabel(delivery: SectionDelivery) { return delivery === 'TalkSung' ? 'Talk-sung' : delivery }
function structuralFunctionLabel(structuralFunction: StructuralFunction) {
  return structuralRole(structuralFunction).label
}
type CreatorStage = CreatorJourneyStage
const activeCreatorStage = ref<CreatorStage>('idea')
const phoneCaptureMode = ref(false)
const phoneChrome = phoneEditorChrome()
const showWorkspaceConnectionBanner = computed(() => {
  if (workspaceConnection.value !== 'ready') return true
  if (installPrompt.value || shellUpdateRegistration.value || browserRecoveryDetail.value) return true
  if (!phoneCaptureMode.value || phoneChrome.showReadyHostStatus) return true
  return view.value !== 'capture' && view.value !== 'structure'
})
let phoneLayoutQuery: MediaQueryList | null = null
const focusedSectionId = ref('')
const sectionViewMode = ref<'all' | 'focused'>('all')
const creatorCompletion = computed(() => creatorProgress(project.value))
const phoneCompletion = computed(() => phoneJourneyProgress(project.value))
const visibleCreatorStages = computed(() => phoneCaptureMode.value ? phoneCreatorStages : creatorStages)
const currentStructurePreview = computed(() => matchingLyricSheetPreview(project.value?.rawLyricDraft ?? '', previewedLyricSheet.value, structurePreview.value))
const editableDemoReview = computed(() => demoReadiness(project.value, currentStructurePreview.value))
const phoneCaptureReview = computed(() => phoneCaptureReadiness(project.value, currentStructurePreview.value, {
  isDirty: isDirty.value,
  activeStage: activeCreatorStage.value,
  lockedLineIds: project.value?.locks.filter(lock => lock.scope === 'LyricLine').map(lock => lock.lineId) ?? [],
}))
const songOutlineItems = computed(() => songOutline(project.value, phoneCaptureMode.value ? phoneCaptureReview.value : editableDemoReview.value))
const roleReview = computed(() => structuralRoleReview(project.value))
const roleReviewActive = ref(false)
async function focusSongSection(sectionId: string) {
  focusedSectionId.value = sectionId
  await nextTick()
  const target = document.getElementById(`section-${sectionId}`)
  if (!target) return
  target.classList.remove('section-focus')
  void target.getBoundingClientRect()
  target.classList.add('section-focus')
  window.setTimeout(() => target.classList.remove('section-focus'), 1_600)
  target.scrollIntoView({ behavior: 'smooth', block: 'start' })
  target.querySelector<HTMLInputElement>('.section-identity input')?.focus({ preventScroll: true })
}
async function reviewNextOpenRole() {
  if (!roleReview.value.nextSectionId) return
  roleReviewActive.value = true
  sectionViewMode.value = 'focused'
  await focusSongSection(roleReview.value.nextSectionId)
  await nextTick()
  document.querySelector<HTMLSelectElement>(`#section-${roleReview.value.nextSectionId} select[name="structuralFunction"]`)?.focus({ preventScroll: true })
  status.value = `Choose ${roleReview.value.nextSectionTitle}'s role in the song, or leave it open.`
}
function showFocusedSection() {
  if (!focusedSectionId.value && project.value?.sections.length) focusedSectionId.value = project.value.sections[0].id
  if (!focusedSectionId.value) return
  sectionViewMode.value = 'focused'
  void focusSongSection(focusedSectionId.value)
}
function showAllSections() {
  roleReviewActive.value = false
  sectionViewMode.value = 'all'
  if (focusedSectionId.value) void focusSongSection(focusedSectionId.value)
}
function navigateFocusedSection(offset: number) {
  if (!project.value) return
  const targetId = adjacentSectionId(project.value.sections, focusedSectionId.value, offset as -1 | 1)
  if (targetId) void focusSongSection(targetId)
}
function focusedSectionIndex() { return project.value?.sections.findIndex(section => section.id === focusedSectionId.value) ?? -1 }
function creatorStageState(stage: CreatorStage) {
  if (phoneCaptureMode.value) return phoneCompletion.value[stage as keyof typeof phoneCompletion.value] ? 'complete' : 'upcoming'
  if (stage === 'review' || stage === 'approve') return 'upcoming'
  return creatorCompletion.value[stage] ? 'complete' : 'upcoming'
}
function syncPhoneCaptureMode() {
  const next = Boolean(phoneLayoutQuery?.matches)
  if (phoneCaptureMode.value === next) return
  phoneCaptureMode.value = next
  activeCreatorStage.value = next
    ? remapDesktopStageForPhone(activeCreatorStage.value)
    : remapPhoneStageForDesktop(activeCreatorStage.value)
}
function handlePhoneLayoutChange() { syncPhoneCaptureMode() }
function journeyProgressLabel(stage: CreatorStage) {
  if (stage === 'idea') return 'Song started'
  if (stage === 'words') return 'Lyrics started'
  if (stage === 'shape') return 'Structure'
  if (stage === 'review') return 'Reviewed form'
  if (stage === 'approve') return 'Capture saved'
  if (stage === 'music') return 'Music exploration'
  return stage === 'harmony' ? 'Harmony' : 'Arrangement'
}
async function goToNextReadinessStep() {
  const step = phoneCaptureMode.value ? phoneCaptureReview.value.nextStep : editableDemoReview.value.nextStep
  if (!step) return
  if (step.action === 'words' || step.action === 'review' || step.action === 'approve') {
    return goToCreatorStage(step.stage)
  }
  activeCreatorStage.value = step.stage as CreatorStage
  if (step.action === 'preview' || step.action === 'resolve') {
    view.value = 'capture'
    sectionViewMode.value = 'all'
    await nextTick()
    const action = document.querySelector<HTMLElement>(`[data-readiness-action="${step.action}"]:not(:disabled)`)
    const target = action?.closest<HTMLElement>('.heading-warning, .capture-actions') ?? document.getElementById('capture-actions')
    if (!target) return
    target.scrollIntoView({ behavior: 'smooth', block: 'center' })
    target.classList.remove('journey-focus')
    void target.getBoundingClientRect()
    target.classList.add('journey-focus')
    window.setTimeout(() => target.classList.remove('journey-focus'), 1_400)
    action?.focus({ preventScroll: true })
    return
  }
  view.value = 'structure'
  if (step.sectionId) {
    focusedSectionId.value = step.sectionId
    sectionViewMode.value = 'focused'
  } else {
    sectionViewMode.value = 'all'
  }
  await nextTick()
  const target = step.action === 'hear'
    ? document.getElementById('song-transport')
    : step.action === 'section'
      ? document.getElementById('section-toolbar')
      : step.stage === 'harmony'
      ? document.getElementById(`harmony-tools-${step.sectionId}`) ?? document.getElementById('harmony-tools')
      : step.stage === 'arrangement'
        ? document.getElementById(`arrangement-${step.sectionId}`)
        : document.getElementById(`section-${step.sectionId}`)
  if (!target) return
  if (target instanceof HTMLDetailsElement) target.open = true
  target.scrollIntoView({ behavior: 'smooth', block: 'center' })
  target.classList.remove('journey-focus')
  void target.getBoundingClientRect()
  target.classList.add('journey-focus')
  window.setTimeout(() => target.classList.remove('journey-focus'), 1_400)
  const action = target.querySelector<HTMLElement>(`[data-readiness-action="${step.action}"]:not(:disabled)`)
  if (action) action.focus({ preventScroll: true })
  else if (target instanceof HTMLDetailsElement) target.querySelector<HTMLElement>('summary')?.focus({ preventScroll: true })
}
async function goToCreatorStage(stage: CreatorStage) {
  roleReviewActive.value = false
  const requested = phoneCaptureMode.value ? remapDesktopStageForPhone(stage) : remapPhoneStageForDesktop(stage)
  const destination = phoneCaptureMode.value
    ? phoneDestination(requested, Boolean(project.value?.sections.length))
    : creatorDestination(requested as DesktopCreatorStage, Boolean(project.value?.sections.length))
  if (!destination) return
  activeCreatorStage.value = (destination.stage ?? requested) as CreatorStage
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
function warnBeforeClose(event: BeforeUnloadEvent) {
  if (isDirty.value || deviceLyricCaptureDirty.value || pendingRoughVocal.value || roughVocalCaptureState.value === 'recording')
    event.preventDefault()
}

async function saveRecoverySnapshot() {
  if (!project.value || !isDirty.value || !persistedRevision.value || busy.value || recoveryBlocked.value) return
  const snapshot: BrowserRecoveryRecord = {
    projectId: project.value.id,
    project: JSON.parse(JSON.stringify(project.value)) as SongProject,
    baseProjectLastModifiedUtc: persistedRevision.value,
    sessionId,
    capturedAtUtc: new Date().toISOString(),
  }
  let browserProtected = false
  try {
    await protectBrowserRecovery(snapshot)
    browserProtected = true
    browserRecoveryNeedsReview.value = false
    await refreshBrowserRecovery()
  } catch (error) {
    activityLog.write('error', 'recovery.browser', error instanceof Error ? error.message : 'Unsaved work could not be protected in browser storage.', { projectId: snapshot.projectId })
  }
  try {
    await projectsApi.saveRecovery(snapshot.project, snapshot.baseProjectLastModifiedUtc, snapshot.sessionId)
    if (browserProtected) {
      await discardBrowserRecovery(snapshot.projectId)
      await refreshBrowserRecovery()
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unsaved recovery snapshot failed.'
    status.value = browserProtected
      ? 'The local project service is unavailable. Unsaved work is protected in this browser until it reconnects.'
      : message
    if (message.includes('another session') || message.includes('Reload it before saving')) {
      recoveryBlocked.value = true
      activityLog.write('error', 'recovery.snapshot', `${message} Automatic host recovery paused; the browser copy remains protected.`, { projectId: snapshot.projectId, browserProtected })
    } else {
      workspaceConnection.value = 'unavailable'
      workspaceHealth.value = null
      activityLog.write(browserProtected ? 'warning' : 'error', 'recovery.snapshot', browserProtected ? `${message} Unsaved work remains protected in this browser.` : message, { projectId: snapshot.projectId, browserProtected })
    }
  }
}

watch(serializedProject, () => {
  if (recoveryTimer) clearTimeout(recoveryTimer)
  if (isDirty.value && !recoveryBlocked.value) recoveryTimer = setTimeout(() => void saveRecoverySnapshot(), 1_000)
})

watch(serializedDeviceLyricCapture, () => {
  if (deviceLyricCaptureTimer) clearTimeout(deviceLyricCaptureTimer)
  if (deviceLyricCaptureDirty.value) deviceLyricCaptureTimer = setTimeout(() => void persistDeviceLyricCapture(), 800)
})

watch(deviceLyricCaptureQuery, () => { showAllDeviceLyricCaptureResults.value = false })
watch(deviceLyricCaptures, nextCaptures => {
  const availableIds = new Set(nextCaptures.map(capture => capture.captureId))
  selectedDeviceLyricCaptureIds.value = selectedDeviceLyricCaptureIds.value.filter(id => availableIds.has(id))
})
watch([libraryQuery, libraryStageFilter], () => { showAllLibraryResults.value = false })
watch(projects, nextProjects => {
  const availableEmptyIds = new Set(nextProjects
    .filter(summary => projectLibraryStage(summary) === 'empty')
    .map(summary => summary.id))
  selectedLibraryProjectIds.value = selectedLibraryProjectIds.value.filter(id => availableEmptyIds.has(id))
})
watch(trashQuery, () => { showAllTrashResults.value = false })
watch(trashedProjects, nextProjects => {
  const availableIds = new Set(nextProjects.map(summary => summary.id))
  selectedTrashProjectIds.value = selectedTrashProjectIds.value.filter(id => availableIds.has(id))
})

watch(
  () => [assignedArrangementRoleIds.value.join(','), instrumentQualityFilter.value, workspaceConnection.value] as const,
  () => { void refreshInstrumentRecommendations() },
)

watch(
  () => [(project.value?.noteEvents ?? []).map(note => `${note.id}:${note.pitch.letter}:${note.pitch.accidental}:${note.pitch.octave}`).join('|'), workspaceConnection.value] as const,
  () => { void refreshInstrumentRangeReviews() },
)

watch(
  () => [view.value, project.value?.id, project.value?.sections.length ?? 0] as const,
  ([nextView, nextProjectId], previous) => {
    if (previous && (nextView !== previous[0] || nextProjectId !== previous[1])) {
      stopChordAudition()
      stopPartAudition()
      stopTransport()
      focusedSectionId.value = ''
      sectionViewMode.value = 'all'
    }
    if (focusedSectionId.value && !project.value?.sections.some(section => section.id === focusedSectionId.value)) {
      focusedSectionId.value = ''
      sectionViewMode.value = 'all'
    }
    if (nextView === 'structure') void refreshLyricTimeline()
  })

onMounted(async () => {
  window.addEventListener('beforeunload', warnBeforeClose)
  window.addEventListener('online', handleConnectivityChange)
  window.addEventListener('offline', handleConnectivityChange)
  window.addEventListener('beforeinstallprompt', captureInstallPrompt)
  window.addEventListener('appinstalled', markApplicationInstalled)
  phoneLayoutQuery = window.matchMedia(`(max-width: ${phoneLayoutMaxWidth}px)`)
  syncPhoneCaptureMode()
  phoneLayoutQuery.addEventListener('change', handlePhoneLayoutChange)
  try {
    await registerApplicationShell(registration => {
      shellUpdateRegistration.value = registration
      activityLog.write('info', 'delivery.update', 'A newer application shell is ready.')
    })
  } catch (error) {
    activityLog.write('warning', 'delivery.shell', error instanceof Error ? error.message : 'Application shell registration failed.')
  }
  await Promise.all([refreshBrowserRecovery(), refreshBrowserProjects(), refreshDeviceLyricCaptures()])
  await Promise.all([refreshWorkspaceHealth(), refreshLibrary(), refreshRecovery()])
  if (workspaceConnection.value === 'ready' && recoveryCount.value > 0) {
    view.value = 'recovery'
    status.value = `${recoveryCount.value} protected song recover${recoveryCount.value === 1 ? 'y' : 'ies'} found.`
  }
})
onBeforeUnmount(() => {
  stopChordAudition()
  stopPartAudition()
  stopTransport()
  discardPendingRoughVocal(false)
  if (deviceLyricCaptureTimer) clearTimeout(deviceLyricCaptureTimer)
  window.removeEventListener('beforeunload', warnBeforeClose)
  window.removeEventListener('online', handleConnectivityChange)
  window.removeEventListener('offline', handleConnectivityChange)
  window.removeEventListener('beforeinstallprompt', captureInstallPrompt)
  window.removeEventListener('appinstalled', markApplicationInstalled)
  phoneLayoutQuery?.removeEventListener('change', handlePhoneLayoutChange)
  if (recoveryTimer) clearTimeout(recoveryTimer)
})
</script>

<template>
  <main :class="{ 'has-project': view !== 'home', 'phone-capture': phoneCaptureMode }">
    <input ref="portableImportInput" hidden type="file" accept=".json,.maskil.json,.maskil,application/json,application/vnd.maskil-forge.project+json,application/vnd.maskil-forge.project+zip,application/zip" @change="selectPortableImport" />
    <aside v-if="showWorkspaceConnectionBanner" class="workspace-connection" :class="workspaceConnection" role="status" aria-live="polite">
      <span class="connection-mark" aria-hidden="true"></span>
      <div><strong>{{ workspaceConnectionTitle }}</strong><small v-if="!phoneCaptureMode || !phoneChrome.compactHostStatus || workspaceConnection !== 'ready'">{{ workspaceConnectionDetail }}</small><small v-if="applicationShellDetail" class="shell-note">{{ applicationShellDetail }}</small><small v-if="browserRecoveryDetail" class="browser-recovery-note">{{ browserRecoveryDetail }}</small></div>
      <div v-if="installPrompt || shellUpdateRegistration || workspaceConnection === 'unavailable'" class="workspace-delivery-actions">
        <button v-if="installPrompt" class="quiet" @click="installApplication">Install app</button>
        <button v-if="shellUpdateRegistration" class="quiet" @click="applyApplicationShellUpdate">Update app</button>
        <button v-if="workspaceConnection === 'unavailable'" class="quiet" :disabled="workspaceCheckBusy" @click="refreshWorkspaceHealth">{{ workspaceCheckBusy ? 'Checking…' : 'Reconnect' }}</button>
      </div>
    </aside>
    <header v-if="view === 'home'" class="welcome library-home">
      <p class="eyebrow">Your songwriting workspace</p>
      <h1>Maskil Forge</h1>
      <p class="tagline">Understand the words. Forge the music.</p>
      <div v-if="workspaceConnection === 'ready'" class="welcome-actions">
        <button @click="requestNewProject">Begin a new song</button>
        <button class="secondary" :disabled="busy" @click="requestPortableImport">Import project file</button>
        <small class="portable-import-help">Open an artist-owned <code>.maskil.json</code> song or a <code>.maskil</code> package that carries original recordings.</small>
        <details class="open-project">
          <summary>Open an existing song</summary>
          <label>Project ID<input v-model="projectId" placeholder="Paste project ID" /></label>
          <button class="secondary" :disabled="busy" @click="requestLoad">Open song</button>
        </details>
      </div>
      <p v-if="workspaceConnection === 'unavailable'" class="offline-home-note">The local project service still owns your song library. You can write in a browser-owned lyric capture or review explicitly cached song saves below; reconnect before changing a host-owned song.</p>
      <p v-else-if="workspaceConnection === 'checking'" class="offline-home-note checking-home-note">Checking the local project service before opening project actions…</p>
      <p v-else class="status home-status" role="status">{{ status }}</p>
      <section class="device-capture-library" aria-labelledby="device-capture-library-title">
        <div class="device-capture-heading">
          <div><p class="eyebrow">Browser-owned words</p><h2 id="device-capture-library-title">Capture lyrics on this device</h2><p>{{ deviceLyricCaptureDetail }} These captures are editable without the local project service, but they are not synchronized or part of the saved-song library until you explicitly add them.</p></div>
          <div class="device-capture-heading-actions"><button v-if="deviceLyricCaptureSummaries.length && !deviceLyricCaptureCleanupMode" class="quiet" @click="beginDeviceLyricCaptureCleanup">Manage captures</button><button v-else-if="deviceLyricCaptureCleanupMode" class="quiet" @click="finishDeviceLyricCaptureCleanup">Finish cleanup</button><button v-if="!deviceLyricCaptureCleanupMode" @click="beginDeviceLyricCapture">New device capture</button></div>
        </div>
        <template v-if="deviceLyricCaptureSummaries.length">
          <div class="library-tools device-capture-tools">
            <label class="library-search">Search device captures<input v-model="deviceLyricCaptureQuery" type="search" placeholder="Title or artist" /></label>
          </div>
          <aside v-if="deviceLyricCaptureCleanupMode" class="library-cleanup-note device-capture-cleanup-note">
            <div><strong>Permanent device cleanup</strong><span>These captures have no Trash or synchronized copy. Select only work you recognize; nothing is selected or removed automatically.</span></div>
            <div class="library-selection-actions"><button class="secondary" :disabled="visibleDeviceLyricCaptures.length === 0" @click="selectVisibleDeviceLyricCaptures">Select visible</button><button class="quiet" :disabled="selectedDeviceLyricCaptures.length === 0" @click="clearDeviceLyricCaptureSelection">Clear selection</button></div>
          </aside>
          <div class="library-result-row" role="status"><span>{{ deviceLyricCaptureResults.resultCount ? `Showing ${deviceLyricCaptureResults.visibleCount} of ${deviceLyricCaptureResults.resultCount} capture${deviceLyricCaptureResults.resultCount === 1 ? '' : 's'}` : 'No captures match this search' }}</span><span v-if="deviceLyricCaptureCleanupMode && selectedDeviceLyricCaptures.length">{{ selectedDeviceLyricCaptures.length }} selected</span></div>
          <p v-if="deviceLyricCaptureResults.resultCount === 0" class="library-message">No browser-owned captures match this title or artist.</p>
          <div v-else class="project-grid device-capture-grid">
            <article v-for="summary in visibleDeviceLyricCaptures" :key="summary.id" class="project-card device-capture-card" :class="{ 'project-card-selected': selectedDeviceLyricCaptureIds.includes(summary.id) }">
              <label v-if="deviceLyricCaptureCleanupMode" class="library-project-selection"><input type="checkbox" :checked="selectedDeviceLyricCaptureIds.includes(summary.id)" :aria-label="`Select ${summary.title}, saved ${formatModified(summary.savedAtUtc)}`" @change="setDeviceLyricCaptureSelected(summary.id, $event)" /><span>Select this device capture</span></label>
              <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
              <dl><div><dt>Words</dt><dd>{{ summary.lyricLineCount ? `${summary.lyricLineCount} non-empty line${summary.lyricLineCount === 1 ? '' : 's'}` : 'Empty capture' }}</dd></div><div><dt>Saved here</dt><dd>{{ formatModified(summary.savedAtUtc) }}</dd></div></dl>
              <div v-if="!deviceLyricCaptureCleanupMode" class="card-actions"><button class="secondary" @click="openDeviceLyricCapture(summary.id)">Continue capture</button><button class="danger" :aria-label="`Permanently delete ${summary.title}`" @click="requestDeviceLyricCaptureDelete(summary.id, summary.title)">Delete</button></div>
            </article>
          </div>
          <div v-if="deviceLyricCaptureResults.resultCount > deviceLyricCaptureRecentLimit" class="library-pagination"><button class="quiet" @click="showAllDeviceLyricCaptureResults = !showAllDeviceLyricCaptureResults">{{ showAllDeviceLyricCaptureResults ? `Show recent ${deviceLyricCaptureRecentLimit}` : `Show ${deviceLyricCaptureResults.hiddenCount} more` }}</button></div>
          <div v-if="deviceLyricCaptureCleanupMode" class="library-cleanup-bar device-capture-cleanup-bar"><span>{{ selectedDeviceLyricCaptures.length ? `${selectedDeviceLyricCaptures.length} capture${selectedDeviceLyricCaptures.length === 1 ? '' : 's'} selected` : 'Select device captures to review them together.' }}</span><button class="danger" :disabled="deviceLyricCaptureBusy || selectedDeviceLyricCaptures.length === 0" @click="requestBulkDeviceLyricCaptureDelete">Permanently delete selected</button></div>
        </template>
      </section>
      <section v-if="workspaceConnection === 'unavailable' && browserRecoverySummaries.length" class="browser-recovery-vault" aria-labelledby="browser-recovery-title">
        <div><p class="eyebrow">Protected on this device</p><h2 id="browser-recovery-title">Unsaved work is waiting safely</h2><p>These browser snapshots cannot be edited while the project service is unavailable. Reconnect it to restore them into the normal review-and-save flow.</p></div>
        <div class="project-grid">
          <article v-for="summary in browserRecoverySummaries" :key="summary.id" class="project-card recovery-card browser-recovery-card">
            <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
            <dl><div><dt>Contents</dt><dd>{{ summary.sectionCount ? `${summary.sectionCount} structured section${summary.sectionCount === 1 ? '' : 's'} · ${summary.lyricLineCount} lyric line${summary.lyricLineCount === 1 ? '' : 's'}` : summary.hasRawLyrics ? `Raw lyric draft · ${summary.lyricLineCount} non-empty line${summary.lyricLineCount === 1 ? '' : 's'}` : 'New idea' }}</dd></div><div><dt>Protected</dt><dd>{{ formatModified(summary.capturedAtUtc) }}</dd></div></dl>
          </article>
        </div>
      </section>
      <section v-if="workspaceConnection === 'unavailable'" class="offline-project-library" aria-labelledby="offline-library-title">
        <div><p class="eyebrow">Saved on this device</p><h2 id="offline-library-title">Review a saved snapshot</h2><p>{{ browserProjectDetail }} These copies are not editable and never replace the local project service.</p></div>
        <p v-if="browserProjectSummaries.length === 0" class="library-message">Open or save a song while connected to make its latest explicit save available for view-only review here.</p>
        <div v-else class="project-grid">
          <article v-for="summary in browserProjectSummaries" :key="summary.id" class="project-card offline-project-card">
            <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
            <dl><div><dt>Saved contents</dt><dd>{{ summary.sectionCount ? `${summary.sectionCount} structured section${summary.sectionCount === 1 ? '' : 's'} · ${summary.lyricLineCount} lyric line${summary.lyricLineCount === 1 ? '' : 's'}` : summary.hasRawLyrics ? `Raw lyric draft · ${summary.lyricLineCount} non-empty line${summary.lyricLineCount === 1 ? '' : 's'}` : 'New idea' }}</dd></div><div v-if="summary.sectionTitles.length"><dt>Song form</dt><dd class="recovery-form">{{ summary.sectionTitles.join(' → ') }}</dd></div><div><dt>Cached</dt><dd>{{ formatModified(summary.savedAtUtc) }}</dd></div></dl>
            <div class="card-actions"><button class="secondary" @click="openOfflineReview(summary.id)">Review saved snapshot</button></div>
          </article>
        </div>
      </section>
      <section v-if="workspaceConnection === 'ready'" class="project-library" aria-labelledby="library-title">
        <div class="library-heading"><div><p class="eyebrow">Song library</p><h2 id="library-title">Continue your work</h2></div><div class="library-actions"><button v-if="recoveryCount" class="recovery-button" @click="openRecovery">Recovery ({{ recoveryCount }})</button><button class="quiet" @click="openTrash">Trash</button><button class="quiet" :disabled="libraryBusy" @click="refreshLibrary">Refresh</button></div></div>
        <p v-if="libraryBusy" class="library-message">Finding your saved songs…</p>
        <p v-else-if="projects.length === 0" class="library-message">No saved songs yet. Begin with any idea, even if it has no structure.</p>
        <template v-else>
          <div class="library-tools">
            <label class="library-search">Search saved songs<input v-model="libraryQuery" type="search" placeholder="Title or artist" /></label>
            <label class="library-filter">Stage<select v-model="libraryStageFilter" :disabled="libraryCleanupMode"><option value="all">All songs</option><option value="structured">Structured songs</option><option value="raw">Raw drafts</option><option value="empty">Empty starts</option></select></label>
            <button v-if="!libraryCleanupMode" class="quiet library-cleanup-toggle" @click="beginLibraryCleanup">Review empty starts</button>
            <button v-else class="quiet library-cleanup-toggle" @click="finishLibraryCleanup">Finish review</button>
          </div>
          <aside v-if="libraryCleanupMode" class="library-cleanup-note">
            <div><strong>Review empty starts only</strong><span>These saved songs have no raw lyrics or structured sections. Select only what you recognize; nothing is selected or removed automatically, and moved songs remain restorable from Trash.</span></div>
            <div class="library-selection-actions"><button class="secondary" :disabled="visibleLibraryProjects.length === 0" @click="selectVisibleEmptyStarts">Select visible</button><button class="quiet" :disabled="selectedLibraryProjects.length === 0" @click="clearLibrarySelection">Clear selection</button></div>
          </aside>
          <div class="library-result-row" role="status"><span>{{ libraryResults.resultCount ? `Showing ${libraryResults.visibleCount} of ${libraryResults.resultCount} song${libraryResults.resultCount === 1 ? '' : 's'}` : 'No songs match this view' }}</span><span v-if="libraryCleanupMode && selectedLibraryProjects.length">{{ selectedLibraryProjects.length }} selected</span></div>
          <p v-if="libraryResults.resultCount === 0" class="library-message">{{ libraryCleanupMode ? 'No empty starts match this search.' : 'No saved songs match this search and stage.' }}</p>
          <div v-else class="project-grid">
            <article v-for="summary in visibleLibraryProjects" :key="summary.id" class="project-card" :class="{ 'project-card-selected': selectedLibraryProjectIds.includes(summary.id) }">
              <label v-if="libraryCleanupMode" class="library-project-selection"><input type="checkbox" :checked="selectedLibraryProjectIds.includes(summary.id)" :aria-label="`Select ${summary.title}, modified ${formatModified(summary.lastModifiedUtc)}`" @change="setLibraryProjectSelected(summary.id, $event)" /><span>Select this empty start</span></label>
              <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
              <dl><div><dt>Stage</dt><dd>{{ summary.sectionCount ? `${summary.sectionCount} structured section${summary.sectionCount === 1 ? '' : 's'}` : summary.hasRawLyrics ? 'Raw lyric draft' : 'New idea' }}</dd></div><div><dt>Modified</dt><dd>{{ formatModified(summary.lastModifiedUtc) }}</dd></div></dl>
              <div class="card-actions"><button class="secondary" :disabled="busy" @click="openSummary(summary.id)">Continue song</button><details class="card-menu"><summary>More actions</summary><div class="card-menu-panel"><button class="secondary" :disabled="busy" @click="duplicateSong(summary.id, summary.title, $event)">Duplicate saved song</button><button class="danger" :disabled="busy" @click="requestDelete(summary.id, summary.title, $event)">Delete song</button></div></details></div>
            </article>
          </div>
          <div v-if="libraryResults.resultCount > libraryRecentLimit" class="library-pagination"><button class="quiet" @click="showAllLibraryResults = !showAllLibraryResults">{{ showAllLibraryResults ? `Show recent ${libraryRecentLimit}` : `Show ${libraryResults.hiddenCount} more` }}</button></div>
          <div v-if="libraryCleanupMode" class="library-cleanup-bar"><span>{{ selectedLibraryProjects.length ? `${selectedLibraryProjects.length} empty start${selectedLibraryProjects.length === 1 ? '' : 's'} selected` : 'Select empty starts to review them together.' }}</span><button class="danger" :disabled="busy || selectedLibraryProjects.length === 0" @click="requestBulkTrash">Move selected to Trash</button></div>
        </template>
      </section>
    </header>

    <section v-else-if="view === 'device-capture' && activeDeviceLyricCapture" class="device-capture-editor" aria-labelledby="device-capture-title">
      <header class="device-capture-editor-heading">
        <button class="quiet" @click="closeDeviceLyricCapture">← Back</button>
        <div><p class="eyebrow">Browser-owned lyric capture</p><h1 id="device-capture-title">{{ activeDeviceLyricCapture.title.trim() || 'Untitled capture' }}</h1><p>Editable on this device, even without the local project service.</p></div>
        <span class="editor-state" :class="{ modified: deviceLyricCaptureDirty }">{{ deviceLyricCaptureBusy ? 'Saving…' : deviceLyricCaptureDirty ? 'Saving locally…' : 'Saved on device' }}</span>
      </header>
      <aside class="device-capture-boundary">
        <strong>This is a browser-owned capture, not a synchronized song.</strong>
        <span>It stays in this browser profile. Clearing site data can remove it. When the local workspace is connected, “Add to song library” creates a new host-owned song and removes this device copy only after that succeeds.</span>
      </aside>
      <section class="device-capture-form">
        <div class="device-capture-identity">
          <label>Capture title<input v-model="activeDeviceLyricCapture.title" maxlength="200" placeholder="Untitled capture" /></label>
          <label>Artist<input v-model="activeDeviceLyricCapture.artist" maxlength="200" placeholder="Artist or songwriter" /></label>
          <label>Genre<select v-model="activeDeviceLyricCapture.genre"><option v-for="genre in genres" :key="genre" :value="genre">{{ genre === 'RAndB' ? 'R&B' : genre }}</option></select></label>
          <label class="device-capture-description">Description<textarea v-model="activeDeviceLyricCapture.description" maxlength="2000" rows="3" placeholder="Song concept or creative context" /></label>
        </div>
        <label class="device-capture-lyrics"><span>Raw lyric draft</span><textarea v-model="activeDeviceLyricCapture.rawLyricDraft" maxlength="100000" rows="18" autofocus placeholder="Write lyrics, fragments, images, or plain thoughts…" /></label>
        <p class="device-capture-save-note">Words save automatically in this browser after you pause. Song sections, harmony, arrangement, and playback begin after the capture enters the connected song library.</p>
        <div class="device-capture-actions">
          <button class="secondary" :disabled="deviceLyricCaptureBusy || !deviceLyricCaptureDirty" @click="persistDeviceLyricCapture">Save on this device</button>
          <button v-if="workspaceConnection === 'ready'" :disabled="deviceLyricCaptureBusy || !activeDeviceLyricCapture.title.trim() || !activeDeviceLyricCapture.rawLyricDraft.trim()" @click="addDeviceLyricCaptureToLibrary">Add to song library</button>
          <button v-else class="secondary" :disabled="workspaceCheckBusy" @click="refreshWorkspaceHealth">{{ workspaceCheckBusy ? 'Checking…' : 'Reconnect to add to library' }}</button>
          <button class="danger" :disabled="deviceLyricCaptureBusy" @click="requestDeviceLyricCaptureDelete(activeDeviceLyricCapture.captureId, activeDeviceLyricCapture.title.trim() || 'Untitled capture')">Delete device capture</button>
        </div>
      </section>
      <p class="status" role="status">{{ status }}</p>
    </section>

    <section v-else-if="view === 'offline-review' && offlineReviewProject" class="offline-review" aria-labelledby="offline-review-title">
      <header class="offline-review-heading">
        <button class="quiet" @click="closeOfflineReview">← Back</button>
        <div><p class="eyebrow">Saved snapshot · View only</p><h1 id="offline-review-title">{{ offlineReviewProject.project.title }}</h1><p>{{ offlineReviewProject.project.artist || 'Artist not set' }} · {{ offlineReviewProject.project.genre === 'Unspecified' ? 'Genre not set' : offlineReviewProject.project.genre }}</p></div>
        <div class="offline-review-actions">
          <button v-if="workspaceConnection === 'ready'" @click="openOfflineReviewEditable">Open editable song</button>
          <button v-else class="secondary" :disabled="workspaceCheckBusy" @click="refreshWorkspaceHealth">{{ workspaceCheckBusy ? 'Checking…' : 'Reconnect to edit' }}</button>
        </div>
      </header>
      <aside class="offline-review-boundary">
        <strong>This is the last explicit save cached on this device.</strong>
        <span>Cached {{ formatModified(offlineReviewProject.savedAtUtc) }}. Changes cannot be made here, and newer saves from another browser or device are not synchronized.</span>
      </aside>
      <section v-if="offlineReviewProject.project.rawLyricDraft.trim()" class="offline-review-raw" aria-labelledby="offline-raw-title">
        <div><p class="eyebrow">Original source</p><h2 id="offline-raw-title">Raw lyric draft</h2></div>
        <pre>{{ offlineReviewProject.project.rawLyricDraft }}</pre>
      </section>
      <section class="offline-review-structure" aria-labelledby="offline-structure-title">
        <div class="offline-review-section-heading"><p class="eyebrow">Song anatomy</p><h2 id="offline-structure-title">{{ offlineReviewProject.project.sections.length ? `${offlineReviewProject.project.sections.length} saved section${offlineReviewProject.project.sections.length === 1 ? '' : 's'}` : 'No structured sections yet' }}</h2></div>
        <p v-if="offlineReviewProject.project.sections.length === 0" class="library-message">This save contains an idea or raw lyric draft, but its song sections have not been created yet.</p>
        <ol v-else class="offline-review-sections">
          <li v-for="(section, index) in offlineReviewProject.project.sections" :key="section.id">
            <header><span>{{ index + 1 }}</span><div><h3>{{ section.title }}</h3><p>{{ label(section.kind) }} · {{ deliveryLabel(section.delivery) }}</p></div></header>
            <p v-if="section.performanceNotes" class="offline-performance-note"><strong>Performance direction</strong>{{ section.performanceNotes }}</p>
            <div v-if="section.lyricLines.some(line => line.text.trim())" class="offline-lyrics">
              <p v-for="line in section.lyricLines.filter(line => line.text.trim())" :key="line.id">{{ line.text }}</p>
            </div>
            <p v-else class="offline-empty-section">No lyric lines saved in this section.</p>
          </li>
        </ol>
      </section>
    </section>

    <section v-else-if="view === 'recovery'" class="trash-view recovery-view" aria-labelledby="recovery-title" :inert="workspaceConnection === 'unavailable'">
      <button class="quiet" @click="goHome">← Song library</button>
      <div class="trash-heading"><p class="eyebrow">Protected work</p><h1 id="recovery-title">Recover unsaved songs</h1><p>Each card represents one song, even when the same work is protected by both the local host and this browser. Nothing is removed automatically.</p></div>
      <p class="status" role="status">{{ status }}</p>
      <p v-if="recoveryCount === 0" class="library-message">No unsaved recovery snapshots remain.</p>
      <template v-else>
        <section class="recovery-hygiene" aria-labelledby="recovery-hygiene-title">
          <div><p class="eyebrow">Recovery hygiene</p><h2 id="recovery-hygiene-title">{{ recoveryCount }} protected song{{ recoveryCount === 1 ? '' : 's' }}</h2><p>The five newest songs stay in view. Ten is a soft attention threshold, not a deletion rule; work older than {{ recoveryStaleDays }} days is labeled stale for explicit review.</p></div>
          <dl><div><dt>Shown now</dt><dd>{{ visibleRecoveryQueue.length }}</dd></div><div><dt>Stale</dt><dd>{{ recoveryHygiene.staleCount }}</dd></div><div><dt>Soft cap</dt><dd>{{ recoverySoftCap }}</dd></div></dl>
          <div class="recovery-hygiene-actions">
            <button v-if="recoveryHygiene.hiddenCount" class="secondary" @click="showAllRecoveries = !showAllRecoveries">{{ showAllRecoveries ? 'Show five recent' : `Show ${recoveryHygiene.hiddenCount} older` }}</button>
            <button v-if="recoveryHygiene.staleCount" class="danger" :disabled="busy" @click="requestStaleRecoveryCleanup">Review stale cleanup ({{ recoveryHygiene.staleCount }})</button>
          </div>
        </section>
        <aside v-if="recoveryHygiene.overSoftCap" class="recovery-cap-warning" role="note"><strong>Recovery attention recommended.</strong><span>{{ recoveryCount }} songs have unsaved work. Restore and save the directions you want to keep, or explicitly discard snapshots you no longer need. New work will still be protected.</span></aside>
        <div class="project-grid recovery-grid">
        <article v-for="summary in visibleRecoveryQueue" :key="summary.id" class="project-card recovery-card" :class="{ 'recovery-card-stale': summary.isStale }">
          <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
          <div class="recovery-badges"><span>{{ summary.sourceLabel }}</span><span v-if="summary.isStale" class="stale-badge">Stale · {{ summary.ageDays }} days</span></div>
          <dl>
            <div><dt>Contents</dt><dd>{{ summary.sectionCount ? `${summary.sectionCount} structured section${summary.sectionCount === 1 ? '' : 's'} · ${summary.lyricLineCount} lyric line${summary.lyricLineCount === 1 ? '' : 's'}` : summary.hasRawLyrics ? `Raw lyric draft · ${summary.lyricLineCount} non-empty line${summary.lyricLineCount === 1 ? '' : 's'}` : 'New idea' }}</dd></div>
            <div v-if="summary.sectionTitles.length"><dt>Song form</dt><dd class="recovery-form">{{ summary.sectionTitles.join(' → ') }}</dd></div>
            <div><dt>Protected</dt><dd>{{ formatModified(summary.capturedAtUtc) }}</dd></div>
          </dl>
          <div class="card-actions recovery-card-actions"><button v-if="summary.hasHostSnapshot" :disabled="busy" @click="restoreRecovery(summary.id)">{{ summary.hasBrowserSnapshot ? 'Restore host copy' : 'Restore unsaved work' }}</button><button v-if="summary.hasBrowserSnapshot" class="secondary" :disabled="busy" @click="restoreBrowserProtectedWork(summary.id)">{{ summary.hasHostSnapshot ? 'Restore browser copy' : 'Restore protected work' }}</button><button class="danger" :disabled="busy" @click="requestRecoveryDiscard(summary, $event)">Discard protected work</button></div>
        </article>
        </div>
      </template>
    </section>

    <section v-else-if="view === 'trash'" class="trash-view" aria-labelledby="trash-title" :inert="workspaceConnection === 'unavailable'">
      <button class="quiet" @click="goHome">← Song library</button>
      <div class="trash-heading"><p class="eyebrow">Recovery</p><h1 id="trash-title">Trash</h1><p>Restore a song to your library or permanently delete it. Permanent deletion cannot be undone.</p></div>
      <p class="status" role="status">{{ status }}</p>
      <p v-if="libraryBusy" class="library-message">Opening Trash…</p>
      <p v-else-if="trashedProjects.length === 0" class="library-message">Trash is empty.</p>
      <template v-else>
        <div class="trash-tools">
          <label>Search Trash<input v-model="trashQuery" type="search" placeholder="Title or artist" /></label>
          <button v-if="!trashSelectionMode" class="quiet" @click="beginTrashSelection">Select songs</button>
          <button v-else class="quiet" @click="finishTrashSelection">Finish selection</button>
        </div>
        <aside class="trash-hygiene-note"><strong>Trash stays until you decide.</strong><span>There is no automatic cap or expiry. {{ trashOldDays }}-day labels are reminders for review, never a deletion rule.</span></aside>
        <aside v-if="trashSelectionMode" class="trash-selection-note">
          <div><strong>Choose exact songs</strong><span>Nothing is selected automatically. Restore returns songs to your library; permanent deletion receives a separate final review.</span></div>
          <div class="library-selection-actions"><button class="secondary" :disabled="visibleTrashQueue.length === 0" @click="selectVisibleTrashProjects">Select visible</button><button class="quiet" :disabled="selectedTrashProjects.length === 0" @click="clearTrashSelection">Clear selection</button></div>
        </aside>
        <div class="library-result-row" role="status"><span>{{ trashResults.resultCount ? `Showing ${trashResults.visibleCount} of ${trashResults.resultCount} song${trashResults.resultCount === 1 ? '' : 's'}` : 'No songs match this search' }}</span><span>{{ trashResults.oldCount ? `${trashResults.oldCount} in Trash ${trashOldDays}+ days` : trashSelectionMode && selectedTrashProjects.length ? `${selectedTrashProjects.length} selected` : '' }}</span></div>
        <p v-if="trashResults.resultCount === 0" class="library-message">No Trash items match this title or artist.</p>
        <div v-else class="project-grid trash-grid">
          <article v-for="summary in visibleTrashQueue" :key="summary.id" class="project-card trash-card" :class="{ 'trash-card-old': summary.isOld, 'project-card-selected': selectedTrashProjectIds.includes(summary.id) }">
            <label v-if="trashSelectionMode" class="library-project-selection"><input type="checkbox" :checked="selectedTrashProjectIds.includes(summary.id)" :aria-label="`Select ${summary.title}, deleted ${formatModified(summary.deletedAtUtc)}`" @change="setTrashProjectSelected(summary.id, $event)" /><span>Select this song</span></label>
            <div><h3>{{ summary.title }}</h3><p>{{ summary.artist || 'Artist not set' }}</p></div>
            <div v-if="summary.isOld" class="recovery-badges"><span class="stale-badge">Review · {{ trashAgeLabel(summary.ageDays) }}</span></div>
            <dl><div><dt>Deleted</dt><dd>{{ formatModified(summary.deletedAtUtc) }}</dd></div><div><dt>In Trash</dt><dd>{{ trashAgeLabel(summary.ageDays) }}</dd></div></dl>
            <div class="card-actions"><button class="secondary" :disabled="busy" @click="restoreSong(summary.id, summary.title)">Restore song</button><button class="danger" :disabled="busy" @click="requestPermanentDelete(summary.id, summary.title)">Permanently delete</button></div>
          </article>
        </div>
        <div v-if="trashResults.resultCount > trashRecentLimit" class="library-pagination"><button class="quiet" @click="showAllTrashResults = !showAllTrashResults">{{ showAllTrashResults ? `Show recent ${trashRecentLimit}` : `Show ${trashResults.hiddenCount} more` }}</button></div>
        <div v-if="trashSelectionMode" class="library-cleanup-bar trash-selection-bar"><span>{{ selectedTrashProjects.length ? `${selectedTrashProjects.length} song${selectedTrashProjects.length === 1 ? '' : 's'} selected` : 'Select songs to restore or permanently delete.' }}</span><div><button class="secondary" :disabled="busy || selectedTrashProjects.length === 0" @click="requestBulkRestore">Restore selected</button><button class="danger" :disabled="busy || selectedTrashProjects.length === 0" @click="requestBulkPermanentDelete">Permanently delete selected</button></div></div>
      </template>
    </section>

    <template v-else-if="project">
      <section v-if="workspaceConnection === 'unavailable'" class="offline-editor-interruption" aria-labelledby="offline-editor-title">
        <p class="eyebrow">Editing paused</p>
        <h1 id="offline-editor-title">{{ project.title }}</h1>
        <p>{{ currentProjectBrowserProtected ? 'Your latest unsaved state is protected in this browser.' : 'This editor cannot protect new changes until the local project service reconnects.' }}</p>
        <p>Reconnect the project service before continuing. Maskil Forge will keep server saves revision-checked and will not overwrite a newer song silently.</p>
        <div class="offline-editor-actions"><button class="quiet" :disabled="workspaceCheckBusy" @click="refreshWorkspaceHealth">{{ workspaceCheckBusy ? 'Checking…' : 'Reconnect' }}</button><button v-if="currentProjectBrowserProtected" class="secondary" @click="leaveProtectedOfflineEditor">View protected recovery</button></div>
      </section>
      <div v-else class="workspace-surface">
      <header class="project-bar">
        <a class="wordmark" href="#" aria-label="Maskil Forge home" @click.prevent="requestHome">Maskil Forge</a>
        <label class="title-field"><span>Song title</span><input v-model="project.title" maxlength="200" /></label>
        <span class="editor-state" :class="{ modified: isDirty, saved: !isDirty }">{{ editorState }}</span>
        <nav class="project-actions" aria-label="Project actions">
          <button v-if="!phoneCaptureMode || phoneChrome.keepUndoRedoInBar" class="secondary" :disabled="busy || !response?.canUndo" @click="undo">Undo</button>
          <button v-if="!phoneCaptureMode || phoneChrome.keepUndoRedoInBar" class="secondary" :disabled="busy || !response?.canRedo" @click="redo">Redo</button>
          <button :disabled="busy || !isDirty" @click="saveProject">Save</button>
        </nav>
        <details class="project-menu">
          <summary>Project</summary>
          <div class="project-menu-panel">
            <button v-if="phoneCaptureMode && !phoneChrome.keepUndoRedoInBar" class="secondary" :disabled="busy || !response?.canUndo" @click="undo">Undo</button>
            <button v-if="phoneCaptureMode && !phoneChrome.keepUndoRedoInBar" class="secondary" :disabled="busy || !response?.canRedo" @click="redo">Redo</button>
            <button class="secondary" @click="requestNewProject">New song</button>
            <button class="secondary" @click="requestHome">Song library</button>
            <button class="secondary" :disabled="busy" @click="exportPortableProject">Export project file</button>
            <button class="secondary" :disabled="busy" @click="requestPortableImport">Import project file</button>
            <button class="danger" @click="requestDelete(project.id, project.title)">Delete this song</button>
            <label>Open by project ID<input v-model="projectId" placeholder="Project UUID" /></label>
            <button class="secondary" :disabled="busy" @click="requestLoad">Open song</button>
            <a href="/logs.html" target="_blank">Activity console ↗</a>
          </div>
        </details>
      </header>

      <p class="status" role="status">{{ status }}</p>

      <nav class="creator-journey" :class="{ 'phone-capture': phoneCaptureMode }" aria-label="Songwriting workspaces">
        <p v-if="!phoneCaptureMode || phoneChrome.showJourneyIntro"><strong>Current workspace</strong><span>{{ phoneCaptureMode ? 'On this phone: idea, words, shape, review, and approve. Harmony and arrangement stay on a larger screen.' : 'Move between creative areas without changing your song.' }}</span></p>
        <ol>
          <li v-for="stage in visibleCreatorStages" :key="stage.id" :class="{ 'stage-active': activeCreatorStage === stage.id }">
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
        <div v-if="!phoneCaptureMode || phoneChrome.showJourneyProgress" class="journey-progress" aria-label="Song development progress">
          <strong>Your song journey</strong>
          <ul>
            <li v-for="stage in visibleCreatorStages" :key="`progress-${stage.id}`" :class="`progress-${creatorStageState(stage.id)}`">
              <span aria-hidden="true">{{ creatorStageState(stage.id) === 'complete' ? '✓' : '○' }}</span>
              {{ journeyProgressLabel(stage.id) }}
            </li>
          </ul>
        </div>
      </nav>

      <section v-if="view === 'capture'" class="capture-workspace" aria-labelledby="capture-title">
        <div class="capture-heading">
          <p v-if="!phoneCaptureMode || !phoneChrome.compactCaptureChrome" class="eyebrow">Start with the words</p>
          <h1 id="capture-title">Capture the idea</h1>
          <p v-if="!phoneCaptureMode || !phoneChrome.compactCaptureChrome">{{ phoneCaptureMode ? 'Write lyrics, fragments, or plain thoughts. Shape the song, then review and save this capture. Harmony, arrangement, and vocal capture stay on a larger screen for now.' : 'Write lyrics, fragments, images, themes, or plain thoughts. You do not need to know the song structure yet.' }}</p>
        </div>
        <label class="raw-lyrics"><span :class="{ 'sr-only': phoneCaptureMode && phoneChrome.compactCaptureChrome }">Raw lyric draft</span><textarea id="raw-lyric-draft" v-model="project.rawLyricDraft" maxlength="100000" :rows="phoneCaptureMode && phoneChrome.compactCaptureChrome ? 10 : 18" autofocus placeholder="Write whatever is on your mind…&#10;&#10;A complete song is not required. Fragments are welcome." /></label>
        <div id="capture-actions" class="capture-actions">
          <button v-if="!phoneCaptureMode || !phoneChrome.compactCaptureChrome" :disabled="busy || !isDirty" @click="saveDraft">Save draft</button>
          <button v-if="!phoneCaptureMode || !phoneChrome.compactCaptureChrome" class="secondary" :disabled="busy" @click="beginStructuring">Shape manually</button>
          <button :data-readiness-action="currentStructurePreview ? undefined : 'preview'" :disabled="busy || !project.rawLyricDraft.trim()" @click="previewPastedStructure">Preview song structure</button>
        </div>
        <section v-if="structurePreview && currentStructurePreview" class="structure-preview" aria-labelledby="structure-preview-title">
          <div class="structure-preview-heading">
            <div><p class="eyebrow">Nothing created yet</p><h2 id="structure-preview-title">Review detected sections</h2><p>Correct the proposal, then create every section as one undoable decision. Your original lyric sheet remains preserved.</p></div>
            <strong>{{ structurePreview.sections.length }} section{{ structurePreview.sections.length === 1 ? '' : 's' }}</strong>
          </div>
          <p v-if="project.sections.length" class="preview-warning">These sections will be appended after your {{ project.sections.length }} existing section{{ project.sections.length === 1 ? '' : 's' }}.</p>
          <aside v-if="structurePreview.unrecognizedHeadings.length" class="heading-warning" role="alert">
            <strong>{{ structurePreview.unrecognizedHeadings.length }} heading{{ structurePreview.unrecognizedHeadings.length === 1 ? ' needs' : 's need' }} review</strong>
            <p>Maskil Forge did not guess their section types. Choose a type to include each block in song order, or leave it unresolved in the preserved raw draft.</p>
            <ol class="unrecognized-sections">
              <li v-for="(unresolved, index) in structurePreview.unrecognizedSections" :key="`${unresolved.heading}:${unresolved.insertionIndex}`">
                <div><strong>[{{ unresolved.heading }}]</strong><small>{{ unresolved.lyrics.length }} lyric line{{ unresolved.lyrics.length === 1 ? '' : 's' }} · {{ unresolved.lyrics.slice(0, 2).join(' / ') || 'No lyric lines' }}</small></div>
                <label>Use as
                  <select v-model="unresolved.resolutionKind" data-readiness-action="resolve">
                    <option :value="undefined">Choose type…</option>
                    <option v-for="kind in (['Intro','Verse','Chorus','PreChorus','Bridge','Outro'] as SectionKind[])" :key="kind" :value="kind">{{ label(kind) }}</option>
                  </select>
                </label>
                <button type="button" class="secondary" :disabled="!unresolved.resolutionKind" @click="resolveUnrecognizedSection(index)">Include section</button>
              </li>
            </ol>
          </aside>
          <p v-if="structurePreview.unassignedLines.length" class="preview-warning">{{ structurePreview.unassignedLines.length }} unassigned line{{ structurePreview.unassignedLines.length === 1 ? '' : 's' }} will remain only in the preserved raw draft.</p>
          <ol v-if="structurePreview.sections.length" class="proposed-sections">
            <li v-for="(section, index) in structurePreview.sections" :key="index">
              <span class="section-number">{{ String(index + 1).padStart(2, '0') }}</span>
              <div class="proposed-section-fields">
                <label>Type<select v-model="section.kind"><option v-for="kind in (['Intro','Verse','Chorus','PreChorus','Bridge','Outro'] as SectionKind[])" :key="kind" :value="kind">{{ label(kind) }}</option></select></label>
                <label>Title<input v-model="section.title" maxlength="100" /></label>
                <label class="structural-role-field">Role in song<select v-model="section.structuralFunction" :aria-describedby="`preview-role-help-${index}`"><option v-for="item in structuralRoles" :key="item.id" :value="item.id">{{ item.label }}</option></select><small :id="`preview-role-help-${index}`" class="structural-role-help">{{ structuralRole(section.structuralFunction).help }}</small></label>
                <label v-if="!phoneCaptureMode || phoneChrome.showSectionPerformance">Delivery<select v-model="section.delivery"><option v-for="delivery in (['Sung','TalkSung','Spoken','Whispered'] as SectionDelivery[])" :key="delivery" :value="delivery">{{ deliveryLabel(delivery) }}</option></select></label>
                <label v-if="!phoneCaptureMode || phoneChrome.showSectionPerformance" class="proposed-direction">Performance direction<input v-model="section.performanceNotes" maxlength="1000" placeholder="Optional direction" /></label>
                <small>{{ section.lyrics.length }} lyric line{{ section.lyrics.length === 1 ? '' : 's' }} · {{ section.lyrics.slice(0, 2).join(' / ') || 'No lyric lines' }}</small>
              </div>
              <div class="proposed-section-actions">
                <button type="button" class="quiet" :disabled="index === 0" @click="moveProposedSection(index, -1)">↑</button>
                <button type="button" class="quiet" :disabled="index === structurePreview.sections.length - 1" @click="moveProposedSection(index, 1)">↓</button>
                <button type="button" class="danger" @click="removeProposedSection(index)">Remove</button>
              </div>
            </li>
          </ol>
          <p v-else class="preview-warning">No recognized sections are ready to create.</p>
          <div class="capture-actions"><button data-readiness-action="preview" :disabled="busy || !structurePreview.sections.length" @click="acceptStructurePreview">Create sections</button><button class="secondary" :disabled="busy" @click="cancelStructurePreview">Cancel preview</button></div>
        </section>
        <p v-if="!phoneCaptureMode || !phoneChrome.compactCaptureChrome" class="preservation-note">Your raw draft remains preserved when you begin creating Verse, Chorus, and other sections.</p>
      </section>

      <template v-else>
      <div v-if="!phoneCaptureMode || !phoneChrome.compactShapeChrome" class="structure-nav"><button class="quiet" @click="returnToDraft">← Raw lyric draft</button></div>
      <section v-if="phoneCaptureMode" class="phone-readiness" aria-labelledby="phone-readiness-title">
        <div class="readiness-next-step">
          <span v-if="!phoneChrome.compactShapeChrome" class="eyebrow">Phone capture</span>
          <h3 id="phone-readiness-title" :class="{ 'sr-only': phoneChrome.compactShapeChrome && phoneCaptureReview.nextStep }">{{ phoneCaptureReview.complete ? 'Capture ready to continue later' : 'Next on this phone' }}</h3>
          <p v-if="!phoneChrome.compactShapeChrome">{{ phoneCaptureReview.nextAction }}</p>
          <button v-if="phoneCaptureReview.nextStep" type="button" :disabled="busy" @click="goToNextReadinessStep">{{ phoneCaptureReview.nextStep.label }} →</button>
        </div>
        <ol v-if="phoneCaptureReview.sections.length && !phoneChrome.compactShapeChrome">
          <li v-for="sectionReview in phoneCaptureReview.sections" :key="sectionReview.sectionId" :class="{ ready: sectionReview.ready }">
            <strong>{{ sectionReview.title }}</strong>
            <span :class="{ complete: sectionReview.hasLyrics }">Lyrics</span>
          </li>
        </ol>
      </section>
      <section v-if="phoneCaptureMode && (activeCreatorStage === 'review' || activeCreatorStage === 'approve')" id="phone-review" class="phone-review" aria-labelledby="phone-review-title">
        <div class="phone-review-heading">
          <p class="eyebrow">Review</p>
          <h2 id="phone-review-title">Read the song as written</h2>
          <p>This is the current words and form. It does not change the song, start playback, or add harmony.</p>
        </div>
        <section v-if="project.rawLyricDraft.trim()" class="phone-review-raw" aria-labelledby="phone-raw-title">
          <p class="eyebrow">Raw lyric draft</p>
          <h3 id="phone-raw-title">Preserved source</h3>
          <pre>{{ project.rawLyricDraft }}</pre>
        </section>
        <section class="phone-review-structure" aria-labelledby="phone-structure-title">
          <p class="eyebrow">Song anatomy</p>
          <h3 id="phone-structure-title">{{ project.sections.length ? `${project.sections.length} section${project.sections.length === 1 ? '' : 's'}` : 'No structured sections yet' }}</h3>
          <p v-if="!project.sections.length" class="phone-review-empty">Add a section in Shape to review the form here.</p>
          <ol v-else class="phone-review-sections">
            <li v-for="(section, index) in project.sections" :key="`review-${section.id}`">
              <header>
                <span>{{ String(index + 1).padStart(2, '0') }}</span>
                <div>
                  <h4>{{ section.title }}</h4>
                  <p>{{ label(section.kind) }}<template v-if="section.structuralFunction !== 'Unspecified'"> · {{ structuralFunctionLabel(section.structuralFunction) }}</template></p>
                </div>
              </header>
              <p v-if="section.performanceNotes.trim()" class="phone-performance-note"><strong>Direction</strong>{{ section.performanceNotes }}</p>
              <div v-if="section.lyricLines.some(line => line.text.trim())" class="phone-lyrics">
                <p v-for="line in section.lyricLines.filter(line => line.text.trim())" :key="line.id">{{ line.text }}</p>
              </div>
              <p v-else class="phone-empty-section">No lyric lines yet.</p>
            </li>
          </ol>
        </section>
        <section v-if="activeCreatorStage === 'review'" class="microphone-preflight" aria-labelledby="microphone-preflight-title">
          <div>
            <p class="eyebrow">Original performance</p>
            <h3 id="microphone-preflight-title">Record a rough vocal take</h3>
            <p>Recording starts only when you ask. The take stays temporary in this tab until you listen and choose Save take.</p>
          </div>
          <p v-if="!roughVocalSupport.supported" class="microphone-preflight-status unavailable" role="status">{{ roughVocalSupport.reason }}</p>
          <p v-else-if="microphonePreflightState === 'ready'" class="microphone-preflight-status ready" role="status"><strong>{{ microphonePreflightLabel }}</strong>{{ microphonePreflightMessage }}</p>
          <p v-else-if="microphonePreflightMessage" class="microphone-preflight-status" :class="{ unavailable: microphonePreflightState === 'failed' }" role="status">{{ microphonePreflightMessage }}</p>
          <p v-if="isDirty" class="microphone-preflight-status unavailable" role="status">Save the current words and structure before attaching a recording to this version.</p>
          <p v-if="roughVocalCaptureMessage" class="microphone-preflight-status" :class="{ ready: roughVocalCaptureState === 'saved' || roughVocalCaptureState === 'review', unavailable: roughVocalCaptureState === 'failed' }" role="status">{{ roughVocalCaptureMessage }}</p>
          <div class="rough-vocal-actions">
            <button
              type="button"
              class="secondary"
              :disabled="!roughVocalSupport.supported || microphonePreflightState === 'checking' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'requesting' || roughVocalCaptureState === 'saving'"
              @click="checkRoughVocalMicrophone">
              {{ microphonePreflightState === 'checking' ? 'Checking microphone…' : microphonePreflightState === 'ready' ? 'Check again' : 'Check microphone' }}
            </button>
            <button
              v-if="roughVocalCaptureState !== 'recording'"
              type="button"
              :disabled="!roughVocalSupport.supported || busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'requesting' || roughVocalCaptureState === 'saving'"
              @click="startRoughVocalRecording">
              {{ roughVocalCaptureState === 'requesting' ? 'Opening microphone…' : project.assets.length ? 'Record another take' : 'Record rough take' }}
            </button>
            <button v-else type="button" class="danger recording-stop" @click="stopRoughVocalRecording(false)">Stop recording</button>
          </div>
          <section v-if="pendingRoughVocal" class="rough-vocal-review" aria-labelledby="rough-vocal-review-title">
            <div>
              <p class="eyebrow">Temporary take</p>
              <h4 id="rough-vocal-review-title">Listen before saving</h4>
              <p>{{ formatRoughVocalDuration(pendingRoughVocal.durationMs) }} · {{ formatRoughVocalBytes(pendingRoughVocal.blob.size) }}</p>
            </div>
            <audio controls preload="metadata" :src="pendingRoughVocal.url" @play="logRoughVocalPlayback('temporary')">Your browser cannot play this temporary recording.</audio>
            <div class="rough-vocal-actions">
              <button type="button" :disabled="roughVocalCaptureState === 'saving' || isDirty" @click="savePendingRoughVocal">{{ roughVocalCaptureState === 'saving' ? 'Saving take…' : 'Save take' }}</button>
              <button type="button" class="danger" :disabled="roughVocalCaptureState === 'saving'" @click="discardPendingRoughVocal(true)">Discard take</button>
            </div>
          </section>
          <section v-if="project.assets.length" class="saved-vocal-takes" aria-labelledby="saved-vocal-takes-title">
            <h4 id="saved-vocal-takes-title">Saved rough takes</h4>
            <ol>
              <li v-for="(asset, index) in project.assets" :key="asset.id">
                <div><strong>{{ asset.name }}</strong><small>{{ new Date(asset.createdUtc).toLocaleString() }} · {{ formatRoughVocalBytes(asset.byteLength) }}</small></div>
                <audio controls preload="none" :src="projectsApi.originalVocalTakeUrl(project.id, asset.id)" @play="logRoughVocalPlayback('saved', asset.id)">Your browser cannot play this saved take.</audio>
                <p v-if="loudnessObservationSummary(asset.id)" class="performance-observation-summary">{{ loudnessObservationSummary(asset.id) }}</p>
                <p v-if="pitchObservationSummary(asset.id)" class="performance-observation-summary pitch">{{ pitchObservationSummary(asset.id) }}</p>
                <p v-if="onsetObservationSummary(asset.id)" class="performance-observation-summary onset">{{ onsetObservationSummary(asset.id) }}</p>
                <details v-if="performanceEvidenceCount(asset.id)" class="performance-evidence-inspector">
                  <summary>
                    <span>Inspect analyzer evidence</span>
                    <small>{{ performanceEvidenceCount(asset.id) }} claim{{ performanceEvidenceCount(asset.id) === 1 ? '' : 's' }} · artist review</small>
                  </summary>
                  <div class="performance-evidence-body">
                    <p>These measurements remain analyzer evidence. Marking a claim inaccurate lets you store a separate correction; the original analyzer values stay unchanged. Accurate claims, or inaccurate claims with a stored correction, can be promoted into an artist gesture snapshot. Promotion still does not create a note, beat, or MIDI event. Desktop Music can inspect these takes and sketch notes from pitch, onset, and loudness gestures.</p>
                    <section v-for="group in performanceEvidenceGroups(asset.id)" :key="group.key" class="performance-evidence-group">
                      <header>
                        <h5>{{ group.label }}</h5>
                        <span>{{ group.count }}</span>
                      </header>
                      <p class="performance-evidence-provenance"><code>{{ group.analyzerId }}</code> · v{{ group.analyzerVersion }} · {{ group.provenanceLabel }} · {{ new Date(group.createdUtc).toLocaleString() }}</p>
                      <ol>
                        <li v-for="row in group.rows" :key="row.id">
                          <time>{{ row.timeLabel }}</time>
                          <span>{{ row.measurementLabel }}</span>
                          <small>{{ row.confidenceLabel }}</small>
                          <div class="performance-evidence-review" :data-verdict="row.reviewVerdict ?? 'Unreviewed'">
                            <strong>{{ row.reviewVerdict ? `Artist marked ${row.reviewVerdict.toLowerCase()}` : 'Unreviewed' }}</strong>
                            <button type="button" class="quiet" :aria-pressed="row.reviewVerdict === 'Accurate'" :disabled="busy || isDirty || workspaceConnection !== 'ready' || row.reviewVerdict === 'Accurate'" @click="reviewPerformanceObservation(asset.id, row.id, 'Accurate')">Accurate</button>
                            <button type="button" class="quiet" :aria-pressed="row.reviewVerdict === 'Inaccurate'" :disabled="busy || isDirty || workspaceConnection !== 'ready' || row.reviewVerdict === 'Inaccurate'" @click="reviewPerformanceObservation(asset.id, row.id, 'Inaccurate')">Inaccurate</button>
                            <button v-if="row.reviewVerdict" type="button" class="quiet" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="reviewPerformanceObservation(asset.id, row.id, null)">Clear</button>
                          </div>
                          <form v-if="row.reviewVerdict === 'Inaccurate'" class="performance-evidence-correction" @submit.prevent="savePerformanceObservationCorrection(asset.id, row)">
                            <p>{{ row.correctionLabel || 'Record a separate correction. Analyzer values stay unchanged.' }}</p>
                            <div class="performance-evidence-correction-fields">
                              <label v-for="field in row.correctionFields" :key="field.name" class="performance-evidence-correction-field">
                                <span>{{ field.label }}</span>
                                <input
                                  type="number"
                                  inputmode="decimal"
                                  :min="field.min || undefined"
                                  :max="field.max || undefined"
                                  :step="field.step"
                                  :value="correctionDraftValue(row.id, field)"
                                  :disabled="busy || isDirty || workspaceConnection !== 'ready'"
                                  :aria-label="field.label"
                                  @input="setCorrectionDraft(row.id, field.name, ($event.target as HTMLInputElement).value)"
                                >
                              </label>
                            </div>
                            <div class="performance-evidence-correction-actions">
                              <button type="submit" :disabled="busy || isDirty || workspaceConnection !== 'ready'">{{ row.hasCorrection ? 'Update correction' : 'Save correction' }}</button>
                              <button v-if="row.hasCorrection" type="button" class="quiet" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="clearPerformanceObservationCorrection(asset.id, row.id)">Remove correction</button>
                            </div>
                          </form>
                          <div v-if="row.canPromote || row.hasGesture" class="performance-evidence-gesture">
                            <p>{{ row.gestureLabel || 'Promote the approved measurements into an artist gesture. This still does not create a note or MIDI event.' }}</p>
                            <div class="performance-evidence-gesture-actions">
                              <button v-if="row.canPromote" type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="setPerformanceObservationGesture(asset.id, row.id, true)">{{ row.hasGesture ? 'Update gesture' : 'Promote gesture' }}</button>
                              <button v-if="row.hasGesture" type="button" class="quiet" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="setPerformanceObservationGesture(asset.id, row.id, null)">Remove gesture</button>
                            </div>
                          </div>
                        </li>
                      </ol>
                      <button v-if="group.remainingCount" type="button" class="quiet performance-evidence-more" @click="showMorePerformanceEvidence(asset.id, group.key, group.count)">Show {{ Math.min(12, group.remainingCount) }} more · {{ group.remainingCount }} remaining</button>
                    </section>
                  </div>
                </details>
                <p v-if="loudnessAnalysisMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ loudnessAnalysisMessages[asset.id] }}</p>
                <p v-if="pitchAnalysisMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ pitchAnalysisMessages[asset.id] }}</p>
                <p v-if="onsetAnalysisMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ onsetAnalysisMessages[asset.id] }}</p>
                <p v-if="performanceReviewMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ performanceReviewMessages[asset.id] }}</p>
                <div class="saved-vocal-take-actions">
                  <button type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="analyzeSavedRoughVocal(asset)">{{ loudnessAnalysisAssetId === asset.id ? 'Analyzing loudness…' : loudnessObservationSummary(asset.id) ? 'Reanalyze loudness' : 'Analyze loudness' }}</button>
                  <button type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="analyzeSavedRoughVocalPitch(asset)">{{ pitchAnalysisAssetId === asset.id ? 'Analyzing pitch…' : pitchObservationSummary(asset.id) ? 'Reanalyze pitch' : 'Analyze pitch' }}</button>
                  <button type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="analyzeSavedRoughVocalOnsets(asset)">{{ onsetAnalysisAssetId === asset.id ? 'Analyzing onsets…' : onsetObservationSummary(asset.id) ? 'Reanalyze onsets' : 'Analyze onsets' }}</button>
                  <button type="button" class="secondary" :disabled="busy || isDirty || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="requestRenameSavedRoughVocal(asset)">Rename</button>
                  <button type="button" class="danger" :disabled="busy || isDirty || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="requestRemoveSavedRoughVocal(asset, index + 1)">Remove take</button>
                </div>
              </li>
            </ol>
          </section>
          <small>Saved takes keep durable names while their original audio bytes remain immutable. Loudness, pitch, and onset analysis decode a take locally on this device and save non-authoritative evidence; they never change the recording or create notes. The evidence inspector exposes each claim and lets the artist mark it accurate or inaccurate, store a separate correction when inaccurate, and promote approved measurements into an artist gesture snapshot. Rerunning one analyzer replaces its claims and clears only verdicts, corrections, and gestures attached to those replaced claims. Backup, recovery, Trash, duplication, and portable <code>.maskil</code> export carry source, evidence, artist verdicts, current corrections, and current gestures. Gesture snapshots stay here as approved evidence. Turning pitch, onset, and loudness gestures into playable notes happens on the desktop Music workspace, not on this phone.</small>
        </section>
        <button type="button" class="secondary" data-readiness-action="review" :disabled="busy || !project.sections.length" @click="goToCreatorStage('approve')">Continue to approve →</button>
      </section>
      <section v-if="phoneCaptureMode && activeCreatorStage === 'approve'" id="phone-approve" class="phone-approve" aria-labelledby="phone-approve-title">
        <p class="eyebrow">Approve</p>
        <h2 id="phone-approve-title">Save this capture</h2>
        <p v-if="isDirty">Save these words and form first. Then Review can record a rough vocal take against that exact saved version.</p>
        <p v-else>This capture is saved. Review can record and attach an artist-approved rough vocal take; harmony and arrangement continue on a larger screen.</p>
        <aside class="phone-capture-boundary">
          <strong>Phone scope</strong>
          <p>Idea, words, shape, review, and approve. Music tools stay on desktop so this screen does not become a miniature DAW.</p>
        </aside>
        <div class="phone-approve-actions">
          <button type="button" data-readiness-action="approve" :disabled="busy || !isDirty" @click="saveDraft">{{ isDirty ? 'Save and approve' : 'Capture saved' }}</button>
          <button type="button" class="quiet" :disabled="busy" @click="goToCreatorStage('shape')">Back to shape</button>
        </div>
      </section>
      <details v-if="!phoneCaptureMode" id="musical-refinement" class="disclosure-panel timeline-disclosure">
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
      <section v-if="!phoneCaptureMode || !phoneChrome.separateReviewFromShape || activeCreatorStage === 'shape'" id="song-structure" class="song-canvas" aria-label="Song structure">
        <div class="canvas-heading">
          <div v-if="!phoneCaptureMode || !phoneChrome.compactShapeChrome"><p class="eyebrow">Song structure</p><h1>Shape the song</h1></div>
          <details v-if="phoneCaptureMode && phoneChrome.compactSectionToolbar" id="section-toolbar" class="phone-section-toolbar">
            <summary>+ Add section</summary>
            <div class="section-toolbar" aria-label="Add song section">
              <button v-for="kind in (['Intro','Verse','Chorus','PreChorus','Bridge','Outro'] as SectionKind[])" :key="kind" class="secondary add-section" data-readiness-action="section" :disabled="busy" @click="addSectionFromMenu(kind)">+ {{ label(kind) }}</button>
            </div>
          </details>
          <div v-else id="section-toolbar" class="section-toolbar" aria-label="Add song section">
            <button v-for="kind in (['Intro','Verse','Chorus','PreChorus','Bridge','Outro'] as SectionKind[])" :key="kind" class="secondary add-section" data-readiness-action="section" :disabled="busy" @click="addSection(kind)">+ {{ label(kind) }}</button>
          </div>
        </div>

        <aside v-if="structureLocked" class="structure-lock-notice">
          <div>
            <p class="eyebrow">Structure timing locked</p>
            <strong>{{ project.musicalParts.length }} musical part{{ project.musicalParts.length === 1 ? ' uses' : 's use' }} the song timeline.</strong>
            <p>Lyrics, harmony, section role, delivery, and direction remain editable. Manage musical parts to unlock order, length, meter, duplication, and deletion.</p>
          </div>
          <button v-if="!phoneCaptureMode" type="button" class="quiet" @click="goToCreatorStage('arrangement')">Manage timing →</button>
        </aside>

        <p v-if="project.sections.length === 0" class="empty-song">Choose a section above and start writing your first line.</p>
        <nav v-else-if="!phoneCaptureMode || !phoneChrome.compactShapeChrome || phoneShowsSongOutline(project.sections.length)" class="song-outline" aria-label="Song section outline">
          <div class="song-outline-heading">
            <div v-if="!phoneCaptureMode || !phoneChrome.compactShapeChrome"><strong>Song outline</strong><small>Jump between {{ project.sections.length }} sections without losing the full song.</small></div>
            <div v-if="!phoneCaptureMode || phoneChrome.showRoleReview" class="role-review-summary">
              <span><strong>{{ roleReview.decidedCount }}/{{ roleReview.sectionCount }}</strong> roles decided</span>
              <small>{{ roleReview.complete ? 'Functional arc reviewed.' : 'Optional · roles are never guessed.' }}</small>
              <button v-if="roleReview.nextSectionId" type="button" class="quiet" @click="reviewNextOpenRole">Review next open role →</button>
            </div>
            <div class="outline-view-actions">
              <span v-if="!phoneCaptureMode">{{ editableDemoReview.readySectionCount }}/{{ editableDemoReview.sectionCount }} ready to hear</span>
              <span v-else>{{ phoneCaptureReview.sections.filter(item => item.ready).length }}/{{ phoneCaptureReview.sections.length }} sections have lyrics</span>
              <button type="button" class="quiet" :disabled="!focusedSectionId || sectionViewMode === 'focused'" @click="showFocusedSection">Focus selected</button>
              <button type="button" class="quiet" :disabled="sectionViewMode === 'all'" @click="showAllSections">Show all</button>
            </div>
          </div>
          <ol>
            <li v-for="(section, index) in project.sections" :key="`outline-${section.id}`">
              <button type="button" :class="{ active: focusedSectionId === section.id, ready: songOutlineItems[index]?.ready }" :aria-current="focusedSectionId === section.id ? 'location' : undefined" @click="focusSongSection(section.id)">
                <span class="outline-order">{{ String(index + 1).padStart(2, '0') }}</span>
                <span class="outline-copy"><strong>{{ section.title }}</strong><small><template v-if="section.structuralFunction !== 'Unspecified'">{{ structuralFunctionLabel(section.structuralFunction) }} · </template>{{ label(section.kind) }}<template v-if="!phoneCaptureMode || phoneChrome.showSectionPerformance"> · {{ deliveryLabel(section.delivery) }}</template><template v-if="!phoneCaptureMode || phoneChrome.showSectionTiming"> · {{ placementFor(section.id)?.durationBars ?? 0 }} bars</template> · {{ section.lyricLines.length }} line{{ section.lyricLines.length === 1 ? '' : 's' }}</small></span>
                <span class="outline-progress">{{ songOutlineItems[index]?.progress }}</span>
              </button>
            </li>
          </ol>
        </nav>
        <div v-if="sectionViewMode === 'focused'" class="focused-section-nav" aria-label="Focused section navigation">
          <button type="button" class="quiet" :disabled="focusedSectionIndex() <= 0" @click="navigateFocusedSection(-1)">← Previous section</button>
          <span>Focused workspace · {{ focusedSectionIndex() + 1 }} of {{ project.sections.length }}</span>
          <button type="button" class="quiet" :disabled="focusedSectionIndex() >= project.sections.length - 1" @click="navigateFocusedSection(1)">Next section →</button>
        </div>
        <p v-if="roleReviewActive && (!phoneCaptureMode || phoneChrome.showRoleReview)" class="role-review-mode" role="status">Role review in progress · saving a decided role continues to the next open section. Choose “Not decided” to stay here.</p>
        <ol class="sections">
          <li v-for="(section, index) in project.sections" v-show="sectionViewMode === 'all' || focusedSectionId === section.id" :id="`section-${section.id}`" :key="section.id" class="section-card" :class="{ 'lyrics-first': phoneCaptureMode && phoneChrome.lyricsBeforeRole }">
            <div class="section-heading">
              <span class="section-number">{{ String(index + 1).padStart(2, '0') }}</span>
              <div class="section-identity">
                <span>{{ label(section.kind) }}</span>
                <label><span class="sr-only">Section title</span><input :value="section.title" maxlength="100" @change="renameSection(section.id, ($event.target as HTMLInputElement).value)" /></label>
                <div v-if="(!phoneCaptureMode || phoneChrome.showSectionTiming) && placementFor(section.id)" class="section-position">
                  <span>Bars {{ placementFor(section.id)!.start.bar }}–{{ placementFor(section.id)!.start.bar + placementFor(section.id)!.durationBars - 1 }}</span>
                  <label>Length <input :value="placementFor(section.id)!.durationBars" type="number" min="1" max="128" :aria-label="`${section.title} length in bars`" :disabled="busy || structureLocked" :title="structureLocked ? 'Remove all musical parts before changing section length.' : undefined" @change="setSectionDuration(section.id, Number(($event.target as HTMLInputElement).value))" /> bars</label>
                </div>
              </div>
              <div class="section-actions">
                <button class="quiet" :disabled="busy || structureLocked || index === 0" :title="structureLocked ? 'Remove all musical parts before reordering sections.' : undefined" @click="moveSection(section.id, index - 1)">↑ <span>Move up</span></button>
                <button class="quiet" :disabled="busy || structureLocked || index === project.sections.length - 1" :title="structureLocked ? 'Remove all musical parts before reordering sections.' : undefined" @click="moveSection(section.id, index + 1)">↓ <span>Move down</span></button>
                <button class="quiet" :disabled="busy || structureLocked" :title="structureLocked ? 'Remove all musical parts before duplicating sections so absolute note timing stays trustworthy.' : 'Copy this section with fresh identities.'" @click="duplicateSection(section.id, section.title)">Duplicate</button>
                <button class="danger" :disabled="busy || structureLocked" :title="structureLocked ? 'Remove all musical parts before deleting sections.' : undefined" @click="removeSection(section.id)">Delete section</button>
              </div>
            </div>
            <form class="section-performance-intent" :class="{ 'phone-collapsed-role': phoneCaptureMode && phoneChrome.collapseSectionRole }" @submit.prevent="setSectionIntent(section.id, $event)">
              <details v-if="phoneCaptureMode && phoneChrome.collapseSectionRole" class="phone-section-role">
                <summary>Role in song</summary>
                <label class="structural-role-field">
                  <span class="sr-only">Role in song</span>
                  <select name="structuralFunction" :value="structuralRoleDrafts[section.id] ?? section.structuralFunction" :disabled="busy" :aria-describedby="`role-help-${section.id}`" @change="structuralRoleDrafts[section.id] = ($event.target as HTMLSelectElement).value as StructuralFunction">
                    <option v-for="item in structuralRoles" :key="item.id" :value="item.id">{{ item.label }}</option>
                  </select>
                  <small :id="`role-help-${section.id}`" class="structural-role-help">{{ structuralRole(structuralRoleDrafts[section.id] ?? section.structuralFunction).help }}</small>
                </label>
                <input type="hidden" name="delivery" :value="section.delivery" />
                <input type="hidden" name="performanceNotes" :value="section.performanceNotes" />
                <button type="submit" class="secondary" :disabled="busy">Save role</button>
              </details>
              <template v-else>
              <label class="structural-role-field">Role in song
                <select name="structuralFunction" :value="structuralRoleDrafts[section.id] ?? section.structuralFunction" :disabled="busy" :aria-describedby="`role-help-${section.id}`" @change="structuralRoleDrafts[section.id] = ($event.target as HTMLSelectElement).value as StructuralFunction">
                  <option v-for="item in structuralRoles" :key="item.id" :value="item.id">{{ item.label }}</option>
                </select>
                <small :id="`role-help-${section.id}`" class="structural-role-help">{{ structuralRole(structuralRoleDrafts[section.id] ?? section.structuralFunction).help }}</small>
              </label>
              <label v-if="!phoneCaptureMode || phoneChrome.showSectionPerformance">Delivery
                <select name="delivery" :value="section.delivery" :disabled="busy">
                  <option v-for="delivery in (['Sung','TalkSung','Spoken','Whispered'] as SectionDelivery[])" :key="delivery" :value="delivery">{{ deliveryLabel(delivery) }}</option>
                </select>
              </label>
              <input v-else type="hidden" name="delivery" :value="section.delivery" />
              <label v-if="!phoneCaptureMode || phoneChrome.showSectionPerformance">Performance direction
                <textarea name="performanceNotes" :value="section.performanceNotes" maxlength="1000" rows="2" placeholder="Ambient piano + distant pad; grounded, restrained, no lift" :disabled="busy" />
              </label>
              <input v-else type="hidden" name="performanceNotes" :value="section.performanceNotes" />
              <button type="submit" class="secondary" :disabled="busy">{{ phoneCaptureMode && !phoneChrome.showSectionPerformance ? 'Save role' : 'Save intent' }}</button>
              </template>
            </form>
            <details v-if="!phoneCaptureMode && reusableFoundationSources(section.id).length" class="reuse-foundation">
              <summary>Start from another section’s musical foundation</summary>
              <div>
                <p>Explicitly replace this section’s harmony, chord voicings, energy, density, and musical jobs. Lyrics, delivery, performance direction, approved notes, and musical parts are never copied.</p>
                <label>Source section
                  <select v-model="foundationSourceDrafts[section.id]" :disabled="busy || partsForSection(section.id).length > 0">
                    <option value="">Choose a section…</option>
                    <option v-for="source in reusableFoundationSources(section.id)" :key="source.id" :value="source.id">{{ source.title }}</option>
                  </select>
                </label>
                <button type="button" class="secondary" :disabled="busy || !foundationSourceDrafts[section.id] || partsForSection(section.id).length > 0" :title="partsForSection(section.id).length ? 'Remove this section’s musical parts before replacing its foundation.' : undefined" @click="reuseSectionFoundation(section.id)">Use musical foundation</button>
              </div>
            </details>
            <details v-if="!phoneCaptureMode" :id="index === 0 ? 'harmony-tools' : `harmony-tools-${section.id}`" class="disclosure-panel harmony-disclosure">
              <summary><span>Explore musical ideas</span><small>Optional · Add chords, compare options, and review how changes connect.</small></summary>
              <div class="harmony-editor" :aria-label="`Harmony for ${section.title}`">
              <div class="harmony-heading">
                <div>
                  <strong>Harmony</strong>
                  <small>Section chords in musical time. Roman analysis and generation come later.</small>
                </div>
                <button class="quiet" data-readiness-action="harmony" :disabled="busy" @click="addHarmonyChord(section.id)">+ Add chord</button>
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
                <button type="button" class="secondary" data-readiness-action="sketch" :disabled="busy" @click="prepareHarmonyNoteSketch(section.id)">
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
                  <button type="button" data-readiness-action="sketch" :disabled="busy" @click="useHarmonyNoteSketch(section.id)">Use this sketch</button>
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
                  <form class="voicing-editor" @submit.prevent="setChordVoicing(section.id, item.id, item.chord, $event)">
                    <label>How the chord is played
                      <input name="voicing" :value="voicingDrafts[item.id] ?? item.voicing?.voices.map(voice => formatRegisteredPitch(voice.pitch)).join(' ') ?? ''" placeholder="C3 G3 C4 E4" :disabled="busy" :aria-describedby="`voicing-help-${item.id}`" @input="voicingDrafts[item.id] = ($event.target as HTMLInputElement).value" />
                    </label>
                    <button type="submit" class="quiet" :disabled="busy">Use these notes</button>
                    <small :id="`voicing-help-${item.id}`">Chord tones: {{ chordToneNames(item.chord).join(' · ') }}. Enter low-to-high notes from A0 to C8. Leave empty to clear.</small>
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
              <div class="lyrics-heading"><span>Lyrics</span><button class="quiet" :data-readiness-action="writableEmptyLyric(section) ? undefined : 'lyrics'" :disabled="busy" @click="addLyricLine(index, true)">+ Add line</button></div>
              <div v-for="(line, lineIndex) in section.lyricLines" :key="line.id" class="lyric-line">
                <span class="lyric-line-number">{{ lineIndex + 1 }}</span>
                <input v-model="line.text" :data-line-id="line.id" :data-readiness-action="writableEmptyLyric(section)?.id === line.id ? 'lyrics' : undefined" maxlength="2000" :aria-label="`Lyric line ${lineIndex + 1}`" placeholder="Write a lyric line…" :disabled="busy || Boolean(lyricLineLock(line.id))" @change="editLyricLine(section.id, line.id, line.text)" @keydown.enter.prevent="addLineAfter(index, lineIndex)" @keydown.backspace="handleLineBackspace(index, lineIndex, line.text)" />
                <div class="lyric-line-actions">
                  <button v-if="lyricLineLock(line.id)" class="quiet" :disabled="busy" @click="unlockCreativeLock(lyricLineLock(line.id)!.id, 'Lyric line unlocked.')">Unlock line</button>
                  <button v-else-if="!phoneCaptureMode || phoneChrome.showLyricLocks" class="quiet" :disabled="busy" @click="lockLyricLine(line.id)">Lock line</button>
                  <button class="quiet lyric-delete" :disabled="busy || Boolean(lyricLineLock(line.id))" @click="removeLyricLine(index, lineIndex)">Remove line</button>
                </div>
                <details v-if="!phoneCaptureMode && line.words.length" class="disclosure-panel lyric-flow-tools">
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
            <details v-if="!phoneCaptureMode || phoneChrome.showDeveloperDetails" class="developer-details"><summary>Developer details</summary><small>Section ID: {{ section.id }}</small><template v-for="line in section.lyricLines" :key="line.id"><small>Line ID: {{ line.id }}</small><template v-for="word in line.words" :key="word.id"><small>Word ID: {{ word.id }} · {{ word.text }}</small><small v-for="syllable in word.syllables" :key="syllable.id">Syllable ID: {{ syllable.id }} · {{ syllable.position }} · {{ syllable.source }} · {{ syllable.text }} · Stress: {{ syllable.stress ? `${syllable.stress.level} (${syllable.stress.provenance})` : 'Unmarked' }}</small></template><small v-for="mark in line.punctuation" :key="mark.id">Punctuation ID: {{ mark.id }} · {{ mark.start }} · {{ mark.text }}</small><template v-for="phrase in line.phrases" :key="phrase.id"><small>Phrase ID: {{ phrase.id }} · {{ phrase.position }} · {{ phrase.source }} · {{ phrase.wordIds.join(', ') }}</small><small v-if="phrase.prosody">Prosodic Pattern ID: {{ phrase.prosody.id }}</small><small v-for="unit in phrase.prosody?.units ?? []" :key="unit.id">Prosodic Unit ID: {{ unit.id }} · {{ unit.position }} · {{ unit.syllableId }} · {{ unit.weight }} · {{ unit.provenance }}</small></template><small v-for="placement in line.syllablePlacements" :key="placement.id">Syllable Placement ID: {{ placement.id }} · {{ placement.syllableId }} · {{ placement.position.bar }}:{{ placement.position.beat }}:{{ placement.position.tick }} · {{ placement.provenance }}</small><template v-for="candidate in line.rhythmCandidates" :key="candidate.id"><small>Rhythm Candidate ID: {{ candidate.id }} · {{ candidate.phraseId }} · {{ candidate.label }} · {{ candidate.provenance }}</small><small v-for="candidateEvent in candidate.events" :key="candidateEvent.id">Rhythm Event ID: {{ candidateEvent.id }} · {{ candidateEvent.position }} · {{ candidateEvent.syllableId }} · {{ candidateEvent.beatPosition.bar }}:{{ candidateEvent.beatPosition.beat }}:{{ candidateEvent.beatPosition.tick }}</small></template><small v-for="breath in line.breathPoints" :key="breath.id">Breath Point ID: {{ breath.id }} · after {{ breath.afterSyllableId }} · {{ breath.provenance }}</small></template></details>
          </li>
        </ol>
      </section>

      <section v-if="!phoneCaptureMode" id="arrangement-blueprint" class="arrangement-blueprint" aria-labelledby="arrangement-title">
        <div class="arrangement-heading">
          <p class="eyebrow">Arrangement blueprint</p>
          <h2 id="arrangement-title">Shape the song’s energy</h2>
          <p>Describe how each section should feel before choosing instruments. These are creative intentions, not generated performances.</p>
        </div>
        <section id="instrument-knowledge" class="instrument-knowledge" aria-labelledby="instrument-knowledge-title">
          <div>
            <span class="eyebrow">Host knowledge</span>
            <h3 id="instrument-knowledge-title">Instrument profiles</h3>
            <p>These profiles are instrument concepts: range, musical jobs, articulations, and expressive qualities. They are not sample libraries or VST patches. Matching, range fit, gesture maps, and MIDI channels stay inspectable. Naming an instrument on a musical part below is an explicit artist choice; it does not retarget a gesture or emit a program change.</p>
          </div>
          <label class="instrument-feeling-filter">Choose by feeling
            <select v-model="instrumentQualityFilter" :disabled="workspaceConnection !== 'ready'">
              <option value="">Any feeling</option>
              <option v-for="quality in instrumentExpressiveQualities" :key="quality" :value="quality">{{ instrumentQualityLabel(quality) }}</option>
            </select>
          </label>
          <p v-if="!instrumentProfiles" class="note-event-empty">Instrument knowledge is unavailable until the local host is connected.</p>
          <article v-for="instrument in instrumentProfiles?.instruments ?? []" :key="instrument.id" class="instrument-profile" :aria-label="`${instrument.name} instrument profile`">
            <div>
              <strong>{{ instrument.name }}</strong>
              <small>{{ instrument.expressiveQualities.map(instrumentQualityLabel).join(' · ') }}</small>
            </div>
            <p v-if="instrument.pitched && instrument.minimumPitch && instrument.maximumPitch">Range {{ formatRegisteredPitch(instrument.minimumPitch) }}–{{ formatRegisteredPitch(instrument.maximumPitch) }}</p>
            <p v-else-if="!instrument.pitched">Unpitched. Kit pieces are not a melodic range.</p>
            <p>Jobs {{ instrument.roles.map(instrumentRoleLabel).join(', ') }}</p>
            <p>Articulations {{ instrument.articulations.map(instrumentArticulationLabel).join(', ') }}</p>
            <p v-if="midiChannelLabel(instrument.id)">{{ midiChannelLabel(instrument.id) }}</p>
            <ul v-if="articulationMapForInstrument(instrument.id)" class="instrument-gesture-map" :aria-label="`Gesture map for ${instrument.name}`">
              <li v-for="line in gestureMapCopy(instrument.id)" :key="`${instrument.id}-${line}`">{{ line }}</li>
            </ul>
          </article>
        </section>
        <section class="demo-readiness" aria-labelledby="demo-readiness-title">
          <div class="readiness-next-step">
            <span class="eyebrow">Hear–revise readiness</span>
            <h3 id="demo-readiness-title">{{ editableDemoReview.readySectionCount }} of {{ editableDemoReview.sectionCount }} sections ready</h3>
            <p>{{ editableDemoReview.nextAction }}</p>
            <button v-if="editableDemoReview.nextStep" type="button" :disabled="busy" @click="goToNextReadinessStep">{{ editableDemoReview.nextStep.label }} →</button>
          </div>
          <ol>
            <li v-for="sectionReview in editableDemoReview.sections" :key="sectionReview.sectionId" :class="{ ready: sectionReview.ready }">
              <strong>{{ sectionReview.title }}</strong>
              <span :class="{ complete: sectionReview.hasLyrics }">Lyrics</span>
              <span :class="{ complete: sectionReview.hasHarmony }">Harmony</span>
              <span :class="{ complete: sectionReview.hasRole }">Job</span>
              <span :class="{ complete: sectionReview.hasPlayablePart }">Playable part</span>
            </li>
          </ol>
        </section>
        <section v-if="project.musicalParts.length" id="song-transport" class="song-transport" aria-label="Song playback transport">
          <div>
            <strong>Song transport</strong>
            <small>Play assembled musical parts across the song timeline. This does not change the project.</small>
          </div>
          <p class="transport-position" aria-live="polite">{{ transportState.positionLabel }}</p>
          <button v-if="!transportState.playing" type="button" data-readiness-action="hear" :disabled="busy" @click="startTransport()">▶ Play song</button>
          <button v-else type="button" class="quiet" data-readiness-action="hear" @click="stopTransport('Playback stopped.')">■ Stop</button>
          <p v-if="transportState.message">{{ transportState.message }}</p>
        </section>
        <div v-if="project.sections.length" class="energy-curve" aria-label="Song energy curve">
          <article v-for="section in project.sections" :key="`energy-${section.id}`">
            <div class="energy-meter" :style="{ '--energy-level': energyValue(arrangementEnergy(section.id)) }" aria-hidden="true"><span /></div>
            <strong>{{ section.title }}</strong>
            <small>{{ arrangementFor(section.id) ? arrangementEnergy(section.id) : 'Not shaped yet' }}</small>
          </article>
        </div>
        <div v-if="project.sections.length" class="arrangement-sections">
          <article v-for="section in project.sections" :id="`arrangement-${section.id}`" :key="`arrangement-${section.id}`" class="arrangement-card">
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
                data-readiness-action="role"
                :class="{ selected: sectionHasRole(section.id, role.id) }"
                :aria-pressed="sectionHasRole(section.id, role.id)"
                :title="role.help"
                :disabled="busy"
                @click="setSectionRole(section.id, role.id, !sectionHasRole(section.id, role.id))">
                <span>{{ role.label }}</span>
                <small>{{ role.help }}</small>
              </button>
            </fieldset>
            <section v-if="assignedRolesForSection(section.id).length" class="instrument-role-matches" :aria-label="`Instrument matches for ${section.title}`">
              <div>
                <strong>Instruments that can do this job</strong>
                <small>{{ instrumentQualityFilter ? `${instrumentQualityLabel(instrumentQualityFilter)} feeling, catalog order, no winner chosen.` : 'Catalog order, no winner chosen. Filter by feeling above.' }}</small>
              </div>
              <article v-for="role in assignedRolesForSection(section.id)" :key="`${section.id}-${role.id}`">
                <strong>{{ role.label }}</strong>
                <p v-if="!recommendedInstrumentsForRole(role.id).length">{{ instrumentQualityFilter ? 'No catalog instrument covers this job with that feeling.' : 'No catalog instrument covers this job yet.' }}</p>
                <ul v-else>
                  <li v-for="instrument in recommendedInstrumentsForRole(role.id)" :key="`${section.id}-${role.id}-${instrument.id}`">
                    <span>{{ instrument.name }}</span>
                    <small>{{ rangeFitForInstrument(section.id, instrument) }}</small>
                  </li>
                </ul>
              </article>
            </section>
            <section v-if="assignedRolesForSection(section.id).length" class="musical-parts" :aria-label="`Musical parts for ${section.title}`">
              <div>
                <strong>Build musical parts</strong>
                <small>Explore a guided idea or connect approved notes yourself. Previewed ideas add nothing until you decide, and no instrument is chosen for you.</small>
              </div>
              <section v-if="sectionHasRole(section.id, 'Accent') && !hasPartForRole(section.id, 'Accent')" class="role-proposal">
                <div>
                  <strong>Explore accents</strong>
                  <small>Maskil Forge can mark bar downbeats with a short, strong hit on the highest approved note at that moment. Preview every note before deciding.</small>
                </div>
                <button type="button" class="secondary" data-readiness-action="part" :disabled="busy || !notesForSection(section.id).length" @click="prepareAccentProposal(section.id)">
                  {{ accentProposals[section.id] ? 'Refresh this idea' : 'Explore this idea' }}
                </button>
                <div v-if="accentProposals[section.id]" class="role-proposal-result">
                  <p><strong>{{ accentProposals[section.id].partLabel }}</strong><span>{{ accentProposals[section.id].events.length }} note{{ accentProposals[section.id].events.length === 1 ? '' : 's' }} · {{ accentProposals[section.id].reusedNoteCount }} already match this accent</span></p>
                  <ol>
                    <li v-for="(note, noteIndex) in accentProposals[section.id].events" :key="`${note.startTick}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.existingNoteEventId ? 'Uses your existing accent note' : 'Creates this accent note' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="useAccentProposal(section.id)">Use this idea</button>
                </div>
              </section>
              <section v-if="sectionHasRole(section.id, 'Countermelody') && !hasPartForRole(section.id, 'Countermelody')" class="role-proposal">
                <div>
                  <strong>Explore countermelody</strong>
                  <small>Maskil Forge can follow the second-highest approved note at moments where more than one note sounds, as a supporting response beneath the top line. Preview every note before deciding.</small>
                </div>
                <button type="button" class="secondary" data-readiness-action="part" :disabled="busy || !notesForSection(section.id).length" @click="prepareCountermelodyProposal(section.id)">
                  {{ countermelodyProposals[section.id] ? 'Refresh this idea' : 'Explore this idea' }}
                </button>
                <div v-if="countermelodyProposals[section.id]" class="role-proposal-result">
                  <p><strong>{{ countermelodyProposals[section.id].partLabel }}</strong><span>{{ countermelodyProposals[section.id].events.length }} note{{ countermelodyProposals[section.id].events.length === 1 ? '' : 's' }} · {{ countermelodyProposals[section.id].reusedNoteCount }} already match this response</span></p>
                  <ol>
                    <li v-for="(note, noteIndex) in countermelodyProposals[section.id].events" :key="`${note.startTick}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.existingNoteEventId ? 'Uses your existing response note' : 'Creates this response note' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="useCountermelodyProposal(section.id)">Use this idea</button>
                </div>
              </section>
              <section v-if="sectionHasRole(section.id, 'HookReinforcement') && !hasPartForRole(section.id, 'HookReinforcement')" class="role-proposal">
                <div>
                  <strong>Explore hook reinforcement</strong>
                  <small>Maskil Forge can emphasize the highest approved note at each musical moment with a clearer, beat-capped hit. Preview every note before deciding.</small>
                </div>
                <button type="button" class="secondary" data-readiness-action="part" :disabled="busy || !notesForSection(section.id).length" @click="prepareHookReinforcementProposal(section.id)">
                  {{ hookReinforcementProposals[section.id] ? 'Refresh this idea' : 'Explore this idea' }}
                </button>
                <div v-if="hookReinforcementProposals[section.id]" class="role-proposal-result">
                  <p><strong>{{ hookReinforcementProposals[section.id].partLabel }}</strong><span>{{ hookReinforcementProposals[section.id].events.length }} note{{ hookReinforcementProposals[section.id].events.length === 1 ? '' : 's' }} · {{ hookReinforcementProposals[section.id].reusedNoteCount }} already match this hook</span></p>
                  <ol>
                    <li v-for="(note, noteIndex) in hookReinforcementProposals[section.id].events" :key="`${note.startTick}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.existingNoteEventId ? 'Uses your existing hook note' : 'Creates this reinforced note' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="useHookReinforcementProposal(section.id)">Use this idea</button>
                </div>
              </section>
              <section v-if="sectionHasRole(section.id, 'Texture') && !hasPartForRole(section.id, 'Texture')" class="role-proposal">
                <div>
                  <strong>Explore texture</strong>
                  <small>Maskil Forge can keep the upper half of each approved chord voicing as softer sustained color. Registered voices stay authoritative; missing voices use temporary preview voicings. Preview every note before deciding.</small>
                </div>
                <button type="button" class="secondary" data-readiness-action="part" :disabled="busy || !section.harmony.length" @click="prepareTextureProposal(section.id)">
                  {{ textureProposals[section.id] ? 'Refresh this idea' : 'Explore this idea' }}
                </button>
                <div v-if="textureProposals[section.id]" class="role-proposal-result">
                  <p>
                    <strong>{{ textureProposals[section.id].partLabel }}</strong>
                    <span>{{ textureProposals[section.id].events.length }} note{{ textureProposals[section.id].events.length === 1 ? '' : 's' }} · {{ textureProposals[section.id].reusedNoteCount }} already match · {{ textureProposals[section.id].usesPreviewVoicings ? 'includes temporary preview voicings' : 'uses registered voicings' }}</span>
                  </p>
                  <ol>
                    <li v-for="(note, noteIndex) in textureProposals[section.id].events" :key="`${note.startTick}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.existingNoteEventId ? 'Uses your existing note' : note.usesPreviewVoicing ? 'Creates this temporary-preview note' : 'Creates this registered-voicing note' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="useTextureProposal(section.id)">Use this idea</button>
                </div>
              </section>
              <section v-if="sectionHasRole(section.id, 'Harmony') && !hasPartForRole(section.id, 'Harmony')" class="role-proposal">
                <div>
                  <strong>Explore harmony support</strong>
                  <small>Maskil Forge can turn this section’s approved chords and voicings into a harmony-support part. Registered voices stay authoritative; missing voices use temporary preview voicings. Preview every note before deciding.</small>
                </div>
                <button type="button" class="secondary" data-readiness-action="part" :disabled="busy || !section.harmony.length" @click="prepareHarmonySupportProposal(section.id)">
                  {{ harmonySupportProposals[section.id] ? 'Refresh this idea' : 'Explore this idea' }}
                </button>
                <div v-if="harmonySupportProposals[section.id]" class="role-proposal-result">
                  <p>
                    <strong>{{ harmonySupportProposals[section.id].partLabel }}</strong>
                    <span>{{ harmonySupportProposals[section.id].events.length }} note{{ harmonySupportProposals[section.id].events.length === 1 ? '' : 's' }} · {{ harmonySupportProposals[section.id].reusedNoteCount }} already match · {{ harmonySupportProposals[section.id].usesPreviewVoicings ? 'includes temporary preview voicings' : 'uses registered voicings' }}</span>
                  </p>
                  <ol>
                    <li v-for="(note, noteIndex) in harmonySupportProposals[section.id].events" :key="`${note.startTick}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.existingNoteEventId ? 'Uses your existing note' : note.usesPreviewVoicing ? 'Creates this temporary-preview note' : 'Creates this registered-voicing note' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="useHarmonySupportProposal(section.id)">Use this idea</button>
                </div>
              </section>
              <section v-if="sectionHasRole(section.id, 'Pulse') && !hasPartForRole(section.id, 'Pulse')" class="role-proposal">
                <div>
                  <strong>Explore pulse</strong>
                  <small>Maskil Forge can place a short mid-register hit on each approved onset so the section keeps a clear rhythmic motion. Preview every note before deciding.</small>
                </div>
                <button type="button" class="secondary" data-readiness-action="part" :disabled="busy || !notesForSection(section.id).length" @click="preparePulseProposal(section.id)">
                  {{ pulseProposals[section.id] ? 'Refresh this idea' : 'Explore this idea' }}
                </button>
                <div v-if="pulseProposals[section.id]" class="role-proposal-result">
                  <p><strong>{{ pulseProposals[section.id].partLabel }}</strong><span>{{ pulseProposals[section.id].events.length }} note{{ pulseProposals[section.id].events.length === 1 ? '' : 's' }} · {{ pulseProposals[section.id].reusedNoteCount }} already match this pulse</span></p>
                  <ol>
                    <li v-for="(note, noteIndex) in pulseProposals[section.id].events" :key="`${note.startTick}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.existingNoteEventId ? 'Uses your existing pulse note' : 'Creates this pulse note' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="usePulseProposal(section.id)">Use this idea</button>
                </div>
              </section>
              <section v-if="sectionHasRole(section.id, 'LowEndSupport') && !hasPartForRole(section.id, 'LowEndSupport')" class="role-proposal">
                <div>
                  <strong>Explore low-end support</strong>
                  <small>Maskil Forge can follow the lowest approved note at each musical moment and place it in a lower register. Preview every note before deciding.</small>
                </div>
                <button type="button" class="secondary" data-readiness-action="part" :disabled="busy || !notesForSection(section.id).length" @click="prepareLowEndSupportProposal(section.id)">
                  {{ lowEndSupportProposals[section.id] ? 'Refresh this idea' : 'Explore this idea' }}
                </button>
                <div v-if="lowEndSupportProposals[section.id]" class="role-proposal-result">
                  <p><strong>{{ lowEndSupportProposals[section.id].partLabel }}</strong><span>{{ lowEndSupportProposals[section.id].events.length }} note{{ lowEndSupportProposals[section.id].events.length === 1 ? '' : 's' }} · {{ lowEndSupportProposals[section.id].reusedNoteCount }} already low enough to reuse</span></p>
                  <ol>
                    <li v-for="(note, noteIndex) in lowEndSupportProposals[section.id].events" :key="`${note.startTick}:${noteIndex}`">
                      <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                      <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                      <small>{{ note.existingNoteEventId ? 'Uses your existing low note' : 'Creates this lower note' }}</small>
                    </li>
                  </ol>
                  <button type="button" :disabled="busy" @click="useLowEndSupportProposal(section.id)">Use this idea</button>
                </div>
              </section>
              <form v-if="notesForSection(section.id).length" class="musical-part-form" @submit.prevent="addMusicalPart(section.id, $event)">
                <label>Part name<input name="label" data-readiness-action="part" maxlength="100" placeholder="Chorus foundation" required :disabled="busy" /></label>
                <label>Musical job<select name="role" required :disabled="busy"><option v-for="role in assignedRolesForSection(section.id)" :key="role.id" :value="role.id">{{ role.label }}</option></select></label>
                <label>Catalog instrument
                  <select name="instrumentProfileId" :disabled="busy || !instrumentProfiles">
                    <option value="">Not assigned</option>
                    <option v-for="instrument in instrumentProfiles?.instruments ?? []" :key="instrument.id" :value="instrument.id">{{ instrument.name }}</option>
                  </select>
                </label>
                <fieldset>
                  <legend>Which approved notes belong to this part?</legend>
                  <label v-for="note in notesForSection(section.id)" :key="note.id" class="musical-part-note">
                    <input name="noteEventIds" type="checkbox" :value="note.id" :disabled="busy" />
                    <span>{{ formatRegisteredPitch(note.pitch) }}</span>
                    <small>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</small>
                  </label>
                </fieldset>
                <p class="arrangement-prompt">Any catalog instrument can be named, including ones that do not cover this job. Recommendations above do not assign one. This does not retarget notes or change MIDI.</p>
                <button type="submit" :disabled="busy">Create musical part</button>
              </form>
              <p v-else class="arrangement-prompt">Approve a harmony note sketch first. Then you can explain which musical job those notes perform.</p>
              <ol v-if="partsForSection(section.id).length" class="musical-part-list">
                <li v-for="part in partsForSection(section.id)" :key="part.id">
                  <form class="musical-part-edit-form" @submit.prevent="setMusicalPart(part.id, $event)">
                    <label>Part name<input name="label" maxlength="100" :value="part.label" required :disabled="busy" /></label>
                    <label>Catalog instrument
                      <select name="instrumentProfileId" :value="part.instrumentProfileId ?? ''" :disabled="busy || !instrumentProfiles">
                        <option value="">Not assigned</option>
                        <option v-for="instrument in instrumentProfiles?.instruments ?? []" :key="instrument.id" :value="instrument.id">{{ instrument.name }}</option>
                      </select>
                    </label>
                    <small>{{ arrangementRoles.find(item => item.id === part.role)?.label ?? part.role }} · {{ instrumentProfileName(part.instrumentProfileId) }} · choose at least one note</small>
                    <fieldset>
                      <legend>Approved notes in this part</legend>
                      <label v-for="note in notesForSection(section.id)" :key="`${part.id}-${note.id}`" class="musical-part-note">
                        <input name="noteEventIds" type="checkbox" :value="note.id" :checked="part.noteEventIds.includes(note.id)" :disabled="busy" />
                        <span>{{ formatRegisteredPitch(note.pitch) }}</span>
                        <small>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</small>
                      </label>
                    </fieldset>
                    <button type="submit" :disabled="busy">Save part</button>
                  </form>
                  <button type="button" class="danger" :disabled="busy" @click="removeMusicalPart(part.id, part.label)">Remove part</button>
                </li>
              </ol>
              <section v-if="partsForSection(section.id).length" class="part-audition" :aria-label="`Hear assembled parts for ${section.title}`">
                <div>
                  <strong>Hear assembled parts</strong>
                  <small>Play the notes already connected to musical parts in this section. This preview does not change the song.</small>
                </div>
                <button v-if="partAuditionState.sectionId !== section.id" type="button" :disabled="busy" @click="hearAssembledParts(section.id)">▶ Hear assembled parts</button>
                <button v-else type="button" class="quiet" @click="stopPartAudition('Playback stopped.')">■ Stop</button>
                <p v-if="partAuditionState.messageSectionId === section.id && partAuditionState.message">{{ partAuditionState.message }}</p>
              </section>
            </section>
          </article>
        </div>
        <p v-else class="arrangement-empty">Add a Verse, Chorus, or another section above. Then you can describe how its energy should grow and what musical jobs it needs.</p>
        <p class="arrangement-boundary">Musical parts connect your approved notes to an arrangement purpose. Naming a catalog instrument is optional and reversible. It does not retarget notes, persist a cello or guitar sketch, or emit MIDI program changes.</p>
      </section>

      <section v-if="!phoneCaptureMode" class="midi-export-panel" aria-labelledby="midi-export-title">
        <div>
          <span class="eyebrow">Take your sketch with you</span>
          <h2 id="midi-export-title">Export playable notes</h2>
          <p v-if="project.noteEvents.length">Your {{ project.noteEvents.length }} approved playable note{{ project.noteEvents.length === 1 ? '' : 's' }} can be opened in another music application. Timing and dynamics are preserved. Named catalog parts export on inspectable MIDI channels. Drum kit stays on channel 10 as Acoustic Bass Drum. Unassigned notes stay on channel 1. MIDI does not emit a program change.</p>
          <p v-else>Your song does not contain playable notes yet. Create and approve a harmony sketch, a pitch-gesture sketch, an onset-gesture sketch, or a loudness-gesture sketch first.</p>
        </div>
        <button type="button" :disabled="busy || !project.noteEvents.length" @click="exportMidi">Export MIDI</button>
      </section>

      <section v-if="!phoneCaptureMode" id="vocal-take-studio" class="vocal-take-studio" aria-labelledby="vocal-take-studio-title">
        <div>
          <span class="eyebrow">Original performance</span>
          <h2 id="vocal-take-studio-title">Saved rough takes</h2>
          <p>Play, analyze, review, promote, and place takes here on the studio screen. Recording still requires a saved song revision. Placement is song time, not a clip on the section timeline. Pitch, onset, and loudness gestures become notes only after you preview and accept the sketches below.</p>
        </div>
        <section class="microphone-preflight" aria-labelledby="desktop-microphone-preflight-title">
          <div>
            <h3 id="desktop-microphone-preflight-title">Record a rough vocal take</h3>
            <p>Recording starts only when you ask. The take stays temporary in this tab until you listen and choose Save take.</p>
          </div>
          <p v-if="!roughVocalSupport.supported" class="microphone-preflight-status unavailable" role="status">{{ roughVocalSupport.reason }}</p>
          <p v-else-if="microphonePreflightState === 'ready'" class="microphone-preflight-status ready" role="status"><strong>{{ microphonePreflightLabel }}</strong>{{ microphonePreflightMessage }}</p>
          <p v-else-if="microphonePreflightMessage" class="microphone-preflight-status" :class="{ unavailable: microphonePreflightState === 'failed' }" role="status">{{ microphonePreflightMessage }}</p>
          <p v-if="isDirty" class="microphone-preflight-status unavailable" role="status">Save the current words and structure before attaching a recording to this version.</p>
          <p v-if="roughVocalCaptureMessage" class="microphone-preflight-status" :class="{ ready: roughVocalCaptureState === 'saved' || roughVocalCaptureState === 'review', unavailable: roughVocalCaptureState === 'failed' }" role="status">{{ roughVocalCaptureMessage }}</p>
          <div class="rough-vocal-actions">
            <button
              type="button"
              class="secondary"
              :disabled="!roughVocalSupport.supported || microphonePreflightState === 'checking' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'requesting' || roughVocalCaptureState === 'saving'"
              @click="checkRoughVocalMicrophone">
              {{ microphonePreflightState === 'checking' ? 'Checking microphone…' : microphonePreflightState === 'ready' ? 'Check again' : 'Check microphone' }}
            </button>
            <button
              v-if="roughVocalCaptureState !== 'recording'"
              type="button"
              :disabled="!roughVocalSupport.supported || busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'requesting' || roughVocalCaptureState === 'saving'"
              @click="startRoughVocalRecording">
              {{ roughVocalCaptureState === 'requesting' ? 'Opening microphone…' : project.assets.length ? 'Record another take' : 'Record rough take' }}
            </button>
            <button v-else type="button" class="danger recording-stop" @click="stopRoughVocalRecording(false)">Stop recording</button>
          </div>
          <section v-if="pendingRoughVocal" class="rough-vocal-review" aria-labelledby="desktop-rough-vocal-review-title">
            <div>
              <p class="eyebrow">Temporary take</p>
              <h4 id="desktop-rough-vocal-review-title">Listen before saving</h4>
              <p>{{ formatRoughVocalDuration(pendingRoughVocal.durationMs) }} · {{ formatRoughVocalBytes(pendingRoughVocal.blob.size) }}</p>
            </div>
            <audio controls preload="metadata" :src="pendingRoughVocal.url" @play="logRoughVocalPlayback('temporary')">Your browser cannot play this temporary recording.</audio>
            <div class="rough-vocal-actions">
              <button type="button" :disabled="roughVocalCaptureState === 'saving' || isDirty" @click="savePendingRoughVocal">{{ roughVocalCaptureState === 'saving' ? 'Saving take…' : 'Save take' }}</button>
              <button type="button" class="danger" :disabled="roughVocalCaptureState === 'saving'" @click="discardPendingRoughVocal(true)">Discard take</button>
            </div>
          </section>
        </section>
        <p v-if="!project.assets.length" class="note-event-empty">No saved takes yet. Record one here, or capture it in phone Review. Immutable audio, analyzer evidence, and artist gestures travel with the song.</p>
        <section v-else class="saved-vocal-takes" aria-labelledby="desktop-saved-vocal-takes-title">
          <h4 id="desktop-saved-vocal-takes-title">Takes on this song</h4>
          <ol>
            <li v-for="(asset, index) in project.assets" :key="`desktop-${asset.id}`">
              <div><strong>{{ asset.name }}</strong><small>{{ new Date(asset.createdUtc).toLocaleString() }} · {{ formatRoughVocalBytes(asset.byteLength) }}</small></div>
              <audio controls preload="none" :src="projectsApi.originalVocalTakeUrl(project.id, asset.id)" @play="logRoughVocalPlayback('saved', asset.id)">Your browser cannot play this saved take.</audio>
              <form class="vocal-take-placement" @submit.prevent="setVocalTakePlacement(asset.id, $event)">
                <p>{{ vocalTakePlacementLabel(asset.id) }}. Changing this start does not move notes you already accepted.</p>
                <label>Bar<input name="bar" type="number" min="1" :value="vocalTakePlacement(asset.id)?.start.bar ?? 1" required :disabled="busy" :aria-label="`${asset.name} start bar`"></label>
                <label>Beat<input name="beat" type="number" min="1" :value="vocalTakePlacement(asset.id)?.start.beat ?? 1" required :disabled="busy" :aria-label="`${asset.name} start beat`"></label>
                <label>Tick<input name="tick" type="number" min="0" :value="vocalTakePlacement(asset.id)?.start.tick ?? 0" required :disabled="busy" :aria-label="`${asset.name} start tick`"></label>
                <button type="submit" :disabled="busy">{{ vocalTakePlacement(asset.id) ? 'Update placement' : 'Place take' }}</button>
                <button v-if="vocalTakePlacement(asset.id)" type="button" class="quiet" :disabled="busy" @click="clearVocalTakePlacement(asset.id)">Clear placement</button>
              </form>
              <p v-if="loudnessObservationSummary(asset.id)" class="performance-observation-summary">{{ loudnessObservationSummary(asset.id) }}</p>
              <p v-if="pitchObservationSummary(asset.id)" class="performance-observation-summary pitch">{{ pitchObservationSummary(asset.id) }}</p>
              <p v-if="onsetObservationSummary(asset.id)" class="performance-observation-summary onset">{{ onsetObservationSummary(asset.id) }}</p>
              <details v-if="performanceEvidenceCount(asset.id)" class="performance-evidence-inspector">
                <summary>
                  <span>Inspect analyzer evidence</span>
                  <small>{{ performanceEvidenceCount(asset.id) }} claim{{ performanceEvidenceCount(asset.id) === 1 ? '' : 's' }} · artist review</small>
                </summary>
                <div class="performance-evidence-body">
                    <p>These measurements remain analyzer evidence. Marking a claim inaccurate lets you store a separate correction; the original analyzer values stay unchanged. Accurate claims, or inaccurate claims with a stored correction, can be promoted into an artist gesture snapshot. Use the pitch, onset, or loudness sketches below to turn promoted gestures into notes.</p>
                  <section v-for="group in performanceEvidenceGroups(asset.id)" :key="`desktop-${group.key}`" class="performance-evidence-group">
                    <header>
                      <h5>{{ group.label }}</h5>
                      <span>{{ group.count }}</span>
                    </header>
                    <p class="performance-evidence-provenance"><code>{{ group.analyzerId }}</code> · v{{ group.analyzerVersion }} · {{ group.provenanceLabel }} · {{ new Date(group.createdUtc).toLocaleString() }}</p>
                    <ol>
                      <li v-for="row in group.rows" :key="`desktop-${row.id}`">
                        <time>{{ row.timeLabel }}</time>
                        <span>{{ row.measurementLabel }}</span>
                        <small>{{ row.confidenceLabel }}</small>
                        <div class="performance-evidence-review" :data-verdict="row.reviewVerdict ?? 'Unreviewed'">
                          <strong>{{ row.reviewVerdict ? `Artist marked ${row.reviewVerdict.toLowerCase()}` : 'Unreviewed' }}</strong>
                          <button type="button" class="quiet" :aria-pressed="row.reviewVerdict === 'Accurate'" :disabled="busy || isDirty || workspaceConnection !== 'ready' || row.reviewVerdict === 'Accurate'" @click="reviewPerformanceObservation(asset.id, row.id, 'Accurate')">Accurate</button>
                          <button type="button" class="quiet" :aria-pressed="row.reviewVerdict === 'Inaccurate'" :disabled="busy || isDirty || workspaceConnection !== 'ready' || row.reviewVerdict === 'Inaccurate'" @click="reviewPerformanceObservation(asset.id, row.id, 'Inaccurate')">Inaccurate</button>
                          <button v-if="row.reviewVerdict" type="button" class="quiet" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="reviewPerformanceObservation(asset.id, row.id, null)">Clear</button>
                        </div>
                        <form v-if="row.reviewVerdict === 'Inaccurate'" class="performance-evidence-correction" @submit.prevent="savePerformanceObservationCorrection(asset.id, row)">
                          <p>{{ row.correctionLabel || 'Record a separate correction. Analyzer values stay unchanged.' }}</p>
                          <div class="performance-evidence-correction-fields">
                            <label v-for="field in row.correctionFields" :key="`desktop-${row.id}-${field.name}`" class="performance-evidence-correction-field">
                              <span>{{ field.label }}</span>
                              <input
                                type="number"
                                inputmode="decimal"
                                :min="field.min || undefined"
                                :max="field.max || undefined"
                                :step="field.step"
                                :value="correctionDraftValue(row.id, field)"
                                :disabled="busy || isDirty || workspaceConnection !== 'ready'"
                                :aria-label="field.label"
                                @input="setCorrectionDraft(row.id, field.name, ($event.target as HTMLInputElement).value)"
                              >
                            </label>
                          </div>
                          <div class="performance-evidence-correction-actions">
                            <button type="submit" :disabled="busy || isDirty || workspaceConnection !== 'ready'">{{ row.hasCorrection ? 'Update correction' : 'Save correction' }}</button>
                            <button v-if="row.hasCorrection" type="button" class="quiet" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="clearPerformanceObservationCorrection(asset.id, row.id)">Remove correction</button>
                          </div>
                        </form>
                        <div v-if="row.canPromote || row.hasGesture" class="performance-evidence-gesture">
                          <p>{{ row.gestureLabel || 'Promote the approved measurements into an artist gesture. Notes are added only after you accept a pitch, onset, or loudness sketch.' }}</p>
                          <div class="performance-evidence-gesture-actions">
                            <button v-if="row.canPromote" type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="setPerformanceObservationGesture(asset.id, row.id, true)">{{ row.hasGesture ? 'Update gesture' : 'Promote gesture' }}</button>
                            <button v-if="row.hasGesture" type="button" class="quiet" :disabled="busy || isDirty || workspaceConnection !== 'ready'" @click="setPerformanceObservationGesture(asset.id, row.id, null)">Remove gesture</button>
                          </div>
                        </div>
                      </li>
                    </ol>
                    <button v-if="group.remainingCount" type="button" class="quiet performance-evidence-more" @click="showMorePerformanceEvidence(asset.id, group.key, group.count)">Show {{ Math.min(12, group.remainingCount) }} more · {{ group.remainingCount }} remaining</button>
                  </section>
                </div>
              </details>
              <p v-if="loudnessAnalysisMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ loudnessAnalysisMessages[asset.id] }}</p>
              <p v-if="pitchAnalysisMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ pitchAnalysisMessages[asset.id] }}</p>
              <p v-if="onsetAnalysisMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ onsetAnalysisMessages[asset.id] }}</p>
              <p v-if="performanceReviewMessages[asset.id]" class="saved-vocal-analysis-status" role="status">{{ performanceReviewMessages[asset.id] }}</p>
              <div class="saved-vocal-take-actions">
                <button type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="analyzeSavedRoughVocal(asset)">{{ loudnessAnalysisAssetId === asset.id ? 'Analyzing loudness…' : loudnessObservationSummary(asset.id) ? 'Reanalyze loudness' : 'Analyze loudness' }}</button>
                <button type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="analyzeSavedRoughVocalPitch(asset)">{{ pitchAnalysisAssetId === asset.id ? 'Analyzing pitch…' : pitchObservationSummary(asset.id) ? 'Reanalyze pitch' : 'Analyze pitch' }}</button>
                <button type="button" :disabled="busy || isDirty || workspaceConnection !== 'ready' || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="analyzeSavedRoughVocalOnsets(asset)">{{ onsetAnalysisAssetId === asset.id ? 'Analyzing onsets…' : onsetObservationSummary(asset.id) ? 'Reanalyze onsets' : 'Analyze onsets' }}</button>
                <button type="button" class="secondary" :disabled="busy || isDirty || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="requestRenameSavedRoughVocal(asset)">Rename</button>
                <button type="button" class="danger" :disabled="busy || isDirty || roughVocalCaptureState === 'recording' || roughVocalCaptureState === 'saving'" @click="requestRemoveSavedRoughVocal(asset, index + 1)">Remove take</button>
              </div>
            </li>
          </ol>
        </section>
      </section>

      <section v-if="!phoneCaptureMode" id="pitch-gesture-notes" class="pitch-gesture-notes" aria-labelledby="pitch-gesture-notes-title">
        <div>
          <span class="eyebrow">From a reviewed take</span>
          <h2 id="pitch-gesture-notes-title">Sketch notes from pitch gestures</h2>
          <p>Preview notes from approved pitch gestures on one saved take. Timing uses the take’s song placement plus take-relative milliseconds at the first tempo; an unplaced take still starts at tick 0. Onset and loudness gestures have their own sketches below. Nothing is added until you choose “Use this sketch.”</p>
        </div>
        <p v-if="!project.assets.length" class="note-event-empty">Record a rough take above and promote a pitch claim first.</p>
        <p v-else-if="!pitchGestureTakes.length" class="note-event-empty">Promote at least one pitch claim to a gesture in the take inspector above.</p>
        <article v-for="asset in pitchGestureTakes" :key="asset.id" class="harmony-note-sketch" :aria-label="`Pitch-gesture note sketch for ${asset.name}`">
          <div>
            <strong>{{ asset.name }}</strong>
            <small>{{ pitchGestureCountForAsset(asset.id) }} pitch gesture{{ pitchGestureCountForAsset(asset.id) === 1 ? '' : 's' }} · {{ vocalTakePlacementLabel(asset.id) }}</small>
          </div>
          <button type="button" class="secondary" :disabled="busy" @click="preparePitchGestureNoteSketch(asset.id)">
            {{ pitchGestureNoteSketches[asset.id] ? 'Refresh note sketch' : 'Prepare note sketch' }}
          </button>
          <div v-if="pitchGestureNoteSketches[asset.id]" class="harmony-note-sketch-result">
            <p>
              <strong>{{ pitchGestureNoteSketches[asset.id].events.length }} notes ready to review</strong>
              <span>Uses the first tempo only. Existing notes stay until you accept, and changing placement later does not move accepted notes.</span>
            </p>
            <ol>
              <li v-for="(note, noteIndex) in pitchGestureNoteSketches[asset.id].events" :key="`${note.gestureId}:${note.startTick}:${noteIndex}`">
                <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                <small>velocity {{ note.velocity }}</small>
              </li>
            </ol>
            <button type="button" :disabled="busy" @click="usePitchGestureNoteSketch(asset.id)">Use this sketch</button>
          </div>
        </article>
      </section>

      <section v-if="!phoneCaptureMode" id="onset-gesture-notes" class="onset-gesture-notes" aria-labelledby="onset-gesture-notes-title">
        <div>
          <span class="eyebrow">From a reviewed take</span>
          <h2 id="onset-gesture-notes-title">Sketch notes from onset gestures</h2>
          <p>Preview short C4 hits from approved onset gestures on one saved take. Timing uses the take’s song placement plus take-relative milliseconds at the first tempo; an unplaced take still starts at tick 0. Strength becomes velocity. Pitch and loudness gestures stay unused here. Nothing is added until you choose “Use this sketch.”</p>
        </div>
        <p v-if="!project.assets.length" class="note-event-empty">Record a rough take above and promote an onset claim first.</p>
        <p v-else-if="!onsetGestureTakes.length" class="note-event-empty">Promote at least one onset claim to a gesture in the take inspector above.</p>
        <article v-for="asset in onsetGestureTakes" :key="asset.id" class="harmony-note-sketch" :aria-label="`Onset-gesture note sketch for ${asset.name}`">
          <div>
            <strong>{{ asset.name }}</strong>
            <small>{{ onsetGestureCountForAsset(asset.id) }} onset gesture{{ onsetGestureCountForAsset(asset.id) === 1 ? '' : 's' }} · {{ vocalTakePlacementLabel(asset.id) }}</small>
          </div>
          <button type="button" class="secondary" :disabled="busy" @click="prepareOnsetGestureNoteSketch(asset.id)">
            {{ onsetGestureNoteSketches[asset.id] ? 'Refresh note sketch' : 'Prepare note sketch' }}
          </button>
          <div v-if="onsetGestureNoteSketches[asset.id]" class="harmony-note-sketch-result">
            <p>
              <strong>{{ onsetGestureNoteSketches[asset.id].events.length }} notes ready to review</strong>
              <span>Uses the first tempo only. Existing notes stay until you accept, and changing placement later does not move accepted notes.</span>
            </p>
            <ol>
              <li v-for="(note, noteIndex) in onsetGestureNoteSketches[asset.id].events" :key="`${note.gestureId}:${note.startTick}:${noteIndex}`">
                <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                <small>velocity {{ note.velocity }}</small>
              </li>
            </ol>
            <button type="button" :disabled="busy" @click="useOnsetGestureNoteSketch(asset.id)">Use this sketch</button>
          </div>
        </article>
      </section>

      <section v-if="!phoneCaptureMode" id="loudness-gesture-notes" class="loudness-gesture-notes" aria-labelledby="loudness-gesture-notes-title">
        <div>
          <span class="eyebrow">From a reviewed take</span>
          <h2 id="loudness-gesture-notes-title">Sketch notes from loudness gestures</h2>
          <p>Preview short C4 hits from approved loudness gestures on one saved take. Timing uses the take’s song placement plus take-relative milliseconds at the first tempo; an unplaced take still starts at tick 0. RMS between −60 and 0 dBFS becomes velocity; quieter frames stay at velocity 1. Peak stays unused. Nothing is added until you choose “Use this sketch.”</p>
        </div>
        <p v-if="!project.assets.length" class="note-event-empty">Record a rough take above and promote a loudness claim first.</p>
        <p v-else-if="!loudnessGestureTakes.length" class="note-event-empty">Promote at least one loudness claim to a gesture in the take inspector above.</p>
        <article v-for="asset in loudnessGestureTakes" :key="asset.id" class="harmony-note-sketch" :aria-label="`Loudness-gesture note sketch for ${asset.name}`">
          <div>
            <strong>{{ asset.name }}</strong>
            <small>{{ loudnessGestureCountForAsset(asset.id) }} loudness gesture{{ loudnessGestureCountForAsset(asset.id) === 1 ? '' : 's' }} · {{ vocalTakePlacementLabel(asset.id) }}</small>
          </div>
          <button type="button" class="secondary" :disabled="busy" @click="prepareLoudnessGestureNoteSketch(asset.id)">
            {{ loudnessGestureNoteSketches[asset.id] ? 'Refresh note sketch' : 'Prepare note sketch' }}
          </button>
          <div v-if="loudnessGestureNoteSketches[asset.id]" class="harmony-note-sketch-result">
            <p>
              <strong>{{ loudnessGestureNoteSketches[asset.id].events.length }} notes ready to review</strong>
              <span>Uses the first tempo only. Existing notes stay until you accept, and changing placement later does not move accepted notes.</span>
            </p>
            <ol>
              <li v-for="(note, noteIndex) in loudnessGestureNoteSketches[asset.id].events" :key="`${note.gestureId}:${note.startTick}:${noteIndex}`">
                <strong>{{ formatRegisteredPitch(note.pitch) }}</strong>
                <span>tick {{ note.startTick }} · {{ note.durationTicks }} ticks</span>
                <small>velocity {{ note.velocity }}</small>
              </li>
            </ol>
            <button type="button" :disabled="busy" @click="useLoudnessGestureNoteSketch(asset.id)">Use this sketch</button>
          </div>
        </article>
      </section>

      <section v-if="!phoneCaptureMode" id="loudness-expression-curves" class="loudness-expression-curves" aria-labelledby="loudness-expression-curves-title">
        <div>
          <span class="eyebrow">From a reviewed take</span>
          <h2 id="loudness-expression-curves-title">Sketch a dynamics curve from loudness gestures</h2>
          <p>Preview a dynamics curve from approved loudness gestures on one saved take. Timing uses the take’s song placement plus take-relative milliseconds at the first tempo; an unplaced take still starts at tick 0. RMS between −60 and 0 dBFS becomes MIDI expression 0–127; quieter frames stay at 0. Peak stays unused. MIDI export can send these points as CC 11 when the song also has playable notes. Nothing is stored until you choose “Use this sketch.” Changing placement later does not move accepted points, and removing the take does not drop the curve.</p>
        </div>
        <p v-if="!project.assets.length" class="note-event-empty">Record a rough take above and promote a loudness claim first.</p>
        <p v-else-if="!loudnessGestureTakes.length" class="note-event-empty">Promote at least one loudness claim to a gesture in the take inspector above.</p>
        <article v-for="asset in loudnessGestureTakes" :key="asset.id" class="harmony-note-sketch" :aria-label="`Loudness-gesture expression sketch for ${asset.name}`">
          <div>
            <strong>{{ asset.name }}</strong>
            <small>{{ loudnessGestureCountForAsset(asset.id) }} loudness gesture{{ loudnessGestureCountForAsset(asset.id) === 1 ? '' : 's' }} · {{ vocalTakePlacementLabel(asset.id) }}</small>
          </div>
          <button type="button" class="secondary" :disabled="busy" @click="prepareLoudnessGestureExpressionSketch(asset.id)">
            {{ loudnessGestureExpressionSketches[asset.id] ? 'Refresh expression sketch' : 'Prepare expression sketch' }}
          </button>
          <div v-if="loudnessGestureExpressionSketches[asset.id]" class="harmony-note-sketch-result">
            <p>
              <strong>{{ loudnessGestureExpressionSketches[asset.id].points.length }} dynamics points ready to review</strong>
              <span>{{ loudnessGestureExpressionSketches[asset.id].name }} · MIDI CC 11. Uses the first tempo only. Existing curves stay until you accept, and changing placement later does not move accepted points.</span>
            </p>
            <ol>
              <li v-for="(point, pointIndex) in loudnessGestureExpressionSketches[asset.id].points" :key="`${point.tick}:${point.value}:${pointIndex}`">
                <strong>tick {{ point.tick }}</strong>
                <span>expression {{ point.value }}</span>
              </li>
            </ol>
            <button type="button" :disabled="busy" @click="useLoudnessGestureExpressionSketch(asset.id)">Use this sketch</button>
          </div>
        </article>
        <ol v-if="project.expressionCurves.length" class="note-event-list">
          <li v-for="curve in project.expressionCurves" :key="curve.id">
            <div>
              <strong>{{ curve.name }}</strong>
              <span>{{ curve.points.length }} point{{ curve.points.length === 1 ? '' : 's' }} · MIDI CC 11{{ curve.instrumentProfileId ? ` · ${instrumentProfileName(curve.instrumentProfileId)}` : '' }}</span>
            </div>
            <button type="button" class="danger" :disabled="busy" @click="removeExpressionCurve(curve.id, curve.name)">Remove</button>
          </li>
        </ol>
      </section>

      <section v-if="!phoneCaptureMode" id="instrument-performance-retarget" class="instrument-performance-retarget" aria-labelledby="instrument-performance-retarget-title">
        <div>
          <span class="eyebrow">From a reviewed take</span>
          <h2 id="instrument-performance-retarget-title">Retarget this take across the catalog</h2>
          <p>Preview the same approved swell, slide, or onset on every catalog instrument, then store what applies onto a musical part that already names that instrument. Loudness gestures become swells; pitch gestures become slides only where the catalog map allows; onset gestures become kit hits. Piano, bass, flute, clarinet, trumpet, and synth pad do not take slides; drum kit does not take swell or slide; pitched instruments do not take kit hits. Violin swell is bow expression; flute swell is breath; clarinet and trumpet swells are legato. Synth pad swell is pad; synth lead swell is filter and synth lead slide is portamento; electric guitar swell is distortion and electric guitar slide is bend. Kit hits use General MIDI Acoustic Bass Drum (C2) instead of a melodic C4; the host does not choose snare or hat. Timing uses the take’s song placement plus take-relative milliseconds at the first tempo. Out-of-range slide pitches are skipped, not transposed. MIDI does not choose an instrument or emit a program change. Named catalog parts export on inspectable MIDI channels; drum kit stays on channel 10; unassigned notes stay on channel 1.</p>
        </div>
        <p v-if="!project.assets.length" class="note-event-empty">Record a rough take above and promote a pitch, loudness, or onset claim first.</p>
        <p v-else-if="!instrumentRetargetTakes.length" class="note-event-empty">Promote at least one pitch, loudness, or onset claim to a gesture in the take inspector above.</p>
        <article v-for="asset in instrumentRetargetTakes" :key="asset.id" class="harmony-note-sketch" :aria-label="`Catalog instrument retarget for ${asset.name}`">
          <div>
            <strong>{{ asset.name }}</strong>
            <small>{{ pitchGestureCountForAsset(asset.id) }} pitch · {{ loudnessGestureCountForAsset(asset.id) }} loudness · {{ onsetGestureCountForAsset(asset.id) }} onset · {{ vocalTakePlacementLabel(asset.id) }}</small>
          </div>
          <button type="button" class="secondary" :disabled="busy" @click="prepareInstrumentPerformanceSketch(asset.id)">
            {{ instrumentPerformanceSketches[asset.id] ? 'Refresh catalog retarget' : 'Prepare catalog retarget' }}
          </button>
          <div v-if="instrumentPerformanceSketches[asset.id]" class="harmony-note-sketch-result">
            <p>
              <strong>Review, then store onto a named catalog part.</strong>
              <span>Uses the first tempo only. Each instrument keeps its own catalog technique. Piano, bass, flute, clarinet, and trumpet slides stay unused. Drum-kit swell and slide stay unused. Pitched instruments do not take kit hits. Kit hits use Acoustic Bass Drum at C2. MIDI emits dynamics as CC 11 on the instrument’s channel without a program change. Named catalog parts use inspectable MIDI channels; drum kit stays on channel 10; unassigned notes stay on channel 1.</span>
            </p>
            <div class="instrument-retarget-targets">
              <article v-for="target in instrumentPerformanceSketches[asset.id].targets" :key="target.instrumentId" class="instrument-retarget-target" :aria-label="`${target.instrumentName} retarget`">
                <strong>{{ target.instrumentName }}</strong>
                <section>
                  <p>
                    <strong>{{ gesturePerformanceCopy(target.swell) }}</strong>
                    <span>{{ target.swell.events.length }} swell event{{ target.swell.events.length === 1 ? '' : 's' }}</span>
                  </p>
                  <ol v-if="target.swell.events.length">
                    <li v-for="(event, eventIndex) in target.swell.events" :key="`${event.gestureId}:swell:${eventIndex}`">
                      <strong>tick {{ event.startTick }}</strong>
                      <span>{{ event.durationTicks }} ticks · expression {{ event.value }}</span>
                    </li>
                  </ol>
                </section>
                <section>
                  <p>
                    <strong>{{ gesturePerformanceCopy(target.slide) }}</strong>
                    <span>{{ target.slide.events.length }} slide event{{ target.slide.events.length === 1 ? '' : 's' }}</span>
                  </p>
                  <ol v-if="target.slide.events.length">
                    <li v-for="(event, eventIndex) in target.slide.events" :key="`${event.gestureId}:slide:${eventIndex}`">
                      <strong>{{ event.pitch ? formatRegisteredPitch(event.pitch) : 'Unpitched' }}</strong>
                      <span>tick {{ event.startTick }} · {{ event.durationTicks }} ticks</span>
                      <small>{{ slideRangeCopy(event.rangeKind) }}</small>
                    </li>
                    </ol>
                </section>
                <section>
                  <p>
                    <strong>{{ gesturePerformanceCopy(target.hit) }}</strong>
                    <span>{{ target.hit.events.length }} hit event{{ target.hit.events.length === 1 ? '' : 's' }}</span>
                  </p>
                  <ol v-if="target.hit.events.length">
                    <li v-for="(event, eventIndex) in target.hit.events" :key="`${event.gestureId}:hit:${eventIndex}`">
                      <strong>{{ event.pitch ? formatRegisteredPitch(event.pitch) : 'Unpitched' }}</strong>
                      <span>tick {{ event.startTick }} · {{ event.durationTicks }} ticks · velocity {{ event.value }}</span>
                    </li>
                  </ol>
                </section>
                <div class="instrument-retarget-accept">
                  <p v-if="!matchingInstrumentParts(target.instrumentId).length">
                    Name {{ target.instrumentName }} on a musical part in Arrangement first.
                  </p>
                  <template v-else>
                    <label v-if="matchingInstrumentParts(target.instrumentId).length > 1">
                      Musical part
                      <select
                        :value="selectedInstrumentSketchPartId(asset.id, target.instrumentId)"
                        :disabled="busy"
                        @change="instrumentSketchPartIds[instrumentSketchPartKey(asset.id, target.instrumentId)] = ($event.target as HTMLSelectElement).value"
                      >
                        <option value="">Choose a part</option>
                        <option v-for="part in matchingInstrumentParts(target.instrumentId)" :key="part.id" :value="part.id">
                          {{ part.label }}
                        </option>
                      </select>
                    </label>
                    <p v-else>Stores onto {{ matchingInstrumentParts(target.instrumentId)[0].label }}.</p>
                    <button
                      v-if="instrumentSketchHasPersistableEvents(target)"
                      type="button"
                      :disabled="busy || !selectedInstrumentSketchPartId(asset.id, target.instrumentId)"
                      @click="useInstrumentPerformanceSketch(asset.id, target.instrumentId)"
                    >
                      Use this {{ target.instrumentName }} sketch
                    </button>
                    <small>{{ instrumentSketchAcceptCopy(target) }}</small>
                  </template>
                </div>
              </article>
            </div>
          </div>
        </article>
      </section>

      <details v-if="!phoneCaptureMode" class="disclosure-panel note-event-foundation">
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
              <form class="note-event-form note-event-edit-form" @submit.prevent="setNoteEvent(note.id, $event)">
                <label>Pitch<input name="pitch" required :value="formatRegisteredPitch(note.pitch)" :disabled="busy" /></label>
                <label>Start tick<input name="startTick" type="number" min="0" :value="note.startTick" required :disabled="busy" /></label>
                <label>Duration<input name="durationTicks" type="number" min="1" :value="note.durationTicks" required :disabled="busy" /></label>
                <label>Velocity<input name="velocity" type="number" min="1" max="127" :value="note.velocity" required :disabled="busy" /></label>
                <button type="submit" :disabled="busy">Save note</button>
              </form>
              <div class="note-event-removal">
                <button type="button" class="danger" :disabled="busy || noteOwners(project.musicalParts, note.id).length > 0" :aria-describedby="noteOwners(project.musicalParts, note.id).length ? `note-owner-${note.id}` : undefined" @click="removeNoteEvent(note.id, note.pitch)">Remove</button>
                <small v-if="noteOwners(project.musicalParts, note.id).length" :id="`note-owner-${note.id}`">{{ noteRemovalGuidance(project.musicalParts, note.id) }}</small>
              </div>
            </li>
          </ol>
        </div>
      </details>

      <details class="song-settings">
        <summary>Song settings</summary>
        <div class="settings-grid">
          <label>Artist<input v-model="project.artist" maxlength="200" placeholder="Artist or songwriter" /></label>
          <label>Genre<select v-model="project.genre"><option v-for="genre in genres" :key="genre" :value="genre">{{ genre === 'RAndB' ? 'R&B' : genre }}</option></select></label>
          <label v-if="!phoneCaptureMode || phoneChrome.showMusicSettings">Tempo<input v-model.number="project.timeline.tempoMap.events[0].beatsPerMinute" type="number" min="20" max="300" /></label>
          <label v-if="!phoneCaptureMode || phoneChrome.showMusicSettings">Time signature<select :value="meterValue(project)" :disabled="busy || structureLocked" :title="structureLocked ? 'Remove all musical parts before changing the time signature.' : undefined" @change="setMeter(($event.target as HTMLSelectElement).value)"><option v-for="meter in meters" :key="meter">{{ meter }}</option></select></label>
          <label v-if="!phoneCaptureMode || phoneChrome.showMusicSettings">Key tonic<select :value="project.key.tonic" :disabled="busy" @change="setKey({ tonic: ($event.target as HTMLSelectElement).value as NoteLetter })"><option v-for="letter in noteLetters" :key="letter" :value="letter">{{ letter }}</option></select></label>
          <label v-if="!phoneCaptureMode || phoneChrome.showMusicSettings">Accidental<select :value="project.key.accidental" :disabled="busy" @change="setKey({ accidental: ($event.target as HTMLSelectElement).value as Accidental })"><option v-for="accidental in accidentals" :key="accidental" :value="accidental">{{ accidental }}</option></select></label>
          <label v-if="!phoneCaptureMode || phoneChrome.showMusicSettings">Mode<select :value="project.key.mode" :disabled="busy" @change="setKey({ mode: ($event.target as HTMLSelectElement).value as ScaleMode })"><option v-for="mode in scaleModes" :key="mode" :value="mode">{{ mode === 'NaturalMinor' ? 'Natural minor' : mode }}</option></select></label>
          <label class="description-field">Description<textarea v-model="project.description" maxlength="2000" rows="3" placeholder="Song concept or creative context" /></label>
        </div>
      </details>
      </template>
      </div>
    </template>

    <div v-if="roughVocalRemovalTarget" class="modal-backdrop" role="presentation" @click.self="cancelRemoveSavedRoughVocal">
      <section class="load-dialog delete-dialog" role="alertdialog" aria-modal="true" aria-labelledby="rough-vocal-remove-title" aria-describedby="rough-vocal-remove-description">
        <p class="eyebrow">Current saved version</p>
        <h2 id="rough-vocal-remove-title">Remove {{ roughVocalRemovalTarget.asset.name }} from this song?</h2>
        <p id="rough-vocal-remove-description">This removes the selected recording from the current saved song and future <code>.maskil</code> exports. Maskil Forge keeps the previous saved version in its local safety backup, so this is not a privacy erase of every historical copy.</p>
        <p>{{ new Date(roughVocalRemovalTarget.asset.createdUtc).toLocaleString() }} · {{ formatRoughVocalBytes(roughVocalRemovalTarget.asset.byteLength) }}</p>
        <div class="dialog-actions">
          <button ref="roughVocalRemovalCancelButton" class="secondary" :disabled="busy" @click="cancelRemoveSavedRoughVocal">Keep take</button>
          <button class="danger" :disabled="busy" @click="confirmRemoveSavedRoughVocal">{{ busy ? 'Removing take…' : 'Remove take' }}</button>
        </div>
      </section>
    </div>

    <div v-if="roughVocalRenameTarget" class="modal-backdrop" role="presentation" @click.self="cancelRenameSavedRoughVocal">
      <section class="load-dialog" role="dialog" aria-modal="true" aria-labelledby="rough-vocal-rename-title" aria-describedby="rough-vocal-rename-description">
        <p class="eyebrow">Saved rough take</p>
        <h2 id="rough-vocal-rename-title">Name this recording</h2>
        <p id="rough-vocal-rename-description">Use a short name you will recognize later. Renaming changes project metadata only; the original recording bytes stay unchanged.</p>
        <label>Take name<input ref="roughVocalRenameInput" v-model="roughVocalRenameName" maxlength="80" autocomplete="off" @keydown.enter.prevent="confirmRenameSavedRoughVocal" /></label>
        <div class="dialog-actions">
          <button class="secondary" :disabled="busy" @click="cancelRenameSavedRoughVocal">Cancel</button>
          <button :disabled="busy || !roughVocalRenameName.trim()" @click="confirmRenameSavedRoughVocal">{{ busy ? 'Saving name…' : 'Save name' }}</button>
        </div>
      </section>
    </div>

    <div v-if="firstPartConfirmation" class="modal-backdrop" role="presentation" @click.self="cancelFirstPartCommit">
      <section class="load-dialog timeline-boundary-dialog" role="dialog" aria-modal="true" aria-labelledby="timeline-boundary-title" aria-describedby="timeline-boundary-description">
        <p class="eyebrow">First musical part</p>
        <h2 id="timeline-boundary-title">Ready to anchor the arrangement?</h2>
        <p id="timeline-boundary-description">Accepting <strong>{{ firstPartConfirmation.label }}</strong> gives its notes absolute positions in the song. Afterward, section order, length, deletion, duplication, and time signature are protected until every musical part is removed.</p>
        <p>Your lyrics, harmony, performance direction, section names, and approved notes remain editable.</p>
        <div class="dialog-actions">
          <button class="secondary" :disabled="busy" autofocus @click="cancelFirstPartCommit">Review structure first</button>
          <button :disabled="busy" @click="confirmFirstPartCommit">Accept first part</button>
        </div>
      </section>
    </div>

    <div v-if="portableImportPreview && pendingPortableImport" class="modal-backdrop" role="presentation" @click.self="cancelPortableImportPreview">
      <section class="load-dialog import-preview-dialog" role="dialog" aria-modal="true" aria-labelledby="import-preview-title" aria-describedby="import-preview-guidance">
        <p class="eyebrow">Portable project</p>
        <h2 id="import-preview-title">Review before import</h2>
        <p class="import-file-name">{{ pendingPortableImport.fileName }}</p>
        <dl class="import-preview-meta">
          <div><dt>Song</dt><dd>{{ portableImportPreview.title }}</dd></div>
          <div><dt>Artist</dt><dd>{{ portableImportPreview.artist || 'Not named' }}</dd></div>
          <div><dt>Genre</dt><dd>{{ portableImportPreview.genre === 'RAndB' ? 'R&B' : portableImportPreview.genre }}</dd></div>
          <div><dt>File version</dt><dd>{{ portableImportPreview.sourceSchemaVersion === portableImportPreview.currentSchemaVersion ? `Version ${portableImportPreview.currentSchemaVersion}` : `Version ${portableImportPreview.sourceSchemaVersion} → ${portableImportPreview.currentSchemaVersion}` }}</dd></div>
          <div><dt>Contents</dt><dd>{{ portableImportPreview.sectionCount ? `${portableImportPreview.sectionCount} section${portableImportPreview.sectionCount === 1 ? '' : 's'} · ${portableImportPreview.lyricLineCount} lyric line${portableImportPreview.lyricLineCount === 1 ? '' : 's'}` : portableImportPreview.hasRawLyrics ? `Raw lyric draft · ${portableImportPreview.lyricLineCount} lyric line${portableImportPreview.lyricLineCount === 1 ? '' : 's'}` : 'New idea' }}</dd></div>
          <div v-if="portableImportPreview.sectionTitles.length"><dt>Song form</dt><dd>{{ portableImportPreview.sectionTitles.join(' → ') }}</dd></div>
          <div v-if="portableImportPreview.originalVocalCount"><dt>Original vocals</dt><dd>{{ portableImportPreview.originalVocalCount }} verified take{{ portableImportPreview.originalVocalCount === 1 ? '' : 's' }} in this package</dd></div>
        </dl>
        <p v-if="portableImportPreview.identityConflict" id="import-preview-guidance" class="import-conflict"><strong>Project already known.</strong> Import it as a new copy to keep the existing song safe and give this file its own identity.</p>
        <p v-else id="import-preview-guidance">{{ portableImportPreview.originalVocalCount ? 'This package carries original vocal bytes with the Song Graph. Import the original to preserve its project identity, or make a separate copy you can change independently.' : 'Import the original to preserve its project identity, or make a separate copy you can change independently.' }}</p>
        <div class="dialog-actions">
          <button class="secondary" :disabled="busy" autofocus @click="cancelPortableImportPreview">Cancel</button>
          <button v-if="!portableImportPreview.identityConflict" :disabled="busy" @click="choosePortableImport(false)">Import project</button>
          <button :class="{ secondary: !portableImportPreview.identityConflict }" :disabled="busy" @click="choosePortableImport(true)">{{ portableImportPreview.identityConflict ? 'Import as new copy' : 'Import as copy' }}</button>
        </div>
      </section>
    </div>

    <div v-if="confirmationOpen" class="modal-backdrop" role="presentation" @click.self="cancelConfirmation">
      <section class="load-dialog" role="dialog" aria-modal="true" aria-labelledby="confirmation-title">
        <p class="eyebrow">Protect your work</p>
        <h2 id="confirmation-title">Unsaved changes detected</h2>
        <p>Save your current song before {{ pendingAction === 'new' ? 'beginning a new one' : pendingAction === 'load' ? 'opening another one' : pendingAction === 'import' ? 'importing a project file' : 'returning to the song library' }}?</p>
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

    <div v-if="deviceLyricCaptureDeleteTarget" class="modal-backdrop" role="presentation" @click.self="cancelDeviceLyricCaptureDelete">
      <section class="load-dialog delete-dialog" role="alertdialog" aria-modal="true" aria-labelledby="device-capture-delete-title" aria-describedby="device-capture-delete-description">
        <p class="eyebrow">Browser-owned work</p>
        <h2 id="device-capture-delete-title">Permanently delete this device capture?</h2>
        <p id="device-capture-delete-description">“{{ deviceLyricCaptureDeleteTarget.title }}” is not in Trash or the saved-song library. Deleting it removes its locally saved words from this browser and cannot be undone.</p>
        <div class="dialog-actions">
          <button class="secondary" :disabled="deviceLyricCaptureBusy" autofocus @click="cancelDeviceLyricCaptureDelete">Keep capture</button>
          <button class="danger" :disabled="deviceLyricCaptureBusy" @click="confirmDeviceLyricCaptureDelete">Yes, permanently delete</button>
        </div>
      </section>
    </div>

    <div v-if="bulkDeviceLyricCaptureDeleteOpen" class="modal-backdrop" role="presentation" @click.self="cancelBulkDeviceLyricCaptureDelete">
      <section class="load-dialog delete-dialog library-cleanup-dialog" role="alertdialog" aria-modal="true" aria-labelledby="bulk-device-capture-delete-title" aria-describedby="bulk-device-capture-delete-description">
        <p class="eyebrow">Cannot be undone</p>
        <h2 id="bulk-device-capture-delete-title">Permanently delete {{ selectedDeviceLyricCaptures.length }} device capture{{ selectedDeviceLyricCaptures.length === 1 ? '' : 's' }}?</h2>
        <p id="bulk-device-capture-delete-description">Every browser-owned capture listed below will be removed from this browser. They are not in Trash or the connected song library, so this action cannot be undone.</p>
        <ul class="library-cleanup-list"><li v-for="summary in selectedDeviceLyricCaptures" :key="summary.id"><strong>{{ summary.title }}</strong><span>{{ summary.artist || 'Artist not set' }} · {{ summary.lyricLineCount ? `${summary.lyricLineCount} lyric line${summary.lyricLineCount === 1 ? '' : 's'}` : 'Empty capture' }} · Saved {{ formatModified(summary.savedAtUtc) }}</span></li></ul>
        <div class="dialog-actions">
          <button ref="bulkDeviceLyricCaptureDeleteCancelButton" class="secondary" :disabled="deviceLyricCaptureBusy" @click="cancelBulkDeviceLyricCaptureDelete">Keep these captures</button>
          <button class="danger" :disabled="deviceLyricCaptureBusy" @click="confirmBulkDeviceLyricCaptureDelete">Yes, permanently delete {{ selectedDeviceLyricCaptures.length }}</button>
        </div>
      </section>
    </div>

    <div v-if="bulkTrashOpen" class="modal-backdrop" role="presentation" @click.self="cancelBulkTrash">
      <section class="load-dialog delete-dialog library-cleanup-dialog" role="alertdialog" aria-modal="true" aria-labelledby="bulk-trash-title" aria-describedby="bulk-trash-description">
        <p class="eyebrow">Reversible library cleanup</p>
        <h2 id="bulk-trash-title">Move {{ selectedLibraryProjects.length }} empty start{{ selectedLibraryProjects.length === 1 ? '' : 's' }} to Trash?</h2>
        <p id="bulk-trash-description">Only the saved songs listed below will leave your library. They can still be restored from Trash; this does not permanently delete project data.</p>
        <ul class="library-cleanup-list">
          <li v-for="summary in selectedLibraryProjects" :key="summary.id"><strong>{{ summary.title }}</strong><span>{{ summary.artist || 'Artist not set' }} · Modified {{ formatModified(summary.lastModifiedUtc) }}</span></li>
        </ul>
        <div class="dialog-actions">
          <button ref="bulkTrashCancelButton" class="secondary" :disabled="busy" @click="cancelBulkTrash">Keep these songs</button>
          <button class="danger" :disabled="busy" @click="confirmBulkTrash">Move {{ selectedLibraryProjects.length }} to Trash</button>
        </div>
      </section>
    </div>

    <div v-if="bulkRestoreOpen" class="modal-backdrop" role="presentation" @click.self="cancelBulkRestore">
      <section class="load-dialog library-cleanup-dialog" role="dialog" aria-modal="true" aria-labelledby="bulk-restore-title" aria-describedby="bulk-restore-description">
        <p class="eyebrow">Return to song library</p>
        <h2 id="bulk-restore-title">Restore {{ selectedTrashProjects.length }} song{{ selectedTrashProjects.length === 1 ? '' : 's' }}?</h2>
        <p id="bulk-restore-description">The selected songs will leave Trash and return to your saved-song library. Their project contents will not change.</p>
        <ul class="library-cleanup-list"><li v-for="summary in selectedTrashProjects" :key="summary.id"><strong>{{ summary.title }}</strong><span>{{ summary.artist || 'Artist not set' }} · Deleted {{ formatModified(summary.deletedAtUtc) }}</span></li></ul>
        <div class="dialog-actions">
          <button ref="bulkRestoreCancelButton" class="secondary" :disabled="busy" @click="cancelBulkRestore">Keep in Trash</button>
          <button :disabled="busy" @click="confirmBulkRestore">Restore {{ selectedTrashProjects.length }} song{{ selectedTrashProjects.length === 1 ? '' : 's' }}</button>
        </div>
      </section>
    </div>

    <div v-if="bulkPermanentDeleteOpen" class="modal-backdrop" role="presentation" @click.self="cancelBulkPermanentDelete">
      <section class="load-dialog delete-dialog library-cleanup-dialog" role="alertdialog" aria-modal="true" aria-labelledby="bulk-permanent-delete-title" aria-describedby="bulk-permanent-delete-description">
        <p class="eyebrow">Cannot be undone</p>
        <h2 id="bulk-permanent-delete-title">Permanently delete {{ selectedTrashProjects.length }} song{{ selectedTrashProjects.length === 1 ? '' : 's' }}?</h2>
        <p id="bulk-permanent-delete-description">Every saved project listed below will be erased forever, including its backup and recovery artifacts. This action cannot be undone.</p>
        <ul class="library-cleanup-list"><li v-for="summary in selectedTrashProjects" :key="summary.id"><strong>{{ summary.title }}</strong><span>{{ summary.artist || 'Artist not set' }} · Deleted {{ formatModified(summary.deletedAtUtc) }}</span></li></ul>
        <div class="dialog-actions">
          <button ref="bulkPermanentDeleteCancelButton" class="secondary" :disabled="busy" @click="cancelBulkPermanentDelete">Keep these songs</button>
          <button class="danger" :disabled="busy" @click="confirmBulkPermanentDelete">Yes, permanently delete {{ selectedTrashProjects.length }}</button>
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

    <div v-if="recoveryDiscardTarget" class="modal-backdrop" role="presentation" @click.self="cancelRecoveryDiscard">
      <section class="load-dialog delete-dialog" role="alertdialog" aria-modal="true" aria-labelledby="recovery-discard-title" aria-describedby="recovery-discard-description">
        <p class="eyebrow">Protected unsaved work</p>
        <h2 id="recovery-discard-title">Discard this protected work?</h2>
        <p id="recovery-discard-description">This permanently removes every recovery copy of “{{ recoveryDiscardTarget.title }}” from {{ recoveryDiscardTarget.sourceLabel.toLowerCase() }} without changing its explicitly saved song. {{ recoveryDiscardTarget.sectionCount ? `${recoveryDiscardTarget.sectionCount} structured section${recoveryDiscardTarget.sectionCount === 1 ? '' : 's'} and ${recoveryDiscardTarget.lyricLineCount} lyric line${recoveryDiscardTarget.lyricLineCount === 1 ? '' : 's'} will no longer be recoverable.` : recoveryDiscardTarget.hasRawLyrics ? `${recoveryDiscardTarget.lyricLineCount} raw lyric line${recoveryDiscardTarget.lyricLineCount === 1 ? '' : 's'} will no longer be recoverable.` : 'Its unsaved idea will no longer be recoverable.' }}</p>
        <p v-if="recoveryDiscardTarget.sectionTitles.length" class="recovery-form">{{ recoveryDiscardTarget.sectionTitles.join(' → ') }}</p>
        <div class="dialog-actions">
          <button ref="recoveryDiscardCancelButton" class="secondary" :disabled="busy" @click="cancelRecoveryDiscard">Keep protected work</button>
          <button class="danger" :disabled="busy" @click="confirmRecoveryDiscard">Yes, discard protected work</button>
        </div>
      </section>
    </div>

    <div v-if="staleRecoveryCleanupOpen" class="modal-backdrop" role="presentation" @click.self="cancelStaleRecoveryCleanup">
      <section class="load-dialog delete-dialog stale-cleanup-dialog" role="alertdialog" aria-modal="true" aria-labelledby="stale-cleanup-title" aria-describedby="stale-cleanup-description">
        <p class="eyebrow">Explicit recovery cleanup</p>
        <h2 id="stale-cleanup-title">Discard all stale protected work?</h2>
        <p id="stale-cleanup-description">These {{ staleRecoveryQueue.length }} song{{ staleRecoveryQueue.length === 1 ? '' : 's' }} have been protected for at least {{ recoveryStaleDays }} days. This removes every host and browser recovery copy listed below, but does not change any explicitly saved song.</p>
        <ul class="stale-cleanup-list">
          <li v-for="summary in staleRecoveryQueue" :key="summary.id"><strong>{{ summary.title }}</strong><span>{{ summary.sectionCount ? `${summary.sectionCount} section${summary.sectionCount === 1 ? '' : 's'} · ${summary.lyricLineCount} lyric line${summary.lyricLineCount === 1 ? '' : 's'}` : summary.hasRawLyrics ? `${summary.lyricLineCount} raw lyric line${summary.lyricLineCount === 1 ? '' : 's'}` : 'New idea' }} · {{ summary.sourceLabel }}</span></li>
        </ul>
        <div class="dialog-actions">
          <button ref="staleRecoveryCleanupCancelButton" class="secondary" :disabled="busy" @click="cancelStaleRecoveryCleanup">Keep stale work</button>
          <button class="danger" :disabled="busy" @click="confirmStaleRecoveryCleanup">Yes, discard {{ staleRecoveryQueue.length }} stale</button>
        </div>
      </section>
    </div>
  </main>
</template>
