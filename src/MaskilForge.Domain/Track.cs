using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public sealed class Clip
{
    [JsonConstructor]
    public Clip(ClipId id, string name, int startBeat, int lengthInBeats)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A clip ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Clip name is required.", nameof(name));
        if (startBeat < 0) throw new ArgumentOutOfRangeException(nameof(startBeat));
        if (lengthInBeats < 1) throw new ArgumentOutOfRangeException(nameof(lengthInBeats));
        Id = id;
        Name = name.Trim();
        StartBeat = startBeat;
        LengthInBeats = lengthInBeats;
    }

    public ClipId Id { get; }
    public string Name { get; }
    public int StartBeat { get; }
    public int LengthInBeats { get; }
}

public sealed class Track
{
    [JsonConstructor]
    public Track(TrackId id, string name, IReadOnlyList<Clip>? clips = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A track ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Track name is required.", nameof(name));
        Id = id;
        Name = name.Trim();
        var materializedClips = clips?.ToList() ?? [];
        if (materializedClips.Select(clip => clip.Id).Distinct().Count() != materializedClips.Count)
            throw new ArgumentException("Clip IDs must be unique within a track.", nameof(clips));
        Clips = materializedClips;
    }

    public TrackId Id { get; }
    public string Name { get; private set; }
    public IReadOnlyList<Clip> Clips { get; }
}
