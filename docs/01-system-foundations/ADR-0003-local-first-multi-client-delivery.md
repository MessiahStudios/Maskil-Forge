# ADR-0003 — Local-First Multi-Client Delivery

- **Status:** Accepted
- **Date:** 2026-08-13

## Context

Maskil Forge should be useful on a phone and a computer without splitting into incompatible web and desktop editions. The current Vue client, .NET application boundary, durable JSON Song Graph, and renderer-independent engine already support reuse, but delivery and storage choices could still couple the product to a browser, desktop shell, cloud account, VST host, or one persistence provider.

Accounts and synchronization would add authentication, authorization, hosted storage, privacy, conflict resolution, quotas, and operating cost before they improve the core songwriting workflow. Conversely, making every future production feature fit browser APIs would weaken requirements such as low-latency recording, MIDI hardware, native files, plugin hosting, and offline rendering.

## Decision

Maskil Forge is web-first and local-first. One canonical Song Graph, command model, migration path, and creative-authority policy serve every client.

- The web application remains the primary creator workflow.
- An installable PWA will provide the broad composition, capture, review, and approval surface across phones and computers.
- A portable, versioned Maskil project package with explicit import and export precedes accounts or cloud synchronization.
- Local/offline storage is a supported product behavior, not merely a temporary development implementation.
- The local host owns the authoritative project library on its filesystem. Development keeps that library in the API's ignored `App_Data/projects` directory; a packaged host defaults to the operating system's per-user application-data directory and may be given an explicit absolute library path.
- A phone or other browser connected to that host reads and writes the same host-owned library through the API. Installing the PWA does not create a second authoritative project library or enlarge browser storage into a user-visible filesystem.
- Browser IndexedDB has three deliberately subordinate roles: dirty-session recovery, view-only saved snapshots, and browser-owned lyric captures. A disconnected lyric capture becomes host-owned only through an explicit durable handoff.
- A future desktop shell may extend the same web client where proven native requirements demand it, including audio interfaces, low-latency monitoring, MIDI hardware, native files, VST3 hosting, and offline rendering.
- The phone experience is deliberately scoped to idea capture, words, structure, rough human-vocal capture, review, and approval. It is not a miniature DAW.
- Cloud backup and device synchronization remain optional future services. Neither is required to open, edit, move, or recover a project.
- Tauri, Electron, and other packaging technologies remain implementation choices until native capability experiments establish concrete requirements.

The Song Graph and Maskil Engine must not assume a UI toolkit, browser, desktop shell, renderer, plugin host, account system, synchronization service, or storage provider.

## Consequences

- Web, PWA, and desktop clients must exchange the same versioned project representation and preserve stable identities, provenance, locks, and migrations.
- Portable-project lifecycle rules must eventually include external assets such as vocal recordings, not only the current JSON tree.
- PWA work must include an explicit offline and recovery model; an application manifest alone is not completion.
- Native bridges expose capability adapters around the shared application rather than forking composition logic.
- Features declare capability requirements so unavailable native functions can be explained or deferred without hiding the rest of the project.
- Optional synchronization must build on portable project semantics and define conflict handling; it must not become the only authoritative copy.
- A connected phone save depends on the selected local host being reachable. Offline browser captures remain vulnerable to that browser profile being cleared until they are handed off or exported; the interface must continue to state that boundary.
- Attaching a browser capture to an existing song requires an explicit identity, revision, and conflict policy. The current handoff creates a new song and must not silently merge lyrics into an existing project.
- Desktop packaging should begin only after a required production workflow has been tested and shown to exceed dependable browser capability.

## Delivery order

1. Finish and validate the shared web creator workflow.
2. Define and test portable project-package import, export, validation, migration, and recovery.
3. Add installable PWA and offline application behavior around the same client.
4. Validate the narrowed phone capture and review journey.
5. Begin voice/performance work and document actual browser capability gaps.
6. Prototype a native shell and bridge only for gaps that materially block production.
7. Consider optional cloud backup and synchronization only after portable local projects are dependable.
