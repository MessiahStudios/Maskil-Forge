using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentProfileCatalogTests
{
    [Fact]
    public void CurrentCatalog_NamesCelloAndGuitarWithDistinctArticulations()
    {
        var catalog = InstrumentProfileCatalogLoader.Current;

        Assert.Equal(1, catalog.Version);
        Assert.Equal(["cello", "guitar"], catalog.Instruments.Select(item => item.Id));

        var cello = catalog.Find("cello");
        Assert.Equal("Cello", cello.Name);
        Assert.Equal(36, cello.MinimumPitch.MidiNumber);
        Assert.Equal(81, cello.MaximumPitch.MidiNumber);
        Assert.Contains(ArrangementRole.Countermelody, cello.Roles);
        Assert.Equal(
            [InstrumentArticulation.Legato, InstrumentArticulation.BowExpression, InstrumentArticulation.Slide],
            cello.Articulations);
        Assert.Equal(
            [InstrumentExpressiveQuality.Warm, InstrumentExpressiveQuality.Intimate, InstrumentExpressiveQuality.Sustained],
            cello.ExpressiveQualities);

        var guitar = catalog.Find("guitar");
        Assert.Equal("Guitar", guitar.Name);
        Assert.Equal(40, guitar.MinimumPitch.MidiNumber);
        Assert.Equal(83, guitar.MaximumPitch.MidiNumber);
        Assert.Contains(ArrangementRole.Pulse, guitar.Roles);
        Assert.Equal(
            [InstrumentArticulation.Picking, InstrumentArticulation.Bend, InstrumentArticulation.HammerOn],
            guitar.Articulations);
        Assert.Equal(
            [InstrumentExpressiveQuality.Bright, InstrumentExpressiveQuality.Percussive, InstrumentExpressiveQuality.Agile],
            guitar.ExpressiveQualities);
        Assert.Empty(cello.Articulations.Intersect(guitar.Articulations));
    }

    [Fact]
    public void Load_RejectsAnInvertedRange()
    {
        var json = """
            {
              "version": 1,
              "instruments": [
                {
                  "id": "cello",
                  "name": "Cello",
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
    public void Load_RejectsDuplicateIds()
    {
        var json = """
            {
              "version": 1,
              "instruments": [
                {
                  "id": "cello",
                  "name": "Cello",
                  "minimumPitch": { "letter": "C", "accidental": "Natural", "octave": 2 },
                  "maximumPitch": { "letter": "A", "accidental": "Natural", "octave": 5 },
                  "roles": ["Harmony"],
                  "articulations": ["Legato"],
                  "expressiveQualities": ["Warm"]
                },
                {
                  "id": "cello",
                  "name": "Other cello",
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
              "version": 2,
              "instruments": [
                {
                  "id": "cello",
                  "name": "Cello",
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
        Assert.Contains("version 2 is not supported", error.Message);
    }
}
