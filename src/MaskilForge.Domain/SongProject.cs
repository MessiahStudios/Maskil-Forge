using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum SongGenre
{
    Unspecified,
    Pop,
    Rock,
    Folk,
    Country,
    RAndB,
    HipHop,
    Electronic,
    Cinematic,
    Alternative,
    Other
}

public sealed class SongProject
{
    private readonly List<SongSection> _sections;
    private readonly List<Track> _tracks;

    [JsonConstructor]
    public SongProject(
        ProjectId id,
        SchemaVersion schemaVersion,
        string title,
        TempoEvent tempo,
        TimeSignatureEvent timeSignature,
        IReadOnlyList<SongSection>? sections = null,
        IReadOnlyList<Track>? tracks = null,
        string artist = "",
        SongGenre genre = SongGenre.Unspecified,
        string description = "",
        string rawLyricDraft = "",
        DateTimeOffset lastModifiedUtc = default)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A project ID is required.", nameof(id));
        if (schemaVersion.Value < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        Id = id;
        SchemaVersion = schemaVersion;
        Rename(title);
        SetArtist(artist);
        SetGenre(genre);
        SetDescription(description);
        SetRawLyricDraft(rawLyricDraft);
        Tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));
        TimeSignature = timeSignature ?? throw new ArgumentNullException(nameof(timeSignature));
        _sections = sections?.ToList() ?? [];
        _tracks = tracks?.ToList() ?? [];
        EnsureUniqueIds();
        LastModifiedUtc = lastModifiedUtc == default ? DateTimeOffset.UtcNow : lastModifiedUtc;
    }

    public ProjectId Id { get; }
    public SchemaVersion SchemaVersion { get; }
    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public SongGenre Genre { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string RawLyricDraft { get; private set; } = string.Empty;
    public DateTimeOffset LastModifiedUtc { get; private set; }
    public TempoEvent Tempo { get; private set; }
    public TimeSignatureEvent TimeSignature { get; private set; }
    public IReadOnlyList<SongSection> Sections => _sections;
    public IReadOnlyList<Track> Tracks => _tracks;

    public static SongProject Create(string title) => new(
        ProjectId.New(),
        SchemaVersion.Current,
        title,
        new TempoEvent(0, 120),
        new TimeSignatureEvent(0, 4, 4));

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Project title is required.", nameof(title));
        if (title.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(title), "Project title cannot exceed 200 characters.");
        Title = title.Trim();
        Touch();
    }

    public void SetArtist(string artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        if (artist.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(artist), "Artist cannot exceed 200 characters.");
        Artist = artist.Trim();
        Touch();
    }

    public void SetGenre(SongGenre genre)
    {
        if (!Enum.IsDefined(genre)) throw new ArgumentOutOfRangeException(nameof(genre), "Genre is invalid.");
        Genre = genre;
        Touch();
    }

    public void SetDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (description.Length > 2_000) throw new ArgumentOutOfRangeException(nameof(description), "Description cannot exceed 2,000 characters.");
        Description = description.Trim();
        Touch();
    }

    public void SetRawLyricDraft(string rawLyricDraft)
    {
        ArgumentNullException.ThrowIfNull(rawLyricDraft);
        if (rawLyricDraft.Length > 100_000) throw new ArgumentOutOfRangeException(nameof(rawLyricDraft), "Raw lyrics cannot exceed 100,000 characters.");
        RawLyricDraft = rawLyricDraft;
        Touch();
    }

    public void SetTempo(decimal beatsPerMinute) { Tempo = new TempoEvent(0, beatsPerMinute); Touch(); }

    public void SetTimeSignature(int numerator, int denominator)
    { TimeSignature = new TimeSignatureEvent(0, numerator, denominator); Touch(); }

    public SongSection AddSection(SectionKind kind, string? title = null)
    {
        var section = SongSection.Create(kind, title);
        InsertSection(_sections.Count, section);
        return section;
    }

    public void InsertSection(int index, SongSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (index < 0 || index > _sections.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_sections.Any(item => item.Id == section.Id)) throw new InvalidOperationException($"Section '{section.Id}' already exists.");
        _sections.Insert(index, section);
        Touch();
    }

    public (SongSection Section, int Index) RemoveSection(SectionId sectionId)
    {
        var index = IndexOf(sectionId);
        var section = _sections[index];
        _sections.RemoveAt(index);
        Touch();
        return (section, index);
    }

    public void RenameSection(SectionId sectionId, string title) { FindSection(sectionId).Rename(title); Touch(); }

    public void MoveSection(SectionId sectionId, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _sections.Count) throw new ArgumentOutOfRangeException(nameof(targetIndex));
        var currentIndex = IndexOf(sectionId);
        if (currentIndex == targetIndex) return;
        var section = _sections[currentIndex];
        _sections.RemoveAt(currentIndex);
        _sections.Insert(targetIndex, section);
        Touch();
    }

    public SongSection FindSection(SectionId sectionId) =>
        _sections.SingleOrDefault(section => section.Id == sectionId)
        ?? throw new KeyNotFoundException($"Section '{sectionId}' was not found.");

    public int IndexOf(SectionId sectionId)
    {
        var index = _sections.FindIndex(section => section.Id == sectionId);
        return index >= 0 ? index : throw new KeyNotFoundException($"Section '{sectionId}' was not found.");
    }

    private void EnsureUniqueIds()
    {
        if (_sections.Select(section => section.Id).Distinct().Count() != _sections.Count)
            throw new ArgumentException("Section IDs must be unique.");
        if (_tracks.Select(track => track.Id).Distinct().Count() != _tracks.Count)
            throw new ArgumentException("Track IDs must be unique.");
    }

    public void Touch() => LastModifiedUtc = DateTimeOffset.UtcNow;
}
