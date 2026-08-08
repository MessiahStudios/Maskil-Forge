using MaskilForge.Domain;

namespace MaskilForge.Engine.Tests;

public sealed class LyricDocumentTests
{
    [Fact]
    public void Tokenize_RecognizesWordsContractionsHyphensAndUnicode()
    {
        const string text = "Grace, don't leave the singer-songwriter—amazed.";

        var tokens = LyricLine.Tokenize(text);

        Assert.Equal(["Grace", "don't", "leave", "the", "singer-songwriter", "amazed"], tokens.Select(item => item.Text));
        Assert.All(tokens, token => Assert.Equal(token.Text, text.Substring(token.Start, token.Length)));
    }

    [Fact]
    public void EditLine_PreservesUnchangedWordIdentifiersAcrossInsertions()
    {
        var line = LyricLine.Create("grace how sweet");
        var originalIds = line.Words.ToDictionary(word => word.Text, word => word.Id);

        line.SetText("Amazing grace how very sweet");

        Assert.Equal(originalIds["grace"], line.Words.Single(word => word.Text == "grace").Id);
        Assert.Equal(originalIds["how"], line.Words.Single(word => word.Text == "how").Id);
        Assert.Equal(originalIds["sweet"], line.Words.Single(word => word.Text == "sweet").Id);
        Assert.DoesNotContain(line.Words.Single(word => word.Text == "Amazing").Id, originalIds.Values);
        Assert.DoesNotContain(line.Words.Single(word => word.Text == "very").Id, originalIds.Values);
    }

    [Fact]
    public void Syllables_AreExplicitAndRetainIdentifiersWhenTheWordSurvivesAnEdit()
    {
        var line = LyricLine.Create("Amazing grace");
        var amazing = line.Words[0];
        line.SetSyllables(amazing.Id, ["A", "maz", "ing"]);
        var syllableIds = amazing.Syllables.Select(item => item.Id).ToList();

        line.SetText("Oh Amazing grace");

        var retained = line.Words.Single(word => word.Text == "Amazing");
        Assert.Equal(["A", "maz", "ing"], retained.Syllables.Select(item => item.Text));
        Assert.Equal(syllableIds, retained.Syllables.Select(item => item.Id));
    }

    [Fact]
    public void LyricDocument_ProjectsRawDraftAndOrderedStructuredLines()
    {
        var project = SongProject.Create("Words");
        project.SetRawLyricDraft("Unstructured source idea");
        var verse = project.AddSection(SectionKind.Verse);
        var verseLine = verse.AddLyricLine("First line");
        var chorus = project.AddSection(SectionKind.Chorus);
        var chorusLine = chorus.AddLyricLine("Hook line");

        var document = project.Lyrics;

        Assert.Equal("Unstructured source idea", document.RawDraft);
        Assert.Equal([verseLine.Id, chorusLine.Id], document.Lines.Select(item => item.Line.Id));
        Assert.Equal([verse.Id, chorus.Id], document.Lines.Select(item => item.SectionId));
    }

    [Fact]
    public void Syllables_RejectInvalidManualData()
    {
        var line = LyricLine.Create("word");

        Assert.Throws<ArgumentException>(() => line.SetSyllables(line.Words[0].Id, [""]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            line.SetSyllables(line.Words[0].Id, Enumerable.Repeat("a", 33)));
    }

    [Fact]
    public void LyricLine_RejectsDuplicateWordIdentifiers()
    {
        var wordId = LyricWordId.New();
        var words = new[]
        {
            new LyricWord(wordId, "one", 0, 3),
            new LyricWord(wordId, "two", 4, 3)
        };

        Assert.Throws<ArgumentException>(() => new LyricLine(LyricLineId.New(), "one two", words));
    }
}
