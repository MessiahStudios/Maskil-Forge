# Maskil Forge

**Understand the words. Forge the music.**

A local-first songwriting workspace for singer-songwriters who know what they want to say and need help shaping it into music. It sits closer to a **DAW workflow + composition assistant + music theory engine** than to “type a prompt → get an MP3.”

> A song exists as structured, editable data before it exists as finished audio.

The artist supplies the words, intent, revisions, and the recorded human lead vocal. Maskil Forge may analyze, guide, and propose; it must not generate or replace the singer. Product identity, boundaries, and the Engine/Forge naming split live in the [product vision](docs/00-product-vision/README.md).

## Now

The prototype and editable-demo MVP are complete through slice 5.15. Current named work is **Milestone 9.12**, a reviewable cleanup treatment. Schema remains **v37**. The instrument catalog is **version 4**. Desktop Music can switch between instrument-aware built-in previews and a user-supplied SF2, SF3, or DLS General MIDI bank, export stored song facts as a format-1 Standard MIDI File, and store vocal direction and an ordered plan of production jobs. Corrective Tone now offers an original/processed comparison with an 80 Hz starting point and optional advanced frequency/Q controls. Each revision requires a fresh comparison and explicit acceptance. Explicit acceptance saves the recipe with its source take; the original recording stays unchanged. Vocal direction can now produce an explained, reviewable job proposal with explicit acceptance and an optional low-cut audition; other suggested jobs remain plans. Reviewed loudness frames can support a cited, undoable suggestion to add Transparent Level Control to the job plan. That job can be compared on a saved take: peaks above −18 dBFS are eased at 2:1 with a 20 ms attack and 120 ms release. Quiet phrases are not boosted, and no makeup gain or limiter is applied. Acceptance stores that fixed recipe beside any accepted low-cut; the original recording stays unchanged. A saved take can also play those accepted treatments together in the current job order, without storing another recording. Saturation / Color can be compared on a saved take: each sample becomes itself minus 0.15 times its cube. Louder moments pick up a little harmonic color and lose a little peak. Quiet phrases are not boosted, and no makeup gain, gate, equalizer, or limiter is applied. Acceptance stores that fixed recipe beside any accepted low-cut and level control; the original recording stays unchanged. The combined listening pass includes it in the current job order. Character Compression can be compared on a saved take: levels above −24 dBFS are shaped at 4:1, a louder moment is followed immediately and released over 250 ms, and the loudest moment is restored to its original peak so quieter phrases can come forward. Nothing is raised above that original peak. Transparent Level Control remains the separate treatment that leaves quiet phrases unboosted. Acceptance stores that fixed recipe beside any accepted low-cut, level control, and saturation; the original recording stays unchanged. The combined listening pass includes it in the current job order. Cleanup can be compared on a saved take: gaps below −40 dBFS ease toward one quarter of their level over 200 ms, and a sung level returns over 10 ms. The gaps are lowered, not silenced, and sung levels are not boosted. Acceptance stores that fixed recipe beside the other accepted treatments; the original recording stays unchanged. The combined listening pass includes it in the current job order. Desktop Music can explicitly discover VST3 files and bundles on the project-service host. Discovered bundles can expose optional reported names, vendors, versions, and classes with manifest attribution. A bounded binary-header check can also compare file format and CPU with the running host. macOS bundles can also resolve executable names from a bounded XML Info.plist, with declaration attribution kept separate from actual header evidence. The temporary inventory can be searched and filtered by header evidence, with repeated reported class IDs reviewed across all candidates. Local macOS users can explicitly check module loading and runtime factory classes in a separate supervised worker. They can select an audio-module class, create and initialize it with a minimal host context, inspect its bounded audio-bus declarations, and terminate it without activating buses or processing audio. Audio processing setup and hosting are still needed for Milestone 9.6. Phone Music stays hidden. Bundled sound banks, offline song WAV rendering, plugin hosting, and the remaining vocal processors are not started.

The app can capture ideas and lyrics, shape sections, time syllables, plan harmony and arrangement, approve playable notes, name catalog instruments on musical parts, hear those parts through distinct synthesized guide voices or a device-local General MIDI sound bank, record and review rough vocal takes, name the desired result for the human lead vocal, plan its production jobs, and export MIDI without inventing unstored material. Undo is session-only. Songs persist in a local library with recovery, Trash, and portable `.maskil` / `.maskil.json` interchange.

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

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Node.js 22 or later with npm. macOS builds also require Apple Command Line Tools (`xcode-select --install`) for the [native VST3 check worker](native/README.md).

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
