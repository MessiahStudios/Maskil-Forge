using System.Security.Cryptography;
using System.Text.Json;
using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record VocalLoudnessEvidence(PerformanceObservationId ObservationId, long StartMilliseconds,
    long DurationMilliseconds, decimal OriginalRmsDbfs, decimal RmsDbfs, bool ArtistCorrected,
    string AnalyzerId, string AnalyzerVersion, PerformanceObservationProvenance Provenance, decimal? Confidence);
public sealed record VocalEvidenceGuidance(string SourceSignature, ProjectAssetId AssetId,
    IReadOnlyList<VocalLoudnessEvidence> Evidence, decimal? SpreadDecibels, bool SuggestLevelControl,
    bool HasChanges, IReadOnlyList<VocalProcessingRole> CurrentRoles, IReadOnlyList<VocalProcessingRole> ProposedRoles);

public static class VocalEvidenceAdvisor
{
    public const int Version = 1;
    public const decimal SpreadThresholdDecibels = 12;

    public static string SourceSignature(SongProject project, ProjectAssetId assetId)
    {
        var observations = project.PerformanceObservations.Where(item => item.SourceAssetId == assetId).ToArray();
        var ids = observations.Select(item => item.Id).ToHashSet();
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version, Context = VocalProfileProposer.SourceSignature(project), assetId, observations,
            Reviews = project.PerformanceObservationReviews.Where(item => ids.Contains(item.ObservationId)),
            Corrections = project.PerformanceObservationCorrections.Where(item => ids.Contains(item.ObservationId))
        })));
    }

    public static VocalEvidenceGuidance Preview(SongProject project, ProjectAssetId assetId)
    {
        if (!project.Assets.Any(item => item.Id == assetId && item.Kind == ProjectAssetKind.OriginalVocalTake))
            throw new ArgumentException("Choose an original vocal take.", nameof(assetId));
        var reviews = project.PerformanceObservationReviews.ToDictionary(item => item.ObservationId);
        var corrections = project.PerformanceObservationCorrections.ToDictionary(item => item.ObservationId);
        var evidence = new List<VocalLoudnessEvidence>();
        // Only the known deterministic loudness contract is interpreted. Other evidence remains inspectable elsewhere.
        var candidates = project.PerformanceObservations.Where(item => item.SourceAssetId == assetId
            && item.Kind == "loudness.frame" && item.AnalyzerId == "maskil.browser.loudness" && item.AnalyzerVersion == "1.0.0"
            && item.Provenance == PerformanceObservationProvenance.DeterministicAnalyzer
            && item.StartMilliseconds < 60_000 && item.StartMilliseconds % 250 == 0
            && item.DurationMilliseconds is > 0 and <= 250 && item.StartMilliseconds + item.DurationMilliseconds <= 60_000)
            .GroupBy(item => item.StartMilliseconds).Where(group => group.Count() == 1).Select(group => group.Single())
            .OrderBy(item => item.StartMilliseconds);
        foreach (var observation in candidates)
        {
            if (!reviews.TryGetValue(observation.Id, out var review)) continue;
            var corrected = review.Verdict == PerformanceObservationReviewVerdict.Inaccurate;
            if (corrected && !corrections.ContainsKey(observation.Id)) continue;
            var measurements = corrected ? corrections[observation.Id].Measurements : observation.Measurements;
            var original = observation.Measurements.SingleOrDefault(item => item.Name == "rmsDbfs" && item.Unit == "dBFS");
            var rms = measurements.SingleOrDefault(item => item.Name == "rmsDbfs" && item.Unit == "dBFS");
            var peak = measurements.SingleOrDefault(item => item.Name == "peakDbfs" && item.Unit == "dBFS");
            if (original is null || original.Value is < -120 or > 0 || rms is null || peak is null
                || rms.Value is <= -60 or > 0 || peak.Value is < -120 or > 0 || rms.Value > peak.Value) continue;
            evidence.Add(new(observation.Id, observation.StartMilliseconds, observation.DurationMilliseconds,
                original.Value, rms.Value, corrected, observation.AnalyzerId, observation.AnalyzerVersion, observation.Provenance, observation.Confidence));
        }
        decimal? spread = evidence.Count >= 3 ? evidence.Max(item => item.RmsDbfs) - evidence.Min(item => item.RmsDbfs) : null;
        var suggest = spread >= SpreadThresholdDecibels;
        var current = project.VocalProcessingChain?.Roles.ToArray() ?? [];
        var changes = suggest && !current.Contains(VocalProcessingRole.TransparentDynamics);
        var proposed = changes ? current.Append(VocalProcessingRole.TransparentDynamics).ToArray() : current;
        return new(SourceSignature(project, assetId), assetId, evidence.AsReadOnly(), spread, suggest, changes,
            Array.AsReadOnly(current), Array.AsReadOnly(proposed));
    }
}

public sealed class AcceptVocalEvidenceGuidanceCommand(ProjectAssetId assetId, string sourceSignature) : IProjectCommand
{
    private SetVocalProcessingChainCommand? _change;
    public void Execute(SongProject project)
    {
        if (_change is not null) { _change.Execute(project); return; }
        if (VocalEvidenceAdvisor.SourceSignature(project, assetId) != sourceSignature)
            throw new InvalidOperationException("The take, reviews, or production plan changed. Preview the guidance again.");
        var guidance = VocalEvidenceAdvisor.Preview(project, assetId);
        if (!guidance.HasChanges) throw new InvalidOperationException("There is no new production job to accept.");
        var change = new SetVocalProcessingChainCommand(guidance.ProposedRoles);
        change.Execute(project);
        _change = change;
    }
    public void Undo(SongProject project) => (_change ?? throw new InvalidOperationException("Command has not been executed.")).Undo(project);
}
