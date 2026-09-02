import { copyFile, mkdir } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const webRoot = join(dirname(fileURLToPath(import.meta.url)), '..')
const source = join(webRoot, 'node_modules', 'spessasynth_lib', 'dist', 'spessasynth_processor.min.js')
const destination = join(webRoot, 'public', 'spessasynth_processor.min.js')

await mkdir(dirname(destination), { recursive: true })
await copyFile(source, destination)
