using System.Text.Json;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class VocalProfileProposalTests
{
    private static SongProject Project(params VocalProductionDescriptor[] descriptors)
    {
        var project = SongProject.Create("Vocal direction");
        project.SetVocalProductionIntent(new VocalProductionIntent(descriptors, "Keep the breath and quiet words.", DateTimeOffset.UtcNow));
        return project;
    }

    [Fact]
    public void WarmAndIntimate_ExplainAStableDeduplicatedPlanWithoutChangingTheSong()
    {
        var project = Project(VocalProductionDescriptor.Intimate, VocalProductionDescriptor.Warm);
        var before = PortableProjectExporter.SerializeDocument(project);
        var proposal = VocalProfileProposer.Propose(project);
        Assert.Equal(new[] { VocalProcessingRole.CorrectiveTone, VocalProcessingRole.Saturation, VocalProcessingRole.TransparentDynamics, VocalProcessingRole.Space }, proposal.Jobs.Select(job => job.Role));
        Assert.All(proposal.Jobs, job => Assert.NotEmpty(job.Reasons));
        Assert.Equal(VocalProcessingRole.CorrectiveTone, Assert.Single(proposal.Jobs, job => job.CanPreview).Role);
        Assert.Empty(proposal.RemovedRoles);
        Assert.True(proposal.HasChanges);
        Assert.Equal(before, PortableProjectExporter.SerializeDocument(project));
        var reversed = Project(VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate);
        Assert.Equal(JsonSerializer.Serialize(proposal.Jobs), JsonSerializer.Serialize(VocalProfileProposer.Propose(reversed).Jobs));
    }

    [Theory]
    [InlineData(VocalProductionDescriptor.Clean)]
    [InlineData(VocalProductionDescriptor.Warm)]
    [InlineData(VocalProductionDescriptor.Intimate)]
    [InlineData(VocalProductionDescriptor.Forward)]
    [InlineData(VocalProductionDescriptor.SoftRock)]
    [InlineData(VocalProductionDescriptor.Cinematic)]
    [InlineData(VocalProductionDescriptor.Aggressive)]
    public void EveryDirection_ProducesExplainedCatalogJobs(VocalProductionDescriptor descriptor)
    {
        var proposal = VocalProfileProposer.Propose(Project(descriptor));
        Assert.NotEmpty(proposal.Jobs);
        Assert.Equal(proposal.Jobs.Count, proposal.Jobs.Select(job => job.Role).Distinct().Count());
        Assert.All(proposal.Jobs, job => {
            Assert.Contains(VocalProcessingRoleCatalog.Roles, role => role.Id == job.Role && role.Name == job.Name);
            Assert.All(job.Reasons, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
        });
        Assert.DoesNotContain(proposal.Jobs, job => job.Role is VocalProcessingRole.Cleanup or VocalProcessingRole.SibilanceControl);
    }

    [Fact]
    public void CombinedDirections_RetainDistinctReasonsAndDynamicsJobs()
    {
        var proposal = VocalProfileProposer.Propose(Project(VocalProductionDescriptor.Clean, VocalProductionDescriptor.Forward, VocalProductionDescriptor.Aggressive, VocalProductionDescriptor.Cinematic));
        Assert.Equal(3, proposal.Jobs.Single(job => job.Role == VocalProcessingRole.TransparentDynamics).Reasons.Count);
        Assert.Contains(proposal.Jobs, job => job.Role == VocalProcessingRole.CharacterCompression);
        Assert.Contains(proposal.Jobs.Single(job => job.Role == VocalProcessingRole.Space).Reasons, reason => reason.Contains("intimacy"));
    }

    [Fact]
    public void Proposal_ReportsRemovedAddedAndReorderedJobs()
    {
        var project = Project(VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate);
        project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.Space, VocalProcessingRole.CorrectiveTone, VocalProcessingRole.Cleanup], DateTimeOffset.UtcNow));
        var proposal = VocalProfileProposer.Propose(project);
        Assert.Equal(new[] { VocalProcessingRole.Cleanup }, proposal.RemovedRoles);
        Assert.Equal(new[] { VocalProcessingRole.Saturation, VocalProcessingRole.TransparentDynamics }, proposal.AddedRoles);
        Assert.True(proposal.OrderChanges);
        Assert.Equal(project.VocalProcessingChain!.Roles, proposal.CurrentRoles);
    }

    [Fact]
    public void AcceptedSettingsAndArtistIntent_SurvivePlanAcceptanceUndoAndRedo()
    {
        var project = Project(VocalProductionDescriptor.Aggressive);
        project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.CorrectiveTone, VocalProcessingRole.Cleanup], DateTimeOffset.UtcNow));
        var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", 1, new string('a', 64), DateTimeOffset.UtcNow, "Original");
        project.RegisterAsset(asset);
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var recipe = Assert.Single(project.VocalProcessingRecipes);
        var intent = project.VocalProductionIntent;
        var chain = project.VocalProcessingChain;
        var proposal = VocalProfileProposer.Propose(project);
        Assert.Contains(proposal.Jobs.Single(job => job.Role == VocalProcessingRole.CorrectiveTone).Reasons, reason => reason.Contains("already has accepted"));
        editor.Execute(new AcceptVocalProfileProposalCommand(proposal.SourceSignature));
        var accepted = project.VocalProcessingChain;
        Assert.Equal(proposal.Jobs.Select(job => job.Role), accepted!.Roles);
        Assert.Same(intent, project.VocalProductionIntent);
        Assert.Same(recipe, Assert.Single(project.VocalProcessingRecipes));
        Assert.Same(asset, Assert.Single(project.Assets));
        editor.Undo();
        Assert.Same(chain, project.VocalProcessingChain);
        editor.Redo();
        Assert.Same(accepted, project.VocalProcessingChain);
        Assert.Same(recipe, Assert.Single(project.VocalProcessingRecipes));
    }

    [Fact]
    public void MissingDirectionStaleProposalAndUnchangedPlan_DoNotCreateHistory()
    {
        Assert.Throws<InvalidOperationException>(() => VocalProfileProposer.Propose(SongProject.Create("No direction")));
        var project = Project(VocalProductionDescriptor.Warm);
        var proposal = VocalProfileProposer.Propose(project);
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Clean], "New intention", DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        Assert.Throws<InvalidOperationException>(() => editor.Execute(new AcceptVocalProfileProposalCommand(proposal.SourceSignature)));
        Assert.Null(project.VocalProcessingChain);
        Assert.False(editor.CanUndo);
        var fresh = VocalProfileProposer.Propose(project);
        editor.Execute(new AcceptVocalProfileProposalCommand(fresh.SourceSignature));
        var unchanged = VocalProfileProposer.Propose(project);
        Assert.False(unchanged.HasChanges);
        Assert.False(unchanged.OrderChanges);
        Assert.Throws<InvalidOperationException>(() => editor.Execute(new AcceptVocalProfileProposalCommand(unchanged.SourceSignature)));
        editor.Undo();
        Assert.Null(project.VocalProcessingChain);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void AcceptedPlan_UsesExistingPortableDataWithoutPersistingTheProposal()
    {
        var project = Project(VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate);
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalProfileProposalCommand(VocalProfileProposer.Propose(project).SourceSignature));
        var bytes = PortableProjectExporter.SerializeDocument(project);
        var imported = PortableProjectImporter.Inspect(System.Text.Encoding.UTF8.GetString(bytes)).Project;
        Assert.Equal(project.VocalProcessingChain!.Roles, imported.VocalProcessingChain!.Roles);
        Assert.Empty(imported.VocalProcessingRecipes);
        Assert.DoesNotContain("vocalProfileProposal", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void EditorTimestampRefresh_DoesNotInvalidateUnchangedProposalInputs()
    {
        var project = Project(VocalProductionDescriptor.Warm);
        var proposal = VocalProfileProposer.Propose(project);
        project.Touch(); // Workspace synchronization refreshes this on every command.
        new ProjectEditor(project).Execute(new AcceptVocalProfileProposalCommand(proposal.SourceSignature));
        Assert.Equal(proposal.Jobs.Select(job => job.Role), project.VocalProcessingChain!.Roles);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChangedPlanOrTake_RejectsThePreviousProposal(bool changePlan)
    {
        var project = Project(VocalProductionDescriptor.Warm);
        var proposal = VocalProfileProposer.Propose(project);
        if (changePlan) project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.Space], DateTimeOffset.UtcNow));
        else project.RegisterAsset(new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", 1, new string('a', 64), DateTimeOffset.UtcNow, "New take"));
        var editor = new ProjectEditor(project);
        Assert.Throws<InvalidOperationException>(() => editor.Execute(new AcceptVocalProfileProposalCommand(proposal.SourceSignature)));
        Assert.False(editor.CanUndo);
    }
}
