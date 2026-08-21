namespace MaskilForge.Api;

public static class OriginalVocalTakeUpload
{
    public const int MaximumByteLength = 25 * 1024 * 1024;

    private static readonly HashSet<string> AllowedMediaTypes =
        ["audio/webm", "audio/ogg", "audio/mp4"];

    public static string NormalizeMediaType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A rough vocal take must declare its recording media type.");

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 200)
            throw new ArgumentException("The rough vocal recording media type is too long.");

        var separator = normalized.IndexOf(';');
        var baseMediaType = separator < 0 ? normalized : normalized[..separator].Trim();
        if (!AllowedMediaTypes.Contains(baseMediaType))
            throw new ArgumentException("This rough vocal recording format is not supported. Use WebM, Ogg, or MP4 audio.");

        return normalized;
    }

    public static async Task<byte[]> ReadAsync(
        Stream content,
        long? contentLength,
        CancellationToken cancellationToken,
        int maximumByteLength = MaximumByteLength)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (maximumByteLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumByteLength));
        if (contentLength is > 0 && contentLength > maximumByteLength)
            throw new ArgumentException($"A rough vocal take cannot exceed {maximumByteLength / 1024 / 1024} MB.");

        using var buffer = new MemoryStream();
        var block = new byte[81_920];
        while (true)
        {
            var read = await content.ReadAsync(block, cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > maximumByteLength)
                throw new ArgumentException($"A rough vocal take cannot exceed {maximumByteLength / 1024 / 1024} MB.");
            await buffer.WriteAsync(block.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
            throw new ArgumentException("The rough vocal take contained no recorded audio.");
        return buffer.ToArray();
    }
}
