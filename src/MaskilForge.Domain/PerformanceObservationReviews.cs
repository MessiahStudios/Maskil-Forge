using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum PerformanceObservationReviewVerdict
{
    Accurate,
    Inaccurate
}

public sealed record PerformanceObservationReview
{
    [JsonConstructor]
    public PerformanceObservationReview(
        PerformanceObservationReviewId id,
        PerformanceObservationId observationId,
        PerformanceObservationReviewVerdict verdict,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A performance observation review ID is required.", nameof(id));
        if (observationId.Value == Guid.Empty) throw new ArgumentException("A reviewed observation ID is required.", nameof(observationId));
        if (!Enum.IsDefined(verdict)) throw new ArgumentOutOfRangeException(nameof(verdict), "The observation review verdict is invalid.");
        if (createdUtc == default) throw new ArgumentException("A review creation time is required.", nameof(createdUtc));
        if (updatedUtc == default) throw new ArgumentException("A review update time is required.", nameof(updatedUtc));
        if (updatedUtc < createdUtc) throw new ArgumentOutOfRangeException(nameof(updatedUtc), "A review cannot be updated before it was created.");

        Id = id;
        ObservationId = observationId;
        Verdict = verdict;
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
    }

    public PerformanceObservationReviewId Id { get; }
    public PerformanceObservationId ObservationId { get; }
    public PerformanceObservationReviewVerdict Verdict { get; }
    public DateTimeOffset CreatedUtc { get; }
    public DateTimeOffset UpdatedUtc { get; }

    public PerformanceObservationReview Revise(PerformanceObservationReviewVerdict verdict, DateTimeOffset updatedUtc) =>
        new(Id, ObservationId, verdict, CreatedUtc, updatedUtc);
}
