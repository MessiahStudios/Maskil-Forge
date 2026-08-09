using MaskilForge.Domain;

namespace MaskilForge.Engine;

public enum VoiceLeadingMotion
{
    Smooth,
    Moderate,
    Wide
}

public enum VoiceLeadingFindingKind
{
    RetainedVoice,
    WideLeap,
    WideSpacing,
    ParallelPerfectInterval,
    VoiceCountChange
}

public enum VoiceLeadingFindingSeverity
{
    Info,
    Warning
}

public sealed record VoiceLeadingFinding(
    VoiceLeadingFindingKind Kind,
    VoiceLeadingFindingSeverity Severity,
    string Message,
    int? FromVoicePosition = null,
    int? ToVoicePosition = null);

/// <summary>
/// Derived continuity between two harmony events. Registered voicings are used when both
/// chords provide them; otherwise the review retains its pitch-class fallback.
/// </summary>
public sealed record VoiceLeadingTransition(
    HarmonyChordId FromChordId,
    HarmonyChordId ToChordId,
    int CommonToneCount,
    decimal AverageNearestMotionSemitones,
    int RootMotionSemitones,
    VoiceLeadingMotion Motion,
    bool UsesRegisteredVoices = false,
    int MaximumVoiceMovementSemitones = 0,
    IReadOnlyList<VoiceLeadingFinding>? Findings = null);

public sealed class VoiceLeadingReview
{
    public VoiceLeadingReview(SectionId sectionId, IReadOnlyList<VoiceLeadingTransition> transitions)
    {
        SectionId = sectionId;
        Transitions = transitions;
        SmoothTransitionCount = transitions.Count(item => item.Motion == VoiceLeadingMotion.Smooth);
        AverageMotionSemitones = transitions.Count == 0
            ? 0
            : decimal.Round(transitions.Average(item => item.AverageNearestMotionSemitones), 2);
    }

    public SectionId SectionId { get; }
    public IReadOnlyList<VoiceLeadingTransition> Transitions { get; }
    public int SmoothTransitionCount { get; }
    public decimal AverageMotionSemitones { get; }
}

public static class VoiceLeadingAnalyzer
{
    public static VoiceLeadingReview ReviewSection(SongProject project, SectionId sectionId)
    {
        ArgumentNullException.ThrowIfNull(project);
        var section = project.FindSection(sectionId);
        var transitions = section.Harmony
            .Zip(section.Harmony.Skip(1), AnalyzeTransition)
            .ToList();
        return new VoiceLeadingReview(sectionId, transitions);
    }

    public static VoiceLeadingTransition AnalyzeTransition(HarmonyChord from, HarmonyChord to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Voicing is not null && to.Voicing is not null)
            return AnalyzeRegisteredTransition(from, to);

        var fromTones = from.Chord.PitchClasses;
        var toTones = to.Chord.PitchClasses;
        var commonTones = fromTones.Count(source => toTones.Any(target => target == source));
        var bidirectionalMotion = fromTones.Select(source => NearestDistance(source, toTones))
            .Concat(toTones.Select(target => NearestDistance(target, fromTones)))
            .ToList();
        var averageMotion = decimal.Round(bidirectionalMotion.Average(value => (decimal)value), 2);
        var rootMotion = NearestDistance(from.Chord.Spelling.PitchClass, [to.Chord.Spelling.PitchClass]);
        var motion = averageMotion switch
        {
            <= 1.5m => VoiceLeadingMotion.Smooth,
            <= 2.5m => VoiceLeadingMotion.Moderate,
            _ => VoiceLeadingMotion.Wide
        };
        return new VoiceLeadingTransition(
            from.Id,
            to.Id,
            commonTones,
            averageMotion,
            rootMotion,
            motion,
            Findings: []);
    }

    private static VoiceLeadingTransition AnalyzeRegisteredTransition(HarmonyChord from, HarmonyChord to)
    {
        var fromVoices = from.Voicing!.Voices;
        var toVoices = to.Voicing!.Voices;
        var pairedCount = Math.Min(fromVoices.Count, toVoices.Count);
        var movements = Enumerable.Range(0, pairedCount)
            .Select(position => Math.Abs(toVoices[position].Pitch.MidiNumber - fromVoices[position].Pitch.MidiNumber))
            .ToList();
        var findings = new List<VoiceLeadingFinding>();
        for (var position = 0; position < pairedCount; position++)
        {
            var movement = movements[position];
            if (movement == 0)
                findings.Add(new(VoiceLeadingFindingKind.RetainedVoice, VoiceLeadingFindingSeverity.Info,
                    $"Voice {position + 1} stays on {fromVoices[position].Pitch.ToDisplayString()}.", position, position));
            else if (movement > 7)
                findings.Add(new(VoiceLeadingFindingKind.WideLeap, VoiceLeadingFindingSeverity.Warning,
                    $"Voice {position + 1} moves {movement} semitones, which may sound like a pronounced register shift.", position, position));
        }
        if (fromVoices.Count != toVoices.Count)
            findings.Add(new(VoiceLeadingFindingKind.VoiceCountChange, VoiceLeadingFindingSeverity.Info,
                $"The voicing changes from {fromVoices.Count} to {toVoices.Count} voices."));

        AddSpacingFindings(toVoices, findings);
        AddParallelPerfectIntervalFindings(fromVoices, toVoices, pairedCount, findings);
        var average = movements.Count == 0 ? 0 : decimal.Round(movements.Average(value => (decimal)value), 2);
        var motion = average switch { <= 2m => VoiceLeadingMotion.Smooth, <= 5m => VoiceLeadingMotion.Moderate, _ => VoiceLeadingMotion.Wide };
        return new VoiceLeadingTransition(
            from.Id, to.Id,
            Enumerable.Range(0, pairedCount).Count(position => movements[position] == 0),
            average,
            NearestDistance(from.Chord.Spelling.PitchClass, [to.Chord.Spelling.PitchClass]),
            motion,
            true,
            movements.Count == 0 ? 0 : movements.Max(),
            findings);
    }

    private static void AddSpacingFindings(IReadOnlyList<ChordVoice> voices, List<VoiceLeadingFinding> findings)
    {
        for (var position = 1; position < voices.Count; position++)
        {
            var spacing = voices[position].Pitch.MidiNumber - voices[position - 1].Pitch.MidiNumber;
            if (spacing > 12)
                findings.Add(new(VoiceLeadingFindingKind.WideSpacing, VoiceLeadingFindingSeverity.Info,
                    $"Voices {position} and {position + 1} are {spacing} semitones apart, creating an open sound.", position - 1, position));
        }
    }

    private static void AddParallelPerfectIntervalFindings(
        IReadOnlyList<ChordVoice> fromVoices,
        IReadOnlyList<ChordVoice> toVoices,
        int pairedCount,
        List<VoiceLeadingFinding> findings)
    {
        for (var lower = 0; lower < pairedCount; lower++)
        for (var upper = lower + 1; upper < pairedCount; upper++)
        {
            var before = Math.Abs(fromVoices[upper].Pitch.MidiNumber - fromVoices[lower].Pitch.MidiNumber) % 12;
            var after = Math.Abs(toVoices[upper].Pitch.MidiNumber - toVoices[lower].Pitch.MidiNumber) % 12;
            var lowerMotion = toVoices[lower].Pitch.MidiNumber - fromVoices[lower].Pitch.MidiNumber;
            var upperMotion = toVoices[upper].Pitch.MidiNumber - fromVoices[upper].Pitch.MidiNumber;
            if (before == after && before is 0 or 7 && lowerMotion != 0 && Math.Sign(lowerMotion) == Math.Sign(upperMotion))
                findings.Add(new(VoiceLeadingFindingKind.ParallelPerfectInterval, VoiceLeadingFindingSeverity.Warning,
                    $"Voices {lower + 1} and {upper + 1} move in the same direction while keeping a perfect {(before == 7 ? "fifth" : "octave")}. This is a color choice, not an automatic error.", lower, upper));
        }
    }

    private static int NearestDistance(PitchClass source, IReadOnlyList<PitchClass> targets) =>
        targets.Min(target =>
        {
            var ascending = Theory.IntervalSemitones(source, target);
            return Math.Min(ascending, 12 - ascending);
        });
}
