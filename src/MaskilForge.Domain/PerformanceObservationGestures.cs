using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public sealed record PerformanceObservationGesture
{
    [JsonConstructor]
    public PerformanceObservationGesture(
        PerformanceObservationGestureId id,
        PerformanceObservationId observationId,
        IReadOnlyList<PerformanceMeasurement> measurements,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A performance observation gesture ID is required.", nameof(id));
        if (observationId.Value == Guid.Empty) throw new ArgumentException("A gestured observation ID is required.", nameof(observationId));
        if (createdUtc == default) throw new ArgumentException("A gesture creation time is required.", nameof(createdUtc));
        if (updatedUtc == default) throw new ArgumentException("A gesture update time is required.", nameof(updatedUtc));
        if (updatedUtc < createdUtc) throw new ArgumentOutOfRangeException(nameof(updatedUtc), "A gesture cannot be updated before it was created.");

        Id = id;
        ObservationId = observationId;
        Measurements = measurements?.ToList() ?? throw new ArgumentNullException(nameof(measurements));
        if (Measurements.Count == 0) throw new ArgumentException("A gesture must contain at least one measurement.", nameof(measurements));
        if (Measurements.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Measurements.Count)
            throw new ArgumentException("Gesture measurement names must be unique within a gesture.", nameof(measurements));
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
    }

    public PerformanceObservationGestureId Id { get; }
    public PerformanceObservationId ObservationId { get; }
    public IReadOnlyList<PerformanceMeasurement> Measurements { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; }

    public PerformanceObservationGesture Revise(IReadOnlyList<PerformanceMeasurement> measurements, DateTimeOffset updatedUtc) =>
        new(Id, ObservationId, measurements, CreatedUtc, updatedUtc);

    public static void ValidateAgainst(PerformanceObservation observation, IReadOnlyList<PerformanceMeasurement> measurements)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(measurements);
        if (measurements.Count != observation.Measurements.Count)
            throw new ArgumentException("A gesture must copy every original measurement and cannot add new ones.", nameof(measurements));

        var originals = observation.Measurements.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var measurement in measurements)
        {
            if (!originals.TryGetValue(measurement.Name, out var original))
                throw new ArgumentException($"A gesture cannot introduce the measurement '{measurement.Name}'.", nameof(measurements));
            if (!string.Equals(measurement.Unit, original.Unit, StringComparison.Ordinal))
                throw new ArgumentException($"Gesture measurement '{measurement.Name}' must keep the original unit '{original.Unit}'.", nameof(measurements));
        }
    }

    public static bool ValuesEqual(IReadOnlyList<PerformanceMeasurement> left, IReadOnlyList<PerformanceMeasurement> right)
    {
        if (left.Count != right.Count) return false;
        var byName = right.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        return left.All(item => byName.TryGetValue(item.Name, out var match)
            && string.Equals(item.Unit, match.Unit, StringComparison.Ordinal)
            && item.Value == match.Value);
    }
}
