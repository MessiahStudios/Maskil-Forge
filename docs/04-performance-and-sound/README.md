# 04 — Performance and Sound

This planned layer will let the artist control musical expression with their voice, complete an audible production, and finish a human lead-vocal performance inside the product. The artist's recorded lead vocal remains the final authoritative vocal. Maskil Forge may capture, analyze, guide, preserve, and process that performance; it must not generate or replace the singer.

The Song Graph stays canonical. Renderer choice, VST processing, and preview tones are replaceable sound paths over that data. They do not own the composition or the lead vocal.

## Vocal authority

The intended vocal workflow is assistance around a human singer:

```text
Artist sings or hums
    -> capture and analysis
    -> guidance, guide melody, and reviewable production
    -> preserved takes and comps
    -> optional editable musical or instrument data
    -> the same human lead vocal remains the released performance
```

Maskil Forge may:

- Capture and analyze vocals for pitch, onset, loudness, timing, and prosody.
- Show pitch, timing, and prosody guidance that informs the next take without silently rewriting the recording.
- Create guide melodies and related rehearsal aids so the artist can hear and follow a part.
- Preserve takes, punch-ins, and comps as inspectable performance history.
- Suggest or apply reviewable vocal production settings, including VST or other audio processing that assists the recorded vocal.
- Let the artist choose a desired vocal result, then propose processing roles rather than a plugin rack.
- Use voice analysis to drive editable musical or instrument-performance data, such as melody, rhythm, expression curves, or retargeted instrumental parts.

Maskil Forge must not:

- Present a generated or synthesized voice as the final lead singer.
- Silently replace an accepted take with processed or generated audio.
- Treat preview tones, guide tracks, or instrumental realizations as the lead vocal.
- Let a renderer, VST, or neural audio model own the Song Graph or the artist's vocal authorship.

Keep the original recording, extracted observations, edited gesture data, retargeted instrumental performance, and production settings separate. Correction, re-targeting, and production changes should be possible without discarding the source take.

The first implemented boundary is a microphone preflight rather than a recording shortcut. It checks secure-browser support only after an explicit artist action, confirms a live input, and immediately closes the test stream without recording, uploading, or saving sound. Recording waits for an external-asset lifecycle that protects original takes through backup, recovery, portable transfer, Trash, and permanent deletion.

Schema v22 adds the first durable half of that lifecycle: a path-free manifest can identify an original vocal asset by stable ID, media type, byte length, SHA-256 digest, and creation time. The manifest intentionally contains neither the audio bytes nor analysis or processing data. Legacy JSON portability refuses non-empty manifests rather than exporting broken media references.

The repository now supplies the local half of the media lifecycle. It accepts a new immutable asset only after staging and verifying its exact length and SHA-256 digest, validates those bytes with every project load, and moves paired media through backup, session recovery, corrupt-data preservation, Trash, restore, and permanent deletion. Cross-device transfer uses a versioned `.maskil` package that carries the Song Graph plus every referenced original-vocal byte and verifies length and SHA-256 on both export and import. JSON-only `.maskil.json` files still refuse referenced media. Recording remains the next capture step now that those bytes can move with the project.

## Performance capture pipeline

```text
Recorded voice -> Deterministic analyzers -> Performance observations
               -> Artist correction/approval -> Gesture data
               -> Editable notes and curves -> Instrument retargeter
               -> MIDI, bends, expression, and articulations
```

Voice analysis may update editable Song Graph material only through explicit, reversible commands. A hummed or sung gesture can control instruments without becoming a substitute lead vocal.

### Performance observations

`PerformanceObservation` is the general boundary between source audio and musical reasoning. Initial observation kinds may describe pitch contour, onset, duration, loudness, timing, and prosody. The model must remain extensible to later timbre, spectral, transient, dynamic, masking, and stereo descriptors without making any one analyzer or audio model canonical.

Each observation should identify:

- The measured value and its time span or source position
- A confidence value or explicit unavailable-confidence state
- The analyzer identity and version that produced it
- The immutable source asset ID from which it was derived
- Analyzer provenance, with any later artist correction or approval recorded separately

Observations are evidence, not artist decisions. They must remain distinguishable from corrected gesture data, accepted notes and curves, and other authoritative Song Graph material. Low-confidence results may be shown, compared, corrected, or rejected; they must not silently become creative truth.

Schema v24 implements the first durable observation boundary as a non-authoritative project collection. Each entry owns a stable observation ID, one immutable source-vocal asset ID, an extensible kind, a millisecond span, named scalar measurements with explicit units, optional confidence, analyzer ID and version, analyzer provenance, and creation time. Existing schema-v23 projects migrate to an empty collection. Observations participate in project durability and asset-owning package transfer, while deleting a source take removes its derived active observations so evidence cannot outlive its source in the current version.

The schema contract by itself does not claim that pitch, onset, loudness, timing, prosody, or model analysis exists. Analyzer execution and artist correction must arrive as separate reviewable slices; this collection cannot directly create or replace approved Song Graph material.

Slice 6.9 introduces the first deliberately narrow execution path: an artist-triggered browser analyzer decodes one already-saved rough take and calculates contiguous 250 ms frames containing RMS and peak amplitude in dBFS. The host accepts only this bounded report shape, requires the current persisted revision and an existing source take, constrains measurements to -120 through 0 dBFS and the recording boundary to one minute, and stamps `maskil.browser.loudness` version `1.0.0` as deterministic analyzer provenance. The browser remains part of the provenance: this is useful single-artist evidence, not a security claim about an untrusted client.

Rerunning the same analyzer replaces only its earlier `loudness.frame` observations for that source. Other analyzers' evidence and the immutable recording bytes remain untouched. The Review summary exposes the frame count, analyzed span, and strongest measured peak without treating it as a mastering target. This slice does not calculate integrated LUFS, pitch, onset, timing intent, prosody, notes, or gestures.

Slice 6.10 adds an independent browser pitch analyzer using normalized autocorrelation over 80 ms windows sampled every 200 ms. Analysis is internally reduced to no more than 8 kHz and limited to 65–1000 Hz. A frame is omitted unless its centered signal clears the analyzer floor and its correlation confidence is at least 0.72. Silence, very quiet input, and uncertain periodicity therefore produce no frequency claim rather than a fabricated pitch.

The host accepts only a dedicated pitch-frame report on that 200 ms grid, with an exact 80 ms duration, bounded frequency and confidence, no more than 300 voiced frames, an existing source take, the current project revision, and the one-minute recording boundary. It stamps `maskil.browser.pitch-acf` version `1.0.0`, deterministic provenance, observation identities, and creation time. A rerun atomically replaces only this analyzer's `pitch.frame` evidence; an empty result deliberately clears its prior frames while preserving loudness evidence and source bytes. Review may summarize the median frequency, but neither that statistic nor any frame is a MIDI note, approved melody, correction target, or permission for automatic promotion.

Slice 6.11 adds independent time-domain onset evidence. The browser downmixes and reduces a saved take to no more than 8 kHz, measures RMS energy in 32 ms windows on a 16 ms grid, and requires minimum signal level, minimum rise, and a minimum previous-frame ratio. Local rise maxima are kept at least 96 ms apart. A quiet source, gradual change, or insufficiently distinct rise produces no candidate rather than fabricated timing.

The host accepts only a dedicated onset-event report with the exact grid, window duration, separation, normalized strength, confidence of at least 0.6, no more than 625 candidates, an existing source take, the current revision, and the one-minute boundary. It stamps `maskil.browser.onset-energy` version `1.0.0`, deterministic provenance, observation identity, and creation time. Reruns replace or clear only this analyzer's `onset.event` evidence. A candidate is not a note onset, beat, tempo, quantization target, timing correction, or artist-approved gesture.

Slice 6.12 makes persisted evidence inspectable before artist correction begins. Each saved take can expand a derived, read-only inspector that groups loudness, pitch, onset, and later extensible kinds by analyzer identity and version. Rows remain in source-time order and expose the stored span, measurements, confidence, provenance, and report time. Large reports reveal twelve rows at a time so phone Review stays bounded. This view creates no second evidence store, schema field, analyzer run, note, beat, correction, or gesture; it only explains claims already present in `performanceObservations`.

Slice 6.13 adds the first artist-authored layer over that evidence without changing the evidence itself. Schema v25 stores at most one `PerformanceObservationReview` per present observation, with a stable review ID, an `Accurate` or `Inaccurate` verdict, and creation and update times. The artist can revise or clear the verdict from the inspector. Analyzer confidence remains the analyzer's claim; the verdict records whether the artist agrees with that claim.

Reviews move with the project and remain separately attributable. Removing a source take removes its observations and their reviews. Rerunning one analyzer replaces its prior claims, so reviews attached to exactly those disappearing claims are invalidated while reviews for other analyzers remain. This avoids orphaned verdicts and prevents a decision about an old claim from silently attaching to a new measurement. A review does not supply a corrected value and cannot create a note, beat, curve, MIDI event, approved gesture, or automatic musical decision.

Slice 6.14 adds that corrected value as a second artist-authored record, not a rewrite of the analyzer claim. Schema v26 stores at most one `PerformanceObservationCorrection` per present observation, and only while that observation currently has an **Inaccurate** review. The correction repeats the original measurement names, units, and count, requires at least one different value, and stays inside the same frequency, loudness, and normalized bounds the analyzers already use. Marking the claim accurate or clearing the verdict drops the correction. Source-take removal and analyzer reruns cascade through corrections the same way they cascade through reviews. The original observation remains the analyzer's evidence; the correction is the artist's alternate measurement and still is not a note, beat, MIDI event, or automatic musical decision.

Slice 6.15 promotes a reviewed claim into an artist-approved gesture snapshot. Schema v27 stores at most one `PerformanceObservationGesture` per present observation. Promotion is allowed only while the claim is **Accurate**, or **Inaccurate** with a stored correction. The host copies those approved measurements; the client does not send measurement values. Clearing the review, removing the correction from an inaccurate claim, removing the source take, or rerunning the owning analyzer drops the gesture. Changing an eligible review or correction refreshes the snapshot in place. The gesture remains distinct from notes, beats, MIDI events, expression curves, and automatic musical decisions.

Slice 6.16 projects those pitch gestures into a transient playable-note sketch. Frequency becomes the nearest MIDI note, millisecond spans become ticks from the first tempo, and the take is placed at song tick 0 until a later placement slice exists. Desktop Music previews the sketch; notes enter the Song Graph only after an explicit accept. Loudness and onset gestures, musical parts, expression curves, and automatic retargeting remain later work. Phone Review does not create notes.

Slice 6.17 lets the desktop Music workspace inspect those same saved takes: playback, analyzer runs, artist verdicts, corrections, and gesture promotion. Recording on the studio screen still requires a saved revision and never uploads until the artist reviews a temporary take. The slice adds no schema fields and does not place a take on the timeline.

Slice 6.18 stores an artist-authored song start for one original-vocal take. Schema v28 keeps placements in a separate collection so asset bytes stay immutable. Desktop Music sets or clears bar, beat, and tick against the current meter; absence still means song tick 0. The pitch-gesture sketch adds that start to take-relative ticks. Placement does not move already-accepted notes, sync playback to the timeline, or follow section reflow.

Slice 6.19 projects approved onset gestures into a transient playable-note sketch of short C4 hits. Millisecond spans become ticks from the first tempo plus the take's song start, and strength becomes velocity. Desktop Music previews the sketch; notes enter the Song Graph only after an explicit accept. Pitch gestures keep their own sketch. Loudness gestures, musical parts, expression curves, and automatic retargeting remain later work. Phone Review does not create notes.

Slice 6.20 projects approved loudness gestures into a transient playable-note sketch of short C4 hits. Millisecond spans become ticks from the first tempo plus the take's song start, and RMS between −60 and 0 dBFS becomes velocity. Peak stays unused. Desktop Music previews the sketch; notes enter the Song Graph only after an explicit accept. Pitch and onset gestures keep their own sketches. Expression curves, musical parts, and automatic retargeting remain later work. Phone Review does not create notes.

Slice 6.21 stores an artist-authored dynamics curve as Song Graph data. Schema v29 keeps `expressionCurves` in a separate collection. Desktop Music previews a transient loudness-gesture sketch, then explicitly accepts one Dynamics curve whose points are MIDI expression 0–127. MIDI export can translate those points to CC 11 when playable notes exist. Placement changes do not move accepted points, and removing the take does not drop the curve. Phone Review does not create curves. Freehand point editing, extra curve kinds, and instrument retargeting remain later work.

Slice 7.1 stores cello and guitar as versioned host-owned instrument profiles: range, arrangement roles, articulations, and expressive qualities. Desktop Arrangement can inspect them. The catalog does not assign an instrument, recommend a choice, or retarget a gesture.

Slice 7.2 lists catalog instruments that can cover an assigned job, optionally filtered by expressive quality. Matches stay in catalog order and are not written to the Song Graph.

Slice 7.3 grows that catalog to a five-instrument proof set chosen for different performance behavior: bowed cello, plucked acoustic guitar, struck piano, grooving electric bass, and unpitched drum kit.

Slice 7.4 compares existing notes with those ranges without moving them.

Slice 7.5 maps swell and slide onto catalog articulations so later retargeters cannot assume cello-like technique. Cello uses bow expression and slide; guitar uses picking and bend; piano strike covers swell; bass finger covers swell; drum kit does not apply. The map does not retarget a recorded gesture.

Slice 7.6 consumes approved loudness gestures as swells and approved pitch gestures as slides on one original-vocal take, then projects cello and guitar performances from that same input. Range collisions are reported without transposition. Piano, bass, and drum kit stay unused here. The sketch is inspectable only: it does not assign an instrument or write Song Graph data.

Slice 7.7 stores an optional catalog `instrumentProfileId` on a musical part so the artist can name cello, guitar, piano, bass, or drum kit. Schema v30 migrates existing parts as unassigned. The assignment does not retarget a gesture, persist the 7.6 sketch, or emit MIDI.

Slice 7.8 persists a reviewed cello or guitar sketch against a part that already names that instrument. Schema v31 tags accepted swells with optional `instrumentProfileId` on the dynamics curve; schema-v30 curves migrate as unassigned. In-range slides join the named part. Out-of-range slides are skipped. MIDI still emits dynamics as CC 11 without a program change.

Slice 7.9 projects the same approved swell or slide onto piano, electric bass, and drum kit using the catalog articulation map. Piano strike and bass finger cover swell; piano and bass slides, and both kit gestures, stay not applicable rather than inventing cello-like technique. Named piano or bass parts can store those swells. Onset-to-hit mapping and later catalog waves remain later work.

Slice 7.10 maps approved onset gestures onto drum-kit Hit. Cello, guitar, piano, and bass stay unused for that gesture. Named kit parts can store the hits as playable notes. The host does not invent kit pieces, General MIDI drum notes, or a drum MIDI channel. Later catalog waves remain later work.

Slice 7.11 grows the catalog to version 3 with violin, flute, clarinet, and trumpet. They are orchestration concepts with their own ranges, jobs, and articulations. Gesture maps stay unused so the host does not invent cello or kit technique for them. Electric guitar and synths remain later work.

Slice 7.12 maps swell and slide onto those Wave 2 instruments using each profile’s own articulations. Violin swell is bow expression and violin slide stays a slide. Flute swell is breath-shaped sustain. Clarinet and trumpet swells are legato. Wind slides and Wave 2 hits stay not applicable. Named parts can store those retargets. Electric guitar and synths remain later work.

Slice 7.13 grows the catalog to version 4 with synth pad, synth lead, and electric guitar. They are modern production concepts with their own ranges, jobs, and articulations. Gesture maps stay unused so the host does not invent cello, guitar, or kit technique for them.

Slice 7.14 maps swell and slide onto those Wave 3 instruments using each profile’s own articulations. Synth pad swell is pad. Synth lead swell is filter and synth lead slide is portamento. Electric guitar swell is distortion and electric guitar slide is bend. Synth pad slides and Wave 3 hits stay not applicable. Named parts can store those retargets. Organ and ensemble strings remain later work.

Slice 7.15 maps drum-kit Hit onto General MIDI Acoustic Bass Drum at C2 (MIDI 36). Preview and persist use that pitch instead of a melodic C4 placeholder. The host does not choose snare or hat, emit a program change, or move hits onto channel 10. The voice-to-MIDI onset sketch stays C4.

Slice 7.16 exports those named kit hits on MIDI channel 10. Unassigned C2 notes and pitched-instrument parts stay on channel 0. Dynamics still emit as CC 11 on channel 0 without a program change.

The AI Director may reason over these structured observations. Direct interpretation by an audio-capable model may supplement deterministic analysis, but it is optional, must carry its own confidence and provenance, and must never be the sole authoritative representation of a performance.

## Instrument-specific retargeting

Voice-to-instrument is treated as performance capture and retargeting. A neutral gesture should be adapted to the target instrument concept: cello may translate a swell into bow expression and a slide into legato; acoustic guitar may translate the same input into picking dynamics, bends, and hammer-ons; piano into strike and decay; flute into breath-shaped sustain. Adapters should enforce range and articulation limitations, and must not assume every target is cello-like. Drum kit is an unpitched target.

Slice 7.6 previews cello and guitar from the same approved gestures using the catalog articulation map. Slice 7.8 can persist that preview onto a part that already names cello or guitar. Slice 7.9 adds piano, bass, and kit adapters from that same map: swell applies as piano strike or bass finger; slides and both kit gestures stay unused. Slice 7.10 maps onset gestures onto drum-kit Hit on a part that already names the kit. Slice 7.12 maps Wave 2 orchestration the same way: violin uses bow expression and slide, flute uses breath, clarinet and trumpet use legato, and none of them take kit hits. Slice 7.13 can name synth pad, synth lead, or electric guitar. Slice 7.14 maps Wave 3 production the same way: synth pad uses pad, synth lead uses filter and portamento, electric guitar uses distortion and bend, and none of them take kit hits. Slice 7.15 stores kit hits as General MIDI Acoustic Bass Drum (C2) rather than a melodic C4, without choosing a kit piece or a drum MIDI channel. Slice 7.16 exports named kit hits on MIDI channel 10 without a program change or channels for pitched instruments. Accepted instrumental notes do not replace the artist's lead vocal.

Slice 7.7 can name a catalog instrument on an existing musical part. That name is not a renderer or a MIDI program.

## Rendering strategy

Introduce renderers incrementally:

1. Browser or simple synth preview
2. General MIDI or SoundFont
3. External DAW MIDI/stem export
4. Native VST3 host and offline rendering

Song logic must not depend on any renderer. VSTs, SoundFonts, DAWs, and possible future neural renderers may produce sound—including processing applied to the artist's recorded vocal—but they must not own the composition logic or stand in for the singer.

## Human vocal workflow

The lead-vocal path is a singer's workflow, not a vocal-generation workflow:

- Count-in, metronome, lyric highlighting, and a guide melody help the artist perform the part.
- Takes, punch-in, and comping preserve source recordings and the artist's chosen composite.
- Pitch, timing, and prosody visualization inform the next take without replacing it.
- Harmony guides and non-destructive vocal effects assist the recorded performance.
- Production suggestions compile into inspectable, reviewable settings the artist can accept, modify, or reject.

Feedback should inform the singer. Processing may polish the recorded vocal. Neither step authors a replacement lead.

## Vocal production intent

Vocal production is an intent-first companion to the recorded take, not a plugin scavenger hunt. A typical independent-producer chain—cleanup, corrective tone, character compression, saturation, transparent level control, sibilance control, and separate space—proves which jobs exist. It is not a list of products, GUIs, or algorithms to clone.

The artist-facing path is:

```text
Artist intent → vocal-production profile → processing-role chain
             → concrete DSP or VST realization → preview → artist approval
```

The product principle is: the artist chooses the desired vocal result; Maskil Forge determines or proposes the processing roles needed to achieve it. Example result vocabulary includes Clean, Warm, Intimate, Forward, Soft Rock, Cinematic, and Aggressive. Those names are later mapping language, not baked presets.

Internally the host reasons in processing **roles**, not plugin dependencies:

- Cleanup / Gate
- Corrective Tone / Dynamic EQ
- Character Compression
- Saturation / Color
- Transparent Dynamics Control
- Sibilance Control / De-Esser
- Space / Reverb and Delay
- Output / Gain or limiting, only if later justified

Character compression and transparent level control remain distinct. Flattening both into a generic compressor would hide a real production choice. Space on the vocal path is a production role; mix buses, sends, and song-level “closer vocal” recipes remain Milestone 11.

A role may later be realized as Maskil built-in DSP or as an artist-selected compatible VST3. The project should describe processing intent independently enough that the renderer stays replaceable. No commercial plugin is the canonical representation of the chain.

The beginner studio uses progressive disclosure: language such as “Warm, controlled, and intimate” first, with threshold, ratio, Q, and similar controls behind Advanced. Phone capture remains a companion; this production surface belongs on the studio screen.

The existing artist-authority contract still applies: analyze, propose, preview, then accept. The host must not silently process, correct, normalize, tune, compress, EQ, or otherwise alter the authoritative take. Processing is non-destructive and reviewable. Built-in processors and VSTs may assist the recorded human lead vocal; they may not generate or replace the singer.

Named Milestone 9 slices for this path begin with inspectable intent and roles before any DSP. Guide vocals, lyric highlighting, punch-in, comping, and pitch or timing feedback remain other Milestone 9 work and are not replaced by the processing-role track.

## Mixing

Begin with channel volume, pan, buses, sends, and automation. Add semantic production commands only after their deterministic operations are defined—for example, “closer vocal” becomes a reviewable set of reverb, direct-level, stereo, and EQ changes. Applying those settings must not substitute a generated vocal for the artist's take. Vocal-path Space in Milestone 9 is a production role on the take; mix routing remains this later song-level layer.

## Planned completion gate

This future gate will be met when:

- A recorded phrase produces editable gesture data without discarding the source recording.
- One gesture can be retargeted to at least two instruments with different articulation behavior.
- Takes and comps preserve source recordings, and the artist's chosen lead vocal remains authoritative.
- Pitch, timing, and prosody guidance, guide melodies, and reviewable vocal production settings assist that performance.
- Projects render consistently through at least one preview renderer and export MIDI/stems.
- No renderer, VST, or generated voice is treated as the final lead singer.
