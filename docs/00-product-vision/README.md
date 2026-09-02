# 00 — Product Vision

## Product definition

Maskil Forge is a planned human-driven procedural songwriting and arrangement engine. It will connect lyrical meaning, speech and vocal prosody, music theory, instrument intelligence, procedural composition, MIDI, human performance, and replaceable audio renderers through a canonical Song Graph.

Maskil Forge is the user-facing application and creative workspace. Maskil Engine is the procedural framework beneath it. They are two naming layers within one product and repository; separate products or repositories are not currently planned.

## Name

"Maskil" is a biblical term appearing in several Psalm headings and is associated with contemplation, instruction, or understanding. Its exact original meaning is not stated here as certain. "Forge" communicates deliberate human craftsmanship. Together, the name reflects understanding lyrical intent and deliberately shaping it into music.

Tagline: **Understand the words. Forge the music.**

## Primary user

Maskil Forge is intended primarily for aspiring and independent singer-songwriters who have lyrics, vocal ideas, or emotional intent but may not understand prosody, arrangement, orchestration, MIDI, or how a particular instrument should perform an idea.

The aim is not to hide music behind a prompt. It is to make musical structure understandable and controllable while helping an artist cross knowledge gaps.

The product begins before formal structure. An artist may first capture fragments, prose, themes, or complete lyrics in a raw draft, then choose when to shape that material into Verse, Chorus, and other editable sections. Maskil Forge should never require the artist to understand song form before preserving an idea.

## Creator experience

The interface should follow the natural creative progression from an idea to words, song shape, musical refinement, harmony, and arrangement. This progression is a guide, not a locked wizard: newcomers should always know the next useful action, while experienced songwriters may move directly to any available capability.

Progressive disclosure keeps the first encounter focused on songwriting rather than terminology. Lyrics and meaning appear before formal structure. Timing, syllables, phrasing, prosody, harmony, and analysis remain available as contextual explorations that first explain their creative purpose. Technical metrics may support a decision, but they should not imply that theory knowledge or a particular score is required to write a valid song.

This presentation principle does not create a separate beginner mode or reduce the engine. A beginner should meet a songwriting companion; deeper exploration should reveal the same composition workstation used by an expert.

## What the product should become

Maskil Forge should feel more like a game engine for songs than a one-click audio generator. It should offer high-level creative controls while preserving words, syllables, beats, notes, chords, tracks, automation, takes, locks, and decisions as editable project data.

Planned interactions include:

- Directing a bridge to feel like surrender while delaying resolution until the chorus.
- Humming a countermelody and retargeting its gesture to a cello, while the recorded human vocal remains the lead.
- Locking lyrics and chords, then regenerating only drums.
- Asking why a phrase feels crowded or difficult to sing.
- Choosing an instrument by expressive quality and musical role rather than by orchestration vocabulary.

These are goals, not claims about currently implemented features.

## Division of responsibility

### The artist supplies

- Lyrics, intent, and taste
- Vocal and gestural ideas
- Edits, comparisons, locks, and approvals
- Revisions and final emotional judgment
- The recorded human lead-vocal performance, which remains the final authoritative vocal

### Maskil Engine will supply

- Deterministic time, theory, and range rules
- Procedural candidate generation
- Constraint enforcement and shared scoring
- Editable MIDI, automation, arrangement, and provenance data
- Renderer-independent composition logic
- Vocal capture, analysis, guidance, take management, and reviewable production assistance that never replaces the lead singer

### The AI director will supply

- Interpretation of natural-language intent
- Proposed, typed engine commands
- Candidate comparisons and decision explanations
- Focused questions when ambiguity materially affects the result

AI is a director and interpreter, not the sole composer. It must not replace the Song Graph, theory rules, command history, user locks, artist approval, or the artist's lead vocal.

## Intended vocal workflow

The artist's human lead vocal is the final authoritative performance. Maskil Forge is a songwriting and production companion for that singer. It is not a vocal generator and must not be framed as creating or replacing the final lead singer.

Maskil Forge may:

- Capture and analyze vocals for pitch, onset, loudness, timing, and prosody.
- Provide pitch, timing, and prosody guidance that informs the singer without overwriting the take.
- Create guide melodies and related rehearsal aids so the artist can hear and follow a part.
- Preserve takes, punch-ins, and comps as inspectable performance history.
- Suggest or apply reviewable vocal production settings, including VST or other audio processing that assists the recorded vocal.
- Let the artist choose a desired vocal result, then propose the processing roles needed to achieve it, without requiring plugin literacy.
- Use voice analysis to drive editable musical or instrument-performance data in the Song Graph, such as melody, rhythm, expression, or retargeted instrumental parts.

Those operations produce or revise inspectable project data. They remain subject to artist approval, locks, undo, and the same command model as every other layer. A generated or processed sound may preview, guide, or support the singer; it does not become the canonical lead vocal. Processing remains non-destructive: the original take stays authoritative until the artist previews and accepts a production change.

Detail lives in [Performance and sound](../04-performance-and-sound/README.md).

## Architectural principles

- A song exists as structured, editable data before it exists as finished audio.
- The canonical Song Graph binds lyrics, syllables, beats, melody, harmony, arrangement, performances, instruments, automation, and provenance.
- The artist's recorded human vocal remains the final authoritative lead performance.
- Voice-to-instrument is performance capture and retargeting, not simple timbre replacement or a substitute lead vocal.
- Instrument recommendations depend on role, expressive behavior, range, articulation, and timbre.
- MIDI is the initial interchange and performance-control layer, not the sound itself.
- Audio rendering remains replaceable. VSTs and other processors may assist the artist's vocal or realize instrumental parts, but they must not own the composition or replace the singer.
- Vocal production is intent-first. The artist names a desired result; the host compiles processing roles. No particular DSP or VST is the canonical vocal chain.
- AI interprets and directs deterministic tools rather than owning the composition or the lead vocal.

## Product boundaries

Maskil Forge is not a Suno clone, an autonomous hit-song generator, or software that generates or replaces a singer. Its composition logic should remain independent of VSTs, SoundFonts, external DAWs, and possible future neural renderers.

The first useful release should not attempt to be a complete DAW or universal VST host. It should first prove that lyrics and intent can become a coherent, audible, editable song blueprint without losing human control.

## Delivery surfaces

Maskil Forge is web-first and local-first. The browser and future installable PWA provide the broad composition, lightweight capture, review, and approval surface. A future desktop shell extends that same application only where native production capabilities—such as low-latency audio, MIDI hardware, local project assets, VST3 hosting, or offline rendering—require it.

Every client uses the same canonical Song Graph, commands, migrations, provenance, locks, and artist decisions. Phone scope prioritizes Idea, Words, Shape, rough human-vocal capture, Review, and Approve rather than reproducing a desktop DAW on a small screen.

A portable, versioned Maskil project package and local/offline storage come before accounts or cloud synchronization. Optional cloud backup or device sync may be added later, but neither may become the only way to open, move, edit, or recover an artist's work. Packaging frameworks and storage providers remain replaceable implementation details.

## Long-term success test

The product vision is fulfilled when an artist can move from lyrics and intent to an editable arrangement, direct instruments through musical or vocal gestures, record and complete their own lead vocal with guidance and reviewable production help, revise individual layers without losing approved work, and export the project or continue it in a DAW—without the product supplying a generated replacement for that lead vocal.

## Current status

The repository contains an executable songwriting MVP and its product documentation. Local project discovery, recovery, Trash, portable project interchange, raw and structured lyrics, manual syllable and prosody decisions, editable section timing, harmony and arrangement intent, approved playable notes, instrument-aware synthesized browser preview, rough-vocal recording and review, narrow browser pitch/onset/loudness analysis, gesture-to-note sketches, and format-1 MIDI interchange are implemented. The current editor labels section bars as arrangement planning rather than lyric-derived or final performed duration.

Automatic lyric interpretation, generated rhythm candidates, syllable duration, rests and melisma, synchronized audio clips, persistent undo history, bundled or production-quality instrumental rendering, vocal-production processing, mixing, and AI direction remain planned. Desktop Music can use an artist-supplied SF2, SF3, or DLS bank for real sample-based General MIDI preview, but the bank remains device-local and tab-scoped. The installable web shell supports browser-owned offline lyric capture and view-only saved snapshots; complete offline project editing and device synchronization are not implemented.
