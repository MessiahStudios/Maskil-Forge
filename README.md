# Maskil Forge

**Understand the words. Forge the music.**

Maskil Forge is a human-driven procedural songwriting and arrangement engine by Messiah Studios. It is designed for singer-songwriters who know what they want to communicate but may not yet know how the lyrics should fit a genre, how the arrangement should develop, or which instruments can best express the idea.

## Why this project matters

Maskil Forge explores the intersection of music composition, software engineering, and human creativity. Instead of replacing songwriters or generating a lead singer, it provides tools that help artists understand and shape their own creative decisions.

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

Phase 2  ✅  Harmony → editable audible demo
         Harmony candidates and richer analysis
         Arrangement blueprint and instrument roles
         MIDI generation, piano-roll editing, simple preview

Phase 3  ○  Performance and sound
         Voice capture / analysis, instrument intelligence
         Human lead-vocal workflow (guidance, takes, reviewable production)
         Replaceable renderers, VST path, mix, export, and AI director
```

Detail lives in the [delivery roadmap](docs/06-delivery-roadmap/README.md). The sections below stay engineering-accurate for contributors and reviewers.

Delivery is web-first and local-first: one canonical Song Graph serves the browser, a future installable PWA, and any later native desktop shell. Portable project interchange comes before optional accounts or cloud synchronization; native packaging begins only when a proven production requirement exceeds dependable browser capability.

## Why Maskil Forge exists

Many aspiring and independent singer-songwriters begin with lyrics, a vocal idea, or an emotional direction but do not yet know how prosody, harmony, arrangement, orchestration, MIDI, or instrument technique can express it. Maskil Forge is intended to bridge that gap without taking authorship away from the artist. The artist supplies the words, intent, vocal gestures, taste, revisions, and the recorded human lead vocal. Maskil Forge may capture and analyze that vocal, provide pitch, timing, and prosody guidance, create guide melodies, preserve takes and comps, suggest or apply reviewable vocal production settings, and use VST or other audio processing to assist the singer. Voice analysis may also drive editable musical or instrument-performance data. The product must not generate or replace the final lead singer.

## How Maskil Forge differs from AI song generators

Prompt-to-song systems primarily generate a finished audio result. Maskil Forge is designed to construct an editable song project.

The artist supplies the lyrical meaning, creative intent, vocal gestures, choices, revisions, and the recorded human lead vocal. The Maskil Engine is designed to analyze and connect those contributions through explicit song structure, prosody, theory, arrangement, MIDI, instrument behavior, and rendering instructions.

AI may interpret requests and propose alternatives, but the artist remains the author and the lead performer. Preview tones, guide tracks, and processed vocals may assist that performance; they do not replace it.

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
    -> human lead-vocal recording, guidance, and reviewable production
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

Released-song validation continues in slice 5.16. Schema v20 makes Intro a first-class section kind and stores section delivery (spoken, talk-sung, sung, or whispered) plus free-form performance direction. Before absolute-timeline musical parts exist, an artist can duplicate a section: lyrics, harmony, registered voicings, duration, arrangement, and roles are copied with fresh identities, while detailed lyric timing, candidates, locks, and accepted musical parts are intentionally not inferred.

Slice 5.17 adds review-first lyric-sheet structuring. Bracketed section headings are parsed deterministically into an editable proposal, unassigned lines remain visible, and acceptance creates the reviewed sections as one undoable operation while preserving the original raw draft.

Slice 5.18 adds a compact, derived song outline for navigating long forms. It summarizes each section’s delivery, duration, lyric count, and next readiness gap and jumps to the authoritative full editor without creating a second persisted representation.

Slice 5.19 adds a transient focused-section workspace with previous/next navigation and an immediate return to the complete song. Focus state is UI-only and never changes the Song Graph.

Slice 5.20 makes the first absolute-timed musical part an explicit timeline commitment. The editor warns before accepting it, consistently protects structural controls afterward, explains how to unlock them without losing approved notes, requires whole-song harmony and arrangement coverage for journey completion, and numbers repeated imported section titles for unambiguous navigation.

Slice 5.21 turns the derived readiness gap into a contextual next-action button that opens the first incomplete section in focused mode at the lyrics, harmony, arrangement-job, or playable-part workspace it needs.

Slice 5.22 adds an explicit, undoable way to reuse another section’s harmony and arrangement foundation with fresh identities, while preserving the target’s lyrics and performance intent and refusing to overwrite absolute-timed parts.

Slice 5.23 makes unknown bracketed lyric-sheet headings visible structural boundaries: preview lists them for review and keeps their following lines isolated instead of silently merging them into a neighboring recognized section.

Slice 5.24 preserves each unknown heading as an ordered lyric block and lets the artist explicitly map it to a supported section type during preview, without retyping or allowing the parser to guess.

Slice 5.25 adds genre-neutral, artist-authored structural function to each section—setup, development, lift, payoff, contrast, transition, or resolution—while schema-v20 songs migrate to an uninferred “not decided” state.

Slice 5.26 brings those roles into lyric-sheet review before import and surfaces decided roles in the whole-song outline, without allowing the parser or genre metadata to guess them.

Slice 5.27 adds an optional whole-song role review count and a direct path to the next undecided section, without making structural roles part of audible-readiness completion.

Slice 5.28 lets an explicitly started role review continue through open sections after each decided save, while ordinary section-intent editing remains unchanged.

Slice 5.29 makes each role's creative meaning visible and accessible in both structure preview and section editing, with one shared genre-neutral vocabulary.

Slice 5.30 makes similarly titled recovery snapshots identifiable before restore by showing their draft state, section and lyric counts, and ordered song form.

Slice 5.31 requires an explicit, content-aware confirmation before permanently discarding a recovery snapshot, with the safe cancellation action focused first.

Slice 5.32 keeps undo and redo entries intact when reversal or replay fails, while returning domain validation details instead of a generic HTTP 500.

Slice 5.33 prevents invalid deletion of part-owned playable notes and visibly names the musical parts that must release each note first.

Slice 5.34 shows chord-tone and register guidance at voicing entry and rejects invalid pitches, ordering, or range locally before an API command is sent.

Slice 5.35 carries each hear–revise readiness prompt through to its first enabled lyrics, harmony, arrangement-job, or playable-part control instead of stopping at the surrounding workspace.

Slice 5.36 sends note-dependent arrangement jobs to a harmony note sketch when the section has no approved notes yet, while harmony-support and texture still go directly to building a part from existing chords.

Slice 5.37 turns a completed hear–revise review into a direct Hear action that focuses the song transport Play control without starting playback.

Slice 5.38 starts an empty song from the same queue: add the first section, then write into an existing blank lyric line instead of creating another empty one.

Slice 5.39 sends a pasted bracket-headed lyric sheet to structure preview from that queue, instead of the manual add-section toolbar.

Slice 5.40 sends unresolved unknown headings from that preview to the first heading-type control, instead of a disabled Create sections button.

Slice 5.41 invalidates readiness navigation when its lyric-sheet preview no longer matches the edited raw draft, returning the artist to a fresh structure preview instead of a stale Create sections action.

Slice 5.42 collapses the hear–revise readiness card before its action and checklist columns begin to overflow at narrow tablet and wide-phone widths.

Slice 5.43 exports the current validated Song Graph as a deterministic, versioned `.maskil.json` project file without repository paths, recovery state, command history, accounts, or cloud storage.

Slice 5.44 imports artist-owned `.maskil.json` files through the existing schema migration and domain validation pipeline, saves valid projects atomically, and refuses identity collisions without overwriting library, Trash, backup, or recovery data.

Slice 5.45 previews and validates a portable project before changing the library, shows its song anatomy and schema migration, and offers an explicit independent-copy path when its identity is already known.

Slice 5.46 duplicates the explicitly saved version of a library song as an independently named project while preserving its nested creative identities, decisions, and provenance.

Slice 5.47 establishes one production origin for the built web client and local project API, exposes the active project schema through a health contract, and makes loss of the local project service visible without claiming that the cached web shell can edit offline.

Slice 5.48 adds an installable application manifest and versioned static-shell cache while keeping every project API request network-only. The installed interface can reopen from cache and report a disconnected host, but it cannot open, edit, recover, or save projects offline yet.

Slice 5.49 adds a browser recovery vault in IndexedDB. Dirty editor state is protected locally before the host recovery request runs, retained across a host interruption, surfaced honestly by the cached shell, and returned to the existing revision-checked recovery flow after reconnection. It does not make the project library or editor offline-capable.

Slice 5.50 caches the exact project state at explicit create, open, import, and save boundaries for device-local, view-only offline review. The cached shell can show raw lyrics, ordered song sections, delivery, performance direction, and structured lyric lines without exposing edit or save controls; reconnecting reopens the host-owned song normally. These browser snapshots are not a synchronized library, an authoritative save, or an offline editor.

Slice 5.51 treats recovery as a queue of unique songs instead of a raw count of host and browser copies. It shows five recent songs first, uses ten as a non-destructive attention threshold, labels work stale after 30 days, preserves distinct restore choices when both sources exist, and requires content-aware confirmation before discarding one song or all stale work. No cap silently evicts unsaved lyrics.

Slice 5.52 keeps the saved-song library uncapped while making larger collections easier to navigate. Artists can search by title or artist, filter structured songs, raw drafts, and empty starts, expand beyond the twelve most recent matches, and explicitly select empty starts for a content-aware bulk move to reversible Trash. Saved songs are never automatically selected, moved, or permanently deleted.

Slice 5.53 keeps Trash uncapped and artist-controlled while adding title-or-artist search, a twelve-result collapsed view, visible age labels, and explicit multi-song restore or permanent-delete review. Thirty-day labels are reminders rather than expiry rules, selection always begins empty, and permanent deletion still requires a content-aware final confirmation.

Slice 5.54 narrows the phone-width creator journey to Idea, Words, Shape, Review, and Approve. The phone path hides harmony, arrangement, MIDI, and note-event tooling, reviews the written song without changing it, and saves the capture while stating that music work and vocal capture continue on a larger screen.

Slice 5.55 keeps that phone path reachable by compacting the sticky editor header to title and Save, moving Undo and Redo into Project, and hiding the duplicate journey checklist so the next capture action is not covered.

Slice 5.56 keeps artist, genre, and description on that phone path while hiding tempo, meter, key, and developer details, and it compacts the connected-host status banner to a title until reconnect or update detail is needed.

Slice 5.57 keeps section titles, order, role, and lyrics on that phone Shape path while hiding bar length, delivery, and performance direction so a verse can be written without becoming a timeline or staging editor.

Slice 5.58 puts the lyric editor first on that phone section card, collapses role into an optional disclosure, hides role-review chrome, and keeps new lyric locks on desktop so writing a line is the next reachable action.

Slice 5.59 keeps that lyric field on the first phone screen by hiding the connected-host banner, duplicate draft link, Shape title, one-section outline, and readiness checklist while leaving the add-section toolbar and next-action button.

Slice 5.60 replaces that six-button phone toolbar with one Add section disclosure so choosing Verse, Chorus, or another kind does not cover the lyric field.

Slice 5.61 puts the raw lyric draft on the first phone Words screen by hiding the long capture lecture, duplicate Save draft and Shape manually actions, and the preservation footnote, while keeping Preview song structure for pasted lyric sheets.

This repository contains the product definition, architectural principles, delivery roadmap, and an executable songwriting prototype. The Prototype boundary and editable-demo MVP are complete through slice 5.15. The application spans idea capture, structured lyrics and prosody, timeline, harmony and voicing, arrangement intent, playable notes, MIDI export, role-aware musical parts, deterministic role realizations through accents, assembled-part audition, basic song transport, minimal note/part editing, and derived hear–revise readiness review. Additional role realization should follow only when artist validation shows that a vertical song needs it.

The schema-v2 timeline foundation uses 480 pulses per quarter note (PPQ), converts between bar/beat/tick positions and absolute ticks, and gives every ordered section a stable timeline placement and editable bar duration. Section edits reflow these placements without changing section identities. This is a musical coordinate system only; it does not provide transport, playback, MIDI generation, or audio timing.

The schema-v3 lyric-document foundation tokenizes structured lyric lines into individually addressable words while preserving the original line text. Unchanged words retain their identifiers when nearby words are inserted or removed. Schema v4 adds ordered syllable entities with stable IDs and `Manual`, `Analyzer`, or `Imported` provenance. Schema v5 adds addressable punctuation and ordered phrases that reference existing word IDs. Schema v6 adds optional syllable stress marks with `None`, `Secondary`, `Primary`, or `Emphasized` levels and explicit provenance. Schema v7 adds optional phrase-relative prosodic patterns whose identified units reference existing syllable IDs and record `Weak`, `Neutral`, or `Strong` weight. Schema v8 adds stable syllable placements at artist-selected bar, beat, and tick coordinates relative to the owning section. Schema v9 lets artists preserve multiple named rhythm options by snapshotting a phrase's current placements, compare those options, and explicitly apply one back to the authoritative beat map. Schema v10 adds optional breath points after existing syllables with stable identities and provenance. Derived prosody scoring can review active placements or saved rhythm options for stress conflicts, breath room, and crowding, but those scores are not stored as project schema fields. Schema v11 adds creative locks for lyric lines and phrase rhythm so accepted wording or timing can be protected from silent overwrite. A derived lyric-timeline view projects those placements onto absolute song time so the editor can show how lyrics fit the section timeline and optionally overlay a saved rhythm option. Schema v12 adds a song-level musical key (tonic, accidental, mode) with deterministic pitch-class, scale, interval, and small-chord theory helpers. Schema v13 adds ordered section harmony chords with stable identities, chord symbols, section-relative start positions, bar durations, and provenance. Schema v14 lets artists preserve multiple named harmony options and explicitly apply one to the active progression. Schema v15 adds optional registered chord voicings with stable voicing and voice identities, spelled notes, octaves, provenance, and configurable register bounds. A derived chord-movement review now uses registered voices when both chords provide them and explains retained notes, leaps, spacing, voice-count changes, and similar-direction perfect intervals; unvoiced chords retain the earlier pitch-class review. The editor does not generate or rank candidates, invent voicings, invent breaths from punctuation, invent locks, invent progressions, or automatically accept a choice.

Schema v16 adds a song-level arrangement blueprint of stable section plans. Artists can describe each existing section with explicit energy and density intentions and view the resulting energy curve without selecting instruments or generating performances. Schema v17 adds stable, artist-authored role assignments so sections can request musical jobs such as pulse, texture, low-end support, transitions, or hook reinforcement without naming instruments. Schema v18 adds stable playable note events with registered pitch, absolute start tick, duration, and velocity. Schema v19 adds stable, artist-authored musical parts that connect an assigned section role to selected approved note IDs without generating material or choosing an instrument. Existing harmony can be projected into a transient playable-note sketch: registered voicings remain authoritative, missing voicings are labeled as temporary previews, and notes enter the Song Graph only after the artist explicitly accepts the sketch. A section marked for low-end support can now preview a deterministic lower-register layer derived from the lowest approved note at each onset; acceptance creates the needed notes and their role-aware part as one reversible decision. A section marked for pulse can preview short mid-register hits on each approved onset; acceptance likewise creates the needed notes and pulse part as one reversible decision. A section marked for harmony support can preview the same chord-and-voicing projection as the playable sketch and accept it as a harmony-support musical part, reusing matching approved notes when present. A section marked for texture can preview the upper half of each chord’s voices as softer sustained color and accept that as a texture musical part, likewise reusing matching notes. A section marked for hook reinforcement can preview beat-capped, emphasized hits on the highest approved note at each onset and accept that as a hook-reinforcement musical part. A section marked for countermelody can preview softer response notes on the second-highest approved pitch at stacked onsets and accept that as a countermelody musical part. A section marked for accents can preview short, strong hits on the highest approved note at each bar downbeat and accept that as an accents musical part. Once musical parts exist, the arrangement workspace can audition their assembled notes together with a transient Web Audio preview that does not change the Song Graph. A basic song transport can also play those assembled notes from the absolute timeline with a live bar/beat playhead. The advanced editor can revise an approved note’s pitch, onset, duration, and velocity and can rename parts or change their approved-note membership while preserving stable identities and undo/redo. Approved notes can be exported as a format-0 Standard MIDI File that preserves the existing PPQ, tempo, meter, pitch, timing, duration, and velocity without inventing notes or instruments. The browser editor can also audition an existing section progression with simple generated tones. It follows the saved tempo and harmony timing, prefers registered voicings, and uses temporary preview voicings when necessary; playback remains transient and does not change the Song Graph or activity history. Maskil Forge remains early-stage: it is not a functional DAW or complete audio generator. Automatic lyric analysis, realization of other arrangement roles, instrument assignment, VST hosting, vocal analysis, procedural music generation, recording, and mixing have not been implemented.

Undo and redo history is currently session-only. Section edits, manual phrase split/join actions, syllable stress decisions, prosodic-weight decisions, syllable placement decisions, rhythm-option capture, rename, removal, and application, breath-point decisions, creative lock/unlock decisions, song-key changes, harmony-chord edits, harmony-option capture, rename, removal, and application, section arrangement decisions, arrangement-role assignments, playable-note edits, and musical-part edits participate in that history. Redo restores the same phrase, pattern, prosodic-unit, placement, candidate, candidate-event, breath-point, lock, harmony-chord, harmony-candidate, harmony-candidate-event, section-arrangement, role-assignment, note-event, and musical-part identities; undo restores the exact prior values and provenance. Saved project content survives closing and reopening, but the command history itself does not.

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

To run the production-style single-origin host, build the web client first and then open the API origin:

```powershell
cd src/MaskilForge.Web
npm run build
cd ../..
dotnet run --project src/MaskilForge.Api --urls http://localhost:5072
```

Open `http://localhost:5072`. A later `dotnet publish` includes the already-built web distribution in its `wwwroot` output; it does not run npm implicitly.

GitHub Actions runs the .NET build, xUnit suite, dependency-locked Vue install, and Vue production build for pull requests and pushes to `main`.

## Documentation

See the [documentation index](docs/README.md) for the complete progression, or read these in order:

1. [Product vision](docs/00-product-vision/README.md) - identity, audience, responsibilities, and boundaries.
2. [System foundations](docs/01-system-foundations/README.md) - Song Graph, timeline, commands, events, constraints, and scoring.
3. [Lyrics and musical meaning](docs/02-lyrics-and-meaning/README.md) - lyrics, prosody, narrative, harmony, and energy.
4. [Composition and arrangement](docs/03-composition-and-arrangement/README.md) - genre data, instrument roles, generators, and MIDI.
5. [Performance and sound](docs/04-performance-and-sound/README.md) - human lead-vocal workflow, voice capture, retargeting, rendering, and mixing.
6. [AI director and product workflow](docs/05-ai-director/README.md) - natural-language direction over deterministic tools.
7. [Delivery roadmap](docs/06-delivery-roadmap/README.md) - build order, milestones, dependencies, and completion gates.

## Publisher

Maskil Forge is a project by **Messiah Studios**.
