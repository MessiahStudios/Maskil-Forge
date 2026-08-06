using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

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
        IReadOnlyList<Track>? tracks = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A project ID is required.", nameof(id));
        if (schemaVersion.Value < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        Id = id;
        SchemaVersion = schemaVersion;
        Rename(title);
        Tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));
        TimeSignature = timeSignature ?? throw new ArgumentNullException(nameof(timeSignature));
        _sections = sections?.ToList() ?? [];
        _tracks = tracks?.ToList() ?? [];
        EnsureUniqueIds();
    }

    public ProjectId Id { get; }
    public SchemaVersion SchemaVersion { get; }
    public string Title { get; private set; } = string.Empty;
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
    }

    public void SetTempo(decimal beatsPerMinute) => Tempo = new TempoEvent(0, beatsPerMinute);

    public void SetTimeSignature(int numerator, int denominator) =>
        TimeSignature = new TimeSignatureEvent(0, numerator, denominator);

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
    }

    public (SongSection Section, int Index) RemoveSection(SectionId sectionId)
    {
        var index = IndexOf(sectionId);
        var section = _sections[index];
        _sections.RemoveAt(index);
        return (section, index);
    }

    public void RenameSection(SectionId sectionId, string title) => FindSection(sectionId).Rename(title);

    public void MoveSection(SectionId sectionId, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _sections.Count) throw new ArgumentOutOfRangeException(nameof(targetIndex));
        var currentIndex = IndexOf(sectionId);
        if (currentIndex == targetIndex) return;
        var section = _sections[currentIndex];
        _sections.RemoveAt(currentIndex);
        _sections.Insert(targetIndex, section);
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
}
