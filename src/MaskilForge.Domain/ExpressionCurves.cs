using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum ExpressionCurveKind
{
    Dynamics
}

public sealed record ExpressionCurvePoint
{
    [JsonConstructor]
    public ExpressionCurvePoint(long tick, int value)
    {
        if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), "An expression point cannot start before tick zero.");
        if (value is < 0 or > 127) throw new ArgumentOutOfRangeException(nameof(value), "An expression value must be between 0 and 127.");
        Tick = tick;
        Value = value;
    }

    public long Tick { get; }
    public int Value { get; }
}

/// <summary>
/// An artist-authored dynamics curve in absolute song time. It may name a catalog
/// instrument. MIDI export may translate Dynamics to that instrument's inspectable
/// controller, or to Expression (CC 11) when the curve is untagged.
/// </summary>
public sealed class ExpressionCurve
{
    public const int MaximumPointCount = 240;

    [JsonConstructor]
    public ExpressionCurve(
        ExpressionCurveId id,
        string name,
        ExpressionCurveKind kind,
        IReadOnlyList<ExpressionCurvePoint> points,
        string? instrumentProfileId = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("An expression-curve ID is required.", nameof(id));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind), "Expression-curve kind is invalid.");
        Name = NormalizeName(name);
        Kind = kind;
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0) throw new ArgumentException("An expression curve must contain at least one point.", nameof(points));
        if (points.Count > MaximumPointCount)
            throw new ArgumentOutOfRangeException(nameof(points), $"An expression curve cannot exceed {MaximumPointCount} points.");
        if (points.Select(item => item.Tick).Distinct().Count() != points.Count)
            throw new ArgumentException("Expression-curve point ticks must be unique.", nameof(points));
        if (string.IsNullOrWhiteSpace(instrumentProfileId)) instrumentProfileId = null;
        else
        {
            instrumentProfileId = instrumentProfileId.Trim();
            if (!InstrumentProfile.IsValidId(instrumentProfileId))
                throw new ArgumentException("An assigned instrument must be a catalog slug of at most 40 characters.", nameof(instrumentProfileId));
        }

        Id = id;
        Points = points.OrderBy(item => item.Tick).ToList();
        InstrumentProfileId = instrumentProfileId;
    }

    public ExpressionCurveId Id { get; }
    public string Name { get; }
    public ExpressionCurveKind Kind { get; }
    public IReadOnlyList<ExpressionCurvePoint> Points { get; }
    public string? InstrumentProfileId { get; }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An expression-curve name is required.", nameof(name));
        var normalized = name.Trim();
        if (normalized.Length > 80) throw new ArgumentOutOfRangeException(nameof(name), "An expression-curve name cannot exceed 80 characters.");
        return normalized;
    }
}
