using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace MaskilForge.Infrastructure;

public sealed record Vst3MacExecutableInspection(string Status, string? Executable = null,
    string Source = "Contents/Info.plist", string? Sha256 = null);

/// <summary>Reads only the declared executable filename. Does not use a native bundle loader.</summary>
public static class Vst3MacBundle
{
    public const int MaxFileBytes = 128 * 1024;

    public static Vst3MacExecutableInspection Read(string bundle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? digest = null;
        Vst3MacExecutableInspection Result(string status, string? executable = null) => new(status, executable, Sha256: digest);
        try
        {
            var path = bundle;
            foreach (var segment in new[] { "", "Contents", "Info.plist" })
            {
                if (segment.Length > 0) path = Path.Combine(path, segment);
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) return Result("LinkedPath");
                if (attributes.HasFlag(FileAttributes.Directory) != (segment != "Info.plist")) return Result("Unreadable");
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > MaxFileBytes) return Result("TooLarge");
            var bytes = new byte[MaxFileBytes + 1];
            var count = 0;
            while (count < bytes.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(bytes, count, Math.Min(16 * 1024, bytes.Length - count));
                if (read == 0) break;
                count += read;
            }
            if (count > MaxFileBytes) return Result("TooLarge");
            digest = Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0, count)));
            if (bytes.AsSpan(0, count).StartsWith("bplist"u8)) return Result("UnsupportedFormat");

            // Apple's ordinary XML plist DOCTYPE is allowed but never resolved. Entities are not expanded.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null,
                MaxCharactersInDocument = MaxFileBytes, IgnoreComments = true, IgnoreProcessingInstructions = true };
            using var content = new MemoryStream(bytes, 0, count, writable: false);
            using (var boundedReader = XmlReader.Create(content, settings))
            {
                while (boundedReader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (boundedReader.Depth > 16) return Result("InvalidOrUnsupported");
                }
            }
            content.Position = 0;
            using var reader = XmlReader.Create(content, settings);
            var root = XDocument.Load(reader).Root;
            if (root?.Name != "plist" || root.Elements().Count() != 1 || root.Elements().Single().Name != "dict")
                return Result("InvalidOrUnsupported");
            var entries = root.Elements().Single().Elements().ToArray();
            if (entries.Length % 2 != 0) return Result("InvalidOrUnsupported");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            string? executable = null;
            for (var index = 0; index < entries.Length; index += 2)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = entries[index];
                if (key.Name != "key" || key.HasElements || !keys.Add(key.Value)) return Result("InvalidOrUnsupported");
                if (key.Value != "CFBundleExecutable") continue;
                var value = entries[index + 1];
                if (value.Name != "string" || value.HasElements) return Result("InvalidOrUnsupported");
                executable = value.Value;
                // A filename directly under Contents/MacOS, never a path supplied by the manifest.
                if (string.IsNullOrWhiteSpace(executable) || executable.Length > 255 || executable is "." or ".." ||
                    executable.Any(character => char.IsControl(character) || character is '/' or '\\' or ':'))
                    return Result("InvalidExecutableName");
            }
            return executable is null ? Result("MissingExecutable") : Result("Available", executable);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return Result("Missing"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Result("Unreadable"); }
        catch (XmlException) { return Result("InvalidOrUnsupported"); }
    }
}
