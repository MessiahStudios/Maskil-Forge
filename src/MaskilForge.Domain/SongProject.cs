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
        ValidateAllSyllablePlacements(TimeSignature);
        ValidateAllRhythmCandidates(TimeSignature);
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
    {
        var proposed = new TimeSignatureEvent(0, numerator, denominator);
        ValidateAllSyllablePlacements(proposed);
        ValidateAllRhythmCandidates(proposed);
        Timeline.TimeSignatureMap.SetInitialTimeSignature(numerator, denominator);
        Touch();
    }

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
        var section = FindSection(sectionId);
        if (section.LyricLines.SelectMany(line => line.SyllablePlacements).Any(item => item.Position.Bar > durationBars))
            throw new InvalidOperationException("Section duration cannot end before an existing syllable placement. Clear or move the placement first.");
        if (section.LyricLines.SelectMany(line => line.RhythmCandidates).SelectMany(item => item.Events).Any(item => item.BeatPosition.Bar > durationBars))
            throw new InvalidOperationException("Section duration cannot end before an existing rhythm option. Remove that option first.");
        Timeline.SetSectionDuration(sectionId, durationBars, _sections.Select(item => item.Id).ToList());
        Touch();
    }

    public void SetSyllablePlacement(
        SectionId sectionId,
        LyricLineId lineId,
        SyllableId syllableId,
        BeatPosition? position,
        PlacementProvenance provenance = PlacementProvenance.Manual)
    {
        if (position is not null) ValidateBeatPosition(sectionId, position.Value, TimeSignature);
        FindSection(sectionId).FindLyricLine(lineId).SetSyllablePlacement(syllableId, position, provenance);
        Touch();
    }

    public MusicalPosition ResolveSyllablePosition(SectionId sectionId, BeatPosition position)
    {
        ValidateBeatPosition(sectionId, position, TimeSignature);
        var section = Timeline.FindSection(sectionId);
        return new MusicalPosition(section.Start.Bar + position.Bar - 1, position.Beat, position.Tick);
    }

    public RhythmCandidate CaptureRhythmCandidate(
        SectionId sectionId,
        LyricLineId lineId,
        LyricPhraseId phraseId,
        string label,
        RhythmCandidateProvenance provenance = RhythmCandidateProvenance.Manual)
    {
        var candidate = FindSection(sectionId).FindLyricLine(lineId)
            .CaptureRhythmCandidate(phraseId, label, provenance);
        Touch();
        return candidate;
    }

    public void ApplyRhythmCandidate(SectionId sectionId, LyricLineId lineId, RhythmCandidateId candidateId)
    {
        var line = FindSection(sectionId).FindLyricLine(lineId);
        var candidate = line.RhythmCandidates.SingleOrDefault(item => item.Id == candidateId)
            ?? throw new KeyNotFoundException($"Rhythm candidate '{candidateId}' was not found.");
        foreach (var item in candidate.Events) ValidateBeatPosition(sectionId, item.BeatPosition, TimeSignature);
        line.ApplyRhythmCandidate(candidateId);
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
        var syllablePlacements = lines.SelectMany(line => line.SyllablePlacements).ToList();
        if (syllablePlacements.Select(item => item.Id).Distinct().Count() != syllablePlacements.Count)
            throw new ArgumentException("Syllable placement IDs must be unique across the project.");
        var rhythmCandidates = lines.SelectMany(line => line.RhythmCandidates).ToList();
        if (rhythmCandidates.Select(item => item.Id).Distinct().Count() != rhythmCandidates.Count)
            throw new ArgumentException("Rhythm candidate IDs must be unique across the project.");
        var rhythmEvents = rhythmCandidates.SelectMany(item => item.Events).ToList();
        if (rhythmEvents.Select(item => item.Id).Distinct().Count() != rhythmEvents.Count)
            throw new ArgumentException("Rhythm candidate event IDs must be unique across the project.");
        var breathPoints = lines.SelectMany(line => line.BreathPoints).ToList();
        if (breathPoints.Select(item => item.Id).Distinct().Count() != breathPoints.Count)
            throw new ArgumentException("Breath point IDs must be unique across the project.");
    }

    private void ValidateAllSyllablePlacements(TimeSignatureEvent meter)
    {
        foreach (var section in _sections)
        foreach (var placement in section.LyricLines.SelectMany(line => line.SyllablePlacements))
            ValidateBeatPosition(section.Id, placement.Position, meter);
    }

    private void ValidateAllRhythmCandidates(TimeSignatureEvent meter)
    {
        foreach (var section in _sections)
        foreach (var rhythmEvent in section.LyricLines.SelectMany(line => line.RhythmCandidates).SelectMany(item => item.Events))
            ValidateBeatPosition(section.Id, rhythmEvent.BeatPosition, meter);
    }

    private void ValidateBeatPosition(SectionId sectionId, BeatPosition position, TimeSignatureEvent meter)
    {
        var section = Timeline.FindSection(sectionId);
        if (position.Bar > section.DurationBars)
            throw new ArgumentOutOfRangeException(nameof(position), $"Bar must be between 1 and {section.DurationBars} within this section.");
        if (position.Beat > meter.Numerator)
            throw new ArgumentOutOfRangeException(nameof(position), $"Beat must be between 1 and {meter.Numerator} for the current meter.");
        var ticksPerBeat = checked(Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        if (position.Tick >= ticksPerBeat)
            throw new ArgumentOutOfRangeException(nameof(position), $"Tick must be between 0 and {ticksPerBeat - 1} for the current meter.");
    }

    public void Touch() => LastModifiedUtc = DateTimeOffset.UtcNow;
}
