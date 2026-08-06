# 01 — System Foundations

This document specifies the planned foundation of Maskil Engine, the procedural framework inside Maskil Forge. Everything else depends on this layer. Intelligent generation should not begin until the project can represent, change, save, and reload a song reliably.

## Canonical Song Graph

`SongProject` will be the aggregate root. It will own or reference:

```text
CreativeIntent  RawLyrics    Sections      Timeline
Harmony         Melody       Rhythm        Tracks
Arrangement     Performances Automation    Rendering
```

All entities use stable IDs such as `SectionId`, `PhraseId`, `TrackId`, `ClipId`, `NoteId`, and `InstrumentId`. Cross-domain relationships reference IDs rather than copying data.

The raw lyric draft and structured lyric lines are separate representations. The draft preserves source material before the artist decides on song form; structured lines provide stable identities and section placement. Moving into structural editing must not destroy the raw source.

Example relationship:

```text
LyricPhrase -> ProsodyPlan -> MelodyPhrase -> HarmonyRegion
            -> ArrangementResponse -> TrackClips -> RenderedAudio
```

## Time model

Define one authoritative musical timeline with:

- Tempo map and time-signature map
- Bars, beats, subdivisions, and absolute ticks
- Sections, markers, clips, and loop regions
- Conversion between ticks, musical positions, and seconds

Musical positions should survive tempo changes. Audio positions need explicit behavior when tempo changes.

## Commands and history

Every meaningful edit is a command, for example:

- `AddSection`
- `ReplaceChord`
- `TransposeSong`
- `GenerateBass`
- `AssignInstrument`
- `ReshapeEnergyCurve`

A command should record its inputs, resulting changes, provenance, and random seed where relevant. This will support undo, redo, reproducibility, and AI explanations.

### Current undo-history decision

Undo and redo history is session-only in the current foundation. The active application session retains reversible section commands, but closing or reloading a project clears that history. Persisting a command journal is a future durability concern and is not implied by project JSON persistence.

## Project schema v1

Project JSON is a durable creative file format rather than a disposable API payload. Schema v1 writes a numeric `schemaVersion`, string `id`, title, creation and modification timestamps, raw lyric draft, sections, tracks, tempo, meter, and metadata. Strongly typed project, section, track, and clip identifiers serialize as strings.

The schema-v1 reader also accepts the original object-shaped schema version (`{ "value": 1 }`) and supplies defaults for raw lyrics and timestamps when loading files created before those fields existed. Future schema changes require explicit migration tests before changing `SchemaVersion.Current`.

Current projects store their complete project tree in one JSON file. Permanent deletion therefore removes that complete current tree. Future external assets such as audio recordings must join the same lifecycle contract before they are introduced.

## Events

Events announce completed changes; they do not secretly own business logic. Examples include `TempoChanged`, `ProsodyUpdated`, `ArrangementRegenerated`, and `PerformanceCaptured`.

## Constraints, locks, and scoring

Generators receive one shared context containing hard constraints, soft preferences, user locks, and scoring weights.

- Hard constraints: playable range, valid time position, locked material
- Soft preferences: genre tendencies, emotional fit, complexity
- Shared scores: theory, prosody, singability, genre, emotion, realism, originality, user preference

Never silently alter locked content. A failed generation should explain which constraints conflict.

## Planned foundation completion gate

This future gate will be met when:

- A project with sections, lyrics, tempo, meter, tracks, and clips can round-trip without data loss.
- Commands undo and redo deterministically.
- IDs and references remain valid after editing.
- Project migrations are versioned.
- Timeline calculations have automated tests.
