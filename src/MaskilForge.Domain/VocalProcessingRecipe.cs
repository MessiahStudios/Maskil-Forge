namespace MaskilForge.Domain;

/// <summary>A reviewed realization of Corrective Tone on one immutable original take.</summary>
public sealed record VocalProcessingRecipe
{
    public const string LowCutProcessorId = "maskil.vocal.low-cut.v1";
    public const double LowCutHertz = 80;
    public const double LowCutQ = 0.7071067811865476;

    public VocalProcessingRecipe(ProjectAssetId assetId, string sourceSha256, string processorId,
        VocalProcessingRole role, double cutoffHertz, double q, DateTimeOffset acceptedUtc)
    {
        if (assetId.Value == Guid.Empty) throw new ArgumentException("A source take is required.", nameof(assetId));
        if (string.IsNullOrWhiteSpace(sourceSha256) || sourceSha256.Length != 64 || sourceSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A valid source digest is required.", nameof(sourceSha256));
        if (processorId != LowCutProcessorId || role != VocalProcessingRole.CorrectiveTone || cutoffHertz != LowCutHertz || q != LowCutQ)
            throw new ArgumentException("Only the fixed 80 Hz low-cut processor is supported.", nameof(processorId));
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
}
