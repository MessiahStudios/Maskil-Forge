namespace MaskilForge.Api;

public static class ProjectLibraryPath
{
    private const string ConfigurationKey = "MaskilForge:LibraryPath";

    public static string Resolve(
        string contentRootPath,
        bool isDevelopment,
        string? configuredPath = null,
        string? localApplicationDataPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!Path.IsPathFullyQualified(configuredPath))
                throw new InvalidOperationException(
                    $"{ConfigurationKey} must be an absolute path so the project library cannot move with the process working directory.");

            return Path.GetFullPath(configuredPath);
        }

        if (isDevelopment)
            return Path.GetFullPath(Path.Combine(contentRootPath, "App_Data", "projects"));

        var applicationData = localApplicationDataPath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData))
            throw new InvalidOperationException(
                $"A per-user application-data directory is unavailable. Configure {ConfigurationKey} with an absolute path.");

        return Path.GetFullPath(Path.Combine(applicationData, "Maskil Forge", "Library"));
    }
}
