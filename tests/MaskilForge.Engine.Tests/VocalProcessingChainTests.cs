using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class VocalProcessingChainTests
{
    [Fact]
    public void Catalog_DefinesDistinctJobsWithoutCreatingAChain()
    {
        Assert.Equal(1, VocalProcessingRoleCatalog.Version);
        Assert.Equal([
            VocalProcessingRole.Cleanup, VocalProcessingRole.CorrectiveTone, VocalProcessingRole.CharacterCompression,
            VocalProcessingRole.Saturation, VocalProcessingRole.TransparentDynamics, VocalProcessingRole.SibilanceControl,
            VocalProcessingRole.Space
        ], VocalProcessingRoleCatalog.Roles.Select(role => role.Id));
        Assert.All(VocalProcessingRoleCatalog.Roles, role =>
        {
            Assert.False(string.IsNullOrWhiteSpace(role.Name));
            Assert.False(string.IsNullOrWhiteSpace(role.Purpose));
            Assert.False(string.IsNullOrWhiteSpace(role.Technique));
        });
        var project = SongProject.Create("Unprocessed");
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Warm], "", DateTimeOffset.UtcNow));
        Assert.Null(project.VocalProcessingChain);
    }

    [Fact]
    public void SetReorderAndClear_AreReversibleWithExactOrderAndTimestamps()
    {
        var editor = new ProjectEditor(SongProject.Create("Production plan"));
        editor.Execute(new SetVocalProcessingChainCommand([VocalProcessingRole.Cleanup, VocalProcessingRole.CharacterCompression, VocalProcessingRole.TransparentDynamics]));
        var first = editor.Project.VocalProcessingChain!;
        editor.Execute(new SetVocalProcessingChainCommand([VocalProcessingRole.TransparentDynamics, VocalProcessingRole.Cleanup]));
        var second = editor.Project.VocalProcessingChain!;
        Assert.Equal([VocalProcessingRole.TransparentDynamics, VocalProcessingRole.Cleanup], second.Roles);
        editor.Undo();
        Assert.Same(first, editor.Project.VocalProcessingChain);
        editor.Undo();
        Assert.Null(editor.Project.VocalProcessingChain);
        editor.Redo();
        Assert.Same(first, editor.Project.VocalProcessingChain);
        editor.Redo();
        Assert.Same(second, editor.Project.VocalProcessingChain);
        editor.Execute(new ClearVocalProcessingChainCommand());
        Assert.Null(editor.Project.VocalProcessingChain);
        editor.Undo();
        Assert.Same(second, editor.Project.VocalProcessingChain);
        editor.Redo();
        Assert.Null(editor.Project.VocalProcessingChain);
    }

    [Fact]
    public void InvalidJobs_DoNotChangeTheProjectOrUndoHistory()
    {
        var editor = new ProjectEditor(SongProject.Create("Invalid plan"));
        var revision = editor.Project.LastModifiedUtc;
        Assert.Throws<ArgumentException>(() => editor.Execute(new SetVocalProcessingChainCommand([])));
        Assert.Throws<ArgumentException>(() => editor.Execute(new SetVocalProcessingChainCommand([(VocalProcessingRole)999])));
        Assert.Throws<ArgumentException>(() => editor.Execute(new SetVocalProcessingChainCommand([VocalProcessingRole.Space, VocalProcessingRole.Space])));
        Assert.Throws<InvalidOperationException>(() => editor.Execute(new ClearVocalProcessingChainCommand()));
        Assert.Null(editor.Project.VocalProcessingChain);
        Assert.Equal(revision, editor.Project.LastModifiedUtc);
        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
    }

    [Fact]
    public void Chain_ProtectsItsOrderFromExternalMutationAndRequiresATimestamp()
    {
        var roles = new[] { VocalProcessingRole.Space, VocalProcessingRole.Cleanup };
        var chain = new VocalProcessingChain(roles, DateTimeOffset.UtcNow);
        roles[0] = VocalProcessingRole.Saturation;
        Assert.Equal([VocalProcessingRole.Space, VocalProcessingRole.Cleanup], chain.Roles);
        Assert.Throws<NotSupportedException>(() => ((IList<VocalProcessingRole>)chain.Roles)[0] = VocalProcessingRole.Cleanup);
        Assert.Throws<ArgumentException>(() => new VocalProcessingChain(roles, default));
        Assert.Throws<ArgumentNullException>(() => new VocalProcessingChain(null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IntentAndChain_HaveIndependentLifecyclesWithoutRewritingMusic()
    {
        var project = SongProject.Create("Independent plans");
        var editor = new ProjectEditor(project);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 80);
        editor.Execute(new SetVocalProcessingChainCommand([VocalProcessingRole.CharacterCompression, VocalProcessingRole.TransparentDynamics]));
        var chain = project.VocalProcessingChain!;
        editor.Execute(new SetVocalProductionIntentCommand([VocalProductionDescriptor.Clean], "Keep the voice natural."));
        Assert.Same(chain, project.VocalProcessingChain);
        editor.Execute(new ClearVocalProductionIntentCommand());
        Assert.Same(chain, project.VocalProcessingChain);
        editor.Undo();
        var intent = project.VocalProductionIntent;
        editor.Execute(new ClearVocalProcessingChainCommand());
        Assert.Same(intent, project.VocalProductionIntent);
        Assert.Same(note, Assert.Single(project.NoteEvents));
        Assert.Empty(project.Assets);
        Assert.Empty(project.ExpressionCurves);
    }

    [Fact]
    public void Schema32_MigratesWithoutInventingJobsOrChangingIntent()
    {
        var project = SongProject.Create("Existing direction");
        project.SetVocalProductionIntent(new VocalProductionIntent([VocalProductionDescriptor.Intimate], "Keep the breath.", DateTimeOffset.UtcNow));
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(project))!.AsObject();
        document["schemaVersion"] = 32;
        document.Remove("vocalProcessingChain");
        var original = document.ToJsonString();
        var inspected = PortableProjectImporter.Inspect(original);
        Assert.Equal(32, inspected.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current, inspected.Project.SchemaVersion);
        Assert.Null(inspected.Project.VocalProcessingChain);
        Assert.Equal(project.Id, inspected.Project.Id);
        Assert.Equal(project.LastModifiedUtc, inspected.Project.LastModifiedUtc);
        Assert.NotNull(project.VocalProductionIntent);
        Assert.Equal(project.VocalProductionIntent.Descriptors, inspected.Project.VocalProductionIntent!.Descriptors);
        Assert.Equal(project.VocalProductionIntent.ArtistNotes, inspected.Project.VocalProductionIntent.ArtistNotes);
        Assert.Equal(original, document.ToJsonString());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[999]")]
    [InlineData("[\"Output\"]")]
    [InlineData("[\"Cleanup\",\"Cleanup\"]")]
    public void PortableImport_RejectsInvalidRoleLists(string roles)
    {
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Invalid jobs")))!.AsObject();
        document["vocalProcessingChain"] = new JsonObject
        {
            ["roles"] = JsonNode.Parse(roles),
            ["updatedUtc"] = DateTimeOffset.UtcNow
        };
        Assert.Throws<InvalidProjectDataException>(() => PortableProjectImporter.Inspect(document.ToJsonString()));
    }

    [Fact]
    public async Task PersistenceRecoveryAndCopies_RetainOrderAndSourceAudio()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maskil-vocal-chain-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Saved chain");
            var chain = new VocalProcessingChain([VocalProcessingRole.SibilanceControl, VocalProcessingRole.CharacterCompression, VocalProcessingRole.Space], DateTimeOffset.UtcNow);
            project.SetVocalProcessingChain(chain);
            await repository.SaveAsync(project, CancellationToken.None);
            AssertChain(chain, (await repository.LoadAsync(project.Id, CancellationToken.None))!.VocalProcessingChain);
            var baseRevision = project.LastModifiedUtc;
            project.SetVocalProcessingChain(new VocalProcessingChain([VocalProcessingRole.Space, VocalProcessingRole.SibilanceControl], DateTimeOffset.UtcNow));
            Assert.NotNull(project.VocalProcessingChain);
            await repository.SaveRecoverySnapshotAsync(new ProjectRecoverySnapshot(project, DateTimeOffset.UtcNow, baseRevision, "chain-test"), CancellationToken.None);
            AssertChain(project.VocalProcessingChain, (await repository.LoadRecoverySnapshotAsync(project.Id, CancellationToken.None))!.Project.VocalProcessingChain);
            AssertChain(chain, (await repository.LoadAsync(project.Id, CancellationToken.None))!.VocalProcessingChain);
            var duplicate = PortableProjectImporter.Duplicate(project, "Plan copy");
            Assert.NotEqual(project.Id, duplicate.Id);
            AssertChain(project.VocalProcessingChain, duplicate.VocalProcessingChain);
            var json = Encoding.UTF8.GetString(PortableProjectExporter.SerializeDocument(project));
            AssertChain(project.VocalProcessingChain, PortableProjectImporter.Inspect(json).Project.VocalProcessingChain);

            var bytes = Encoding.UTF8.GetBytes("Artist-owned original audio");
            var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), DateTimeOffset.UtcNow, "Source take");
            project.RegisterAsset(asset);
            var package = PortableProjectPackage.Inspect(PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = bytes }));
            AssertChain(project.VocalProcessingChain, package.Project.VocalProcessingChain);
            Assert.Equal(bytes, package.Assets[asset.Id]);
            Assert.Equal(asset, Assert.Single(package.Project.Assets));
            project.RemoveAsset(asset.Id);
            AssertChain(package.Project.VocalProcessingChain!, project.VocalProcessingChain);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static void AssertChain(VocalProcessingChain expected, VocalProcessingChain? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Roles, actual.Roles);
        Assert.Equal(expected.UpdatedUtc, actual.UpdatedUtc);
    }
}
