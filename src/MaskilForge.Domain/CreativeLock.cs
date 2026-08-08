using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum CreativeLockScope
{
    LyricLine,
    PhraseRhythm
}

public enum LockProvenance
{
    Manual,
    Analyzer,
    Imported
}

/// <summary>
/// An artist-authored protection over accepted lyric text or phrase rhythm decisions.
/// </summary>
public sealed class CreativeLock
{
    [JsonConstructor]
    public CreativeLock(
        CreativeLockId id,
        CreativeLockScope scope,
        LyricLineId lineId,
        LyricPhraseId? phraseId,
        LockProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A creative lock ID is required.", nameof(id));
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope), "Creative lock scope is invalid.");
        if (lineId.Value == Guid.Empty) throw new ArgumentException("A lyric line ID is required.", nameof(lineId));
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Lock provenance is invalid.");
        if (scope == CreativeLockScope.LyricLine && phraseId is not null)
            throw new ArgumentException("A lyric-line lock cannot reference a phrase.", nameof(phraseId));
        if (scope == CreativeLockScope.PhraseRhythm)
        {
            if (phraseId is null || phraseId.Value.Value == Guid.Empty)
                throw new ArgumentException("A phrase-rhythm lock requires a lyric phrase ID.", nameof(phraseId));
        }

        Id = id;
        Scope = scope;
        LineId = lineId;
        PhraseId = phraseId;
        Provenance = provenance;
    }

    public CreativeLockId Id { get; }
    public CreativeLockScope Scope { get; }
    public LyricLineId LineId { get; }
    public LyricPhraseId? PhraseId { get; }
    public LockProvenance Provenance { get; }
}
