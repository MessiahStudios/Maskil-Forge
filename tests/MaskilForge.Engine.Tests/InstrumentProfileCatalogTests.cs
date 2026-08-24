using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentProfileCatalogTests
{
    [Fact]
    public void CurrentCatalog_NamesTheWaveOneProofSet()
    {
        var catalog = InstrumentProfileCatalogLoader.Current;

        Assert.Equal(2, catalog.Version);
        Assert.Equal(
            ["cello", "acoustic-guitar", "piano", "electric-bass", "drum-kit"],
            catalog.Instruments.Select(item => item.Id));

        var cello = catalog.Find("cello");
        Assert.True(cello.Pitched);
        Assert.Equal(36, cello.MinimumPitch!.MidiNumber);
        Assert.Equal(81, cello.MaximumPitch!.MidiNumber);
        Assert.Equal(
            [ArrangementRole.Foundation, ArrangementRole.Harmony, ArrangementRole.LowEndSupport, ArrangementRole.Texture, ArrangementRole.Countermelody],
            cello.Roles);
        Assert.Equal(
            [InstrumentArticulation.Legato, InstrumentArticulation.BowExpression, InstrumentArticulation.Slide],
            cello.Articulations);

        var guitar = catalog.Find("acoustic-guitar");
        Assert.Equal("Acoustic Guitar", guitar.Name);
        Assert.True(guitar.Pitched);
        Assert.Equal(40, guitar.MinimumPitch!.MidiNumber);
        Assert.Equal(83, guitar.MaximumPitch!.MidiNumber);
        Assert.Equal(
            [InstrumentExpressiveQuality.Intimate, InstrumentExpressiveQuality.Percussive, InstrumentExpressiveQuality.Agile],
            guitar.ExpressiveQualities);
        Assert.Empty(cello.Articulations.Intersect(guitar.Articulations));

        var piano = catalog.Find("piano");
        Assert.Equal(21, piano.MinimumPitch!.MidiNumber);
        Assert.Equal(108, piano.MaximumPitch!.MidiNumber);
        Assert.Contains(ArrangementRole.HookReinforcement, piano.Roles);
        Assert.Equal([InstrumentArticulation.Strike, InstrumentArticulation.Pedal], piano.Articulations);

        var bass = catalog.Find("electric-bass");
        Assert.Equal(28, bass.MinimumPitch!.MidiNumber);
        Assert.Equal(67, bass.MaximumPitch!.MidiNumber);
        Assert.Contains(ArrangementRole.LowEndSupport, bass.Roles);
        Assert.Equal(
            [InstrumentArticulation.Finger, InstrumentArticulation.Picking, InstrumentArticulation.Slap],
            bass.Articulations);

        var drums = catalog.Find("drum-kit");
        Assert.False(drums.Pitched);
        Assert.Null(drums.MinimumPitch);
        Assert.Null(drums.MaximumPitch);
        Assert.Equal([ArrangementRole.Pulse, ArrangementRole.Accent], drums.Roles);
        Assert.Equal([InstrumentArticulation.Hit, InstrumentArticulation.Choke], drums.Articulations);
        Assert.Equal([InstrumentExpressiveQuality.Percussive], drums.ExpressiveQualities);
    }

    [Fact]
    public void Load_RejectsAnInvertedRange()
    {
        var json = """
            {
              "version": 2,
              "instruments": [
                {
                  "id": "cello",
                  "name": "Cello",
                  "pitched": true,
                  "minimumPitch": { "letter": "A", "accidental": "Natural", "octave": 5 },
                  "maximumPitch": { "letter": "C", "accidental": "Natural", "octave": 2 },
                  "roles": ["Harmony"],
                  "articulations": ["Legato"],
                  "expressiveQualities": ["Warm"]
                }
              ]
            }
            """;

        var error = Assert.Throws<ArgumentException>(() => InstrumentProfileCatalogLoader.Load(json));
        Assert.Contains("maximum pitch cannot sit below its minimum pitch", error.Message);
    }

    [Fact]
    public void Load_RejectsAMelodicRangeOnAnUnpitchedInstrument()
    {
        var json = """
            {
              "version": 2,
              "instruments": [
                {
                  "id": "drum-kit",
                  "name": "Drum Kit",
                  "pitched": false,
                  "minimumPitch": { "letter": "C", "accidental": "Natural", "octave": 2 },
                  "maximumPitch": { "letter": "B", "accidental": "Natural", "octave": 3 },
                  "roles": ["Pulse"],
                  "articulations": ["Hit"],
                  "expressiveQualities": ["Percussive"]
                }
              ]
            }
            """;

        var error = Assert.Throws<ArgumentException>(() => InstrumentProfileCatalogLoader.Load(json));
        Assert.Contains("unpitched instrument cannot name a melodic range", error.Message);
    }

    [Fact]
    public void Load_RejectsDuplicateIds()
    {
        var json = """
            {
              "version": 2,
              "instruments": [
                {
                  "id": "cello",
                  "name": "Cello",
                  "pitched": true,
                  "minimumPitch": { "letter": "C", "accidental": "Natural", "octave": 2 },
                  "maximumPitch": { "letter": "A", "accidental": "Natural", "octave": 5 },
                  "roles": ["Harmony"],
                  "articulations": ["Legato"],
                  "expressiveQualities": ["Warm"]
                },
                {
                  "id": "cello",
                  "name": "Other cello",
                  "pitched": true,
                  "minimumPitch": { "letter": "C", "accidental": "Natural", "octave": 2 },
                  "maximumPitch": { "letter": "A", "accidental": "Natural", "octave": 5 },
                  "roles": ["Texture"],
                  "articulations": ["Slide"],
                  "expressiveQualities": ["Intimate"]
                }
              ]
            }
            """;

        var error = Assert.Throws<ArgumentException>(() => InstrumentProfileCatalogLoader.Load(json));
        Assert.Contains("Instrument-profile IDs must be unique", error.Message);
    }

    [Fact]
    public void Load_RejectsAnUnsupportedCatalogVersion()
    {
        var json = """
            {
              "version": 1,
              "instruments": [
                {
                  "id": "cello",
                  "name": "Cello",
                  "pitched": true,
                  "minimumPitch": { "letter": "C", "accidental": "Natural", "octave": 2 },
                  "maximumPitch": { "letter": "A", "accidental": "Natural", "octave": 5 },
                  "roles": ["Harmony"],
                  "articulations": ["Legato"],
                  "expressiveQualities": ["Warm"]
                }
              ]
            }
            """;

        var error = Assert.Throws<InvalidOperationException>(() => InstrumentProfileCatalogLoader.Load(json));
        Assert.Contains("version 1 is not supported", error.Message);
    }
}
