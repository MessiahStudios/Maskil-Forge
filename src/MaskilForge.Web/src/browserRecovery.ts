import type { SongProject } from './api'

export interface BrowserRecoveryRecord {
  projectId: string
  project: SongProject
  baseProjectLastModifiedUtc: string
  sessionId: string
  capturedAtUtc: string
}

export interface BrowserProjectRecord {
  projectId: string
  project: SongProject
  savedAtUtc: string
}

const databaseName = 'maskil-forge-browser'
const databaseVersion = 2
const recoveryStoreName = 'recoverySnapshots'
const projectStoreName = 'savedProjectSnapshots'

function openDatabase() {
  return new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open(databaseName, databaseVersion)
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(recoveryStoreName)) {
        request.result.createObjectStore(recoveryStoreName, { keyPath: 'projectId' })
      }
      if (!request.result.objectStoreNames.contains(projectStoreName)) {
        request.result.createObjectStore(projectStoreName, { keyPath: 'projectId' })
      }
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('Maskil Forge browser storage could not be opened.'))
    request.onblocked = () => reject(new Error('Maskil Forge browser storage is blocked by another Maskil Forge window.'))
  })
}

async function readRequest<T>(storeName: string, createRequest: (store: IDBObjectStore) => IDBRequest<T>) {
  const database = await openDatabase()
  try {
    return await new Promise<T>((resolve, reject) => {
      const request = createRequest(database.transaction(storeName, 'readonly').objectStore(storeName))
      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(request.error ?? new Error('Maskil Forge browser storage could not be read.'))
    })
  } finally {
    database.close()
  }
}

async function writeRequest(storeName: string, createRequest: (store: IDBObjectStore) => IDBRequest) {
  const database = await openDatabase()
  try {
    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(storeName, 'readwrite')
      createRequest(transaction.objectStore(storeName))
      transaction.oncomplete = () => resolve()
      transaction.onerror = () => reject(transaction.error ?? new Error('Maskil Forge browser storage could not be updated.'))
      transaction.onabort = () => reject(transaction.error ?? new Error('Maskil Forge browser storage update was cancelled.'))
    })
  } finally {
    database.close()
  }
}

export function protectBrowserRecovery(record: BrowserRecoveryRecord) {
  return writeRequest(recoveryStoreName, store => store.put(record))
}

export async function listBrowserRecoveries() {
  const records = await readRequest<BrowserRecoveryRecord[]>(recoveryStoreName, store => store.getAll())
  return records.sort((left, right) => right.capturedAtUtc.localeCompare(left.capturedAtUtc))
}

export async function loadBrowserRecovery(projectId: string) {
  return await readRequest<BrowserRecoveryRecord | undefined>(recoveryStoreName, store => store.get(projectId)) ?? null
}

export function discardBrowserRecovery(projectId: string) {
  return writeRequest(recoveryStoreName, store => store.delete(projectId))
}

export function cacheBrowserProject(record: BrowserProjectRecord) {
  return writeRequest(projectStoreName, store => store.put(record))
}

export async function listBrowserProjects() {
  const records = await readRequest<BrowserProjectRecord[]>(projectStoreName, store => store.getAll())
  return records.sort((left, right) => right.savedAtUtc.localeCompare(left.savedAtUtc))
}

export async function loadBrowserProject(projectId: string) {
  return await readRequest<BrowserProjectRecord | undefined>(projectStoreName, store => store.get(projectId)) ?? null
}

export function discardBrowserProject(projectId: string) {
  return writeRequest(projectStoreName, store => store.delete(projectId))
}
