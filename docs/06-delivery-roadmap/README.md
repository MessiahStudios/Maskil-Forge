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

The current foundation implements the local project summary list, raw lyric draft, direct transition into Song Graph editing, unsaved-work prompts, independent saved-song duplication, portable project import/export, confirmed recoverable deletion, Trash browsing and restoration, and separately confirmed permanent deletion. Automatic structural suggestions and lyric analysis remain future work.

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

### Milestone 5.26 — Structural role review flow

Let the artist assign each section's genre-neutral role while reviewing a pasted lyric-sheet proposal, before any Song Graph sections are created. The parser leaves every role unspecified and never infers function from genre or heading. The accepted import preserves those reviewed roles in the same undoable structure command, while the song outline exposes decided roles as a compact whole-song arc.

**Deliverable:** a complete pasted song can move from detected anatomy to an artist-reviewed functional arc without repetitive post-import editing or hidden genre assumptions.

### Milestone 5.27 — Optional structural role review

Derive whole-song role coverage from the Song Graph and expose the first section whose role remains undecided. The Shape workspace can focus that existing section control directly, while clearly labeling the review as optional and separate from audible readiness. No completion flag is persisted, and no role is generated or required.

**Deliverable:** existing and partially reviewed songs can complete their functional arc deliberately without hunting through every section or mistaking genre conventions for rules.

### Milestone 5.28 — Continuous structural role review

Carry an explicitly started role-review session through the remaining undecided sections. Saving a decided role advances to the next open section in song order; leaving the role undecided stays in place, and exiting focused review stops automatic continuation. The session is transient UI state and does not change ordinary intent editing or persist workflow flags.

**Deliverable:** an artist can review a long song's functional arc as one calm sequence of explicit decisions instead of repeatedly returning to the outline.

### Milestone 5.29 — Visible structural role guidance

Show the selected structural role's genre-neutral meaning directly beneath the role control in both lyric-sheet preview and section editing. Guidance updates with the unsaved selection, is programmatically associated with its control, and explains rather than recommends. The shared vocabulary has one presentation source so review and editing cannot drift apart.

**Deliverable:** artists can make informed role decisions with mouse, keyboard, or touch without relying on hidden hover text or genre conventions.

### Milestone 5.30 — Identifiable recovery snapshots

Summarize the actual protected contents on every recovery card: raw-draft state or structured section and lyric counts, plus the ordered song form when present. Keep recovery metadata compact and read-only so similarly titled drafts can be distinguished before restore without duplicating the editor.

**Deliverable:** an artist with several protected drafts can identify the likely song by its contents and form instead of restoring snapshots one at a time.

### Milestone 5.31 — Confirmed recovery discard

Protect recovery snapshots from adjacent-action misclicks with an explicit, accessible confirmation that repeats the snapshot title, content count, and song form before permanent discard. Cancellation remains the initial keyboard focus, and no recovery data changes until confirmation.

**Deliverable:** protected unsaved work cannot be permanently discarded by a single accidental action from the recovery list.

### Milestone 5.32 — Failure-safe command history

Move undo and redo entries between history stacks only after their operation succeeds. Preserve failed entries for retry or diagnosis, and translate artist-actionable command validation failures into structured API responses instead of generic server errors.

**Deliverable:** a rejected undo or redo never silently consumes history, and the client receives the actual constraint that prevented the action.

### Milestone 5.33 — Visible note ownership constraints

Derive which musical parts reference each playable note and prevent invalid removal directly in the advanced note editor. Keep referenced notes editable, disable only deletion, and name every owning part with the action needed to release the note.

**Deliverable:** artists understand why a playable note cannot be deleted before attempting the action, without weakening referential integrity or hiding precise note editing.

### Milestone 5.34 — Guided chord voicing entry

Show the owning chord's pitch classes beside manual voicing entry and validate chord membership, playable register, and low-to-high order before sending a command. Keep empty input as the explicit clearing path and retain domain validation as the authoritative boundary.

**Deliverable:** artists can author valid chord voicings from visible musical constraints instead of learning note membership or register rules through server errors.

### Milestone 5.35 — Direct readiness action focus

Identify the concrete artist action behind each hear–revise readiness gap and move focus to its first enabled control after opening the relevant section and workspace. Preserve the workspace highlight and fall back safely when no enabled action is available.

**Deliverable:** readiness guidance completes its navigation promise for lyrics, harmony, arrangement jobs, and playable parts without requiring an extra search or keyboard step.

### Milestone 5.36 — Playable-part source-note guidance

When the remaining hear–revise gap is a playable part, distinguish jobs that can realize from existing chords from jobs that need approved notes first. Harmony-support and texture remain part actions. Pulse, accents, and other note-dependent jobs with no in-section notes open Harmony and land on preparing a note sketch. Notes belonging to another section do not count. This slice adds no schema fields, automatic acceptance, or new role realization.

**Deliverable:** an artist assigned a note-dependent arrangement job is taken to the chord-to-notes step instead of a disabled part control.

### Milestone 5.37 — Ready-demo hear action

When every section already has lyrics, harmony, an arrangement job, and a resolved playable part, the derived readiness queue exposes one whole-song action: hear the assembled demo. The prompt leaves focused-section mode, highlights the existing song transport, and lands on Play without starting playback, writing project data, or persisting a completion flag.

**Deliverable:** finishing the hear–revise checklist takes the artist to listening, not to a dead-end status message.

### Milestone 5.38 — Start-of-song readiness actions

When a project has no sections, the derived queue offers adding the first section and lands on the first enabled add-section control without inventing a form. When the lyrics gap is an existing blank, unlocked line, focus that input instead of Add line; Add line remains the fallback when every line already has text or is locked. The readiness review stays visible for empty songs. This slice adds no schema fields or automatic section or lyric creation.

**Deliverable:** starting a song from the hear–revise queue creates the first section, then writes into the blank line that section already has, instead of adding a second empty line.

### Milestone 5.39 — Lyric-sheet readiness start

When a project has no sections yet, but the raw draft contains bracketed lyric-sheet headings, the derived queue opens Capture and lands on Preview song structure instead of the manual add-section toolbar. After a proposal exists, the same action lands on Create sections. Drafts without those headings keep the 5.38 add-section path. The parser still does not guess or auto-accept.

**Deliverable:** a pasted lyric sheet can enter the reviewed-structure path from hear–revise readiness without first creating an empty Intro.

### Milestone 5.40 — Unknown-heading readiness review

When a lyric-sheet preview still has unresolved unknown headings, the derived queue lands on the first heading-type control instead of a disabled Create sections button. Mapping remains an explicit artist choice; the parser still does not guess. After those headings are resolved or absent, the queue returns to preview or create.

**Deliverable:** custom form blocks such as post-choruses are reviewed from hear–revise readiness instead of silently remaining only in the raw draft.

### Milestone 5.41 — Current lyric-sheet preview readiness

Treat a structure preview as actionable only while it still matches the raw lyric sheet that produced it. If the artist edits the draft, return readiness to the enabled Preview song structure control rather than repeatedly offering a stale Create sections action.

**Deliverable:** the empty-song readiness queue recovers cleanly after lyric-sheet edits and never presents an outdated structure proposal as current.

### Milestone 5.42 — Responsive readiness overview

Collapse the hear–revise readiness action and section checklist into one column at the narrow-layout boundary, while retaining compact checklist rows until the phone breakpoint.

**Deliverable:** artists can read the next action and every section status at narrow tablet and wide-phone widths without horizontal clipping or scrolling.

### Milestone 5.43 — Portable project export

Export the current validated editor state as a deterministic, human-readable `.maskil.json` file using the authoritative Song Graph schema and string enum representation. Exclude repository paths, backups, recovery snapshots, session command history, and other machine-local state.

**Deliverable:** an artist can retain or move a complete versioned project without an account, while import and future asset packaging have one explicit artifact to target.

### Milestone 5.44 — Portable project import

Import an artist-owned `.maskil.json` file through the same schema migrations and Song Graph invariants used by local persistence. Persist the validated project atomically, refuse files from unsupported future schemas, and never silently overwrite the same project identity in the library, Trash, backups, or recovery data.

**Deliverable:** an artist can move a complete project between Maskil Forge installations and reopen it with its identity and creative decisions intact.

### Milestone 5.45 — Portable import preview and copy safety

Validate an artist-owned project before changing the library and present its title, artist, genre, song form, lyric scope, and any schema migration in one review step. Preserve the original identity only when it is available; when that identity already exists anywhere in protected local storage, offer a clearly named independent copy with a new root identity while preserving the nested creative decisions.

**Deliverable:** an artist understands what a portable file contains and can bring back a second version without risking the song already stored on the device.

### Milestone 5.46 — Independent saved-song duplication

Duplicate the explicitly saved version of a library song into a new root project identity without changing the source or regenerating nested section, lyric, harmony, note, part, provenance, or lock decisions. Name repeated copies distinctly and keep recovery snapshots outside the copy boundary.

**Deliverable:** an artist can branch an arrangement or lyric direction locally without exporting and re-importing the song or risking the original.

### Milestone 5.47 — Single-origin delivery boundary

Serve the built web client and the local project API from one production host, while retaining the separate Vite proxy for development. Expose a small health contract with the active project schema and persistence boundary, and show that state in the client. If the project service is unavailable, say plainly that the shell cannot open or save songs and that offline editing is not implemented yet.

**Deliverable:** the production client has one dependable origin for UI and project operations, and an artist cannot mistake an available shell for an offline-safe songwriting session.

### Milestone 5.48 — Installable application shell

Add complete installation metadata, platform icons, explicit install/update controls, and a versioned service worker that caches the editor shell, activity console, and their static assets. Project API traffic remains network-only, including project import, export, recovery, and saves. When only the cached shell is available, retain the existing unavailable-host explanation and never imply that project edits are protected offline.

**Deliverable:** an artist can install and reopen the Maskil Forge interface on a supported device, while the product remains explicit that offline project storage and recovery are not implemented.

### Milestone 5.49 — Browser recovery vault

Protect dirty editor state in device-local IndexedDB before attempting the existing host recovery request. Keep that browser snapshot through a host interruption, show its title and protected contents in the cached shell without exposing unusable editor actions, and return it to the normal revision-checked recovery path after reconnection. A stale host revision keeps the browser copy available for explicit review instead of overwriting either version.

**Deliverable:** losing the local project service no longer leaves the current unsaved draft dependent on an in-memory tab, while offline project-library access and editing remain explicitly outside this slice.

### Milestone 5.50 — Offline saved-song review

Cache the exact Song Graph at explicit create, open, import, and save boundaries in device-local browser storage. When the local project service is unavailable, list only those cached saved snapshots and open them in a dedicated view-only review surface that preserves the raw lyric draft, ordered song anatomy, section delivery, performance direction, and lyric lines. Keep all editing and saving behind reconnection, and remove cached copies when their host-owned songs are deleted.

**Deliverable:** an artist can reopen the installed shell on the same device and read a recently saved song without the local host, while the interface remains explicit that the snapshot is neither synchronized nor editable nor authoritative.

### Milestone 5.51 — Recovery queue hygiene

Represent host and browser recovery copies as one protected song in counts and lists while preserving an explicit restore choice when both sources exist. Show the five newest songs first, treat ten unique songs as a soft attention threshold, and label work stale after 30 days. Older work remains protected until the artist expands the queue and explicitly confirms a content-aware single-song or stale-group discard.

**Deliverable:** the recovery surface remains understandable as unfinished songs accumulate, without silently deleting lyrics or mistaking duplicate storage copies for separate creative work.

### Milestone 5.52 — Saved-song library hygiene

Keep the saved-song library uncapped and preserve the existing newest-first order while adding title-or-artist search, meaningful creative-stage filters, and a twelve-result collapsed view. Provide a dedicated empty-start review mode that selects nothing by default and can move only explicitly selected empty starts to reversible Trash after a content-aware confirmation. Permanent deletion remains a separate Trash action.

**Deliverable:** a growing saved-song library stays navigable and artists can safely review accidental empty starts without automatic pruning or treating unfinished songs as disposable.

### Milestone 5.53 — Trash hygiene

Keep Trash uncapped and preserve its newest-deleted-first order while adding title-or-artist search, a twelve-result collapsed view, and visible age labels. Let artists enter a selection mode that starts empty, explicitly select visible songs, and review an exact list before restoring multiple songs or permanently deleting them. Treat 30-day labels only as review reminders; never expire, select, restore, or erase songs automatically.

**Deliverable:** Trash remains a reversible safety net that stays understandable as it grows, with efficient artist-controlled cleanup and an unmistakable boundary before permanent loss.

### Milestone 5.54 — Narrowed phone capture journey

Validate the existing phone-width editor as a capture path rather than a miniature desktop DAW. At the 620px phone boundary the creator journey shows Idea, Words, Shape, Review, and Approve. Music, Harmony, and Arrangement stay on a larger screen. Review is a read-only look at the raw draft and structured lyrics; Approve saves that capture and states that harmony, arrangement, playback, and rough vocal capture continue later. Phone readiness asks for words, form, and lyrics—not chords or playable parts. This slice adds no schema fields, vocal recording, or automatic acceptance.

**Deliverable:** an artist on a phone can write, shape, review, and save a song without being guided into harmony, MIDI, or arrangement tooling.

### Milestone 5.55 — Compact phone editor chrome

Keep the phone capture path reachable by shrinking the sticky editor header. Title and Save stay in the bar; Undo and Redo move into Project. The long journey intro and progress checklist hide on phone because the stage buttons and capture readiness already name the next action. This slice changes no schema, persistence, or desktop chrome.

**Deliverable:** a phone artist can tap the next capture action without the sticky header covering it.

### Milestone 5.56 — Phone identity, not music settings

Keep artist, genre, and description available on the phone capture path. Hide tempo, meter, key, and developer identity details so the phone editor does not become a miniature theory or debug surface. When the local host is connected, the status banner keeps its title and omits schema copy until a reconnect or update needs that detail. This slice changes no schema or desktop settings.

**Deliverable:** a phone artist can name the song’s identity without being asked to set key, tempo, or inspect internal IDs.

### Milestone 5.57 — Phone sections write lyrics, not bars or delivery

Keep section titles, order, duplication, deletion, structural role, and lyrics on the phone Shape path. Hide bar length, delivery, and performance direction so shaping a song on a phone stays words-and-form rather than timeline or vocal staging. Desktop section cards still expose those production fields. This slice changes no schema or defaults.

**Deliverable:** a phone artist can add a verse, name it, and write lyrics without being asked how many bars it lasts or how it should be sung.

### Milestone 5.58 — Phone Shape puts lyrics first

After a section exists, the phone card shows the lyric editor next. Role in song collapses behind an optional disclosure, the outline’s role-review chrome hides, and new lyric locks stay on desktop so Lock/Remove does not sit on every line. Unlock remains available if a line is already locked. Desktop section order and lock controls are unchanged. This slice changes no schema.

**Deliverable:** a phone artist can type the first lyric line without scrolling past role review, bar anatomy, or lock controls.

### Milestone 5.59 — Phone Shape keeps the lyric line on screen

Once a section exists, phone Shape hides the connected-host banner, the duplicate draft link, the “Shape the song” title, the one-section outline, and the readiness checklist so the lyric field sits in the first screen. The add-section toolbar and next-action button remain; section reorder and delete stay on one row with the title. A two-section song still gets a compact outline for jumping. Desktop Shape chrome is unchanged. This slice changes no schema.

**Deliverable:** a phone artist can add a verse and reach the lyric input without scrolling past duplicate navigation.

### Milestone 5.60 — Phone Shape adds a section from one control

Replace the six-button add-section toolbar with a single “Add section” disclosure on phone. Opening it still offers Intro through Outro; choosing one adds that section and closes the menu. The next-action button opens the disclosure instead of guessing a section kind. Desktop keeps the full toolbar. This slice changes no schema.

**Deliverable:** a phone artist can add a verse without a three-row section-kind keypad covering the lyric field.

### Milestone 5.61 — Phone Words puts the draft on screen

Keep the raw lyric textarea as the first writing surface on phone. Hide the long capture lecture, the duplicate Save draft and Shape manually actions, and the preservation footnote—Save stays in the header and Shape stays in the journey. Preview song structure remains for pasted lyric sheets. Desktop capture chrome is unchanged. This slice changes no schema.

**Deliverable:** a phone artist can type into the raw draft without scrolling past explanatory copy or duplicate structure actions.

### Milestone 5.62 — Browser-owned lyric capture

Add the first deliberately narrow offline editing surface: a raw lyric capture stored in this browser's IndexedDB with explicit device ownership. Let the artist start, automatically protect, reopen, continue, and permanently delete that capture while the local project service is unavailable. Keep it separate from host-owned saved songs, cached view-only snapshots, recovery, structure commands, and synchronization. After reconnection, an explicit handoff creates one new host-owned song with fresh identity and removes the browser copy only after the complete title, artist, genre, description, and raw lyrics are durably saved.

**Deliverable:** an artist can write and retain words on a phone or computer without the host, while the UI never implies that a complete song project is offline-editable or synchronized.

### Milestone 5.63 — Device-capture hygiene

Keep browser-owned lyric captures uncapped and preserve their newest-saved-first order while adding title-or-artist search and a twelve-result collapsed view. Provide an explicit cleanup mode that starts with nothing selected, can select only the currently visible captures, and presents every selected title, artist, lyric count, and save time before permanent removal. Device captures have no Trash or synchronized fallback, so deletion remains unmistakably irreversible and never runs automatically.

**Deliverable:** offline lyric captures remain manageable as they accumulate without a silent cap, expiry rule, or cleanup process deciding which words matter.

### Milestone 5.64 — Stable host library boundary

Keep the local host's authoritative library outside browser-managed storage and outside a packaged application's replaceable installation files. Development continues using the API's ignored `App_Data/projects` directory so existing test songs do not move unexpectedly. A non-development host defaults to the operating system's per-user application-data directory, and an explicit absolute `MaskilForge:LibraryPath` setting can select another artist-controlled location without making the process working directory part of persistence identity.

A connected phone or installed PWA continues reading and saving that same host-owned library through the API; IndexedDB remains limited to recovery, view-only saved snapshots, and browser-owned lyric captures. This slice does not copy the complete library onto the phone, synchronize two authoritative libraries, or merge a device capture into an existing song. That future paired-device handoff must define project identity, base revision, conflicts, and artist review before it changes an existing host song.

**Deliverable:** packaged-host upgrades and application relocation cannot silently move the authoritative song library, while development data stays compatible and every client retains one honest storage authority.

### Milestone 6.1 — Explicit microphone preflight

Begin the human-performance path without prematurely creating unmanaged audio assets. Phone Review exposes an artist-triggered microphone check only when the app is in a secure, MediaRecorder-capable browser. The check requests permission, confirms at least one live audio input, immediately stops every acquired track, and reports permission, missing-device, and busy-device failures distinctly. It records, uploads, and saves no audio and logs no device label. Review and Approve remain focused read-and-decide destinations instead of rendering the editable Shape workspace beneath them.

Keep rough-take recording unavailable until original audio joins the same explicit backup, recovery, portable export, Trash, and permanent-deletion lifecycle expected of project data. This readiness result is transient UI state and changes neither the Song Graph nor the saved project.

**Deliverable:** an artist can confirm that a phone or computer is ready for future rough vocal capture without Maskil Forge silently opening a microphone or creating audio it cannot yet protect.

### Milestone 6.2 — Path-free audio asset manifest

Advance the project schema to v22 with an explicit manifest for external creative assets, beginning with original human vocal takes. Each immutable entry owns a stable project-asset identity, asset kind, normalized media type, exact byte length, SHA-256 digest, and creation time. It stores no repository path, browser URL, audio bytes, analysis result, production setting, or generated replacement vocal. Schema-v21 projects and recovery snapshots migrate to an empty manifest without inventing media.

Keep the existing `.maskil.json` document honest: export refuses a project whose manifest references media it cannot carry, and import refuses a JSON document that names external assets without their verified bytes. Recording remains unavailable until an asset-owning package and repository lifecycle can satisfy those references through backup, recovery, Trash, restore, and permanent deletion.

**Deliverable:** the canonical project can identify original vocal media without depending on one machine, while no legacy export can claim to be portable after separating the singer's recording from the project.

### Milestone 6.3 — Repository-owned immutable asset lifecycle

Store each registered original-vocal asset in a project-owned directory under its stable identity. Stage new content before acceptance, require its exact manifest byte length and SHA-256 digest, refuse identity overwrite, and revalidate the content whenever the project loads. Pair the asset directory with the JSON document through known-good backup, session recovery, corrupt-data preservation, Trash, restore, and confirmed permanent deletion so none of those operations silently separates a take from its project.

This slice changes repository durability, not the recording experience. It adds no microphone capture endpoint, browser upload flow, playback, analysis, or package format. Rough-take recording remains unavailable until portable export and import can carry and verify both the manifest and every referenced byte.

**Deliverable:** Maskil Forge can own and recover immutable original-recording bytes locally without weakening project integrity, while still refusing to create recordings it cannot yet move safely between devices.

### Milestone 6.4 — Asset-owning portable package

Define package format version 1 as a `.maskil` archive that carries `maskil-package.json`, the Song Graph as `project.json`, and each manifest-referenced original vocal as `assets/{id}.bin`. Export reads repository-owned bytes, verifies exact length and SHA-256, and refuses missing, extra, or mismatched media. Import migrates `project.json`, verifies every referenced byte, and persists the project with its assets without overwriting an existing identity. JSON-only `.maskil.json` files still refuse referenced media. Local saved-song duplication copies verified asset bytes into the new project identity. This slice adds no recording, playback, analysis, or microphone capture.

**Deliverable:** an artist can move a project that already owns original vocal bytes between Maskil Forge installations without leaving the singer's recording behind, while JSON-only files stay honest about what they cannot carry.

### Milestone 6 development gate — Remote-device activity relay

Before rough recording is debugged on real phones, let a Development host receive the browser's existing structured activity entries and expose its bounded in-memory device sessions in the activity console. Sessions identify only phone, tablet, or desktop display context, viewport size, and installed-versus-browser mode. They carry no audio bytes, microphone label, persistent device identity, project mutation, or production telemetry. The relay retains at most sixteen current sessions and one thousand entries per session, disappears on host restart, and is not mapped outside the Development environment.

**Deliverable:** while interacting with Maskil Forge on a phone through a secure development origin, a developer can select that transient phone session on the host's activity console and follow its compatibility and microphone-state events without handling the phone's audio or treating telemetry as project data.

### Milestone 6.5 — Durable rough-vocal capture

Let an artist record one rough vocal take at a time from phone Review through an explicit MediaRecorder action, with a one-minute browser limit and a 25 MB host limit. The browser closes every microphone track after stop, keeps the resulting audio only in tab memory, and requires playback review before Save take uploads anything. Discard removes that temporary take without changing the Song Graph or host library.

Saving requires a connected host and an explicitly saved project revision. The host validates the recording media type and size, computes SHA-256 itself, registers a fresh `OriginalVocalTake` manifest entry, and commits the immutable bytes through the existing asset-owning repository transaction. A stale project revision rejects the attachment without registering or writing an asset, while the reviewed browser recording remains available for retry. Saved takes can be played from their verified repository bytes and travel through backup, recovery, Trash, duplication, permanent deletion, and `.maskil` package export.

This slice adds no trimming, naming, take deletion independent of the project, section/timeline placement, waveform, pitch or onset analysis, transcription, comping, effects, background upload, offline recording, or audio telemetry. Activity logs may report capture state, review playback, duration, format, and byte length but never carry microphone labels or audio bytes. A secure HTTPS origin grants microphone capability; it does not authorize access to the project service. Any internet-reachable development tunnel must add an access policy before it carries private lyrics or vocal performances.

**Deliverable:** a phone or computer can create the first artist-reviewed original vocal asset without weakening project revision safety, portability, or human-performance authority.

### Milestone 6.6 — Saved rough-take removal

Let an artist remove one selected rough vocal from the current saved song after a content-aware confirmation that identifies its take number, capture time, and size. Removal requires the current persisted project revision, refuses stale devices without changing the manifest or media, and removes only that take's active immutable bytes while preserving every other recording. The updated song, playback list, and future `.maskil` exports no longer reference the removed take.

Before changing the active project, the repository refreshes its known-good previous-version backup with the complete pre-removal manifest and verified media. The confirmation therefore states that removing a take is not a privacy erase of historical safety copies. Permanent deletion of the whole song through Trash still removes active, backup, recovery, and trashed assets together. Independent backup-history cleanup, take naming, trimming, bulk removal, timeline placement, analysis, transcription, and comping remain later slices.

Activity logs may report the selected asset identity, byte length, outcome, and remaining take count, but never include audio bytes or microphone labels. Removal is unavailable while the editor has unsaved Song Graph changes or a recording save is in progress.

**Deliverable:** an artist can correct an accidental or unwanted saved-take choice without deleting the song, silently weakening revision safety, or mistaking current-version removal for deletion of every protected historical copy.

### Milestone 6.7 — Durable rough-take naming

Advance the project schema to v23 so every registered project asset owns a short, durable display name in addition to its immutable integrity metadata. Schema-v22 packages migrate original-vocal assets in manifest order to deterministic `Take 1`, `Take 2`, and later defaults without changing asset identities, hashes, timestamps, or bytes. New captures choose the first available numbered default so removing an earlier take does not force duplicate labels.

Let an artist rename one saved rough take from Review with an explicit, phone-sized dialog. Names are trimmed, required, limited to eighty characters, and committed only against the current persisted project revision. A stale phone or laptop is refused without changing the active name or recording. The rename updates project metadata, backup state, recovery behavior, duplication, and portable `.maskil` interchange while leaving the immutable original recording byte-for-byte unchanged.

Activity logs report only the project and asset identity plus the rename outcome; the artist-authored name and audio remain outside development telemetry. Trimming, tags, ratings, take reordering, bulk cleanup, timeline placement, analysis, transcription, and comping remain later work.

**Deliverable:** an artist can identify several saved performances by meaning instead of list position, and those names survive project movement without weakening recording integrity or multi-device revision safety.

### Milestone 6.8 — Performance observation foundation

Advance the project schema to v24 with an explicit `performanceObservations` evidence collection. A performance observation has its own stable identity, references one immutable original-vocal asset, names an extensible observation kind, locates a millisecond time span, carries one or more named scalar measurements with units, and retains optional zero-to-one confidence, analyzer identity and version, analyzer provenance, and creation time. Schema-v23 projects migrate to an empty collection and do not invent analysis.

Observations are persisted and carried by backup, recovery, duplication, Trash, and asset-owning `.maskil` packages, but remain a non-authoritative evidence partition rather than artist-approved notes, gestures, or production decisions. Every observation must reference a present original-vocal asset. Removing that source take also removes its derived observations from the active version so no orphaned evidence can survive after its bytes leave the current project.

This slice defines no pitch, onset, loudness, prosody, or audio-model analyzer; no endpoint accepts untrusted observation uploads; and no observation silently changes Song Graph material. Analyzer execution, comparison UI, reruns, artist correction, gesture promotion, and voice-to-MIDI remain later slices.

**Deliverable:** future analyzers have one validated, portable, attributable evidence boundary before any measurement is allowed to influence musical reasoning.

### Milestone 6.9 — Saved-take loudness observation pilot

Give one existing rough vocal take an explicit **Analyze loudness** action in phone Review. The browser decodes the same host-owned recording it can already play and deterministically divides the decoded samples into contiguous 250 ms frames. Each frame reports RMS and peak amplitude in dBFS. Analysis begins only from an artist action, uses no microphone, uploads no audio, and keeps the immutable source asset as the authority.

The host accepts a dedicated loudness-frame report rather than arbitrary observations. It requires the current project revision and the named original-vocal asset, rejects empty, unordered, overlapping, overlong, excessive, out-of-range, or incorrectly sized reports, and stamps observation identities, analyzer ID `maskil.browser.loudness`, version `1.0.0`, deterministic provenance, and creation time itself. Every frame is exactly 250 ms except for a shorter final frame, so a run may cover no more than the existing one-minute recording limit and 240 frames. Rerunning atomically replaces only that analyzer's previous `loudness.frame` evidence for the same source; unrelated evidence and source bytes remain unchanged.

Review shows a compact frame-count, analyzed-span, and strongest-peak summary. Activity logs expose the project, asset, analyzer, outcome, and frame count without recording bytes or measured levels. These observations remain non-authoritative evidence carried by the existing persistence and package lifecycle. This slice makes no mastering recommendation and adds no integrated LUFS, pitch, onset, timing-intent, prosody, gesture, note-promotion, or automatic musical-decision behavior.

**Deliverable:** an artist can produce the first honest, inspectable analyzer evidence from a saved phone recording while the system proves revision safety, rerun ownership, portability, and strict separation from creative truth.

### Milestone 6.10 — Confidence-gated pitch observation pilot

Add a separate **Analyze pitch** action beside saved-take loudness analysis in phone Review. The browser decodes the immutable host-owned take, reduces analysis to no more than 8 kHz, and runs normalized autocorrelation over 80 ms windows on a 200 ms grid. It searches only 65–1000 Hz and emits a frame only when the centered signal clears the analyzer floor and normalized correlation reaches at least 0.72. Silence, very quiet input, and uncertain periodicity make no pitch claim.

The host accepts a dedicated pitch-frame report rather than arbitrary observations. It requires the current project revision and named source take, validates the exact grid and window duration, bounded frequency and confidence, strictly increasing positions, no more than 300 voiced frames, and the one-minute recording boundary, then stamps observation identities, analyzer ID `maskil.browser.pitch-acf`, version `1.0.0`, deterministic provenance, and creation time. Rerunning atomically replaces only that analyzer's earlier `pitch.frame` evidence. A valid empty result clears stale pitch claims while leaving loudness evidence and source bytes untouched.

Review shows the confident voiced-frame count and median detected frequency with an explicit evidence-only label. Activity logs retain project, asset, analyzer, outcome, and frame count but no audio, frequency, or confidence values. The analyzer creates no MIDI note, approved melody, contour, correction target, onset, timing gesture, or automatic musical decision.

**Deliverable:** a saved human performance can produce bounded, attributable frequency evidence while silence and uncertainty remain honest absences and artist-owned music stays untouched.

### Milestone 6.11 — Confidence-gated onset observation pilot

Add a separate **Analyze onsets** action beside saved-take loudness and pitch analysis in phone Review. The browser decodes the immutable source locally, downmixes and reduces analysis to no more than 8 kHz, then measures RMS energy in 32 ms windows on a 16 ms grid. A candidate must clear a signal floor, a minimum energy rise, a previous-frame ratio, and 0.6 confidence. Local rise maxima are kept at least 96 ms apart. Quiet input, gradual changes, and uncertain rises make no onset claim.

The host accepts a dedicated onset-event report rather than arbitrary observations. It requires the current project revision and source take, validates the exact grid and window, ordered 96 ms separation, normalized strength, bounded confidence, at most 625 candidates, and the one-minute source boundary, then stamps observation identities, analyzer ID `maskil.browser.onset-energy`, version `1.0.0`, deterministic provenance, and creation time. A rerun replaces only that analyzer's prior `onset.event` evidence; an empty result clears those candidates while preserving loudness, pitch, and source bytes.

Review shows the candidate count and first approximate position with an evidence-only label. Activity logs retain project, asset, analyzer, outcome, and event count but no onset positions, strengths, confidence values, or audio. Candidates create no notes, tempo, beat grid, quantization, timing correction, gesture, or automatic musical decision.

**Deliverable:** a saved human performance can expose bounded rhythmic-transition evidence without turning analyzer timing into artist-approved musical structure.

### Milestone 6.12 — Saved-take evidence inspector

Add an expandable, read-only evidence inspector beneath each saved take in phone Review. Group persisted observations by kind, analyzer identity, and analyzer version; show provenance and report time; and order individual claims by their source-audio span. Each row exposes the stored span, measurements, and confidence rather than only the existing count, median, or strongest-value summary. Known loudness, pitch, and onset measurements receive compact readable labels, while unknown future kinds remain visible through generic formatting.

Reveal large groups in deterministic twelve-row pages so a one-minute report remains usable on a narrow phone without mounting every possible claim at once. Paging is transient display state. The inspector reads the existing schema-v24 collection and writes no project data, reruns no analyzer, and creates no note, tempo, beat, quantization target, correction, approval, or gesture. Artist correction and gesture promotion remain later explicit, reversible slices.

**Deliverable:** an artist can inspect exactly what each analyzer claimed and how certain it was before any evidence is allowed to influence editable musical material.

### Milestone 6.13 — Artist verdicts on analyzer claims

Advance the project schema to v25 with a separate `performanceObservationReviews` collection. From a visible evidence row, the artist can mark one claim **Accurate** or **Inaccurate**, revise that verdict, or clear it to **Unreviewed**. Each stored review owns a stable identity, references exactly one present observation, records creation and update times, and remains separate from both analyzer confidence and any later musical approval.

Review writes require the current persisted project revision. A source-take removal cascades through its observations and reviews. Rerunning an analyzer replaces its prior claims and invalidates only reviews attached to those disappearing claim IDs; reviews for unaffected analyzers remain. This prevents an old artist decision from silently attaching to newly measured evidence. Backup, recovery, duplication, Trash, and `.maskil` packages carry the current reviews with the project.

This slice records agreement or disagreement only. It adds no corrected measurement, selection range, note, beat, tempo, quantization target, MIDI event, expression curve, gesture promotion, or automatic musical decision. Those remain explicit later commands built on reviewed evidence.

**Deliverable:** the artist can make a durable, reversible judgment about individual analyzer claims before any correction or gesture data is allowed to exist.

### Milestone 6.14 — Artist-authored observation corrections

Advance the project schema to v26 with a separate `performanceObservationCorrections` collection. After an evidence row is marked **Inaccurate**, the artist can store one correction for that claim, revise it, or remove it. The original observation stays immutable. A correction uses the same measurement names, units, and count as the analyzer claim, changes at least one value, and stays inside the existing loudness, pitch, and onset bounds. Marking the claim **Accurate** or returning it to **Unreviewed** drops any stored correction.

Writes require the current persisted project revision. Removing a source take, removing an observation, or rerunning the analyzer that owns a claim also removes corrections attached to disappearing claim IDs. Backup, recovery, duplication, Trash, and `.maskil` packages carry current corrections with the project. Activity logs retain project and observation identity plus outcome, not measurement values, audio, or microphone labels.

This slice still creates no note, beat, tempo, MIDI event, expression curve, gesture promotion, or automatic musical decision. A correction is artist-authored evidence beside the analyzer claim, not a rewrite of the recording or an approved musical change.

**Deliverable:** an artist can record a durable, reversible numeric correction for an inaccurate analyzer claim without overwriting the original evidence or promoting it into musical material.

### Milestone 6.15 — Artist-approved performance gestures

Advance the project schema to v27 with a separate `performanceObservationGestures` collection. From a reviewed evidence row, the artist can promote one claim into an approved gesture snapshot, revise that snapshot, or remove it. Promotion is allowed only while the claim is **Accurate**, or **Inaccurate** with a stored correction. The host copies the approved measurements itself; the client sends only promote or clear. Unreviewed claims and inaccurate claims without a correction cannot be promoted, and any existing gesture for that claim is dropped.

Writes require the current persisted project revision. Changing the review or correction of a promoted claim refreshes the snapshot in place when it remains eligible, or drops it when eligibility is lost. Removing a source take, removing an observation, clearing the review, or rerunning the analyzer that owns a claim also removes gestures attached to disappearing claim IDs. Backup, recovery, duplication, Trash, and `.maskil` packages carry current gestures with the project. Activity logs retain project and observation identity plus outcome (`promoted` or `cleared`), not measurement values, audio, or microphone labels.

This slice still creates no note, beat, tempo, MIDI event, expression curve, or automatic musical decision. A gesture is an artist-approved snapshot of reviewed evidence, not a rewrite of the recording and not yet a musical change.

**Deliverable:** an artist can promote reviewed analyzer evidence into a durable, reversible gesture snapshot without creating notes, MIDI, or automatic musical decisions.

### Milestone 6.16 — Pitch-gesture note sketch

Convert approved pitch gestures on one original-vocal take into a transient, inspectable playable-note sketch. Each gesture measurement named `frequencyHertz` maps to the nearest MIDI note with `69 + 12 * log2(f / 440)`, spelled with sharps, and clamped to 0–127. Time uses the first tempo event only: `ticks = milliseconds * BPM * 480 / 60000`, rounded away from zero, with duration at least one tick. The take starts at song tick 0 until a later slice places takes on the timeline. Velocity is 96. Loudness and onset gestures are ignored. Preparing the sketch does not modify the Song Graph and does not bump schema.

Desktop Music can preview the sketch and explicitly accept it. Acceptance adds `NoteEvent`s through the existing add/restore path; undo removes only those accepted notes; existing notes stay. Dropping a gesture later does not delete already-accepted notes. The sketch assigns no musical part, maps no loudness to velocity, and creates no expression curves. A take with no pitch-frequency gestures cannot prepare a sketch. Activity logs retain project, take, and note-count identity, not frequencies. Phone Review stays capture, review, and promote; it does not create notes.

**Deliverable:** an artist can preview and explicitly accept playable notes from approved pitch gestures on one take, with take-relative timing from tick 0, without automatic musical decisions.

### Milestone 6.17 — Desktop saved-take studio

Expose saved original-vocal takes on the desktop Music workspace so the studio screen can play, analyze, review, correct, and promote the same host-owned recordings phone Review already owns. Recording remains an explicit MediaRecorder action against the current saved revision. The evidence inspector, artist verdicts, corrections, and gesture snapshots use the existing APIs and schema v27 collections; this slice does not bump schema, place takes on the timeline, or create notes.

Desktop Music lands on this take studio, then the existing pitch-gesture sketch. Phone Review stays the capture companion and still does not create notes. Activity logs keep the same take and observation identity rules: no audio bytes, frequencies, or microphone labels.

**Deliverable:** an artist can inspect and promote a saved rough take on the studio screen, then explicitly sketch notes from pitch gestures, without turning desktop into a miniature DAW or moving take placement into the Song Graph.

### Milestone 6.18 — Vocal-take song placement

Advance the Song Graph to schema v28 with a separate `vocalTakePlacements` collection. Each original-vocal take may have one artist-authored song start as bar, beat, and tick. Schema-v27 projects migrate to an empty collection; missing placement still means song tick 0. Writes require an existing original-vocal asset and a start that fits the current meter. Removing the take drops its placement. Changing meter refuses a start that would leave the new beat grid.

Desktop Music can set, update, or clear that start. The pitch-gesture note sketch adds the placement's absolute tick to take-relative timing. Changing or clearing placement does not rewrite already-accepted notes. Placement is song time, not a section clip, waveform region, or transport sync. Phone Review stays capture, review, and promote. Activity logs retain project and take identity plus the placed bar, beat, and tick—not audio bytes or microphone labels.

This slice still creates no onset or loudness notes, musical part, expression curve, or automatic musical decision.

**Deliverable:** an artist can place a saved rough take in song time so sketched notes follow that start, without turning the take into a DAW clip or moving already-accepted notes.

### Milestone 6.19 — Onset-gesture note sketch

Convert approved onset gestures on one original-vocal take into a transient, inspectable playable-note sketch. Each gesture whose observation kind is `onset.event` becomes a natural C4 hit. Time uses the first tempo event only: `ticks = milliseconds * BPM * 480 / 60000`, rounded away from zero, with duration at least one tick, plus the take's vocal-take placement when present. Velocity is `Clamp(Round(strength * 127, AwayFromZero), 1, 127)` from the gesture measurement named `strength`; missing strength uses 96. Pitch-frequency and loudness gestures are ignored. Preparing the sketch does not modify the Song Graph and does not bump schema.

Desktop Music can preview the sketch and explicitly accept it. Acceptance adds `NoteEvent`s through the existing add/restore path; undo removes only those accepted notes; existing notes stay. Dropping a gesture later or changing placement does not delete or move already-accepted notes. The sketch assigns no musical part and creates no expression curves. A take with no onset gestures cannot prepare a sketch. Activity logs retain project, take, and note-count identity, not strength or audio. Phone Review stays capture, review, and promote; it does not create notes.

**Deliverable:** an artist can preview and explicitly accept short C4 notes from approved onset gestures on one take, with take-relative timing plus song placement, without automatic musical decisions.

## Delivery foundation — Portable before platform-specific

Before native packaging or account infrastructure, define a versioned Maskil project package that can be explicitly exported, validated, migrated, imported, and recovered. The current JSON Song Graph is the creative core; the package must grow to own referenced vocal and audio assets when those arrive.

Then make the shared web client installable and intentionally offline-capable. Validate a narrowed phone journey for Idea, Words, Shape, rough human-vocal capture, Review, and Approve. Do not treat a manifest alone, a browser cache without project recovery, or a miniature desktop layout as PWA completion.

Begin a desktop-shell experiment only after performance work identifies a concrete browser limitation in low-latency audio, MIDI hardware, native files, plugin hosting, or rendering. Tauri, Electron, and similar frameworks remain undecided until that experiment. Accounts, cloud backup, and device synchronization are optional later services rather than prerequisites for portable local projects.

**Deliverable:** the same project moves safely between supported clients without an account, while each client exposes only the capabilities appropriate to its device.

## Milestone 6 — Voice performance capture

Build recording, pitch/onset/loudness extraction, extensible `PerformanceObservation` data, gesture editing, voice-to-MIDI, and expression curves. Each observation retains confidence, analyzer identity and version, source-asset identity, and analyzer provenance. Observations remain separate from artist-corrected or approved Song Graph decisions. Captured gestures may drive editable musical or instrument-performance data. They do not generate or replace the artist's lead vocal.

**Deliverable:** humming or singing produces inspectable, attributable observations that can control musical material through explicit artist-reviewed commands, while the human lead vocal remains the authoritative performance.

## Milestone 7 — Instrument intelligence

Expand instrument knowledge, recommendations, range checks, articulation maps, and at least two performance retargeters.

**Deliverable:** an artist can choose by emotional quality instead of orchestration vocabulary.

## Milestone 8 — Rendering integrations

Add SoundFont or equivalent rendering, external DAW export, plugin scanning, VST3 hosting, presets, automation, and offline rendering in that order. VST and other audio processors may assist instrumental playback and the artist's recorded vocal; they must not own the Song Graph or replace the lead singer.

**Deliverable:** the same Song Graph can drive multiple sound sources.

## Milestone 9 — Human vocal production

Build guide vocals, lyric highlighting, take management, punch-in, comping, pitch/timing feedback, harmony guides, and non-destructive vocal effects. Production settings remain reviewable. The recorded, artist-chosen take is the lead vocal; guidance and processing assist that singer rather than generating a replacement.

**Deliverable:** the artist can complete the human lead-vocal workflow inside the product without the product becoming the singer.

## Milestone 10 — AI director

Expose tested engine functions as typed tools, add intent interpretation, structured musical and performance observations as reasoning inputs, plan preview, command validation, explanations, and conversational revision. Any direct audio-capable model interpretation is supplemental, carries confidence and provenance, and cannot replace structured observations or artist review.

**Deliverable:** natural language safely directs the same operations available in the UI, and analyzer-informed proposals expose their evidence and uncertainty before acceptance.

## Milestone 11 — Mix, export, and release workflow

Build mixer routing, automation, production recipes, stem/WAV export, DAW handoff, project reports, and provenance.

**Deliverable:** a user can finish or hand off a song without losing editability or origin history.

## Practical release slices

### Prototype

**Complete.** Milestones 0–3 prove structured lyrics, time, prosody, harmony, registered voicing, advanced voice-leading review, and basic chord audition.

### MVP

**Complete.** Milestones 0–4 and slices 5.1–5.15 prove the structured hear–revise–save–export loop. Additional role realization should follow only when artist validation shows that a complete vertical song needs it.

### Artist alpha

Milestones 6–7 and basic vocal takes: prove voice-driven control of musical material and instrument intelligence, with the artist's vocal remaining authoritative.

### Production beta

Milestones 8–11: rendering, AI direction, vocal production, mixing, and export.

## What not to build early

- Full VST hosting before MIDI generation is musically useful
- A broad AI chat layer before typed commands exist
- Advanced mixing before arrangement and export are stable
- Dozens of genres before one vertical slice works end to end
- Neural final-audio generation as a substitute for the Song Graph
- Generated or synthesized lead vocals as a substitute for the artist's performance
- Mandatory accounts or cloud synchronization before portable project interchange
- A desktop shell before a required workflow demonstrates a native capability gap
- A phone-sized imitation of the desktop production workspace

## Recommended first vertical slice

Support one song with `Verse -> Chorus -> Verse -> Chorus`, one meter, a constrained tempo range, one genre profile, lyric prosody candidates, a small chord vocabulary, piano/bass/drums roles, simple preview playback, MIDI export, save/load, locks, and undo/redo.

That slice tests the defining idea: meaning becomes structured music, the artist can revise any layer, and accepted work survives regeneration.
