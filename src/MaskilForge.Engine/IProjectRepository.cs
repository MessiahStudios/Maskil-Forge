using MaskilForge.Domain;

namespace MaskilForge.Engine;

public interface IProjectRepository
{
    Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrashedProjectSummary>> ListTrashAsync(CancellationToken cancellationToken = default);
    Task<SongProject?> LoadAsync(ProjectId id, CancellationToken cancellationToken = default);
    Task SaveAsync(SongProject project, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectRecoverySummary>> ListRecoverySnapshotsAsync(CancellationToken cancellationToken = default);
    Task<ProjectRecoverySnapshot?> LoadRecoverySnapshotAsync(ProjectId id, CancellationToken cancellationToken = default);
    Task SaveRecoverySnapshotAsync(ProjectRecoverySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<bool> DeleteRecoverySnapshotAsync(ProjectId id, CancellationToken cancellationToken = default);
    Task<bool> MoveToTrashAsync(ProjectId id, CancellationToken cancellationToken = default);
    Task<bool> RestoreFromTrashAsync(ProjectId id, CancellationToken cancellationToken = default);
    Task<bool> PermanentlyDeleteAsync(ProjectId id, CancellationToken cancellationToken = default);
}

public sealed record ProjectSummary(
    ProjectId Id,
    string Title,
    string Artist,
    SongGenre Genre,
    DateTimeOffset LastModifiedUtc,
    int SectionCount,
    bool HasRawLyrics);

public sealed record TrashedProjectSummary(
    ProjectId Id,
    string Title,
    string Artist,
    DateTimeOffset DeletedAtUtc);

public sealed record ProjectRecoverySnapshot(
    SongProject Project,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset BaseProjectLastModifiedUtc,
    string SessionId);

public sealed record ProjectRecoverySummary(
    ProjectId Id,
    string Title,
    string Artist,
    DateTimeOffset CapturedAtUtc,
    int SectionCount,
    int LyricLineCount,
    bool HasRawLyrics,
    IReadOnlyList<string> SectionTitles);
