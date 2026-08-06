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

public sealed class LyricLine
{
    [JsonConstructor]
    public LyricLine(Guid id, string text)
    {
        if (id == Guid.Empty) throw new ArgumentException("A lyric line ID is required.", nameof(id));
        Id = id;
        SetText(text);
    }

    public Guid Id { get; }
    public string Text { get; private set; } = string.Empty;

    public static LyricLine Create(string text = "") => new(Guid.NewGuid(), text);

    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 2_000) throw new ArgumentOutOfRangeException(nameof(text), "A lyric line cannot exceed 2,000 characters.");
        Text = text;
    }
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
        _lyricLines.Clear();
        _lyricLines.AddRange(lines);
    }

    public void EditLyricLine(Guid lineId, string text)
    {
        var line = _lyricLines.SingleOrDefault(item => item.Id == lineId)
            ?? throw new KeyNotFoundException($"Lyric line '{lineId}' was not found.");
        line.SetText(text);
    }

    public static string DefaultTitle(SectionKind kind) => kind switch
    {
        SectionKind.PreChorus => "Pre-Chorus",
        _ => kind.ToString()
    };
}
