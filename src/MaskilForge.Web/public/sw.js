const shellCacheName = 'maskil-forge-shell-v14'
const shellDocuments = ['/', '/logs.html']
const shellResources = [
  ...shellDocuments,
  '/manifest.webmanifest',
  '/icons/maskil-forge.svg',
  '/icons/maskil-forge-192.png',
  '/icons/maskil-forge-512.png',
]

function isProjectApi(url) {
  return url.pathname === '/api' || url.pathname.startsWith('/api/')
}

async function fetchAndCache(cache, path) {
  const response = await fetch(new Request(path, { cache: 'reload' }))
  if (!response.ok) throw new Error(`Could not cache ${path}.`)
  await cache.put(path, response.clone())
  return response
}

async function cacheApplicationShell() {
  const cache = await caches.open(shellCacheName)
  const documents = await Promise.all(shellResources.map(path => fetchAndCache(cache, path)))
  const assetPaths = new Set()

  for (const response of documents.slice(0, shellDocuments.length)) {
    const html = await response.text()
    for (const match of html.matchAll(/(?:src|href)="(\/assets\/[^"]+)"/g)) assetPaths.add(match[1])
  }

  await Promise.all([...assetPaths].map(path => fetchAndCache(cache, path)))
}

self.addEventListener('install', event => {
  event.waitUntil(cacheApplicationShell())
})

self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    const cacheNames = await caches.keys()
    await Promise.all(cacheNames
      .filter(name => name.startsWith('maskil-forge-shell-') && name !== shellCacheName)
      .map(name => caches.delete(name)))
    await self.clients.claim()
  })())
})

self.addEventListener('message', event => {
  if (event.data?.type === 'SKIP_WAITING') void self.skipWaiting()
})

self.addEventListener('fetch', event => {
  const request = event.request
  const url = new URL(request.url)

  if (request.method !== 'GET' || url.origin !== self.location.origin || isProjectApi(url)) return

  if (request.mode === 'navigate') {
    event.respondWith((async () => {
      try {
        const response = await fetch(request)
        if (response.ok) {
          const cache = await caches.open(shellCacheName)
          await cache.put(request, response.clone())
        }
        return response
      } catch {
        return await caches.match(request) ?? await caches.match('/')
      }
    })())
    return
  }

  event.respondWith((async () => {
    const cached = await caches.match(request)
    if (cached) return cached
    const response = await fetch(request)
    if (response.ok) {
      const cache = await caches.open(shellCacheName)
      await cache.put(request, response.clone())
    }
    return response
  })())
})
