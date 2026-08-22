using MaskilForge.Domain;

namespace MaskilForge.Api;

public sealed record PitchFrameReport(
    long StartMilliseconds,
    long DurationMilliseconds,
    decimal FrequencyHertz,
    decimal Confidence);

public static class PitchObservationReport
{
    public const string AnalyzerId = "maskil.browser.pitch-acf";
    public const string AnalyzerVersion = "1.0.0";
    public const string ObservationKind = "pitch.frame";
    public const long WindowDurationMilliseconds = 80;
    public const long HopDurationMilliseconds = 200;
    public const int MaximumFrameCount = 300;
    public const long MaximumAnalyzedDurationMilliseconds = 60_000;
    public const decimal MinimumFrequencyHertz = 65;
    public const decimal MaximumFrequencyHertz = 1_000;
    public const decimal MinimumConfidence = .72m;

    public static IReadOnlyList<PerformanceObservation> CreateObservations(
        ProjectAssetId sourceAssetId,
        IReadOnlyList<PitchFrameReport> frames,
        DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count > MaximumFrameCount)
            throw new ArgumentOutOfRangeException(nameof(frames), $"Pitch analysis cannot exceed {MaximumFrameCount} voiced frames.");

        var observations = new List<PerformanceObservation>(frames.Count);
        long previousStart = -HopDurationMilliseconds;
        foreach (var frame in frames)
        {
            if (frame.StartMilliseconds < 0
                || frame.StartMilliseconds % HopDurationMilliseconds != 0
                || frame.StartMilliseconds <= previousStart)
                throw new ArgumentException("Pitch frames must be strictly ordered on the 200 millisecond analysis grid.", nameof(frames));
            if (frame.DurationMilliseconds != WindowDurationMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(frames), "Each pitch frame must span exactly 80 milliseconds.");
            if (frame.StartMilliseconds > MaximumAnalyzedDurationMilliseconds - frame.DurationMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(frames), "Pitch analysis cannot exceed the one-minute recording limit.");
            if (frame.FrequencyHertz is < MinimumFrequencyHertz or > MaximumFrequencyHertz)
                throw new ArgumentOutOfRangeException(nameof(frames), "Pitch frequency must be between 65 and 1000 hertz.");
            if (frame.Confidence is < MinimumConfidence or > 1)
                throw new ArgumentOutOfRangeException(nameof(frames), "Pitch confidence must be between 0.72 and 1.");

            previousStart = frame.StartMilliseconds;
            observations.Add(new PerformanceObservation(
                PerformanceObservationId.New(),
                sourceAssetId,
                ObservationKind,
                frame.StartMilliseconds,
                frame.DurationMilliseconds,
                [new PerformanceMeasurement("frequencyHertz", frame.FrequencyHertz, "hertz")],
                frame.Confidence,
                AnalyzerId,
                AnalyzerVersion,
                PerformanceObservationProvenance.DeterministicAnalyzer,
                createdUtc));
        }

        return observations;
    }
}
