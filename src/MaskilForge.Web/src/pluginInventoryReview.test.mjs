import test from 'node:test'
import assert from 'node:assert/strict'
import { candidateArchitecture, reviewPluginInventory } from './pluginInventoryReview.js'

const processor = '0123456789ABCDEF0123456789ABCDEF'
const controller = 'FEDCBA9876543210FEDCBA9876543210'
const header = (hostMatch, status = 'Recognized') => ({ hostMatch, status })
const candidate = (name, classes = [], files = []) => ({
  name, relativePath: `Vendor/${name}.vst3`, kind: 'Bundle',
  metadata: { status: 'Available', module: { name: 'Café EQ', vendor: 'Fixture Audio', version: '2.0', classes } },
  binary: { status: 'Inspected', files },
})
const reported = (id = processor, version = '1.0', category = 'Audio Module Class') => ({
  id, name: 'Warm Equalizer', category, vendor: 'Class Vendor', version, subCategories: ['Fx', 'EQ'],
})
const location = (name, candidates = [], status = 'Complete') => ({ name, candidates, status, issues: [], hint: name })
const inventory = (...locations) => ({ locations })

test('empty inventory retains missing and empty folder statuses', () => {
  assert.deepEqual(reviewPluginInventory(null), { locations: [], totalCount: 0, visibleCount: 0, repeatedClasses: [] })
  const review = reviewPluginInventory(inventory(location('User', [], 'Missing'), location('System')))
  assert.equal(review.totalCount, 0)
  assert.deepEqual(review.locations.map(root => [root.status, root.totalCount, root.rows.length]), [['Missing', 0, 0], ['Complete', 0, 0]])
})

test('search combines terms across reported fields and normalizes case and Unicode', () => {
  const source = inventory(location('User', [candidate('Modern', [reported()])]))
  for (const query of ['CAFE\u0301 fixture 2.0', 'user vendor/modern', `${processor.toLowerCase()} equalizer`, 'class vendor audio module eq 1.0']) {
    assert.equal(reviewPluginInventory(source, { query }).visibleCount, 1, query)
  }
  assert.equal(reviewPluginInventory(source, { query: 'fixture absent' }).visibleCount, 0)
  assert.equal(reviewPluginInventory(source, { query: '  \t  ' }).visibleCount, 1)
})

test('unavailable metadata cannot contribute search terms or repeated IDs', () => {
  const missing = candidate('Missing', [reported()])
  missing.metadata.status = 'InvalidOrUnsupported'
  const source = inventory(location('User', [missing, candidate('Valid', [reported()])]))
  assert.equal(reviewPluginInventory(source, { query: 'missing' }).visibleCount, 1)
  assert.equal(reviewPluginInventory(source, { query: 'fixture' }).visibleCount, 1)
  assert.equal(reviewPluginInventory(source).repeatedClasses.length, 0)
})

test('architecture classification requires affirmative header evidence', () => {
  const cases = [
    [[], 'undetermined'], [[header('Match'), header('Different')], 'match'],
    [[header('Different'), header('Different')], 'different'],
    [[header('Different'), header('Unknown')], 'undetermined'],
    [[header('Different'), header('Different', 'Invalid')], 'undetermined'],
    [[header('Match', 'Unreadable')], 'undetermined'],
  ]
  for (const [files, expected] of cases) assert.equal(candidateArchitecture(candidate('Test', [], files)), expected)
  assert.equal(candidateArchitecture({}), 'undetermined')
  assert.equal(candidateArchitecture({ binary: { status: 'ScanLimit', files: [header('Match')] } }), 'undetermined')
})

test('repeated IDs identify distinct candidates across folders, including controllers', () => {
  const source = inventory(
    location('User', [candidate('Same', [reported(), reported(processor.toLowerCase()), reported(controller, '1.0', 'Component Controller Class')])]),
    location('System', [candidate('Same', [reported(processor.toLowerCase(), '2.0'), reported(controller, '2.0', 'Component Controller Class')])]),
  )
  const review = reviewPluginInventory(source)
  assert.equal(review.totalCount, 2)
  assert.equal(review.repeatedClasses.length, 2)
  assert.deepEqual(review.repeatedClasses[0].occurrences.map(item => [item.location, item.version]), [['User', '1.0'], ['System', '2.0']])
  assert.equal(new Set(review.repeatedClasses[0].occurrences.map(item => item.key)).size, 2)
  assert.equal(review.locations[0].rows[0].repeatedClassCount, 2)
  assert.equal(review.repeatedClasses[1].occurrences[0].category, 'Component Controller Class')
})

test('one candidate repeating a class and invalid IDs do not produce duplicate findings', () => {
  const source = inventory(location('User', [candidate('A', [reported(), reported(), reported('bad')]), candidate('B', [reported('bad')])]))
  assert.equal(reviewPluginInventory(source).repeatedClasses.length, 0)
  assert.equal(reviewPluginInventory(source, { repeatedOnly: true }).visibleCount, 0)
})

test('filters combine without changing full-scan repeated-ID findings or root counts', () => {
  const source = inventory(location('User', [
    candidate('New', [reported()], [header('Match')]),
    candidate('Old', [reported(processor, '0.5')], [header('Different')]),
    candidate('Unknown'),
  ]), location('Empty'))
  const review = reviewPluginInventory(source, { query: 'new', architecture: 'match', repeatedOnly: true })
  assert.equal(review.totalCount, 3)
  assert.equal(review.visibleCount, 1)
  assert.equal(review.repeatedClasses[0].occurrences.length, 2)
  assert.equal(review.repeatedClasses[0].occurrences[1].version, '0.5')
  assert.equal(reviewPluginInventory(source, { architecture: 'different' }).locations[0].rows[0].candidate.name, 'Old')
  assert.equal(reviewPluginInventory(source, { architecture: 'undetermined' }).locations[0].rows[0].candidate.name, 'Unknown')
  const hidden = reviewPluginInventory(source, { query: 'absent' })
  assert.deepEqual(hidden.locations.map(root => [root.totalCount, root.rows.length]), [[3, 0], [0, 0]])
  assert.equal(hidden.repeatedClasses.length, 1)
})

test('review preserves the original scan and candidate identities', () => {
  const source = inventory(location('User', [candidate('A', [reported()]), candidate('B', [reported()])]))
  const before = JSON.stringify(source)
  function freeze(value) { if (value && typeof value === 'object') { Object.values(value).forEach(freeze); Object.freeze(value) } }
  freeze(source)
  const review = reviewPluginInventory(source, { query: 'a', repeatedOnly: true })
  assert.equal(review.locations[0].rows[0].candidate, source.locations[0].candidates[0])
  assert.equal(JSON.stringify(source), before)
})
