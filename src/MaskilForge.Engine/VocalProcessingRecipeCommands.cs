using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed class AcceptVocalLowCutCommand(ProjectAssetId assetId, string sourceSha256) : IProjectCommand
{
    private VocalProcessingRecipe? _previous;
    private VocalProcessingRecipe? _accepted;

    public void Execute(SongProject project)
    {
        var recipe = _accepted ?? new VocalProcessingRecipe(assetId, sourceSha256, VocalProcessingRecipe.LowCutProcessorId,
            VocalProcessingRole.CorrectiveTone, VocalProcessingRecipe.LowCutHertz, VocalProcessingRecipe.LowCutQ, DateTimeOffset.UtcNow);
        var previous = project.VocalProcessingRecipes.SingleOrDefault(item => item.AssetId == assetId);
        project.SetVocalProcessingRecipe(recipe);
        if (_accepted is null) _previous = previous;
        _accepted = recipe;
    }

    public void Undo(SongProject project)
    {
        if (_accepted is null) throw new InvalidOperationException("Command has not been executed.");
        if (_previous is null) project.ClearVocalProcessingRecipe(assetId);
        else project.SetVocalProcessingRecipe(_previous);
    }
}

public sealed class ClearVocalProcessingRecipeCommand(ProjectAssetId assetId) : IProjectCommand
{
    private VocalProcessingRecipe? _previous;
    public void Execute(SongProject project) => _previous = project.ClearVocalProcessingRecipe(assetId);
    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.SetVocalProcessingRecipe(_previous);
    }
}
