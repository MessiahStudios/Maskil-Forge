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
/// Expression (CC 11). Unused catalog instruments do not get a track. The
/// conductor track also emits the stored song key as a MIDI key signature when
/// that key has a conventional major or minor spelling, emits one MIDI
/// marker per stored section at that section's start tick, emits one MIDI
/// lyric event per stored syllable placement, emits one MIDI text event
/// per stored harmony chord at that chord's start tick, emits one MIDI
/// cue point per stored breath after a placed syllable at that syllable's
/// song tick, and emits the stored artist name as a MIDI copyright notice
/// when that name is present. The conductor track also emits the stored song
/// description as MIDI text at tick 0 when that description is present. It
/// also emits each decided section structural function as MIDI text at that
/// section's start tick. Each catalog or Unassigned track also emits
/// one MIDI instrument name per stored musical-part label that actually
/// contributes notes to that track. Catalog track names stay the 7.22
/// instrument names. Every track ends no earlier than the current stored
/// song-form boundary, while later musical events remain authoritative.
/// That boundary is the artist's current arrangement plan, not a duration
/// inferred from lyrics or a claim about the final performed recording. The
/// host does not invent sections, unplaced lyrics, a progression that was
/// never written, a timed breath coordinate, an author that was never
/// named, a part label that never exported notes, a description that was
/// never written, or a song role that was never decided. Harmony options,
/// visualization breath offsets, genre, title, raw lyrics, unspecified
/// functions, and arrangement-role names stay off the file. Artist-authored
/// text is bounded by Unicode scalar count and encoded as strict UTF-8; the
/// ASCII subset remains byte-for-byte unchanged.
/// </summary>
public static class MidiFileExporter
{
    public const string ConductorTrackName = "Conductor";
    public const string UnassignedTrackName = "Unassigned";
    private const long MaximumVariableLengthValue = 0x0FFFFFFF;
    private const int MaximumMetaTextRuneCount = 80;
    private static readonly Encoding MidiTextEncoding = new UTF8Encoding(false, true);

    public static byte[] Export(SongProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.NoteEvents.Count == 0)
            throw new InvalidOperationException("Your song does not contain playable notes yet. Create a harmony sketch first.");

        var channels = InstrumentMidiChannelMapper.Map();
        var events = BuildEvents(project, channels);
        var songFormEndTick = SongFormEndTick(project);
        var conductor = events.Where(item => item.Priority <= 1).ToList();
        var performed = events.Where(item => item.Priority >= 2).ToList();
        var usedChannels = performed.Select(item => item.Channel).ToHashSet();

        var tracks = new List<(string Name, IReadOnlyList<MidiEvent> Events, IReadOnlyList<string> InstrumentNames)>
        {
            (SanitizeMetaText(project.Title, ConductorTrackName), conductor, [])
        };
        var unassigned = InstrumentMidiChannelMapper.ZeroBasedChannel(channels.UnassignedMidiChannel);
        if (usedChannels.Contains(unassigned))
            tracks.Add((UnassignedTrackName, performed.Where(item => item.Channel == unassigned).ToList(),
                InstrumentNamesFor(unassigned, project, channels)));

        foreach (var assignment in channels.Assignments)
        {
            var channel = InstrumentMidiChannelMapper.ZeroBasedChannel(assignment.MidiChannel);
            if (!usedChannels.Contains(channel)) continue;
            tracks.Add((assignment.InstrumentName, performed.Where(item => item.Channel == channel).ToList(),
                InstrumentNamesFor(channel, project, channels)));
        }

        using var file = new MemoryStream();
        file.Write("MThd"u8);
        WriteInt32(file, 6);
        WriteInt16(file, 1);
        WriteInt16(file, checked((short)tracks.Count));
        WriteInt16(file, checked((short)project.Timeline.TicksPerQuarterNote));
        foreach (var track in tracks)
        {
            var bytes = WriteTrack(track.Name, track.Events, songFormEndTick, track.InstrumentNames);
            file.Write("MTrk"u8);
            WriteInt32(file, bytes.Length);
            file.Write(bytes);
        }
        return file.ToArray();
    }

    private static long SongFormEndTick(SongProject project)
    {
        if (project.Timeline.SectionPlacements.Count == 0) return 0;

        var endBarExclusive = project.Timeline.SectionPlacements.Max(item => item.EndBarExclusive);
        var endTick = project.Timeline.ToAbsoluteTicks(new MusicalPosition(endBarExclusive, 1, 0));
        if (endTick > MaximumVariableLengthValue)
            throw new InvalidOperationException("The song form extends beyond the timing range supported by a Standard MIDI File.");
        return endTick;
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

        var artist = SanitizeMetaText(project.Artist, string.Empty);
        if (artist.Length > 0)
            events.Add(new MidiEvent(0, 1, -1, 0, Guid.Empty, CopyrightMetaMessage(artist)));

        var description = SanitizeMetaText(project.Description, string.Empty);
        if (description.Length > 0)
            events.Add(new MidiEvent(0, 1, 0, 0, Guid.Empty, DescriptionTextMetaMessage(description)));

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

        var keySignature = MidiKeySignatureMapper.Map(project.Key);
        if (keySignature is not null)
        {
            events.Add(new MidiEvent(0, 1, 1, 0, Guid.Empty, MidiKeySignatureMapper.MetaMessage(keySignature)));
        }

        var markerIndex = 0;
        foreach (var placement in project.Timeline.SectionPlacements)
        {
            var section = project.FindSection(placement.SectionId);
            var tick = project.Timeline.ToAbsoluteTicks(placement.Start);
            if (tick > MaximumVariableLengthValue)
                throw new InvalidOperationException("A section marker extends beyond the timing range supported by a Standard MIDI File.");
            var name = SanitizeMetaText(section.Title, SongSection.DefaultTitle(section.Kind));
            events.Add(new MidiEvent(tick, 1, 2 + markerIndex, 0, Guid.Empty, MarkerMetaMessage(name)));
            markerIndex++;
            if (section.StructuralFunction == StructuralFunction.Unspecified) continue;
            var function = SanitizeMetaText(section.StructuralFunction.ToString(), string.Empty);
            if (function.Length == 0) continue;
            events.Add(new MidiEvent(tick, 1, 4_000 + markerIndex, 0, Guid.Empty, FunctionTextMetaMessage(function)));
        }

        var lyricIndex = 0;
        foreach (var lyric in LyricTimelineProjector.Project(project).Markers
                     .Where(item => item.Kind == LyricTimelineMarkerKind.ActivePlacement))
        {
            if (lyric.AbsoluteTick > MaximumVariableLengthValue)
                throw new InvalidOperationException("A lyric event extends beyond the timing range supported by a Standard MIDI File.");
            var text = SanitizeMetaText(lyric.SyllableText, string.Empty);
            if (text.Length == 0) continue;
            events.Add(new MidiEvent(lyric.AbsoluteTick, 1, 10_000 + lyricIndex, 0, Guid.Empty, LyricMetaMessage(text)));
            lyricIndex++;
        }

        var chordIndex = 0;
        foreach (var placement in project.Timeline.SectionPlacements)
        {
            var section = project.FindSection(placement.SectionId);
            foreach (var chord in section.Harmony.OrderBy(item => item.Start))
            {
                var songPosition = project.ResolveSyllablePosition(section.Id, chord.Start);
                var tick = project.Timeline.ToAbsoluteTicks(songPosition);
                if (tick > MaximumVariableLengthValue)
                    throw new InvalidOperationException("A harmony chord extends beyond the timing range supported by a Standard MIDI File.");
                var text = SanitizeMetaText(chord.Chord.ToDisplayString(), string.Empty);
                if (text.Length == 0) continue;
                events.Add(new MidiEvent(tick, 1, 5_000 + chordIndex, 0, Guid.Empty, ChordTextMetaMessage(text)));
                chordIndex++;
            }
        }

        var breathIndex = 0;
        foreach (var lyric in LyricTimelineProjector.Project(project).Markers
                     .Where(item => item.Kind == LyricTimelineMarkerKind.ActivePlacement && item.HasBreathAfter))
        {
            if (lyric.AbsoluteTick > MaximumVariableLengthValue)
                throw new InvalidOperationException("A breath cue extends beyond the timing range supported by a Standard MIDI File.");
            events.Add(new MidiEvent(lyric.AbsoluteTick, 1, 15_000 + breathIndex, 0, Guid.Empty, CuePointMetaMessage("Breath")));
            breathIndex++;
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

    private static IReadOnlyList<string> InstrumentNamesFor(
        byte channel,
        SongProject project,
        InstrumentMidiChannelMapSet map)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in project.MusicalParts)
        {
            var label = SanitizeMetaText(part.Label, string.Empty);
            if (label.Length == 0) continue;
            var expected = ExpectedChannelFor(part, map);
            if (expected is null || expected.Value != channel) continue;
            if (!part.NoteEventIds.Any(id =>
            {
                var note = project.NoteEvents.FirstOrDefault(item => item.Id == id);
                return note is not null && ChannelFor(note, project, map) == channel;
            })) continue;
            if (!seen.Add(label)) continue;
            names.Add(label);
        }

        return names;
    }

    private static byte? ExpectedChannelFor(MusicalPart part, InstrumentMidiChannelMapSet map)
    {
        if (part.InstrumentProfileId is null)
            return InstrumentMidiChannelMapper.ZeroBasedChannel(map.UnassignedMidiChannel);

        var assignment = map.Assignments.FirstOrDefault(item =>
            string.Equals(item.InstrumentId, part.InstrumentProfileId, StringComparison.Ordinal));
        return assignment is null
            ? null
            : InstrumentMidiChannelMapper.ZeroBasedChannel(assignment.MidiChannel);
    }

    private static byte[] WriteTrack(
        string name,
        IReadOnlyList<MidiEvent> events,
        long minimumEndTick,
        IReadOnlyList<string> instrumentNames)
    {
        using var track = new MemoryStream();
        WriteVariableLength(track, 0);
        WriteMetaText(track, 0x03, name);
        foreach (var label in instrumentNames)
        {
            WriteVariableLength(track, 0);
            WriteMetaText(track, 0x04, label);
        }
        long previousTick = 0;
        foreach (var item in events)
        {
            WriteVariableLength(track, item.Tick - previousTick);
            track.Write(item.Data);
            previousTick = item.Tick;
        }
        WriteVariableLength(track, Math.Max(previousTick, minimumEndTick) - previousTick);
        track.Write([0xFF, 0x2F, 0x00]);
        return track.ToArray();
    }

    private static void WriteMetaText(Stream stream, byte type, string text)
    {
        var payload = MidiTextEncoding.GetBytes(text);
        stream.WriteByte(0xFF);
        stream.WriteByte(type);
        WriteVariableLength(stream, payload.Length);
        stream.Write(payload);
    }

    private static byte[] FunctionTextMetaMessage(string text) => MetaTextMessage(0x01, text);

    private static byte[] DescriptionTextMetaMessage(string text) => MetaTextMessage(0x01, text);

    private static byte[] CopyrightMetaMessage(string text) => MetaTextMessage(0x02, text);

    private static byte[] MarkerMetaMessage(string name) => MetaTextMessage(0x06, name);

    private static byte[] LyricMetaMessage(string text) => MetaTextMessage(0x05, text);

    private static byte[] ChordTextMetaMessage(string text) => MetaTextMessage(0x01, text);

    private static byte[] CuePointMetaMessage(string text) => MetaTextMessage(0x07, text);

    private static byte[] MetaTextMessage(byte type, string text)
    {
        var payload = MidiTextEncoding.GetBytes(text);
        using var message = new MemoryStream();
        message.WriteByte(0xFF);
        message.WriteByte(type);
        WriteVariableLength(message, payload.Length);
        message.Write(payload);
        return message.ToArray();
    }

    private static string SanitizeMetaText(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var text = string.Concat(value.Trim()
            .EnumerateRunes()
            .Where(rune => !Rune.IsControl(rune))
            .Take(MaximumMetaTextRuneCount)
            .Select(rune => rune.ToString()))
            .Trim();
        if (text.Length == 0) return fallback;
        return text;
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

    // Priority: tempo 0, copyright, description, meter, key signature, section markers, structural-function text, chord-symbol text, lyrics, and breath cues 1, program change 2,
    // pitch-bend RPN MSB 3, RPN LSB 4, data entry 5, portamento off 6, dynamics CC 7,
    // note-off 8, note-on 9.
    private sealed record MidiEvent(long Tick, int Priority, int Pitch, byte Channel, Guid NoteId, byte[] Data);
}
