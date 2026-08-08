# 06 — Delivery Roadmap

This is the recommended logical build order for Maskil Forge and its underlying Maskil Engine. The repository currently contains the product and architecture foundation only. A milestone should start only after its dependency gate is reliable.

## Milestone 0 — Decisions and skeleton

Define the supported desktop platforms, initial genre, first renderer, project file format, tick resolution, audio/MIDI libraries, and boundaries between TypeScript, .NET, and native audio code. When implementation begins, use `MaskilForge` as the C# namespace root. Create the solution, tests, CI, schema versioning, and architecture decision records.

**Deliverable:** an empty project opens, saves, reloads, and reports its schema version.

## Milestone 1 — Song foundation

Build the Song Graph, sections, timeline, tempo/meter, tracks, clips, markers, commands, undo/redo, persistence, migrations, and validation.

**Deliverable:** a reliable non-audio song editor.

### Milestone 1.6 — Project library and lyric capture

Provide a deliberate welcome screen, discover locally saved projects without requiring a known identifier, capture and save an unstructured lyric draft, and let the artist move between raw writing and direct section editing without destroying either representation. Track last-modified time and protect unsaved work when switching projects.

The current foundation implements the local project summary list, raw lyric draft, direct transition into Song Graph editing, unsaved-work prompts, confirmed recoverable deletion, Trash browsing and restoration, and separately confirmed permanent deletion. Duplication, import/export, automatic structural suggestions, and lyric analysis remain future work.

**Deliverable:** an artist can begin with words rather than a predefined song form, close the application, find the project again, and continue with the raw draft and structured sections intact.

### Next durability slice

The focused `feature/project-durability` slice now provides the migration boundary for schema evolution, compatibility with early schema-v1 files, explicit future-version rejection, validated temporary saves, known-good-file backups, content-addressed corrupt-file recovery copies, per-project failure isolation during library listing, and structured API errors. Confirmed permanent deletion includes these durability artifacts. Session-only undo remains an explicit current decision; a persistent command journal should not be added casually.

The follow-up `feature/session-recovery` slice adds automatic dirty-editor snapshots, a startup recovery screen, restore/discard actions, snapshot cleanup after save or deletion, and persisted-revision checks that reject stale saves. The recovery state remains separate from the explicitly saved song.

Still planned for later durability work are timed snapshot retention policies, user-facing saved-version history, and recovery from external asset failures. The timeline foundation now provides the first real schema evolution: schema-v1 projects and recovery snapshots migrate in memory to schema v2 while preserving their settings and identities.

### Milestone 1.8 — Timeline foundation

The current timeline slice establishes a 480-PPQ musical clock, validated bar/beat/tick positions, absolute-tick conversion, tempo and time-signature maps, and ordered section placements with editable bar durations. Section add, remove, reorder, resize, undo, and redo keep placements contiguous and tied to stable section IDs. GitHub Actions verifies the .NET solution and Vue production build on pull requests and `main`.

Variable tempo/meter regions, markers, clip placement, seconds conversion, transport, playback, MIDI, and audio remain outside this slice.

**Deliverable:** Maskil Forge can describe where each song section exists in musical time without acting as a DAW.

## Milestone 2 — Lyrics and prosody

Build the lyric document, token/syllable/stress annotations, beat mapping, breath analysis, rhythm candidates, scoring, locks, and a basic lyric/timeline UI.

### Milestone 2.1 — Lyric document foundation

The current slice gives lyric lines, words, and syllables strongly typed identifiers; tokenizes structured lines with exact source offsets; preserves unchanged word identities across edits; and exposes addressable word tokens in the editor. Schema-v2 songs and recovery snapshots migrate deterministically to schema v3. Syllable collections are explicitly editable data but remain empty until a future analyzer or artist supplies boundaries.

Automatic syllable extraction, stress, rhyme, breath points, rhythm candidates, locks, scoring, and beat mapping remain future Milestone 2 slices.

**Deliverable:** Maskil Forge can identify and preserve the individual words that later lyric intelligence will analyze.

### Milestone 2.2 — Syllable foundation

The current slice makes syllables ordered entities with stable identifiers and explicit `Manual`, `Analyzer`, or `Imported` provenance. The editor lets the artist enter and correct boundaries with a visible separator, treats those corrections as authoritative, and retains matching IDs when surrounding boundaries shift. Schema-v3 songs and recovery snapshots migrate in memory to schema v4 without changing existing syllable IDs.

No automatic pronunciation or syllabification service is implemented yet. Stress, breath points, beat placement, melisma, scoring, and AI assistance remain later focused slices.

**Deliverable:** an artist can define how each word is intended to be sung, save it, reload it, and retain the same ordered syllable identities and provenance.

### Milestone 2.3 — Phrase foundation

The current slice identifies punctuation without removing it from the original lyric text and represents each phrase as an ordered reference to existing word IDs. New and schema-v4 lines begin as one default phrase. Artists can add phrase breaks or join adjacent phrases with explicit, readable controls; these decisions receive `Manual` provenance and participate in session undo/redo without changing phrase identities during redo. Phrase and punctuation identities survive save/load and compatible nearby text edits. Schema-v4 songs and recovery snapshots migrate deterministically to schema v5.

Punctuation does not automatically imply a breath or phrase break. Phrase meaning, emphasis, stress, breath recommendations, automatic boundary suggestions, rhythm candidates, and beat mapping remain future work.

**Deliverable:** an artist can group a lyric line into meaningful sung ideas without changing its words, and those groupings remain stable, editable project data.

**Deliverable:** the app explains and demonstrates how lyrics fit musical time.

## Milestone 3 — Theory and harmony

Build music primitives, keys/scales/chords, progressions, transposition, voice leading, range checks, harmony candidates, and simple chord audition.

**Deliverable:** approved prosody sits over valid, editable harmony.

## Milestone 4 — Arrangement blueprint

Build energy curves, role-based arrangement, genre and instrument profiles, section contrast, entries/exits, and arrangement candidates.

**Deliverable:** a complete visual song blueprint with instrument roles.

## Milestone 5 — MIDI composition and preview

Build melody, bass, drums, voicings, countermelody, and transition generators; add piano-roll editing, transport, simple synthesis, and MIDI export.

**Deliverable:** the blueprint becomes an audible, editable demo.

This is the first strong MVP boundary.

## Milestone 6 — Voice performance capture

Build recording, pitch/onset/loudness extraction, gesture editing, voice-to-MIDI, and expression curves.

**Deliverable:** humming or singing can control musical material.

## Milestone 7 — Instrument intelligence

Expand instrument knowledge, recommendations, range checks, articulation maps, and at least two performance retargeters.

**Deliverable:** an artist can choose by emotional quality instead of orchestration vocabulary.

## Milestone 8 — Rendering integrations

Add SoundFont or equivalent rendering, external DAW export, plugin scanning, VST3 hosting, presets, automation, and offline rendering in that order.

**Deliverable:** the same Song Graph can drive multiple sound sources.

## Milestone 9 — Human vocal production

Build guide vocals, lyric highlighting, take management, punch-in, comping, pitch/timing feedback, harmony guides, and non-destructive vocal effects.

**Deliverable:** the artist can complete the lead-vocal workflow inside the product.

## Milestone 10 — AI director

Expose tested engine functions as typed tools, add intent interpretation, plan preview, command validation, explanations, and conversational revision.

**Deliverable:** natural language safely directs the same operations available in the UI.

## Milestone 11 — Mix, export, and release workflow

Build mixer routing, automation, production recipes, stem/WAV export, DAW handoff, project reports, and provenance.

**Deliverable:** a user can finish or hand off a song without losing editability or origin history.

## Practical release slices

### Prototype

Milestones 0–3: prove structured lyrics, time, prosody, and harmony.

### MVP

Milestones 0–5: create and audition a complete editable song demo.

### Artist alpha

Milestones 6–7 and basic vocal takes: prove voice-driven control and instrument intelligence.

### Production beta

Milestones 8–11: rendering, AI direction, vocal production, mixing, and export.

## What not to build early

- Full VST hosting before MIDI generation is musically useful
- A broad AI chat layer before typed commands exist
- Advanced mixing before arrangement and export are stable
- Dozens of genres before one vertical slice works end to end
- Neural final-audio generation as a substitute for the Song Graph

## Recommended first vertical slice

Support one song with `Verse -> Chorus -> Verse -> Chorus`, one meter, a constrained tempo range, one genre profile, lyric prosody candidates, a small chord vocabulary, piano/bass/drums roles, simple preview playback, MIDI export, save/load, locks, and undo/redo.

That slice tests the defining idea: meaning becomes structured music, the artist can revise any layer, and accepted work survives regeneration.
