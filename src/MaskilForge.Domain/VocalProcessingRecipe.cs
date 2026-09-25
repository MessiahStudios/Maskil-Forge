using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

/// <summary>A reviewed realization of one production role on one immutable original take.</summary>
public sealed record VocalProcessingRecipe
{
    public const string LowCutProcessorId = "maskil.vocal.low-cut.v1";
    public const string AdjustableLowCutProcessorId = "maskil.vocal.low-cut.v2";
    public const string LevelControlProcessorId = "maskil.vocal.level-control.v1";
    public const string SaturationProcessorId = "maskil.vocal.saturation.v1";
    public const string CharacterCompressionProcessorId = "maskil.vocal.character-compression.v1";
    public const string CleanupProcessorId = "maskil.vocal.cleanup.v1";
    public const double LowCutHertz = 80;
    public const double LowCutQ = 0.7071067811865476;
    public const double LevelThresholdDecibels = -18;
    public const double LevelRatio = 2;
    public const double LevelAttackMilliseconds = 20;
    public const double LevelReleaseMilliseconds = 120;
    public const double SaturationColorAmount = 0.15;
    public const double CharacterThresholdDecibels = -24;
    public const double CharacterRatio = 4;
    public const double CharacterAttackMilliseconds = 0;
    public const double CharacterReleaseMilliseconds = 250;
    public const double CleanupThresholdDecibels = -40;
    public const double CleanupRatio = 4;
    public const double CleanupAttackMilliseconds = 10;
    public const double CleanupReleaseMilliseconds = 200;

    public VocalProcessingRecipe(ProjectAssetId assetId, string sourceSha256, string processorId,
        VocalProcessingRole role, double? cutoffHertz, double? q, DateTimeOffset acceptedUtc,
        double? thresholdDecibels = null, double? ratio = null, double? attackMilliseconds = null, double? releaseMilliseconds = null,
        double? colorAmount = null)
    {
        if (assetId.Value == Guid.Empty) throw new ArgumentException("A source take is required.", nameof(assetId));
        if (string.IsNullOrWhiteSpace(sourceSha256) || sourceSha256.Length != 64 || sourceSha256.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("A valid source digest is required.", nameof(sourceSha256));
        if (acceptedUtc == default) throw new ArgumentException("An acceptance time is required.", nameof(acceptedUtc));
        if (role == VocalProcessingRole.CorrectiveTone)
        {
            if (processorId != LowCutProcessorId && processorId != AdjustableLowCutProcessorId)
                throw new ArgumentException("Choose a supported Corrective Tone processor.", nameof(processorId));
            if (thresholdDecibels is not null || ratio is not null || attackMilliseconds is not null || releaseMilliseconds is not null || colorAmount is not null)
                throw new ArgumentException("Low-cut settings do not include level-control or saturation parameters.", nameof(processorId));
            if (cutoffHertz is null || q is null)
                throw new ArgumentException("Low-cut frequency and Q are required.", nameof(cutoffHertz));
            if (processorId == LowCutProcessorId && (cutoffHertz != LowCutHertz || q != LowCutQ))
                throw new ArgumentException("Version 1 preserves its fixed 80 Hz low-cut settings.", nameof(processorId));
            ValidateLowCutSettings(cutoffHertz.Value, q.Value);
        }
        else if (role == VocalProcessingRole.TransparentDynamics)
        {
            if (processorId != LevelControlProcessorId)
                throw new ArgumentException("Choose the supported Transparent Level Control processor.", nameof(processorId));
            if (cutoffHertz is not null || q is not null || colorAmount is not null)
                throw new ArgumentException("Level-control settings do not include low-cut or saturation parameters.", nameof(processorId));
            if (thresholdDecibels != LevelThresholdDecibels || ratio != LevelRatio ||
                attackMilliseconds != LevelAttackMilliseconds || releaseMilliseconds != LevelReleaseMilliseconds)
                throw new ArgumentException("Version 1 preserves its fixed level-control starting point.", nameof(processorId));
        }
        else if (role == VocalProcessingRole.Saturation)
        {
            if (processorId != SaturationProcessorId)
                throw new ArgumentException("Choose the supported Saturation processor.", nameof(processorId));
            if (cutoffHertz is not null || q is not null || thresholdDecibels is not null || ratio is not null ||
                attackMilliseconds is not null || releaseMilliseconds is not null)
                throw new ArgumentException("Saturation settings do not include low-cut or level-control parameters.", nameof(processorId));
            if (colorAmount != SaturationColorAmount)
                throw new ArgumentException("Version 1 preserves its fixed saturation starting point.", nameof(colorAmount));
        }
        else if (role == VocalProcessingRole.CharacterCompression)
        {
            if (processorId != CharacterCompressionProcessorId)
                throw new ArgumentException("Choose the supported Character Compression processor.", nameof(processorId));
            if (cutoffHertz is not null || q is not null || colorAmount is not null)
                throw new ArgumentException("Character-compression settings do not include low-cut or saturation parameters.", nameof(processorId));
            if (thresholdDecibels != CharacterThresholdDecibels || ratio != CharacterRatio ||
                attackMilliseconds != CharacterAttackMilliseconds || releaseMilliseconds != CharacterReleaseMilliseconds)
                throw new ArgumentException("Version 1 preserves its fixed character-compression starting point.", nameof(processorId));
        }
        else if (role == VocalProcessingRole.Cleanup)
        {
            if (processorId != CleanupProcessorId)
                throw new ArgumentException("Choose the supported Cleanup processor.", nameof(processorId));
            if (cutoffHertz is not null || q is not null || colorAmount is not null)
                throw new ArgumentException("Cleanup settings do not include low-cut or saturation parameters.", nameof(processorId));
            if (thresholdDecibels != CleanupThresholdDecibels || ratio != CleanupRatio ||
                attackMilliseconds != CleanupAttackMilliseconds || releaseMilliseconds != CleanupReleaseMilliseconds)
                throw new ArgumentException("Version 1 preserves its fixed cleanup starting point.", nameof(processorId));
        }
        else throw new ArgumentException("Choose a production role that has a built-in processor.", nameof(role));
        AssetId = assetId;
        SourceSha256 = sourceSha256.ToLowerInvariant();
        ProcessorId = processorId;
        Role = role;
        CutoffHertz = cutoffHertz;
        Q = q;
        ThresholdDecibels = thresholdDecibels;
        Ratio = ratio;
        AttackMilliseconds = attackMilliseconds;
        ReleaseMilliseconds = releaseMilliseconds;
        ColorAmount = colorAmount;
        AcceptedUtc = acceptedUtc;
    }

    public static VocalProcessingRecipe FixedSaturation(ProjectAssetId assetId, string sourceSha256, DateTimeOffset acceptedUtc) =>
        new(assetId, sourceSha256, SaturationProcessorId, VocalProcessingRole.Saturation, null, null, acceptedUtc,
            colorAmount: SaturationColorAmount);

    public ProjectAssetId AssetId { get; }
    public string SourceSha256 { get; }
    public string ProcessorId { get; }
    public VocalProcessingRole Role { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CutoffHertz { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Q { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ThresholdDecibels { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Ratio { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AttackMilliseconds { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ReleaseMilliseconds { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ColorAmount { get; }
    public DateTimeOffset AcceptedUtc { get; }

    public static VocalProcessingRecipe FixedCleanup(ProjectAssetId assetId, string sourceSha256, DateTimeOffset acceptedUtc) =>
        new(assetId, sourceSha256, CleanupProcessorId, VocalProcessingRole.Cleanup, null, null, acceptedUtc,
            CleanupThresholdDecibels, CleanupRatio, CleanupAttackMilliseconds, CleanupReleaseMilliseconds);

    public static VocalProcessingRecipe FixedCharacterCompression(ProjectAssetId assetId, string sourceSha256, DateTimeOffset acceptedUtc) =>
        new(assetId, sourceSha256, CharacterCompressionProcessorId, VocalProcessingRole.CharacterCompression, null, null, acceptedUtc,
            CharacterThresholdDecibels, CharacterRatio, CharacterAttackMilliseconds, CharacterReleaseMilliseconds);

    public static VocalProcessingRecipe FixedLevelControl(ProjectAssetId assetId, string sourceSha256, DateTimeOffset acceptedUtc) =>
        new(assetId, sourceSha256, LevelControlProcessorId, VocalProcessingRole.TransparentDynamics, null, null, acceptedUtc,
            LevelThresholdDecibels, LevelRatio, LevelAttackMilliseconds, LevelReleaseMilliseconds);

    public static void ValidateLowCutSettings(double cutoffHertz, double q)
    {
        if (!double.IsFinite(cutoffHertz) || cutoffHertz < 40 || cutoffHertz > 200 || cutoffHertz != Math.Truncate(cutoffHertz))
            throw new ArgumentException("Low-cut frequency must be a whole number from 40 to 200 Hz.", nameof(cutoffHertz));
        if (!double.IsFinite(q) || q < 0.5 || q > 1)
            throw new ArgumentException("Low-cut Q must be from 0.5 to 1.", nameof(q));
    }
}
