# 02 — Lyrics and Musical Meaning

This planned Maskil Engine layer will turn text and creative intent into annotated material that later generators can use. Speech and vocal prosody are treated as core musical inputs, not optional decoration.

## Capture before structure

Maskil Forge allows an artist to preserve an unstructured lyric draft before assigning words to Verse, Chorus, or other Song Graph sections. The raw draft may contain finished lyrics, fragments, prose, images, themes, or notes. It remains editable source material and is not destructively replaced when structured lyric-line objects are created.

The current foundation stores this source as `RawLyricDraft` and stores section lyrics as individually identified `LyricLine` objects. Each structured line now contains ordered `LyricWord` tokens with stable `LyricWordId` values and exact source offsets. Apostrophes and internal hyphens remain part of their word; surrounding punctuation remains untouched in the original line text.

Editing a line reconciles the previous and next word sequences so unchanged words retain their identities when nearby words are inserted or removed. A read-only `LyricDocument` projection connects the raw draft to structured lines and their owning sections without duplicating stored creative state.

`LyricSyllable` and `SyllableId` now provide an ordered, provenance-aware representation. Each syllable stores its stable ID, text, zero-based position within the word, and source: `Manual`, `Analyzer`, or `Imported`. Matching syllable IDs survive nearby boundary insertions, and an artist correction replaces provenance with `Manual` without allowing a future analyzer to silently become authoritative. The editor accepts boundaries separated by `|` and clearly identifies manual data.

Empty syllable collections mean “not analyzed,” not “zero syllables.” The current slice does not guess pronunciation: analyzer and imported provenance are domain capabilities for later typed integrations, not claims that an analyzer or importer already exists. Automatic syllable extraction, automatic stress detection, rhyme, breath analysis, suggested structure, rhythm candidates, and musical placement remain planned rather than implemented.

`StressMark` is an optional annotation on an existing syllable identity. Its level is `None`, `Secondary`, `Primary`, or `Emphasized`, and its provenance is `Manual`, `Analyzer`, or `Imported`. No mark means the artist has not made a decision; `None` means the artist explicitly intends no stress. The editor writes `Manual` marks only, preserves them on surviving syllables, and includes the change in session undo/redo. Analyzer and imported provenance are representation capabilities, not implemented analysis features.

`LyricPunctuation` preserves punctuation groups as identified tokens with exact source offsets while apostrophes and internal hyphens remain part of their words. `LyricPhrase` stores a stable ID, zero-based position, provenance, and an ordered list of existing word IDs. Every word belongs to exactly one contiguous phrase. A new or migrated line begins as one `Default` phrase; split and join operations are explicit artist actions and produce `Manual` provenance. Nearby word edits retain surviving phrase and punctuation IDs without copying or rewriting lyric text.

## Processing order

1. Parse raw lyrics into sections, lines, phrases, words, and syllables. Word tokenization, punctuation identity, artist-controlled syllable boundaries, manual phrase structure, and artist-authored syllable stress are now foundational; automatic analysis remains planned.
2. Analyze rhyme, repeated language, important words, and breath opportunities without replacing artist-authored stress decisions.
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
