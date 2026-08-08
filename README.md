# Maskil Forge

**Understand the words. Forge the music.**

Maskil Forge is a human-driven procedural songwriting and arrangement engine by Messiah Studios. It is designed for singer-songwriters who know what they want to communicate but may not yet know how the lyrics should fit a genre, how the arrangement should develop, or which instruments can best express the idea.

## Why Maskil Forge exists

Many aspiring and independent singer-songwriters begin with lyrics, a vocal idea, or an emotional direction but do not yet know how prosody, harmony, arrangement, orchestration, MIDI, or instrument technique can express it. Maskil Forge is intended to bridge that gap without taking authorship away from the artist. The artist supplies the words, intent, vocal gestures, taste, revisions, and final lead-vocal performance; the system helps turn those decisions into inspectable musical structure.

## How Maskil Forge differs from AI song generators

Prompt-to-song systems primarily generate a finished audio result. Maskil Forge is designed to construct an editable song project.

The artist supplies the lyrical meaning, creative intent, vocal gestures, choices, revisions, and final lead vocal. The Maskil Engine is designed to analyze and connect those contributions through explicit song structure, prosody, theory, arrangement, MIDI, instrument behavior, and rendering instructions.

AI may interpret requests and propose alternatives, but the artist remains the author and performer.

The central rule is:

> A song exists as structured, editable data before it exists as finished audio.

## Core workflow

```text
Artist input
    -> meaning and performance analysis
    -> structured Song Graph
    -> procedural music logic
    -> MIDI and automation
    -> replaceable audio rendering
    -> human vocal recording
    -> mix, export, and revision
```

The repeating creative loop is:

```text
Understand -> Structure -> Generate -> Render -> Listen -> Revise
```

Every stage is intended to remain editable.

## Core architecture

The user-facing application and creative workspace is **Maskil Forge**. Its underlying procedural songwriting framework is **Maskil Engine**. These are two naming layers within one product and repository, not separate products.

```text
Maskil Forge workspace
    -> application commands and history
    -> Maskil Engine
       -> Song Graph and timeline
       -> lyrics, prosody, theory, and narrative
       -> composition, arrangement, and instruments
       -> performance capture and retargeting
    -> MIDI and automation
    -> replaceable renderer
```

Future C# code will use `MaskilForge` as its namespace root. The repository is named `Maskil-Forge`.

## Current project status

This repository contains the product definition, architectural principles, delivery roadmap, and an initial executable songwriting foundation. The current vertical slice supports a local song library, raw lyric drafts, structured projects, ordered sections, individually identified lyric lines and words, JSON persistence, reversible section operations, and a Trash workflow with restore and separately confirmed permanent deletion. Raw drafts remain separate from structured sections so an artist can capture words before deciding how the song is organized.

The schema-v2 timeline foundation uses 480 pulses per quarter note (PPQ), converts between bar/beat/tick positions and absolute ticks, and gives every ordered section a stable timeline placement and editable bar duration. Section edits reflow these placements without changing section identities. This is a musical coordinate system only; it does not provide transport, playback, MIDI generation, or audio timing.

The schema-v3 lyric-document foundation tokenizes structured lyric lines into individually addressable words while preserving the original line text. Unchanged words retain their identifiers when nearby words are inserted or removed. Schema v4 adds ordered syllable entities with stable IDs and `Manual`, `Analyzer`, or `Imported` provenance. Schema v5 adds addressable punctuation and ordered phrases that reference existing word IDs. Schema v6 adds optional syllable stress marks with `None`, `Secondary`, `Primary`, or `Emphasized` levels and explicit provenance. Schema v7 adds optional phrase-relative prosodic patterns whose identified units reference existing syllable IDs and record `Weak`, `Neutral`, or `Strong` weight. Schema v8 adds stable syllable placements at artist-selected bar, beat, and tick coordinates relative to the owning section. Schema v9 lets artists preserve multiple named rhythm options by snapshotting a phrase's current placements, compare those options, and explicitly apply one back to the authoritative beat map. The editor does not generate candidates or automatically accept a choice.

Maskil Forge remains early-stage: it is not a functional DAW or complete audio generator. Automatic lyric analysis, AI direction, MIDI, VST hosting, vocal analysis, procedural music generation, recording, and mixing have not been implemented.

Undo and redo history is currently session-only. Section edits, manual phrase split/join actions, syllable stress decisions, prosodic-weight decisions, syllable placement decisions, and rhythm-option capture, rename, removal, and application participate in that history. Redo restores the same phrase, pattern, prosodic-unit, placement, candidate, and candidate-event identities; undo restores the exact prior values and provenance. Saved project content survives closing and reopening, but the command history itself does not.

Project persistence validates a temporary JSON file before replacing the active copy and retains the previous validated save as an ignored local backup. Invalid or malformed project files are not silently promoted to backups: they are preserved once by content as recovery copies, while healthy songs remain available in the library. Confirmed permanent deletion removes the song's Trash entry, backup, and recovery artifacts. User-facing saved-version history and recovery for future external media assets are not implemented yet.

Schema-v1 project files and recovery snapshots migrate in memory through schemas v2, v3, v4, v5, v6, v7, v8, and v9 when loaded. Their existing tempo, time signature, section identifiers, line identifiers, text, and section order are retained. Migrated sections receive an initial eight-bar placement, existing lyric words receive deterministic identifiers, schema-v3 syllables retain their IDs while receiving ordered positions and manual provenance, schema-v4 lines receive deterministic punctuation IDs and one default phrase, schema-v5 syllables begin with no stress annotation, schema-v6 phrases begin with no prosodic pattern, schema-v7 lines begin with no syllable placements, and schema-v8 lines begin with no rhythm candidates. Migration never invents weight, timing, or alternative decisions. The original file is not rewritten until the artist explicitly saves it.

The current session-recovery slice automatically protects dirty editor state in a separate validated snapshot after a short editing pause. On the next startup, the artist can restore or discard that snapshot without overwriting the explicitly saved song. Saves use the last persisted project revision to reject stale browser sessions instead of silently replacing newer work. Recovery snapshots are not version history, and undo/redo remains session-only.

## Run the foundation locally

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 22 or later with npm

From the repository root, restore and run the API:

```powershell
dotnet restore MaskilForge.sln
dotnet run --project src/MaskilForge.Api --urls http://localhost:5072
```

In a second terminal, install the web dependencies and run the Vue development server:

```powershell
cd src/MaskilForge.Web
npm install
npm run dev
```

Open `http://localhost:5173`. Project JSON files are written to the API's ignored `App_Data/projects` directory. To verify production builds and tests:

```powershell
dotnet build MaskilForge.sln
dotnet test MaskilForge.sln
cd src/MaskilForge.Web
npm run build
```

GitHub Actions runs the .NET build, xUnit suite, dependency-locked Vue install, and Vue production build for pull requests and pushes to `main`.

## Documentation

See the [documentation index](docs/README.md) for the complete progression, or read these in order:

1. [Product vision](docs/00-product-vision/README.md) - identity, audience, responsibilities, and boundaries.
2. [System foundations](docs/01-system-foundations/README.md) - Song Graph, timeline, commands, events, constraints, and scoring.
3. [Lyrics and musical meaning](docs/02-lyrics-and-meaning/README.md) - lyrics, prosody, narrative, harmony, and energy.
4. [Composition and arrangement](docs/03-composition-and-arrangement/README.md) - genre data, instrument roles, generators, and MIDI.
5. [Performance and sound](docs/04-performance-and-sound/README.md) - voice capture, retargeting, rendering, recording, and mixing.
6. [AI director and product workflow](docs/05-ai-director/README.md) - natural-language direction over deterministic tools.
7. [Delivery roadmap](docs/06-delivery-roadmap/README.md) - build order, milestones, dependencies, and completion gates.

## Publisher

Maskil Forge is a project by **Messiah Studios**.
