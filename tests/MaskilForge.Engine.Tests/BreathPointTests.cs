using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class BreathPointTests
{
    [Fact]
    public void SetBreathPoint_CreatesStableIdentityWithManualProvenance()
    {
        var (line, syllables) = CreateSyllabledLine("one two");

        line.SetBreathPoint(syllables[0], true);

        var breath = Assert.Single(line.BreathPoints);
        Assert.Equal(syllables[0], breath.AfterSyllableId);
        Assert.Equal(BreathProvenance.Manual, breath.Provenance);
        Assert.NotEqual(Guid.Empty, breath.Id.Value);
    }

    [Fact]
    public void SetBreathPoint_RejectsUnknownSyllablesAndDuplicateAnchorsStaySingular()
    {
        var (line, syllables) = CreateSyllabledLine("one two");
        line.SetBreathPoint(syllables[0], true);
        var originalId = line.BreathPoints[0].Id;

        line.SetBreathPoint(syllables[0], true, BreathProvenance.Imported);

        Assert.Throws<KeyNotFoundException>(() => line.SetBreathPoint(SyllableId.New(), true));
        var breath = Assert.Single(line.BreathPoints);
        Assert.Equal(originalId, breath.Id);
        Assert.Equal(BreathProvenance.Imported, breath.Provenance);
    }

    [Fact]
    public void CompatibleLyricAndSyllableEdits_PreserveBreathIdentities()
    {
        var (line, syllables) = CreateSyllabledLine("one two");
        line.SetBreathPoint(syllables[0], true);
        line.SetBreathPoint(syllables[1], true);
        var firstId = line.BreathPoints[0].Id;
        var secondId = line.BreathPoints[1].Id;

        line.SetText("Oh one two");
        Assert.Equal([firstId, secondId], line.BreathPoints.Select(item => item.Id));
        Assert.Equal(syllables, line.BreathPoints.Select(item => item.AfterSyllableId));

        line.SetSyllables(line.Words[1].Id, ["changed"]);
        var surviving = Assert.Single(line.BreathPoints);
        Assert.Equal(secondId, surviving.Id);
        Assert.Equal(syllables[1], surviving.AfterSyllableId);
    }

    [Fact]
    public void ClearingABreathPoint_RemovesOnlyThatDecision()
    {
        var (line, syllables) = CreateSyllabledLine("one two");
        line.SetBreathPoint(syllables[0], true);
        line.SetBreathPoint(syllables[1], true);
        var keepId = line.BreathPoints[1].Id;

        line.SetBreathPoint(syllables[0], false);

        var surviving = Assert.Single(line.BreathPoints);
        Assert.Equal(keepId, surviving.Id);
        Assert.Equal(syllables[1], surviving.AfterSyllableId);
    }

    [Fact]
    public void PunctuationDoesNotInventBreathPoints()
    {
        var project = SongProject.Create("Breath");
        var line = project.AddSection(SectionKind.Verse).AddLyricLine("Wait, breathe.");
        line.SetSyllables(line.Words[0].Id, ["Wait"]);
        line.SetSyllables(line.Words[1].Id, ["breathe"]);

        Assert.NotEmpty(line.Punctuation);
        Assert.Empty(line.BreathPoints);
    }

    private static (LyricLine Line, IReadOnlyList<SyllableId> Syllables) CreateSyllabledLine(string text)
    {
        var project = SongProject.Create("Breath");
        var line = project.AddSection(SectionKind.Verse).AddLyricLine(text);
        foreach (var word in line.Words)
            line.SetSyllables(word.Id, [word.Text]);
        return (line, line.Words.SelectMany(word => word.Syllables).Select(item => item.Id).ToList());
    }
}
