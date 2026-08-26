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

Many aspiring and independent singer-songwriters begin with lyrics, a vocal idea, or an emotional direction but do not yet know how prosody, harmony, arrangement, orchestration, MIDI, or instrument technique can express it. Maskil Forge is intended to bridge that gap without taking authorship away from the artist. The artist supplies the words, intent, vocal gestures, taste, revisions, and the recorded human lead vocal. Maskil Forge may capture and analyze that vocal, provide pitch, timing, and prosody guidance, create guide melodies, preserve takes and comps, suggest or apply reviewable vocal production settings, and use VST or other audio processing to assist the singer. The artist chooses a desired vocal result; the host proposes processing roles rather than asking a new songwriter to assemble a plugin chain. Voice analysis may also drive editable musical or instrument-performance data. The product must not generate or replace the final lead singer.

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

Slice 5.62 adds the first honest offline editing boundary: a browser-owned raw lyric capture can be started, automatically protected in IndexedDB, reopened, edited, and explicitly deleted without the local project service. It remains visibly separate from host-owned saved songs and synchronization. After reconnection, an explicit handoff atomically creates a new saved song with fresh project identity and removes the browser copy only after the host save succeeds; song structure and music tooling remain outside this device-only editor.

Slice 5.63 keeps browser-owned captures uncapped and newest-first while adding title-or-artist search, a twelve-result collapsed view, and an explicit multi-capture cleanup mode. Selection begins empty, visible selection remains artist-controlled, and a content-aware confirmation names every capture before permanent deletion. No browser-owned work is expired, selected, or removed automatically.

Slice 5.64 formalizes the host-owned library boundary. Development retains the ignored `App_Data/projects` location, while non-development hosts use the operating system's stable per-user application-data directory—`%LOCALAPPDATA%\Maskil Forge\Library` on Windows—unless `MaskilForge:LibraryPath` supplies an absolute artist-controlled location. A connected phone or PWA saves through the API into that same host library; IndexedDB remains recovery, view-only cache, and device-capture storage rather than a synchronized second library.

Slice 6.1 begins the human-performance path with an explicit microphone preflight in phone Review. It verifies secure-browser and recording support, requests microphone access only after the artist chooses to check, confirms a live input, and immediately closes every track without recording, uploading, or saving audio. Review and Approve no longer spill into the editable Shape workspace beneath them. Original rough takes remain unavailable until audio assets participate in backup, recovery, portable export, Trash, and permanent deletion.

Slice 6.2 advances that boundary with schema v22 and a path-free project asset manifest for original human vocal media. Every manifest entry has a stable identity, media type, byte length, SHA-256 digest, and creation time while carrying neither machine paths nor audio bytes. Schema-v21 projects migrate to an explicit empty manifest. Legacy `.maskil.json` export and import fail closed when external media is referenced so portability can never silently leave a singer's recording behind.

Slice 6.3 gives those manifest entries repository-owned immutable bytes. Asset content is staged, length- and SHA-256-verified before acceptance, revalidated whenever its project loads, and carried beside the project through known-good backup, session recovery, Trash, restore, corrupt-data preservation, and permanent deletion. Recording remains unavailable until a portable package can carry and verify the same bytes across devices.

Slice 6.4 adds that package: a versioned `.maskil` archive that carries the Song Graph and every referenced original-vocal byte, verifying length and SHA-256 on export and import. JSON-only `.maskil.json` files still refuse media they cannot carry. Local library duplication copies the same verified bytes into a new project identity. Recording remains the next capture step.

The Milestone 6 remote-device development gate makes real-phone compatibility observable before recording begins. When the host runs in Development, browser sessions forward the existing structured activity entries into a bounded, in-memory relay; the activity console can select a phone, tablet, or desktop session and follow it from another device. The relay carries no audio bytes or microphone labels, creates no persistent device identity, clears on host restart, and is unavailable outside Development.

Slice 6.5 adds the first durable rough-vocal recording flow. Phone Review records at most one minute into temporary tab memory, closes every microphone track after stop, and requires the artist to listen before Save take uploads anything. The host enforces a 25 MB limit, computes SHA-256, registers a fresh original-vocal manifest entry, and writes the immutable bytes only against the current persisted song revision. Saved takes can be played from the host and already participate in backup, recovery, Trash, duplication, permanent deletion, and asset-owning `.maskil` export. Trimming, take naming or deletion, timeline placement, analysis, transcription, comping, effects, and offline recording remain later work. HTTPS enables microphone capture but is not access control, so an internet-reachable development tunnel must be protected before carrying private lyrics or performances.

Slice 6.6 gives each saved rough take an explicit single-take removal review. Removal is revision-checked, removes only the selected manifest entry and active immutable byte, keeps every other take intact, clears stale session recovery, and excludes the take from future playback and `.maskil` exports. The confirmation is deliberately honest that the host's previous-version safety backup retains the earlier project and bytes; this is current-version editing, not a privacy erase of historical copies. Take naming, trimming, bulk cleanup, analysis, and independent backup-history management remain later work.

Slice 6.7 advances the Song Graph to schema v23 with a durable, validated name on every project asset. Existing schema-v22 vocal manifests migrate in order to `Take 1`, `Take 2`, and later defaults; new captures select the first unused numbered name. Review can rename one saved take only against the current persisted revision, and the operation changes metadata while preserving the asset identity, SHA-256 digest, creation time, and original audio bytes. Names travel through backup, recovery, duplication, Trash, and `.maskil` packages, while activity logs retain only asset identity and outcome. Trimming, ratings, reordering, bulk cleanup, timeline placement, and performance analysis remain later work.

Slice 6.8 advances the Song Graph to schema v24 with a separate `performanceObservations` evidence collection. Each observation retains a stable identity, immutable source-vocal identity, millisecond span, extensible named measurements and units, optional confidence, analyzer identity and version, provenance, and creation time. Schema-v23 projects migrate to an empty collection without inventing analysis. Observations move with the project and source bytes through persistence and `.maskil` packaging, but they remain distinct from artist-approved notes or gestures; removing a source take also removes its derived active observations. This slice defines the contract only and adds no analyzer or automatic musical decision.

Slice 6.9 lets an artist explicitly analyze one saved rough take into deterministic 250 ms loudness frames. The recording is decoded locally by the Review browser; only bounded RMS and peak dBFS measurements are sent to the connected host, which validates a contiguous one-minute-or-shorter report and stamps the fixed analyzer identity, version, provenance, source asset, and creation time. Saving requires the current project revision. A rerun atomically replaces only that analyzer's earlier loudness frames, preserves unrelated evidence, and never rewrites the original audio. Review shows a compact evidence summary and activity logs expose coarse start, success, or failure context without audio or measurement values. Pitch, onset, integrated loudness, gesture correction, note promotion, and automatic musical decisions remain later work.

Slice 6.10 adds a separate artist-triggered pitch-evidence pilot for saved rough takes. The Review browser analyzes 80 ms windows on a 200 ms grid, searches only 65–1000 Hz, and reports only voiced frames that clear both a signal floor and 0.72 normalized-autocorrelation confidence. The host accepts no arbitrary observations: it validates the dedicated grid, duration, frequency, confidence, source, one-minute limit, and current revision before stamping `maskil.browser.pitch-acf` version `1.0.0` and deterministic provenance. Reruns replace only this analyzer's pitch frames; an empty confident result clears its earlier claims while preserving loudness evidence and immutable audio. Review shows frame count and median frequency as evidence only, and activity logs retain no frequency or confidence values. The slice creates no MIDI notes, melody contour, pitch correction, onset, gesture, or automatic musical decision.

Slice 6.11 adds a third independent browser analyzer for confidence-gated onset candidates. It measures energy rises in 32 ms windows on a 16 ms grid, requires a bounded signal level, rise, and previous-frame ratio, and keeps candidates at least 96 ms apart. The host validates at most 625 ordered candidates inside the one-minute source boundary, stamps `maskil.browser.onset-energy` version `1.0.0`, and stores only normalized strength plus confidence as non-authoritative `onset.event` evidence. Reruns replace or clear only this analyzer's candidates while preserving loudness, pitch, and immutable source bytes. Review labels the count and first candidate as evidence only; activity logs retain no timing, strength, or confidence values. The slice creates no note, tempo, beat grid, quantization, correction, gesture, or automatic musical decision.

Slice 6.12 turns those saved-take summaries into a genuine evidence inspector. Review groups claims by observation kind and analyzer, identifies analyzer version and provenance, orders every claim by source time, and exposes the exact span, measurements, and confidence in bounded twelve-row pages that remain usable on a phone. The inspector is a derived read-only view over existing schema-v24 observations: opening it or revealing another page writes no project state, reruns no analyzer, and promotes nothing into notes, beats, corrections, or gesture data. Unknown future observation kinds remain visible through generic labels instead of being silently discarded.

Slice 6.13 advances the Song Graph to schema v25 with a separate `performanceObservationReviews` collection. From the evidence inspector, the artist can mark one analyzer claim accurate or inaccurate, revise that verdict, or clear it back to unreviewed. Each verdict has its own stable identity and timestamps, references exactly one present observation, travels through persistence and portable project movement, and remains distinct from analyzer confidence and musical approval. Removing a source take or rerunning the analyzer that owns a claim removes only reviews whose referenced claims disappear. A verdict creates no corrected value, note, beat, MIDI, gesture, or automatic musical decision.

Slice 6.14 advances the Song Graph to schema v26 with a separate `performanceObservationCorrections` collection. After a claim is marked inaccurate, the artist can store one correction that keeps the original measurement names and units, changes at least one value, and leaves the analyzer evidence unchanged. Accurate and unreviewed verdicts drop any correction. Source-take removal and analyzer reruns cascade the same way as reviews. Corrections travel through backup, recovery, Trash, duplication, and `.maskil` packages. They still create no note, beat, MIDI, or automatic musical decision.

Slice 6.15 advances the Song Graph to schema v27 with a separate `performanceObservationGestures` collection. After a claim is marked accurate, or marked inaccurate and given a correction, the artist can promote it into one gesture snapshot. The host copies the approved measurements; the client sends only promote or clear. Unreviewed claims and uncorrected inaccurate claims cannot be promoted, and losing eligibility drops any existing gesture. Gestures travel through backup, recovery, Trash, duplication, and `.maskil` packages. They still create no note, beat, MIDI, or automatic musical decision.

Slice 6.16 turns approved pitch gestures on one original-vocal take into a transient playable-note sketch. Frequency maps to the nearest MIDI note, time uses the first tempo only, and the take starts at song tick 0 until take placement exists. Desktop Music can preview then explicitly accept those notes; undo removes only the accepted events. Loudness and onset gestures stay unused, no musical part is assigned, and dropping a later gesture does not delete already-accepted notes. The sketch is not stored and does not bump schema. Phone Review remains capture, review, and promote.

Slice 6.17 opens those saved takes on the desktop Music workspace. The studio screen can play, analyze, review, correct, and promote the same host-owned recordings, and can record another take against the current saved revision. Schema stays at v27. Take timeline placement, onset or loudness notes, and automatic musical decisions remain later work.

Slice 6.18 advances the Song Graph to schema v28 with a separate `vocalTakePlacements` collection. Desktop Music can place one original-vocal take at an explicit song bar, beat, and tick. Unplaced takes still start at tick 0. Pitch-gesture sketches add that start to take-relative timing; changing or clearing placement does not move already-accepted notes, attach audio to the timeline, or create a DAW clip. Removing the take drops its placement. Phone Review stays the capture companion. Onset or loudness notes and automatic musical decisions remain later work.

Slice 6.19 turns approved onset gestures on one original-vocal take into a transient playable-note sketch of short C4 hits. Time uses the first tempo plus the take's song placement; strength maps to velocity. Desktop Music can preview then explicitly accept those notes; undo removes only the accepted events. Pitch and loudness gestures stay unused here, no musical part is assigned, and dropping a later gesture or changing placement does not delete or move already-accepted notes. The sketch is not stored and does not bump schema. Phone Review remains capture, review, and promote. Loudness mapping remains later work.

Slice 6.20 turns approved loudness gestures on one original-vocal take into a transient playable-note sketch of short C4 hits. Time uses the first tempo plus the take's song placement; RMS between −60 and 0 dBFS maps to velocity, and peak stays unused. Desktop Music can preview then explicitly accept those notes; undo removes only the accepted events. Pitch and onset gestures stay unused here, no musical part or expression curve is assigned, and dropping a later gesture or changing placement does not delete or move already-accepted notes. The sketch is not stored and does not bump schema. Phone Review remains capture, review, and promote. Expression curves remain later work.

Slice 6.21 advances the Song Graph to schema v29 with a separate `expressionCurves` collection. Desktop Music can preview a dynamics curve from approved loudness gestures, then explicitly accept it. Points use the first tempo plus the take's song placement; RMS between −60 and 0 dBFS maps to MIDI expression 0–127. MIDI export can emit those points as CC 11 when the song also has playable notes, with CC ordered before note-on at the same tick. Undo or Remove drops only that curve. Changing placement later does not move accepted points, and removing the take does not drop the curve. Phone Review remains capture, review, and promote. Freehand point editing, extra curve kinds, and automatic retargeting remain later work.

Slice 7.1 adds versioned host-owned instrument profiles for cello and guitar. Each profile names a playable range, the arrangement jobs it can fulfill, articulations, and expressive qualities. Desktop Arrangement can inspect that knowledge. The slice does not assign an instrument to a musical part, recommend a choice, check existing notes against range, or retarget a gesture. Schema stays at v29.

Slice 7.2 derives inspectable instrument recommendations from that catalog. Assigned arrangement roles match instruments that can cover the job; an optional expressive-quality filter further requires that feeling. Matches stay in catalog order with no ranking or assignment. Desktop Arrangement can inspect them. Schema stays at v29.

Slice 7.3 grows the host catalog to version 2 with a five-instrument proof set: cello, acoustic guitar, piano, electric bass, and drum kit. Profiles remain instrument concepts rather than sample-library or VST patches. Melodic instruments keep a playable range; drum kit is unpitched and has no melodic range. Schema stays at v29. Violin, flute, clarinet, trumpet, electric guitar, and later production instruments remain later catalog waves.

Slice 7.4 compares existing playable notes with those catalog ranges. Melodic instruments report notes below or above their inclusive bounds; drum kit is not applicable. The review is inspectable only: it does not transpose, assign an instrument, or change the Song Graph. Schema stays at v29.

Slice 7.5 maps two neutral performance ideas — swell and slide — onto catalog articulations. Cello uses bow expression and slide, acoustic guitar uses picking and bend, piano approximates a swell as strike, and electric bass uses finger. Drum kit and piano or bass slides stay not applicable rather than inventing cello-like technique. The map is inspectable only: it does not retarget a recorded gesture, assign an instrument, or change the Song Graph. Schema stays at v29.

Slice 7.6 consumes approved pitch and loudness gestures on one original-vocal take and projects them onto cello and acoustic guitar at once. The same swell becomes cello bow expression and guitar picking; the same slide becomes cello slide and guitar bend. Out-of-range slide pitches are reported, not transposed. Piano, bass, and drum kit are not this slice’s retargeters. The projection is inspectable only: it does not assign `instrumentProfileId`, emit MIDI, or change the Song Graph. Schema stays at v29.

Slice 7.7 advances the Song Graph to schema v30 with an optional catalog `instrumentProfileId` on each musical part. Desktop Arrangement can name cello, acoustic guitar, piano, electric bass, or drum kit, or leave the part unassigned. The choice is explicit and reversible. Unknown slugs are rejected. Schema-v29 parts migrate as unassigned. The slice does not retarget a gesture, persist a cello or guitar sketch, auto-pick from recommendations, require the instrument to cover the job, or emit MIDI program changes. Catalog stays at version 2. Phone Arrangement remains hidden.

Slice 7.8 persists a reviewed cello or guitar retarget onto a musical part that already names that instrument. Schema v31 tags accepted swell curves with optional `instrumentProfileId`; schema-v30 curves migrate as unassigned. In-range slides join the named part. Out-of-range slides are skipped, not transposed. The host does not auto-assign, auto-create a part, retarget piano, bass, or drum kit, or emit MIDI program changes. Desktop Music can accept the sketch; Phone Music remains hidden. Catalog stays at version 2.

Slice 7.9 projects those same approved gestures onto piano, electric bass, and drum kit. Piano strike and bass finger cover swell; piano and bass slides, and both kit gestures, stay not applicable. Named piano or bass parts can store the swell. Kit has nothing persistable here. Schema stays at v31. Catalog stays at version 2. Phone Music remains hidden.

Slice 7.10 maps approved onset gestures onto drum-kit Hit. Cello, guitar, piano, and bass do not take those hits. Named kit parts store them as playable notes using the same C4 placeholder and strength-to-velocity mapping as the onset-note sketch. Schema stays at v31. Catalog stays at version 2. The host does not choose a kit piece, emit a program change, or move hits onto a drum MIDI channel. Phone Music remains hidden.

Slice 7.11 grows the host catalog to version 3 with four orchestration concepts: violin, flute, clarinet, and trumpet. They remain instrument concepts rather than patches. Desktop Arrangement can inspect them and name one on a musical part. Swell, slide, and hit stay unmapped for these instruments. Schema stays at v31. Electric guitar, synths, and other Wave 3 instruments remain later work. Phone Arrangement remains hidden.

Slice 7.12 maps those Wave 2 instruments onto the existing swell, slide, and hit gestures without inventing cello or kit technique. Violin swell is bow expression; violin slide stays a slide. Flute swell is breath. Clarinet and trumpet swells are legato. Wind slides and Wave 2 hits stay not applicable. Desktop Music can store a reviewed retarget on a part that already names that instrument. Schema stays at v31. Catalog stays at version 3. Phone Music remains hidden.

Slice 7.13 grows the host catalog to version 4 with three modern production concepts: synth pad, synth lead, and electric guitar. They remain instrument concepts rather than patches. Desktop Arrangement can inspect them and name one on a musical part. Swell, slide, and hit stay unmapped for these instruments so the host does not invent cello, guitar, or kit technique. Schema stays at v31. Organ, ensemble strings, and other later instruments remain later work. Phone Arrangement remains hidden.

Slice 7.14 maps those Wave 3 instruments onto the existing swell, slide, and hit gestures without inventing cello, acoustic-guitar, or kit technique. Synth pad swell is pad; synth pad does not take slides. Synth lead swell is filter; synth lead slide is portamento. Electric guitar swell is distortion; electric guitar slide is bend. Wave 3 hits stay not applicable. Desktop Music can store a reviewed retarget on a part that already names that instrument. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.15 maps drum-kit Hit onto one inspectable General MIDI percussion pitch: Acoustic Bass Drum at C2 (MIDI 36). Preview and persist use that pitch instead of a melodic C4 placeholder. The voice-to-MIDI onset sketch stays C4. The host does not choose snare or hat, emit a program change, or move hits onto channel 10. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.16 exports notes on a musical part that already names drum kit onto MIDI channel 10. Acoustic Bass Drum stays C2. Unassigned notes and pitched-instrument parts stay on channel 0. Dynamics still emit as CC 11 on channel 0. MIDI does not emit a program change or assign channels to cello, guitar, or other pitched instruments. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.17 exports named catalog parts on inspectable MIDI channels in catalog order, skipping channel 10 except for drum kit. Cello is 2, acoustic guitar 3, piano 4, electric bass 5, violin 6, flute 7, clarinet 8, trumpet 9, synth pad 11, synth lead 12, and electric guitar 13. Drum kit stays on 10. Unassigned notes stay on 1. Tagged dynamics use the same channel as the named instrument. MIDI still does not emit a program change. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.18 emits inspectable General MIDI program changes for named pitched catalog parts on those channels. Cello is program 43, acoustic guitar (steel) 26, piano 1, electric bass (finger) 34, violin 41, flute 74, clarinet 72, trumpet 57, synth pad (warm) 90, synth lead (sawtooth) 82, and electric guitar (distortion) 31. Drum kit still has no program change. Unassigned notes still have none. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.19 emits tagged dynamics on each instrument’s inspectable MIDI controller. Flute swell is Breath Controller (CC 2). Synth lead swell is Brightness (CC 74). Other catalog swells stay Expression (CC 11). Untagged dynamics stay CC 11. Drum kit still has no dynamics controller. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.20 declares an inspectable pitch-bend range of ±2 semitones for cello and violin slides and for acoustic-guitar and electric-guitar bends. MIDI export emits that range as RPN 0 and does not move the pitch wheel. Synth-lead portamento is not pitch bend. Drum kit still has no range. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.21 declares an inspectable Portamento controller for synth-lead slides. MIDI export emits CC 65 Off so stored notes stay discrete. The host does not turn portamento on or invent a glide between pitches. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.22 exports a format-1 Standard MIDI File with a named conductor track and one named track for Unassigned and each catalog instrument that actually exports notes. Unused catalog instruments do not get a track. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.23 emits the stored song key as a MIDI key signature on the conductor track. Conventional major and minor spellings map to sharps or flats; unusual spellings are omitted rather than invented. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

Slice 7.24 emits each stored section title as a MIDI marker on the conductor track at that section's start tick. Songs without sections omit markers rather than inventing them. The host does not emit lyric events or cue points. Schema stays at v31. Catalog stays at version 4. Phone Music remains hidden.

This repository contains the product definition, architectural principles, delivery roadmap, and an executable songwriting prototype. The Prototype boundary and editable-demo MVP are complete through slice 5.15. The application spans idea capture, structured lyrics and prosody, timeline, harmony and voicing, arrangement intent, playable notes, MIDI export, role-aware musical parts, deterministic role realizations through accents, assembled-part audition, basic song transport, minimal note/part editing, and derived hear–revise readiness review. Additional role realization should follow only when artist validation shows that a vertical song needs it.

The schema-v2 timeline foundation uses 480 pulses per quarter note (PPQ), converts between bar/beat/tick positions and absolute ticks, and gives every ordered section a stable timeline placement and editable bar duration. Section edits reflow these placements without changing section identities. This is a musical coordinate system only; it does not provide transport, playback, MIDI generation, or audio timing.

The schema-v3 lyric-document foundation tokenizes structured lyric lines into individually addressable words while preserving the original line text. Unchanged words retain their identifiers when nearby words are inserted or removed. Schema v4 adds ordered syllable entities with stable IDs and `Manual`, `Analyzer`, or `Imported` provenance. Schema v5 adds addressable punctuation and ordered phrases that reference existing word IDs. Schema v6 adds optional syllable stress marks with `None`, `Secondary`, `Primary`, or `Emphasized` levels and explicit provenance. Schema v7 adds optional phrase-relative prosodic patterns whose identified units reference existing syllable IDs and record `Weak`, `Neutral`, or `Strong` weight. Schema v8 adds stable syllable placements at artist-selected bar, beat, and tick coordinates relative to the owning section. Schema v9 lets artists preserve multiple named rhythm options by snapshotting a phrase's current placements, compare those options, and explicitly apply one back to the authoritative beat map. Schema v10 adds optional breath points after existing syllables with stable identities and provenance. Derived prosody scoring can review active placements or saved rhythm options for stress conflicts, breath room, and crowding, but those scores are not stored as project schema fields. Schema v11 adds creative locks for lyric lines and phrase rhythm so accepted wording or timing can be protected from silent overwrite. A derived lyric-timeline view projects those placements onto absolute song time so the editor can show how lyrics fit the section timeline and optionally overlay a saved rhythm option. Schema v12 adds a song-level musical key (tonic, accidental, mode) with deterministic pitch-class, scale, interval, and small-chord theory helpers. Schema v13 adds ordered section harmony chords with stable identities, chord symbols, section-relative start positions, bar durations, and provenance. Schema v14 lets artists preserve multiple named harmony options and explicitly apply one to the active progression. Schema v15 adds optional registered chord voicings with stable voicing and voice identities, spelled notes, octaves, provenance, and configurable register bounds. A derived chord-movement review now uses registered voices when both chords provide them and explains retained notes, leaps, spacing, voice-count changes, and similar-direction perfect intervals; unvoiced chords retain the earlier pitch-class review. The editor does not generate or rank candidates, invent voicings, invent breaths from punctuation, invent locks, invent progressions, or automatically accept a choice.

Schema v16 adds a song-level arrangement blueprint of stable section plans. Artists can describe each existing section with explicit energy and density intentions and view the resulting energy curve without selecting instruments or generating performances. Schema v17 adds stable, artist-authored role assignments so sections can request musical jobs such as pulse, texture, low-end support, transitions, or hook reinforcement without naming instruments. Schema v18 adds stable playable note events with registered pitch, absolute start tick, duration, and velocity. Schema v19 adds stable, artist-authored musical parts that connect an assigned section role to selected approved note IDs without generating material. Schema v30 adds an optional catalog `instrumentProfileId` on each musical part so the artist can name cello, guitar, piano, bass, or drum kit without generating notes, retargeting a gesture, or emitting a MIDI program change; schema-v29 parts migrate as unassigned. Existing harmony can be projected into a transient playable-note sketch: registered voicings remain authoritative, missing voicings are labeled as temporary previews, and notes enter the Song Graph only after the artist explicitly accepts the sketch. A section marked for low-end support can now preview a deterministic lower-register layer derived from the lowest approved note at each onset; acceptance creates the needed notes and their role-aware part as one reversible decision. A section marked for pulse can preview short mid-register hits on each approved onset; acceptance likewise creates the needed notes and pulse part as one reversible decision. A section marked for harmony support can preview the same chord-and-voicing projection as the playable sketch and accept it as a harmony-support musical part, reusing matching approved notes when present. A section marked for texture can preview the upper half of each chord’s voices as softer sustained color and accept that as a texture musical part, likewise reusing matching notes. A section marked for hook reinforcement can preview beat-capped, emphasized hits on the highest approved note at each onset and accept that as a hook-reinforcement musical part. A section marked for countermelody can preview softer response notes on the second-highest approved pitch at stacked onsets and accept that as a countermelody musical part. A section marked for accents can preview short, strong hits on the highest approved note at each bar downbeat and accept that as an accents musical part. Once musical parts exist, the arrangement workspace can audition their assembled notes together with a transient Web Audio preview that does not change the Song Graph. A basic song transport can also play those assembled notes from the absolute timeline with a live bar/beat playhead. The advanced editor can revise an approved note’s pitch, onset, duration, and velocity and can rename parts or change their approved-note membership while preserving stable identities and undo/redo. Approved notes can be exported as a format-1 Standard MIDI File that preserves the existing PPQ, tempo, meter, pitch, timing, duration, and velocity without inventing notes or instruments. The browser editor can also audition an existing section progression with simple generated tones. It follows the saved tempo and harmony timing, prefers registered voicings, and uses temporary preview voicings when necessary; playback remains transient and does not change the Song Graph or activity history. Maskil Forge remains early-stage: it is not a functional DAW or complete audio generator. Automatic lyric analysis, realization of other arrangement roles, persisting retargeted performances, VST hosting, vocal analysis, procedural music generation, recording, and mixing have not been implemented.

Undo and redo history is currently session-only. Section edits, manual phrase split/join actions, syllable stress decisions, prosodic-weight decisions, syllable placement decisions, rhythm-option capture, rename, removal, and application, breath-point decisions, creative lock/unlock decisions, song-key changes, harmony-chord edits, harmony-option capture, rename, removal, and application, section arrangement decisions, arrangement-role assignments, playable-note edits, and musical-part edits participate in that history. Redo restores the same phrase, pattern, prosodic-unit, placement, candidate, candidate-event, breath-point, lock, harmony-chord, harmony-candidate, harmony-candidate-event, section-arrangement, role-assignment, note-event, and musical-part identities; undo restores the exact prior values and provenance. Saved project content survives closing and reopening, but the command history itself does not.

Project persistence validates a temporary JSON file before replacing the active copy and retains the previous validated save as an ignored local backup. Invalid or malformed project files are not silently promoted to backups: they are preserved once by content as recovery copies, while healthy songs remain available in the library. Development writes to the API's ignored `App_Data/projects` directory; a non-development host writes to the operating system's per-user `Maskil Forge/Library` application-data directory unless the absolute `MaskilForge:LibraryPath` configuration setting selects another location. Manifest-owned asset bytes are immutable, content-verified, and paired with the project through backup, session recovery, Trash, restore, corrupt-data preservation, and permanent deletion. User-facing saved-version history is not implemented yet. An asset-owning `.maskil` package now carries verified original-vocal bytes with the Song Graph; JSON-only interchange still refuses referenced media.

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
