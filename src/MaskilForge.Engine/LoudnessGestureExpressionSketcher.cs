using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>A transient, reviewable projection. It is not stored until explicitly accepted.</summary>
public sealed record LoudnessGestureExpressionSketch(
    ProjectAssetId SourceAssetId,
    string Name,
    ExpressionCurveKind Kind,
    long StartTick,
    IReadOnlyList<ExpressionCurvePoint> Points);

public static class LoudnessGestureExpressionSketcher
{
    public const string RmsMeasurementName = "rmsDbfs";
    public const string LoudnessObservationKind = "loudness.frame";
    private const int FallbackValue = 96;
    private const decimal MinimumRmsDecibels = -120m;
    private const decimal MaximumRmsDecibels = 0m;
    private const decimal VelocityFloorDecibels = -60m;

    public static LoudnessGestureExpressionSketch Project(SongProject project, ProjectAssetId sourceAssetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var asset = project.Assets.FirstOrDefault(item => item.Id == sourceAssetId && item.Kind == ProjectAssetKind.OriginalVocalTake)
            ?? throw new KeyNotFoundException($"Original vocal asset '{sourceAssetId}' was not found.");

        var observations = project.PerformanceObservations
            .Where(item => item.SourceAssetId == sourceAssetId
                && string.Equals(item.Kind, LoudnessObservationKind, StringComparison.Ordinal))
            .ToDictionary(item => item.Id);
        var beatsPerMinute = project.Tempo.BeatsPerMinute;
        var ticksPerQuarterNote = project.Timeline.TicksPerQuarterNote;
        var takeStartTick = project.VocalTakeStartTick(sourceAssetId);
        var points = new List<ExpressionCurvePoint>();

        foreach (var gesture in project.PerformanceObservationGestures)
        {
            if (!observations.TryGetValue(gesture.ObservationId, out var observation)) continue;
            var rms = gesture.Measurements.FirstOrDefault(item =>
                string.Equals(item.Name, RmsMeasurementName, StringComparison.OrdinalIgnoreCase));
            if (rms is not null && rms.Value is < MinimumRmsDecibels or > MaximumRmsDecibels)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceAssetId),
                    "Loudness-gesture RMS must be between -120 and 0 dBFS.");

            points.Add(new ExpressionCurvePoint(
                checked(takeStartTick + MillisecondsToTicks(observation.StartMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                rms is null ? FallbackValue : ToValue(rms.Value)));
        }

        if (points.Count == 0)
            throw new InvalidOperationException("Promote at least one loudness claim to a gesture before preparing an expression sketch.");

        return new LoudnessGestureExpressionSketch(
            sourceAssetId,
            CurveName(asset.Name),
            ExpressionCurveKind.Dynamics,
            takeStartTick,
            points
                .GroupBy(item => item.Tick)
                .Select(group => group.Last())
                .OrderBy(item => item.Tick)
                .ToList());
    }

    private static string CurveName(string assetName)
    {
        var name = $"{assetName} dynamics";
        return name.Length <= 80 ? name : name[..80];
    }

    private static int ToValue(decimal rmsDecibels)
    {
        var scaled = (rmsDecibels - VelocityFloorDecibels) / (MaximumRmsDecibels - VelocityFloorDecibels);
        var value = (int)decimal.Round(scaled * 127m, MidpointRounding.AwayFromZero);
        return Math.Clamp(value, 0, 127);
    }

    private static long MillisecondsToTicks(long milliseconds, decimal beatsPerMinute, int ticksPerQuarterNote)
    {
        var ticks = milliseconds * beatsPerMinute * ticksPerQuarterNote / 60_000m;
        return (long)decimal.Round(ticks, 0, MidpointRounding.AwayFromZero);
    }
}
