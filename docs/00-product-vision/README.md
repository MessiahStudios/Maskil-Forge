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
- Humming a countermelody and retargeting its gesture to a cello.
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
- The intended final human lead-vocal performance

### Maskil Engine will supply

- Deterministic time, theory, and range rules
- Procedural candidate generation
- Constraint enforcement and shared scoring
- Editable MIDI, automation, arrangement, and provenance data
- Renderer-independent composition logic

### The AI director will supply

- Interpretation of natural-language intent
- Proposed, typed engine commands
- Candidate comparisons and decision explanations
- Focused questions when ambiguity materially affects the result

AI is a director and interpreter, not the sole composer. It must not replace the Song Graph, theory rules, command history, user locks, or artist approval.

## Architectural principles

- A song exists as structured, editable data before it exists as finished audio.
- The canonical Song Graph binds lyrics, syllables, beats, melody, harmony, arrangement, performances, instruments, automation, and provenance.
- Human vocals remain the intended final lead performance.
- Voice-to-instrument is performance capture and retargeting, not simple timbre replacement.
- Instrument recommendations depend on role, expressive behavior, range, articulation, and timbre.
- MIDI is the initial interchange and performance-control layer, not the sound itself.
- Audio rendering remains replaceable.
- AI interprets and directs deterministic tools rather than owning the composition.

## Product boundaries

Maskil Forge is not a Suno clone, an autonomous hit-song generator, or software intended to replace a singer. Its composition logic should remain independent of VSTs, SoundFonts, external DAWs, and possible future neural renderers.

The first useful release should not attempt to be a complete DAW or universal VST host. It should first prove that lyrics and intent can become a coherent, audible, editable song blueprint without losing human control.

## Long-term success test

The product vision is fulfilled when an artist can move from lyrics and intent to an editable arrangement, direct instruments through musical or vocal gestures, record their own lead vocal, revise individual layers without losing approved work, and export the project or continue it in a DAW.

## Current status

The repository contains the documentation foundation and an early executable songwriting foundation. Local project discovery, raw lyric capture, ordered sections, identified lyric lines, JSON persistence, and reversible section operations are implemented. Musical interpretation, prosody, composition, MIDI, audio, performance capture, and AI direction remain planned.
