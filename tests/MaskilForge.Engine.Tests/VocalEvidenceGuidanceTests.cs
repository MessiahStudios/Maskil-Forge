using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class VocalEvidenceGuidanceTests
{
    private static (SongProject Project, ProjectAsset Asset) Fixture()
    {
        var project = SongProject.Create("Reviewed dynamics");
        var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", 1, new string('a', 64), DateTimeOffset.UtcNow, "Original");
        project.RegisterAsset(asset);
        return (project, asset);
    }
    private static PerformanceObservation Frame(SongProject project, ProjectAsset asset, long start, decimal rms,
        bool reviewed = true, string analyzer = "maskil.browser.loudness", string version = "1.0.0",
        PerformanceObservationProvenance provenance = PerformanceObservationProvenance.DeterministicAnalyzer)
    {
        var observation = new PerformanceObservation(PerformanceObservationId.New(), asset.Id, "loudness.frame", start, 250,
            [new("rmsDbfs", rms, "dBFS"), new("peakDbfs", 0, "dBFS")], null, analyzer, version, provenance, DateTimeOffset.UtcNow);
        project.RegisterPerformanceObservation(observation);
        if (reviewed) project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, DateTimeOffset.UtcNow);
        return observation;
    }

    [Theory]
    [InlineData(-22, true)]
    [InlineData(-21.99, false)]
    public void ReviewedRange_UsesTheExplicitThresholdWithoutMutatingTheSong(double low, bool suggested)
    {
        var (project, asset) = Fixture();
        Frame(project, asset, 0, (decimal)low); Frame(project, asset, 250, -15); Frame(project, asset, 500, -10);
        var before = PortableProjectExporter.SerializeDocument(project);
        var guidance = VocalEvidenceAdvisor.Preview(project, asset.Id);
        Assert.Equal(suggested, guidance.SuggestLevelControl);
        Assert.Equal(suggested, guidance.HasChanges);
        Assert.Equal(-10m - (decimal)low, guidance.SpreadDecibels);
        Assert.Equal(3, guidance.Evidence.Count);
        Assert.Equal(before, PortableProjectExporter.SerializeDocument(project));
    }

    [Theory]
    [InlineData("unreviewed")]
    [InlineData("inaccurate")]
    [InlineData("quiet")]
    [InlineData("model")]
    [InlineData("imported")]
    [InlineData("version")]
    [InlineData("analyzer")]
    [InlineData("duplicate-time")]
    [InlineData("off-grid")]
    public void IneligibleEvidence_CannotSupplyTheThirdSupportingFrame(string scenario)
    {
        var (project, asset) = Fixture();
        Frame(project, asset, 0, -15); Frame(project, asset, 250, -10);
        var third = Frame(project, asset, scenario == "off-grid" ? 501 : 500, scenario == "quiet" ? -60 : -40,
            reviewed: scenario != "unreviewed", analyzer: scenario == "analyzer" ? "unknown" : "maskil.browser.loudness",
            version: scenario == "version" ? "2.0.0" : "1.0.0",
            provenance: scenario == "model" ? PerformanceObservationProvenance.AudioModel : scenario == "imported" ? PerformanceObservationProvenance.ImportedAnalyzer : PerformanceObservationProvenance.DeterministicAnalyzer);
        if (scenario == "inaccurate") project.SetPerformanceObservationReview(third.Id, PerformanceObservationReviewVerdict.Inaccurate, DateTimeOffset.UtcNow);
        if (scenario == "duplicate-time") Frame(project, asset, 500, -30);
        var guidance = VocalEvidenceAdvisor.Preview(project, asset.Id);
        Assert.False(guidance.SuggestLevelControl);
        Assert.Null(guidance.SpreadDecibels);
        Assert.Equal(2, guidance.Evidence.Count);
    }

    [Fact]
    public void ArtistCorrection_SuppliesEffectiveValueAndKeepsOriginalAttribution()
    {
        var (project, asset) = Fixture();
        var original = Frame(project, asset, 0, -12);
        Frame(project, asset, 250, -15); Frame(project, asset, 500, -10);
        project.SetPerformanceObservationReview(original.Id, PerformanceObservationReviewVerdict.Inaccurate, DateTimeOffset.UtcNow);
        project.SetPerformanceObservationCorrection(original.Id, [new("rmsDbfs", -30, "dBFS"), new("peakDbfs", 0, "dBFS")], DateTimeOffset.UtcNow);
        var guidance = VocalEvidenceAdvisor.Preview(project, asset.Id);
        var evidence = guidance.Evidence.Single(item => item.ObservationId == original.Id);
        Assert.True(evidence.ArtistCorrected);
        Assert.Equal(-12, evidence.OriginalRmsDbfs);
        Assert.Equal(-30, evidence.RmsDbfs);
        Assert.Equal(original.AnalyzerId, evidence.AnalyzerId);
        Assert.Equal(20, guidance.SpreadDecibels);
        Assert.Equal(-12, original.Measurements[0].Value);
    }

    [Fact]
    public void Guidance_IsScopedToOneTakeAndRequiresAnOriginalAsset()
    {
        var (project, asset) = Fixture();
        var other = new ProjectAsset(ProjectAssetId.New(), asset.Kind, asset.MediaType, 1, asset.Sha256, asset.CreatedUtc, "Other");
        project.RegisterAsset(other);
        Frame(project, asset, 0, -30); Frame(project, asset, 250, -10); Frame(project, other, 500, -20);
        Assert.False(VocalEvidenceAdvisor.Preview(project, asset.Id).SuggestLevelControl);
        Assert.Throws<ArgumentException>(() => VocalEvidenceAdvisor.Preview(project, ProjectAssetId.New()));
    }

    [Fact]
    public void Acceptance_AppendsOneJobAndUndoRestoresTheExactPlanAndRecipe()
    {
        var (project, asset) = Fixture();
        Frame(project, asset, 0, -30); Frame(project, asset, 250, -15); Frame(project, asset, 500, -10);
        project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.Space, VocalProcessingRole.CorrectiveTone], DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256, 140, .9));
        var recipe = Assert.Single(project.VocalProcessingRecipes);
        var oldPlan = project.VocalProcessingChain;
        var guidance = VocalEvidenceAdvisor.Preview(project, asset.Id);
        project.Touch(); // Normal workspace synchronization must not invalidate evidence.
        editor.Execute(new AcceptVocalEvidenceGuidanceCommand(asset.Id, guidance.SourceSignature));
        var plan = project.VocalProcessingChain;
        Assert.Equal(new[] { VocalProcessingRole.Space, VocalProcessingRole.CorrectiveTone, VocalProcessingRole.TransparentDynamics }, plan!.Roles);
        Assert.Same(recipe, Assert.Single(project.VocalProcessingRecipes));
        Assert.Same(asset, Assert.Single(project.Assets));
        Assert.False(VocalEvidenceAdvisor.Preview(project, asset.Id).HasChanges);
        editor.Undo(); Assert.Same(oldPlan, project.VocalProcessingChain);
        editor.Redo(); Assert.Same(plan, project.VocalProcessingChain);
        Assert.Same(recipe, Assert.Single(project.VocalProcessingRecipes));
    }

    [Theory]
    [InlineData("review")]
    [InlineData("correction")]
    [InlineData("observation")]
    [InlineData("plan")]
    public void ChangedInputs_RejectStaleGuidanceWithoutAddingHistory(string change)
    {
        var (project, asset) = Fixture();
        var first = Frame(project, asset, 0, -30); Frame(project, asset, 250, -15); Frame(project, asset, 500, -10);
        var guidance = VocalEvidenceAdvisor.Preview(project, asset.Id);
        if (change is "review" or "correction") project.SetPerformanceObservationReview(first.Id, PerformanceObservationReviewVerdict.Inaccurate, DateTimeOffset.UtcNow);
        if (change == "correction") project.SetPerformanceObservationCorrection(first.Id, [new("rmsDbfs", -35, "dBFS"), new("peakDbfs", 0, "dBFS")], DateTimeOffset.UtcNow);
        if (change == "observation") Frame(project, asset, 750, -35);
        if (change == "plan") project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.Space], DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        Assert.Throws<InvalidOperationException>(() => editor.Execute(new AcceptVocalEvidenceGuidanceCommand(asset.Id, guidance.SourceSignature)));
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void NoSuggestion_CannotBeAccepted()
    {
        var (project, asset) = Fixture();
        var guidance = VocalEvidenceAdvisor.Preview(project, asset.Id);
        var editor = new ProjectEditor(project);
        Assert.Throws<InvalidOperationException>(() => editor.Execute(new AcceptVocalEvidenceGuidanceCommand(asset.Id, guidance.SourceSignature)));
        Assert.False(editor.CanUndo);
        Assert.Null(project.VocalProcessingChain);
    }
}
