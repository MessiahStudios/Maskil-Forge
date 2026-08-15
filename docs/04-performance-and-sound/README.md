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
- Use voice analysis to drive editable musical or instrument-performance data, such as melody, rhythm, expression curves, or retargeted instrumental parts.

Maskil Forge must not:

- Present a generated or synthesized voice as the final lead singer.
- Silently replace an accepted take with processed or generated audio.
- Treat preview tones, guide tracks, or instrumental realizations as the lead vocal.
- Let a renderer, VST, or neural audio model own the Song Graph or the artist's vocal authorship.

Keep the original recording, extracted observations, edited gesture data, retargeted instrumental performance, and production settings separate. Correction, re-targeting, and production changes should be possible without discarding the source take.

The first implemented boundary is a microphone preflight rather than a recording shortcut. It checks secure-browser support only after an explicit artist action, confirms a live input, and immediately closes the test stream without recording, uploading, or saving sound. Recording waits for an external-asset lifecycle that protects original takes through backup, recovery, portable transfer, Trash, and permanent deletion.

Schema v22 adds the first durable half of that lifecycle: a path-free manifest can identify an original vocal asset by stable ID, media type, byte length, SHA-256 digest, and creation time. The manifest intentionally contains neither the audio bytes nor analysis or processing data. Legacy JSON portability refuses non-empty manifests rather than exporting broken media references; an asset-owning package remains the next prerequisite for recording.

The repository now supplies the local half of the media lifecycle. It accepts a new immutable asset only after staging and verifying its exact length and SHA-256 digest, validates those bytes with every project load, and moves paired media through backup, session recovery, corrupt-data preservation, Trash, restore, and permanent deletion. This is storage infrastructure rather than a capture feature: the microphone preflight still creates no recording, and portable packaging remains the final durability gate before rough takes can be enabled.

## Performance capture pipeline

```text
Recorded voice -> Pitch/onset/loudness analysis -> Gesture data
               -> Editable notes and curves -> Instrument retargeter
               -> MIDI, bends, expression, and articulations
```

Voice analysis may update editable Song Graph material only through explicit, reversible commands. A hummed or sung gesture can control instruments without becoming a substitute lead vocal.

## Instrument-specific retargeting

Voice-to-instrument is treated as performance capture and retargeting. A neutral gesture should be adapted to the target instrument: cello may translate a swell into bow expression and a slide into legato; guitar may translate the same input into picking dynamics, bends, and hammer-ons. Adapters should enforce range and articulation limitations.

Retargeted instrumental parts remain editable project data. They do not replace the artist's lead vocal.

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

## Mixing

Begin with channel volume, pan, buses, sends, and automation. Add semantic production commands only after their deterministic operations are defined—for example, “closer vocal” becomes a reviewable set of reverb, direct-level, stereo, and EQ changes. Applying those settings must not substitute a generated vocal for the artist's take.

## Planned completion gate

This future gate will be met when:

- A recorded phrase produces editable gesture data without discarding the source recording.
- One gesture can be retargeted to at least two instruments with different articulation behavior.
- Takes and comps preserve source recordings, and the artist's chosen lead vocal remains authoritative.
- Pitch, timing, and prosody guidance, guide melodies, and reviewable vocal production settings assist that performance.
- Projects render consistently through at least one preview renderer and export MIDI/stems.
- No renderer, VST, or generated voice is treated as the final lead singer.
