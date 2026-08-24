using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record LoudnessGestureNoteSketchEvent(
    RegisteredPitch Pitch,
    long StartTick,
    long DurationTicks,
    int Velocity,
    PerformanceObservationGestureId GestureId,
    PerformanceObservationId ObservationId);

/// <summary>A transient, reviewable projection. It is not stored until explicitly accepted.</summary>
public sealed record LoudnessGestureNoteSketch(
    ProjectAssetId SourceAssetId,
    long StartTick,
    IReadOnlyList<LoudnessGestureNoteSketchEvent> Events);

public static class LoudnessGestureNoteSketcher
{
    public const string RmsMeasurementName = "rmsDbfs";
    public const string LoudnessObservationKind = "loudness.frame";
    private static readonly RegisteredPitch HitPitch = new(NoteLetter.C, Accidental.Natural, 4);
    private const int FallbackVelocity = 96;
    private const decimal MinimumRmsDecibels = -120m;
    private const decimal MaximumRmsDecibels = 0m;
    private const decimal VelocityFloorDecibels = -60m;

    public static LoudnessGestureNoteSketch Project(SongProject project, ProjectAssetId sourceAssetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Assets.All(asset => asset.Id != sourceAssetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new KeyNotFoundException($"Original vocal asset '{sourceAssetId}' was not found.");

        var observations = project.PerformanceObservations
            .Where(item => item.SourceAssetId == sourceAssetId
                && string.Equals(item.Kind, LoudnessObservationKind, StringComparison.Ordinal))
            .ToDictionary(item => item.Id);
        var beatsPerMinute = project.Tempo.BeatsPerMinute;
        var ticksPerQuarterNote = project.Timeline.TicksPerQuarterNote;
        var takeStartTick = project.VocalTakeStartTick(sourceAssetId);
        var events = new List<LoudnessGestureNoteSketchEvent>();

        foreach (var gesture in project.PerformanceObservationGestures)
        {
            if (!observations.TryGetValue(gesture.ObservationId, out var observation)) continue;
            var rms = gesture.Measurements.FirstOrDefault(item =>
                string.Equals(item.Name, RmsMeasurementName, StringComparison.OrdinalIgnoreCase));
            if (rms is not null && rms.Value is < MinimumRmsDecibels or > MaximumRmsDecibels)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceAssetId),
                    "Loudness-gesture RMS must be between -120 and 0 dBFS.");

            events.Add(new LoudnessGestureNoteSketchEvent(
                HitPitch,
                checked(takeStartTick + MillisecondsToTicks(observation.StartMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                Math.Max(1, MillisecondsToTicks(observation.DurationMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                rms is null ? FallbackVelocity : ToVelocity(rms.Value),
                gesture.Id,
                observation.Id));
        }

        if (events.Count == 0)
            throw new InvalidOperationException("Promote at least one loudness claim to a gesture before preparing a note sketch.");

        return new LoudnessGestureNoteSketch(
            sourceAssetId,
            takeStartTick,
            events
                .OrderBy(item => item.StartTick)
                .ThenBy(item => item.ObservationId.Value)
                .ToList());
    }

    private static int ToVelocity(decimal rmsDecibels)
    {
        var scaled = (rmsDecibels - VelocityFloorDecibels) / (MaximumRmsDecibels - VelocityFloorDecibels);
        var velocity = (int)decimal.Round(scaled * 127m, MidpointRounding.AwayFromZero);
        return Math.Clamp(velocity, 1, 127);
    }

    private static long MillisecondsToTicks(long milliseconds, decimal beatsPerMinute, int ticksPerQuarterNote)
    {
        var ticks = milliseconds * beatsPerMinute * ticksPerQuarterNote / 60_000m;
        return (long)decimal.Round(ticks, 0, MidpointRounding.AwayFromZero);
    }
}
