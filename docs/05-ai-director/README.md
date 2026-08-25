# 05 — AI Director and Product Workflow

The planned AI director is an interpreter and planner above Maskil Engine. It should arrive only after deterministic tools work through the normal interface. It is not the sole composer, must not replace the artist, and must not generate a replacement lead vocal.

## Responsibilities

- Convert user language into typed, validated commands
- Interpret lyrics and emotional direction
- Reason over structured musical and performance observations produced by analyzers
- Propose several candidates and compare their tradeoffs
- Explain decisions using command inputs and scores
- Ask only questions that materially change generation

## Observation boundary

The AI Director consumes structured Song Graph state and analyzer-produced observations; it does not treat raw audio interpretation as the canonical understanding of a song. Performance observations may include pitch contour, onset, duration, loudness, timing, prosody, expression curves, and later timbre or mix descriptors. Each observation retains confidence, analyzer identity and version, source-asset identity, and provenance so the Director can expose uncertainty instead of presenting a measurement as an artist decision.

When vocal production exists, the Director should reason in those same observations and in processing roles: for example, that wide level variation may call for gentle transparent control. It must not treat a commercial plugin name, preset, or hidden parameter dump as the plan. A production proposal still compiles into inspectable, previewable, reversible commands.

An audio-capable local or cloud model may provide supplemental observations when useful. Those results follow the same confidence, provenance, validation, and review boundaries as deterministic analysis and must not become the sole authoritative representation of a performance. Core workflows must remain possible with local deterministic analyzers and without an audio-capable model.

## Required interaction contract

The AI creates a proposed change set. The application validates it against the current project, locks, and constraints. The user previews and accepts, modifies, or rejects it. Accepted commands enter normal history.

The model must not directly mutate project JSON, invent entity IDs, bypass validation, hide a chain of edits behind an irreversible action, or generate a replacement for the artist's lead vocal.

Analyzer observations may inform a proposed change set, but only explicit, validated, reversible commands can promote corrected or approved material into authoritative Song Graph state. The proposal and explanation must preserve the distinction between source audio, observed measurements, artist corrections, and accepted creative decisions.

## Tool progression

Start with narrow commands such as `AnalyzeLyrics`, `CreateProsodyCandidates`, `GenerateHarmonyCandidates`, and `ReshapeEnergyCurve`. Add broad requests such as “make the chorus bigger” only when they can compile into inspectable narrow commands.

## Explanations

An explanation should state:

- What the user asked for
- Which musical parameters changed
- Which content stayed locked
- Why the selected candidate scored well
- What tradeoff or uncertainty remains, including material observation confidence

## Planned completion gate

This future gate will be met when:

- Natural-language requests compile into schema-valid commands.
- Invalid IDs and conflicting locks cannot mutate a project.
- The user can preview the proposed diff before acceptance.
- Every accepted AI change is undoable and attributable.
- Analyzer-derived proposals identify the observations, confidence, provenance, and source assets that informed them.
- Direct audio-model interpretation is optional and cannot bypass structured observations or artist review.
- Core editing and generation remain usable without AI.
