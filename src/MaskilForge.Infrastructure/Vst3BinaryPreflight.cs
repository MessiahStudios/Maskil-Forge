using System.Runtime.InteropServices;

namespace MaskilForge.Infrastructure;

public sealed record Vst3BinaryFile(string Source, string Status, string? Format, IReadOnlyList<string> Architectures, string HostMatch);
public sealed record Vst3BinaryInspection(string HostPlatform, string HostArchitecture, string Status, IReadOnlyList<Vst3BinaryFile> Files,
    Vst3MacExecutableInspection? MacExecutable = null);

public sealed class Vst3BinaryPreflight(string platform, string architecture, int maxCandidates = 128)
{
    private int _remaining = maxCandidates >= 0 ? maxCandidates : throw new ArgumentOutOfRangeException(nameof(maxCandidates));
    public static Vst3BinaryPreflight ForHost(string platform) => new(platform, RuntimeInformation.ProcessArchitecture.ToString());
    public Vst3BinaryInspection Inspect(string candidate, bool bundle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_remaining == 0) return new(platform, architecture, "ScanLimit", []);
        _remaining--;
        var files = new List<Vst3BinaryFile>();
        var name = Path.GetFileNameWithoutExtension(candidate);
        var macExecutable = bundle ? Vst3MacBundle.Read(candidate, cancellationToken) : null;
        var declaredSource = macExecutable?.Status == "Available" ? $"Contents/MacOS/{macExecutable.Executable}" : null;
        // At most twelve module paths. A valid plist replaces the same-name macOS guess.
        var sources = bundle
            ? new[] { "x86-win", "x86_64-win", "arm-win", "arm64-win", "arm64ec-win", "arm64x-win", "i386-linux", "i686-linux", "x86_64-linux", "aarch64-linux", "armv7l-linux", "MacOS" }
                .Select(folder => $"Contents/{folder}/{name}{(folder.EndsWith("-win") ? ".vst3" : folder.EndsWith("-linux") ? ".so" : "")}").ToArray()
            : new[] { Path.GetFileName(candidate) };
        if (declaredSource is not null) sources[^1] = declaredSource;
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var path = candidate;
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) { Add("LinkedPath"); continue; }
                if (bundle)
                {
                    var parts = source.Split('/');
                    for (var index = 0; index < parts.Length; index++)
                    {
                        path = Path.Combine(path, parts[index]);
                        attributes = File.GetAttributes(path);
                        if (attributes.HasFlag(FileAttributes.ReparsePoint)) { Add("LinkedPath"); break; }
                        if (attributes.HasFlag(FileAttributes.Directory) != (index < parts.Length - 1)) { Add("Unreadable"); break; }
                    }
                    if (files.Count > 0 && files[^1].Source == source) continue;
                }
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var info = Vst3BinaryHeaders.Read(stream, cancellationToken);
                files.Add(new(source, info.Status, info.Format, info.Architectures, Match(info, platform, architecture)));
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                if (source == declaredSource) Add("Missing");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { Add("Unreadable"); }
            void Add(string status) => files.Add(new(source, status, null, [], "Unknown"));
        }
        return new(platform, architecture, files.Count == 0 ? "NoConventionalBinary" : "Inspected", files.AsReadOnly(), macExecutable);
    }

    public static string Match(BinaryHeaderInfo info, string platform, string architecture)
    {
        var expected = platform switch { "Windows" => "PE", "macOS" => "Mach-O", "Linux" => "ELF", _ => null };
        if (info.Status != "Recognized" || expected is null || architecture is not ("X86" or "X64" or "Arm" or "Arm64")) return "Unknown";
        if (info.Format != expected) return "Different";
        if (info.Architectures.Contains(architecture)) return "Match";
        // Hybrid Windows/Arm and unrecognized CPUs require a native loader check, not a guessed compatibility verdict.
        if (info.Architectures.Any(item => item.StartsWith("Unknown", StringComparison.Ordinal) || item is "Arm64EC" or "Arm64X")) return "Unknown";
        return "Different";
    }
}
