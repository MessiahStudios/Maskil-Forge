using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Infrastructure;

public sealed class JsonFileProjectRepository(string directory) : IProjectRepository
{
    private static readonly ProjectMigrationPipeline MigrationPipeline = ProjectMigrationPipeline.CreateCurrent();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly ConcurrentDictionary<ProjectId, SemaphoreSlim> _projectLocks = new();

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
        var projectLock = _projectLocks.GetOrAdd(project.Id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        try
        {
            await SaveWithoutLockAsync(project, cancellationToken);
            var recoveryPath = GetSessionRecoveryPath(project.Id);
            if (File.Exists(recoveryPath)) File.Delete(recoveryPath);
        }
        finally { projectLock.Release(); }
    }

    public async Task SaveWithAssetAsync(
        SongProject project,
        ProjectAsset asset,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead) throw new ArgumentException("Project asset content must be readable.", nameof(content));
        if (project.Assets.All(item => item != asset))
            throw new InvalidOperationException("The project must register the exact asset metadata before its immutable bytes are saved.");

        var projectLock = _projectLocks.GetOrAdd(project.Id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        var assetDirectory = GetAssetDirectory(GetPath(project.Id));
        var assetPath = GetAssetPath(assetDirectory, asset.Id);
        var temporaryPath = assetPath + $".tmp-{Guid.NewGuid():N}";
        try
        {
            if (File.Exists(assetPath)) throw new InvalidOperationException($"Project asset '{asset.Id}' is immutable and already exists.");
            Directory.CreateDirectory(assetDirectory);
            await using (var destination = File.Create(temporaryPath))
            {
                await content.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }
            await ValidateAssetFileAsync(asset, temporaryPath, cancellationToken);
            File.Move(temporaryPath, assetPath);
            try { await SaveWithoutLockAsync(project, cancellationToken); }
            catch
            {
                File.Delete(assetPath);
                DeleteDirectoryIfEmpty(assetDirectory);
                throw;
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            projectLock.Release();
        }
    }

    public async Task SaveWithoutAssetAsync(
        SongProject project,
        ProjectAsset asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(asset);
        if (project.Assets.Any(item => item.Id == asset.Id))
            throw new InvalidOperationException("The project must remove the asset manifest entry before its bytes are removed.");

        var projectLock = _projectLocks.GetOrAdd(project.Id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        var path = GetPath(project.Id);
        var assetDirectory = GetAssetDirectory(path);
        var assetPath = GetAssetPath(assetDirectory, asset.Id);
        var stagedRemovalPath = assetPath + $".remove-{Guid.NewGuid():N}";
        var replacementCommitted = false;
        try
        {
            var persisted = await ReadProjectAsync(path, project.Id, true, cancellationToken)
                ?? throw new InvalidProjectDataException("The saved project was not found.");
            var persistedAsset = persisted.Assets.SingleOrDefault(item => item.Id == asset.Id)
                ?? throw new InvalidProjectDataException($"Project asset '{asset.Id}' is not registered in the saved project.");
            if (persistedAsset != asset)
                throw new InvalidProjectDataException($"Project asset '{asset.Id}' metadata does not match the saved project.");
            if (!persisted.Assets.Where(item => item.Id != asset.Id).Select(item => item.Id)
                    .SequenceEqual(project.Assets.Select(item => item.Id)))
                throw new InvalidOperationException("Only the selected project asset can be removed in this save.");

            await ValidateAssetFileAsync(asset, assetPath, cancellationToken);

            Directory.CreateDirectory(GetBackupDirectory());
            var backupPath = GetBackupPath(project.Id);
            File.Copy(path, backupPath, true);
            MirrorAssetDirectory(assetDirectory, GetAssetDirectory(backupPath));

            File.Move(assetPath, stagedRemovalPath);
            try
            {
                await WriteProjectReplacementWithoutBackupAsync(project, path, cancellationToken);
                replacementCommitted = true;
            }
            catch
            {
                File.Move(stagedRemovalPath, assetPath);
                throw;
            }

            File.Delete(stagedRemovalPath);
            DeleteDirectoryIfEmpty(assetDirectory);
            var recoveryPath = GetSessionRecoveryPath(project.Id);
            if (File.Exists(recoveryPath)) File.Delete(recoveryPath);
            DeleteAssetDirectory(GetAssetDirectory(recoveryPath));
        }
        catch (OperationCanceledException) { throw; }
        catch (ProjectPersistenceException) { throw; }
        catch (Exception exception)
        {
            throw new ProjectSaveException("The project asset could not be removed. The previous saved version remains available in the local backup.", exception);
        }
        finally
        {
            if (!replacementCommitted && File.Exists(stagedRemovalPath) && !File.Exists(assetPath))
                File.Move(stagedRemovalPath, assetPath);
            projectLock.Release();
        }
    }

    public async Task<Stream?> OpenAssetAsync(
        ProjectId projectId,
        ProjectAssetId assetId,
        CancellationToken cancellationToken = default)
    {
        var project = await LoadAsync(projectId, cancellationToken);
        var asset = project?.Assets.SingleOrDefault(item => item.Id == assetId);
        if (asset is null) return null;
        var path = GetAssetPath(GetAssetDirectory(GetPath(projectId)), assetId);
        await ValidateAssetFileAsync(asset, path, cancellationToken);
        return File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task ImportAsync(SongProject project, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Assets.Count > 0)
            throw new InvalidOperationException(
                "This project references external media. Import an asset-owning .maskil package instead.");
        await ImportCoreAsync(project, null, cancellationToken);
    }

    public async Task ImportWithAssetsAsync(
        SongProject project,
        IReadOnlyDictionary<ProjectAssetId, byte[]> assets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(assets);
        PortableProjectPackage.ValidateCompleteAssetSet(project, assets);
        await ImportCoreAsync(project, assets, cancellationToken);
    }

    private async Task ImportCoreAsync(
        SongProject project,
        IReadOnlyDictionary<ProjectAssetId, byte[]>? assets,
        CancellationToken cancellationToken)
    {
        var projectLock = _projectLocks.GetOrAdd(project.Id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        var assetDirectory = GetAssetDirectory(GetPath(project.Id));
        var temporaryDirectory = assetDirectory + $".tmp-{Guid.NewGuid():N}";
        try
        {
            if (ProjectIdentityExists(project.Id))
                throw new InvalidOperationException(
                    "A project with this identity already exists in the song library, Trash, backup, or recovery data. Nothing was overwritten.");
            if (assets is { Count: > 0 })
            {
                Directory.CreateDirectory(temporaryDirectory);
                foreach (var asset in project.Assets)
                {
                    var path = GetAssetPath(temporaryDirectory, asset.Id);
                    await File.WriteAllBytesAsync(path, assets[asset.Id], cancellationToken);
                    await ValidateAssetFileAsync(asset, path, cancellationToken);
                }
                DeleteAssetDirectory(assetDirectory);
                Directory.Move(temporaryDirectory, assetDirectory);
            }
            try { await SaveWithoutLockAsync(project, cancellationToken); }
            catch
            {
                DeleteAssetDirectory(assetDirectory);
                throw;
            }
        }
        finally
        {
            DeleteAssetDirectory(temporaryDirectory);
            projectLock.Release();
        }
    }

    public Task<bool> ProjectIdentityExistsAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProjectIdentityExists(id));
    }

    private async Task SaveWithoutLockAsync(SongProject project, CancellationToken cancellationToken)
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
                    var backupPath = GetBackupPath(project.Id);
                    File.Copy(path, backupPath, true);
                    MirrorAssetDirectory(GetAssetDirectory(path), GetAssetDirectory(backupPath));
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

    private async Task WriteProjectReplacementWithoutBackupAsync(
        SongProject project,
        string path,
        CancellationToken cancellationToken)
    {
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
                    snapshot.CapturedAtUtc,
                    snapshot.Project.Sections.Count,
                    snapshot.Project.Sections.Count > 0
                        ? snapshot.Project.Sections.Sum(section => section.LyricLines.Count)
                        : snapshot.Project.RawLyricDraft.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line)),
                    !string.IsNullOrWhiteSpace(snapshot.Project.RawLyricDraft),
                    snapshot.Project.Sections.Select(section => section.Title).ToList()));
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
        var projectLock = _projectLocks.GetOrAdd(snapshot.Project.Id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        try { await SaveRecoverySnapshotWithoutLockAsync(snapshot, cancellationToken); }
        finally { projectLock.Release(); }
    }

    private async Task SaveRecoverySnapshotWithoutLockAsync(ProjectRecoverySnapshot snapshot, CancellationToken cancellationToken)
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
            MirrorAssetDirectory(
                GetAssetDirectory(GetPath(snapshot.Project.Id)),
                GetAssetDirectory(path));
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

    public async Task<bool> DeleteRecoverySnapshotAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var projectLock = _projectLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        try
        {
            var path = GetSessionRecoveryPath(id);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            DeleteAssetDirectory(GetAssetDirectory(path));
            return true;
        }
        finally { projectLock.Release(); }
    }

    public async Task<bool> MoveToTrashAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var projectLock = _projectLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        try
        {
            var source = GetPath(id);
            if (!File.Exists(source)) return false;
            var trashDirectory = GetTrashDirectory();
            Directory.CreateDirectory(trashDirectory);
            var destination = Path.Combine(trashDirectory, $"{id}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Move(source, destination);
            var sourceAssets = GetAssetDirectory(source);
            var destinationAssets = GetAssetDirectory(destination);
            try
            {
                if (Directory.Exists(sourceAssets)) Directory.Move(sourceAssets, destinationAssets);
            }
            catch
            {
                File.Move(destination, source);
                throw;
            }
            var sessionRecoveryPath = GetSessionRecoveryPath(id);
            if (File.Exists(sessionRecoveryPath)) File.Delete(sessionRecoveryPath);
            DeleteAssetDirectory(GetAssetDirectory(sessionRecoveryPath));
            return true;
        }
        finally { projectLock.Release(); }
    }

    public async Task<bool> RestoreFromTrashAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var projectLock = _projectLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        try
        {
            var source = FindTrashPath(id);
            if (source is null) return false;
            Directory.CreateDirectory(_directory);
            var destination = GetPath(id);
            if (File.Exists(destination)) throw new InvalidOperationException("A project with this ID already exists in the song library.");
            File.Move(source, destination);
            var sourceAssets = GetAssetDirectory(source);
            var destinationAssets = GetAssetDirectory(destination);
            try
            {
                if (Directory.Exists(sourceAssets)) Directory.Move(sourceAssets, destinationAssets);
            }
            catch
            {
                File.Move(destination, source);
                throw;
            }
            return true;
        }
        finally { projectLock.Release(); }
    }

    public async Task<bool> PermanentlyDeleteAsync(ProjectId id, CancellationToken cancellationToken = default)
    {
        var projectLock = _projectLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        await projectLock.WaitAsync(cancellationToken);
        try
        {
            var path = FindTrashPath(id);
            if (path is null) return false;
            File.Delete(path);
            DeleteAssetDirectory(GetAssetDirectory(path));
            var backupPath = GetBackupPath(id);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            DeleteAssetDirectory(GetAssetDirectory(backupPath));
            var sessionPath = GetSessionRecoveryPath(id);
            if (File.Exists(sessionPath)) File.Delete(sessionPath);
            DeleteAssetDirectory(GetAssetDirectory(sessionPath));
            if (Directory.Exists(GetRecoveryDirectory()))
            {
                foreach (var recoveryPath in Directory.EnumerateFiles(GetRecoveryDirectory(), $"{id}-*.json"))
                {
                    File.Delete(recoveryPath);
                    DeleteAssetDirectory(GetAssetDirectory(recoveryPath));
                }
            }
            return true;
        }
        finally { projectLock.Release(); }
    }

    private string GetTrashDirectory() => Path.Combine(_directory, "trash");
    private string GetBackupDirectory() => Path.Combine(_directory, "backups");
    private string GetRecoveryDirectory() => Path.Combine(_directory, "recovery");
    private string GetSessionRecoveryDirectory() => Path.Combine(_directory, "sessions");
    private string GetBackupPath(ProjectId id) => Path.Combine(GetBackupDirectory(), $"{id}.json");
    private string GetSessionRecoveryPath(ProjectId id) => Path.Combine(GetSessionRecoveryDirectory(), $"{id}.json");
    private bool HasRecoveryCopy(ProjectId id) => Directory.Exists(GetRecoveryDirectory())
        && Directory.EnumerateFiles(GetRecoveryDirectory(), $"{id}-*.json").Any();
    private bool ProjectIdentityExists(ProjectId id) => File.Exists(GetPath(id))
        || Directory.Exists(GetAssetDirectory(GetPath(id)))
        || File.Exists(GetBackupPath(id))
        || File.Exists(GetSessionRecoveryPath(id))
        || HasRecoveryCopy(id)
        || FindTrashPath(id) is not null;
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
            EnsureStableTimestamps(normalized, path);
            var project = normalized.Deserialize<SongProject>(JsonOptions)
                ?? throw new InvalidProjectDataException("The project file contains no project data.");
            if (expectedId is not null && project.Id != expectedId.Value)
                throw new InvalidProjectDataException("The project ID does not match its requested identity.");
            await ValidateProjectAssetsAsync(project, path, cancellationToken);
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
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var envelope = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidProjectDataException("The recovery snapshot root must be a JSON object.");
            var project = envelope["project"] as JsonObject
                ?? throw new InvalidProjectDataException("The recovery snapshot contains no project data.");
            var normalized = MigrationPipeline.Normalize(project);
            EnsureStableTimestamps(normalized, path);
            envelope["project"] = normalized;
            var snapshot = envelope.Deserialize<ProjectRecoverySnapshot>(JsonOptions)
                ?? throw new InvalidProjectDataException("The recovery snapshot contains no project data.");
            if (snapshot.CapturedAtUtc == default || snapshot.BaseProjectLastModifiedUtc == default || string.IsNullOrWhiteSpace(snapshot.SessionId))
                throw new InvalidProjectDataException("The recovery snapshot metadata is invalid.");
            await ValidateProjectAssetsAsync(snapshot.Project, path, cancellationToken);
            return snapshot;
        }
        catch (OperationCanceledException) { throw; }
        catch (ProjectPersistenceException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidProjectDataException("The recovery snapshot is invalid.", null, exception);
        }
    }

    private static void EnsureStableTimestamps(JsonObject project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        var fallback = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        if (!HasTimestamp(project, "createdUtc"))
            project["createdUtc"] = fallback;
        if (!HasTimestamp(project, "lastModifiedUtc"))
            project["lastModifiedUtc"] = project["createdUtc"]!.DeepClone();
    }

    private static bool HasTimestamp(JsonObject project, string name)
    {
        if (!project.TryGetPropertyValue(name, out var node) || node is null)
            return false;
        try
        {
            var value = node.Deserialize<DateTimeOffset?>(JsonOptions);
            return value is not null && value.Value != default;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task ValidateProjectAssetsAsync(
        SongProject project,
        string documentPath,
        CancellationToken cancellationToken)
    {
        if (project.Assets.Count == 0) return;
        var assetDirectory = GetAssetDirectory(documentPath);
        if (!Directory.Exists(assetDirectory))
            throw new InvalidProjectDataException("The project asset directory is missing. Original media was not treated as optional.");
        foreach (var asset in project.Assets)
            await ValidateAssetFileAsync(asset, GetAssetPath(assetDirectory, asset.Id), cancellationToken);
    }

    private static async Task ValidateAssetFileAsync(
        ProjectAsset asset,
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            throw new InvalidProjectDataException($"Project asset '{asset.Id}' is missing.");
        var info = new FileInfo(path);
        if (info.Length != asset.ByteLength)
            throw new InvalidProjectDataException($"Project asset '{asset.Id}' byte length does not match its manifest.");
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        if (!string.Equals(digest, asset.Sha256, StringComparison.Ordinal))
            throw new InvalidProjectDataException($"Project asset '{asset.Id}' SHA-256 does not match its manifest.");
    }

    private static string GetAssetDirectory(string documentPath)
    {
        var stablePath = documentPath.EndsWith(".tmp", StringComparison.Ordinal)
            ? documentPath[..^4]
            : documentPath;
        var parent = Path.GetDirectoryName(stablePath)
            ?? throw new InvalidOperationException("A project document directory is required.");
        return Path.Combine(parent, $"{Path.GetFileNameWithoutExtension(stablePath)}.assets");
    }

    private static string GetAssetPath(string assetDirectory, ProjectAssetId assetId) =>
        Path.Combine(assetDirectory, $"{assetId}.bin");

    private static void MirrorAssetDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            DeleteAssetDirectory(destination);
            return;
        }

        var temporary = destination + $".tmp-{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(temporary);
            foreach (var file in Directory.EnumerateFiles(source))
                File.Copy(file, Path.Combine(temporary, Path.GetFileName(file)), true);
            DeleteAssetDirectory(destination);
            Directory.Move(temporary, destination);
        }
        finally { DeleteAssetDirectory(temporary); }
    }

    private static void DeleteAssetDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static void DeleteDirectoryIfEmpty(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path);
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
            if (!File.Exists(destination))
            {
                File.Copy(source, destination);
                MirrorAssetDirectory(GetAssetDirectory(source), GetAssetDirectory(destination));
            }
            return fileName;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
