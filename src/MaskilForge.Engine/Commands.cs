using MaskilForge.Domain;

namespace MaskilForge.Engine;

public interface IProjectCommand
{
    void Execute(SongProject project);
    void Undo(SongProject project);
}

public sealed class AddSectionCommand(SectionKind kind, string? title = null) : IProjectCommand
{
    private SongSection? _section;

    public SectionId? SectionId => _section?.Id;

    public void Execute(SongProject project)
    {
        _section ??= SongSection.Create(kind, title);
        project.InsertSection(project.Sections.Count, _section);
    }

    public void Undo(SongProject project)
    {
        if (_section is null) throw new InvalidOperationException("Command has not been executed.");
        project.RemoveSection(_section.Id);
    }
}

public sealed class RenameSectionCommand(SectionId sectionId, string title) : IProjectCommand
{
    private string? _previousTitle;

    public void Execute(SongProject project)
    {
        var section = project.FindSection(sectionId);
        _previousTitle ??= section.Title;
        section.Rename(title);
    }

    public void Undo(SongProject project)
    {
        if (_previousTitle is null) throw new InvalidOperationException("Command has not been executed.");
        project.RenameSection(sectionId, _previousTitle);
    }
}

public sealed class MoveSectionCommand(SectionId sectionId, int targetIndex) : IProjectCommand
{
    private int? _previousIndex;

    public void Execute(SongProject project)
    {
        _previousIndex ??= project.IndexOf(sectionId);
        project.MoveSection(sectionId, targetIndex);
    }

    public void Undo(SongProject project)
    {
        if (_previousIndex is null) throw new InvalidOperationException("Command has not been executed.");
        project.MoveSection(sectionId, _previousIndex.Value);
    }
}

public sealed class SetSectionDurationCommand(SectionId sectionId, int durationBars) : IProjectCommand
{
    private int? _previousDurationBars;

    public void Execute(SongProject project)
    {
        _previousDurationBars ??= project.Timeline.FindSection(sectionId).DurationBars;
        project.SetSectionDuration(sectionId, durationBars);
    }

    public void Undo(SongProject project)
    {
        if (_previousDurationBars is null) throw new InvalidOperationException("Command has not been executed.");
        project.SetSectionDuration(sectionId, _previousDurationBars.Value);
    }
}

public sealed class RemoveSectionCommand(SectionId sectionId) : IProjectCommand
{
    private SongSection? _removedSection;
    private int? _removedIndex;
    private int? _removedDurationBars;

    public void Execute(SongProject project)
    {
        var removed = project.RemoveSection(sectionId);
        _removedSection ??= removed.Section;
        _removedIndex ??= removed.Index;
        _removedDurationBars ??= removed.DurationBars;
    }

    public void Undo(SongProject project)
    {
        if (_removedSection is null || _removedIndex is null || _removedDurationBars is null)
            throw new InvalidOperationException("Command has not been executed.");
        project.InsertSection(_removedIndex.Value, _removedSection, _removedDurationBars.Value);
    }
}
