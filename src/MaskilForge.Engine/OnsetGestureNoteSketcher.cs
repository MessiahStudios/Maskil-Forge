using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record OnsetGestureNoteSketchEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    PerformanceObservationGestureId GestureId,
    PerformanceObservationId ObservationId);

/// <summary>A transient, reviewable projection. It is not stored until explicitly accepted.</summary>
public sealed record OnsetGestureNoteSketch(
    ProjectAssetId SourceAssetId,
    long StartTick,
    IReadOnlyList<OnsetGestureNoteSketchEvent> Events);

public static class OnsetGestureNoteSketcher
{
    public const string StrengthMeasurementName = "strength";
    public const string OnsetObservationKind = "onset.event";
    private static readonly RegisteredPitch HitPitch = new(NoteLetter.C, Accidental.Natural, 4);
    private const int FallbackVelocity = 96;

    public static OnsetGestureNoteSketch Project(SongProject project, ProjectAssetId sourceAssetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Assets.All(asset => asset.Id != sourceAssetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new KeyNotFoundException($"Original vocal asset '{sourceAssetId}' was not found.");

        var observations = project.PerformanceObservations
            .Where(item => item.SourceAssetId == sourceAssetId
                && string.Equals(item.Kind, OnsetObservationKind, StringComparison.Ordinal))
            .ToDictionary(item => item.Id);
        var beatsPerMinute = project.Tempo.BeatsPerMinute;
        var ticksPerQuarterNote = project.Timeline.TicksPerQuarterNote;
        var takeStartTick = project.VocalTakeStartTick(sourceAssetId);
        var events = new List<OnsetGestureNoteSketchEvent>();

        foreach (var gesture in project.PerformanceObservationGestures)
        {
            if (!observations.TryGetValue(gesture.ObservationId, out var observation)) continue;
            var strength = gesture.Measurements.FirstOrDefault(item =>
                string.Equals(item.Name, StrengthMeasurementName, StringComparison.OrdinalIgnoreCase));
            if (strength is not null && strength.Value is < 0 or > 1)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceAssetId),
                    "Onset-gesture strength must be between 0 and 1.");

            events.Add(new OnsetGestureNoteSketchEvent(
                HitPitch,
                checked(takeStartTick + MillisecondsToTicks(observation.StartMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                Math.Max(1, MillisecondsToTicks(observation.DurationMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                strength is null ? FallbackVelocity : ToVelocity(strength.Value),
                gesture.Id,
                observation.Id));
        }

        if (events.Count == 0)
            throw new InvalidOperationException("Promote at least one onset claim to a gesture before preparing a note sketch.");

        return new OnsetGestureNoteSketch(
            sourceAssetId,
            takeStartTick,
            events
                .OrderBy(item => item.StartTick)
                .ThenBy(item => item.ObservationId.Value)
                .ToList());
    }

    private static int ToVelocity(decimal strength)
    {
        var velocity = (int)decimal.Round(strength * 127m, MidpointRounding.AwayFromZero);
        return Math.Clamp(velocity, 1, 127);
    }

    private static long MillisecondsToTicks(long milliseconds, decimal beatsPerMinute, int ticksPerQuarterNote)
    {
        var ticks = milliseconds * beatsPerMinute * ticksPerQuarterNote / 60_000m;
        return (long)decimal.Round(ticks, 0, MidpointRounding.AwayFromZero);
    }
}
