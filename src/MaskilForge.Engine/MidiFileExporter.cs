using System.Buffers.Binary;
using System.Numerics;
using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// Translates the project's approved playable notes and timeline metadata into a
/// format-0 Standard MIDI File. Dynamics curves emit as CC 11 on channel 0 even when
/// tagged with a catalog instrument. Notes on a musical part that names drum-kit
/// export on channel 10. Other notes stay on channel 0. Export never emits
/// program changes or channels for pitched catalog instruments.
/// </summary>
public static class MidiFileExporter
{
    private const long MaximumVariableLengthValue = 0x0FFFFFFF;
    private const byte DrumKitChannel = 9;

    public static byte[] Export(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.NoteEvents.Count == 0)
            throw new InvalidOperationException("Your song does not contain playable notes yet. Create a harmony sketch first.");

        var events = BuildEvents(project);
        using var track = new MemoryStream();
        long previousTick = 0;
        foreach (var item in events)
        {
            WriteVariableLength(track, item.Tick - previousTick);
            track.Write(item.Data);
            previousTick = item.Tick;
        }
        WriteVariableLength(track, 0);
        track.Write([0xFF, 0x2F, 0x00]);

        using var file = new MemoryStream();
        file.Write("MThd"u8);
        WriteInt32(file, 6);
        WriteInt16(file, 0);
        WriteInt16(file, 1);
        WriteInt16(file, checked((short)project.Timeline.TicksPerQuarterNote));
        file.Write("MTrk"u8);
        WriteInt32(file, checked((int)track.Length));
        track.Position = 0;
        track.CopyTo(file);
        return file.ToArray();
    }

    private static IReadOnlyList<MidiEvent> BuildEvents(SongProject project)
    {
        var events = new List<MidiEvent>();
        foreach (var tempo in project.Timeline.TempoMap.Events)
        {
            var tick = checked((long)tempo.Beat * project.Timeline.TicksPerQuarterNote);
            var microseconds = decimal.ToInt32(decimal.Round(60_000_000m / tempo.BeatsPerMinute, 0, MidpointRounding.AwayFromZero));
            events.Add(new MidiEvent(tick, 0, 0, 0, Guid.Empty,
            [
                0xFF, 0x51, 0x03,
                (byte)(microseconds >> 16),
                (byte)(microseconds >> 8),
                (byte)microseconds
            ]));
        }

        foreach (var meter in project.Timeline.TimeSignatureMap.Events)
        {
            var tick = checked((long)meter.Beat * project.Timeline.TicksPerQuarterNote);
            var denominatorPower = checked((byte)BitOperations.Log2((uint)meter.Denominator));
            events.Add(new MidiEvent(tick, 1, 0, 0, Guid.Empty,
            [
                0xFF, 0x58, 0x04,
                checked((byte)meter.Numerator),
                denominatorPower,
                24,
                8
            ]));
        }

        foreach (var curve in project.ExpressionCurves)
        {
            if (curve.Kind != ExpressionCurveKind.Dynamics) continue;
            foreach (var point in curve.Points)
            {
                if (point.Tick > MaximumVariableLengthValue)
                    throw new InvalidOperationException("An expression curve extends beyond the timing range supported by a Standard MIDI File.");
                events.Add(new MidiEvent(point.Tick, 2, 11, 0, curve.Id.Value, [0xB0, 11, checked((byte)point.Value)]));
            }
        }

        foreach (var note in project.NoteEvents)
        {
            if (note.EndTickExclusive > MaximumVariableLengthValue)
                throw new InvalidOperationException("A playable note extends beyond the timing range supported by a Standard MIDI File.");
            var pitch = checked((byte)note.Pitch.MidiNumber);
            var channel = ChannelFor(note, project);
            events.Add(new MidiEvent(note.StartTick, 4, pitch, channel, note.Id.Value, [(byte)(0x90 | channel), pitch, checked((byte)note.Velocity)]));
            events.Add(new MidiEvent(note.EndTickExclusive, 3, pitch, channel, note.Id.Value, [(byte)(0x80 | channel), pitch, 0x00]));
        }

        return events
            .OrderBy(item => item.Tick)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Pitch)
            .ThenBy(item => item.Channel)
            .ThenBy(item => item.NoteId)
            .ToList();
    }

    private static byte ChannelFor(NoteEvent note, SongProject project)
    {
        var onKit = project.MusicalParts.Any(part =>
            string.Equals(part.InstrumentProfileId, DrumKitGeneralMidiMapper.DrumKitInstrumentId, StringComparison.Ordinal)
            && part.NoteEventIds.Contains(note.Id));
        return onKit ? DrumKitChannel : (byte)0;
    }

    private static void WriteVariableLength(Stream stream, long value)
    {
        if (value is < 0 or > MaximumVariableLengthValue)
            throw new InvalidOperationException("A MIDI event delta is outside the Standard MIDI File timing range.");
        Span<byte> buffer = stackalloc byte[4];
        var index = buffer.Length - 1;
        buffer[index] = (byte)(value & 0x7F);
        while ((value >>= 7) > 0) buffer[--index] = (byte)((value & 0x7F) | 0x80);
        stream.Write(buffer[index..]);
    }

    private static void WriteInt16(Stream stream, short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    // Priority: tempo 0, meter 1, CC 2, note-off 3, note-on 4.
    private sealed record MidiEvent(long Tick, int Priority, int Pitch, byte Channel, Guid NoteId, byte[] Data);
}
