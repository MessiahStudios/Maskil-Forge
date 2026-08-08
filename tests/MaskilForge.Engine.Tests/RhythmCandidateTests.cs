using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class RhythmCandidateTests
{
    [Fact]
    public void CaptureCandidate_SnapshotsCurrentPhrasePlacementsWithStableIdentity()
    {
        var (project, section, line, phrase, syllables) = CreateMappedPhrase();

        var candidate = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");

        Assert.Equal("Option A", candidate.Label);
        Assert.Equal(phrase.Id, candidate.PhraseId);
        Assert.Equal(RhythmCandidateProvenance.Manual, candidate.Provenance);
        Assert.Equal(syllables, candidate.Events.Select(item => item.SyllableId));
        Assert.Equal([new BeatPosition(1, 1, 0), new BeatPosition(1, 3, 0)],
            candidate.Events.Select(item => item.BeatPosition));
        Assert.Equal([0, 1], candidate.Events.Select(item => item.Position));
    }

    [Fact]
    public void CaptureCandidate_RequiresAnArtistPlacementAndAUsefulLabel()
    {
        var project = SongProject.Create("Options");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        line.SetSyllables(line.Words[0].Id, ["home"]);

        Assert.Throws<InvalidOperationException>(() => project.CaptureRhythmCandidate(
            section.Id, line.Id, line.Phrases[0].Id, "Option A"));
        project.SetSyllablePlacement(section.Id, line.Id, line.Words[0].Syllables[0].Id, new BeatPosition(1, 1, 0));
        Assert.Throws<ArgumentException>(() => project.CaptureRhythmCandidate(
            section.Id, line.Id, line.Phrases[0].Id, " "));
    }

    [Fact]
    public void MultipleCandidates_RemainIndependentAndApplyingOneRestoresItsTiming()
    {
        var (project, section, line, phrase, syllables) = CreateMappedPhrase();
        var originalPlacementIds = line.SyllablePlacements.Select(item => item.Id).ToList();
        var optionA = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(2, 4, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(2, 1, 0));
        var optionB = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option B");

        project.ApplyRhythmCandidate(section.Id, line.Id, optionA.Id);

        Assert.Equal(2, line.RhythmCandidates.Count);
        Assert.NotEqual(optionA.Id, optionB.Id);
        Assert.Equal(originalPlacementIds, line.SyllablePlacements.Select(item => item.Id));
        Assert.Equal(optionA.Events.Select(item => item.BeatPosition), line.SyllablePlacements.Select(item => item.Position));
    }

    [Fact]
    public void CompatibleLyricAndSyllableEdits_PreserveCandidateAndEventIdentities()
    {
        var (project, section, line, phrase, syllables) = CreateMappedPhrase();
        var candidate = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Keep me");
        var eventIds = candidate.Events.Select(item => item.Id).ToList();

        line.SetText("Oh one two");
        var preservedCandidate = Assert.Single(line.RhythmCandidates);

        Assert.Equal(candidate.Id, preservedCandidate.Id);
        Assert.Equal(eventIds, preservedCandidate.Events.Select(item => item.Id));
        Assert.Equal(syllables, preservedCandidate.Events.Select(item => item.SyllableId));
    }

    [Fact]
    public void RemovingReferencedSyllables_FiltersOrRemovesCandidate()
    {
        var (project, section, line, phrase, syllables) = CreateMappedPhrase();
        var candidate = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");

        line.SetSyllables(line.Words[0].Id, ["changed"]);
        var surviving = Assert.Single(line.RhythmCandidates);
        Assert.Equal(candidate.Id, surviving.Id);
        Assert.Equal(syllables[1], Assert.Single(surviving.Events).SyllableId);

        line.SetSyllables(line.Words[1].Id, ["changed"]);
        Assert.Empty(line.RhythmCandidates);
    }

    [Fact]
    public void PhraseSplit_PartitionsCandidateWithoutCopyingSyllableIdentity()
    {
        var (project, section, line, phrase, _) = CreateMappedPhrase();
        var candidate = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");
        var eventIds = candidate.Events.Select(item => item.Id).ToList();

        line.SplitPhraseAfter(line.Words[0].Id);

        Assert.Equal(2, line.RhythmCandidates.Count);
        Assert.Equal(candidate.Id, line.RhythmCandidates[0].Id);
        Assert.NotEqual(candidate.Id, line.RhythmCandidates[1].Id);
        Assert.Equal(line.Phrases[0].Id, line.RhythmCandidates[0].PhraseId);
        Assert.Equal(line.Phrases[1].Id, line.RhythmCandidates[1].PhraseId);
        Assert.Equal(eventIds, line.RhythmCandidates.SelectMany(item => item.Events).Select(item => item.Id));
    }

    [Fact]
    public void PhraseJoin_ReassignsCandidatesToTheSurvivingPhrase()
    {
        var (project, section, line, phrase, _) = CreateMappedPhrase();
        line.SplitPhraseAfter(line.Words[0].Id);
        var first = project.CaptureRhythmCandidate(section.Id, line.Id, line.Phrases[0].Id, "First half");
        var second = project.CaptureRhythmCandidate(section.Id, line.Id, line.Phrases[1].Id, "Second half");
        var survivingPhraseId = line.Phrases[0].Id;

        line.JoinPhraseWithPrevious(line.Phrases[1].Id);

        Assert.Equal(2, line.RhythmCandidates.Count);
        Assert.All(line.RhythmCandidates, candidate => Assert.Equal(survivingPhraseId, candidate.PhraseId));
        Assert.Equal([first.Id, second.Id], line.RhythmCandidates.Select(item => item.Id));
    }

    [Fact]
    public void TimelineChanges_CannotInvalidateSavedRhythmOptions()
    {
        var (project, section, line, phrase, syllables) = CreateMappedPhrase();
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(8, 4, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(8, 3, 0));
        project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Late option");
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], null);
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], null);

        Assert.Throws<InvalidOperationException>(() => project.SetSectionDuration(section.Id, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetTimeSignature(3, 4));
    }

    [Fact]
    public void SerializedCandidate_RejectsUnknownOrOutOfOrderSyllables()
    {
        var source = LyricLine.Create("one two");
        foreach (var word in source.Words) word.SetSyllables([word.Text]);
        var phrase = source.Phrases[0];
        var candidate = new RhythmCandidate(
            RhythmCandidateId.New(),
            phrase.Id,
            "Invalid",
            RhythmCandidateProvenance.Imported,
            [new RhythmCandidateEvent(RhythmCandidateEventId.New(), SyllableId.New(), 0, new BeatPosition(1, 1, 0))]);

        Assert.Throws<ArgumentException>(() => new LyricLine(
            source.Id,
            source.Text,
            source.Words,
            source.Punctuation,
            source.Phrases,
            source.SyllablePlacements,
            [candidate]));
    }

    private static (SongProject Project, SongSection Section, LyricLine Line, LyricPhrase Phrase, IReadOnlyList<SyllableId> Syllables)
        CreateMappedPhrase()
    {
        var project = SongProject.Create("Options");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var syllables = line.Words.SelectMany(item => item.Syllables).Select(item => item.Id).ToList();
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(1, 3, 0));
        return (project, section, line, line.Phrases[0], syllables);
    }
}
