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
}
