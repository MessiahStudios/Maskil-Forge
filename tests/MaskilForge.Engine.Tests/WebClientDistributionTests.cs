using MaskilForge.Api;

namespace MaskilForge.Engine.Tests;

public sealed class WebClientDistributionTests
{
    [Fact]
    public void Locate_RequiresAnEntryPointAndPrefersPublishedFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"maskil-forge-host-{Guid.NewGuid():N}");
        var apiDirectory = Path.Combine(directory, "MaskilForge.Api");
        var sourceDistribution = Path.Combine(directory, "MaskilForge.Web", "dist");
        var publishedDistribution = Path.Combine(apiDirectory, "wwwroot");
        Directory.CreateDirectory(apiDirectory);

        try
        {
            var missing = WebClientDistribution.Locate(apiDirectory);
            Assert.False(missing.IsAvailable);
            Assert.Equal(Path.Combine(sourceDistribution, "index.html"), missing.IndexPath);

            Directory.CreateDirectory(sourceDistribution);
            File.WriteAllText(missing.IndexPath, "<!doctype html><title>Source build</title>");

            var sourceBuild = WebClientDistribution.Locate(apiDirectory);
            Assert.True(sourceBuild.IsAvailable);
            Assert.Equal(Path.GetFullPath(sourceDistribution), sourceBuild.RootPath);

            Directory.CreateDirectory(publishedDistribution);
            File.WriteAllText(Path.Combine(publishedDistribution, "index.html"), "<!doctype html><title>Published build</title>");

            var publishedBuild = WebClientDistribution.Locate(apiDirectory);
            Assert.True(publishedBuild.IsAvailable);
            Assert.Equal(Path.GetFullPath(publishedDistribution), publishedBuild.RootPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
