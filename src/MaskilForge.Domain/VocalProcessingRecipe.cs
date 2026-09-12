namespace MaskilForge.Domain;

/// <summary>A reviewed realization of Corrective Tone on one immutable original take.</summary>
public sealed record VocalProcessingRecipe
{
    public const string LowCutProcessorId = "maskil.vocal.low-cut.v1";
    public const string AdjustableLowCutProcessorId = "maskil.vocal.low-cut.v2";
    public const double LowCutHertz = 80;
    public const double LowCutQ = 0.7071067811865476;

    public VocalProcessingRecipe(ProjectAssetId assetId, string sourceSha256, string processorId,
        VocalProcessingRole role, double cutoffHertz, double q, DateTimeOffset acceptedUtc)
    {
        if (assetId.Value == Guid.Empty) throw new ArgumentException("A source take is required.", nameof(assetId));
        if (string.IsNullOrWhiteSpace(sourceSha256) || sourceSha256.Length != 64 || sourceSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A valid source digest is required.", nameof(sourceSha256));
        if (role != VocalProcessingRole.CorrectiveTone ||
            (processorId != LowCutProcessorId && processorId != AdjustableLowCutProcessorId))
            throw new ArgumentException("Choose a supported Corrective Tone processor.", nameof(processorId));
        if (processorId == LowCutProcessorId && (cutoffHertz != LowCutHertz || q != LowCutQ))
            throw new ArgumentException("Version 1 preserves its fixed 80 Hz low-cut settings.", nameof(processorId));
        ValidateLowCutSettings(cutoffHertz, q);
        if (acceptedUtc == default) throw new ArgumentException("An acceptance time is required.", nameof(acceptedUtc));
        AssetId = assetId;
        SourceSha256 = sourceSha256.ToLowerInvariant();
        ProcessorId = processorId;
        Role = role;
        CutoffHertz = cutoffHertz;
        Q = q;
        AcceptedUtc = acceptedUtc;
    }

    public ProjectAssetId AssetId { get; }
    public string SourceSha256 { get; }
    public string ProcessorId { get; }
    public VocalProcessingRole Role { get; }
    public double CutoffHertz { get; }
    public double Q { get; }
    public DateTimeOffset AcceptedUtc { get; }

    public static void ValidateLowCutSettings(double cutoffHertz, double q)
    {
        if (!double.IsFinite(cutoffHertz) || cutoffHertz < 40 || cutoffHertz > 200 || cutoffHertz != Math.Truncate(cutoffHertz))
            throw new ArgumentException("Low-cut frequency must be a whole number from 40 to 200 Hz.", nameof(cutoffHertz));
        if (!double.IsFinite(q) || q < 0.5 || q > 1)
            throw new ArgumentException("Low-cut Q must be from 0.5 to 1.", nameof(q));
    }
}
