using MaskilForge.Domain;

namespace MaskilForge.Engine;

public enum ProsodyFindingKind
{
    StressConflict,
    BreathIssue,
    Crowding
}

public enum ProsodyFindingSeverity
{
    Info,
    Warning
}

/// <summary>
/// An inspectable reason a prosody score is reduced. Scores are derived, not stored creative state.
/// </summary>
public sealed class ProsodyFinding
{
    public ProsodyFinding(
        ProsodyFindingKind kind,
        ProsodyFindingSeverity severity,
        string message,
        SyllableId? syllableId = null,
        SyllableId? relatedSyllableId = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!Enum.IsDefined(severity)) throw new ArgumentOutOfRangeException(nameof(severity));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("A finding message is required.", nameof(message));
        Kind = kind;
        Severity = severity;
        Message = message.Trim();
        SyllableId = syllableId;
        RelatedSyllableId = relatedSyllableId;
    }

    public ProsodyFindingKind Kind { get; }
    public ProsodyFindingSeverity Severity { get; }
    public string Message { get; }
    public SyllableId? SyllableId { get; }
    public SyllableId? RelatedSyllableId { get; }
}

/// <summary>
/// Deterministic review of timed syllables against stress, breath, and crowding expectations.
/// </summary>
public sealed class ProsodyScore
{
    public ProsodyScore(
        LyricPhraseId phraseId,
        RhythmCandidateId? rhythmCandidateId,
        int overall,
        int stress,
        int breath,
        int crowding,
        IReadOnlyList<ProsodyFinding> findings)
    {
        if (phraseId.Value == Guid.Empty) throw new ArgumentException("A phrase ID is required.", nameof(phraseId));
        ArgumentNullException.ThrowIfNull(findings);
        Overall = Clamp(overall);
        Stress = Clamp(stress);
        Breath = Clamp(breath);
        Crowding = Clamp(crowding);
        PhraseId = phraseId;
        RhythmCandidateId = rhythmCandidateId;
        Findings = findings.ToList();
    }

    public LyricPhraseId PhraseId { get; }
    public RhythmCandidateId? RhythmCandidateId { get; }
    public int Overall { get; }
    public int Stress { get; }
    public int Breath { get; }
    public int Crowding { get; }
    public IReadOnlyList<ProsodyFinding> Findings { get; }

    private static int Clamp(int value) => Math.Clamp(value, 0, 100);
}

public static class ProsodyScorer
{
    private const int CrowdingGapTicksThresholdDivisor = 2; // half a beat
    private const int BreathRoomTicksMinimumBeats = 1;
    private const int LongPhraseSyllableThreshold = 6;
    private const int LongPhraseBeatThreshold = 8;

    public static ProsodyScore ScoreActivePhrase(
        SongProject project,
        SectionId sectionId,
        LyricLineId lineId,
        LyricPhraseId phraseId) =>
        Score(project, sectionId, lineId, phraseId, candidateId: null);

    public static ProsodyScore ScoreRhythmCandidate(
        SongProject project,
        SectionId sectionId,
        LyricLineId lineId,
        RhythmCandidateId candidateId)
    {
        var line = project.FindSection(sectionId).FindLyricLine(lineId);
        var candidate = line.RhythmCandidates.SingleOrDefault(item => item.Id == candidateId)
            ?? throw new KeyNotFoundException($"Rhythm candidate '{candidateId}' was not found.");
        return Score(project, sectionId, lineId, candidate.PhraseId, candidateId);
    }

    private static ProsodyScore Score(
        SongProject project,
        SectionId sectionId,
        LyricLineId lineId,
        LyricPhraseId phraseId,
        RhythmCandidateId? candidateId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        var line = section.FindLyricLine(lineId);
        var phrase = line.Phrases.SingleOrDefault(item => item.Id == phraseId)
            ?? throw new KeyNotFoundException($"Lyric phrase '{phraseId}' was not found.");
        var meter = project.TimeSignature;
        var ticksPerBeat = checked(project.Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var phraseSyllables = PhraseSyllables(line, phrase);
        var timed = ResolveTimedSyllables(line, phrase, phraseSyllables, candidateId, ticksPerBeat, meter.Numerator);

        var findings = new List<ProsodyFinding>();
        if (timed.Count == 0)
        {
            findings.Add(new ProsodyFinding(
                ProsodyFindingKind.Crowding,
                ProsodyFindingSeverity.Info,
                "Place at least one syllable in musical time before reviewing prosody."));
            return new ProsodyScore(phraseId, candidateId, 0, 0, 0, 0, findings);
        }

        CollectStressFindings(timed, findings);
        CollectCrowdingFindings(timed, ticksPerBeat, findings);
        CollectBreathFindings(line, phraseSyllables, timed, ticksPerBeat, findings);

        var stress = ScoreCategory(findings, ProsodyFindingKind.StressConflict);
        var breath = ScoreCategory(findings, ProsodyFindingKind.BreathIssue);
        var crowding = ScoreCategory(findings, ProsodyFindingKind.Crowding);
        var overall = (stress + breath + crowding) / 3;
        return new ProsodyScore(phraseId, candidateId, overall, stress, breath, crowding, findings);
    }

    private static IReadOnlyList<(LyricWord Word, LyricSyllable Syllable)> PhraseSyllables(
        LyricLine line,
        LyricPhrase phrase)
    {
        var wordById = line.Words.ToDictionary(item => item.Id);
        return phrase.WordIds
            .SelectMany(wordId => wordById[wordId].Syllables.Select(syllable => (wordById[wordId], syllable)))
            .ToList();
    }

    private static IReadOnlyList<TimedSyllable> ResolveTimedSyllables(
        LyricLine line,
        LyricPhrase phrase,
        IReadOnlyList<(LyricWord Word, LyricSyllable Syllable)> phraseSyllables,
        RhythmCandidateId? candidateId,
        int ticksPerBeat,
        int beatsPerBar)
    {
        var syllableById = phraseSyllables.ToDictionary(item => item.Syllable.Id, item => item);
        IEnumerable<(SyllableId SyllableId, BeatPosition Position)> sources;
        if (candidateId is null)
        {
            var phraseSyllableIds = syllableById.Keys.ToHashSet();
            sources = line.SyllablePlacements
                .Where(item => phraseSyllableIds.Contains(item.SyllableId))
                .Select(item => (item.SyllableId, item.Position));
        }
        else
        {
            var candidate = line.RhythmCandidates.Single(item => item.Id == candidateId);
            if (candidate.PhraseId != phrase.Id)
                throw new InvalidOperationException("Rhythm candidate does not belong to the requested phrase.");
            sources = candidate.Events.Select(item => (item.SyllableId, item.BeatPosition));
        }

        return sources
            .Where(item => syllableById.ContainsKey(item.SyllableId))
            .Select(item =>
            {
                var entry = syllableById[item.SyllableId];
                var weight = phrase.Prosody?.Units.FirstOrDefault(unit => unit.SyllableId == item.SyllableId)?.Weight;
                return new TimedSyllable(
                    entry.Word,
                    entry.Syllable,
                    item.Position,
                    ToSectionTicks(item.Position, ticksPerBeat, beatsPerBar),
                    ClassifyBeat(item.Position, beatsPerBar),
                    weight);
            })
            .OrderBy(item => item.Ticks)
            .ThenBy(item => item.Syllable.Id.Value)
            .ToList();
    }

    private static void CollectStressFindings(IReadOnlyList<TimedSyllable> timed, List<ProsodyFinding> findings)
    {
        foreach (var item in timed)
        {
            var stress = item.Syllable.Stress?.Level;
            if (stress is StressLevel.Primary or StressLevel.Emphasized
                && item.BeatStrength is BeatStrength.Weak or BeatStrength.Offbeat)
            {
                findings.Add(new ProsodyFinding(
                    ProsodyFindingKind.StressConflict,
                    ProsodyFindingSeverity.Warning,
                    $"“{item.Syllable.Text}” carries {stress.Value.ToString().ToLowerInvariant()} stress on a {DescribeBeat(item)}.",
                    item.Syllable.Id));
            }

            if (item.ProsodicWeight == ProsodicWeight.Strong
                && item.BeatStrength is BeatStrength.Weak or BeatStrength.Offbeat)
            {
                findings.Add(new ProsodyFinding(
                    ProsodyFindingKind.StressConflict,
                    ProsodyFindingSeverity.Warning,
                    $"“{item.Syllable.Text}” is marked strong in the phrase but lands on a {DescribeBeat(item)}.",
                    item.Syllable.Id));
            }
        }
    }

    private static void CollectCrowdingFindings(
        IReadOnlyList<TimedSyllable> timed,
        int ticksPerBeat,
        List<ProsodyFinding> findings)
    {
        var crowdingGap = Math.Max(1, ticksPerBeat / CrowdingGapTicksThresholdDivisor);
        for (var index = 1; index < timed.Count; index++)
        {
            var previous = timed[index - 1];
            var current = timed[index];
            var gap = current.Ticks - previous.Ticks;
            if (gap < crowdingGap)
            {
                findings.Add(new ProsodyFinding(
                    ProsodyFindingKind.Crowding,
                    ProsodyFindingSeverity.Warning,
                    $"“{previous.Syllable.Text}” and “{current.Syllable.Text}” are only {gap} ticks apart (under half a beat).",
                    previous.Syllable.Id,
                    current.Syllable.Id));
            }
        }

        foreach (var group in timed.GroupBy(item => (item.Position.Bar, item.Position.Beat)))
        {
            var members = group.ToList();
            if (members.Count < 3) continue;
            findings.Add(new ProsodyFinding(
                ProsodyFindingKind.Crowding,
                ProsodyFindingSeverity.Warning,
                $"Bar {group.Key.Bar}, beat {group.Key.Beat} packs {members.Count} syllables, which is hard to sing clearly.",
                members[0].Syllable.Id,
                members[^1].Syllable.Id));
        }
    }

    private static void CollectBreathFindings(
        LyricLine line,
        IReadOnlyList<(LyricWord Word, LyricSyllable Syllable)> phraseSyllables,
        IReadOnlyList<TimedSyllable> timed,
        int ticksPerBeat,
        List<ProsodyFinding> findings)
    {
        var phraseSyllableIds = phraseSyllables.Select(item => item.Syllable.Id).ToHashSet();
        var breaths = line.BreathPoints
            .Where(item => phraseSyllableIds.Contains(item.AfterSyllableId))
            .Select(item => item.AfterSyllableId)
            .ToHashSet();
        var breathRoom = ticksPerBeat * BreathRoomTicksMinimumBeats;

        for (var index = 0; index < timed.Count - 1; index++)
        {
            var current = timed[index];
            if (!breaths.Contains(current.Syllable.Id)) continue;
            var next = timed[index + 1];
            var gap = next.Ticks - current.Ticks;
            if (gap < breathRoom)
            {
                findings.Add(new ProsodyFinding(
                    ProsodyFindingKind.BreathIssue,
                    ProsodyFindingSeverity.Warning,
                    $"Breath after “{current.Syllable.Text}” leaves only {gap} ticks before “{next.Syllable.Text}”.",
                    current.Syllable.Id,
                    next.Syllable.Id));
            }
        }

        var spanBeats = timed.Count == 0
            ? 0
            : (timed[^1].Ticks - timed[0].Ticks + ticksPerBeat - 1) / ticksPerBeat;
        if (timed.Count >= LongPhraseSyllableThreshold
            || spanBeats >= LongPhraseBeatThreshold)
        {
            var interiorBreath = timed
                .Take(timed.Count - 1)
                .Any(item => breaths.Contains(item.Syllable.Id));
            if (!interiorBreath)
            {
                findings.Add(new ProsodyFinding(
                    ProsodyFindingKind.BreathIssue,
                    ProsodyFindingSeverity.Warning,
                    $"This phrase covers {timed.Count} timed syllables across about {Math.Max(1, spanBeats)} beats with no interior breath mark."));
            }
        }
    }

    private static int ScoreCategory(IReadOnlyList<ProsodyFinding> findings, ProsodyFindingKind kind)
    {
        var relevant = findings.Where(item => item.Kind == kind).ToList();
        if (relevant.Count == 0) return 100;
        var penalty = relevant.Sum(item => item.Severity == ProsodyFindingSeverity.Warning ? 25 : 10);
        return Math.Max(0, 100 - penalty);
    }

    private static long ToSectionTicks(BeatPosition position, int ticksPerBeat, int beatsPerBar) =>
        ((long)(position.Bar - 1) * beatsPerBar * ticksPerBeat)
        + ((long)(position.Beat - 1) * ticksPerBeat)
        + position.Tick;

    private static BeatStrength ClassifyBeat(BeatPosition position, int beatsPerBar)
    {
        if (position.Tick != 0) return BeatStrength.Offbeat;
        if (position.Beat == 1) return BeatStrength.Strong;
        if (beatsPerBar % 2 == 0 && position.Beat == beatsPerBar / 2 + 1) return BeatStrength.Medium;
        return BeatStrength.Weak;
    }

    private static string DescribeBeat(TimedSyllable item) => item.BeatStrength switch
    {
        BeatStrength.Strong => "strong beat",
        BeatStrength.Medium => "medium beat",
        BeatStrength.Weak => "weak beat",
        _ => "offbeat"
    };

    private enum BeatStrength
    {
        Strong,
        Medium,
        Weak,
        Offbeat
    }

    private sealed record TimedSyllable(
        LyricWord Word,
        LyricSyllable Syllable,
        BeatPosition Position,
        long Ticks,
        BeatStrength BeatStrength,
        ProsodicWeight? ProsodicWeight);
}
