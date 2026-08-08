using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class ProsodyScorerTests
{
    [Fact]
    public void ScoreActivePhrase_WithoutPlacements_ExplainsThatTimingIsRequired()
    {
        var (project, section, line, phrase, _) = CreatePhrase("home");

        var score = ProsodyScorer.ScoreActivePhrase(project, section.Id, line.Id, phrase.Id);

        Assert.Equal(0, score.Overall);
        Assert.Null(score.RhythmCandidateId);
        var finding = Assert.Single(score.Findings);
        Assert.Equal(ProsodyFindingKind.Crowding, finding.Kind);
        Assert.Equal(ProsodyFindingSeverity.Info, finding.Severity);
        Assert.Contains("Place at least one syllable", finding.Message);
    }

    [Fact]
    public void ScoreActivePhrase_FlagsPrimaryStressOnAWeakBeat()
    {
        var (project, section, line, phrase, syllables) = CreatePhrase("home");
        line.SetStress(line.Words[0].Id, syllables[0], StressLevel.Primary);
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 2, 0));

        var score = ProsodyScorer.ScoreActivePhrase(project, section.Id, line.Id, phrase.Id);

        Assert.True(score.Stress < 100);
        Assert.Contains(score.Findings, item =>
            item.Kind == ProsodyFindingKind.StressConflict
            && item.Message.Contains("primary stress", StringComparison.OrdinalIgnoreCase)
            && item.SyllableId == syllables[0]);
    }

    [Fact]
    public void ScoreActivePhrase_FlagsCrowdingWhenSyllablesShareLessThanHalfABeat()
    {
        var (project, section, line, phrase, syllables) = CreatePhrase("one two");
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(1, 1, 100));

        var score = ProsodyScorer.ScoreActivePhrase(project, section.Id, line.Id, phrase.Id);

        Assert.True(score.Crowding < 100);
        Assert.Contains(score.Findings, item =>
            item.Kind == ProsodyFindingKind.Crowding
            && item.Message.Contains("ticks apart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScoreActivePhrase_FlagsBreathWithInsufficientRoomBeforeTheNextOnset()
    {
        var (project, section, line, phrase, syllables) = CreatePhrase("one two");
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(1, 1, 240));
        line.SetBreathPoint(syllables[0], true);

        var score = ProsodyScorer.ScoreActivePhrase(project, section.Id, line.Id, phrase.Id);

        Assert.True(score.Breath < 100);
        Assert.Contains(score.Findings, item =>
            item.Kind == ProsodyFindingKind.BreathIssue
            && item.Message.Contains("Breath after", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScoreRhythmCandidate_UsesCandidateTimingInsteadOfActivePlacements()
    {
        var (project, section, line, phrase, syllables) = CreatePhrase("one two three four five six");
        for (var index = 0; index < syllables.Count; index++)
            project.SetSyllablePlacement(
                section.Id,
                line.Id,
                syllables[index],
                new BeatPosition(1 + index / 4, (index % 4) + 1, 0));
        var candidate = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Long option");

        var score = ProsodyScorer.ScoreRhythmCandidate(project, section.Id, line.Id, candidate.Id);

        Assert.Equal(candidate.Id, score.RhythmCandidateId);
        Assert.True(score.Breath < 100);
        Assert.Contains(score.Findings, item =>
            item.Kind == ProsodyFindingKind.BreathIssue
            && item.Message.Contains("no interior breath", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CleanAlignedPlacement_ScoresPerfectly()
    {
        var (project, section, line, phrase, syllables) = CreatePhrase("one two");
        line.SetStress(line.Words[0].Id, syllables[0], StressLevel.Primary);
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(1, 3, 0));
        line.SetBreathPoint(syllables[0], true);

        var score = ProsodyScorer.ScoreActivePhrase(project, section.Id, line.Id, phrase.Id);

        Assert.Equal(100, score.Overall);
        Assert.Equal(100, score.Stress);
        Assert.Equal(100, score.Breath);
        Assert.Equal(100, score.Crowding);
        Assert.Empty(score.Findings);
    }

    private static (
        SongProject Project,
        SongSection Section,
        LyricLine Line,
        LyricPhrase Phrase,
        IReadOnlyList<SyllableId> Syllables) CreatePhrase(string text)
    {
        var project = SongProject.Create("Score");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine(text);
        foreach (var word in line.Words)
            line.SetSyllables(word.Id, [word.Text]);
        return (
            project,
            section,
            line,
            line.Phrases[0],
            line.Words.SelectMany(word => word.Syllables).Select(item => item.Id).ToList());
    }
}
