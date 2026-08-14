namespace MaskilForge.Api;

public sealed record WebClientDistribution(string RootPath, string IndexPath, bool IsAvailable)
{
    public static WebClientDistribution Locate(string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var publishedRoot = Path.GetFullPath(Path.Combine(contentRootPath, "wwwroot"));
        var sourceRoot = Path.GetFullPath(Path.Combine(contentRootPath, "..", "MaskilForge.Web", "dist"));
        var rootPath = File.Exists(Path.Combine(publishedRoot, "index.html")) ? publishedRoot : sourceRoot;
        var indexPath = Path.Combine(rootPath, "index.html");
        return new WebClientDistribution(rootPath, indexPath, File.Exists(indexPath));
    }
}
