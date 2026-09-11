using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class VocalProductionIntentTests
{
    [Fact]
    public void SetIntent_IsInspectableReversibleAndDoesNotRequireATake()
    {
        var editor = new ProjectEditor(SongProject.Create("Vocal direction"));

        editor.Execute(new SetVocalProductionIntentCommand(
            [VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate],
            "  Keep the breath in the verses.  "));

        var intent = Assert.IsType<VocalProductionIntent>(editor.Project.VocalProductionIntent);
        Assert.Equal([VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate], intent.Descriptors);
        Assert.Equal("Keep the breath in the verses.", intent.ArtistNotes);
        Assert.Empty(editor.Project.Assets);
        Assert.True(editor.CanUndo);

        editor.Undo();
        Assert.Null(editor.Project.VocalProductionIntent);

        editor.Redo();
        Assert.Equal(intent, editor.Project.VocalProductionIntent);
    }

    [Fact]
    public void ClearIntent_IsReversible()
    {
        var project = SongProject.Create("Clear direction");
        var original = new VocalProductionIntent(
            [VocalProductionDescriptor.Clean, VocalProductionDescriptor.Forward],
            "Keep the lead natural.",
            DateTimeOffset.UtcNow);
        project.SetVocalProductionIntent(original);
        var editor = new ProjectEditor(project);

        editor.Execute(new ClearVocalProductionIntentCommand());
        Assert.Null(project.VocalProductionIntent);

        editor.Undo();
        Assert.Equal(original, project.VocalProductionIntent);
    }

    [Fact]
    public void Intent_RequiresFocusedArtistFacingInput()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new VocalProductionIntent([], "", now));
        Assert.Throws<ArgumentException>(() => new VocalProductionIntent([(VocalProductionDescriptor)999], "", now));
        Assert.Throws<ArgumentException>(() => new VocalProductionIntent([VocalProductionDescriptor.Clean], "", default));
        Assert.Throws<ArgumentException>(() => new VocalProductionIntent(
            [
                VocalProductionDescriptor.Clean,
                VocalProductionDescriptor.Warm,
                VocalProductionDescriptor.Intimate,
                VocalProductionDescriptor.Forward,
                VocalProductionDescriptor.Cinematic
            ],
            "",
            now));
        Assert.Throws<ArgumentException>(() => new VocalProductionIntent(
            [VocalProductionDescriptor.Clean],
            new string('x', VocalProductionIntent.MaximumArtistNotesLength + 1),
            now));
    }

    [Fact]
    public void Schema31_MigratesAndIntentRoundTripsPortably()
    {
        var legacy = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Pre-intent song")))!.AsObject();
        legacy["schemaVersion"] = 31;
        legacy.Remove("vocalProductionIntent");

        var migrated = PortableProjectImporter.Inspect(legacy.ToJsonString());

        Assert.Equal(31, migrated.SourceSchemaVersion);
        Assert.Equal(32, migrated.Project.SchemaVersion.Value);
        Assert.Null(migrated.Project.VocalProductionIntent);

        var project = SongProject.Create("Portable intent");
        project.SetVocalProductionIntent(new VocalProductionIntent(
            [VocalProductionDescriptor.SoftRock, VocalProductionDescriptor.Cinematic],
            "Controlled verses; larger final chorus.",
            DateTimeOffset.UtcNow));

        var restored = PortableProjectImporter.Inspect(Encoding.UTF8.GetString(PortableProjectExporter.SerializeDocument(project))).Project;

        AssertIntent(project.VocalProductionIntent!, restored.VocalProductionIntent);
        Assert.Equal(SchemaVersion.Current, restored.SchemaVersion);
    }

    [Fact]
    public void UpdateIntent_UndoRedoRestoresExactChoicesAndTimestamps()
    {
        var editor = new ProjectEditor(SongProject.Create("Revised direction"));
        editor.Execute(new SetVocalProductionIntentCommand([VocalProductionDescriptor.Warm], "First direction"));
        var original = editor.Project.VocalProductionIntent!;
        editor.Execute(new SetVocalProductionIntentCommand([VocalProductionDescriptor.Forward], "New direction"));
        var revised = editor.Project.VocalProductionIntent!;
        editor.Undo();
        Assert.Same(original, editor.Project.VocalProductionIntent);
        editor.Redo();
        Assert.Same(revised, editor.Project.VocalProductionIntent);
        editor.Execute(new ClearVocalProductionIntentCommand());
        editor.Undo();
        Assert.Same(revised, editor.Project.VocalProductionIntent);
        editor.Redo();
        Assert.Null(editor.Project.VocalProductionIntent);
    }

    [Fact]
    public void Intent_CopiesAndProtectsItsDescriptorCollection()
    {
        var descriptors = new[] { VocalProductionDescriptor.Warm, VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate };
        var intent = new VocalProductionIntent(descriptors, "", DateTimeOffset.UtcNow);
        descriptors[0] = VocalProductionDescriptor.Clean;
        Assert.Equal([VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate], intent.Descriptors);
        Assert.Throws<NotSupportedException>(() => ((IList<VocalProductionDescriptor>)intent.Descriptors)[0] = VocalProductionDescriptor.Clean);
    }

    [Fact]
    public void Import_RejectsUnknownNumericDescriptors()
    {
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Invalid direction")))!.AsObject();
        document["vocalProductionIntent"] = new JsonObject
        {
            ["descriptors"] = new JsonArray(999),
            ["artistNotes"] = "",
            ["updatedUtc"] = DateTimeOffset.UtcNow
        };
        Assert.Throws<InvalidProjectDataException>(() => PortableProjectImporter.Inspect(document.ToJsonString()));
    }

    [Fact]
    public async Task Intent_PersistsRecoversDuplicatesAndPackagesWithoutChangingSourceAudio()
    {
        var directory = Path.Combine(Path.GetTempPath(), "maskil-vocal-intent-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Persisted direction");
            var intent = new VocalProductionIntent([VocalProductionDescriptor.Warm, VocalProductionDescriptor.Intimate], "Preserve the breath.", DateTimeOffset.UtcNow);
            project.SetVocalProductionIntent(intent);
            await repository.SaveAsync(project, CancellationToken.None);
            var loaded = await repository.LoadAsync(project.Id, CancellationToken.None);
            AssertIntent(intent, loaded!.VocalProductionIntent);

            var savedRevision = project.LastModifiedUtc;
            var revised = new VocalProductionIntent([VocalProductionDescriptor.Clean], "Keep it natural.", DateTimeOffset.UtcNow);
            project.SetVocalProductionIntent(revised);
            await repository.SaveRecoverySnapshotAsync(new ProjectRecoverySnapshot(project, DateTimeOffset.UtcNow, savedRevision, "vocal-intent-test"), CancellationToken.None);
            var recovered = await repository.LoadRecoverySnapshotAsync(project.Id, CancellationToken.None);
            AssertIntent(revised, recovered!.Project.VocalProductionIntent);
            AssertIntent(intent, (await repository.LoadAsync(project.Id, CancellationToken.None))!.VocalProductionIntent);

            var duplicate = PortableProjectImporter.Duplicate(project, "Direction copy");
            Assert.NotEqual(project.Id, duplicate.Id);
            AssertIntent(revised, duplicate.VocalProductionIntent);

            var bytes = Encoding.UTF8.GetBytes("Immutable artist performance");
            var asset = new ProjectAsset(ProjectAssetId.New(), ProjectAssetKind.OriginalVocalTake, "audio/webm", bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), DateTimeOffset.UtcNow, "Original take");
            project.RegisterAsset(asset);
            var package = PortableProjectPackage.Inspect(PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]> { [asset.Id] = bytes }));
            AssertIntent(revised, package.Project.VocalProductionIntent);
            Assert.Equal(bytes, package.Assets[asset.Id]);
            Assert.Equal(asset, Assert.Single(package.Project.Assets));
            project.RemoveAsset(asset.Id);
            Assert.Same(revised, project.VocalProductionIntent);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void AssertIntent(VocalProductionIntent expected, VocalProductionIntent? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Descriptors, actual.Descriptors);
        Assert.Equal(expected.ArtistNotes, actual.ArtistNotes);
        Assert.Equal(expected.UpdatedUtc, actual.UpdatedUtc);
    }
}
