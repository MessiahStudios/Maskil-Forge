using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

/// <summary>
/// A playable note in absolute project time. It is musical data only: no playback,
/// export, instrument, track, or generated-part ownership is implied.
/// </summary>
public sealed class NoteEvent
{
    [JsonConstructor]
    public NoteEvent(NoteEventId id, RegisteredPitch pitch, long startTick, long durationTicks, int velocity)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A note-event ID is required.", nameof(id));
        Pitch = pitch ?? throw new ArgumentNullException(nameof(pitch));
        if (startTick < 0) throw new ArgumentOutOfRangeException(nameof(startTick), "A note cannot start before tick zero.");
        if (durationTicks < 1) throw new ArgumentOutOfRangeException(nameof(durationTicks), "A note must last at least one tick.");
        if (velocity is < 1 or > 127) throw new ArgumentOutOfRangeException(nameof(velocity), "Velocity must be between 1 and 127.");
        if (startTick > long.MaxValue - durationTicks) throw new ArgumentOutOfRangeException(nameof(durationTicks), "The note end tick is too large.");

        Id = id;
        StartTick = startTick;
        DurationTicks = durationTicks;
        Velocity = velocity;
    }

    public NoteEventId Id { get; }
    public RegisteredPitch Pitch { get; }
    public long StartTick { get; }
    public long DurationTicks { get; }
    public int Velocity { get; }
    [JsonIgnore] public long EndTickExclusive => StartTick + DurationTicks;

    public NoteEvent With(RegisteredPitch pitch, long startTick, long durationTicks, int velocity) =>
        new(Id, pitch, startTick, durationTicks, velocity);
}
