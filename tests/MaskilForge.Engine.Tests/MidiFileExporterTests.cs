using System.Buffers.Binary;
using System.Text;
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
        Assert.Equal(1, parsed.Format);
        Assert.Equal(2, parsed.TrackCount);
        Assert.Equal(480, parsed.Division);
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x51, 0x03, 0x09, 0x27, 0xC0 }));
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x58, 0x04, 0x06, 0x03, 0x18, 0x08 }));
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x59, 0x02, 0x00, 0x00 }));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x01);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x02);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x04);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x06);

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
    public void Export_EmitsInspectableProgramChangeForAssignedCello()
    {
        var project = SongProject.Create("Assigned cello MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Chorus foundation", [note.Id], "cello");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x91, 48, 100]);
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xC1, 42 }));
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xB1, 101, 0 }));
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xB1, 100, 0 }));
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xB1, 6, 2 }));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes[0] == 0xC0);
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xE0);
    }

    [Fact]
    public void Export_PlacesNamedDrumKitHitsOnChannelTenWithoutAProgramChange()
    {
        var project = SongProject.Create("Assigned kit MIDI");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var kitNote = project.AddNoteEvent(DrumKitGeneralMidiMapper.AcousticBassDrumPitch, 0, 120, 102);
        var celloNote = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        var unassigned = project.AddNoteEvent(DrumKitGeneralMidiMapper.AcousticBassDrumPitch, 240, 120, 80);
        project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Verse pulse", [kitNote.Id], "drum-kit");
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Verse cello", [celloNote.Id], "cello");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x99, 36, 102]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0x89, 36, 0]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0x91, 48, 100]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0x90, 36, 80]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0xC1, 42]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes is [0x90, 36, 102]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes[0] is 0xC0 or 0xC9);
    }

    [Fact]
    public void Export_PlacesNamedPitchedInstrumentsOnInspectableChannelsWithProgramChanges()
    {
        var project = SongProject.Create("Assigned catalog MIDI");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        project.SetSectionRole(section.Id, ArrangementRole.Texture);
        project.SetSectionRole(section.Id, ArrangementRole.HookReinforcement);
        var cello = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        var guitar = project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 3), 0, 480, 90);
        var electric = project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3), 0, 480, 80);
        var unassigned = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 240, 120, 70);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Verse cello", [cello.Id], "cello");
        project.AddMusicalPart(section.Id, ArrangementRole.Texture, "Verse guitar", [guitar.Id], "acoustic-guitar");
        project.AddMusicalPart(section.Id, ArrangementRole.HookReinforcement, "Verse electric", [electric.Id], "electric-guitar");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x91, 48, 100]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0x92, 52, 90]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0x9C, 55, 80]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0x90, 60, 70]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0xC1, 42]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0xC2, 25]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0xCC, 30]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes[0] == 0xC0);
    }

    [Fact]
    public void Export_PrefersDrumKitChannelWhenANoteAlsoBelongsToAPitchedPart()
    {
        var project = SongProject.Create("Shared kit note MIDI");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var shared = project.AddNoteEvent(DrumKitGeneralMidiMapper.AcousticBassDrumPitch, 0, 120, 102);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Verse cello", [shared.Id], "cello");
        project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Verse pulse", [shared.Id], "drum-kit");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x99, 36, 102]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes is [0x91, 36, 102]);
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xC0);
        Assert.Contains(parsed.Events, item => item.Bytes.SequenceEqual(InstrumentName("Verse pulse")));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(InstrumentName("Verse cello")));
    }

    [Fact]
    public void Export_UsesCatalogOrderWhenANoteBelongsToMultiplePitchedParts()
    {
        var project = SongProject.Create("Shared pitched MIDI");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        project.SetSectionRole(section.Id, ArrangementRole.Texture);
        var shared = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.Texture, "Verse guitar", [shared.Id], "acoustic-guitar");
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Verse cello", [shared.Id], "cello");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x91, 48, 100]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes is [0x92, 48, 100]);
        Assert.Contains(parsed.Events, item => item.Bytes is [0xC1, 42]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes[0] == 0xC2);
    }

    [Fact]
    public void Export_LeavesUnassignedDrumPitchOnChannelOne()
    {
        var project = SongProject.Create("Unassigned C2 MIDI");
        project.AddNoteEvent(DrumKitGeneralMidiMapper.AcousticBassDrumPitch, 0, 120, 96);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x90, 36, 96]);
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0x0F) == 0x09);
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xC0);
    }

    [Fact]
    public void Export_EmitsAssignedInstrumentDynamicsAfterTheProgramChange()
    {
        var project = SongProject.Create("Cello swell MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Chorus foundation", [note.Id], "cello");
        project.AddExpressionCurve(
            "Cello swell",
            ExpressionCurveKind.Dynamics,
            [new ExpressionCurvePoint(0, 88)],
            "cello");

        var parsed = Parse(MidiFileExporter.Export(project));

        var timed = parsed.Events
            .Where(item => item.Bytes[0] is 0xC1 or 0xB1 or 0x91 or 0x81)
            .Select(item => (item.Tick, item.Bytes[0], item.Bytes.ElementAtOrDefault(1), item.Bytes.ElementAtOrDefault(2)))
            .ToArray();

        Assert.Equal(new[]
        {
            (0L, (byte)0xC1, (byte)42, (byte)0),
            (0L, (byte)0xB1, (byte)101, (byte)0),
            (0L, (byte)0xB1, (byte)100, (byte)0),
            (0L, (byte)0xB1, (byte)6, (byte)2),
            (0L, (byte)0xB1, (byte)11, (byte)88),
            (0L, (byte)0x91, (byte)48, (byte)100),
            (480L, (byte)0x81, (byte)48, (byte)0)
        }, timed);
    }

    [Fact]
    public void Export_EmitsFluteDynamicsAsBreathController()
    {
        var project = SongProject.Create("Flute swell MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Texture);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 5), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.Texture, "Chorus flute", [note.Id], "flute");
        project.AddExpressionCurve(
            "Flute swell",
            ExpressionCurveKind.Dynamics,
            [new ExpressionCurvePoint(0, 88)],
            "flute");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xB6, 2, 88 }));
        Assert.Contains(parsed.Events, item => item.Bytes is [0x96, 72, 100]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xB6, 11, 88 }));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xB6, 101, 0 }));
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xE0);
    }

    [Fact]
    public void Export_EmitsSynthLeadDynamicsAsBrightness()
    {
        var project = SongProject.Create("Synth lead swell MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.HookReinforcement);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.HookReinforcement, "Chorus lead", [note.Id], "synth-lead");
        project.AddExpressionCurve(
            "Synth lead swell",
            ExpressionCurveKind.Dynamics,
            [new ExpressionCurvePoint(0, 88)],
            "synth-lead");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xBB, 74, 88 }));
        Assert.Contains(parsed.Events, item => item.Bytes is [0x9B, 60, 100]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xBB, 11, 88 }));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xBB, 101, 0 }));
        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xBB, 65, 0 }));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xBB, 65, 127 }));
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xE0);
    }

    [Fact]
    public void Export_EmitsSynthLeadPortamentoOffWithoutTurningItOn()
    {
        var project = SongProject.Create("Synth lead portamento MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.HookReinforcement);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.HookReinforcement, "Chorus lead", [note.Id], "synth-lead");
        project.AddExpressionCurve(
            "Synth lead swell",
            ExpressionCurveKind.Dynamics,
            [new ExpressionCurvePoint(0, 88)],
            "synth-lead");

        var parsed = Parse(MidiFileExporter.Export(project));
        var timed = parsed.Events
            .Where(item => item.Bytes[0] is 0xCB or 0xBB or 0x9B or 0x8B)
            .Select(item => (item.Tick, item.Bytes[0], item.Bytes.ElementAtOrDefault(1), item.Bytes.ElementAtOrDefault(2)))
            .ToArray();

        Assert.Equal(new[]
        {
            (0L, (byte)0xCB, (byte)81, (byte)0),
            (0L, (byte)0xBB, (byte)65, (byte)0),
            (0L, (byte)0xBB, (byte)74, (byte)88),
            (0L, (byte)0x9B, (byte)60, (byte)100),
            (480L, (byte)0x8B, (byte)60, (byte)0)
        }, timed);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xBB, 65, 127 }));
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xE0);
    }

    [Fact]
    public void Export_OmitsPortamentoForCello()
    {
        var project = SongProject.Create("Assigned cello MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Chorus foundation", [note.Id], "cello");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x91, 48, 100]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xB1, 65, 0 }));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xB1, 65, 127 }));
    }

    [Fact]
    public void Export_EmitsGuitarPitchBendRangeWithoutMovingTheWheel()
    {
        var project = SongProject.Create("Guitar bend MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Texture);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 3), 0, 480, 96);
        project.AddMusicalPart(section.Id, ArrangementRole.Texture, "Chorus guitar", [note.Id], "acoustic-guitar");

        var parsed = Parse(MidiFileExporter.Export(project));
        var timed = parsed.Events
            .Where(item => item.Bytes[0] is 0xC2 or 0xB2 or 0x92 or 0x82)
            .Select(item => (item.Tick, item.Bytes[0], item.Bytes.ElementAtOrDefault(1), item.Bytes.ElementAtOrDefault(2)))
            .ToArray();

        Assert.Equal(new[]
        {
            (0L, (byte)0xC2, (byte)25, (byte)0),
            (0L, (byte)0xB2, (byte)101, (byte)0),
            (0L, (byte)0xB2, (byte)100, (byte)0),
            (0L, (byte)0xB2, (byte)6, (byte)2),
            (0L, (byte)0x92, (byte)52, (byte)96),
            (480L, (byte)0x82, (byte)52, (byte)0)
        }, timed);
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xE0);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xB2, 65, 0 }));
    }

    [Fact]
    public void Export_OmitsPitchBendRangeForDrumKit()
    {
        var project = SongProject.Create("Assigned kit MIDI range");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        var kitNote = project.AddNoteEvent(DrumKitGeneralMidiMapper.AcousticBassDrumPitch, 0, 120, 102);
        project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Verse pulse", [kitNote.Id], "drum-kit");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Bytes is [0x99, 36, 102]);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(new byte[] { 0xB9, 101, 0 }));
        Assert.DoesNotContain(parsed.Events, item => (item.Bytes[0] & 0xF0) == 0xE0);
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
    public void Export_WritesNamedFormatOneTracksInCatalogOrder()
    {
        var project = SongProject.Create("Assigned kit MIDI");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        var kitNote = project.AddNoteEvent(DrumKitGeneralMidiMapper.AcousticBassDrumPitch, 0, 120, 102);
        var celloNote = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        var unassigned = project.AddNoteEvent(DrumKitGeneralMidiMapper.AcousticBassDrumPitch, 240, 120, 80);
        project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Verse pulse", [kitNote.Id], "drum-kit");
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Verse cello", [celloNote.Id], "cello");

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Equal(1, parsed.Format);
        Assert.Equal(4, parsed.TrackCount);
        Assert.Equal(
            [
                "Assigned kit MIDI",
                MidiFileExporter.UnassignedTrackName,
                "Cello",
                "Drum Kit"
            ],
            parsed.Tracks.Select(TrackName));
        Assert.DoesNotContain("Piano", parsed.Tracks.Select(TrackName));
        Assert.Contains(parsed.Tracks[2], item => item.Bytes.SequenceEqual(InstrumentName("Verse cello")));
        Assert.Contains(parsed.Tracks[3], item => item.Bytes.SequenceEqual(InstrumentName("Verse pulse")));
        Assert.DoesNotContain(parsed.Tracks[0], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x04);
        Assert.DoesNotContain(parsed.Tracks[1], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x04);
        Assert.Contains(parsed.Tracks[2], item => item.Bytes is [0x91, 48, 100]);
        Assert.Contains(parsed.Tracks[3], item => item.Bytes is [0x99, 36, 102]);
        Assert.Contains(parsed.Tracks[1], item => item.Bytes is [0x90, 36, 80]);
        Assert.DoesNotContain(parsed.Tracks[2], item => item.Bytes is [0x99, 36, 102]);
    }

    [Fact]
    public void Export_EmitsStoredMinorAndFlatKeySignatures()
    {
        var minor = SongProject.Create("A minor MIDI");
        minor.SetKey(new MusicalKey(NoteLetter.A, Accidental.Natural, ScaleMode.NaturalMinor));
        minor.AddNoteEvent(new RegisteredPitch(NoteLetter.A, Accidental.Natural, 3), 0, 480, 96);
        var minorParsed = Parse(MidiFileExporter.Export(minor));
        Assert.Contains(minorParsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x59, 0x02, 0x00, 0x01 }));

        var flat = SongProject.Create("F major MIDI");
        flat.SetKey(new MusicalKey(NoteLetter.F, Accidental.Natural, ScaleMode.Major));
        flat.AddNoteEvent(new RegisteredPitch(NoteLetter.F, Accidental.Natural, 4), 0, 480, 96);
        var flatParsed = Parse(MidiFileExporter.Export(flat));
        Assert.Contains(flatParsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x59, 0x02, 0xFF, 0x00 }));
    }

    [Fact]
    public void Export_OmitsKeySignatureOutsideTheCircleOfFifths()
    {
        var project = SongProject.Create("Exotic key MIDI");
        project.SetKey(new MusicalKey(NoteLetter.B, Accidental.Sharp, ScaleMode.Major));
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x59);
    }

    [Fact]
    public void Export_EmitsStoredArtistAsConductorCopyright()
    {
        var project = SongProject.Create("Artist copyright MIDI");
        project.SetArtist("Paper Satellites");
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(Copyright("Paper Satellites")));
        Assert.Contains(parsed.Tracks[0], item => item.Bytes.SequenceEqual(Copyright("Paper Satellites")));
        Assert.DoesNotContain(parsed.Tracks[1], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x02);
        Assert.Equal(1, parsed.Tracks[0].Count(item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x02));
    }

    [Fact]
    public void Export_OmitsEmptyArtistCopyright()
    {
        var project = SongProject.Create("Untitled artist MIDI");
        project.SetArtist("   ");
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x02);
    }

    [Fact]
    public void Export_EmitsStoredDescriptionAsConductorText()
    {
        var project = SongProject.Create("Description text MIDI");
        project.SetDescription("Orbit story sketch");
        project.SetGenre(SongGenre.Pop);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(Text("Orbit story sketch")));
        Assert.Contains(parsed.Tracks[0], item => item.Bytes.SequenceEqual(Text("Orbit story sketch")));
        Assert.DoesNotContain(parsed.Tracks[1], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x01);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(Text("Pop")));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(Text("Description text MIDI")));
        Assert.Equal(1, parsed.Tracks[0].Count(item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x01));
    }

    [Fact]
    public void Export_OmitsEmptyDescriptionText()
    {
        var project = SongProject.Create("Untitled description MIDI");
        project.SetDescription("   ");
        project.SetGenre(SongGenre.Folk);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x01);
    }

    [Fact]
    public void Export_EmitsStoredPartLabelsAsInstrumentNames()
    {
        var project = SongProject.Create("Part label MIDI");
        var section = project.AddSection(SectionKind.Chorus);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        project.SetSectionRole(section.Id, ArrangementRole.Pulse);
        var cello = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        var pulse = project.AddNoteEvent(new RegisteredPitch(NoteLetter.E, Accidental.Natural, 4), 0, 240, 90);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Chorus foundation", [cello.Id], "cello");
        project.AddMusicalPart(section.Id, ArrangementRole.Pulse, "Chorus pulse", [pulse.Id]);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Equal(
            [
                "Part label MIDI",
                MidiFileExporter.UnassignedTrackName,
                "Cello"
            ],
            parsed.Tracks.Select(TrackName));
        Assert.Contains(parsed.Tracks[1], item => item.Bytes.SequenceEqual(InstrumentName("Chorus pulse")));
        Assert.Contains(parsed.Tracks[2], item => item.Bytes.SequenceEqual(InstrumentName("Chorus foundation")));
        Assert.DoesNotContain(parsed.Tracks[0], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x04);
        Assert.Equal(1, parsed.Tracks[1].Count(item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x04));
        Assert.Equal(1, parsed.Tracks[2].Count(item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x04));
    }

    [Fact]
    public void Export_OmitsDuplicatePartLabelsOnTheSameTrack()
    {
        var project = SongProject.Create("Duplicate part label MIDI");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionRole(section.Id, ArrangementRole.Foundation);
        project.SetSectionRole(section.Id, ArrangementRole.Texture);
        var first = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 3), 0, 480, 100);
        var second = project.AddNoteEvent(new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3), 480, 480, 90);
        project.AddMusicalPart(section.Id, ArrangementRole.Foundation, "Verse cello", [first.Id], "cello");
        project.AddMusicalPart(section.Id, ArrangementRole.Texture, "Verse cello", [second.Id], "cello");

        var parsed = Parse(MidiFileExporter.Export(project));
        var cello = Assert.Single(parsed.Tracks, track => TrackName(track) == "Cello");

        Assert.Equal(1, cello.Count(item => item.Bytes.SequenceEqual(InstrumentName("Verse cello"))));
    }

    [Fact]
    public void Export_EmitsStoredSectionTitlesAsConductorMarkers()
    {
        var project = SongProject.Create("Section marker MIDI");
        var verse = project.AddSection(SectionKind.Verse);
        var chorus = project.AddSection(SectionKind.Chorus, "Lift");
        project.RenameSection(verse.Id, "Opening verse");
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var chorusTick = project.Timeline.ToAbsoluteTicks(project.Timeline.FindSection(chorus.Id).Start);

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(Marker("Opening verse")));
        Assert.Contains(parsed.Events, item => item.Tick == chorusTick && item.Bytes.SequenceEqual(Marker("Lift")));
        Assert.Contains(parsed.Tracks[0], item => item.Bytes.SequenceEqual(Marker("Opening verse")));
        Assert.Contains(parsed.Tracks[0], item => item.Bytes.SequenceEqual(Marker("Lift")));
        Assert.DoesNotContain(parsed.Tracks[1], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x06);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x05);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x07);
        Assert.Equal(2, parsed.TrackCount);
    }

    [Fact]
    public void Export_HoldsEveryTrackThroughTheCurrentStoredSongForm()
    {
        var project = SongProject.Create("Planned form MIDI");
        var verse = project.AddSection(SectionKind.Verse);
        var outro = project.AddSection(SectionKind.Outro);
        project.SetSectionDuration(verse.Id, 4);
        project.SetSectionDuration(outro.Id, 3);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var formEndBar = project.Timeline.SectionPlacements.Max(item => item.EndBarExclusive);
        var formEndTick = project.Timeline.ToAbsoluteTicks(new MusicalPosition(formEndBar, 1, 0));

        Assert.All(parsed.Tracks, track => Assert.Equal(formEndTick, EndOfTrack(track).Tick));
    }

    [Fact]
    public void Export_PreservesARealEventThatExtendsBeyondTheCurrentStoredSongForm()
    {
        var project = SongProject.Create("Later event MIDI");
        var section = project.AddSection(SectionKind.Verse);
        project.SetSectionDuration(section.Id, 1);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 2_400, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var formEndTick = project.Timeline.ToAbsoluteTicks(new MusicalPosition(2, 1, 0));

        Assert.Equal(formEndTick, EndOfTrack(parsed.Tracks[0]).Tick);
        Assert.Equal(note.EndTickExclusive, EndOfTrack(parsed.Tracks[1]).Tick);
    }

    [Fact]
    public void Export_EmitsPlacedSyllablesAsConductorLyrics()
    {
        var project = SongProject.Create("Lyric MIDI");
        var verse = project.AddSection(SectionKind.Verse);
        var line = verse.AddLyricLine("hold on");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var first = line.Words[0].Syllables[0].Id;
        var second = line.Words[1].Syllables[0].Id;
        project.SetSyllablePlacement(verse.Id, line.Id, first, new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(verse.Id, line.Id, second, new BeatPosition(2, 3, 0));
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var secondTick = project.Timeline.ToAbsoluteTicks(
            project.ResolveSyllablePosition(verse.Id, new BeatPosition(2, 3, 0)));

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(Lyric("hold")));
        Assert.Contains(parsed.Events, item => item.Tick == secondTick && item.Bytes.SequenceEqual(Lyric("on")));
        Assert.Contains(parsed.Tracks[0], item => item.Bytes.SequenceEqual(Lyric("hold")));
        Assert.DoesNotContain(parsed.Tracks[1], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x05);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x01);
    }

    [Fact]
    public void Export_OmitsUnplacedLyricsAndRhythmCandidateGhosts()
    {
        var project = SongProject.Create("Unplaced lyric MIDI");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var first = line.Words[0].Syllables[0].Id;
        var second = line.Words[1].Syllables[0].Id;
        project.SetSyllablePlacement(section.Id, line.Id, first, new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, second, new BeatPosition(1, 3, 0));
        project.CaptureRhythmCandidate(section.Id, line.Id, line.Phrases[0].Id, "Option A");
        project.SetSyllablePlacement(section.Id, line.Id, second, new BeatPosition(2, 1, 0));
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var activeSecond = project.Timeline.ToAbsoluteTicks(
            project.ResolveSyllablePosition(section.Id, new BeatPosition(2, 1, 0)));
        var ghostSecond = project.Timeline.ToAbsoluteTicks(
            project.ResolveSyllablePosition(section.Id, new BeatPosition(1, 3, 0)));

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(Lyric("one")));
        Assert.Contains(parsed.Events, item => item.Tick == activeSecond && item.Bytes.SequenceEqual(Lyric("two")));
        Assert.DoesNotContain(parsed.Events, item => item.Tick == ghostSecond && item.Bytes.SequenceEqual(Lyric("two")));
        Assert.Equal(2, parsed.Events.Count(item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x05));
    }

    [Fact]
    public void Export_EmitsStoredHarmonyAsConductorText()
    {
        var project = SongProject.Create("Harmony text MIDI");
        var verse = project.AddSection(SectionKind.Verse);
        project.AddHarmonyChord(verse.Id, new ChordSymbol(NoteLetter.C), new BeatPosition(1, 1, 0), 2);
        project.AddHarmonyChord(
            verse.Id,
            new ChordSymbol(NoteLetter.A, Accidental.Natural, ChordQuality.Minor),
            new BeatPosition(3, 1, 0),
            2);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var minorTick = project.Timeline.ToAbsoluteTicks(
            project.ResolveSyllablePosition(verse.Id, new BeatPosition(3, 1, 0)));

        Assert.Contains(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(Text("C")));
        Assert.Contains(parsed.Events, item => item.Tick == minorTick && item.Bytes.SequenceEqual(Text("Am")));
        Assert.Contains(parsed.Tracks[0], item => item.Bytes.SequenceEqual(Text("C")));
        Assert.DoesNotContain(parsed.Tracks[1], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x01);
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x07);
    }

    [Fact]
    public void Export_OmitsHarmonyCandidatesAndEmptyProgressions()
    {
        var project = SongProject.Create("Harmony option MIDI");
        var section = project.AddSection(SectionKind.Verse);
        var stored = project.AddHarmonyChord(section.Id, new ChordSymbol(NoteLetter.G, Accidental.Natural, ChordQuality.DominantSeventh), new BeatPosition(1, 1, 0), 2);
        project.CaptureHarmonyCandidate(section.Id, "Option A");
        project.RemoveHarmonyChord(section.Id, stored.Id);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));

        Assert.Empty(section.Harmony);
        Assert.Contains(section.HarmonyCandidates, item => item.Label == "Option A");
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.SequenceEqual(Text("G7")));
        Assert.DoesNotContain(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x01);
    }

    [Fact]
    public void Export_EmitsStoredBreathsAsConductorCuePoints()
    {
        var project = SongProject.Create("Breath cue MIDI");
        var verse = project.AddSection(SectionKind.Verse);
        var line = verse.AddLyricLine("hold on");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var first = line.Words[0].Syllables[0].Id;
        var second = line.Words[1].Syllables[0].Id;
        project.SetSyllablePlacement(verse.Id, line.Id, first, new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(verse.Id, line.Id, second, new BeatPosition(2, 3, 0));
        line.SetBreathPoint(first, true);
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var firstTick = project.Timeline.ToAbsoluteTicks(
            project.ResolveSyllablePosition(verse.Id, new BeatPosition(1, 1, 0)));
        var visualizationTick = firstTick + project.Timeline.TicksPerQuarterNote / 4;

        Assert.Contains(parsed.Events, item => item.Tick == firstTick && item.Bytes.SequenceEqual(Cue("Breath")));
        Assert.DoesNotContain(parsed.Events, item => item.Tick == visualizationTick && item.Bytes.SequenceEqual(Cue("Breath")));
        Assert.Contains(parsed.Tracks[0], item => item.Bytes.SequenceEqual(Cue("Breath")));
        Assert.DoesNotContain(parsed.Tracks[1], item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x07);
        Assert.Single(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x07);
    }

    [Fact]
    public void Export_OmitsUnplacedBreathsAndRhythmCandidateGhosts()
    {
        var project = SongProject.Create("Unplaced breath MIDI");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var first = line.Words[0].Syllables[0].Id;
        var second = line.Words[1].Syllables[0].Id;
        line.SetBreathPoint(first, true);
        line.SetBreathPoint(second, true);
        project.SetSyllablePlacement(section.Id, line.Id, second, new BeatPosition(1, 3, 0));
        project.CaptureRhythmCandidate(section.Id, line.Id, line.Phrases[0].Id, "Option A");
        project.SetSyllablePlacement(section.Id, line.Id, second, new BeatPosition(2, 1, 0));
        project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 480, 100);

        var parsed = Parse(MidiFileExporter.Export(project));
        var activeSecond = project.Timeline.ToAbsoluteTicks(
            project.ResolveSyllablePosition(section.Id, new BeatPosition(2, 1, 0)));
        var ghostSecond = project.Timeline.ToAbsoluteTicks(
            project.ResolveSyllablePosition(section.Id, new BeatPosition(1, 3, 0)));

        Assert.Contains(parsed.Events, item => item.Tick == activeSecond && item.Bytes.SequenceEqual(Cue("Breath")));
        Assert.DoesNotContain(parsed.Events, item => item.Tick == ghostSecond && item.Bytes.SequenceEqual(Cue("Breath")));
        Assert.DoesNotContain(parsed.Events, item => item.Tick == 0 && item.Bytes.SequenceEqual(Cue("Breath")));
        Assert.Single(parsed.Events, item => item.Bytes.Length >= 2 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x07);
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
        var trackCount = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(10, 2));
        var division = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(12, 2));

        var tracks = new List<IReadOnlyList<ParsedEvent>>();
        var events = new List<ParsedEvent>();
        var offset = 14;
        while (offset < bytes.Length)
        {
            Assert.Equal("MTrk"u8.ToArray(), bytes.AsSpan(offset, 4).ToArray());
            offset += 4;
            var trackLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            offset += 4;
            var end = offset + trackLength;
            var trackEvents = new List<ParsedEvent>();
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
                    trackEvents.Add(new ParsedEvent(tick, new[] { status, type, (byte)length }.Concat(data).ToArray()));
                    continue;
                }
                var high = status & 0xF0;
                var dataCount = high is 0xC0 or 0xD0 ? 1 : 2;
                var message = new byte[1 + dataCount];
                message[0] = status;
                for (var index = 0; index < dataCount; index++) message[1 + index] = bytes[offset++];
                trackEvents.Add(new ParsedEvent(tick, message));
            }
            Assert.Equal(end, offset);
            tracks.Add(trackEvents);
            events.AddRange(trackEvents);
        }

        Assert.Equal(trackCount, tracks.Count);
        return new ParsedMidi(format, trackCount, division, events, tracks);
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

    private static byte[] InstrumentName(string text)
    {
        var payload = new UTF8Encoding(false, true).GetBytes(text);
        return new byte[] { 0xFF, 0x04, (byte)payload.Length }.Concat(payload).ToArray();
    }

    private static byte[] Copyright(string text)
    {
        var payload = Encoding.ASCII.GetBytes(text);
        return new byte[] { 0xFF, 0x02, (byte)payload.Length }.Concat(payload).ToArray();
    }

    private static byte[] Lyric(string text)
    {
        var payload = Encoding.ASCII.GetBytes(text);
        return new byte[] { 0xFF, 0x05, (byte)payload.Length }.Concat(payload).ToArray();
    }

    private static byte[] Marker(string name)
    {
        var payload = Encoding.ASCII.GetBytes(name);
        return new byte[] { 0xFF, 0x06, (byte)payload.Length }.Concat(payload).ToArray();
    }

    private static byte[] Text(string text)
    {
        var payload = Encoding.ASCII.GetBytes(text);
        return new byte[] { 0xFF, 0x01, (byte)payload.Length }.Concat(payload).ToArray();
    }

    private static byte[] Cue(string text)
    {
        var payload = Encoding.ASCII.GetBytes(text);
        return new byte[] { 0xFF, 0x07, (byte)payload.Length }.Concat(payload).ToArray();
    }

    private static string TrackName(IReadOnlyList<ParsedEvent> events)
    {
        var named = events.First(item => item.Bytes.Length >= 3 && item.Bytes[0] == 0xFF && item.Bytes[1] == 0x03);
        return Encoding.ASCII.GetString(named.Bytes, 3, named.Bytes.Length - 3);
    }

    private static ParsedEvent EndOfTrack(IReadOnlyList<ParsedEvent> events) =>
        Assert.Single(events, item => item.Bytes.SequenceEqual(new byte[] { 0xFF, 0x2F, 0x00 }));

    private sealed record ParsedMidi(
        short Format,
        short TrackCount,
        short Division,
        IReadOnlyList<ParsedEvent> Events,
        IReadOnlyList<IReadOnlyList<ParsedEvent>> Tracks);
    private sealed record ParsedEvent(long Tick, byte[] Bytes);
}
