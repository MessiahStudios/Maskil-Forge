# 03 — Composition and Arrangement

This Maskil Engine layer is now partially implemented. Section energy and density, arrangement-role assignments, registered harmony voicings, playable note events, MIDI export, role-aware musical parts, and the first deterministic low-end support realization all preserve the artist's locks and choices. Additional role realization and complete editable-demo playback remain planned.

## Build roles before instruments

The arranger reasons first about roles:

```text
Foundation  Pulse  Harmony  Low-end support
Texture     Accent Transition Countermelody Hook reinforcement
```

It should then recommend instruments capable of fulfilling those roles within genre, register, energy, and playability constraints. Recommendations should consider expressive behavior, range, articulation, and timbre rather than instrument name alone.

## Data-driven knowledge

Genre profiles describe probabilities and tendencies: tempo, meter, density, dynamics, form, harmony, drums, and vocal phrasing. Instrument profiles describe range, timbre, attack, sustain, articulations, roles, limitations, and renderer mappings.

Profiles belong in versioned data files, not hardcoded conditionals.

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

Use a simple preview renderer before VST hosting. MIDI is the initial interchange and performance-control layer, not the sound itself. The important proof is that the same structured project can drive audible playback through replaceable renderers, not that the first sounds are release quality.

## Current boundary and completion gate

The current implementation can derive an inspectable low-end support idea from approved notes, accept it as one reversible musical part, audition existing harmony, and export approved notes as MIDI. It does not yet generate a complete arrangement.

The broader completion gate will be met when:

- The engine generates a complete arrangement for a small supported genre set.
- All generated notes are editable and traceable to a command and seed.
- Regenerating drums does not change locked harmony or melody.
- Range and register collisions are reported.
- MIDI export matches preview timing.
