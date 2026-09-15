namespace MaskilForge.Infrastructure;

public sealed record Vst3SearchLocation(string Name, string Hint, string Path);
public sealed record Vst3HostPaths(string Platform, string UserHome, string LocalAppData,
    string CommonProgramFiles, string CommonProgramFilesX86, string ApplicationDirectory);
public sealed record Vst3Candidate(string Name, string RelativePath, string Kind, Vst3MetadataInspection Metadata, Vst3BinaryInspection Binary);
public sealed record Vst3LocationResult(string Name, string Hint, string Status,
    IReadOnlyList<string> Issues, IReadOnlyList<Vst3Candidate> Candidates);
public sealed record Vst3DiscoveryResult(string Platform, DateTimeOffset ScannedUtc,
    IReadOnlyList<Vst3LocationResult> Locations);
public sealed record Vst3ScanLimits(int MaxEntries = 10_000, int MaxCandidates = 512, int MaxDepth = 8);

public static class Vst3SearchLocations
{
    public static Vst3HostPaths CurrentHost() => new(
        OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : "Unsupported",
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86), AppContext.BaseDirectory);

    public static IReadOnlyList<Vst3SearchLocation> Resolve(Vst3HostPaths host)
    {
        var locations = new List<Vst3SearchLocation>();
        void Add(string name, string hint, string basis, params string[] segments)
        {
            // An unavailable special folder must never fall back to the working directory.
            if (!string.IsNullOrWhiteSpace(basis)) locations.Add(new(name, hint, Path.Combine([basis, .. segments])));
        }
        switch (host.Platform)
        {
            case "Windows":
                Add("Current user", "%LOCALAPPDATA%/Programs/Common/VST3", host.LocalAppData, "Programs", "Common", "VST3");
                Add("System", "%CommonProgramFiles%/VST3", host.CommonProgramFiles, "VST3");
                if (!string.Equals(host.CommonProgramFiles, host.CommonProgramFilesX86, StringComparison.OrdinalIgnoreCase))
                    Add("System (x86)", "%CommonProgramFiles(x86)%/VST3", host.CommonProgramFilesX86, "VST3");
                Add("Maskil host", "Host application/VST3", host.ApplicationDirectory, "VST3");
                break;
            case "macOS":
                Add("Current user", "~/Library/Audio/Plug-Ins/VST3", host.UserHome, "Library", "Audio", "Plug-Ins", "VST3");
                Add("System", "/Library/Audio/Plug-Ins/VST3", "/Library/Audio/Plug-Ins/VST3");
                Add("Maskil host", "Host application/VST3", host.ApplicationDirectory, "VST3");
                break;
            case "Linux":
                Add("Current user", "~/.vst3", host.UserHome, ".vst3");
                foreach (var path in new[] { "/usr/lib64/vst3", "/usr/lib/vst3", "/usr/local/lib64/vst3", "/usr/local/lib/vst3" })
                    Add($"System ({path})", path, path);
                Add("Maskil host", "Host application/vst3", host.ApplicationDirectory, "vst3");
                break;
        }
        return locations.AsReadOnly();
    }
}

/// <summary>Lists candidates, reported metadata, and bounded binary headers. Never loads executable modules.</summary>
public sealed class Vst3Discovery
{
    private readonly string _platform;
    private readonly IReadOnlyList<Vst3SearchLocation> _locations;
    private readonly Vst3ScanLimits _limits;
    private readonly SemaphoreSlim _scanGate = new(1, 1);

    public Vst3Discovery() : this(Vst3SearchLocations.CurrentHost()) { }
    private Vst3Discovery(Vst3HostPaths host) : this(host.Platform, Vst3SearchLocations.Resolve(host)) { }
    public Vst3Discovery(string platform, IReadOnlyList<Vst3SearchLocation> locations, Vst3ScanLimits? limits = null)
    {
        _platform = platform;
        _locations = locations.ToArray();
        _limits = limits ?? new();
        if (_limits.MaxEntries < 1 || _limits.MaxCandidates < 1 || _limits.MaxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(limits));
    }

    public async Task<Vst3DiscoveryResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        await _scanGate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                var results = new List<Vst3LocationResult>();
                var metadata = new Vst3MetadataReader();
                var binaries = Vst3BinaryPreflight.ForHost(_platform);
                foreach (var location in _locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results.Add(ScanLocation(location, metadata, binaries, cancellationToken));
                }
                return new Vst3DiscoveryResult(_platform, DateTimeOffset.UtcNow, results.AsReadOnly());
            }, cancellationToken);
        }
        finally { _scanGate.Release(); }
    }

    private Vst3LocationResult ScanLocation(Vst3SearchLocation location, Vst3MetadataReader metadata, Vst3BinaryPreflight binaries, CancellationToken cancellationToken)
    {
        var candidates = new List<Vst3Candidate>();
        var issues = new HashSet<string>(StringComparer.Ordinal);
        Vst3LocationResult Result(string status) => new(location.Name, location.Hint, status,
            issues.Order(StringComparer.Ordinal).ToArray(), candidates.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.RelativePath, StringComparer.Ordinal).ToArray());
        try
        {
            var attributes = File.GetAttributes(location.Path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint)) return Result("SkippedLink");
            if (!attributes.HasFlag(FileAttributes.Directory)) return Result("Unavailable");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return Result("Missing"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Result("Unavailable"); }

        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((location.Path, 0));
        var entries = 0;
        var stopped = false;
        while (pending.TryPop(out var directory) && !stopped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                foreach (var path in Directory.EnumerateFileSystemEntries(directory.Path))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++entries > _limits.MaxEntries) { issues.Add("EntryLimit"); stopped = true; break; }
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if (attributes.HasFlag(FileAttributes.ReparsePoint)) { issues.Add("LinkedEntry"); continue; }
                        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                        if (Path.GetExtension(path).Equals(".vst3", StringComparison.OrdinalIgnoreCase))
                        {
                            if (candidates.Count == _limits.MaxCandidates) { issues.Add("CandidateLimit"); stopped = true; break; }
                            candidates.Add(new(Path.GetFileName(path), Path.GetRelativePath(location.Path, path).Replace('\\', '/'),
                                isDirectory ? "Bundle" : "File", isDirectory ? metadata.Read(path, cancellationToken) : new("NotApplicable"),
                                binaries.Inspect(path, isDirectory, cancellationToken)));
                            continue; // Inspect only known manifest/binary paths; never enumerate bundle internals.
                        }
                        if (isDirectory)
                        {
                            if (directory.Depth >= _limits.MaxDepth) issues.Add("DepthLimit");
                            else pending.Push((path, directory.Depth + 1));
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { issues.Add("UnreadableEntry"); }
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { issues.Add("UnreadableEntry"); }
        }
        return Result(issues.Count == 0 ? "Complete" : "Partial");
    }
}
