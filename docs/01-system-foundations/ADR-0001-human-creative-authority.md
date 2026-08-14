# ADR-0001 — Human Creative Authority

- **Status:** Accepted
- **Date:** 2026-08-06
- **Scope:** Maskil Forge and Maskil Engine

## Context

Maskil Forge is intended to help an artist understand lyrical material and deliberately shape it into editable music. Future analysis, procedural generation, vocal processing, and AI direction could otherwise blur the boundary between assistance and authorship, overwrite decisions the artist has already accepted, or present a generated voice as the lead singer.

## Decision

The artist's accepted creative decisions are authoritative.

Maskil Forge may analyze source material, suggest alternatives, and generate editable musical structures. Those operations must produce inspectable project data and must not silently replace accepted or locked work. AI systems operate through typed, validated Maskil Engine commands rather than owning or directly mutating the canonical creative state.

The artist retains control over:

- original and revised lyrics;
- accepted structure, harmony, arrangement, performance, and sound choices;
- which material is locked, regenerated, restored, or discarded;
- the recorded human lead-vocal performance, which remains the final authoritative vocal;
- final approval and export decisions.

Vocal capture, analysis, guide melodies, take management, and reviewable production processing—including VST or other audio assistance—are allowed. They must produce inspectable project data and must not generate or silently replace the artist's lead vocal. Voice analysis may drive editable musical or instrument-performance data through the same command, lock, and undo rules as every other layer.

## Consequences

- Raw creative input remains preserved separately from derived structure where needed.
- Generated candidates remain proposals until the artist accepts them.
- Locks and accepted decisions must survive unrelated regeneration.
- Commands require explicit scope, validation, and undo behavior where appropriate.
- AI explanations and provenance can describe changes, but AI does not become the project authority.
- Renderers remain replaceable because finished audio is not the canonical song state.
- A generated, processed, or previewed vocal sound may assist the singer; it does not become the authoritative lead performance.

This decision applies to future AI, prosody, composition, MIDI, performance, vocal production, and rendering work.
