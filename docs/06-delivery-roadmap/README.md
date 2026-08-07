# 06 — Delivery Roadmap

This is the recommended logical build order for Maskil Forge and its underlying Maskil Engine. The repository currently contains the product and architecture foundation only. A milestone should start only after its dependency gate is reliable.

## Milestone 0 — Decisions and skeleton

Define the supported desktop platforms, initial genre, first renderer, project file format, tick resolution, audio/MIDI libraries, and boundaries between TypeScript, .NET, and native audio code. When implementation begins, use `MaskilForge` as the C# namespace root. Create the solution, tests, CI, schema versioning, and architecture decision records.

**Deliverable:** an empty project opens, saves, reloads, and reports its schema version.

## Milestone 1 — Song foundation

Build the Song Graph, sections, timeline, tempo/meter, tracks, clips, markers, commands, undo/redo, persistence, migrations, and validation.

**Deliverable:** a reliable non-audio song editor.

### Milestone 1.6 â€” Project library and lyric capture

Provide a deliberate welcome screen, discover locally saved projects without requiring a known identifier, capture and save an unstructured lyric draft, and let the artist move between raw writing and direct section editing without destroying either representation. Track last-modified time and protect unsaved work when switching projects.

The current foundation implements the local project summary list, raw lyric draft, direct transition into Song Graph editing, unsaved-work prompts, confirmed recoverable deletion, Trash browsing and restoration, and separately confirmed permanent deletion. Duplication, import/export, automatic structural suggestions, and lyric analysis remain future work.

**Deliverable:** an artist can begin with words rather than a predefined song form, close the application, find the project again, and continue with the raw draft and structured sections intact.

### Next durability slice

The focused `feature/project-durability` slice now provides the migration boundary for schema evolution, compatibility with early schema-v1 files, explicit future-version rejection, validated temporary saves, previous-good-file backups, corrupt-file recovery copies, and structured API errors. Session-only undo remains an explicit current decision; a persistent command journal should not be added casually.

Still planned for later durability work are automatic crash snapshots, recovery-session UI, stale concurrent-session detection, and migrations that transform a real schema version beyond v1. The migration mechanism exists now, but no fictional v2 migration is included before a v2 schema is defined.

## Milestone 2 — Lyrics and prosody

Build the lyric document, token/syllable/stress annotations, beat mapping, breath analysis, rhythm candidates, scoring, locks, and a basic lyric/timeline UI.

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
