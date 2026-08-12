# 06 — Delivery Roadmap

This is the recommended logical build order for Maskil Forge and its underlying Maskil Engine. The repository has completed the Prototype boundary and the editable-demo MVP through end-to-end creator validation (slice 5.15). A milestone should start only after its dependency gate is reliable.

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

Automatic syllable extraction, automatic stress detection, rhyme, breath points, candidate generation, locks, and scoring remain future Milestone 2 slices. Artist-authored stress representation is delivered separately in Milestone 2.4, manual beat anchors in Milestone 2.6, and manually captured rhythm options in Milestone 2.7.

**Deliverable:** Maskil Forge can identify and preserve the individual words that later lyric intelligence will analyze.

### Milestone 2.2 — Syllable foundation

The current slice makes syllables ordered entities with stable identifiers and explicit `Manual`, `Analyzer`, or `Imported` provenance. The editor lets the artist enter and correct boundaries with a visible separator, treats those corrections as authoritative, and retains matching IDs when surrounding boundaries shift. Schema-v3 songs and recovery snapshots migrate in memory to schema v4 without changing existing syllable IDs.

No automatic pronunciation or syllabification service is implemented yet. Stress, breath points, beat placement, melisma, scoring, and AI assistance remain later focused slices.

**Deliverable:** an artist can define how each word is intended to be sung, save it, reload it, and retain the same ordered syllable identities and provenance.

### Milestone 2.3 — Phrase foundation

The current slice identifies punctuation without removing it from the original lyric text and represents each phrase as an ordered reference to existing word IDs. New and schema-v4 lines begin as one default phrase. Artists can add phrase breaks or join adjacent phrases with explicit, readable controls; these decisions receive `Manual` provenance and participate in session undo/redo without changing phrase identities during redo. Phrase and punctuation identities survive save/load and compatible nearby text edits. Schema-v4 songs and recovery snapshots migrate deterministically to schema v5.

Punctuation does not automatically imply a breath or phrase break. Phrase meaning, automatic emphasis or stress detection, breath recommendations, automatic boundary suggestions, and candidate generation remain future work. Manual beat anchors are delivered separately in Milestone 2.6, and manually captured rhythm options in Milestone 2.7.

**Deliverable:** an artist can group a lyric line into meaningful sung ideas without changing its words, and those groupings remain stable, editable project data.

### Milestone 2.4 — Stress foundation

The current slice adds optional stress marks to existing syllable identities. Artists can explicitly choose `None`, `Secondary`, `Primary`, or `Emphasized`; every editor decision receives `Manual` provenance. An unmarked syllable remains distinct from an explicit no-stress decision. Matching syllables keep their marks across compatible boundary and line edits, save/load preserves the annotation, and session undo/redo restores the exact prior level and provenance. Schema-v5 songs and recovery snapshots migrate in memory to schema v6 with syllables left unmarked.

No analyzer currently assigns stress. Vocal analysis, emotional scoring, genre prediction, melody generation, rhythm suggestions, beat placement, AI, MIDI, playback, and audio remain outside this slice.

**Deliverable:** an artist can record which sung syllables carry weight, revise that decision safely, and preserve it as structured creative intent.

### Milestone 2.5 — Prosody foundation

The current slice adds an optional phrase-relative prosodic pattern with stable pattern and unit identifiers. Each unit references an existing syllable ID and records `Weak`, `Neutral`, or `Strong` weight plus `Manual`, `Analyzer`, or `Imported` provenance. The editor creates manual decisions only and leaves unmapped syllables explicitly undecided. Stress remains a separate syllable annotation; the engine does not infer prosodic weight from it.

Compatible lyric and syllable edits preserve surviving units. Phrase split partitions existing units, phrase join recombines them in syllable order, and neither operation invents weights. Save/load and session undo/redo preserve exact pattern and unit identities and provenance. Schema-v6 songs and recovery snapshots migrate in memory to schema v7 with phrases left without a prosodic pattern.

Automatic prosody detection, natural-language scoring, breath analysis, automatic beat mapping, generated rhythm candidates, melodic contour, AI, MIDI, playback, and audio remain outside this slice. Manual beat anchors are delivered separately in Milestone 2.6, and manually captured rhythm options in Milestone 2.7.

**Deliverable:** an artist can describe the relative weight of chosen syllables inside a phrase as stable, editable data without committing them to musical time.

### Milestone 2.6 — Beat mapping foundation

The current slice connects selected syllables to section-relative musical coordinates through stable `SyllablePlacementId` values. A `BeatPosition` identifies the bar, beat, and PPQ tick within the owning section; the existing timeline resolves that anchor to an absolute song position. Moving a section therefore moves its lyric timing without rewriting the artist's internal phrase placement.

Artists can place, move, and clear individual syllable anchors. Placements record `Manual`, `Analyzer`, or `Imported` provenance, while the current editor creates manual data only. Domain boundaries reject coordinates outside the section or meter, prevent lyric order from moving backward in musical time, and stop section or meter edits from invalidating accepted anchors. Compatible lyric and syllable edits preserve surviving placement IDs; save/load and session undo/redo preserve exact identities and provenance. Schema-v7 songs and recovery snapshots migrate in memory to schema v8 with all syllables left unplaced.

No automatic beat assignment, rhythm generation, note duration, melisma, breath analysis, generated candidates, melody, harmony, AI, MIDI, transport, playback, or audio is included. Manual candidate representation is delivered separately in Milestone 2.7.

**Deliverable:** an artist can state exactly where a chosen syllable occurs in the song's musical coordinate system without generating a performance.

### Milestone 2.7 — Rhythm candidate foundation

The current slice lets an artist capture the active beat placements for one phrase as a named rhythm option. Stable `RhythmCandidateId` and `RhythmCandidateEventId` values preserve each possibility without copying lyric text: every event references an existing syllable ID and stores an alternative section-relative onset. Multiple options may coexist, and the artist explicitly chooses when to apply one back to the active syllable placements.

Capture, rename, removal, and application participate in session undo/redo with exact identity restoration. Compatible lyric and syllable edits retain surviving events; phrase splits partition options and phrase joins re-associate them with the surviving phrase. Section duration and meter changes cannot invalidate saved options. Schema-v8 projects and recovery snapshots migrate in memory to schema v9 with an empty candidate collection, so migration never invents alternatives.

This slice does not generate, rank, score, audition, or automatically accept rhythm. It adds no duration, rests, melisma, breath analysis, locks, melody, harmony, AI, MIDI, transport, playback, or audio.

**Deliverable:** an artist can preserve, compare, and deliberately apply multiple onset possibilities for a phrase while the active beat map remains authoritative.

### Milestone 2.8 — Breath point foundation

The current slice adds optional inhale marks after existing syllables. Stable `BreathPointId` values reference one syllable through `AfterSyllableId` and record `Manual`, `Analyzer`, or `Imported` provenance. The editor creates manual decisions only. Absence means undecided; punctuation and phrase breaks never invent a breath. Compatible lyric and syllable edits preserve surviving identities, save/load retains exact marks, and session undo/redo restores identity and provenance. Schema-v9 songs and recovery snapshots migrate in memory to schema v10 with empty breath collections.

No automatic breath analysis, timed breath placement, scoring, locks, duration, melisma, melody, harmony, AI, MIDI, transport, playback, or audio is included.

**Deliverable:** an artist can mark where they intend to breathe, revise that decision safely, and preserve it as structured creative intent.

### Milestone 2.9 — Prosody scoring foundation

The current slice adds derived, deterministic prosody review for a phrase's active placements or a saved rhythm option. Scores cover stress conflicts, breath room, and crowding, and each reduced score includes inspectable findings that explain why. Review is available from the editor without writing score data into the project schema; punctuation still never invents a breath, and no option is accepted automatically.

No locks, audition, automatic candidate generation, timed breath placement, duration, melisma, melody, harmony, AI, MIDI, transport, playback, or audio is included.

**Deliverable:** an artist can compare rhythm options with explicit reasons for stress, breath, and crowding concerns.

### Milestone 2.10 — Creative lock foundation

The current slice adds durable artist locks for lyric lines and phrase rhythm. Stable `CreativeLockId` values record `LyricLine` or `PhraseRhythm` scope with `Manual`, `Analyzer`, or `Imported` provenance. Locked lyric lines reject word, syllable, stress, prosody, breath, and phrase-boundary edits. Locked phrase rhythm rejects placement changes and applying a rhythm option, while capture and review remain available. Session undo/redo restores exact lock identities. Schema-v10 projects and recovery snapshots migrate in memory to schema v11 with an empty lock collection.

No automatic locking, chord locks, audition, generation, melody, harmony, AI, MIDI, transport, playback, or audio is included.

**Deliverable:** an artist can protect accepted lyric wording or phrase timing so later edits and regeneration cannot silently overwrite them.

### Milestone 2.11 — Lyric timeline UI foundation

The current slice projects existing section placements and syllable anchors onto a derived song-timeline view. `LyricTimelineProjector` resolves section-relative beat positions to absolute ticks and song bars, exposes active placements, optional breath-after marks, and an optional rhythm-candidate overlay, and never writes those marks into the project schema. The structure editor shows a horizontal lyric timeline above the section list: section spans, bar ticks, clickable syllable markers, and jump-to-control selection. Comparing a saved rhythm option draws dashed ghost markers beside the authoritative placements.

No duration, melisma, timed breath coordinates, transport, playback, MIDI, generation, or schema bump is included. Scores and locks remain separate slices.

**Deliverable:** the app explains and demonstrates how lyrics fit musical time.

### Milestone 3.1 — Theory primitives foundation

The current slice adds deterministic music-theory primitives: pitch class, note spelling, intervals, major/natural-minor scales, a small chord vocabulary (major, minor, diminished, augmented, dominant seventh), transposition helpers, and an editable song `MusicalKey`. Schema v12 stores the song key (default C major). Setting the key participates in session undo/redo. Schema-v11 projects and recovery snapshots migrate in memory to schema v12 with C major when no key is present.

No chord progressions, Roman-numeral analysis, voice leading, range checks against instruments, harmony candidates, audition, MIDI, transport, playback, or audio is included.

**Deliverable:** Maskil Forge can name and transpose tonal materials and record the song's key as durable creative state.

### Milestone 3.2 — Harmony progression foundation

The current slice stores ordered, identified section harmony chords: each chord has a stable `HarmonyChordId`, a `ChordSymbol`, a section-relative start `BeatPosition`, a bar duration, and provenance. Artists can add, edit, and remove chords with session undo/redo. Derived Roman-numeral labels are available from theory helpers for diatonic matches but are not stored. Schema v13 adds an empty `harmony` collection on each section; schema-v12 projects migrate in memory without inventing chords.

No progression generation, candidate ranking, voice leading, audition, MIDI, transport, playback, or audio is included.

**Deliverable:** an artist can author a section’s chord progression as durable, timed Song Graph data.

### Milestone 3.3 — Harmony candidate foundation

The current slice lets an artist capture a section's active chord progression as a named harmony option. Stable `HarmonyCandidateId` and `HarmonyCandidateEventId` values preserve each alternative independently of the authoritative harmony chords. Multiple options may coexist; rename, removal, and explicit application participate in session undo/redo with exact identity restoration. Applying an option preserves compatible active chord identities. Schema v14 adds an empty `harmonyCandidates` collection to every section, and schema-v13 projects migrate in memory without inventing alternatives.

This slice does not generate, rank, score, audition, or automatically accept harmony. Voice leading, instrument range checks, MIDI, transport, playback, and audio remain later slices.

**Deliverable:** an artist can preserve and explicitly compare multiple durable chord progressions for a section.

### Milestone 3.4 — Voice-leading analysis foundation

The current slice begins a derived, non-persistent review of adjacent harmony chords. It measures shared pitch classes, shortest circular root motion, and average nearest chord-tone motion, then classifies each transition as smooth, moderate, or wide. The analysis references stable harmony-chord identities and does not alter or select the artist's progression.

This pitch-class review does not claim octave/register voicings, parallel-motion detection, instrument assignments, range checks, generation, audition, MIDI, playback, or audio.

**Deliverable:** an artist can inspect basic movement between adjacent chord symbols without changing the progression.

### Milestone 3.5 — Voicing and register foundation

The current slice represents how an existing chord symbol is physically arranged as explicit, ordered pitches in musical register. A chord such as C major may therefore retain an artist-controlled voicing such as `C3 G3 C4 E4` without changing the harmony chord's identity or meaning.

The slice should establish stable voicing and voice identities, octave-aware pitch representation, deterministic validation against the owning chord, basic configurable register bounds, artist-controlled editing, provenance, persistence, migration, and undo/redo. It may describe spacing, crossings, out-of-range pitches, or unusual register choices, but it must preserve artist authority rather than silently correcting or replacing a voicing.

Automatic harmony generation, AI chord suggestions, MIDI export, audio playback, piano-roll editing, instrument assignment, and rendering remain outside this slice. Detailed instrument profiles and performance technique belong to later arrangement and instrument-intelligence work; this foundation creates the reusable registered-pitch and range rules those systems will evaluate.

**Deliverable:** an artist can state exactly which registered notes realize a harmony chord and preserve that choice as editable Song Graph data.

### Milestone 3.6 — Advanced voice-leading analysis

The current slice extends the derived harmony review from pitch classes to actual registered voices when both adjacent chords have voicings, while preserving the earlier pitch-class fallback. It analyzes ordinal note movement, retained voices, wide leaps, destination spacing, voice-count changes, and relevant similar-direction perfect intervals. Findings explain musical consequences in approachable language and remain advisory, inspectable, and non-persistent.

This slice does not generate or rank replacement progressions, automatically rewrite artist voicings, claim full contrapuntal analysis, assign instruments, or add playback, MIDI, or audio.

**Deliverable:** an artist can understand how smoothly the actual voices move between chords and why a transition feels stable, open, tense, or abrupt.

### Milestone 3.7 — Chord audition foundation

The current slice provides the first narrow audible feedback loop for existing harmony and voicing choices. The harmony workspace can play or stop a section progression through a transient Web Audio preview, following the project's existing tempo, meter, chord positions, and chord durations. Registered voicings take priority; chords without them use clearly identified temporary preview voicings.

Playback state, generated tones, and preview voicings are not stored or written to activity history. This slice does not add MIDI, recording, a piano roll, transport state, instrument selection, audio files, backend audio work, or schema changes.

**Deliverable:** an artist can hear a deterministic, non-destructive representation of the harmony they already created without needing to understand the underlying theory.

## Milestone 3 — Theory and harmony

Build music primitives, keys/scales/chords, progressions, transposition, voice leading, range checks, harmony candidates, and simple chord audition.

**Deliverable:** approved prosody sits over valid, editable harmony.

## Milestone 4 — Arrangement blueprint

Build energy curves, role-based arrangement, genre and instrument profiles, section contrast, entries/exits, and arrangement candidates.

**Deliverable:** a complete visual song blueprint with instrument roles.

### Milestone 4.1 — Section energy and density foundation

The current slice lets the artist describe each existing section with songwriter-facing energy and density intentions. These plans have stable identities, reference existing section IDs, survive save/load, and participate in undo/redo. Existing schema-v15 projects migrate to an empty arrangement blueprint; the application does not infer creative decisions during migration.

The editor presents the resulting song-level energy curve and makes Arrangement an available workspace. This slice does not assign instruments or roles, generate parts, create notes, change chord audition, export MIDI, or claim that an arrangement has been performed.

**Deliverable:** an artist can see and preserve how the song should rise, fall, open up, or become more crowded before choosing any instruments.

### Milestone 4.2 — Arrangement roles foundation

The current slice lets the artist assign musical jobs to each existing section before selecting instruments. Available roles include foundation, pulse, harmony support, low-end support, texture, accents, transitions, countermelody, and hook reinforcement. Every assignment has a stable identity, references an existing section, records provenance, participates in undo/redo, and survives save/load.

Schema-v16 projects migrate to an empty role-assignment collection; the engine does not infer which roles a section needs. This slice does not recommend or assign instruments, generate performances or notes, score candidates, change playback, or export MIDI.

**Deliverable:** an artist can describe what each section needs musically without knowing orchestration vocabulary or committing to a specific instrument.

### Milestone 4.3 — Creator flow validation

Validate the existing Idea-to-Arrangement journey as a complete beginner workflow before expanding the composition model. Fix only reproduced presentation problems: truthful progress language, clear prerequisite guidance when Harmony or Arrangement has no section to work with, visible destinations, and intentional empty states. Workspace navigation remains unlogged because it does not change the song.

This checkpoint changes no domain model, schema, persistence behavior, migration, or API contract. Advanced tools remain available through the existing progressive disclosures.

**Deliverable:** a new creator can move through the current songwriting path without mistaking unavailable context for a broken control.

## Milestone 5 — MIDI composition and preview

Turn approved musical intent into an audible, editable song demo without silently replacing the songwriter's decisions. Slices 5.1–5.15 provide playable note events, harmony realization, MIDI export, role-aware musical parts, deterministic role realizations through accents, assembled-part audition, basic song transport, minimal note/part editing, and end-to-end readiness review. Full piano-roll and DAW-style editing are later capabilities, not prerequisites for the MVP boundary.

### Milestone 5.1 — MIDI event foundation

Represent a playable note as stable project data: registered pitch, absolute start tick, duration in ticks, and velocity. Note events validate MIDI pitch and velocity boundaries, remain ordered in project time, survive save/load, and participate in undo/redo. Existing schema-v17 projects migrate to an empty note-event collection; migration does not invent musical material.

The browser exposes this foundation only through an advanced disclosure so beginners are not asked to work in raw ticks. This slice does not convert harmony, infer notes from arrangement roles, select instruments, create tracks or clips, generate parts, start playback, add transport or piano-roll editing, or write a `.mid` file.

**Deliverable:** Maskil Forge can preserve an explicit playable note without claiming that it generated, performed, or exported it.

### Milestone 5.2 — Harmony to playable sketch

Project an existing section’s harmony into a transient, inspectable playable-note sketch on the absolute song timeline. Registered chord voicings are used exactly; chords without registered notes receive clearly labeled temporary preview voicings. Preparing or refreshing a sketch does not modify the Song Graph.

Only the explicit **Use this sketch** decision adds stable note events. Acceptance is additive, preserves unrelated artist-authored notes, and is reversible as one undoable operation. This slice does not regenerate notes when harmony later changes, silently replace existing notes, select instruments, create tracks, export MIDI, or add transport and piano-roll editing.

**Deliverable:** the artist can review how existing harmony becomes concrete notes and decide whether those notes belong in the project.

### Milestone 5.3 — Minimal MIDI export

Translate the project's approved playable note events into a format-0 Standard MIDI File using the existing 480-PPQ timeline, tempo, and time signature. Pitch, absolute start tick, duration, and velocity are preserved exactly. Same-tick events are deterministic: note-offs precede note-ons, notes are ordered by pitch, and stable note identity is the final tie-breaker.

Export is transient and does not modify or save the project, create command history, or add activity history. The file contains no invented notes, quantization, program changes, instrument assignments, tracks, generated parts, transport state, or piano-roll data.

**Deliverable:** another music application can open an exact portable representation of the songwriter's approved playable notes.

### Milestone 5.4 — Role-aware musical-part foundation

Add stable, artist-authored musical parts that connect one assigned arrangement role in one section to selected approved note-event IDs. A part explains why existing notes belong in the arrangement without generating notes, choosing an instrument, or changing MIDI timing.

Creating and removing a part is explicit and undoable. Referenced notes and roles remain protected until the part is removed; removing a part leaves its notes intact. Section order, duration, and meter changes also ask the artist to remove parts first so absolute note timing cannot drift away from section intent. Schema v19 migrates existing projects to an empty musical-part collection and does not infer assignments from existing notes or roles.

**Deliverable:** Maskil Forge can preserve the relationship between arrangement purpose and playable material before role realization begins.

### Milestone 5.5 — Low-end support realization

Offer one narrow, deterministic role-aware idea: for each approved-note onset in a section marked **Low-end support**, select the lowest approved note and move that pitch class downward by octaves until it reaches the low register. Existing notes already in that register are reused instead of duplicated. Pitch choices, timing, duration, velocity, and reuse are visible before acceptance.

Preparing or refreshing the idea is transient. Only **Use this idea** creates the necessary stable notes and one role-aware musical part, together as a single undoable decision. The slice does not choose a bass instrument, invent rhythm, alter harmony, replace notes, automatically regenerate after edits, or realize any other arrangement role.

**Deliverable:** the songwriter can hear and export an explainable low-end layer derived from approved material while remaining the final decision-maker.

### Milestone 5.6 — Pulse realization

Offer one narrow, deterministic rhythm idea from approved timing: for each unique approved-note onset in a section marked **Pulse**, place a short mid-register hit (C3) that keeps the section's motion clear. Existing notes that already match that pulse pitch and onset are reused instead of duplicated. Timing, duration, velocity, and reuse are visible before acceptance.

Preparing or refreshing the idea is transient. Only **Use this idea** creates the necessary stable notes and one role-aware musical part, together as a single undoable decision. The slice does not invent onsets beyond approved timing, choose a drum instrument, alter harmony, replace notes, automatically regenerate after edits, or realize any other arrangement role.

**Deliverable:** the songwriter can hear and export an explainable pulse layer derived from approved timing while remaining the final decision-maker.

### Milestone 5.7 — Harmony support realization

Offer one narrow, deterministic harmony-role idea from approved chords and voicings: for a section marked **Harmony**, project the section’s existing harmony through the same playable-note rules as the harmony sketch, then package those notes as a harmony-support musical part. Registered voicings stay authoritative; chords without them use clearly labeled temporary preview voicings. Existing notes that already match pitch, onset, and duration are reused instead of duplicated.

Preparing or refreshing the idea is transient. Only **Use this idea** creates any missing stable notes and one role-aware musical part, together as a single undoable decision. The slice does not invent chords, rewrite voicings, choose an instrument, replace notes, automatically regenerate after edits, or realize any other arrangement role.

**Deliverable:** the songwriter can accept an explainable harmony-support layer derived from approved harmony while remaining the final decision-maker.

### Milestone 5.8 — Texture realization

Offer one narrow, deterministic texture idea from approved chords and voicings: for a section marked **Texture**, project the section’s existing harmony, keep the upper half of each onset’s voices as softer sustained color, and package those notes as a texture musical part. Registered voicings stay authoritative; chords without them use clearly labeled temporary preview voicings. Existing notes that already match pitch, onset, and duration are reused instead of duplicated.

Preparing or refreshing the idea is transient. Only **Use this idea** creates any missing stable notes and one role-aware musical part, together as a single undoable decision. The slice does not invent chords, rewrite voicings, choose an instrument, replace notes, automatically regenerate after edits, or realize any other arrangement role.

**Deliverable:** the songwriter can accept an explainable texture layer derived from approved harmony while remaining the final decision-maker.

### Milestone 5.9 — Hook reinforcement realization

Offer one narrow, deterministic hook idea from approved timing and pitch: for a section marked **Hook reinforcement**, select the highest approved note at each onset, cap its duration at one beat, and emphasize it with a stronger velocity. Existing notes that already match pitch, onset, and that capped duration are reused instead of duplicated.

Preparing or refreshing the idea is transient. Only **Use this idea** creates any missing stable notes and one role-aware musical part, together as a single undoable decision. The slice does not invent melodies beyond approved notes, choose an instrument, replace notes, automatically regenerate after edits, or realize any other arrangement role.

**Deliverable:** the songwriter can accept an explainable hook-reinforcement layer derived from approved material while remaining the final decision-maker.

### Milestone 5.10 — Countermelody realization

Offer one narrow, deterministic supporting-line idea from approved stacked notes: for a section marked **Countermelody**, at each onset that already has two or more approved notes, follow the second-highest pitch as a softer response beneath the top line. Existing notes that already match that pitch, onset, duration, and response velocity are reused instead of duplicated.

Preparing or refreshing the idea is transient. Only **Use this idea** creates any missing stable notes and one role-aware musical part, together as a single undoable decision. The slice does not invent onsets, invent a second voice where only one note exists, choose an instrument, replace notes, automatically regenerate after edits, or realize any other arrangement role.

**Deliverable:** the songwriter can accept an explainable countermelody layer derived from approved material while remaining the final decision-maker.

### Milestone 5.11 — Accent realization

Offer one narrow, deterministic emphasis idea from approved timing: for a section marked **Accents**, select the highest approved note on each bar downbeat and place a short, strong hit there. Existing notes that already match that pitch, onset, accent duration, and velocity are reused instead of duplicated.

Preparing or refreshing the idea is transient. Only **Use this idea** creates any missing stable notes and one role-aware musical part, together as a single undoable decision. The slice does not invent downbeats, mark off-beat material, choose an instrument, replace notes, automatically regenerate after edits, or realize any other arrangement role.

**Deliverable:** the songwriter can accept an explainable accents layer derived from approved material while remaining the final decision-maker.

### Milestone 5.12 — Assembled musical-part audition

Provide a transient Web Audio preview of the notes already connected to musical parts in a section. Matching note IDs are resolved and deduplicated, absolute ticks are converted with the project tempo, and playback starts from the earliest assembled onset. Start and stop are available in the arrangement workspace; the preview does not write project data, command history, or activity history beyond ordinary UI logging.

This slice does not add a transport clock, seeking, pause/resume, looping, instrument selection, schema changes, or piano-roll editing. Orphan approved notes that are not connected to a musical part are not included.

**Deliverable:** an artist can hear how accepted role parts sound together before deciding what to change next.

### Milestone 5.13 — Basic playback transport

Add a song-level play/stop transport over assembled musical-part notes on the absolute timeline. Playback starts from tick zero, keeps a live bar/beat playhead, and stops cleanly without writing project data. Section assembled-part audition remains available as a normalized local preview.

This slice does not add seeking, pause/resume, looping, tempo automation, instrument selection, schema changes, or piano-roll editing.

**Deliverable:** an artist can start and stop song playback while seeing where they are in musical time.

### Milestone 5.14 — Minimal note and part editing

Expose the existing stable note-event update command in the advanced playable-note inspector so pitch, onset, duration, and velocity can be revised without replacing note identity. Let an artist rename a musical part and change which approved in-section notes it contains through one undoable command that likewise preserves the part identity and role. Linked-note edits must continue to satisfy the owning section boundary.

Transport preview now uses peak simultaneous polyphony for gain scaling, so longer songs do not become quieter merely because they contain more notes. Attack and release times contract for very short events to keep Web Audio automation ordered.

This slice does not add a piano roll, drag editing, quantization, instrument assignment, seeking, looping, automation, or schema changes.

**Deliverable:** after hearing an assembled section or song, an artist can revise its exact approved notes and part membership, then listen again without rebuilding the musical part.

### Milestone 5.15 — End-to-end editable-demo validation

Add a derived, non-persistent hear–revise readiness review to the Arrangement workspace. Each section reports whether it has lyrics, harmony, an arrangement job, and a playable musical part whose note references resolve. The review identifies the first artist-actionable gap and declares the structured demo ready only when every section can participate in the audible flow.

An integration test exercises the recommended multi-section vertical path through lyric and harmony authoring, explicit part realization, stable note and part revision, undo/redo, MIDI export, validated JSON persistence, and reopening with identities intact. Browser validation confirms the incomplete-state guidance and layout without runtime warnings or errors.

This slice adds no persisted readiness flags, automatic acceptance, instruments, new realization roles, piano roll, recording, or schema changes. Transition realization remains deferred until a real song demonstrates that it is required rather than merely available to build.

**Deliverable:** Maskil Forge can explain what prevents each section from joining an editable audible demo, and the complete hear–revise–save–export path is covered as one vertical workflow.

### Editable-demo MVP boundary

**Complete.** Minimal editing and end-to-end validation now close the hear–revise loop. Transition realization was not required to prove the vertical workflow and remains a future artist-driven addition. Further work must continue the existing preview-first, explicit-acceptance, reversible-decision model; MVP completion does not imply full piano-roll editing, automatic orchestration, instrument libraries, recording, mixing, or DAW replacement.

### Milestone 5.16 — Released-song structure validation

Use “Essence of Shadows” as the first released-song case study. Add Intro as a first-class section and preserve section delivery (spoken, talk-sung, sung, or whispered) plus concise performance direction in schema v20. Let an artist duplicate a repeated section before musical parts exist, copying lyrics, harmony, registered voicings, duration, arrangement, and roles with fresh identities.

Duplication does not copy detailed lyric timing, saved candidates, creative locks, note events, or musical parts. Once parts exist, duplication is blocked until they are removed so absolute timeline timing cannot silently drift.

**Deliverable:** the complete ten-section form of “Essence of Shadows” can be represented honestly, including its spoken intro, restrained delivery changes, repeated choruses, final chorus, and whispered outro, before deeper musical realization begins.

### Milestone 5.17 — Review-first lyric-sheet structuring

Let an artist paste a familiar bracket-headed lyric sheet and preview a deterministic structural proposal. Recognized headings become editable section types and titles; explicit delivery words and heading directions remain visible for correction. Unassigned lines are reported instead of guessed, and the artist can reorder or remove proposed sections before acceptance.

Creating the reviewed structure is one undoable command with stable identities across redo. The raw lyric sheet remains authoritative source material, and a changed draft invalidates a stale preview.

**Deliverable:** “Essence of Shadows” can move from one pasted lyric sheet to ten reviewed, editable song sections without repetitive line-by-line setup or silent interpretation.

### Milestone 5.18 — Compact song-outline navigation

Add a derived, non-persistent outline above the full section editor so long-form songs remain navigable. Each outline entry preserves song order and summarizes section title, kind, delivery, duration, lyric-line count, and the first readiness gap. Selecting an entry scrolls to and focuses the authoritative full section card; the outline never becomes a second editing model.

The outline remains compact and horizontally scrollable at narrow widths, stays available while moving through a long song, and visually distinguishes sections ready for audible review. It introduces no schema fields, section hiding, or alternative persistence path.

**Deliverable:** an artist working through the ten-section “Essence of Shadows” form can understand the whole song and jump directly to the next section requiring attention.

### Milestone 5.19 — Focused section workspace

Let an artist switch the structure editor between the complete song and one selected section without changing project data. Focused mode retains the compact song outline, provides previous/next section movement, and keeps “Show all” immediately available. Removing or changing songs safely resets transient focus state.

This is a view preference only. It does not create alternate section data, alter persistence, collapse creative decisions, or prevent whole-song review.

**Deliverable:** after navigating a long song, an artist can work deeply on one section without visual overload and move through the complete form sequentially.

### Milestone 5.20 — Timeline commitment boundary UX

Make the transition from flexible song structure to absolute-timed musical parts explicit. Before accepting the first musical part, the artist sees which structural operations will become protected. Once parts exist, the structure workspace explains why section order, length, deletion, duplication, and meter are unavailable and provides a direct route to review and remove parts. Lyrics, harmony, performance intent, section names, and approved notes remain editable.

Whole-song journey progress also requires harmony and arrangement coverage for every section rather than treating work on a single section as completion. Repeated imported section titles are disambiguated in song order so a full released-song form remains easy to navigate.

**Deliverable:** creating the first part is an informed commitment instead of a silent lock, and the UI always exposes a safe path back to structural editing.

### Milestone 5.21 — Actionable readiness queue

Turn the existing derived hear–revise readiness result into direct navigation. The first incomplete section exposes one contextual action that opens that section in focused mode and lands on the workspace required for its next gap: lyrics, harmony, arrangement job, or playable part. The queue remains derived from the Song Graph and never applies creative decisions automatically.

**Deliverable:** an artist can move through a complete multi-section song without repeatedly searching for the next unfinished control.

### Milestone 5.22 — Reusable section foundations

Let an artist explicitly start one section from another section’s musical foundation. The operation replaces harmony, registered voicings, energy, density, and musical jobs with fresh identities in one undoable command. It never copies lyrics, delivery, performance direction, approved notes, or absolute-timed musical parts, and it rejects targets that already own parts.

**Deliverable:** repeated choruses and related sections can share an intentional starting point without collapsing into linked data or requiring repetitive reconstruction.

### Milestone 5.23 — Unknown-heading import safety

Treat every bracketed lyric-sheet heading as a structural boundary even when its section type is unknown. Unknown headings are listed explicitly in preview, and their following lines remain isolated in the preserved raw draft rather than being silently attached to the preceding recognized section. The parser does not invent new section kinds or guess the artist’s intent.

**Deliverable:** uncommon forms such as post-choruses, refrains, interludes, and custom headings fail visibly and safely during preview.

### Milestone 5.24 — Resolve custom song-form blocks

Preserve each unknown heading together with its following lyric lines and original insertion position. Structure preview lets the artist explicitly map that block to a supported section type, inserting it back into the proposal in song order with its title, delivery direction, and lyrics intact. Leaving it unresolved keeps it only in the raw draft.

**Deliverable:** a custom heading can move from visible parser uncertainty to an artist-approved section without retyping or losing its place in the song.

### Milestone 5.25 — Artist-authored structural function

Add a genre-neutral structural function to every section: unspecified, setup, development, lift, payoff, contrast, transition, or resolution. Function is explicit artist-authored Song Graph data, independent from the section’s conventional name and from genre metadata. Editing participates in undo/redo; schema-v20 projects migrate to unspecified without inference.

**Deliverable:** a Chorus, Drop-like custom block, instrumental passage, or any supported section can describe the song-level job it performs without genre dictating its anatomy.

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

**Complete.** Milestones 0–3 prove structured lyrics, time, prosody, harmony, registered voicing, advanced voice-leading review, and basic chord audition.

### MVP

**Complete.** Milestones 0–4 and slices 5.1–5.15 prove the structured hear–revise–save–export loop. Additional role realization should follow only when artist validation shows that a complete vertical song needs it.

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
