using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Infrastructure;

public sealed class JsonFileProjectRepository(string directory) : IProjectRepository
{
    private static readonly ProjectMigrationPipeline MigrationPipeline = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _directory = string.IsNullOrWhiteSpace(directory)
        ? throw new ArgumentException("A persistence directory is required.", nameof(directory))
        : Path.GetFullPath(directory);

    public async Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory)) return [];
        var summaries = new List<ProjectSummary>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SongProject? project;
            try { project = await ReadProjectAsync(path, null, true, cancellationToken); }
            catch (ProjectPersistenceException) { continue; }
            if (project is not null)
                summaries.Add(new ProjectSummary(project.Id, project.Title, project.Artist, project.Genre, project.LastModifiedUtc, project.Sections.Count, !string.IsNullOrWhiteSpace(project.RawLyricDraft)));
        }
        return summaries.OrderByDescending(project => project.LastModifiedUtc).ToList();
    }

    public async Task<IReadOnlyList<TrashedProjectSummary>> ListTrashAsync(CancellationToken cancellationToken = default)
    {
        var trashDirectory = GetTrashDirectory();
        if (!Directory.Exists(trashDirectory)) return [];
        var summaries = new List<TrashedProjectSummary>();
        foreach (var path in Directory.EnumerateFiles(trashDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SongProject? project;
            try { project = await ReadProjectAsync(path, null, true, cancellationToken); }
            catch (ProjectPersistenceException) { continue; }
            if (project is not null)
                summaries.Add(new TrashedProjectSummary(project.Id, project.Title, project.Artist, File.GetLastWriteTimeUtc(path)));
        }
        return summaries.OrderByDescending(project => project.DeletedAtUtc).ToList();
    }

    public async Task<SongProject?> LoadAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path)) return null;
        return await ReadProjectAsync(path, id, true, cancellationToken);
    }

    public async Task SaveAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        Directory.CreateDirectory(_directory);
        var path = GetPath(project.Id);
        var temporaryPath = path + ".tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, project, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            _ = await ReadProjectAsync(temporaryPath, project.Id, false, cancellationToken)
                ?? throw new ProjectSaveException("The temporary project file could not be validated.");

            if (File.Exists(path))
            {
                try
                {
                    _ = await ReadProjectAsync(path, project.Id, true, cancellationToken);
                    Directory.CreateDirectory(GetBackupDirectory());
                    File.Copy(path, GetBackupPath(project.Id), true);
                }
                catch (UnsupportedProjectSchemaException exception)
                {
                    throw new ProjectSaveException(
                        "The existing project was created by a newer version and was not replaced.", exception);
                }
                catch (ProjectPersistenceException)
                {
                    // ReadProjectAsync has preserved the invalid active file for recovery.
                    // Keep any existing known-good backup and continue with the validated save.
                }
            }
            File.Move(temporaryPath, path, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (ProjectSaveException) { throw; }
        catch (Exception exception)
        {
            throw new ProjectSaveException("The project could not be saved. The previous project file was left in place.", exception);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public async Task<IReadOnlyList<ProjectRecoverySummary>> ListRecoverySnapshotsAsync(CancellationToken cancellationToken = default)
    {
        var recoveryDirectory = GetSessionRecoveryDirectory();
        if (!Directory.Exists(recoveryDirectory)) return [];
        var summaries = new List<ProjectRecoverySummary>();
        foreach (var path in Directory.EnumerateFiles(recoveryDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var snapshot = await ReadRecoverySnapshotAsync(path, cancellationToken);
                summaries.Add(new ProjectRecoverySummary(
                    snapshot.Project.Id,
                    snapshot.Project.Title,
                    snapshot.Project.Artist,
                    snapshot.CapturedAtUtc));
            }
            catch (ProjectPersistenceException) { }
        }
        return summaries.OrderByDescending(item => item.CapturedAtUtc).ToList();
    }

    public async Task<ProjectRecoverySnapshot?> LoadRecoverySnapshotAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var path = GetSessionRecoveryPath(id);
        return File.Exists(path) ? await ReadRecoverySnapshotAsync(path, cancellationToken) : null;
    }

    public async Task SaveRecoverySnapshotAsync(ProjectRecoverySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Project);
        if (string.IsNullOrWhiteSpace(snapshot.SessionId)) throw new ArgumentException("A recovery session ID is required.", nameof(snapshot));
        var saved = await LoadAsync(snapshot.Project.Id, cancellationToken)
            ?? throw new InvalidProjectDataException("The saved project for this recovery snapshot was not found.");
        if (saved.LastModifiedUtc != snapshot.BaseProjectLastModifiedUtc)
            throw new StaleProjectSessionException();

        var directory = GetSessionRecoveryDirectory();
        Directory.CreateDirectory(directory);
        var path = GetSessionRecoveryPath(snapshot.Project.Id);
        var temporaryPath = path + ".tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            _ = await ReadRecoverySnapshotAsync(temporaryPath, cancellationToken);
            File.Move(temporaryPath, path, true);
        }
        catch (OperationCanceledException) { throw; }
        catch (StaleProjectSessionException) { throw; }
        catch (Exception exception)
        {
            throw new ProjectSaveException("The recovery snapshot could not be saved.", exception);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public Task<bool> DeleteRecoverySnapshotAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetSessionRecoveryPath(id);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    public Task<bool> MoveToTrashAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = GetPath(id);
        if (!File.Exists(source)) return Task.FromResult(false);
        var trashDirectory = GetTrashDirectory();
        Directory.CreateDirectory(trashDirectory);
        var destination = Path.Combine(trashDirectory, $"{id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
        File.Move(source, destination);
        var sessionRecoveryPath = GetSessionRecoveryPath(id);
        if (File.Exists(sessionRecoveryPath)) File.Delete(sessionRecoveryPath);
        return Task.FromResult(true);
    }

    public Task<bool> RestoreFromTrashAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = FindTrashPath(id);
        if (source is null) return Task.FromResult(false);
        Directory.CreateDirectory(_directory);
        var destination = GetPath(id);
        if (File.Exists(destination)) throw new InvalidOperationException("A project with this ID already exists in the song library.");
        File.Move(source, destination);
        return Task.FromResult(true);
    }

    public Task<bool> PermanentlyDeleteAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = FindTrashPath(id);
        if (path is null) return Task.FromResult(false);
        File.Delete(path);
        var backupPath = GetBackupPath(id);
        if (File.Exists(backupPath)) File.Delete(backupPath);
        if (Directory.Exists(GetRecoveryDirectory()))
        {
            foreach (var recoveryPath in Directory.EnumerateFiles(GetRecoveryDirectory(), $"{id}-*.json"))
                File.Delete(recoveryPath);
        }
        return Task.FromResult(true);
    }

    private string GetTrashDirectory() => Path.Combine(_directory, "trash");
    private string GetBackupDirectory() => Path.Combine(_directory, "backups");
    private string GetRecoveryDirectory() => Path.Combine(_directory, "recovery");
    private string GetSessionRecoveryDirectory() => Path.Combine(_directory, "sessions");
    private string GetBackupPath(ProjectId id) => Path.Combine(GetBackupDirectory(), $"{id}.json");
    private string GetSessionRecoveryPath(ProjectId id) => Path.Combine(GetSessionRecoveryDirectory(), $"{id}.json");
    private string? FindTrashPath(ProjectId id) => Directory.Exists(GetTrashDirectory())
        ? Directory.EnumerateFiles(GetTrashDirectory(), $"{id}-*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
        : null;

    private string GetPath(ProjectId id) => Path.Combine(_directory, $"{id}.json");

    private async Task<SongProject?> ReadProjectAsync(
        string path,
        ProjectId? expectedId,
        bool createRecoveryCopy,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var normalized = MigrationPipeline.Normalize(ProjectMigrationPipeline.Parse(json));
            var project = normalized.Deserialize<SongProject>(JsonOptions)
                ?? throw new InvalidProjectDataException("The project file contains no project data.");
            if (expectedId is not null && project.Id != expectedId.Value)
                throw new InvalidProjectDataException("The project ID does not match its requested identity.");
            return project;
        }
        catch (OperationCanceledException) { throw; }
        catch (UnsupportedProjectSchemaException) { throw; }
        catch (CorruptProjectException exception)
        {
            var recovery = createRecoveryCopy ? CreateRecoveryCopy(path) : null;
            throw new CorruptProjectException(
                recovery is null ? exception.Message : $"{exception.Message} A recovery copy was created.",
                recovery,
                exception);
        }
        catch (ProjectPersistenceException exception)
        {
            var recovery = createRecoveryCopy ? CreateRecoveryCopy(path) : null;
            throw new InvalidProjectDataException(
                recovery is null ? exception.Message : $"{exception.Message} A recovery copy was created.",
                recovery,
                exception);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            var recovery = createRecoveryCopy ? CreateRecoveryCopy(path) : null;
            throw new InvalidProjectDataException(
                recovery is null ? "The project data is invalid." : "The project data is invalid. A recovery copy was created.",
                recovery,
                exception);
        }
    }

    private async Task<ProjectRecoverySnapshot> ReadRecoverySnapshotAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var snapshot = await JsonSerializer.DeserializeAsync<ProjectRecoverySnapshot>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidProjectDataException("The recovery snapshot contains no project data.");
            if (snapshot.Project.SchemaVersion.Value > SchemaVersion.Current.Value)
                throw new UnsupportedProjectSchemaException(snapshot.Project.SchemaVersion.Value, SchemaVersion.Current.Value);
            if (snapshot.CapturedAtUtc == default || snapshot.BaseProjectLastModifiedUtc == default || string.IsNullOrWhiteSpace(snapshot.SessionId))
                throw new InvalidProjectDataException("The recovery snapshot metadata is invalid.");
            return snapshot;
        }
        catch (OperationCanceledException) { throw; }
        catch (ProjectPersistenceException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidProjectDataException("The recovery snapshot is invalid.", null, exception);
        }
    }

    private string? CreateRecoveryCopy(string source)
    {
        try
        {
            Directory.CreateDirectory(GetRecoveryDirectory());
            using var stream = File.OpenRead(source);
            var fingerprint = Convert.ToHexString(SHA256.HashData(stream))[..16].ToLowerInvariant();
            var fileName = $"{Path.GetFileNameWithoutExtension(source)}-{fingerprint}.json";
            var destination = Path.Combine(GetRecoveryDirectory(), fileName);
            if (!File.Exists(destination)) File.Copy(source, destination);
            return fileName;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
