using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// Translates the project's approved playable notes and timeline metadata into a
/// format-1 Standard MIDI File. A conductor track holds tempo and meter. Each
/// used inspectable channel gets its own named track: Unassigned, then catalog
/// instruments in catalog order. Notes and tagged dynamics use inspectable MIDI
/// channels from the catalog map. Named pitched parts also emit inspectable
/// General MIDI program changes on those channels. Tagged dynamics use each
/// instrument's inspectable controller: flute swell is Breath Controller (CC 2),
/// synth-lead swell is Brightness (CC 74), and other catalog swells stay
/// Expression (CC 11). Named cello, violin, acoustic-guitar, and electric-guitar
/// parts declare an inspectable pitch-bend range of ±2 semitones. MIDI does not
/// move the pitch wheel. Synth-lead portamento is CC 65 and stays off so stored
/// notes stay discrete. Drum-kit notes stay on channel 10 without a program
/// change. Unassigned notes and untagged dynamics stay on channel 1 with
/// Expression (CC 11). Unused catalog instruments do not get a track.
/// </summary>
public static class MidiFileExporter
{
    public const string ConductorTrackName = "Conductor";
    public const string UnassignedTrackName = "Unassigned";
    private const long MaximumVariableLengthValue = 0x0FFFFFFF;
    private const int MaximumTrackNameLength = 80;

    public static byte[] Export(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.NoteEvents.Count == 0)
            throw new InvalidOperationException("Your song does not contain playable notes yet. Create a harmony sketch first.");

        var channels = InstrumentMidiChannelMapper.Map();
        var events = BuildEvents(project, channels);
        var conductor = events.Where(item => item.Priority <= 1).ToList();
        var performed = events.Where(item => item.Priority >= 2).ToList();
        var usedChannels = performed.Select(item => item.Channel).ToHashSet();

        var tracks = new List<(string Name, IReadOnlyList<MidiEvent> Events)>
        {
            (SanitizeTrackName(project.Title, ConductorTrackName), conductor)
        };
        var unassigned = InstrumentMidiChannelMapper.ZeroBasedChannel(channels.UnassignedMidiChannel);
        if (usedChannels.Contains(unassigned))
            tracks.Add((UnassignedTrackName, performed.Where(item => item.Channel == unassigned).ToList()));

        foreach (var assignment in channels.Assignments)
        {
            var channel = InstrumentMidiChannelMapper.ZeroBasedChannel(assignment.MidiChannel);
            if (!usedChannels.Contains(channel)) continue;
            tracks.Add((assignment.InstrumentName, performed.Where(item => item.Channel == channel).ToList()));
        }

        using var file = new MemoryStream();
        file.Write("MThd"u8);
        WriteInt32(file, 6);
        WriteInt16(file, 1);
        WriteInt16(file, checked((short)tracks.Count));
        WriteInt16(file, checked((short)project.Timeline.TicksPerQuarterNote));
        foreach (var track in tracks)
        {
            var bytes = WriteTrack(track.Name, track.Events);
            file.Write("MTrk"u8);
            WriteInt32(file, bytes.Length);
            file.Write(bytes);
        }
        return file.ToArray();
    }

    private static IReadOnlyList<MidiEvent> BuildEvents(SongProject project, InstrumentMidiChannelMapSet channels)
    {
        var programs = InstrumentMidiProgramMapper.Map();
        var controllers = InstrumentMidiControllerMapper.Map();
        var pitchBends = InstrumentMidiPitchBendMapper.Map();
        var portamentos = InstrumentMidiPortamentoMapper.Map();
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

        var usedInstrumentIds = project.NoteEvents
            .Select(note => InstrumentIdFor(note, project, channels))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var program in programs.Assignments)
        {
            if (!program.Applicable || program.MidiProgram is null) continue;
            if (!usedInstrumentIds.Contains(program.InstrumentId)) continue;
            var channelAssignment = channels.Assignments.First(item =>
                string.Equals(item.InstrumentId, program.InstrumentId, StringComparison.Ordinal));
            var channel = InstrumentMidiChannelMapper.ZeroBasedChannel(channelAssignment.MidiChannel);
            var programByte = InstrumentMidiProgramMapper.ZeroBasedProgram(program.MidiProgram.Value);
            events.Add(new MidiEvent(0, 2, 0, channel, Guid.Empty, [(byte)(0xC0 | channel), programByte]));
        }

        foreach (var bend in pitchBends.Assignments)
        {
            if (!bend.Applicable || bend.RangeSemitones is null) continue;
            if (!usedInstrumentIds.Contains(bend.InstrumentId)) continue;
            var channelAssignment = channels.Assignments.First(item =>
                string.Equals(item.InstrumentId, bend.InstrumentId, StringComparison.Ordinal));
            var channel = InstrumentMidiChannelMapper.ZeroBasedChannel(channelAssignment.MidiChannel);
            var semitones = checked((byte)bend.RangeSemitones.Value);
            events.Add(new MidiEvent(0, 3, InstrumentMidiPitchBendMapper.RpnMsbController, channel, Guid.Empty,
                [(byte)(0xB0 | channel), InstrumentMidiPitchBendMapper.RpnMsbController, 0]));
            events.Add(new MidiEvent(0, 4, InstrumentMidiPitchBendMapper.RpnLsbController, channel, Guid.Empty,
                [(byte)(0xB0 | channel), InstrumentMidiPitchBendMapper.RpnLsbController, 0]));
            events.Add(new MidiEvent(0, 5, InstrumentMidiPitchBendMapper.DataEntryMsbController, channel, Guid.Empty,
                [(byte)(0xB0 | channel), InstrumentMidiPitchBendMapper.DataEntryMsbController, semitones]));
        }

        foreach (var portamento in portamentos.Assignments)
        {
            if (!portamento.Applicable || portamento.ControllerNumber is null) continue;
            if (!usedInstrumentIds.Contains(portamento.InstrumentId)) continue;
            var channelAssignment = channels.Assignments.First(item =>
                string.Equals(item.InstrumentId, portamento.InstrumentId, StringComparison.Ordinal));
            var channel = InstrumentMidiChannelMapper.ZeroBasedChannel(channelAssignment.MidiChannel);
            events.Add(new MidiEvent(0, 6, portamento.ControllerNumber.Value, channel, Guid.Empty,
            [
                (byte)(0xB0 | channel),
                checked((byte)portamento.ControllerNumber.Value),
                InstrumentMidiPortamentoMapper.PortamentoOffValue
            ]));
        }

        foreach (var curve in project.ExpressionCurves)
        {
            if (curve.Kind != ExpressionCurveKind.Dynamics) continue;
            foreach (var point in curve.Points)
            {
                if (point.Tick > MaximumVariableLengthValue)
                    throw new InvalidOperationException("An expression curve extends beyond the timing range supported by a Standard MIDI File.");
                var channel = ChannelFor(curve, channels);
                var controller = ControllerFor(curve, controllers);
                events.Add(new MidiEvent(point.Tick, 7, controller, channel, curve.Id.Value, [(byte)(0xB0 | channel), controller, checked((byte)point.Value)]));
            }
        }

        foreach (var note in project.NoteEvents)
        {
            if (note.EndTickExclusive > MaximumVariableLengthValue)
                throw new InvalidOperationException("A playable note extends beyond the timing range supported by a Standard MIDI File.");
            var pitch = checked((byte)note.Pitch.MidiNumber);
            var channel = ChannelFor(note, project, channels);
            events.Add(new MidiEvent(note.StartTick, 9, pitch, channel, note.Id.Value, [(byte)(0x90 | channel), pitch, checked((byte)note.Velocity)]));
            events.Add(new MidiEvent(note.EndTickExclusive, 8, pitch, channel, note.Id.Value, [(byte)(0x80 | channel), pitch, 0x00]));
        }

        return events
            .OrderBy(item => item.Tick)
            .ThenBy(item => item.Priority)
            .ThenBy(item => item.Pitch)
            .ThenBy(item => item.Channel)
            .ThenBy(item => item.NoteId)
            .ToList();
    }

    private static byte ChannelFor(NoteEvent note, SongProject project, InstrumentMidiChannelMapSet map)
    {
        var instrumentId = InstrumentIdFor(note, project, map);
        if (instrumentId is null)
            return InstrumentMidiChannelMapper.ZeroBasedChannel(map.UnassignedMidiChannel);

        var assignment = map.Assignments.First(item =>
            string.Equals(item.InstrumentId, instrumentId, StringComparison.Ordinal));
        return InstrumentMidiChannelMapper.ZeroBasedChannel(assignment.MidiChannel);
    }

    private static string? InstrumentIdFor(NoteEvent note, SongProject project, InstrumentMidiChannelMapSet map)
    {
        var partIds = project.MusicalParts
            .Where(part => part.InstrumentProfileId is not null && part.NoteEventIds.Contains(note.Id))
            .Select(part => part.InstrumentProfileId!)
            .ToHashSet(StringComparer.Ordinal);
        if (partIds.Contains(DrumKitGeneralMidiMapper.DrumKitInstrumentId))
            return DrumKitGeneralMidiMapper.DrumKitInstrumentId;

        return map.Assignments.FirstOrDefault(item => partIds.Contains(item.InstrumentId))?.InstrumentId;
    }

    private static byte ControllerFor(ExpressionCurve curve, InstrumentMidiControllerMapSet map)
    {
        if (string.IsNullOrEmpty(curve.InstrumentProfileId))
            return InstrumentMidiControllerMapper.ExpressionControllerNumber;

        var assignment = map.Assignments.FirstOrDefault(item =>
            string.Equals(item.InstrumentId, curve.InstrumentProfileId, StringComparison.Ordinal));
        if (assignment is null || !assignment.Applicable || assignment.ControllerNumber is null)
            return InstrumentMidiControllerMapper.ExpressionControllerNumber;

        return checked((byte)assignment.ControllerNumber.Value);
    }

    private static byte ChannelFor(ExpressionCurve curve, InstrumentMidiChannelMapSet map)
    {
        if (string.IsNullOrEmpty(curve.InstrumentProfileId))
            return InstrumentMidiChannelMapper.ZeroBasedChannel(map.UnassignedMidiChannel);

        var assignment = map.Assignments.FirstOrDefault(item =>
            string.Equals(item.InstrumentId, curve.InstrumentProfileId, StringComparison.Ordinal));
        return assignment is null
            ? InstrumentMidiChannelMapper.ZeroBasedChannel(map.UnassignedMidiChannel)
            : InstrumentMidiChannelMapper.ZeroBasedChannel(assignment.MidiChannel);
    }

    private static byte[] WriteTrack(string name, IReadOnlyList<MidiEvent> events)
    {
        using var track = new MemoryStream();
        WriteVariableLength(track, 0);
        WriteMetaText(track, 0x03, name);
        long previousTick = 0;
        foreach (var item in events)
        {
            WriteVariableLength(track, item.Tick - previousTick);
            track.Write(item.Data);
            previousTick = item.Tick;
        }
        WriteVariableLength(track, 0);
        track.Write([0xFF, 0x2F, 0x00]);
        return track.ToArray();
    }

    private static void WriteMetaText(Stream stream, byte type, string text)
    {
        var payload = Encoding.ASCII.GetBytes(text);
        stream.WriteByte(0xFF);
        stream.WriteByte(type);
        WriteVariableLength(stream, payload.Length);
        stream.Write(payload);
    }

    private static string SanitizeTrackName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = new string(value.Trim().Where(character => character is >= ' ' and <= '~').ToArray()).Trim();
        if (text.Length == 0) return fallback;
        return text.Length <= MaximumTrackNameLength ? text : text[..MaximumTrackNameLength].TrimEnd();
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

    // Priority: tempo 0, meter 1, program change 2, pitch-bend RPN MSB 3, RPN LSB 4,
    // data entry 5, portamento off 6, dynamics CC 7, note-off 8, note-on 9.
    private sealed record MidiEvent(long Tick, int Priority, int Pitch, byte Channel, Guid NoteId, byte[] Data);
}
