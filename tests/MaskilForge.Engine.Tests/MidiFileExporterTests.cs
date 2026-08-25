using System.Buffers.Binary;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class MidiFileExporterTests
{
    [Fact]
    public void Export_PreservesTimingPitchVelocityTempoAndMeter()
    {
        var project = SongProject.Create("Portable sketch");
        project.SetTempo(100);
        project.SetTimeSignature(6, 8);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 480, 90);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 4), 0, 480, 80);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.D, Accidental.Natural, 4), 480, 240, 70);

        var first = MidiFileExporter.Export(project);
        var second = MidiFileExporter.Export(project);
        var parsed = Parse(first);

        Assert.Equal(first, second);
        Assert.Equal(0, parsed.Format);
        Assert.Equal(1, parsed.TrackCount);
        Assert.Equal(480, parsed.Division);
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x51, 0x03, 0x09, 0x27, 0xC0 }));
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x58, 0x04, 0x06, 0x03, 0x18, 0x08 }));

        var notes = parsed.Events.Where(item => item.Bytes[0] is 0x80 or 0x90).ToList();
        Assert.Equal(new[]
        {
            (0L, 0x90, 60, 100),
            (0L, 0x90, 64, 90),
            (0L, 0x90, 67, 80),
            (480L, 0x80, 60, 0),
            (480L, 0x80, 64, 0),
            (480L, 0x80, 67, 0),
            (480L, 0x90, 62, 70),
            (720L, 0x80, 62, 0)
        }, notes.Select(item => (item.Tick, (int)item.Bytes[0], (int)item.Bytes[1], (int)item.Bytes[2])));
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xC0);
    }

    [Fact]
    public void Export_DoesNotEmitProgramChangesForAssignedCatalogInstruments()
    {
        var project = SongProject.Create("Assigned cello MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Chorus foundation", [note.Id], "cello");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x90, 48, 100]);
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xC0);
    }

    [Fact]
    public void Export_EmitsDynamicsAsExpressionControlChangeBeforeNoteOn()
    {
        var project = SongProject.Create("Expression MIDI");
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);
        project.AddExpressionCurve(
            "Take dynamics",
            ExpressionCurveKind.Dynamics,
            [new ExpressionCurvePoint(0, 88), new ExpressionCurvePoint(480, 40)]);

        var parsed = Parse(MidiFileExporter.Export(project));
        var timed = parsed.Events
            .Where(item => item.Bytes[0] is 0x80 or 0x90 or 0xB0)
            .Select(item => (item.Tick, (int)item.Bytes[0], (int)item.Bytes[1], (int)item.Bytes[2]))
            .ToArray();

        Assert.Equal(new[]
        {
            (0L, 0xB0, 11, 88),
            (0L, 0x90, 60, 100),
            (480L, 0xB0, 11, 40),
            (480L, 0x80, 60, 0)
        }, timed);
    }

    [Fact]
    public void Export_RequiresApprovedPlayableNotes()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MidiFileExporter.Export(SongProject.Create("Empty")));
        Assert.Contains("does not contain playable notes", exception.Message);
    }

    private static ParsedMidi Parse(byte[] bytes)
    {
        Assert.Equal("MThd"u8.ToArray(), bytes[..4]);
        Assert.Equal(6, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(4, 4)));
        var format = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(8, 2));
        var tracks = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(10, 2));
        var division = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(12, 2));
        Assert.Equal("MTrk"u8.ToArray(), bytes[14..18]);
        var trackLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(18, 4));
        Assert.Equal(bytes.Length, 22 + trackLength);

        var events = new List<ParsedEvent>();
        var offset = 22;
        var end = offset + trackLength;
        long tick = 0;
        while (offset < end)
        {
            tick += ReadVariableLength(bytes, ref offset);
            var status = bytes[offset++];
            if (status == 0xFF)
            {
                var type = bytes[offset++];
                var length = checked((int)ReadVariableLength(bytes, ref offset));
                var data = bytes.AsSpan(offset, length).ToArray();
                offset += length;
                events.Add(new ParsedEvent(tick, new[] { status, type, (byte)length }.Concat(data).ToArray()));
                continue;
            }
            var message = new[] { status, bytes[offset++], bytes[offset++] };
            events.Add(new ParsedEvent(tick, message));
        }
        return new ParsedMidi(format, tracks, division, events);
    }

    private static long ReadVariableLength(byte[] bytes, ref int offset)
    {
        long value = 0;
        byte next;
        do
        {
            next = bytes[offset++];
            value = (value << 7) | (long)(next & 0x7F);
        } while ((next & 0x80) != 0);
        return value;
    }

    private sealed record ParsedMidi(short Format, short TrackCount, short Division, IReadOnlyList<ParsedEvent> Events);
    private sealed record ParsedEvent(long Tick, byte[] Bytes);
}
