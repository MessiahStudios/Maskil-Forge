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
        var chorus = project.AddSection(SectionKind.Chorus);
        var editor = new ProjectEditor(project);

        editor.Execute(new RemoveSectionCommand(verse.Id));
        Assert.Equal(chorus.Id, Assert.Single(project.Sections).Id);
        editor.Undo();

        Assert.Equal([verse.Id, chorus.Id], project.Sections.Select(section => section.Id));
        Assert.Equal("A line worth keeping", project.Sections[0].LyricLines[0].Text);
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
}
