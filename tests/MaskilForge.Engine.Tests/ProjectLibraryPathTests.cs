using MaskilForge.Api;

namespace MaskilForge.Engine.Tests;

public sealed class ProjectLibraryPathTests
{
    [Fact]
    public void Resolve_KeepsDevelopmentProjectsInTheExistingIgnoredDirectory()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "maskil-forge-api");

        var result = ProjectLibraryPath.Resolve(contentRoot, isDevelopment: true);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "projects")),
            result);
    }

    [Fact]
    public void Resolve_UsesAStablePerUserLibraryOutsideTheApplicationInProduction()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "maskil-forge-api");
        var localApplicationData = Path.Combine(Path.GetTempPath(), "maskil-forge-user-data");

        var result = ProjectLibraryPath.Resolve(
            contentRoot,
            isDevelopment: false,
            localApplicationDataPath: localApplicationData);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(localApplicationData, "Maskil Forge", "Library")),
            result);
    }

    [Fact]
    public void Resolve_RequiresAnAbsoluteConfiguredLibraryPath()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "maskil-forge-api");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProjectLibraryPath.Resolve(contentRoot, isDevelopment: false, configuredPath: "relative-library"));

        Assert.Contains("must be an absolute path", exception.Message);
    }

    [Fact]
    public void Resolve_PrefersAnExplicitAbsoluteLibraryPathInEveryEnvironment()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "maskil-forge-api");
        var configuredPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "artist-maskil-library"));

        var result = ProjectLibraryPath.Resolve(
            contentRoot,
            isDevelopment: true,
            configuredPath: configuredPath);

        Assert.Equal(configuredPath, result);
    }
}
