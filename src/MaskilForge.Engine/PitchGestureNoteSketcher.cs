using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record PitchGestureNoteSketchEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    PerformanceObservationGestureId GestureId,
    PerformanceObservationId ObservationId);

/// <summary>A transient, reviewable projection. It is not stored until explicitly accepted.</summary>
public sealed record PitchGestureNoteSketch(
    ProjectAssetId SourceAssetId,
    long StartTick,
    IReadOnlyList<PitchGestureNoteSketchEvent> Events);

public static class PitchGestureNoteSketcher
{
    public const string FrequencyMeasurementName = "frequencyHertz";
    private const int PreviewVelocity = 96;

    public static PitchGestureNoteSketch Project(SongProject project, ProjectAssetId sourceAssetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Assets.All(asset => asset.Id != sourceAssetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new KeyNotFoundException($"Original vocal asset '{sourceAssetId}' was not found.");

        var observations = project.PerformanceObservations
            .Where(item => item.SourceAssetId == sourceAssetId)
            .ToDictionary(item => item.Id);
        var beatsPerMinute = project.Tempo.BeatsPerMinute;
        var ticksPerQuarterNote = project.Timeline.TicksPerQuarterNote;
        var takeStartTick = project.VocalTakeStartTick(sourceAssetId);
        var events = new List<PitchGestureNoteSketchEvent>();

        foreach (var gesture in project.PerformanceObservationGestures)
        {
            if (!observations.TryGetValue(gesture.ObservationId, out var observation)) continue;
            var frequency = gesture.Measurements.FirstOrDefault(item =>
                string.Equals(item.Name, FrequencyMeasurementName, StringComparison.OrdinalIgnoreCase));
            if (frequency is null) continue;
            if (frequency.Value <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceAssetId),
                    "Pitch-gesture frequency must be greater than zero.");

            events.Add(new PitchGestureNoteSketchEvent(
                FromFrequency(frequency.Value),
                checked(takeStartTick + MillisecondsToTicks(observation.StartMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                Math.Max(1, MillisecondsToTicks(observation.DurationMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                PreviewVelocity,
                gesture.Id,
                observation.Id));
        }

        if (events.Count == 0)
            throw new InvalidOperationException("Promote at least one pitch claim to a gesture before preparing a note sketch.");

        return new PitchGestureNoteSketch(
            sourceAssetId,
            takeStartTick,
            events
                .OrderBy(item => item.StartTick)
                .ThenBy(item => item.ObservationId.Value)
                .ToList());
    }

    private static long MillisecondsToTicks(long milliseconds, decimal beatsPerMinute, int ticksPerQuarterNote)
    {
        var ticks = milliseconds * beatsPerMinute * ticksPerQuarterNote / 60_000m;
        return (long)decimal.Round(ticks, 0, MidpointRounding.AwayFromZero);
    }

    private static RegisteredPitch FromFrequency(decimal frequencyHertz)
    {
        var midiNumber = (int)decimal.Round(
            69m + 12m * (decimal)Math.Log2((double)frequencyHertz / 440d),
            MidpointRounding.AwayFromZero);
        return FromMidiNumber(Math.Clamp(midiNumber, 0, 127));
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
