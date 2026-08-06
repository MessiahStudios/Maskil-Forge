using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class SongProjectTests
{
    [Fact]
    public void CreateProject_AssignsDefaultsAndStableId()
    {
        var project = SongProject.Create("First Song");

        Assert.NotEqual(Guid.Empty, project.Id.Value);
        Assert.Equal("First Song", project.Title);
        Assert.Equal(120, project.Tempo.BeatsPerMinute);
        Assert.Equal((4, 4), (project.TimeSignature.Numerator, project.TimeSignature.Denominator));
        Assert.Equal(SchemaVersion.Current, project.SchemaVersion);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(120)]
    [InlineData(300)]
    public void SetTempo_AcceptsValidValues(int tempo)
    {
        var project = SongProject.Create("Tempo Test");
        project.SetTempo(tempo);
        Assert.Equal(tempo, project.Tempo.BeatsPerMinute);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(301)]
    public void SetTempo_RejectsInvalidValues(int tempo)
    {
        var project = SongProject.Create("Tempo Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetTempo(tempo));
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(6, 8)]
    [InlineData(7, 16)]
    public void SetTimeSignature_AcceptsValidValues(int numerator, int denominator)
    {
        var project = SongProject.Create("Meter Test");
        project.SetTimeSignature(numerator, denominator);
        Assert.Equal((numerator, denominator), (project.TimeSignature.Numerator, project.TimeSignature.Denominator));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(4, 3)]
    [InlineData(33, 4)]
    public void SetTimeSignature_RejectsInvalidValues(int numerator, int denominator)
    {
        var project = SongProject.Create("Meter Test");
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetTimeSignature(numerator, denominator));
    }

    [Fact]
    public void Sections_PreserveInsertionOrderAndIdentifiers()
    {
        var project = SongProject.Create("Order Test");
        var verse = project.AddSection(SectionKind.Verse);
        var chorus = project.AddSection(SectionKind.Chorus);

        Assert.Equal([verse.Id, chorus.Id], project.Sections.Select(section => section.Id));
        Assert.NotEqual(verse.Id, chorus.Id);
    }

    [Fact]
    public void Metadata_ValidatesAndStoresCreativeContext()
    {
        var project = SongProject.Create("Metadata Test");

        project.SetArtist("Independent Artist");
        project.SetGenre(SongGenre.Folk);
        project.SetDescription("An intimate acoustic song.");

        Assert.Equal("Independent Artist", project.Artist);
        Assert.Equal(SongGenre.Folk, project.Genre);
        Assert.Equal("An intimate acoustic song.", project.Description);
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetArtist(new string('a', 201)));
        Assert.Throws<ArgumentOutOfRangeException>(() => project.SetDescription(new string('d', 2_001)));
    }
}
