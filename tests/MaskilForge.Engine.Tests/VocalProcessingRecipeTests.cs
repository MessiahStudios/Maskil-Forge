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

    [Fact]
    public async Task Recipes_SurviveSaveRecoveryDuplicateAndPackageWithOriginalBytesIntact()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maskil-processing-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var (project, asset) = Fixture();
            var editor = new ProjectEditor(project);
            editor.Execute(new AcceptVocalLowCutCommand(asset.Id, asset.Sha256));
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
}
