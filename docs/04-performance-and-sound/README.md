# 04 — Performance and Sound

This planned layer will let the artist control musical expression with their voice and, eventually, finish an audible production. The human singer remains the intended final lead performer.

## Performance capture pipeline

```text
Recorded voice -> Pitch/onset/loudness analysis -> Gesture data
               -> Editable notes and curves -> Instrument retargeter
               -> MIDI, bends, expression, and articulations
```

Keep the original recording, extracted observations, edited gesture data, and retargeted performance separate. This makes correction and re-targeting possible without re-recording.

## Instrument-specific retargeting

Voice-to-instrument is treated as performance capture and retargeting. A neutral gesture should be adapted to the target instrument: cello may translate a swell into bow expression and a slide into legato; guitar may translate the same input into picking dynamics, bends, and hammer-ons. Adapters should enforce range and articulation limitations.

## Rendering strategy

Introduce renderers incrementally:

1. Browser or simple synth preview
2. General MIDI or SoundFont
3. External DAW MIDI/stem export
4. Native VST3 host and offline rendering

Song logic must not depend on any renderer. VSTs, SoundFonts, DAWs, and possible future neural renderers may produce sound, but they must not own the composition logic.

## Human vocal workflow

Add count-in, metronome, guide melody, lyric highlighting, takes, punch-in, comping, and pitch/timing visualization. Feedback should inform the singer without replacing them.

## Mixing

Begin with channel volume, pan, buses, sends, and automation. Add semantic production commands only after their deterministic operations are defined—for example, “closer vocal” becomes a reviewable set of reverb, direct-level, stereo, and EQ changes.

## Planned completion gate

This future gate will be met when:

- A recorded phrase produces editable gesture data.
- One gesture can be retargeted to at least two instruments with different articulation behavior.
- Takes and comps preserve source recordings.
- Projects render consistently through at least one preview renderer and export MIDI/stems.
