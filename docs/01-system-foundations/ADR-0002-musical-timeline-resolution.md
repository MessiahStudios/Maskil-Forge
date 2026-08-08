# ADR-0002 — Musical Timeline Resolution

## Status

Accepted for the timeline foundation.

## Decision

Maskil Forge represents musical time at 480 pulses per quarter note (PPQ). User-facing musical positions use one-based bars and beats with a zero-based tick inside the beat. Absolute ticks are the stable arithmetic coordinate used for conversion and future MIDI-oriented work.

The first timeline slice supports one tempo and one time signature beginning at beat zero. Song sections start on bar boundaries and carry an explicit duration in bars. Their placements reference stable `SectionId` values and reflow in Song Graph order.

## Why 480 PPQ

480 divides common quarter-note, eighth-note, sixteenth-note, triplet, and finer practical subdivisions without requiring floating-point coordinates. It is familiar in MIDI-oriented systems and gives future MIDI work an appropriate foundation without implementing MIDI now.

## Consequences

- Timeline calculations use integers and validate positions at domain boundaries.
- Changing the stored PPQ would require an explicit schema migration.
- Multiple tempo or meter regions, seconds conversion, transport, playback, MIDI files, and audio synchronization remain separate future decisions.
