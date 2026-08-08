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
        SongTimeline timeline,
        IReadOnlyList<SongSection>? sections = null,
        IReadOnlyList<Track>? tracks = null,
        string artist = "",
        SongGenre genre = SongGenre.Unspecified,
        string description = "",
        string rawLyricDraft = "",
        DateTimeOffset createdUtc = default,
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
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _sections = sections?.ToList() ?? [];
        _tracks = tracks?.ToList() ?? [];
        EnsureUniqueIds();
        Timeline.ValidateSectionOrder(_sections.Select(section => section.Id).ToList());
        CreatedUtc = createdUtc == default ? DateTimeOffset.UtcNow : createdUtc;
        LastModifiedUtc = lastModifiedUtc == default ? CreatedUtc : lastModifiedUtc;
    }

    public ProjectId Id { get; }
    public SchemaVersion SchemaVersion { get; }
    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public SongGenre Genre { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string RawLyricDraft { get; private set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset LastModifiedUtc { get; private set; }
    public SongTimeline Timeline { get; }
    [JsonIgnore] public TempoEvent Tempo => Timeline.TempoMap.Events[0];
    [JsonIgnore] public TimeSignatureEvent TimeSignature => Timeline.TimeSignatureMap.Events[0];
    [JsonIgnore] public LyricDocument Lyrics => new(
        RawLyricDraft,
        _sections.SelectMany(section => section.LyricLines.Select(line => new LyricDocumentLine(section.Id, line))).ToList());
    public IReadOnlyList<SongSection> Sections => _sections;
    public IReadOnlyList<Track> Tracks => _tracks;

    public static SongProject Create(string title) => new(
        ProjectId.New(),
        SchemaVersion.Current,
        title,
        SongTimeline.CreateDefault());

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

    public void SetTempo(decimal beatsPerMinute) { Timeline.TempoMap.SetInitialTempo(beatsPerMinute); Touch(); }

    public void SetTimeSignature(int numerator, int denominator)
    { Timeline.TimeSignatureMap.SetInitialTimeSignature(numerator, denominator); Touch(); }

    public SongSection AddSection(SectionKind kind, string? title = null)
    {
        var section = SongSection.Create(kind, title);
        InsertSection(_sections.Count, section);
        return section;
    }

    public void InsertSection(int index, SongSection section, int durationBars = 8)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (index < 0 || index > _sections.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_sections.Any(item => item.Id == section.Id)) throw new InvalidOperationException($"Section '{section.Id}' already exists.");
        _sections.Insert(index, section);
        Timeline.ReflowSections(
            _sections.Select(item => item.Id).ToList(),
            new Dictionary<SectionId, int> { [section.Id] = durationBars });
        Touch();
    }

    public (SongSection Section, int Index, int DurationBars) RemoveSection(SectionId sectionId)
    {
        var index = IndexOf(sectionId);
        var section = _sections[index];
        var durationBars = Timeline.FindSection(sectionId).DurationBars;
        _sections.RemoveAt(index);
        Timeline.ReflowSections(_sections.Select(item => item.Id).ToList());
        Touch();
        return (section, index, durationBars);
    }

    public void RenameSection(SectionId sectionId, string title) { FindSection(sectionId).Rename(title); Touch(); }

    public void SetSectionDuration(SectionId sectionId, int durationBars)
    {
        Timeline.SetSectionDuration(sectionId, durationBars, _sections.Select(item => item.Id).ToList());
        Touch();
    }

    public void MoveSection(SectionId sectionId, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _sections.Count) throw new ArgumentOutOfRangeException(nameof(targetIndex));
        var currentIndex = IndexOf(sectionId);
        if (currentIndex == targetIndex) return;
        var section = _sections[currentIndex];
        _sections.RemoveAt(currentIndex);
        _sections.Insert(targetIndex, section);
        Timeline.ReflowSections(_sections.Select(item => item.Id).ToList());
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
        var lines = _sections.SelectMany(section => section.LyricLines).ToList();
        if (lines.Select(line => line.Id).Distinct().Count() != lines.Count)
            throw new ArgumentException("Lyric line IDs must be unique across the project.");
        var words = lines.SelectMany(line => line.Words).ToList();
        if (words.Select(word => word.Id).Distinct().Count() != words.Count)
            throw new ArgumentException("Lyric word IDs must be unique across the project.");
        var syllables = words.SelectMany(word => word.Syllables).ToList();
        if (syllables.Select(syllable => syllable.Id).Distinct().Count() != syllables.Count)
            throw new ArgumentException("Syllable IDs must be unique across the project.");
        var punctuation = lines.SelectMany(line => line.Punctuation).ToList();
        if (punctuation.Select(item => item.Id).Distinct().Count() != punctuation.Count)
            throw new ArgumentException("Punctuation IDs must be unique across the project.");
        var phrases = lines.SelectMany(line => line.Phrases).ToList();
        if (phrases.Select(item => item.Id).Distinct().Count() != phrases.Count)
            throw new ArgumentException("Lyric phrase IDs must be unique across the project.");
        var patterns = phrases.Where(item => item.Prosody is not null).Select(item => item.Prosody!).ToList();
        if (patterns.Select(item => item.Id).Distinct().Count() != patterns.Count)
            throw new ArgumentException("Prosodic pattern IDs must be unique across the project.");
        var prosodicUnits = patterns.SelectMany(item => item.Units).ToList();
        if (prosodicUnits.Select(item => item.Id).Distinct().Count() != prosodicUnits.Count)
            throw new ArgumentException("Prosodic unit IDs must be unique across the project.");
    }

    public void Touch() => LastModifiedUtc = DateTimeOffset.UtcNow;
}
