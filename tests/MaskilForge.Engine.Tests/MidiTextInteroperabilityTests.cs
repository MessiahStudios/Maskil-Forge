using System.Buffers.Binary;
using System.Text;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class MidiTextInteroperabilityTests
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    [Fact]
    public void Export_PreservesUtf8SongSectionAndPlacedSyllableText()
    {
        var project = SongProject.Create("Canción 夜");
        project.SetArtist("Café 夜");
        var section = project.AddSection(SectionKind.Chorus, "Refrão 夜");
        var line = section.AddLyricLine("café");
        line.SetSyllables(line.Words[0].Id, ["café"]);
        project.SetSyllablePlacement(
            section.Id,
            line.Id,
            line.Words[0].Syllables[0].Id,
            new BeatPosition(1, 1, 0));
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Café 夜", [note.Id], "cello");

        var first = MidiFileExporter.Export(project);
        var second = MidiFileExporter.Export(project);
        var text = ReadConductorText(first);

        Assert.Equal(first, second);
        Assert.Contains(text, item => item.Type == 0x03 && item.Text == "Canción 夜");
        Assert.Contains(text, item => item.Type == 0x02 && item.Text == "Café 夜");
        Assert.Contains(text, item => item.Type == 0x06 && item.Text == "Refrão 夜");
        Assert.Contains(text, item => item.Type == 0x05 && item.Text == "café");
        Assert.Contains(ReadTextMeta(first, 0x04), item => item.Text == "Café 夜");
    }

    [Fact]
    public void Export_UsesVariableLengthSizeForLongUtf8Metadata()
    {
        var title = string.Concat(Enumerable.Repeat("夜", 70))
            + string.Concat(Enumerable.Repeat("🎵", 20));
        var expected = string.Concat(Enumerable.Repeat("夜", 70))
            + string.Concat(Enumerable.Repeat("🎵", 10));
        var project = SongProject.Create(title);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var text = ReadConductorText(MidiFileExporter.Export(project));
        var trackName = Assert.Single(text, item => item.Type == 0x03);

        Assert.Equal(expected, trackName.Text);
        Assert.Equal(250, trackName.PayloadLength);
        Assert.Equal(2, trackName.LengthByteCount);
    }

    private static IReadOnlyList<TextMetaEvent> ReadTextMeta(byte[] bytes, byte type)
    {
        Assert.Equal("MThd"u8.ToArray(), bytes[..4]);
        var offset = 14;
        var result = new List<TextMetaEvent>();
        var trackCount = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(10, 2));
        for (var track = 0; track < trackCount; track++)
        {
            Assert.Equal("MTrk"u8.ToArray(), bytes.AsSpan(offset, 4).ToArray());
            offset += 4;
            var trackLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            offset += 4;
            var end = offset + trackLength;
            while (offset < end)
            {
                ReadVariableLength(bytes, ref offset, out _);
                var status = bytes[offset++];
                if (status == 0xFF)
                {
                    var metaType = bytes[offset++];
                    var length = checked((int)ReadVariableLength(bytes, ref offset, out var lengthByteCount));
                    var payload = bytes.AsSpan(offset, length).ToArray();
                    offset += length;
                    if (metaType == type)
                        result.Add(new TextMetaEvent(metaType, StrictUtf8.GetString(payload), length, lengthByteCount));
                    continue;
                }

                var high = status & 0xF0;
                offset += high is 0xC0 or 0xD0 ? 1 : 2;
            }

            Assert.Equal(end, offset);
        }

        return result;
    }

    private static IReadOnlyList<TextMetaEvent> ReadConductorText(byte[] bytes)
    {
        Assert.Equal("MThd"u8.ToArray(), bytes[..4]);
        var offset = 14;
        Assert.Equal("MTrk"u8.ToArray(), bytes.AsSpan(offset, 4).ToArray());
        offset += 4;
        var trackLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
        offset += 4;
        var end = offset + trackLength;
        var result = new List<TextMetaEvent>();

        while (offset < end)
        {
            ReadVariableLength(bytes, ref offset, out _);
            Assert.Equal(0xFF, bytes[offset++]);
            var type = bytes[offset++];
            var length = checked((int)ReadVariableLength(bytes, ref offset, out var lengthByteCount));
            var payload = bytes.AsSpan(offset, length).ToArray();
            offset += length;
            if (type is 0x01 or 0x02 or 0x03 or 0x05 or 0x06 or 0x07)
                result.Add(new TextMetaEvent(type, StrictUtf8.GetString(payload), length, lengthByteCount));
            if (type == 0x2F) break;
        }

        Assert.Equal(end, offset);
        return result;
    }

    private static long ReadVariableLength(byte[] bytes, ref int offset, out int byteCount)
    {
        long value = 0;
        byteCount = 0;
        byte next;
        do
        {
            next = bytes[offset++];
            byteCount++;
            value = (value << 7) | (long)(next & 0x7F);
        } while ((next & 0x80) != 0);
        return value;
    }

    private sealed record TextMetaEvent(byte Type, string Text, int PayloadLength, int LengthByteCount);
}
