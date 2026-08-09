using MaskilForge.Domain;

namespace MaskilForge.Engine;

public enum VoiceLeadingMotion
{
    Smooth,
    Moderate,
    Wide
}

/// <summary>
/// Derived chord-tone continuity between two harmony events. This is pitch-class analysis;
/// octave/register voicings and instrument assignments remain future work.
/// </summary>
public sealed record VoiceLeadingTransition(
    HarmonyChordId FromChordId,
    HarmonyChordId ToChordId,
    int CommonToneCount,
    decimal AverageNearestMotionSemitones,
    int RootMotionSemitones,
    VoiceLeadingMotion Motion);

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
            motion);
    }

    private static int NearestDistance(PitchClass source, IReadOnlyList<PitchClass> targets) =>
        targets.Min(target =>
        {
            var ascending = Theory.IntervalSemitones(source, target);
            return Math.Min(ascending, 12 - ascending);
        });
}
