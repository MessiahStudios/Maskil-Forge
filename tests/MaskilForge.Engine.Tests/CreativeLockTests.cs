using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class CreativeLockTests
{
    [Fact]
    public void LockLyricLine_BlocksTextAndSyllableEditsUntilUnlocked()
    {
        var project = SongProject.Create("Locks");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("hold these words");
        var lockItem = project.LockLyricLine(line.Id);

        Assert.Equal(CreativeLockScope.LyricLine, lockItem.Scope);
        Assert.True(project.IsLyricLineLocked(line.Id));
        Assert.Throws<InvalidOperationException>(() => project.EnsureLyricLineUnlocked(line.Id));

        project.Unlock(lockItem.Id);
        Assert.False(project.IsLyricLineLocked(line.Id));
        section.EditLyricLine(line.Id, "changed words");
        Assert.Equal("changed words", line.Text);
    }

    [Fact]
    public void LockPhraseRhythm_BlocksPlacementAndApplyButAllowsCapture()
    {
        var (project, section, line, phrase, syllables) = CreateMappedPhrase();
        project.LockPhraseRhythm(line.Id, phrase.Id);

        Assert.Throws<InvalidOperationException>(() =>
            project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 2, 0)));
        var option = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");
        Assert.Throws<InvalidOperationException>(() =>
            project.ApplyRhythmCandidate(section.Id, line.Id, option.Id));
    }

    [Fact]
    public void CompatiblePhraseLoss_DropsOrphanedRhythmLocks()
    {
        var project = SongProject.Create("Locks");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        line.SetSyllables(line.Words[0].Id, ["one"]);
        line.SetSyllables(line.Words[1].Id, ["two"]);
        line.SplitPhraseAfter(line.Words[0].Id);
        var phraseId = line.Phrases[0].Id;
        project.LockPhraseRhythm(line.Id, phraseId);

        line.SetText("only");
        project.ReconcileLocks();

        Assert.Empty(project.Locks);
    }

    [Fact]
    public void RemoveSection_RejectsWhenLocksRemain()
    {
        var project = SongProject.Create("Locks");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("stay");
        project.LockLyricLine(line.Id);

        Assert.Throws<InvalidOperationException>(() => project.RemoveSection(section.Id));
    }

    private static (
        SongProject Project,
        SongSection Section,
        LyricLine Line,
        LyricPhrase Phrase,
        IReadOnlyList<SyllableId> Syllables) CreateMappedPhrase()
    {
        var project = SongProject.Create("Locks");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        line.SetSyllables(line.Words[0].Id, ["one"]);
        line.SetSyllables(line.Words[1].Id, ["two"]);
        var syllables = line.Words.SelectMany(word => word.Syllables).Select(item => item.Id).ToList();
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(1, 3, 0));
        return (project, section, line, line.Phrases[0], syllables);
    }
}
