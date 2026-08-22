using MaskilForge.Domain;

namespace MaskilForge.Api;

public sealed record LoudnessFrameReport(
    long StartMilliseconds,
    long DurationMilliseconds,
    decimal RmsDecibels,
    decimal PeakDecibels);

public static class LoudnessObservationReport
{
    public const string AnalyzerId = "maskil.browser.loudness";
    public const string AnalyzerVersion = "1.0.0";
    public const string ObservationKind = "loudness.frame";
    public const long FrameDurationMilliseconds = 250;
    public const int MaximumFrameCount = 240;
    public const long MaximumAnalyzedDurationMilliseconds = 60_000;

    public static IReadOnlyList<PerformanceObservation> CreateObservations(
        ProjectAssetId sourceAssetId,
        IReadOnlyList<LoudnessFrameReport> frames,
        DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0) throw new ArgumentException("Loudness analysis must contain at least one frame.", nameof(frames));
        if (frames.Count > MaximumFrameCount)
            throw new ArgumentOutOfRangeException(nameof(frames), $"Loudness analysis cannot exceed {MaximumFrameCount} frames.");

        var observations = new List<PerformanceObservation>(frames.Count);
        long expectedStart = 0;
        for (var index = 0; index < frames.Count; index++)
        {
            var frame = frames[index];
            if (frame.StartMilliseconds != expectedStart)
                throw new ArgumentException("Loudness frames must be ordered, contiguous, and begin at zero.", nameof(frames));
            var isFinalFrame = index == frames.Count - 1;
            if (frame.DurationMilliseconds < 1
                || frame.DurationMilliseconds > FrameDurationMilliseconds
                || (!isFinalFrame && frame.DurationMilliseconds != FrameDurationMilliseconds))
                throw new ArgumentOutOfRangeException(nameof(frames), "Loudness frames must span 250 milliseconds, except for a shorter final frame.");
            if (frame.RmsDecibels is < -120 or > 0 || frame.PeakDecibels is < -120 or > 0)
                throw new ArgumentOutOfRangeException(nameof(frames), "Loudness measurements must be between -120 and 0 dBFS.");
            if (frame.RmsDecibels > frame.PeakDecibels)
                throw new ArgumentException("A frame RMS level cannot exceed its peak level.", nameof(frames));

            expectedStart = checked(frame.StartMilliseconds + frame.DurationMilliseconds);
            if (expectedStart > MaximumAnalyzedDurationMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(frames), "Loudness analysis cannot exceed the one-minute recording limit.");

            observations.Add(new PerformanceObservation(
                PerformanceObservationId.New(),
                sourceAssetId,
                ObservationKind,
                frame.StartMilliseconds,
                frame.DurationMilliseconds,
                [
                    new PerformanceMeasurement("rmsDbfs", frame.RmsDecibels, "dBFS"),
                    new PerformanceMeasurement("peakDbfs", frame.PeakDecibels, "dBFS")
                ],
                null,
                AnalyzerId,
                AnalyzerVersion,
                PerformanceObservationProvenance.DeterministicAnalyzer,
                createdUtc));
        }

        return observations;
    }
}
