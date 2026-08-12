using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum SectionKind
{
    Intro,
    Verse,
    Chorus,
    PreChorus,
    Bridge,
    Outro
}

public enum SectionDelivery
{
    Sung,
    TalkSung,
    Spoken,
    Whispered
}

public enum StructuralFunction
{
    Unspecified,
    Setup,
    Development,
    Lift,
    Payoff,
    Contrast,
    Transition,
    Resolution
}

public sealed class SongSection
{
    private readonly List<LyricLine> _lyricLines;
    private readonly List<HarmonyChord> _harmony;
    private readonly List<HarmonyCandidate> _harmonyCandidates;

    [JsonConstructor]
    public SongSection(
        SectionId id,
        SectionKind kind,
        string title,
        IReadOnlyList<LyricLine>? lyricLines = null,
        IReadOnlyList<HarmonyChord>? harmony = null,
        IReadOnlyList<HarmonyCandidate>? harmonyCandidates = null,
        SectionDelivery delivery = SectionDelivery.Sung,
        string performanceNotes = "",
        StructuralFunction structuralFunction = StructuralFunction.Unspecified)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(id));
        Id = id;
        Kind = kind;
        Rename(title);
        _lyricLines = lyricLines?.ToList() ?? [];
        if (_lyricLines.Select(line => line.Id).Distinct().Count() != _lyricLines.Count)
            throw new ArgumentException("Lyric line IDs must be unique.", nameof(lyricLines));
        _harmony = harmony?.ToList() ?? [];
        if (_harmony.Select(item => item.Id).Distinct().Count() != _harmony.Count)
            throw new ArgumentException("Harmony chord IDs must be unique.", nameof(harmony));
        SortHarmony();
        _harmonyCandidates = harmonyCandidates?.ToList() ?? [];
        if (_harmonyCandidates.Select(item => item.Id).Distinct().Count() != _harmonyCandidates.Count)
            throw new ArgumentException("Harmony candidate IDs must be unique.", nameof(harmonyCandidates));
        SetPerformanceIntent(delivery, performanceNotes);
        SetStructuralFunction(structuralFunction);
    }

    public SectionId Id { get; }
    public SectionKind Kind { get; }
    public string Title { get; private set; } = string.Empty;
    public IReadOnlyList<LyricLine> LyricLines => _lyricLines;
    public IReadOnlyList<HarmonyChord> Harmony => _harmony;
    public IReadOnlyList<HarmonyCandidate> HarmonyCandidates => _harmonyCandidates;
    public SectionDelivery Delivery { get; private set; }
    public string PerformanceNotes { get; private set; } = string.Empty;
    public StructuralFunction StructuralFunction { get; private set; }

    public static SongSection Create(SectionKind kind, string? title = null) =>
        new(SectionId.New(), kind, title ?? DefaultTitle(kind));

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Section title is required.", nameof(title));
        if (title.Trim().Length > 100) throw new ArgumentOutOfRangeException(nameof(title), "Section title cannot exceed 100 characters.");
        Title = title.Trim();
    }

    public void SetPerformanceIntent(SectionDelivery delivery, string performanceNotes)
    {
        if (!Enum.IsDefined(delivery)) throw new ArgumentOutOfRangeException(nameof(delivery));
        ArgumentNullException.ThrowIfNull(performanceNotes);
        if (performanceNotes.Trim().Length > 1_000)
            throw new ArgumentOutOfRangeException(nameof(performanceNotes), "Section performance notes cannot exceed 1,000 characters.");
        Delivery = delivery;
        PerformanceNotes = performanceNotes.Trim();
    }

    public void SetStructuralFunction(StructuralFunction structuralFunction)
    {
        if (!Enum.IsDefined(structuralFunction)) throw new ArgumentOutOfRangeException(nameof(structuralFunction));
        StructuralFunction = structuralFunction;
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

    public void SetHarmony(IEnumerable<HarmonyChord> chords)
    {
        ArgumentNullException.ThrowIfNull(chords);
        var replacement = chords.ToList();
        if (replacement.Select(item => item.Id).Distinct().Count() != replacement.Count)
            throw new ArgumentException("Harmony chord IDs must be unique.", nameof(chords));
        _harmony.Clear();
        _harmony.AddRange(replacement);
        SortHarmony();
    }

    public HarmonyChord AddHarmonyChord(ChordSymbol chord, BeatPosition start, int durationBars = 1)
    {
        var item = HarmonyChord.Create(chord, start, durationBars);
        _harmony.Add(item);
        SortHarmony();
        return item;
    }

    public void UpsertHarmonyChord(HarmonyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var index = _harmony.FindIndex(item => item.Id == chord.Id);
        if (index < 0) _harmony.Add(chord);
        else _harmony[index] = chord;
        SortHarmony();
    }

    public HarmonyChord RemoveHarmonyChord(HarmonyChordId harmonyChordId)
    {
        var index = _harmony.FindIndex(item => item.Id == harmonyChordId);
        if (index < 0) throw new KeyNotFoundException($"Harmony chord '{harmonyChordId}' was not found.");
        var removed = _harmony[index];
        _harmony.RemoveAt(index);
        return removed;
    }

    public HarmonyChord FindHarmonyChord(HarmonyChordId harmonyChordId) =>
        _harmony.SingleOrDefault(item => item.Id == harmonyChordId)
        ?? throw new KeyNotFoundException($"Harmony chord '{harmonyChordId}' was not found.");

    public HarmonyCandidate CaptureHarmonyCandidate(string label, HarmonyProvenance provenance = HarmonyProvenance.Manual)
    {
        var events = _harmony.Select((item, position) => new HarmonyCandidateEvent(
            HarmonyCandidateEventId.New(), position, item.Chord, item.Start, item.DurationBars)).ToList();
        var candidate = new HarmonyCandidate(HarmonyCandidateId.New(), label, provenance, events);
        _harmonyCandidates.Add(candidate);
        return candidate;
    }

    public void InsertHarmonyCandidate(int index, HarmonyCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (index < 0 || index > _harmonyCandidates.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_harmonyCandidates.Any(item => item.Id == candidate.Id))
            throw new InvalidOperationException($"Harmony candidate '{candidate.Id}' already exists.");
        _harmonyCandidates.Insert(index, candidate);
    }

    public (HarmonyCandidate Candidate, int Index) RemoveHarmonyCandidate(HarmonyCandidateId candidateId)
    {
        var index = _harmonyCandidates.FindIndex(item => item.Id == candidateId);
        if (index < 0) throw new KeyNotFoundException($"Harmony candidate '{candidateId}' was not found.");
        var candidate = _harmonyCandidates[index];
        _harmonyCandidates.RemoveAt(index);
        return (candidate, index);
    }

    public void RenameHarmonyCandidate(HarmonyCandidateId candidateId, string label)
    {
        var index = _harmonyCandidates.FindIndex(item => item.Id == candidateId);
        if (index < 0) throw new KeyNotFoundException($"Harmony candidate '{candidateId}' was not found.");
        _harmonyCandidates[index] = _harmonyCandidates[index].WithLabel(label);
    }

    public HarmonyCandidate FindHarmonyCandidate(HarmonyCandidateId candidateId) =>
        _harmonyCandidates.SingleOrDefault(item => item.Id == candidateId)
        ?? throw new KeyNotFoundException($"Harmony candidate '{candidateId}' was not found.");

    public static string DefaultTitle(SectionKind kind) => kind switch
    {
        SectionKind.PreChorus => "Pre-Chorus",
        _ => kind.ToString()
    };

    private void SortHarmony() =>
        _harmony.Sort((left, right) =>
        {
            var byStart = left.Start.CompareTo(right.Start);
            return byStart != 0 ? byStart : left.Id.Value.CompareTo(right.Id.Value);
        });
}
