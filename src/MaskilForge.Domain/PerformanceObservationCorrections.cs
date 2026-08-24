using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public sealed record PerformanceObservationCorrection
{
    [JsonConstructor]
    public PerformanceObservationCorrection(
        PerformanceObservationCorrectionId id,
        PerformanceObservationId observationId,
        IReadOnlyList<PerformanceMeasurement> measurements,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A performance observation correction ID is required.", nameof(id));
        if (observationId.Value == Guid.Empty) throw new ArgumentException("A corrected observation ID is required.", nameof(observationId));
        if (createdUtc == default) throw new ArgumentException("A correction creation time is required.", nameof(createdUtc));
        if (updatedUtc == default) throw new ArgumentException("A correction update time is required.", nameof(updatedUtc));
        if (updatedUtc < createdUtc) throw new ArgumentOutOfRangeException(nameof(updatedUtc), "A correction cannot be updated before it was created.");

        Id = id;
        ObservationId = observationId;
        Measurements = measurements?.ToList() ?? throw new ArgumentNullException(nameof(measurements));
        if (Measurements.Count == 0) throw new ArgumentException("A correction must contain at least one measurement.", nameof(measurements));
        if (Measurements.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Measurements.Count)
            throw new ArgumentException("Corrected measurement names must be unique within a correction.", nameof(measurements));
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
    }

    public PerformanceObservationCorrectionId Id { get; }
    public PerformanceObservationId ObservationId { get; }
    public IReadOnlyList<PerformanceMeasurement> Measurements { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; }

    public PerformanceObservationCorrection Revise(IReadOnlyList<PerformanceMeasurement> measurements, DateTimeOffset updatedUtc) =>
        new(Id, ObservationId, measurements, CreatedUtc, updatedUtc);

    public static void ValidateAgainst(PerformanceObservation observation, IReadOnlyList<PerformanceMeasurement> measurements)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(measurements);
        if (measurements.Count != observation.Measurements.Count)
            throw new ArgumentException("A correction must replace every original measurement and cannot add new ones.", nameof(measurements));

        var originals = observation.Measurements.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var measurement in measurements)
        {
            if (!originals.TryGetValue(measurement.Name, out var original))
                throw new ArgumentException($"A correction cannot introduce the measurement '{measurement.Name}'.", nameof(measurements));
            if (!string.Equals(measurement.Unit, original.Unit, StringComparison.Ordinal))
                throw new ArgumentException($"Corrected measurement '{measurement.Name}' must keep the original unit '{original.Unit}'.", nameof(measurements));
            ValidateValue(measurement);
            if (measurement.Value != original.Value) changed = true;
        }

        if (!changed)
            throw new ArgumentException("A correction must change at least one measurement value.", nameof(measurements));
    }

    private static void ValidateValue(PerformanceMeasurement measurement)
    {
        if (string.Equals(measurement.Name, "frequencyHertz", StringComparison.OrdinalIgnoreCase)
            || string.Equals(measurement.Unit, "hertz", StringComparison.OrdinalIgnoreCase))
        {
            if (measurement.Value is < 65 or > 1000)
                throw new ArgumentOutOfRangeException(nameof(measurement), "Corrected frequency must stay between 65 and 1000 Hz.");
            return;
        }

        if (string.Equals(measurement.Unit, "dBFS", StringComparison.OrdinalIgnoreCase))
        {
            if (measurement.Value is < -120 or > 0)
                throw new ArgumentOutOfRangeException(nameof(measurement), "Corrected loudness must stay between −120 and 0 dBFS.");
            return;
        }

        if (string.Equals(measurement.Unit, "normalized", StringComparison.OrdinalIgnoreCase)
            && measurement.Value is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(measurement), "Corrected normalized measurements must stay between 0 and 1.");
    }
}
