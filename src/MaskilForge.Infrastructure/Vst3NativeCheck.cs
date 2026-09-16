using System.Security.Cryptography;

namespace MaskilForge.Infrastructure;

public sealed record Vst3NativeCheckRequest(string Location, string RelativePath, string ExpectedPlistSha256);
public sealed record Vst3NativeCheckResult(string Status, string LastCompletedStage, DateTimeOffset CheckedUtc,
    string? BinarySource = null, string? BinarySha256 = null, string? PlistSha256 = null, int? ExitCode = null);

public sealed class Vst3NativeCheck(Vst3Discovery discovery)
{
    private const long MaxBinaryBytes = 512L * 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string WorkerPath { get; } = Path.Combine(AppContext.BaseDirectory, "native", "maskil-vst3-probe");
    public bool IsAvailable => OperatingSystem.IsMacOS() && File.Exists(WorkerPath);

    public async Task<Vst3NativeCheckResult> CheckAsync(Vst3NativeCheckRequest request, CancellationToken cancellationToken = default)
    {
        Vst3NativeCheckResult Empty(string status) => new(status, "NotStarted", DateTimeOffset.UtcNow);
        if (!IsAvailable) return Empty("WorkerUnavailable");
        if (!await _gate.WaitAsync(0, cancellationToken)) return Empty("Busy");
        try
        {
            var resolved = await discovery.ResolveNativeCandidateAsync(request.Location, request.RelativePath, cancellationToken);
            if (resolved is null) return Empty("CandidateUnavailable");
            var (bundle, candidate) = resolved.Value;
            var declaration = candidate.Binary.MacExecutable;
            if (declaration?.Status != "Available" || declaration.Sha256 != request.ExpectedPlistSha256)
                return Empty("RescanRequired");
            var source = $"Contents/MacOS/{declaration.Executable}";
            if (!candidate.Binary.Files.Any(file => file.Source == source && file.Status == "Recognized" && file.Format == "Mach-O" && file.HostMatch == "Match"))
                return Empty("HeaderNotMatched");
            var binary = Path.Combine(bundle, source);
            var before = await Digest(binary, cancellationToken);
            if (before is null) return Empty("BinaryTooLarge");
            // Repeat bounded path/header/declaration checks immediately before starting the worker.
            var refreshed = new Vst3BinaryPreflight("macOS", candidate.Binary.HostArchitecture).Inspect(bundle, true, cancellationToken);
            if (refreshed.MacExecutable != declaration || !refreshed.Files.Any(file => file.Source == source && file.HostMatch == "Match"))
                return Empty("RescanRequired");
            var outcome = await new Vst3ProbeProcess().RunAsync(WorkerPath, bundle, binary, cancellationToken);
            var status = outcome.Status;
            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var after = new Vst3BinaryPreflight("macOS", candidate.Binary.HostArchitecture).Inspect(bundle, true, cancellationToken);
                    if (after.MacExecutable != declaration || !after.Files.Any(file => file.Source == source && file.HostMatch == "Match") ||
                        await Digest(binary, cancellationToken) != before) status = "SourceChanged";
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { status = "SourceChanged"; }
            }
            return new(status, outcome.LastCompletedStage, DateTimeOffset.UtcNow, source, before, declaration.Sha256, outcome.ExitCode);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Empty("CandidateUnavailable"); }
        finally { _gate.Release(); }
    }

    private static async Task<string?> Digest(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        if (stream.Length > MaxBinaryBytes) return null;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long length = 0;
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            length += count;
            if (length > MaxBinaryBytes) return null;
            digest.AppendData(buffer, 0, count);
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }
}
