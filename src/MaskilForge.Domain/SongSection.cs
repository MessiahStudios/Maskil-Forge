using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum SectionKind
{
    Verse,
    Chorus,
    PreChorus,
    Bridge,
    Outro
}

public sealed class SongSection
{
    private readonly List<LyricLine> _lyricLines;

    [JsonConstructor]
    public SongSection(SectionId id, SectionKind kind, string title, IReadOnlyList<LyricLine>? lyricLines = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(id));
        Id = id;
        Kind = kind;
        Rename(title);
        _lyricLines = lyricLines?.ToList() ?? [];
        if (_lyricLines.Select(line => line.Id).Distinct().Count() != _lyricLines.Count)
            throw new ArgumentException("Lyric line IDs must be unique.", nameof(lyricLines));
    }

    public SectionId Id { get; }
    public SectionKind Kind { get; }
    public string Title { get; private set; } = string.Empty;
    public IReadOnlyList<LyricLine> LyricLines => _lyricLines;

    public static SongSection Create(SectionKind kind, string? title = null) =>
        new(SectionId.New(), kind, title ?? DefaultTitle(kind));

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Section title is required.", nameof(title));
        if (title.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(title), "Section title cannot exceed 100 characters.");
        Title = title.Trim();
    }

    public LyricLine AddLyricLine(string text = "")
    {
        var line = LyricLine.Create(text);
        _lyricLines.Add(line);
        return line;
    }

    public void SetLyricLines(IEnumerable<LyricLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var replacement = lines.ToList();
        if (replacement.Select(line => line.Id).Distinct().Count() != replacement.Count)
            throw new ArgumentException("Lyric line IDs must be unique.", nameof(lines));
        _lyricLines.Clear();
        _lyricLines.AddRange(replacement);
    }

    public void EditLyricLine(LyricLineId lineId, string text)
    {
        FindLyricLine(lineId).SetText(text);
    }

    public LyricLine FindLyricLine(LyricLineId lineId) =>
        _lyricLines.SingleOrDefault(item => item.Id == lineId)
        ?? throw new KeyNotFoundException($"Lyric line '{lineId}' was not found.");

    public static string DefaultTitle(SectionKind kind) => kind switch
    {
        SectionKind.PreChorus => "Pre-Chorus",
        _ => kind.ToString()
    };
}
