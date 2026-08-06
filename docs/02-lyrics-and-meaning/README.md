# 02 — Lyrics and Musical Meaning

This planned Maskil Engine layer will turn text and creative intent into annotated material that later generators can use. Speech and vocal prosody are treated as core musical inputs, not optional decoration.

## Capture before structure

Maskil Forge allows an artist to preserve an unstructured lyric draft before assigning words to Verse, Chorus, or other Song Graph sections. The raw draft may contain finished lyrics, fragments, prose, images, themes, or notes. It remains editable source material and is not destructively replaced when structured lyric-line objects are created.

The current foundation stores this source as `RawLyricDraft` and stores section lyrics as individually identified `LyricLine` objects. Automatic interpretation, syllable analysis, stress analysis, and suggested section boundaries remain planned capabilities rather than implemented behavior.

## Processing order

1. Parse raw lyrics into sections, lines, phrases, words, and syllables.
2. Annotate stress, rhyme, repeated language, important words, and breath opportunities.
3. Describe narrative roles and emotional transitions.
4. Generate multiple rhythmic/prosody candidates.
5. Score candidates and let the artist audition, edit, or lock one.
6. Build harmony and energy plans around the approved meaning and phrasing.

## Prosody model

A candidate maps syllables to musical time and stores:

- Onset, duration, stress, and melisma
- Breath points and phrase boundaries
- Sustained-vowel opportunities
- Crowding, syncopation, and vocal difficulty
- Natural-stress, hook-clarity, and genre-fit scores

The system should expose why a score is low instead of only showing a number.

## Narrative and energy

Each section has an energy curve, tension curve, narrative role, and intended contrast with adjacent sections. These are normalized guides, not audio volume values.

Emotional intent maps to musical tendencies. For example, vulnerability may reduce density and register width, while confrontation may increase accents and rhythmic activity. These mappings remain editable data.

## Theory foundation

Implement notes, intervals, scales, modes, keys, chords, inversions, Roman numerals, cadences, voice leading, transposition, and range checking as deterministic code.

## Planned completion gate

This future gate will be met when:

- Lyrics have stable phrase/word/syllable IDs.
- A phrase produces at least three editable rhythm candidates.
- Scores identify stress conflicts, breath issues, and crowding.
- Harmony can be transposed without breaking note relationships.
- The user can lock an accepted lyric, rhythm, or chord decision.
