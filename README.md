# Maskil Forge

**Understand the words. Forge the music.**

Maskil Forge is a human-driven procedural songwriting and arrangement engine by Messiah Studios. It is designed for singer-songwriters who know what they want to communicate but may not yet know how the lyrics should fit a genre, how the arrangement should develop, or which instruments can best express the idea.

## Why Maskil Forge exists

Many aspiring and independent singer-songwriters begin with lyrics, a vocal idea, or an emotional direction but do not yet know how prosody, harmony, arrangement, orchestration, MIDI, or instrument technique can express it. Maskil Forge is intended to bridge that gap without taking authorship away from the artist. The artist supplies the words, intent, vocal gestures, taste, revisions, and final lead-vocal performance; the system helps turn those decisions into inspectable musical structure.

## How Maskil Forge differs from AI song generators

Prompt-to-song systems primarily generate a finished audio result. Maskil Forge is designed to construct an editable song project.

The artist supplies the lyrical meaning, creative intent, vocal gestures, choices, revisions, and final lead vocal. The Maskil Engine is designed to analyze and connect those contributions through explicit song structure, prosody, theory, arrangement, MIDI, instrument behavior, and rendering instructions.

AI may interpret requests and propose alternatives, but the artist remains the author and performer.

The central rule is:

> A song exists as structured, editable data before it exists as finished audio.

## Core workflow

```text
Artist input
    -> meaning and performance analysis
    -> structured Song Graph
    -> procedural music logic
    -> MIDI and automation
    -> replaceable audio rendering
    -> human vocal recording
    -> mix, export, and revision
```

The repeating creative loop is:

```text
Understand -> Structure -> Generate -> Render -> Listen -> Revise
```

Every stage is intended to remain editable.

## Core architecture

The user-facing application and creative workspace is **Maskil Forge**. Its underlying procedural songwriting framework is **Maskil Engine**. These are two naming layers within one product and repository, not separate products.

```text
Maskil Forge workspace
    -> application commands and history
    -> Maskil Engine
       -> Song Graph and timeline
       -> lyrics, prosody, theory, and narrative
       -> composition, arrangement, and instruments
       -> performance capture and retargeting
    -> MIDI and automation
    -> replaceable renderer
```

Future C# code will use `MaskilForge` as its namespace root. The repository is named `Maskil-Forge`.

## Current project status

This repository contains the product definition, architectural principles, delivery roadmap, and an initial executable Song Graph foundation. The current vertical slice supports structured projects, ordered sections, lyric lines, JSON persistence, and reversible section operations. Maskil Forge remains early-stage: it is not a functional DAW or complete audio generator, and AI, MIDI, VST hosting, vocal analysis, procedural music generation, recording, and mixing have not been implemented.

## Run the foundation locally

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js 22 or later with npm

From the repository root, restore and run the API:

```powershell
dotnet restore MaskilForge.sln
dotnet run --project src/MaskilForge.Api --urls http://localhost:5072
```

In a second terminal, install the web dependencies and run the Vue development server:

```powershell
cd src/MaskilForge.Web
npm install
npm run dev
```

Open `http://localhost:5173`. Project JSON files are written to the API's ignored `App_Data/projects` directory. To verify production builds and tests:

```powershell
dotnet build MaskilForge.sln
dotnet test MaskilForge.sln
cd src/MaskilForge.Web
npm run build
```

## Documentation

See the [documentation index](docs/README.md) for the complete progression, or read these in order:

1. [Product vision](docs/00-product-vision/README.md) - identity, audience, responsibilities, and boundaries.
2. [System foundations](docs/01-system-foundations/README.md) - Song Graph, timeline, commands, events, constraints, and scoring.
3. [Lyrics and musical meaning](docs/02-lyrics-and-meaning/README.md) - lyrics, prosody, narrative, harmony, and energy.
4. [Composition and arrangement](docs/03-composition-and-arrangement/README.md) - genre data, instrument roles, generators, and MIDI.
5. [Performance and sound](docs/04-performance-and-sound/README.md) - voice capture, retargeting, rendering, recording, and mixing.
6. [AI director and product workflow](docs/05-ai-director/README.md) - natural-language direction over deterministic tools.
7. [Delivery roadmap](docs/06-delivery-roadmap/README.md) - build order, milestones, dependencies, and completion gates.

## Publisher

Maskil Forge is a project by **Messiah Studios**.
