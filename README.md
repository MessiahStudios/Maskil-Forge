# Maskil Forge

**Understand the words. Forge the music.**

A local-first songwriting workspace for singer-songwriters who know what they want to say and need help shaping it into music. It sits closer to a **DAW workflow + composition assistant + music theory engine** than to “type a prompt → get an MP3.”

> A song exists as structured, editable data before it exists as finished audio.

The artist supplies the words, intent, revisions, and the recorded human lead vocal. Maskil Forge may analyze, guide, and propose; it must not generate or replace the singer. Product identity, boundaries, and the Engine/Forge naming split live in the [product vision](docs/00-product-vision/README.md).

## Now

The prototype and editable-demo MVP are complete through slice 5.15. Current named work is **Milestone 8.1**. Schema is **v31**. The instrument catalog is **version 4**. Desktop Music can hear instrument-aware built-in previews and export stored song facts as a format-1 Standard MIDI File. Phone Music stays hidden. General MIDI / SoundFont rendering and Milestone 9.1 vocal-production intent are not started.

The app can capture ideas and lyrics, shape sections, time syllables, plan harmony and arrangement, approve playable notes, name catalog instruments on musical parts, hear those parts through distinct synthesized guide voices, record and review rough vocal takes, and export MIDI without inventing unstored material. Undo is session-only. Songs persist in a local library with recovery, Trash, and portable `.maskil` / `.maskil.json` interchange.

It is not a DAW, a prompt-to-song generator, or a complete audio renderer.

Named slices, deliverables, and “not this slice” boundaries live in the [delivery roadmap](docs/06-delivery-roadmap/README.md). Do not append slice diaries here.

```text
Phase 1  ✅  Song foundations
Phase 2  ✅  Harmony → editable audible demo
Phase 3  ○  Performance and sound
```

Delivery is web-first and local-first. One Song Graph serves the browser, a future installable PWA, and any later native shell. Portable interchange comes before accounts or cloud sync.

## Architecture

**Maskil Forge** is the workspace. **Maskil Engine** is the procedural layer beneath it. One product, one repository, C# namespace `MaskilForge`.

```text
Artist input
    -> Song Graph
    -> MIDI and automation
    -> replaceable rendering
    -> human lead-vocal capture, guidance, and reviewable production
    -> mix, export, and revision
```

The creative loop is `Understand -> Structure -> Generate -> Render -> Listen -> Revise`. Every stage stays editable.

## Run locally

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Node.js 22 or later with npm.

```powershell
dotnet restore MaskilForge.sln
dotnet run --project src/MaskilForge.Api --urls http://localhost:5072
```

In a second terminal:

```powershell
cd src/MaskilForge.Web
npm install
npm run dev
```

Open `http://localhost:5173`. Development project files go to the API's ignored `App_Data/projects` directory.

```powershell
dotnet build MaskilForge.sln
dotnet test MaskilForge.sln
cd src/MaskilForge.Web
npm run build
```

Production-style single-origin host (build the web client first):

```powershell
cd src/MaskilForge.Web
npm run build
cd ../..
dotnet run --project src/MaskilForge.Api --urls http://localhost:5072
```

Open `http://localhost:5072`. `dotnet publish` includes an already-built web distribution in `wwwroot`; it does not run npm. GitHub Actions runs the .NET build, tests, and Vue production build on pull requests and `main`.

## Documentation

See the [documentation index](docs/README.md):

1. [Product vision](docs/00-product-vision/README.md)
2. [System foundations](docs/01-system-foundations/README.md)
3. [Lyrics and musical meaning](docs/02-lyrics-and-meaning/README.md)
4. [Composition and arrangement](docs/03-composition-and-arrangement/README.md)
5. [Performance and sound](docs/04-performance-and-sound/README.md)
6. [AI director and product workflow](docs/05-ai-director/README.md)
7. [Delivery roadmap](docs/06-delivery-roadmap/README.md)

## Publisher

Maskil Forge is a project by **Messiah Studios**.
