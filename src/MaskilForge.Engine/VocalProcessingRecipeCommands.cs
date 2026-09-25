using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed class AcceptVocalLowCutCommand(ProjectAssetId assetId, string sourceSha256,
    double cutoffHertz = VocalProcessingRecipe.LowCutHertz, double q = VocalProcessingRecipe.LowCutQ) : IProjectCommand
{
    private VocalProcessingRecipe? _previous;
    private VocalProcessingRecipe? _accepted;

    public void Execute(SongProject project)
    {
        var processorId = cutoffHertz == VocalProcessingRecipe.LowCutHertz && q == VocalProcessingRecipe.LowCutQ
            ? VocalProcessingRecipe.LowCutProcessorId : VocalProcessingRecipe.AdjustableLowCutProcessorId;
        var recipe = _accepted ?? new VocalProcessingRecipe(assetId, sourceSha256, processorId,
            VocalProcessingRole.CorrectiveTone, cutoffHertz, q, DateTimeOffset.UtcNow);
        var previous = project.VocalProcessingRecipes.SingleOrDefault(item => item.AssetId == assetId && item.Role == VocalProcessingRole.CorrectiveTone);
        project.SetVocalProcessingRecipe(recipe);
        if (_accepted is null) _previous = previous;
        _accepted = recipe;
    }

    public void Undo(SongProject project)
    {
        if (_accepted is null) throw new InvalidOperationException("Command has not been executed.");
        if (_previous is null) project.ClearVocalProcessingRecipe(assetId, VocalProcessingRole.CorrectiveTone);
        else project.SetVocalProcessingRecipe(_previous);
    }
}

public sealed class AcceptVocalLevelControlCommand(ProjectAssetId assetId, string sourceSha256) : IProjectCommand
{
    private VocalProcessingRecipe? _previous;
    private VocalProcessingRecipe? _accepted;

    public void Execute(SongProject project)
    {
        var recipe = _accepted ?? VocalProcessingRecipe.FixedLevelControl(assetId, sourceSha256, DateTimeOffset.UtcNow);
        var previous = project.VocalProcessingRecipes.SingleOrDefault(item =>
            item.AssetId == assetId && item.Role == VocalProcessingRole.TransparentDynamics);
        project.SetVocalProcessingRecipe(recipe);
        if (_accepted is null) _previous = previous;
        _accepted = recipe;
    }

    public void Undo(SongProject project)
    {
        if (_accepted is null) throw new InvalidOperationException("Command has not been executed.");
        if (_previous is null) project.ClearVocalProcessingRecipe(assetId, VocalProcessingRole.TransparentDynamics);
        else project.SetVocalProcessingRecipe(_previous);
    }
}

public sealed class ClearVocalProcessingRecipeCommand(ProjectAssetId assetId, VocalProcessingRole role = VocalProcessingRole.CorrectiveTone) : IProjectCommand
{
    private VocalProcessingRecipe? _previous;
    public void Execute(SongProject project) => _previous = project.ClearVocalProcessingRecipe(assetId, role);
    public void Undo(SongProject project)
    {
        if (_previous is null) throw new InvalidOperationException("Command has not been executed.");
        project.SetVocalProcessingRecipe(_previous);
    }
}
