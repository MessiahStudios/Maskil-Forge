using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// One song-section span on the absolute musical timeline.
/// </summary>
public sealed class LyricTimelineSectionSpan
{
    public LyricTimelineSectionSpan(
        SectionId sectionId,
        SectionKind kind,
        string title,
        MusicalPosition start,
        int durationBars,
        long startTick,
        long endTickExclusive)
    {
        if (sectionId.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(sectionId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A section title is required.", nameof(title));
        if (durationBars < 1) throw new ArgumentOutOfRangeException(nameof(durationBars));
        if (startTick < 0) throw new ArgumentOutOfRangeException(nameof(startTick));
        if (endTickExclusive <= startTick) throw new ArgumentOutOfRangeException(nameof(endTickExclusive));
        SectionId = sectionId;
        Kind = kind;
        Title = title.Trim();
        Start = start;
        DurationBars = durationBars;
        StartTick = startTick;
        EndTickExclusive = endTickExclusive;
    }

    public SectionId SectionId { get; }
    public SectionKind Kind { get; }
    public string Title { get; }
    public MusicalPosition Start { get; }
    public int DurationBars { get; }
    public long StartTick { get; }
    public long EndTickExclusive { get; }
}

public enum LyricTimelineMarkerKind
{
    ActivePlacement,
    RhythmCandidate,
    BreathAfter
}

/// <summary>
/// A derived syllable or breath mark projected onto absolute song time.
/// </summary>
public sealed class LyricTimelineMarker
{
    public LyricTimelineMarker(
        LyricTimelineMarkerKind kind,
        SectionId sectionId,
        LyricLineId lineId,
        LyricPhraseId? phraseId,
        SyllableId syllableId,
        SyllablePlacementId? placementId,
        RhythmCandidateId? rhythmCandidateId,
        string syllableText,
        string wordText,
        BeatPosition sectionRelative,
        MusicalPosition songPosition,
        long absoluteTick,
        StressLevel? stressLevel,
        ProsodicWeight? prosodicWeight,
        bool hasBreathAfter)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (sectionId.Value == Guid.Empty) throw new ArgumentException("A section ID is required.", nameof(sectionId));
        if (lineId.Value == Guid.Empty) throw new ArgumentException("A line ID is required.", nameof(lineId));
        if (syllableId.Value == Guid.Empty) throw new ArgumentException("A syllable ID is required.", nameof(syllableId));
        if (string.IsNullOrWhiteSpace(syllableText)) throw new ArgumentException("Syllable text is required.", nameof(syllableText));
        if (string.IsNullOrWhiteSpace(wordText)) throw new ArgumentException("Word text is required.", nameof(wordText));
        if (absoluteTick < 0) throw new ArgumentOutOfRangeException(nameof(absoluteTick));
        Kind = kind;
        SectionId = sectionId;
        LineId = lineId;
        PhraseId = phraseId;
        SyllableId = syllableId;
        PlacementId = placementId;
        RhythmCandidateId = rhythmCandidateId;
        SyllableText = syllableText.Trim();
        WordText = wordText.Trim();
        SectionRelative = sectionRelative;
        SongPosition = songPosition;
        AbsoluteTick = absoluteTick;
        StressLevel = stressLevel;
        ProsodicWeight = prosodicWeight;
        HasBreathAfter = hasBreathAfter;
    }

    public LyricTimelineMarkerKind Kind { get; }
    public SectionId SectionId { get; }
    public LyricLineId LineId { get; }
    public LyricPhraseId? PhraseId { get; }
    public SyllableId SyllableId { get; }
    public SyllablePlacementId? PlacementId { get; }
    public RhythmCandidateId? RhythmCandidateId { get; }
    public string SyllableText { get; }
    public string WordText { get; }
    public BeatPosition SectionRelative { get; }
    public MusicalPosition SongPosition { get; }
    public long AbsoluteTick { get; }
    public StressLevel? StressLevel { get; }
    public ProsodicWeight? ProsodicWeight { get; }
    public bool HasBreathAfter { get; }
}

/// <summary>
/// Derived lyric-to-timeline view. Not stored creative state.
/// </summary>
public sealed class LyricTimelineView
{
    public LyricTimelineView(
        long totalTicks,
        int ticksPerBeat,
        int beatsPerBar,
        IReadOnlyList<LyricTimelineSectionSpan> sections,
        IReadOnlyList<LyricTimelineMarker> markers)
    {
        if (totalTicks < 0) throw new ArgumentOutOfRangeException(nameof(totalTicks));
        if (ticksPerBeat < 1) throw new ArgumentOutOfRangeException(nameof(ticksPerBeat));
        if (beatsPerBar < 1) throw new ArgumentOutOfRangeException(nameof(beatsPerBar));
        ArgumentNullException.ThrowIfNull(sections);
        ArgumentNullException.ThrowIfNull(markers);
        TotalTicks = totalTicks;
        TicksPerBeat = ticksPerBeat;
        BeatsPerBar = beatsPerBar;
        Sections = sections.ToList();
        Markers = markers.ToList();
    }

    public long TotalTicks { get; }
    public int TicksPerBeat { get; }
    public int BeatsPerBar { get; }
    public IReadOnlyList<LyricTimelineSectionSpan> Sections { get; }
    public IReadOnlyList<LyricTimelineMarker> Markers { get; }
}

/// <summary>
/// Projects artist syllable placements onto the song timeline so the editor can explain musical fit.
/// </summary>
public static class LyricTimelineProjector
{
    public static LyricTimelineView Project(
        SongProject project,
        RhythmCandidateId? rhythmCandidateId = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var meter = project.Timeline.TimeSignatureMap.Events[0];
        var ticksPerBeat = project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator;
        var beatsPerBar = meter.Numerator;
        var sections = new List<LyricTimelineSectionSpan>();
        var markers = new List<LyricTimelineMarker>();

        foreach (var section in project.Sections)
        {
            var placement = project.Timeline.FindSection(section.Id);
            var startTick = project.Timeline.ToAbsoluteTicks(placement.Start);
            var endTick = project.Timeline.ToAbsoluteTicks(
                new MusicalPosition(placement.EndBarExclusive, 1, 0));
            sections.Add(new LyricTimelineSectionSpan(
                section.Id,
                section.Kind,
                section.Title,
                placement.Start,
                placement.DurationBars,
                startTick,
                endTick));

            foreach (var line in section.LyricLines)
            {
                var syllableLookup = BuildSyllableLookup(line);
                foreach (var item in line.SyllablePlacements.OrderBy(entry => entry.Position))
                {
                    if (!syllableLookup.TryGetValue(item.SyllableId, out var info)) continue;
                    var songPosition = project.ResolveSyllablePosition(section.Id, item.Position);
                    var absoluteTick = project.Timeline.ToAbsoluteTicks(songPosition);
                    var hasBreath = line.BreathPoints.Any(breath => breath.AfterSyllableId == item.SyllableId);
                    markers.Add(new LyricTimelineMarker(
                        LyricTimelineMarkerKind.ActivePlacement,
                        section.Id,
                        line.Id,
                        info.PhraseId,
                        item.SyllableId,
                        item.Id,
                        null,
                        info.Syllable.Text,
                        info.Word.Text,
                        item.Position,
                        songPosition,
                        absoluteTick,
                        info.Syllable.Stress?.Level,
                        info.ProsodicWeight,
                        hasBreath));

                    if (hasBreath)
                    {
                        var breathTick = Math.Min(absoluteTick + ticksPerBeat / 4, endTick - 1);
                        markers.Add(new LyricTimelineMarker(
                            LyricTimelineMarkerKind.BreathAfter,
                            section.Id,
                            line.Id,
                            info.PhraseId,
                            item.SyllableId,
                            item.Id,
                            null,
                            info.Syllable.Text,
                            info.Word.Text,
                            item.Position,
                            songPosition,
                            breathTick,
                            info.Syllable.Stress?.Level,
                            info.ProsodicWeight,
                            true));
                    }
                }

                if (rhythmCandidateId is not null)
                {
                    var candidate = line.RhythmCandidates.SingleOrDefault(entry => entry.Id == rhythmCandidateId);
                    if (candidate is null) continue;
                    foreach (var candidateEvent in candidate.Events.OrderBy(entry => entry.BeatPosition))
                    {
                        if (!syllableLookup.TryGetValue(candidateEvent.SyllableId, out var info)) continue;
                        var songPosition = project.ResolveSyllablePosition(section.Id, candidateEvent.BeatPosition);
                        var absoluteTick = project.Timeline.ToAbsoluteTicks(songPosition);
                        markers.Add(new LyricTimelineMarker(
                            LyricTimelineMarkerKind.RhythmCandidate,
                            section.Id,
                            line.Id,
                            candidate.PhraseId,
                            candidateEvent.SyllableId,
                            null,
                            candidate.Id,
                            info.Syllable.Text,
                            info.Word.Text,
                            candidateEvent.BeatPosition,
                            songPosition,
                            absoluteTick,
                            info.Syllable.Stress?.Level,
                            info.ProsodicWeight,
                            line.BreathPoints.Any(breath => breath.AfterSyllableId == candidateEvent.SyllableId)));
                    }
                }
            }
        }

        var totalTicks = sections.Count == 0 ? 0 : sections.Max(item => item.EndTickExclusive);
        return new LyricTimelineView(
            totalTicks,
            ticksPerBeat,
            beatsPerBar,
            sections,
            markers.OrderBy(item => item.AbsoluteTick)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.SyllableText, StringComparer.Ordinal)
                .ToList());
    }

    private static Dictionary<SyllableId, SyllableInfo> BuildSyllableLookup(LyricLine line)
    {
        var wordById = line.Words.ToDictionary(word => word.Id);
        var phraseBySyllable = new Dictionary<SyllableId, LyricPhraseId>();
        var weightBySyllable = new Dictionary<SyllableId, ProsodicWeight>();
        foreach (var phrase in line.Phrases)
        {
            foreach (var wordId in phrase.WordIds)
            {
                if (!wordById.TryGetValue(wordId, out var word)) continue;
                foreach (var syllable in word.Syllables)
                    phraseBySyllable[syllable.Id] = phrase.Id;
            }

            if (phrase.Prosody is null) continue;
            foreach (var unit in phrase.Prosody.Units)
                weightBySyllable[unit.SyllableId] = unit.Weight;
        }

        var lookup = new Dictionary<SyllableId, SyllableInfo>();
        foreach (var word in line.Words)
        {
            foreach (var syllable in word.Syllables)
            {
                lookup[syllable.Id] = new SyllableInfo(
                    word,
                    syllable,
                    phraseBySyllable.GetValueOrDefault(syllable.Id),
                    weightBySyllable.TryGetValue(syllable.Id, out var weight) ? weight : null);
            }
        }

        return lookup;
    }

    private sealed record SyllableInfo(
        LyricWord Word,
        LyricSyllable Syllable,
        LyricPhraseId? PhraseId,
        ProsodicWeight? ProsodicWeight);
}
