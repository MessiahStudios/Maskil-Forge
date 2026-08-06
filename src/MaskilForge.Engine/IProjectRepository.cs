using MaskilForge.Domain;

namespace MaskilForge.Engine;

public interface IProjectRepository
{
    Task<SongProject?> LoadAsync(ProjectId id, CancellationToken cancellationToken = default);
    Task SaveAsync(SongProject project, CancellationToken cancellationToken = default);
}
