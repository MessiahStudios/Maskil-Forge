using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentRoleRecommenderTests
{
    [Fact]
    public void Recommend_MatchesCatalogRolesInCatalogOrder()
    {
        var set = InstrumentRoleRecommender.Recommend([ArrangementRole.Countermelody, ArrangementRole.Pulse]);

        Assert.Null(set.Quality);
        Assert.Equal([ArrangementRole.Countermelody, ArrangementRole.Pulse], set.Recommendations.Select(item => item.Role));
        Assert.Equal(
            ["cello", "violin", "flute", "clarinet", "synth-lead"],
            Assert.Single(set.Recommendations, item => item.Role == ArrangementRole.Countermelody).Instruments.Select(item => item.Id));
        Assert.Equal(
            ["acoustic-guitar", "piano", "electric-bass", "drum-kit"],
            Assert.Single(set.Recommendations, item => item.Role == ArrangementRole.Pulse).Instruments.Select(item => item.Id));
    }

    [Fact]
    public void Recommend_FiltersByExpressiveQualityWithoutRanking()
    {
        var set = InstrumentRoleRecommender.Recommend(
            [ArrangementRole.Countermelody, ArrangementRole.Pulse],
            InstrumentExpressiveQuality.Warm);

        Assert.Equal(InstrumentExpressiveQuality.Warm, set.Quality);
        Assert.Equal(
            ["cello", "clarinet"],
            Assert.Single(set.Recommendations, item => item.Role == ArrangementRole.Countermelody).Instruments.Select(item => item.Id));
        Assert.Equal(["electric-bass"], Assert.Single(set.Recommendations, item => item.Role == ArrangementRole.Pulse).Instruments.Select(item => item.Id));
    }

    [Fact]
    public void Recommend_DeduplicatesRolesAndLeavesUnmatchedJobsEmpty()
    {
        var set = InstrumentRoleRecommender.Recommend(
            [ArrangementRole.Foundation, ArrangementRole.Foundation, ArrangementRole.LowEndSupport],
            InstrumentExpressiveQuality.Bright);

        Assert.Equal([ArrangementRole.Foundation, ArrangementRole.LowEndSupport], set.Recommendations.Select(item => item.Role));
        Assert.Equal(["piano"], Assert.Single(set.Recommendations, item => item.Role == ArrangementRole.Foundation).Instruments.Select(item => item.Id));
        Assert.Empty(Assert.Single(set.Recommendations, item => item.Role == ArrangementRole.LowEndSupport).Instruments);
    }

    [Fact]
    public void Recommend_RequiresAtLeastOneRole()
    {
        var error = Assert.Throws<ArgumentException>(() => InstrumentRoleRecommender.Recommend([]));
        Assert.Contains("Choose at least one arrangement role", error.Message);
    }

    [Fact]
    public void Recommend_RejectsInvalidRoleOrQuality()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstrumentRoleRecommender.Recommend([(ArrangementRole)999]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InstrumentRoleRecommender.Recommend([ArrangementRole.Pulse], (InstrumentExpressiveQuality)999));
    }
}
