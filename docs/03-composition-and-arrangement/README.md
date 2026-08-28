# 03 — Composition and Arrangement

This Maskil Engine layer now supports the editable-demo MVP. Section energy and density, arrangement-role assignments, registered harmony voicings, playable note events, MIDI export, role-aware musical parts, deterministic role realizations through accents, assembled-part audition, basic song transport, minimal note/part editing, and derived demo-readiness review all preserve the artist's locks and choices.

## Build roles before instruments

The arranger reasons first about roles:

```text
Foundation  Pulse  Harmony  Low-end support
Texture     Accent Transition Countermelody Hook reinforcement
```

It should then recommend instruments capable of fulfilling those roles within genre, register, energy, and playability constraints. Recommendations should consider expressive behavior, range, articulation, and timbre rather than instrument name alone.

## Data-driven knowledge

Genre profiles describe probabilities and tendencies: tempo, meter, density, dynamics, form, harmony, drums, and vocal phrasing. Instrument profiles describe range, timbre, attack, sustain, articulations, roles, limitations, and renderer mappings.

Profiles belong in versioned data files, not hardcoded conditionals. Catalog entries name instrument concepts. Renderer mappings — SoundFont, Kontakt, VST, or an external DAW — belong to Milestone 8 and must not become the instrument identity. Vocal-production processing roles belong to Milestone 9 and must not become instrument-profile identity either.

Slice 7.1 adds the first catalog: cello and guitar profiles that name range, arrangement roles, articulations, and expressive qualities. Desktop Arrangement can inspect that knowledge.

Slice 7.2 matches catalog instruments to assigned arrangement roles, optionally filtered by expressive quality. Matches stay in catalog order. They are inspectable only: the slice does not assign an instrument, rank a winner, check range, or retarget a gesture.

Slice 7.3 replaces the two-instrument catalog with version 2 of the host-owned proof set: cello, acoustic guitar, piano, electric bass, and drum kit. These are instrument concepts, not renderer patches. Drum kit is unpitched.

Slice 7.4 reports which existing notes sit outside a catalog instrument’s inclusive range. Drum kit range does not apply. The review does not transpose, assign, or retarget.

Slice 7.5 maps swell and slide onto named catalog articulations without retargeting a recorded gesture or assigning an instrument. Drum kit remains not applicable.

Slice 7.6 retargets those same gestures from one original-vocal take onto cello and acoustic guitar, side by side. The projection is inspectable only.

Slice 7.7 lets the artist name a catalog instrument on a musical part. The assignment is optional Song Graph data, explicit, and reversible. It does not retarget a gesture, persist a performance sketch, auto-pick a recommendation, or emit MIDI.

Slice 7.8 persists a reviewed cello or guitar retarget onto a musical part that already names that instrument. In-range slides become notes on the part; swells become a dynamics curve tagged with the same catalog instrument. Out-of-range slides are skipped, not transposed.

Slice 7.9 adds piano, electric-bass, and drum-kit adapters from the same approved gestures. Piano strike and bass finger cover swell; piano and bass slides, and both kit gestures, stay not applicable. Swells can be stored on a part that already names piano or bass. Kit has nothing persistable here.

Slice 7.10 maps approved onset gestures onto drum-kit Hit. Pitched instruments do not take those hits. Named kit parts can store them. General MIDI drum mapping and later catalog waves remain later Milestone 7 slices.

Slice 7.11 grows the catalog to version 3 with violin, flute, clarinet, and trumpet. Artists can inspect them and name one on a musical part. Gesture maps stay unused for these instruments. Electric guitar and synths remain later work.

Slice 7.12 maps swell onto those instruments’ own articulations and lets a named part store the result. Violin can also take a slide. Flute, clarinet, and trumpet slides stay unused, and none of them take kit hits.

Slice 7.13 grows the catalog to version 4 with synth pad, synth lead, and electric guitar. Artists can inspect them and name one on a musical part. Gesture maps stay unused for these instruments.

Slice 7.14 maps swell onto those instruments’ own articulations and lets a named part store the result. Synth pad swell is pad. Synth lead can also take a portamento slide. Electric guitar can also take a bend. Synth pad slides stay unused, and none of them take kit hits.

Slice 7.15 maps drum-kit Hit onto General MIDI Acoustic Bass Drum (C2) so stored kit hits are a percussion pitch instead of a melodic C4. The host does not choose snare or hat, and does not move those hits onto a drum MIDI channel.

Slice 7.16 exports named drum-kit notes on MIDI channel 10. Unassigned notes and pitched-instrument parts stay on channel 0. MIDI does not emit a program change.

Slice 7.17 exports named catalog parts on inspectable MIDI channels. Drum kit stays on 10. Unassigned notes stay on 1. MIDI does not emit a program change.

Slice 7.18 emits inspectable General MIDI program changes for named pitched parts. Drum kit and unassigned notes still have none.

Slice 7.19 emits tagged dynamics on each instrument’s inspectable MIDI controller. Flute swell is CC 2. Synth lead swell is CC 74. Other catalog swells and untagged curves stay CC 11.

Slice 7.20 declares an inspectable ±2-semitone pitch-bend range for cello, violin, acoustic guitar, and electric guitar. MIDI does not move the pitch wheel. Synth-lead portamento is not pitch bend.

Slice 7.21 declares synth-lead portamento as CC 65 and keeps it off so stored notes stay discrete.

Slice 7.22 exports a format-1 MIDI file with a named conductor track and one named track per used catalog channel.

Slice 7.23 emits the stored song key as a MIDI key signature on the conductor track.

Slice 7.24 emits each stored section title as a MIDI marker on the conductor track.

Slice 7.25 emits each stored syllable placement as a MIDI lyric on the conductor track.

Slice 7.26 emits each stored section harmony chord as MIDI text on the conductor track.

Slice 7.27 emits each stored breath after a placed syllable as a MIDI cue point on the conductor track.

Slice 7.28 preserves artist-authored non-ASCII track names, section markers, and placed-syllable lyrics as strict UTF-8 MIDI metadata instead of silently deleting characters. ASCII remains byte-for-byte unchanged. Text payloads use Standard MIDI variable-length sizes, control characters stay off the file, and metadata is bounded to 80 Unicode scalar values. This is a practical multi-language interchange contract; the Song Graph remains authoritative when a legacy MIDI reader supports only ASCII. Schema stays at v31 and the catalog stays at version 4.

Slice 7.29 preserves normalized Unicode in the suggested `.mid`, `.maskil`, and `.maskil.json` download names. The shared filename rule replaces path-unsafe punctuation, removes control and formatting characters, avoids Windows-reserved stems, and bounds the result without transliterating artist-authored scripts. Export payloads and project titles remain unchanged.

Slice 7.30 makes the current song-form boundary explicit in Standard MIDI export. Every emitted track ends no earlier than the exclusive end of the last stored section, while a later approved note or controller event remains authoritative for that track. Stored section bars are an editable arrangement plan; they are not calculated from lyric length. Syllable placements identify onsets but do not claim sung duration, and `NoteEvent` start and duration remain the authority for playable material. Extending a track to the planned boundary adds no notes, rests, lyrics, audio, or inferred performance. Songs without sections retain event-derived length.

## Generator order

1. Section energy and density plan
2. Harmony candidates
3. Vocal melody or melody support
4. Instrument-role assignment
5. Bass and drum foundation
6. Harmony voicings and textures
7. Countermelodies and hook reinforcement
8. Transitions and fills

Each generator returns multiple scored candidates and declares what source data it used. Regenerating one layer must preserve locks and unaffected layers.

## First audible target

Use a simple preview renderer before VST hosting. MIDI is the initial interchange and performance-control layer, not the sound itself. Preview tones and later VST rendering realize instrumental and supporting parts; they are not the lead vocal. The important proof is that the same structured project can drive audible playback through replaceable renderers, not that the first sounds are release quality.

## Current boundary and completion gate

The current implementation can derive inspectable role ideas from approved material, accept each as one reversible musical part, revise approved note details and part membership, audition existing harmony, audition assembled musical parts together, play the song with a basic transport playhead, explain which sections are ready for that audible flow, and export approved notes as MIDI. The vertical hear–revise–save–export workflow is covered end to end.

The broader completion gate will be met when:

- The engine generates a complete arrangement for a small supported genre set.
- All generated notes are editable and traceable to a command and seed.
- Regenerating drums does not change locked harmony or melody.
- Range and register collisions are reported.
- MIDI export matches preview timing.
