using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed class ProjectEditor(SongProject project)
{
    private readonly Stack<IProjectCommand> _undo = [];
    private readonly Stack<IProjectCommand> _redo = [];

    public SongProject Project { get; } = project ?? throw new ArgumentNullException(nameof(project));
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Execute(IProjectCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute(Project);
        _undo.Push(command);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var command)) return false;
        command.Undo(Project);
        _redo.Push(command);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var command)) return false;
        command.Execute(Project);
        _undo.Push(command);
        return true;
    }
}
