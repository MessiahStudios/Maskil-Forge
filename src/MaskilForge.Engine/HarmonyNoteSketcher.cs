using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record HarmonyNoteSketchEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    bool UsesPreviewVoicing);

/// <summary>A transient, reviewable projection. It is not stored until explicitly accepted.</summary>
public sealed record HarmonyNoteSketch(
    SectionId SectionId,
    IReadOnlyList<HarmonyNoteSketchEvent> Events,
    bool UsesPreviewVoicings);

public static class HarmonyNoteSketcher
{
    private const int PreviewVelocity = 96;

    public static HarmonyNoteSketch Project(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        if (section.Harmony.Count == 0)
            throw new InvalidOperationException("Add at least one harmony chord before preparing a playable-note sketch.");

        var meter = project.TimeSignature;
        var ticksPerBeat = checked(project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var ticksPerBar = checked((long)meter.Numerator * ticksPerBeat);
        var sectionStart = project.Timeline.ToAbsoluteTicks(project.Timeline.FindSection(sectionId).Start);
        var events = new List<HarmonyNoteSketchEvent>();

        foreach (var harmony in section.Harmony.OrderBy(item => item.Start))
        {
            var relativeStart = checked(
                (long)(harmony.Start.Bar - 1) * ticksPerBar
                + (long)(harmony.Start.Beat - 1) * ticksPerBeat
                + harmony.Start.Tick);
            var duration = checked((long)harmony.DurationBars * ticksPerBar);
            var usesPreview = harmony.Voicing is null;
            var pitches = harmony.Voicing?.Voices.Select(item => item.Pitch).ToList()
                ?? PreviewPitches(harmony.Chord);
            events.AddRange(pitches.Select(pitch => new HarmonyNoteSketchEvent(
                pitch,
                checked(sectionStart + relativeStart),
                duration,
                PreviewVelocity,
                usesPreview)));
        }

        return new HarmonyNoteSketch(
            sectionId,
            events.OrderBy(item => item.StartTick).ThenBy(item => item.Pitch.MidiNumber).ToList(),
            events.Any(item => item.UsesPreviewVoicing));
    }

    private static List<RegisteredPitch> PreviewPitches(ChordSymbol chord)
    {
        var rootPitchClass = chord.Spelling.PitchClass.Value;
        var root = 48 + rootPitchClass;
        return chord.PitchClasses
            .Select(item => item.Value)
            .Select(pitchClass => root + ((pitchClass - rootPitchClass + 12) % 12))
            .Select(FromMidiNumber)
            .ToList();
    }

    private static RegisteredPitch FromMidiNumber(int midiNumber)
    {
        var octave = midiNumber / 12 - 1;
        return (midiNumber % 12) switch
        {
            0 => new RegisteredPitch(NoteLetter.C, Accidental.Natural, octave),
            1 => new RegisteredPitch(NoteLetter.C, Accidental.Sharp, octave),
            2 => new RegisteredPitch(NoteLetter.D, Accidental.Natural, octave),
            3 => new RegisteredPitch(NoteLetter.D, Accidental.Sharp, octave),
            4 => new RegisteredPitch(NoteLetter.E, Accidental.Natural, octave),
            5 => new RegisteredPitch(NoteLetter.F, Accidental.Natural, octave),
            6 => new RegisteredPitch(NoteLetter.F, Accidental.Sharp, octave),
            7 => new RegisteredPitch(NoteLetter.G, Accidental.Natural, octave),
            8 => new RegisteredPitch(NoteLetter.G, Accidental.Sharp, octave),
            9 => new RegisteredPitch(NoteLetter.A, Accidental.Natural, octave),
            10 => new RegisteredPitch(NoteLetter.A, Accidental.Sharp, octave),
            _ => new RegisteredPitch(NoteLetter.B, Accidental.Natural, octave),
        };
    }
}
