using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum PerformanceObservationProvenance
{
    DeterministicAnalyzer,
    ImportedAnalyzer,
    AudioModel
}

public sealed record PerformanceMeasurement
{
    [JsonConstructor]
    public PerformanceMeasurement(string name, decimal value, string unit)
    {
        Name = RequiredText(name, 100, "A performance measurement name is required.", "A performance measurement name cannot exceed 100 characters.");
        Value = value;
        Unit = RequiredText(unit, 40, "A performance measurement unit is required.", "A performance measurement unit cannot exceed 40 characters.");
    }

    public string Name { get; }
    public decimal Value { get; }
    public string Unit { get; }

    private static string RequiredText(string value, int maximumLength, string requiredMessage, string lengthMessage)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(requiredMessage, nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(nameof(value), lengthMessage);
        return normalized;
    }
}

public sealed record PerformanceObservation
{
    [JsonConstructor]
    public PerformanceObservation(
        PerformanceObservationId id,
        ProjectAssetId sourceAssetId,
        string kind,
        long startMilliseconds,
        long durationMilliseconds,
        IReadOnlyList<PerformanceMeasurement> measurements,
        decimal? confidence,
        string analyzerId,
        string analyzerVersion,
        PerformanceObservationProvenance provenance,
        DateTimeOffset createdUtc)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A performance observation ID is required.", nameof(id));
        if (sourceAssetId.Value == Guid.Empty) throw new ArgumentException("A source asset ID is required.", nameof(sourceAssetId));
        if (startMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(startMilliseconds), "Observation start cannot be negative.");
        if (durationMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds), "Observation duration cannot be negative.");
        if (confidence is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(confidence), "Observation confidence must be between zero and one when available.");
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Observation provenance is invalid.");
        if (createdUtc == default) throw new ArgumentException("An observation creation time is required.", nameof(createdUtc));

        Id = id;
        SourceAssetId = sourceAssetId;
        Kind = RequiredText(kind, 100, "An observation kind is required.", "An observation kind cannot exceed 100 characters.");
        StartMilliseconds = startMilliseconds;
        DurationMilliseconds = durationMilliseconds;
        Measurements = measurements?.ToList() ?? throw new ArgumentNullException(nameof(measurements));
        if (Measurements.Count == 0) throw new ArgumentException("An observation must contain at least one measurement.", nameof(measurements));
        if (Measurements.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Measurements.Count)
            throw new ArgumentException("Performance measurement names must be unique within an observation.", nameof(measurements));
        Confidence = confidence;
        AnalyzerId = RequiredText(analyzerId, 100, "An analyzer ID is required.", "An analyzer ID cannot exceed 100 characters.");
        AnalyzerVersion = RequiredText(analyzerVersion, 50, "An analyzer version is required.", "An analyzer version cannot exceed 50 characters.");
        Provenance = provenance;
        CreatedUtc = createdUtc;
    }

    public PerformanceObservationId Id { get; }
    public ProjectAssetId SourceAssetId { get; }
    public string Kind { get; }
    public long StartMilliseconds { get; }
    public long DurationMilliseconds { get; }
    public IReadOnlyList<PerformanceMeasurement> Measurements { get; }
    public decimal? Confidence { get; }
    public string AnalyzerId { get; }
    public string AnalyzerVersion { get; }
    public PerformanceObservationProvenance Provenance { get; }
    public DateTimeOffset CreatedUtc { get; }

    private static string RequiredText(string value, int maximumLength, string requiredMessage, string lengthMessage)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(requiredMessage, nameof(value));
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(nameof(value), lengthMessage);
        return normalized;
    }
}
