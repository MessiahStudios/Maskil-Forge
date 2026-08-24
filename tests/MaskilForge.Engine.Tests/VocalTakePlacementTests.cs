using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class VocalTakePlacementTests
{
    [Fact]
    public void SetVocalTakePlacement_IsExplicitReversibleAndOnePerTake()
    {
        var project = SongProject.Create("Placed take");
        var asset = CreateAsset();
        project.RegisterAsset(asset);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetVocalTakePlacementCommand(asset.Id, new MusicalPosition(9, 1, 0)));
        var placed = Assert.Single(editor.Project.VocalTakePlacements);
        Assert.Equal(asset.Id, placed.AssetId);
        Assert.Equal(9, placed.Start.Bar);
        Assert.Equal(1, placed.Start.Beat);
        Assert.Equal(0, placed.Start.Tick);
        Assert.Equal(15_360, editor.Project.VocalTakeStartTick(asset.Id));

        editor.Execute(new SetVocalTakePlacementCommand(asset.Id, new MusicalPosition(3, 2, 120)));
        var updated = Assert.Single(editor.Project.VocalTakePlacements);
        Assert.Equal(placed.Id, updated.Id);
        Assert.Equal(3, updated.Start.Bar);
        Assert.Equal(2, updated.Start.Beat);
        Assert.Equal(120, updated.Start.Tick);

        editor.Undo();
        Assert.Equal(9, Assert.Single(editor.Project.VocalTakePlacements).Start.Bar);

        editor.Undo();
        Assert.Empty(editor.Project.VocalTakePlacements);
        Assert.Equal(0, editor.Project.VocalTakeStartTick(asset.Id));

        editor.Redo();
        Assert.Equal(placed.Id, Assert.Single(editor.Project.VocalTakePlacements).Id);
    }

    [Fact]
    public void ClearVocalTakePlacement_RestoresTheSameRecord()
    {
        var project = SongProject.Create("Cleared take");
        var asset = CreateAsset();
        project.RegisterAsset(asset);
        var editor = new ProjectEditor(project);
        editor.Execute(new SetVocalTakePlacementCommand(asset.Id, new MusicalPosition(2, 1, 0)));
        var placed = Assert.Single(editor.Project.VocalTakePlacements);

        editor.Execute(new ClearVocalTakePlacementCommand(asset.Id));
        Assert.Empty(editor.Project.VocalTakePlacements);

        editor.Undo();
        var restored = Assert.Single(editor.Project.VocalTakePlacements);
        Assert.Equal(placed.Id, restored.Id);
        Assert.Equal(placed.Start, restored.Start);
        Assert.Equal(placed.CreatedUtc, restored.CreatedUtc);
    }

    [Fact]
    public void Placement_RequiresAnExistingOriginalVocalTakeAndValidMeter()
    {
        var project = SongProject.Create("Invalid placement");
        var asset = CreateAsset();
        project.RegisterAsset(asset);

        Assert.Throws<KeyNotFoundException>(() =>
            project.SetVocalTakePlacement(ProjectAssetId.New(), new MusicalPosition(1, 1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            project.SetVocalTakePlacement(asset.Id, new MusicalPosition(1, 5, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            project.SetVocalTakePlacement(asset.Id, new MusicalPosition(1, 1, 480)));
        Assert.Throws<KeyNotFoundException>(() => project.ClearVocalTakePlacement(asset.Id));
    }

    [Fact]
    public void RemovingATake_DropsItsPlacementWithoutRewritingNotes()
    {
        var project = SongProject.Create("Removed take");
        var asset = CreateAsset();
        project.RegisterAsset(asset);
        project.SetVocalTakePlacement(asset.Id, new MusicalPosition(4, 1, 0));
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 192, 80, 96);

        project.RemoveAsset(asset.Id);

        Assert.Empty(project.VocalTakePlacements);
        Assert.Equal(note.Id, Assert.Single(project.NoteEvents).Id);
        Assert.Equal(192, note.StartTick);
    }

    [Fact]
    public void TimeSignatureChange_RefusesAPlacementThatWouldLeaveTheMeter()
    {
        var project = SongProject.Create("Meter clash");
        var asset = CreateAsset();
        project.RegisterAsset(asset);
        project.SetVocalTakePlacement(asset.Id, new MusicalPosition(1, 4, 0));

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => project.SetTimeSignature(3, 4));
        Assert.Contains("Beat must be between 1 and 3", error.Message);
        Assert.Equal(4, Assert.Single(project.VocalTakePlacements).Start.Beat);
    }

    [Fact]
    public void Schema27_MigratesToAnExplicitEmptyVocalTakePlacementCollection()
    {
        var document = JsonNode.Parse(PortableProjectExporter.SerializeDocument(SongProject.Create("Pre-placement song")))!.AsObject();
        document["schemaVersion"] = 27;
        document.Remove("vocalTakePlacements");

        var inspected = PortableProjectImporter.Inspect(document.ToJsonString());

        Assert.Equal(27, inspected.SourceSchemaVersion);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
        Assert.Empty(inspected.Project.VocalTakePlacements);
        Assert.Empty(inspected.Project.PerformanceObservationGestures);
    }

    [Fact]
    public void Placement_RoundTripsWithTheProjectWithoutAttachingAudio()
    {
        var project = SongProject.Create("Portable placement");
        var asset = CreateAsset();
        project.RegisterAsset(asset);
        var placed = project.SetVocalTakePlacement(asset.Id, new MusicalPosition(5, 3, 60));

        var package = PortableProjectPackage.Export(project, new Dictionary<ProjectAssetId, byte[]>
        {
            [asset.Id] = Encoding.UTF8.GetBytes("artist-placed source performance")
        });
        var inspected = PortableProjectPackage.Inspect(package);

        var restored = Assert.Single(inspected.Project.VocalTakePlacements);
        Assert.Equal(placed.Id, restored.Id);
        Assert.Equal(placed.AssetId, restored.AssetId);
        Assert.Equal(placed.Start, restored.Start);
        Assert.Equal(placed.CreatedUtc, restored.CreatedUtc);
        Assert.Equal(placed.UpdatedUtc, restored.UpdatedUtc);
        Assert.Equal(SchemaVersion.Current.Value, inspected.Project.SchemaVersion.Value);
    }

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-placed source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Placed take");
    }
}
