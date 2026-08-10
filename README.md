# Maskil Forge

**Understand the words. Forge the music.**

Maskil Forge is a human-driven procedural songwriting and arrangement engine by Messiah Studios. It is designed for singer-songwriters who know what they want to communicate but may not yet know how the lyrics should fit a genre, how the arrangement should develop, or which instruments can best express the idea.

## Why this project matters

Maskil Forge explores the intersection of music composition, software engineering, and human creativity. Instead of replacing musicians with generated audio, it provides tools that help artists understand and shape their own creative decisions.

The product sits closer to a **DAW workflow + composition assistant + music theory engine** than to “type a prompt → get an MP3.” The central engineering claim is:

> A song exists as structured, editable data before it exists as finished audio.

That means lyrics, syllables, timing, locks, and theory live as inspectable project state—so later MIDI, arrangement, and rendering can revise individual layers without discarding the artist’s authorship.

### Build phases

```text
Phase 1  ✅  Song foundations
         Song Graph, project library, lyric capture
         Timeline (PPQ), sections, persistence, undo/redo
         Syllables, phrases, stress, prosody, beat mapping
         Rhythm options, breath marks, scoring, creative locks
         Lyric timeline UI, musical key / theory primitives
         Section harmony progressions (timed chords)

Phase 2  🚧  Harmony → audible demo
         Harmony candidates and richer analysis
         Arrangement blueprint and instrument roles
         MIDI generation, piano-roll editing, simple preview

Phase 3  ○  Performance and sound
         Voice capture / analysis, instrument intelligence
         Replaceable renderers, VST path, vocal production
         Mix, export, and AI director over the same Song Graph
```

Detail lives in the [delivery roadmap](docs/06-delivery-roadmap/README.md). The sections below stay engineering-accurate for contributors and reviewers.

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

This repository contains the product definition, architectural principles, delivery roadmap, and an executable songwriting prototype. The Prototype boundary is complete, and Milestone 5 is in progress at its 5.5 foundation checkpoint. The application now spans idea capture, structured lyrics and prosody, timeline, harmony and voicing, arrangement intent, playable notes, MIDI export, role-aware musical parts, and the first deterministic low-end support realization. It is not yet the complete editable-song-demo MVP: additional role realization, assembled-part audition, basic transport, minimal editing, and end-to-end validation remain.

The schema-v2 timeline foundation uses 480 pulses per quarter note (PPQ), converts between bar/beat/tick positions and absolute ticks, and gives every ordered section a stable timeline placement and editable bar duration. Section edits reflow these placements without changing section identities. This is a musical coordinate system only; it does not provide transport, playback, MIDI generation, or audio timing.

The schema-v3 lyric-document foundation tokenizes structured lyric lines into individually addressable words while preserving the original line text. Unchanged words retain their identifiers when nearby words are inserted or removed. Schema v4 adds ordered syllable entities with stable IDs and `Manual`, `Analyzer`, or `Imported` provenance. Schema v5 adds addressable punctuation and ordered phrases that reference existing word IDs. Schema v6 adds optional syllable stress marks with `None`, `Secondary`, `Primary`, or `Emphasized` levels and explicit provenance. Schema v7 adds optional phrase-relative prosodic patterns whose identified units reference existing syllable IDs and record `Weak`, `Neutral`, or `Strong` weight. Schema v8 adds stable syllable placements at artist-selected bar, beat, and tick coordinates relative to the owning section. Schema v9 lets artists preserve multiple named rhythm options by snapshotting a phrase's current placements, compare those options, and explicitly apply one back to the authoritative beat map. Schema v10 adds optional breath points after existing syllables with stable identities and provenance. Derived prosody scoring can review active placements or saved rhythm options for stress conflicts, breath room, and crowding, but those scores are not stored as project schema fields. Schema v11 adds creative locks for lyric lines and phrase rhythm so accepted wording or timing can be protected from silent overwrite. A derived lyric-timeline view projects those placements onto absolute song time so the editor can show how lyrics fit the section timeline and optionally overlay a saved rhythm option. Schema v12 adds a song-level musical key (tonic, accidental, mode) with deterministic pitch-class, scale, interval, and small-chord theory helpers. Schema v13 adds ordered section harmony chords with stable identities, chord symbols, section-relative start positions, bar durations, and provenance. Schema v14 lets artists preserve multiple named harmony options and explicitly apply one to the active progression. Schema v15 adds optional registered chord voicings with stable voicing and voice identities, spelled notes, octaves, provenance, and configurable register bounds. A derived chord-movement review now uses registered voices when both chords provide them and explains retained notes, leaps, spacing, voice-count changes, and similar-direction perfect intervals; unvoiced chords retain the earlier pitch-class review. The editor does not generate or rank candidates, invent voicings, invent breaths from punctuation, invent locks, invent progressions, or automatically accept a choice.

Schema v16 adds a song-level arrangement blueprint of stable section plans. Artists can describe each existing section with explicit energy and density intentions and view the resulting energy curve without selecting instruments or generating performances. Schema v17 adds stable, artist-authored role assignments so sections can request musical jobs such as pulse, texture, low-end support, transitions, or hook reinforcement without naming instruments. Schema v18 adds stable playable note events with registered pitch, absolute start tick, duration, and velocity. Schema v19 adds stable, artist-authored musical parts that connect an assigned section role to selected approved note IDs without generating material or choosing an instrument. Existing harmony can be projected into a transient playable-note sketch: registered voicings remain authoritative, missing voicings are labeled as temporary previews, and notes enter the Song Graph only after the artist explicitly accepts the sketch. A section marked for low-end support can now preview a deterministic lower-register layer derived from the lowest approved note at each onset; acceptance creates the needed notes and their role-aware part as one reversible decision. Approved notes can be exported as a format-0 Standard MIDI File that preserves the existing PPQ, tempo, meter, pitch, timing, duration, and velocity without inventing notes or instruments. The browser editor can also audition an existing section progression with simple generated tones. It follows the saved tempo and harmony timing, prefers registered voicings, and uses temporary preview voicings when necessary; playback remains transient and does not change the Song Graph or activity history. Maskil Forge remains early-stage: it is not a functional DAW or complete audio generator. Automatic lyric analysis, realization of other arrangement roles, instrument assignment, VST hosting, vocal analysis, procedural music generation, recording, and mixing have not been implemented.

Undo and redo history is currently session-only. Section edits, manual phrase split/join actions, syllable stress decisions, prosodic-weight decisions, syllable placement decisions, rhythm-option capture, rename, removal, and application, breath-point decisions, creative lock/unlock decisions, song-key changes, harmony-chord edits, harmony-option capture, rename, removal, and application, section arrangement decisions, and arrangement-role assignments participate in that history. Redo restores the same phrase, pattern, prosodic-unit, placement, candidate, candidate-event, breath-point, lock, harmony-chord, harmony-candidate, harmony-candidate-event, section-arrangement, and role-assignment identities; undo restores the exact prior values and provenance. Saved project content survives closing and reopening, but the command history itself does not.

Project persistence validates a temporary JSON file before replacing the active copy and retains the previous validated save as an ignored local backup. Invalid or malformed project files are not silently promoted to backups: they are preserved once by content as recovery copies, while healthy songs remain available in the library. Confirmed permanent deletion removes the song's Trash entry, backup, and recovery artifacts. User-facing saved-version history and recovery for future external media assets are not implemented yet.

Schema-v1 project files and recovery snapshots migrate in memory through schemas v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, and v14 when loaded. Their existing tempo, time signature, section identifiers, line identifiers, text, and section order are retained. Migrated sections receive an initial eight-bar placement, existing lyric words receive deterministic identifiers, schema-v3 syllables retain their IDs while receiving ordered positions and manual provenance, schema-v4 lines receive deterministic punctuation IDs and one default phrase, schema-v5 syllables begin with no stress annotation, schema-v6 phrases begin with no prosodic pattern, schema-v7 lines begin with no syllable placements, schema-v8 lines begin with no rhythm candidates, schema-v9 lines begin with no breath points, schema-v10 projects begin with no creative locks, schema-v11 projects receive a default C major key, schema-v12 sections begin with no harmony chords, and schema-v13 sections begin with no harmony candidates. Migration never invents weight, timing, breath, lock, progression, or alternative decisions. The original file is not rewritten until the artist explicitly saves it.

The current session-recovery slice automatically protects dirty editor state in a separate validated snapshot after a short editing pause. On the next startup, the artist can restore or discard that snapshot without overwriting the explicitly saved song. Saves use the last persisted project revision to reject stale browser sessions instead of silently replacing newer work. Recovery snapshots are not version history, and undo/redo remains session-only.

The editor now presents these capabilities through a lightweight creator journey: Idea, Words, Shape, Music, Harmony, and Arrangement. It is guidance rather than a wizard, so an experienced artist can jump directly to an available area. Raw lyric capture remains the first creative workspace, while lyric timing, syllables, phrasing, prosody, and harmony remain fully available through optional, purpose-led disclosures. This is a presentation layer only: journey state is derived from the existing Song Graph and does not add schema fields, change persistence, or make advanced tools required.

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
