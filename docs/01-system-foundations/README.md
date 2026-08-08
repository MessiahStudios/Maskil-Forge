# 01 — System Foundations

This document specifies the planned foundation of Maskil Engine, the procedural framework inside Maskil Forge. Everything else depends on this layer. Intelligent generation should not begin until the project can represent, change, save, and reload a song reliably.

Architecture decisions:

- [ADR-0001 — Human Creative Authority](ADR-0001-human-creative-authority.md)
- [ADR-0002 — Musical Timeline Resolution](ADR-0002-musical-timeline-resolution.md)

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

The current authoritative timeline uses 480 pulses per quarter note (PPQ). A `MusicalPosition` uses one-based bars and beats plus a zero-based tick within the beat. The timeline converts these positions to and from absolute ticks under the current meter.

`SongTimeline` owns a `TempoMap`, `TimeSignatureMap`, and ordered `SectionPlacement` values. The current slice intentionally supports one tempo and one time signature at beat zero. Sections begin on bar boundaries, receive an eight-bar default duration, and reflow contiguously when added, removed, reordered, or resized. Each placement references its existing `SectionId`.

Future slices may add multiple tempo and meter regions, markers, clips, loop regions, conversion to seconds, and explicit audio behavior when tempo changes. The current timeline does not implement those capabilities, MIDI, transport, or playback.

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

## Project schema versions

Project JSON is a durable creative file format rather than a disposable API payload. Schema v5 writes a numeric `schemaVersion`, string `id`, title, creation and modification timestamps, raw lyric draft, sections, tracks, timeline, structured lyric words, ordered syllables with provenance, addressable punctuation, word-referencing phrases, and metadata. The timeline contains its resolution, tempo map, time-signature map, and section placements. Strongly typed project, section, lyric line, word, syllable, punctuation, phrase, track, and clip identifiers serialize as strings.

The schema-v1 reader accepts both numeric and original object-shaped schema versions (`{ "value": 1 }`) and supplies defaults for raw lyrics and timestamps when loading early files. The v1-to-v2 migration moves the existing tempo and meter into their maps and creates ordered eight-bar placements using the original section IDs. The v2-to-v3 migration tokenizes existing lyric lines and derives deterministic word IDs from the preserved line ID, token order, and text. The v3-to-v4 migration preserves existing syllable IDs and text while adding contiguous zero-based positions and `Manual` provenance. The v4-to-v5 migration derives deterministic punctuation IDs from the preserved line and creates one default phrase referencing all existing word IDs. Migrations run in memory; the source file is not rewritten until an explicit save. Recovery snapshots use the same migration path.

All reads now pass through a version-aware migration pipeline before domain deserialization. A file from a future schema is rejected without modifying it. Malformed JSON, mismatched project identity, and invalid duplicate lyric or clip identifiers are rejected when loaded directly; one content-addressed copy of the original bytes is retained in the ignored `recovery` directory. Library and Trash listing isolate damaged files so healthy projects remain accessible.

Saving uses a temporary file that is flushed and read back through the same validation boundary before it can replace the active project. Before an existing project is promoted to the ignored `backups` directory, it must pass that validation boundary. A damaged active file is retained for recovery without overwriting an existing known-good backup. These artifacts are implementation safeguards, not a user-facing version-history system or a substitute for future crash-recovery snapshots.

Dirty editor state is protected separately from explicit saves. After a short pause in editing, the web client writes a validated recovery snapshot under the ignored project data directory. Startup lists these snapshots before the normal library and lets the artist restore the unsaved state into the editor or discard it without modifying the saved project. A successful explicit save or intentional move to Trash removes the associated snapshot.

Each editor remembers the `LastModifiedUtc` revision it originally loaded. Snapshot and explicit-save requests must still match that persisted revision. A conflicting save returns `409 stale_session`, protecting newer saved work from an older browser session. This is optimistic concurrency for the current local API; it is not multi-user collaboration or a persistent command journal.

Current projects store their complete project tree in one JSON file. Confirmed permanent deletion removes its Trash file and any matching backup and recovery artifacts. Future external assets such as audio recordings must join the same lifecycle contract before they are introduced.

## Events

Events announce completed changes; they do not secretly own business logic. Examples include `TempoChanged`, `ProsodyUpdated`, `ArrangementRegenerated`, and `PerformanceCaptured`.

## Constraints, locks, and scoring

Generators receive one shared context containing hard constraints, soft preferences, user locks, and scoring weights.

- Hard constraints: playable range, valid time position, locked material
- Soft preferences: genre tendencies, emotional fit, complexity
- Shared scores: theory, prosody, singability, genre, emotion, realism, originality, user preference

Never silently alter locked content. A failed generation should explain which constraints conflict.

## Foundation completion gate

The current foundation demonstrates that:

- A project with sections, lyrics, tempo, meter, tracks, and clips can round-trip without data loss.
- Commands undo and redo deterministically.
- IDs and references remain valid after editing.
- The schema-v1-to-v2 migration retains musical settings, section IDs, and order.
- Timeline coordinate conversion and section reflow have automated tests.
