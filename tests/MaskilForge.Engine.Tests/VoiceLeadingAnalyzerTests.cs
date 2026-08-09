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

    [Fact]
    public void AnalyzeTransition_UsesRegisteredVoicesAndExplainsWideMovement()
    {
        var project = SongProject.Create("Registered voice leading");
        var section = project.AddSection(SectionKind.Verse);
        var first = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));
        var second = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.F), new BeatPosition(2, 1, 0));
        project.SetChordVoicing(section.Id, first.Id, [Pitch(NoteLetter.C, 3), Pitch(NoteLetter.G, 3), Pitch(NoteLetter.E, 4)]);
        project.SetChordVoicing(section.Id, second.Id, [Pitch(NoteLetter.F, 3), Pitch(NoteLetter.A, 3), Pitch(NoteLetter.F, 5)]);

        var transition = VoiceLeadingAnalyzer.AnalyzeTransition(first.With(voicing: section.FindHarmonyChord(first.Id).Voicing, replaceVoicing: true), section.FindHarmonyChord(second.Id));

        Assert.True(transition.UsesRegisteredVoices);
        Assert.Equal(13, transition.MaximumVoiceMovementSemitones);
        Assert.Contains(transition.Findings!, item => item.Kind == VoiceLeadingFindingKind.WideLeap);
        Assert.Equal(VoiceLeadingMotion.Wide, transition.Motion);
    }

    [Fact]
    public void AnalyzeTransition_FallsBackWhenEitherChordHasNoVoicing()
    {
        var from = HarmonyChord.Create(new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0));
        var to = HarmonyChord.Create(new ChordSymbol(NoteLetter.A, quality: ChordQuality.Minor), new BeatPosition(2, 1, 0));

        var transition = VoiceLeadingAnalyzer.AnalyzeTransition(from, to);

        Assert.False(transition.UsesRegisteredVoices);
        Assert.Empty(transition.Findings!);
        Assert.Equal(VoiceLeadingMotion.Smooth, transition.Motion);
    }

    private static RegisteredPitch Pitch(NoteLetter letter, int octave) => new(letter, Accidental.Natural, octave);
}
