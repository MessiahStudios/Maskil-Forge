using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentArticulationMapperTests
{
    [Fact]
    public void Map_TranslatesSwellAndSlideWithoutAssumingCelloBehavior()
    {
        var set = InstrumentArticulationMapper.Map();
        var cello = Assert.Single(set.Maps, item => item.InstrumentId == "cello");
        var guitar = Assert.Single(set.Maps, item => item.InstrumentId == "acoustic-guitar");
        var piano = Assert.Single(set.Maps, item => item.InstrumentId == "piano");

        Assert.Equal("Cello", cello.InstrumentName);
        Assert.Equal(InstrumentArticulation.BowExpression, Mapped(cello, NeutralPerformanceGesture.Swell));
        Assert.Equal(InstrumentArticulation.Slide, Mapped(cello, NeutralPerformanceGesture.Slide));

        Assert.Equal(InstrumentArticulation.Picking, Mapped(guitar, NeutralPerformanceGesture.Swell));
        Assert.Equal(InstrumentArticulation.Bend, Mapped(guitar, NeutralPerformanceGesture.Slide));

        Assert.Equal(InstrumentArticulation.Strike, Mapped(piano, NeutralPerformanceGesture.Swell));
        Assert.False(Lookup(piano, NeutralPerformanceGesture.Slide).Applicable);
        Assert.Null(Lookup(piano, NeutralPerformanceGesture.Slide).Articulation);
    }

    [Fact]
    public void Map_LeavesDrumKitAndBassSlideNotApplicable()
    {
        var set = InstrumentArticulationMapper.Map();
        var bass = Assert.Single(set.Maps, item => item.InstrumentId == "electric-bass");
        var drums = Assert.Single(set.Maps, item => item.InstrumentId == "drum-kit");

        Assert.Equal(InstrumentArticulation.Finger, Mapped(bass, NeutralPerformanceGesture.Swell));
        Assert.False(Lookup(bass, NeutralPerformanceGesture.Slide).Applicable);

        Assert.All(drums.Mappings, mapping =>
        {
            Assert.False(mapping.Applicable);
            Assert.Null(mapping.Articulation);
        });
    }

    [Fact]
    public void Map_KeepsCatalogOrderAndUsesOnlyNamedArticulations()
    {
        var catalog = InstrumentProfileCatalogLoader.Current;
        var set = InstrumentArticulationMapper.Map();

        Assert.Equal(
            ["cello", "acoustic-guitar", "piano", "electric-bass", "drum-kit"],
            set.Maps.Select(item => item.InstrumentId));
        Assert.All(set.Maps, map =>
        {
            Assert.Equal(
                [NeutralPerformanceGesture.Swell, NeutralPerformanceGesture.Slide],
                map.Mappings.Select(item => item.Gesture));
            var profile = catalog.Find(map.InstrumentId);
            Assert.All(map.Mappings.Where(item => item.Applicable), mapping =>
                Assert.Contains(mapping.Articulation!.Value, profile.Articulations));
        });
    }

    [Fact]
    public void Map_RejectsAMappedArticulationMissingFromTheProfile()
    {
        var catalog = new InstrumentProfileCatalog(2, [
            new InstrumentProfile(
                "cello",
                "Cello",
                true,
                new RegisteredPitch(NoteLetter.C, Accidental.Natural, 2),
                new RegisteredPitch(NoteLetter.A, Accidental.Natural, 5),
                [ArrangementRole.Countermelody],
                [InstrumentArticulation.Legato],
                [InstrumentExpressiveQuality.Warm]),
        ]);

        var error = Assert.Throws<InvalidOperationException>(() => InstrumentArticulationMapper.Map(catalog));
        Assert.Contains("cannot map", error.Message);
    }

    [Fact]
    public void Map_TreatsUnknownCatalogInstrumentsAsNotApplicable()
    {
        var catalog = new InstrumentProfileCatalog(2, [
            new InstrumentProfile(
                "violin",
                "Violin",
                true,
                new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3),
                new RegisteredPitch(NoteLetter.A, Accidental.Natural, 7),
                [ArrangementRole.Countermelody],
                [InstrumentArticulation.Legato, InstrumentArticulation.Slide],
                [InstrumentExpressiveQuality.Intimate]),
        ]);

        var set = InstrumentArticulationMapper.Map(catalog);
        var violin = Assert.Single(set.Maps);

        Assert.Equal("violin", violin.InstrumentId);
        Assert.All(violin.Mappings, mapping =>
        {
            Assert.False(mapping.Applicable);
            Assert.Null(mapping.Articulation);
        });
    }

    private static InstrumentArticulation Mapped(InstrumentArticulationMap map, NeutralPerformanceGesture gesture)
    {
        var mapping = Lookup(map, gesture);
        Assert.True(mapping.Applicable);
        return mapping.Articulation!.Value;
    }

    private static InstrumentArticulationMapping Lookup(InstrumentArticulationMap map, NeutralPerformanceGesture gesture) =>
        Assert.Single(map.Mappings, item => item.Gesture == gesture);
}
