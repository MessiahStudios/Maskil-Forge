# Maskil Forge Documentation

This directory contains the product and architecture foundation for Maskil Forge. The documents are numbered to show their intended reading and dependency order: begin with the product vision, proceed through the planned engine layers, and finish with the delivery roadmap.

Maskil Forge is early-stage. These documents define intended behavior and future completion gates; they do not claim that the described application features are already functional.

## Reading order

1. [Product vision](00-product-vision/README.md) - Defines the product identity, primary user, naming layers, responsibilities, boundaries, and guiding principles.
2. [System foundations](01-system-foundations/README.md) - Specifies the canonical Song Graph, musical timeline, commands, events, constraints, locks, and shared scoring.
3. [Lyrics and musical meaning](02-lyrics-and-meaning/README.md) - Describes the planned connection between lyrics, speech and vocal prosody, narrative movement, energy, rhythm, and harmony.
4. [Composition and arrangement](03-composition-and-arrangement/README.md) - Describes role-based arrangement, genre and instrument knowledge, procedural candidates, MIDI, and preview rendering.
5. [Performance and sound](04-performance-and-sound/README.md) - Defines the human lead-vocal workflow, voice capture and retargeting, replaceable renderers, reviewable vocal production, and mixing goals. The artist's recorded vocal remains authoritative.
6. [AI director and product workflow](05-ai-director/README.md) - Defines AI as an interpreter and director over typed, deterministic Maskil Engine operations.
7. [Delivery roadmap](06-delivery-roadmap/README.md) - Orders implementation milestones from the project skeleton through composition, performance, rendering, AI direction, and export.

## Progression

```text
Product purpose and audience
    -> canonical song data and editing rules
    -> lyrical meaning and musical time
    -> composition and arrangement
    -> performance and replaceable sound rendering
    -> AI-directed workflow
    -> implementation and release sequence
```

The numbered folders remain directly under `docs/`. The Git branch name `docs/maskil-forge-foundation` is not a documentation directory.

