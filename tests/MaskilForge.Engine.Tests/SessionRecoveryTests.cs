using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

namespace MaskilForge.Engine.Tests;

public sealed class SessionRecoveryTests
{
    [Fact]
    public async Task RecoverySnapshot_PreservesUnsavedProjectWithoutReplacingSavedProject()
    {
        var directory = NewDirectory();
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var saved = SongProject.Create("Saved Title");
            var section = saved.AddSection(SectionKind.Verse);
            var line = section.AddLyricLine("Saved lyric");
            await repository.SaveAsync(saved, CancellationToken.None);
            var baseRevision = saved.LastModifiedUtc;

            var unsaved = Clone(saved, "Unsaved Recovered Title", "An unsaved raw lyric");
            unsaved.FindSection(section.Id).EditLyricLine(line.Id, "Unsaved lyric edit");
            await repository.SaveRecoverySnapshotAsync(
                new ProjectRecoverySnapshot(unsaved, DateTimeOffset.UtcNow, baseRevision, "test-session"),
                CancellationToken.None);

            var recovered = await repository.LoadRecoverySnapshotAsync(saved.Id, CancellationToken.None);
            var persisted = await repository.LoadAsync(saved.Id, CancellationToken.None);

            Assert.NotNull(recovered);
            Assert.Equal(saved.Id, recovered.Project.Id);
            Assert.Equal(section.Id, recovered.Project.Sections[0].Id);
            Assert.Equal(line.Id, recovered.Project.Sections[0].LyricLines[0].Id);
            Assert.Equal("Unsaved lyric edit", recovered.Project.Sections[0].LyricLines[0].Text);
            Assert.Equal("An unsaved raw lyric", recovered.Project.RawLyricDraft);
            Assert.Equal("Saved Title", persisted!.Title);
            Assert.Equal("Saved lyric", persisted.Sections[0].LyricLines[0].Text);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task RecoverySnapshot_MigratesSchemaV1ProjectData()
    {
        var directory = NewDirectory();
        var projectId = ProjectId.New();
        var sectionId = SectionId.New();
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "sessions"));
            var captured = DateTimeOffset.UtcNow;
            var json = $$"""
            {
              "project": {
                "id": "{{projectId}}",
                "schemaVersion": 1,
                "title": "Recovered V1",
                "tempo": { "beat": 0, "beatsPerMinute": 120 },
                "timeSignature": { "beat": 0, "numerator": 4, "denominator": 4 },
                "sections": [{
                  "id": "{{sectionId}}",
                  "kind": "Verse",
                  "title": "Verse",
                  "lyricLines": [{ "id": "{{LyricLineId.New()}}", "text": "Recovered words" }]
                }]
              },
              "capturedAtUtc": "{{captured:O}}",
              "baseProjectLastModifiedUtc": "{{captured:O}}",
              "sessionId": "legacy-session"
            }
            """;
            await File.WriteAllTextAsync(Path.Combine(directory, "sessions", $"{projectId}.json"), json);

            var snapshot = await new JsonFileProjectRepository(directory).LoadRecoverySnapshotAsync(projectId, CancellationToken.None);

            Assert.NotNull(snapshot);
            Assert.Equal(SchemaVersion.Current, snapshot.Project.SchemaVersion);
            var placement = Assert.Single(snapshot.Project.Timeline.SectionPlacements);
            Assert.Equal(sectionId, placement.SectionId);
            Assert.Equal(1, placement.Start.Bar);
            Assert.Equal(8, placement.DurationBars);
            Assert.Equal(["Recovered", "words"], snapshot.Project.Sections[0].LyricLines[0].Words.Select(word => word.Text));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task RecoverySnapshot_IsRejectedWhenSavedProjectRevisionChanged()
    {
        var directory = NewDirectory();
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Original");
            await repository.SaveAsync(project, CancellationToken.None);
            var staleRevision = project.LastModifiedUtc;
            var staleEditor = Clone(project, "Stale edit", "");

            project.Rename("Saved elsewhere");
            await repository.SaveAsync(project, CancellationToken.None);

            await Assert.ThrowsAsync<StaleProjectSessionException>(() =>
                repository.SaveRecoverySnapshotAsync(
                    new ProjectRecoverySnapshot(staleEditor, DateTimeOffset.UtcNow, staleRevision, "stale-session"),
                    CancellationToken.None));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task ExplicitSave_RemovesRecoverySnapshot()
    {
        var directory = NewDirectory();
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Saved Title", CancellationToken.None);
            var baseRevision = editor.Project.LastModifiedUtc;
            var unsaved = Clone(editor.Project, "Recovered then saved", "Draft words");
            await repository.SaveRecoverySnapshotAsync(
                new ProjectRecoverySnapshot(unsaved, DateTimeOffset.UtcNow, baseRevision, "test-session"),
                CancellationToken.None);

            var updated = await workspace.UpdateAsync(unsaved, baseRevision, CancellationToken.None);

            Assert.NotNull(updated);
            Assert.Null(await repository.LoadRecoverySnapshotAsync(editor.Project.Id, CancellationToken.None));
            Assert.Equal("Recovered then saved", (await repository.LoadAsync(editor.Project.Id, CancellationToken.None))!.Title);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task MovingProjectToTrash_RemovesItsRecoverySnapshot()
    {
        var directory = NewDirectory();
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Trash recovery");
            await repository.SaveAsync(project, CancellationToken.None);
            await repository.SaveRecoverySnapshotAsync(
                new ProjectRecoverySnapshot(Clone(project, "Unsaved", "words"), DateTimeOffset.UtcNow, project.LastModifiedUtc, "test-session"),
                CancellationToken.None);

            Assert.True(await repository.MoveToTrashAsync(project.Id, CancellationToken.None));

            Assert.Null(await repository.LoadRecoverySnapshotAsync(project.Id, CancellationToken.None));
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task ExplicitSave_IsRejectedWhenAnotherSessionAlreadySaved()
    {
        var directory = NewDirectory();
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var workspace = new ProjectWorkspace(repository);
            var editor = await workspace.CreateAsync("Original", CancellationToken.None);
            var staleRevision = editor.Project.LastModifiedUtc;
            var staleEditor = Clone(editor.Project, "Stale editor", "stale words");
            var savedElsewhere = Clone(editor.Project, "Saved elsewhere", "newer words");
            savedElsewhere.Touch();
            await repository.SaveAsync(savedElsewhere, CancellationToken.None);

            await Assert.ThrowsAsync<StaleProjectSessionException>(() =>
                workspace.UpdateAsync(staleEditor, staleRevision, CancellationToken.None));

            Assert.Equal("Saved elsewhere", (await repository.LoadAsync(editor.Project.Id, CancellationToken.None))!.Title);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task ConcurrentSnapshotAndExplicitSave_CannotLeaveAStaleRecoverySnapshot()
    {
        var directory = NewDirectory();
        try
        {
            var repository = new JsonFileProjectRepository(directory);
            var project = SongProject.Create("Concurrent recovery");
            await repository.SaveAsync(project, CancellationToken.None);
            var baseRevision = project.LastModifiedUtc;
            var unsaved = Clone(project, "Unsaved snapshot", new string('w', 50_000));
            project.Rename("Explicit save wins");

            var snapshotTask = repository.SaveRecoverySnapshotAsync(
                new ProjectRecoverySnapshot(unsaved, DateTimeOffset.UtcNow, baseRevision, "snapshot-session"),
                CancellationToken.None);
            var saveTask = repository.SaveAsync(project, CancellationToken.None);
            try { await snapshotTask; }
            catch (StaleProjectSessionException) { }
            await saveTask;

            Assert.Null(await repository.LoadRecoverySnapshotAsync(project.Id, CancellationToken.None));
            Assert.Equal("Explicit save wins", (await repository.LoadAsync(project.Id, CancellationToken.None))!.Title);
        }
        finally { DeleteDirectory(directory); }
    }

    [Fact]
    public async Task LoadAsync_MissingTimestamps_StayStableAcrossLoadsForSessionChecks()
    {
        var directory = NewDirectory();
        var projectId = ProjectId.New();
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{projectId}.json");
            var original = $$"""
            {
              "id": "{{projectId}}",
              "schemaVersion": 1,
              "title": "Legacy Timestamps",
              "tempo": { "beat": 0, "beatsPerMinute": 120 },
              "timeSignature": { "beat": 0, "numerator": 4, "denominator": 4 }
            }
            """;
            await File.WriteAllTextAsync(path, original);
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));

            var repository = new JsonFileProjectRepository(directory);
            var first = await repository.LoadAsync(projectId, CancellationToken.None);
            await Task.Delay(20);
            var second = await repository.LoadAsync(projectId, CancellationToken.None);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first.LastModifiedUtc, second.LastModifiedUtc);
            Assert.Equal(first.CreatedUtc, second.CreatedUtc);
            Assert.Equal(new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero), first.LastModifiedUtc);

            var unsaved = Clone(first, "Edited locally", "draft words");
            await repository.SaveRecoverySnapshotAsync(
                new ProjectRecoverySnapshot(unsaved, DateTimeOffset.UtcNow, first.LastModifiedUtc, "legacy-session"),
                CancellationToken.None);

            var workspace = new ProjectWorkspace(repository);
            var saved = await workspace.UpdateAsync(unsaved, first.LastModifiedUtc, CancellationToken.None);
            Assert.NotNull(saved);
            Assert.Equal("Edited locally", saved.Project.Title);
        }
        finally { DeleteDirectory(directory); }
    }

    private static SongProject Clone(SongProject source, string title, string rawLyricDraft) => new(
        source.Id,
        source.SchemaVersion,
        title,
        new SongTimeline(
            TimelineResolution.TicksPerQuarterNote,
            new TempoMap(source.Timeline.TempoMap.Events),
            new TimeSignatureMap(source.Timeline.TimeSignatureMap.Events),
            source.Timeline.SectionPlacements),
        source.Sections,
        source.Tracks,
        source.Artist,
        source.Genre,
        source.Description,
        rawLyricDraft,
        source.CreatedUtc,
        source.LastModifiedUtc);

    private static string NewDirectory() => Path.Combine(Path.GetTempPath(), $"maskil-forge-{Guid.NewGuid():N}");
    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
