using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed class SetVocalProcessingChainCommand(IReadOnlyList<VocalProcessingRole> roles) : IProjectCommand
{
    private VocalProcessingChain? _previous;
    private VocalProcessingChain? _applied;

    public void Execute(SongProject project)
    {
        if (_applied is null)
        {
            var next = new VocalProcessingChain(roles, DateTimeOffset.UtcNow);
            _previous = project.VocalProcessingChain;
            _applied = next;
        }
        project.SetVocalProcessingChain(_applied);
    }

    public void Undo(SongProject project)
    {
        if (_applied is null) throw new InvalidOperationException("Command has not been executed.");
        if (_previous is null) project.ClearVocalProcessingChain();
        else project.SetVocalProcessingChain(_previous);
    }
}

public sealed class ClearVocalProcessingChainCommand : IProjectCommand
{
    private VocalProcessingChain? _previous;

    public void Execute(SongProject project)
    {
        _previous ??= project.VocalProcessingChain
            ?? throw new InvalidOperationException("The project has no vocal processing chain to clear.");
        project.ClearVocalProcessingChain();
    }

    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.SetVocalProcessingChain(_previous);
    }
}
