export interface InstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
}

export function isStandaloneApplication() {
  return window.matchMedia('(display-mode: standalone)').matches
}

export async function registerApplicationShell(onUpdateReady?: (registration: ServiceWorkerRegistration) => void) {
  if (import.meta.env.DEV || !('serviceWorker' in navigator)) return null

  const registration = await navigator.serviceWorker.register('/sw.js', { scope: '/' })
  if (registration.waiting) onUpdateReady?.(registration)

  registration.addEventListener('updatefound', () => {
    const worker = registration.installing
    worker?.addEventListener('statechange', () => {
      if (worker.state === 'installed' && navigator.serviceWorker.controller) onUpdateReady?.(registration)
    })
  })

  void registration.update().catch(() => undefined)
  return registration
}

export function activateApplicationShellUpdate(registration: ServiceWorkerRegistration) {
  registration.waiting?.postMessage({ type: 'SKIP_WAITING' })
}
