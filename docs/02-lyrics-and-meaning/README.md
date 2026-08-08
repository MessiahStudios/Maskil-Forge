# 02 — Lyrics and Musical Meaning

This planned Maskil Engine layer will turn text and creative intent into annotated material that later generators can use. Speech and vocal prosody are treated as core musical inputs, not optional decoration.

## Capture before structure

Maskil Forge allows an artist to preserve an unstructured lyric draft before assigning words to Verse, Chorus, or other Song Graph sections. The raw draft may contain finished lyrics, fragments, prose, images, themes, or notes. It remains editable source material and is not destructively replaced when structured lyric-line objects are created.

The current foundation stores this source as `RawLyricDraft` and stores section lyrics as individually identified `LyricLine` objects. Each structured line now contains ordered `LyricWord` tokens with stable `LyricWordId` values and exact source offsets. Apostrophes and internal hyphens remain part of their word; surrounding punctuation remains untouched in the original line text.

Editing a line reconciles the previous and next word sequences so unchanged words retain their identities when nearby words are inserted or removed. A read-only `LyricDocument` projection connects the raw draft to structured lines and their owning sections without duplicating stored creative state.

`LyricSyllable` and `SyllableId` now provide an ordered, provenance-aware representation. Each syllable stores its stable ID, text, zero-based position within the word, and source: `Manual`, `Analyzer`, or `Imported`. Matching syllable IDs survive nearby boundary insertions, and an artist correction replaces provenance with `Manual` without allowing a future analyzer to silently become authoritative. The editor accepts boundaries separated by `|` and clearly identifies manual data.

Empty syllable collections mean “not analyzed,” not “zero syllables.” The current slice does not guess pronunciation: analyzer and imported provenance are domain capabilities for later typed integrations, not claims that an analyzer or importer already exists. Automatic syllable extraction, automatic stress detection, rhyme, breath analysis, suggested structure, generated rhythm candidates, and automatic musical placement remain planned rather than implemented.

`StressMark` is an optional annotation on an existing syllable identity. Its level is `None`, `Secondary`, `Primary`, or `Emphasized`, and its provenance is `Manual`, `Analyzer`, or `Imported`. No mark means the artist has not made a decision; `None` means the artist explicitly intends no stress. The editor writes `Manual` marks only, preserves them on surviving syllables, and includes the change in session undo/redo. Analyzer and imported provenance are representation capabilities, not implemented analysis features.

`ProsodicPattern` represents the relative shape an artist assigns within one phrase. Its ordered `ProsodicUnit` objects reference existing syllable IDs instead of copying text, carry stable pattern and unit identities, and store `Weak`, `Neutral`, or `Strong` weight with provenance. Stress and prosodic weight are intentionally separate: stress records intended emphasis on a syllable, while prosodic weight describes that syllable relative to a particular phrase. The engine does not derive one from the other.

A pattern may describe only some syllables. An unmapped syllable means “undecided,” not `Neutral`. Manual edits, compatible lyric edits, phrase split/join, save/load, and session undo/redo preserve surviving unit identities and provenance. Splitting partitions existing units; joining recombines them in syllable order. Neither operation invents weights. Analyzer and imported provenance remain representation capabilities only—no prosody analyzer is implemented.

`SyllablePlacement` is the first explicit bridge from language to musical time. It has a stable `SyllablePlacementId`, references one existing syllable ID, and stores a section-relative `BeatPosition` as bar, beat, and tick plus provenance. The placement itself is the prosodic anchor; a second parallel anchor entity would duplicate identity without adding meaning. Section-relative coordinates allow a Verse or Chorus to move in the Song Graph while its internal lyric timing moves with it. The existing section timeline resolves the relative coordinate to an absolute song position.

Placement is artist-authored and partial. Unplaced means undecided. Existing placements must remain within the section and current meter, advance through musical time in lyric order, and survive compatible lyric, boundary, section-reorder, save/load, and undo/redo operations with the same identity. Removing a syllable removes its now-invalid placement. The engine does not infer placements from stress or prosodic weight, and it does not generate rhythm.

`RhythmCandidate` represents a named possibility without replacing the active beat map. An artist saves one by snapshotting the current placements for a phrase; its stable `RhythmCandidateEvent` values reference the same syllable IDs and store their alternative section-relative positions. Multiple options can coexist. Applying one is an explicit artist command that replaces only that phrase's active placements and preserves compatible placement IDs. Candidate capture, rename, removal, and application are reversible, and save/load preserves exact candidate and event identities.

Compatible lyric and syllable edits filter candidates to surviving identities. Phrase splits partition candidate events without duplicating lyric data, while joins re-associate the options with the surviving phrase. The current editor does not generate, score, audition, rank, or automatically accept candidates. Candidates contain point onsets only; duration, rests, melisma, locks, and performance data remain future work.

`BreathPoint` records an artist-authored inhale after an existing syllable. It has a stable `BreathPointId`, references that syllable through `AfterSyllableId`, and stores `Manual`, `Analyzer`, or `Imported` provenance. Absence means undecided. Punctuation and phrase breaks do not invent breaths. Compatible lyric and syllable edits retain surviving breath identities; removing a syllable removes its now-invalid breath mark. The editor writes manual decisions only. Timed breath placement, automatic breath analysis, locks, and generation remain later slices.

Prosody scoring is derived review, not stored creative state. `ProsodyScorer` evaluates a phrase's active syllable placements or one saved rhythm candidate and returns category scores for stress, breath, and crowding plus inspectable findings. Primary or emphasized stress and strong phrase weight on weak or offbeat positions reduce the stress score. Breath marks with less than a beat before the next onset, and long timed phrases with no interior breath, reduce the breath score. Sub-half-beat gaps and three-or-more syllables on one beat reduce the crowding score. The engine never invents placements, breaths, or locks from a score.

`CreativeLock` protects accepted decisions. A lyric-line lock references one `LyricLineId` and blocks word, syllable, stress, prosody, breath, and phrase-boundary edits. A phrase-rhythm lock references one line and `LyricPhraseId`, blocks placement edits and applying rhythm options, and still allows capturing or reviewing options. Locks carry stable identities and provenance, survive save/load, and participate in session undo/redo. Migration to schema v11 never invents locks.

The lyric timeline is a derived view, not stored state. `LyricTimelineProjector` maps section spans and syllable placements onto absolute song ticks, can overlay one rhythm candidate for comparison, and marks breath-after opportunities near their host syllables. The editor uses that projection to show how placed lyrics sit in musical time and to jump from a timeline mark back to the matching syllable controls.

`LyricPunctuation` preserves punctuation groups as identified tokens with exact source offsets while apostrophes and internal hyphens remain part of their words. `LyricPhrase` stores a stable ID, zero-based position, provenance, and an ordered list of existing word IDs. Every word belongs to exactly one contiguous phrase. A new or migrated line begins as one `Default` phrase; split and join operations are explicit artist actions and produce `Manual` provenance. Nearby word edits retain surviving phrase and punctuation IDs without copying or rewriting lyric text.

## Processing order

1. Parse raw lyrics into sections, lines, phrases, words, and syllables. Word tokenization, punctuation identity, artist-controlled syllable boundaries, manual phrase structure, artist-authored syllable stress, phrase-relative prosodic weight, manual beat anchors, and manually marked breath points are now foundational; automatic analysis remains planned.
2. Analyze rhyme, repeated language, important words, and breath opportunities without replacing artist-authored stress or breath decisions.
3. Describe narrative roles and emotional transitions.
4. Preserve multiple artist-authored rhythm possibilities; later analyzers may propose additional candidates through the same typed model.
5. Score candidates with inspectable stress, breath, and crowding findings; lock accepted lyric wording or phrase rhythm before later audition and generation.
6. Build harmony and energy plans around the approved meaning and phrasing.

## Future timed prosody model

The current foundation records phrase-relative weight, optional point anchors, multiple named onset alternatives, optional after-syllable breath marks, and derived prosody review findings. Later timed-prosody slices will extend those candidates with:

- Onset, duration, stress, and melisma
- Timed breath points and phrase boundaries
- Sustained-vowel opportunities
- Crowding, syncopation, and vocal difficulty refinements
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
