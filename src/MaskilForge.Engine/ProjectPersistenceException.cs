namespace MaskilForge.Engine;

public abstract class ProjectPersistenceException(
    string message,
    string code,
    string? recoveryCopyFileName = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public string? RecoveryCopyFileName { get; } = recoveryCopyFileName;
}

public sealed class UnsupportedProjectSchemaException(int version, int currentVersion)
    : ProjectPersistenceException(
        $"This project uses schema version {version}, but this version of Maskil Forge supports up to version {currentVersion}.",
        "unsupported_schema")
{
    public int Version { get; } = version;
    public int CurrentVersion { get; } = currentVersion;
}

public sealed class CorruptProjectException(string message, string? recoveryCopyFileName = null, Exception? innerException = null)
    : ProjectPersistenceException(message, "corrupt_project", recoveryCopyFileName, innerException);

public sealed class InvalidProjectDataException(string message, string? recoveryCopyFileName = null, Exception? innerException = null)
    : ProjectPersistenceException(message, "invalid_project_data", recoveryCopyFileName, innerException);

public sealed class ProjectSaveException(string message, Exception? innerException = null)
    : ProjectPersistenceException(message, "save_failed", null, innerException);

public sealed class StaleProjectSessionException()
    : ProjectPersistenceException(
        "This song was saved by another session. Reload it before saving or replacing its recovery snapshot.",
        "stale_session");
