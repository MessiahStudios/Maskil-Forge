using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class CommandHistoryTests
{
    [Fact]
    public void UndoAndRedo_AddSection_PreservesIdentifier()
    {
        var editor = new ProjectEditor(SongProject.Create("History"));
        var command = new AddSectionCommand(SectionKind.Verse);

        editor.Execute(command);
        var sectionId = Assert.Single(editor.Project.Sections).Id;
        Assert.True(editor.Undo());
        Assert.Empty(editor.Project.Sections);
        Assert.True(editor.Redo());
        Assert.Equal(sectionId, Assert.Single(editor.Project.Sections).Id);
    }

    [Fact]
    public void UndoAndRedo_RenameSection_RestoresBothTitles()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var editor = new ProjectEditor(project);

        editor.Execute(new RenameSectionCommand(section.Id, "Verse One"));
        Assert.Equal("Verse One", section.Title);
        editor.Undo();
        Assert.Equal("Verse", section.Title);
        editor.Redo();
        Assert.Equal("Verse One", section.Title);
    }

    [Fact]
    public void UndoRemove_RestoresSectionAtOriginalIndexWithLyrics()
    {
        var project = SongProject.Create("History");
        var verse = project.AddSection(SectionKind.Verse);
        verse.AddLyricLine("A line worth keeping");
        project.SetSectionDuration(verse.Id, 12);
        var chorus = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new RemoveSectionCommand(verse.Id));
        Assert.Equal(chorus.Id, Assert.Single(project.Sections).Id);
        editor.Undo();

        Assert.Equal([verse.Id, chorus.Id], project.Sections.Select(section => section.Id));
        Assert.Equal("A line worth keeping", project.Sections[0].LyricLines[0].Text);
        Assert.Equal(12, project.Timeline.FindSection(verse.Id).DurationBars);
        Assert.Equal(13, project.Timeline.FindSection(chorus.Id).Start.Bar);
    }

    [Fact]
    public void UndoReorder_RestoresOriginalOrder()
    {
        var project = SongProject.Create("History");
        var verse = project.AddSection(SectionKind.Verse);
        var preChorus = project.AddSection(SectionKind.PreChorus);
        var chorus = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new MoveSectionCommand(chorus.Id, 0));
        Assert.Equal([chorus.Id, verse.Id, preChorus.Id], project.Sections.Select(section => section.Id));
        editor.Undo();
        Assert.Equal([verse.Id, preChorus.Id, chorus.Id], project.Sections.Select(section => section.Id));
    }

    [Fact]
    public void UndoAndRedo_SplitPhrase_PreservesExactPhraseState()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("I thought the way out was through pain");
        var originalPhrase = Assert.Single(line.Phrases);
        var editor = new ProjectEditor(project);

        editor.Execute(new SplitLyricPhraseCommand(section.Id, line.Id, line.Words[4].Id));
        var splitIds = line.Phrases.Select(phrase => phrase.Id).ToList();
        Assert.Equal(2, line.Phrases.Count);
        Assert.All(line.Phrases, phrase => Assert.Equal(PhraseSource.Manual, phrase.Source));

        Assert.True(editor.Undo());
        var restored = Assert.Single(line.Phrases);
        Assert.Equal(originalPhrase.Id, restored.Id);
        Assert.Equal(PhraseSource.Default, restored.Source);
        Assert.Equal(line.Words.Select(word => word.Id), restored.WordIds);

        Assert.True(editor.Redo());
        Assert.Equal(splitIds, line.Phrases.Select(phrase => phrase.Id));
        Assert.All(line.Phrases, phrase => Assert.Equal(PhraseSource.Manual, phrase.Source));
    }

    [Fact]
    public void UndoAndRedo_JoinPhrase_PreservesExactPhraseState()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("I thought the way out was through pain");
        line.SplitPhraseAfter(line.Words[4].Id);
        var splitIds = line.Phrases.Select(phrase => phrase.Id).ToList();
        var editor = new ProjectEditor(project);

        editor.Execute(new JoinLyricPhraseCommand(section.Id, line.Id, line.Phrases[1].Id));
        var joined = Assert.Single(line.Phrases);
        var joinedId = joined.Id;
        Assert.Equal(PhraseSource.Manual, joined.Source);

        Assert.True(editor.Undo());
        Assert.Equal(splitIds, line.Phrases.Select(phrase => phrase.Id));
        Assert.All(line.Phrases, phrase => Assert.Equal(PhraseSource.Manual, phrase.Source));

        Assert.True(editor.Redo());
        var redone = Assert.Single(line.Phrases);
        Assert.Equal(joinedId, redone.Id);
        Assert.Equal(line.Words.Select(word => word.Id), redone.WordIds);
    }

    [Fact]
    public void UndoAndRedo_SyllableStress_RestoresExactLevelAndProvenance()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("through pain");
        var word = line.Words[1];
        line.SetSyllables(word.Id, ["pain"]);
        var syllableId = word.Syllables[0].Id;
        line.SetStress(word.Id, syllableId, StressLevel.Secondary, StressProvenance.Analyzer);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSyllableStressCommand(
            section.Id,
            line.Id,
            word.Id,
            syllableId,
            StressLevel.Emphasized));
        Assert.Equal(StressLevel.Emphasized, word.Syllables[0].Stress?.Level);
        Assert.Equal(StressProvenance.Manual, word.Syllables[0].Stress?.Provenance);

        Assert.True(editor.Undo());
        Assert.Equal(StressLevel.Secondary, word.Syllables[0].Stress?.Level);
        Assert.Equal(StressProvenance.Analyzer, word.Syllables[0].Stress?.Provenance);

        Assert.True(editor.Redo());
        Assert.Equal(StressLevel.Emphasized, word.Syllables[0].Stress?.Level);
        Assert.Equal(StressProvenance.Manual, word.Syllables[0].Stress?.Provenance);
    }

    [Fact]
    public void Undo_ClearSyllableStress_RestoresTheArtistMark()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        var syllableId = word.Syllables[0].Id;
        line.SetStress(word.Id, syllableId, StressLevel.Primary);
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSyllableStressCommand(section.Id, line.Id, word.Id, syllableId, null));
        Assert.Null(word.Syllables[0].Stress);

        Assert.True(editor.Undo());
        Assert.Equal(StressLevel.Primary, word.Syllables[0].Stress?.Level);
        Assert.Equal(StressProvenance.Manual, word.Syllables[0].Stress?.Provenance);
    }

    [Fact]
    public void UndoAndRedo_ProsodicWeight_RestoresExactPatternIdentityAndProvenance()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("through pain");
        var word = line.Words[1];
        line.SetSyllables(word.Id, ["pain"]);
        var phraseId = line.Phrases[0].Id;
        var syllableId = word.Syllables[0].Id;
        line.SetProsodicWeight(
            phraseId,
            syllableId,
            ProsodicWeight.Weak,
            ProsodyProvenance.Imported);
        var patternId = line.Phrases[0].Prosody!.Id;
        var unitId = line.Phrases[0].Prosody!.Units[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new SetProsodicWeightCommand(
            section.Id,
            line.Id,
            phraseId,
            syllableId,
            ProsodicWeight.Strong));
        AssertProsody(line, patternId, unitId, ProsodicWeight.Strong, ProsodyProvenance.Manual);

        Assert.True(editor.Undo());
        AssertProsody(line, patternId, unitId, ProsodicWeight.Weak, ProsodyProvenance.Imported);

        Assert.True(editor.Redo());
        AssertProsody(line, patternId, unitId, ProsodicWeight.Strong, ProsodyProvenance.Manual);
    }

    [Fact]
    public void UndoAndRedo_NewProsodicWeight_ReusesGeneratedIdentifiers()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        var phraseId = line.Phrases[0].Id;
        var syllableId = word.Syllables[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new SetProsodicWeightCommand(
            section.Id,
            line.Id,
            phraseId,
            syllableId,
            ProsodicWeight.Neutral));
        var patternId = line.Phrases[0].Prosody!.Id;
        var unitId = line.Phrases[0].Prosody!.Units[0].Id;

        Assert.True(editor.Undo());
        Assert.Null(line.Phrases[0].Prosody);

        Assert.True(editor.Redo());
        AssertProsody(line, patternId, unitId, ProsodicWeight.Neutral, ProsodyProvenance.Manual);
    }

    private static void AssertProsody(
        LyricLine line,
        ProsodicPatternId patternId,
        ProsodicUnitId unitId,
        ProsodicWeight weight,
        ProsodyProvenance provenance)
    {
        var pattern = Assert.IsType<ProsodicPattern>(line.Phrases[0].Prosody);
        var unit = Assert.Single(pattern.Units);
        Assert.Equal(patternId, pattern.Id);
        Assert.Equal(unitId, unit.Id);
        Assert.Equal(weight, unit.Weight);
        Assert.Equal(provenance, unit.Provenance);
    }

    [Fact]
    public void UndoAndRedo_SyllablePlacement_RestoresExactIdentityAndProvenance()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        var syllableId = word.Syllables[0].Id;
        project.SetSyllablePlacement(
            section.Id,
            line.Id,
            syllableId,
            new BeatPosition(1, 1, 0),
            PlacementProvenance.Imported);
        var placementId = line.SyllablePlacements[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSyllablePlacementCommand(
            section.Id,
            line.Id,
            syllableId,
            new BeatPosition(2, 3, 120)));
        AssertPlacement(line, placementId, new BeatPosition(2, 3, 120), PlacementProvenance.Manual);

        Assert.True(editor.Undo());
        AssertPlacement(line, placementId, new BeatPosition(1, 1, 0), PlacementProvenance.Imported);

        Assert.True(editor.Redo());
        AssertPlacement(line, placementId, new BeatPosition(2, 3, 120), PlacementProvenance.Manual);
    }

    [Fact]
    public void UndoAndRedo_NewSyllablePlacement_ReusesGeneratedIdentifier()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        var syllableId = word.Syllables[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new SetSyllablePlacementCommand(
            section.Id,
            line.Id,
            syllableId,
            new BeatPosition(1, 2, 0)));
        var placementId = line.SyllablePlacements[0].Id;

        Assert.True(editor.Undo());
        Assert.Empty(line.SyllablePlacements);

        Assert.True(editor.Redo());
        AssertPlacement(line, placementId, new BeatPosition(1, 2, 0), PlacementProvenance.Manual);
    }

    [Fact]
    public void UndoAndRedo_BreathPoint_RestoresExactIdentityAndProvenance()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        var syllableId = word.Syllables[0].Id;
        line.SetBreathPoint(syllableId, true, BreathProvenance.Imported);
        var breathId = line.BreathPoints[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new SetBreathPointCommand(section.Id, line.Id, syllableId, false));
        Assert.Empty(line.BreathPoints);

        Assert.True(editor.Undo());
        AssertBreath(line, breathId, syllableId, BreathProvenance.Imported);

        Assert.True(editor.Redo());
        Assert.Empty(line.BreathPoints);
    }

    [Fact]
    public void UndoAndRedo_NewBreathPoint_ReusesGeneratedIdentifier()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        var syllableId = word.Syllables[0].Id;
        var editor = new ProjectEditor(project);

        editor.Execute(new SetBreathPointCommand(section.Id, line.Id, syllableId, true));
        var breathId = line.BreathPoints[0].Id;

        Assert.True(editor.Undo());
        Assert.Empty(line.BreathPoints);

        Assert.True(editor.Redo());
        AssertBreath(line, breathId, syllableId, BreathProvenance.Manual);
    }

    [Fact]
    public void UndoAndRedo_CreativeLocks_RestoresExactIdentity()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("home");
        line.SetSyllables(line.Words[0].Id, ["home"]);
        var editor = new ProjectEditor(project);

        editor.Execute(new LockLyricLineCommand(line.Id));
        var lockId = Assert.Single(project.Locks).Id;

        Assert.True(editor.Undo());
        Assert.Empty(project.Locks);

        Assert.True(editor.Redo());
        Assert.Equal(lockId, Assert.Single(project.Locks).Id);

        editor.Execute(new UnlockCreativeLockCommand(lockId));
        Assert.Empty(project.Locks);
        Assert.True(editor.Undo());
        Assert.Equal(lockId, Assert.Single(project.Locks).Id);
    }

    private static void AssertPlacement(
        LyricLine line,
        SyllablePlacementId placementId,
        BeatPosition position,
        PlacementProvenance provenance)
    {
        var placement = Assert.Single(line.SyllablePlacements);
        Assert.Equal(placementId, placement.Id);
        Assert.Equal(position, placement.Position);
        Assert.Equal(provenance, placement.Provenance);
    }

    private static void AssertBreath(
        LyricLine line,
        BreathPointId breathId,
        SyllableId afterSyllableId,
        BreathProvenance provenance)
    {
        var breath = Assert.Single(line.BreathPoints);
        Assert.Equal(breathId, breath.Id);
        Assert.Equal(afterSyllableId, breath.AfterSyllableId);
        Assert.Equal(provenance, breath.Provenance);
    }

    [Fact]
    public void UndoAndRedo_CapturedRhythmCandidate_RestoresExactIdentifiers()
    {
        var (project, section, line, phrase, _) = CreateRhythmHistoryProject();
        var editor = new ProjectEditor(project);
        var command = new CaptureRhythmCandidateCommand(section.Id, line.Id, phrase.Id, "Option A");

        editor.Execute(command);
        var candidate = Assert.Single(line.RhythmCandidates);
        var candidateId = candidate.Id;
        var eventIds = candidate.Events.Select(item => item.Id).ToList();

        Assert.True(editor.Undo());
        Assert.Empty(line.RhythmCandidates);

        Assert.True(editor.Redo());
        var restored = Assert.Single(line.RhythmCandidates);
        Assert.Equal(candidateId, restored.Id);
        Assert.Equal(eventIds, restored.Events.Select(item => item.Id));
    }

    [Fact]
    public void UndoAndRedo_ApplyRhythmCandidate_RestoresExactPlacementSnapshots()
    {
        var (project, section, line, phrase, syllables) = CreateRhythmHistoryProject();
        var option = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(2, 3, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(2, 1, 0));
        var beforeIds = line.SyllablePlacements.Select(item => item.Id).ToList();
        var editor = new ProjectEditor(project);

        editor.Execute(new ApplyRhythmCandidateCommand(section.Id, line.Id, option.Id));
        var appliedIds = line.SyllablePlacements.Select(item => item.Id).ToList();
        Assert.Equal(option.Events.Select(item => item.BeatPosition), line.SyllablePlacements.Select(item => item.Position));

        Assert.True(editor.Undo());
        Assert.Equal(beforeIds, line.SyllablePlacements.Select(item => item.Id));
        Assert.Equal([new BeatPosition(2, 1, 0), new BeatPosition(2, 3, 0)], line.SyllablePlacements.Select(item => item.Position));

        Assert.True(editor.Redo());
        Assert.Equal(appliedIds, line.SyllablePlacements.Select(item => item.Id));
        Assert.Equal(option.Events.Select(item => item.BeatPosition), line.SyllablePlacements.Select(item => item.Position));
    }

    [Fact]
    public void UndoAndRedo_PhraseSplit_RestoresExactRhythmCandidateStructure()
    {
        var (project, section, line, phrase, _) = CreateRhythmHistoryProject();
        var candidate = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");
        var originalEventIds = candidate.Events.Select(item => item.Id).ToList();
        var editor = new ProjectEditor(project);

        editor.Execute(new SplitLyricPhraseCommand(section.Id, line.Id, line.Words[0].Id));
        var splitCandidateIds = line.RhythmCandidates.Select(item => item.Id).ToList();
        Assert.Equal(2, splitCandidateIds.Count);

        Assert.True(editor.Undo());
        var restored = Assert.Single(line.RhythmCandidates);
        Assert.Equal(candidate.Id, restored.Id);
        Assert.Equal(originalEventIds, restored.Events.Select(item => item.Id));

        Assert.True(editor.Redo());
        Assert.Equal(splitCandidateIds, line.RhythmCandidates.Select(item => item.Id));
        Assert.Equal(originalEventIds, line.RhythmCandidates.SelectMany(item => item.Events).Select(item => item.Id));
    }

    [Fact]
    public void RenameAndRemoveRhythmCandidate_AreReversible()
    {
        var (project, section, line, phrase, _) = CreateRhythmHistoryProject();
        var candidate = project.CaptureRhythmCandidate(section.Id, line.Id, phrase.Id, "Option A");
        var editor = new ProjectEditor(project);

        editor.Execute(new RenameRhythmCandidateCommand(section.Id, line.Id, candidate.Id, "Verse push"));
        Assert.Equal("Verse push", line.RhythmCandidates[0].Label);
        Assert.True(editor.Undo());
        Assert.Equal("Option A", line.RhythmCandidates[0].Label);

        editor.Execute(new RemoveRhythmCandidateCommand(section.Id, line.Id, candidate.Id));
        Assert.Empty(line.RhythmCandidates);
        Assert.True(editor.Undo());
        Assert.Equal(candidate.Id, Assert.Single(line.RhythmCandidates).Id);
    }

    private static (SongProject Project, SongSection Section, LyricLine Line, LyricPhrase Phrase, IReadOnlyList<SyllableId> Syllables)
        CreateRhythmHistoryProject()
    {
        var project = SongProject.Create("History");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var syllables = line.Words.SelectMany(item => item.Syllables).Select(item => item.Id).ToList();
        project.SetSyllablePlacement(section.Id, line.Id, syllables[0], new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, syllables[1], new BeatPosition(1, 3, 0));
        return (project, section, line, line.Phrases[0], syllables);
    }
}
