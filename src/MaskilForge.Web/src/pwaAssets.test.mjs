import assert from 'node:assert/strict'
import { readFileSync, existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const webRoot = join(dirname(fileURLToPath(import.meta.url)), '..')
const manifest = JSON.parse(readFileSync(join(webRoot, 'public', 'manifest.webmanifest'), 'utf8'))
const serviceWorker = readFileSync(join(webRoot, 'public', 'sw.js'), 'utf8')

test('install manifest owns the app scope and required PNG icon sizes', () => {
  assert.equal(manifest.id, '/')
  assert.equal(manifest.start_url, '/')
  assert.equal(manifest.scope, '/')
  assert.equal(manifest.display, 'standalone')

  for (const size of ['192x192', '512x512']) {
    const icon = manifest.icons.find(candidate => candidate.sizes === size && candidate.type === 'image/png')
    assert.ok(icon, `Missing ${size} PNG icon.`)
    assert.ok(existsSync(join(webRoot, 'public', icon.src.replace(/^\//, ''))), `Missing ${icon.src}.`)
  }
})

test('application shell cache explicitly leaves project API requests on the network', () => {
  assert.match(serviceWorker, /url\.pathname\.startsWith\('\/api\/'\)/)
  assert.match(serviceWorker, /isProjectApi\(url\)\) return/)
  assert.match(serviceWorker, /caches\.match\('\/'\)/)
  assert.doesNotMatch(serviceWorker, /cache\.put\([^\n]*\/api/)
})
