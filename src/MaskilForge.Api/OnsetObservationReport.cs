using MaskilForge.Domain;

namespace MaskilForge.Api;

public sealed record OnsetEventReport(
    long StartMilliseconds,
    long DurationMilliseconds,
    decimal Strength,
    decimal Confidence);

public static class OnsetObservationReport
{
    public const string AnalyzerId = "maskil.browser.onset-energy";
    public const string AnalyzerVersion = "1.0.0";
    public const string ObservationKind = "onset.event";
    public const long WindowDurationMilliseconds = 32;
    public const long HopDurationMilliseconds = 16;
    public const long MinimumSeparationMilliseconds = 96;
    public const int MaximumEventCount = 625;
    public const long MaximumAnalyzedDurationMilliseconds = 60_000;
    public const decimal MinimumConfidence = .6m;

    public static IReadOnlyList<PerformanceObservation> CreateObservations(
        ProjectAssetId sourceAssetId,
        IReadOnlyList<OnsetEventReport> events,
        DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (events.Count > MaximumEventCount)
            throw new ArgumentOutOfRangeException(nameof(events), $"Onset analysis cannot exceed {MaximumEventCount} candidates.");

        var observations = new List<PerformanceObservation>(events.Count);
        long? previousStart = null;
        foreach (var candidate in events)
        {
            if (candidate.StartMilliseconds < 0 || candidate.StartMilliseconds % HopDurationMilliseconds != 0)
                throw new ArgumentException("Onset candidates must be ordered on the 16 millisecond analysis grid.", nameof(events));
            if (candidate.DurationMilliseconds != WindowDurationMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(events), "Each onset candidate must span exactly 32 milliseconds.");
            if (candidate.StartMilliseconds > MaximumAnalyzedDurationMilliseconds - candidate.DurationMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(events), "Onset analysis cannot exceed the one-minute recording limit.");
            if (previousStart.HasValue && candidate.StartMilliseconds - previousStart.Value < MinimumSeparationMilliseconds)
                throw new ArgumentException("Onset candidates must be strictly ordered and at least 96 milliseconds apart.", nameof(events));
            if (candidate.Strength is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(events), "Onset strength must be between 0 and 1.");
            if (candidate.Confidence is < MinimumConfidence or > 1)
                throw new ArgumentOutOfRangeException(nameof(events), "Onset confidence must be between 0.6 and 1.");

            previousStart = candidate.StartMilliseconds;
            observations.Add(new PerformanceObservation(
                PerformanceObservationId.New(),
                sourceAssetId,
                ObservationKind,
                candidate.StartMilliseconds,
                candidate.DurationMilliseconds,
                [new PerformanceMeasurement("strength", candidate.Strength, "normalized")],
                candidate.Confidence,
                AnalyzerId,
                AnalyzerVersion,
                PerformanceObservationProvenance.DeterministicAnalyzer,
                createdUtc));
        }

        return observations;
    }
}
