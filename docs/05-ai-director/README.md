# 05 — AI Director and Product Workflow

The planned AI director is an interpreter and planner above Maskil Engine. It should arrive only after deterministic tools work through the normal interface. It is not the sole composer and must not replace the artist.

## Responsibilities

- Convert user language into typed, validated commands
- Interpret lyrics and emotional direction
- Propose several candidates and compare their tradeoffs
- Explain decisions using command inputs and scores
- Ask only questions that materially change generation

## Required interaction contract

The AI creates a proposed change set. The application validates it against the current project, locks, and constraints. The user previews and accepts, modifies, or rejects it. Accepted commands enter normal history.

The model must not directly mutate project JSON, invent entity IDs, bypass validation, or hide a chain of edits behind an irreversible action.

## Tool progression

Start with narrow commands such as `AnalyzeLyrics`, `CreateProsodyCandidates`, `GenerateHarmonyCandidates`, and `ReshapeEnergyCurve`. Add broad requests such as “make the chorus bigger” only when they can compile into inspectable narrow commands.

## Explanations

An explanation should state:

- What the user asked for
- Which musical parameters changed
- Which content stayed locked
- Why the selected candidate scored well
- What tradeoff or uncertainty remains

## Planned completion gate

This future gate will be met when:

- Natural-language requests compile into schema-valid commands.
- Invalid IDs and conflicting locks cannot mutate a project.
- The user can preview the proposed diff before acceptance.
- Every accepted AI change is undoable and attributable.
- Core editing and generation remain usable without AI.
