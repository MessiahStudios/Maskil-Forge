using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class VoiceLeadingAnalyzerTests
{
    [Fact]
    public void ReviewSection_ReportsAdjacentChordToneContinuity()
    {
        var project = SongProject.Create("Voice leading");
        var section = project.AddSection(SectionKind.Verse);
        var first = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));
        var second = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.A, quality: ChordQuality.Minor), new BeatPosition(2, 1, 0));
        var third = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.F), new BeatPosition(3, 1, 0));

        var review = VoiceLeadingAnalyzer.ReviewSection(project, section.Id);

        Assert.Equal(2, review.Transitions.Count);
        Assert.Equal(first.Id, review.Transitions[0].FromChordId);
        Assert.Equal(second.Id, review.Transitions[0].ToChordId);
        Assert.Equal(2, review.Transitions[0].CommonToneCount);
        Assert.Equal(VoiceLeadingMotion.Smooth, review.Transitions[0].Motion);
        Assert.Equal(third.Id, review.Transitions[1].ToChordId);
        Assert.Equal(2, review.SmoothTransitionCount);
    }

    [Fact]
    public void ReviewSection_WithFewerThanTwoChords_ReturnsEmptyDerivedReview()
    {
        var project = SongProject.Create("Voice leading");
        var section = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));

        var review = VoiceLeadingAnalyzer.ReviewSection(project, section.Id);

        Assert.Empty(review.Transitions);
        Assert.Equal(0, review.AverageMotionSemitones);
    }

    [Fact]
    public void AnalyzeTransition_UsesShortestCircularRootMotion()
    {
        var from = HarmonyChord.Create(new ChordSymbol(NoteLetter.B), new BeatPosition(1, 1, 0));
        var to = HarmonyChord.Create(new ChordSymbol(NoteLetter.C), new BeatPosition(2, 1, 0));

        var transition = VoiceLeadingAnalyzer.AnalyzeTransition(from, to);

        Assert.Equal(1, transition.RootMotionSemitones);
    }
}
