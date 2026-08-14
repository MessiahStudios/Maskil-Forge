import type { SongProject } from './api'

export interface BrowserRecoveryRecord {
  projectId: string
  project: SongProject
  baseProjectLastModifiedUtc: string
  sessionId: string
  capturedAtUtc: string
}

const databaseName = 'maskil-forge-browser'
const databaseVersion = 1
const recoveryStoreName = 'recoverySnapshots'

function openDatabase() {
  return new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open(databaseName, databaseVersion)
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(recoveryStoreName)) {
        request.result.createObjectStore(recoveryStoreName, { keyPath: 'projectId' })
      }
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('Browser recovery storage could not be opened.'))
    request.onblocked = () => reject(new Error('Browser recovery storage is blocked by another Maskil Forge window.'))
  })
}

async function readRequest<T>(createRequest: (store: IDBObjectStore) => IDBRequest<T>) {
  const database = await openDatabase()
  try {
    return await new Promise<T>((resolve, reject) => {
      const request = createRequest(database.transaction(recoveryStoreName, 'readonly').objectStore(recoveryStoreName))
      request.onsuccess = () => resolve(request.result)
      request.onerror = () => reject(request.error ?? new Error('Browser recovery storage could not be read.'))
    })
  } finally {
    database.close()
  }
}

async function writeRequest(createRequest: (store: IDBObjectStore) => IDBRequest) {
  const database = await openDatabase()
  try {
    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(recoveryStoreName, 'readwrite')
      createRequest(transaction.objectStore(recoveryStoreName))
      transaction.oncomplete = () => resolve()
      transaction.onerror = () => reject(transaction.error ?? new Error('Browser recovery storage could not be updated.'))
      transaction.onabort = () => reject(transaction.error ?? new Error('Browser recovery storage update was cancelled.'))
    })
  } finally {
    database.close()
  }
}

export function protectBrowserRecovery(record: BrowserRecoveryRecord) {
  return writeRequest(store => store.put(record))
}

export async function listBrowserRecoveries() {
  const records = await readRequest<BrowserRecoveryRecord[]>(store => store.getAll())
  return records.sort((left, right) => right.capturedAtUtc.localeCompare(left.capturedAtUtc))
}

export async function loadBrowserRecovery(projectId: string) {
  return await readRequest<BrowserRecoveryRecord | undefined>(store => store.get(projectId)) ?? null
}

export function discardBrowserRecovery(projectId: string) {
  return writeRequest(store => store.delete(projectId))
}
