using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class VocalProcessingRecipeTests
{
    private static readonly byte[] Source = Encoding.UTF8.GetBytes("immutable human performance");

    private static (SongProject Project, ProjectAsset Asset) Fixture()
    {
        var project = SongProject.Create("Low-cut review");
        var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", Source.Length,
            Convert.ToHexString(SHA256.HashData(Source)).ToLowerInvariant(), DateTimeOffset.UtcNow, "Original take");
        project.RegisterAsset(asset);
        project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.CorrectiveTone, VocalProcessingRole.Space], DateTimeOffset.UtcNow));
        return (project, asset);
    }

    [Fact]
    public void AcceptanceAndClear_AreExplicitAndRestoreTheExactRecipe()
    {
        var (project, asset) = Fixture();
        var editor = new ProjectEditor(project);
        Assert.Empty(project.VocalProcessingRecipes);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var recipe = Assert.Single(project.VocalProcessingRecipes);
        Assert.Equal("maskil.vocal.low-cut.v1", recipe.ProcessorId);
        Assert.Equal(VocalProcessingRole.CorrectiveTone, recipe.Role);
        Assert.Equal(80, recipe.CutoffHertz);
        Assert.Equal(VocalProcessingRecipe.LowCutQ, recipe.Q);
        Assert.Equal(asset, Assert.Single(project.Assets));
        editor.Undo();
        Assert.Empty(project.VocalProcessingRecipes);
        editor.Redo();
        Assert.Same(recipe, Assert.Single(project.VocalProcessingRecipes));
        editor.Execute(new ClearVocalProcessingRecipeCommand(asset.Id));
        Assert.Empty(project.VocalProcessingRecipes);
        editor.Undo();
        Assert.Same(recipe, Assert.Single(project.VocalProcessingRecipes));
        editor.Redo();
        Assert.Empty(project.VocalProcessingRecipes);
    }

    [Fact]
    public void InvalidAcceptance_LeavesProjectAndHistoryUntouched()
    {
        var (project, asset) = Fixture();
        var editor = new ProjectEditor(project);
        var revision = project.LastModifiedUtc;
        Assert.Throws<ArgumentException>(() => editor.Execute(new AcceptVocalLowCutCommand(ProjectAssetId.New(), asset.Sha256)));
        Assert.Throws<ArgumentException>(() => editor.Execute(new AcceptVocalLowCutCommand(asset.Id, new string('0', 64))));
        Assert.Equal(revision, project.LastModifiedUtc);
        Assert.False(editor.CanUndo);
        project.ClearVocalProcessingChain();
        Assert.Throws<ArgumentException>(() => editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256)));
        Assert.Empty(project.VocalProcessingRecipes);
    }

    [Fact]
    public void RoleRemoval_RequiresExplicitRecipeClearingButOtherPlanEditsAreAllowed()
    {
        var (project, asset) = Fixture();
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var chain = project.VocalProcessingChain;
        Assert.Throws<InvalidOperationException>(() => project.ClearVocalProcessingChain());
        Assert.Throws<InvalidOperationException>(() => project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.Space], DateTimeOffset.UtcNow)));
        Assert.Same(chain, project.VocalProcessingChain);
        editor.Execute(new SetVocalProcessingChainCommand([VocalProcessingRole.Space, VocalProcessingRole.CorrectiveTone]));
        Assert.Single(project.VocalProcessingRecipes);
        editor.Undo();
        editor.Execute(new ClearVocalProcessingRecipeCommand(asset.Id));
        editor.Execute(new ClearVocalProcessingChainCommand());
        editor.Undo();
        editor.Undo();
        Assert.Single(project.VocalProcessingRecipes);
        Assert.Same(chain, project.VocalProcessingChain);
    }

    [Fact]
    public void RemovingOneTake_RemovesOnlyItsRecipeAndKeepsThePlan()
    {
        var (project, asset) = Fixture();
        var second = new ProjectAsset(ProjectAssetId.New(), asset.Kind, asset.MediaType, asset.ByteLength, asset.Sha256, asset.CreatedUtc, "Second take");
        project.RegisterAsset(second);
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        editor.Execute(new AcceptVocalLowCutCommand(second.Id, second.Sha256));
        project.RemoveAsset(asset.Id);
        Assert.Equal(second.Id, Assert.Single(project.VocalProcessingRecipes).AssetId);
        Assert.NotNull(project.VocalProcessingChain);
    }

    [Theory]
    [InlineData("processorId", "unknown")]
    [InlineData("cutoffHertz", "120")]
    [InlineData("q", "2")]
    [InlineData("role", "Space")]
    [InlineData("sourceSha256", "0000000000000000000000000000000000000000000000000000000000000000")]
    public void ImportedRecipes_RejectUnsupportedSettingsAndWrongSources(string field, string value)
    {
        var (project, asset) = Fixture();
        new ProjectEditor(project).Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        var recipe = document["vocalProcessingRecipes"]![0]!;
        recipe[field] = field is "cutoffHertz" or "q" ? JsonValue.Create(double.Parse(value)) : JsonValue.Create(value);
        // Import without media is rejected too; inspect the domain JSON directly to check the recipe boundary.
        Assert.ThrowsAny<ArgumentException>(() => System.Text.Json.JsonSerializer.Deserialize<SongProject>(document.ToJsonString(), JsonOptions));
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData("null")]
    [InlineData("duplicate")]
    [InlineData("missing-source")]
    [InlineData("missing-role")]
    [InlineData("missing-time")]
    public void ImportedRecipes_RejectBrokenRelationships(string scenario)
    {
        var (project, asset) = Fixture();
        new ProjectEditor(project).Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        var recipes = document["vocalProcessingRecipes"]!.AsArray();
        switch (scenario)
        {
            case "null": recipes.Add((JsonNode?)null); break;
            case "duplicate": recipes.Add(recipes[0]!.DeepClone()); break;
            case "missing-source": document["assets"] = new JsonArray(); break;
            case "missing-role": document["vocalProcessingChain"] = null; break;
            case "missing-time": recipes[0]!["acceptedUtc"] = "0001-01-01T00:00:00+00:00"; break;
        }
        Assert.ThrowsAny<ArgumentException>(() => System.Text.Json.JsonSerializer.Deserialize<SongProject>(document.ToJsonString(), JsonOptions));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Recipes_SurviveSaveRecoveryDuplicateAndPackageWithOriginalBytesIntact(bool adjustable)
    {
        var directory = Path.Combine(Path.GetTempPath(), "maskil-processing-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var (project, asset) = Fixture();
            var editor = new ProjectEditor(project);
            editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256,
                adjustable ? 120 : VocalProcessingRecipe.LowCutHertz, adjustable ? .9 : VocalProcessingRecipe.LowCutQ));
            var recipe = Assert.Single(project.VocalProcessingRecipes);
            var assets = new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = Source };
            var package = PortableProjectPackage.Inspect(PortableProjectPackage.Export(project, assets));
            Assert.Equal(recipe, Assert.Single(package.Project.VocalProcessingRecipes));
            Assert.Equal(Source, package.Assets[asset.Id]);
            var duplicate = PortableProjectImporter.Duplicate(project, "Independent copy");
            Assert.NotEqual(project.Id, duplicate.Id);
            Assert.Equal(recipe, Assert.Single(duplicate.VocalProcessingRecipes));
            var repository = new JsonFileProjectRepository(directory);
            await repository.SaveWithAssetAsync(project, asset, new MemoryStream(Source));
            Assert.Equal(recipe, Assert.Single((await repository.LoadAsync(project.Id, CancellationToken.None))!.VocalProcessingRecipes));
            var savedRevision = project.LastModifiedUtc;
            project.Rename("Recovered low-cut review");
            await repository.SaveRecoverySnapshotAsync(new ProjectRecoverySnapshot(project, DateTimeOffset.UtcNow, savedRevision, "processing-test"));
            Assert.Equal(recipe, Assert.Single((await repository.LoadRecoverySnapshotAsync(project.Id))!.Project.VocalProcessingRecipes));
            await using var original = await repository.OpenAssetAsync(project.Id, asset.Id);
            using var restored = new MemoryStream();
            await original!.CopyToAsync(restored);
            Assert.Equal(Source, restored.ToArray());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void Schema33_MigrationAddsNoAcceptedProcessing()
    {
        var project = SongProject.Create("Old production plan");
        project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.CorrectiveTone], DateTimeOffset.UtcNow));
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        document["schemaVersion"] = 33;
        document.Remove("vocalProcessingRecipes");
        var imported = PortableProjectImporter.Inspect(document.ToJsonString());
        Assert.Equal(33, imported.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current, imported.Project.SchemaVersion);
        Assert.Empty(imported.Project.VocalProcessingRecipes);
        Assert.Equal(project.VocalProcessingChain!.Roles, imported.Project.VocalProcessingChain!.Roles);
    }

    [Fact]
    public void AdvancedRevision_UndoesToTheExactLegacyRecipeAndInvalidatesProfileProposals()
    {
        var (project, asset) = Fixture();
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Warm], "", DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var legacy = Assert.Single(project.VocalProcessingRecipes);
        var proposal = VocalProfileProposer.Propose(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256, 120, .9));
        var revised = Assert.Single(project.VocalProcessingRecipes);
        Assert.Equal(VocalProcessingRecipe.AdjustableLowCutProcessorId, revised.ProcessorId);
        Assert.Equal(120, revised.CutoffHertz);
        Assert.Equal(.9, revised.Q);
        Assert.Throws<InvalidOperationException>(() => editor.Execute(new AcceptVocalProfileProposalCommand(proposal.SourceSignature)));
        editor.Undo();
        Assert.Same(legacy, Assert.Single(project.VocalProcessingRecipes));
        editor.Redo();
        Assert.Same(revised, Assert.Single(project.VocalProcessingRecipes));
        editor.Execute(new ClearVocalProcessingRecipeCommand(asset.Id));
        editor.Undo();
        Assert.Same(revised, Assert.Single(project.VocalProcessingRecipes));
        Assert.Same(asset, Assert.Single(project.Assets));
    }

    [Theory]
    [InlineData(39, .7)]
    [InlineData(201, .7)]
    [InlineData(80.5, .7)]
    [InlineData(double.NaN, .7)]
    [InlineData(double.PositiveInfinity, .7)]
    [InlineData(80, .49)]
    [InlineData(80, 1.01)]
    [InlineData(80, double.NaN)]
    [InlineData(80, double.PositiveInfinity)]
    public void InvalidAdvancedSettings_KeepTheAcceptedRecipeAndHistory(double cutoffHertz, double q)
    {
        var (project, asset) = Fixture();
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var original = Assert.Single(project.VocalProcessingRecipes);
        var revision = project.LastModifiedUtc;
        Assert.Throws<ArgumentException>(() => editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256, cutoffHertz, q)));
        Assert.Same(original, Assert.Single(project.VocalProcessingRecipes));
        Assert.Equal(revision, project.LastModifiedUtc);
        editor.Undo();
        Assert.Empty(project.VocalProcessingRecipes);
        Assert.False(editor.CanUndo);
    }

    [Fact]
    public void Schema34Package_MigratesWithLegacySettingsAndOriginalAudioUnchanged()
    {
        var (project, asset) = Fixture();
        new ProjectEditor(project).Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var recipe = Assert.Single(project.VocalProcessingRecipes);
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        document["schemaVersion"] = 34;
        var oldProject = System.Text.Json.JsonSerializer.Deserialize<SongProject>(document.ToJsonString(), JsonOptions)!;
        var imported = PortableProjectPackage.Inspect(PortableProjectPackage.Export(oldProject, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = Source }));
        Assert.Equal(34, imported.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current, imported.Project.SchemaVersion);
        Assert.Equal(recipe, Assert.Single(imported.Project.VocalProcessingRecipes));
        Assert.Equal(Source, imported.Assets[asset.Id]);
    }

    [Fact]
    public void LevelControl_CanShareATakeWithLowCutAndRestoresEachRecipe()
    {
        var (project, asset) = Fixture();
        project.SetVocalProcessingChain(new VocalProcessingChain(
            [VocalProcessingRole.CorrectiveTone, VocalProcessingRole.TransparentDynamics], DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        editor.Execute(new AcceptVocalLevelControlCommand(asset.Id, asset.Sha256));
        Assert.Equal(2, project.VocalProcessingRecipes.Count);
        var level = project.VocalProcessingRecipes.Single(item => item.Role == VocalProcessingRole.TransparentDynamics);
        Assert.Equal(VocalProcessingRecipe.LevelControlProcessorId, level.ProcessorId);
        Assert.Equal(-18, level.ThresholdDecibels);
        Assert.Equal(2, level.Ratio);
        Assert.Equal(20, level.AttackMilliseconds);
        Assert.Equal(120, level.ReleaseMilliseconds);
        Assert.Null(level.CutoffHertz);
        editor.Undo();
        Assert.Equal(VocalProcessingRole.CorrectiveTone, Assert.Single(project.VocalProcessingRecipes).Role);
        editor.Redo();
        editor.Execute(new ClearVocalProcessingRecipeCommand(asset.Id, VocalProcessingRole.TransparentDynamics));
        Assert.Equal(VocalProcessingRole.CorrectiveTone, Assert.Single(project.VocalProcessingRecipes).Role);
        editor.Undo();
        Assert.Equal(2, project.VocalProcessingRecipes.Count);
        Assert.Throws<InvalidOperationException>(() => project.SetVocalProcessingChain(
            new VocalProcessingChain([VocalProcessingRole.CorrectiveTone], DateTimeOffset.UtcNow)));
        Assert.Throws<InvalidOperationException>(() => project.ClearVocalProcessingChain());
        editor.Execute(new ClearVocalProcessingRecipeCommand(asset.Id, VocalProcessingRole.CorrectiveTone));
        project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.TransparentDynamics], DateTimeOffset.UtcNow));
        Assert.Equal(VocalProcessingRole.TransparentDynamics, Assert.Single(project.VocalProcessingRecipes).Role);
    }

    [Fact]
    public void LevelControl_RejectsAMissingRoleAndDoesNotAlterLowCutJson()
    {
        var (project, asset) = Fixture();
        var editor = new ProjectEditor(project);
        Assert.Throws<ArgumentException>(() => editor.Execute(new AcceptVocalLevelControlCommand(asset.Id, asset.Sha256)));
        Assert.False(editor.CanUndo);
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        var json = PortableProjectExporter.SerializeDocument(project);
        Assert.DoesNotContain("thresholdDecibels", System.Text.Encoding.UTF8.GetString(json));
        project.SetVocalProcessingChain(new VocalProcessingChain(
            [VocalProcessingRole.CorrectiveTone, VocalProcessingRole.TransparentDynamics], DateTimeOffset.UtcNow));
        editor.Execute(new AcceptVocalLevelControlCommand(asset.Id, asset.Sha256));
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        var level = document["vocalProcessingRecipes"]!.AsArray().Single(item => item!["role"]!.GetValue<string>() == "TransparentDynamics")!;
        level["thresholdDecibels"] = -12;
        Assert.ThrowsAny<ArgumentException>(() => System.Text.Json.JsonSerializer.Deserialize<SongProject>(document.ToJsonString(), JsonOptions));
    }

    [Fact]
    public void AcceptedLevelControl_IsRetainedByAProfileAndSurvivesSchema35Migration()
    {
        var (project, asset) = Fixture();
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Aggressive], "", DateTimeOffset.UtcNow));
        project.SetVocalProcessingChain(new VocalProcessingChain(
            [VocalProcessingRole.CorrectiveTone, VocalProcessingRole.TransparentDynamics], DateTimeOffset.UtcNow));
        new ProjectEditor(project).Execute(new AcceptVocalLevelControlCommand(asset.Id, asset.Sha256));
        var proposal = VocalProfileProposer.Propose(project);
        Assert.Contains(proposal.Jobs.Single(job => job.Role == VocalProcessingRole.TransparentDynamics).Reasons, reason => reason.Contains("already has accepted"));
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        document["schemaVersion"] = 35;
        var oldProject = System.Text.Json.JsonSerializer.Deserialize<SongProject>(document.ToJsonString(), JsonOptions)!;
        var imported = PortableProjectPackage.Inspect(PortableProjectPackage.Export(oldProject, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = Source }));
        Assert.Equal(35, imported.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current, imported.Project.SchemaVersion);
        Assert.Equal(VocalProcessingRecipe.LevelControlProcessorId, Assert.Single(imported.Project.VocalProcessingRecipes).ProcessorId);
        Assert.Equal(Source, imported.Assets[asset.Id]);
    }

    [Fact]
    public void Saturation_CanShareATakeAndSurvivesSchema36Migration()
    {
        var (project, asset) = Fixture();
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Clean], "", DateTimeOffset.UtcNow));
        project.SetVocalProcessingChain(new VocalProcessingChain(
            [VocalProcessingRole.CorrectiveTone, VocalProcessingRole.Saturation], DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        Assert.Throws<ArgumentException>(() => editor.Execute(new AcceptVocalSaturationCommand(asset.Id, "not-a-digest")));
        editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
        editor.Execute(new AcceptVocalSaturationCommand(asset.Id, asset.Sha256));
        var saturation = project.VocalProcessingRecipes.Single(item => item.Role == VocalProcessingRole.Saturation);
        Assert.Equal(VocalProcessingRecipe.SaturationProcessorId, saturation.ProcessorId);
        Assert.Equal(VocalProcessingRecipe.SaturationColorAmount, saturation.ColorAmount);
        Assert.Null(saturation.CutoffHertz);
        Assert.Null(saturation.ThresholdDecibels);
        var proposal = VocalProfileProposer.Propose(project);
        Assert.Contains(proposal.Jobs.Single(job => job.Role == VocalProcessingRole.Saturation).Reasons, reason => reason.Contains("already has accepted"));
        editor.Undo();
        Assert.Equal(VocalProcessingRole.CorrectiveTone, Assert.Single(project.VocalProcessingRecipes).Role);
        editor.Redo();
        var json = System.Text.Encoding.UTF8.GetString(PortableProjectExporter.SerializeDocument(project));
        Assert.Contains("colorAmount", json);
        Assert.DoesNotContain("\"colorAmount\":null", json);
        Assert.Throws<InvalidOperationException>(() => project.SetVocalProcessingChain(
            new VocalProcessingChain([VocalProcessingRole.CorrectiveTone], DateTimeOffset.UtcNow)));
        var document = JsonNode.Parse(json)!.AsObject();
        document["schemaVersion"] = 36;
        document["vocalProcessingRecipes"]!.AsArray().Remove(
            document["vocalProcessingRecipes"]!.AsArray().Single(item => item!["role"]!.GetValue<string>() == "Saturation"));
        var oldProject = System.Text.Json.JsonSerializer.Deserialize<SongProject>(document.ToJsonString(), JsonOptions)!;
        var imported = PortableProjectPackage.Inspect(PortableProjectPackage.Export(oldProject, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = Source }));
        Assert.Equal(36, imported.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current, imported.Project.SchemaVersion);
        Assert.Equal(VocalProcessingRecipe.LowCutProcessorId, Assert.Single(imported.Project.VocalProcessingRecipes).ProcessorId);
        Assert.Equal(Source, imported.Assets[asset.Id]);
        var altered = JsonNode.Parse(json)!.AsObject();
        altered["vocalProcessingRecipes"]!.AsArray().Single(item => item!["role"]!.GetValue<string>() == "Saturation")!["colorAmount"] = 0.4;
        Assert.ThrowsAny<ArgumentException>(() => System.Text.Json.JsonSerializer.Deserialize<SongProject>(altered.ToJsonString(), JsonOptions));
    }

    [Fact]
    public void CharacterCompression_StaysDistinctFromLevelControlAndSurvivesSave()
    {
        var (project, asset) = Fixture();
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Clean], "", DateTimeOffset.UtcNow));
        project.SetVocalProcessingChain(new VocalProcessingChain(
            [VocalProcessingRole.TransparentDynamics, VocalProcessingRole.CharacterCompression], DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalLevelControlCommand(asset.Id, asset.Sha256));
        editor.Execute(new AcceptVocalCharacterCompressionCommand(asset.Id, asset.Sha256));
        var character = project.VocalProcessingRecipes.Single(item => item.Role == VocalProcessingRole.CharacterCompression);
        Assert.Equal(VocalProcessingRecipe.CharacterCompressionProcessorId, character.ProcessorId);
        Assert.Equal(-24, character.ThresholdDecibels);
        Assert.Equal(4, character.Ratio);
        Assert.Equal(0, character.AttackMilliseconds);
        Assert.Equal(250, character.ReleaseMilliseconds);
        Assert.Null(character.ColorAmount);
        Assert.Null(character.CutoffHertz);
        var proposal = VocalProfileProposer.Propose(project);
        Assert.Contains(proposal.Jobs.Single(job => job.Role == VocalProcessingRole.CharacterCompression).Reasons, reason => reason.Contains("already has accepted"));
        editor.Undo();
        Assert.Equal(VocalProcessingRole.TransparentDynamics, Assert.Single(project.VocalProcessingRecipes).Role);
        editor.Redo();
        Assert.Throws<InvalidOperationException>(() => project.SetVocalProcessingChain(
            new VocalProcessingChain([VocalProcessingRole.TransparentDynamics], DateTimeOffset.UtcNow)));
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        Assert.Equal(SchemaVersion.Current.Value, document["schemaVersion"]!.GetValue<int>());
        var stored = document["vocalProcessingRecipes"]!.AsArray().Single(item => item!["role"]!.GetValue<string>() == "CharacterCompression")!;
        stored["ratio"] = 2;
        Assert.ThrowsAny<ArgumentException>(() => System.Text.Json.JsonSerializer.Deserialize<SongProject>(document.ToJsonString(), JsonOptions));
    }

    [Fact]
    public void Cleanup_LowersOnlyTheAcceptedRoleAndBlocksPlanRemoval()
    {
        var (project, asset) = Fixture();
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Warm], "", DateTimeOffset.UtcNow));
        project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.Cleanup], DateTimeOffset.UtcNow));
        var editor = new ProjectEditor(project);
        editor.Execute(new AcceptVocalCleanupCommand(asset.Id, asset.Sha256));
        var cleanup = Assert.Single(project.VocalProcessingRecipes);
        Assert.Equal(VocalProcessingRecipe.CleanupProcessorId, cleanup.ProcessorId);
        Assert.Equal(-40, cleanup.ThresholdDecibels);
        Assert.Equal(4, cleanup.Ratio);
        Assert.Equal(10, cleanup.AttackMilliseconds);
        Assert.Equal(200, cleanup.ReleaseMilliseconds);
        Assert.Null(cleanup.ColorAmount);
        var proposal = VocalProfileProposer.Propose(project);
        Assert.Contains(proposal.Jobs.Single(job => job.Role == VocalProcessingRole.Cleanup).Reasons, reason => reason.Contains("already has accepted"));
        editor.Undo();
        Assert.Empty(project.VocalProcessingRecipes);
        editor.Redo();
        Assert.Throws<InvalidOperationException>(() => project.ClearVocalProcessingChain());
        Assert.Equal(SchemaVersion.Current.Value, JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!["schemaVersion"]!.GetValue<int>());
    }
}
