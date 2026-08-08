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
    public void PunctuationTokens_PreserveMarksWithoutSplittingInternalWordPunctuation()
    {
        const string text = "Don't stop, mother-in-law—listen!";

        var tokens = LyricLine.TokenizePunctuation(text);

        Assert.Equal([",", "—", "!"], tokens.Select(item => item.Text));
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
        Assert.Equal([0, 1, 2], retained.Syllables.Select(item => item.Position));
        Assert.All(retained.Syllables, item => Assert.Equal(SyllableSource.Manual, item.Source));
    }

    [Fact]
    public void ManualSyllableCorrection_PreservesMatchingIdentifiersWhenPositionsShift()
    {
        var line = LyricLine.Create("Amazing");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["A", "maz", "ing"], SyllableSource.Analyzer);
        var originalIds = word.Syllables.ToDictionary(item => item.Text, item => item.Id);

        line.SetSyllables(word.Id, ["uh", "A", "maz", "ing"]);

        Assert.Equal(["uh", "A", "maz", "ing"], word.Syllables.Select(item => item.Text));
        Assert.Equal([0, 1, 2, 3], word.Syllables.Select(item => item.Position));
        Assert.NotEqual(originalIds["A"], word.Syllables[0].Id);
        Assert.Equal(originalIds["A"], word.Syllables[1].Id);
        Assert.Equal(originalIds["maz"], word.Syllables[2].Id);
        Assert.Equal(originalIds["ing"], word.Syllables[3].Id);
        Assert.All(word.Syllables, item => Assert.Equal(SyllableSource.Manual, item.Source));
    }

    [Fact]
    public void SyllableSource_RecordsAnalyzerImportedAndManualOrigins()
    {
        var line = LyricLine.Create("fire");
        var word = line.Words[0];

        line.SetSyllables(word.Id, ["fi", "re"], SyllableSource.Analyzer);
        Assert.All(word.Syllables, item => Assert.Equal(SyllableSource.Analyzer, item.Source));

        line.SetSyllables(word.Id, ["fire"], SyllableSource.Imported);
        Assert.Single(word.Syllables);
        Assert.Equal(SyllableSource.Imported, word.Syllables[0].Source);

        line.SetSyllables(word.Id, ["fire"]);
        Assert.Equal(SyllableSource.Manual, word.Syllables[0].Source);
    }

    [Fact]
    public void ArtistStress_RecordsLevelAndManualProvenanceOnTheAddressedSyllable()
    {
        var line = LyricLine.Create("through pain");
        var pain = line.Words[1];
        line.SetSyllables(pain.Id, ["pain"]);
        var syllable = Assert.Single(pain.Syllables);

        line.SetStress(pain.Id, syllable.Id, StressLevel.Emphasized);

        Assert.NotNull(pain.Syllables[0].Stress);
        Assert.Equal(StressLevel.Emphasized, pain.Syllables[0].Stress!.Level);
        Assert.Equal(StressProvenance.Manual, pain.Syllables[0].Stress!.Provenance);
    }

    [Fact]
    public void StressMark_SurvivesMatchingSyllableBoundaryAndLineEdits()
    {
        var line = LyricLine.Create("Amazing grace");
        var amazing = line.Words[0];
        line.SetSyllables(amazing.Id, ["A", "maz", "ing"]);
        var stressedId = amazing.Syllables[1].Id;
        line.SetStress(amazing.Id, stressedId, StressLevel.Primary, StressProvenance.Imported);

        line.SetSyllables(amazing.Id, ["uh", "A", "maz", "ing"]);
        line.SetText("Oh Amazing grace");

        var retained = line.Words.Single(word => word.Text == "Amazing")
            .Syllables.Single(syllable => syllable.Id == stressedId);
        Assert.Equal(StressLevel.Primary, retained.Stress?.Level);
        Assert.Equal(StressProvenance.Imported, retained.Stress?.Provenance);
    }

    [Fact]
    public void ExplicitNoStress_IsDistinctFromAnUnmarkedSyllable()
    {
        var line = LyricLine.Create("quiet");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["qui", "et"]);

        line.SetStress(word.Id, word.Syllables[0].Id, StressLevel.None);

        Assert.Equal(StressLevel.None, word.Syllables[0].Stress?.Level);
        Assert.Null(word.Syllables[1].Stress);
    }

    [Fact]
    public void ArtistProsody_RecordsPhraseRelativeWeightWithoutInferringFromStress()
    {
        var line = LyricLine.Create("through pain");
        var phrase = line.Phrases[0];
        var pain = line.Words[1];
        line.SetSyllables(pain.Id, ["pain"]);
        var syllable = pain.Syllables[0];
        line.SetStress(pain.Id, syllable.Id, StressLevel.Emphasized);

        Assert.Null(line.Phrases[0].Prosody);

        line.SetProsodicWeight(phrase.Id, syllable.Id, ProsodicWeight.Strong);

        var pattern = Assert.IsType<ProsodicPattern>(line.Phrases[0].Prosody);
        var unit = Assert.Single(pattern.Units);
        Assert.Equal(syllable.Id, unit.SyllableId);
        Assert.Equal(ProsodicWeight.Strong, unit.Weight);
        Assert.Equal(ProsodyProvenance.Manual, unit.Provenance);
    }

    [Fact]
    public void ProsodicWeightEdit_PreservesPatternAndUnitIdentifiers()
    {
        var line = LyricLine.Create("way out");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var phraseId = line.Phrases[0].Id;
        var syllableId = line.Words[1].Syllables[0].Id;
        line.SetProsodicWeight(phraseId, syllableId, ProsodicWeight.Weak);
        var patternId = line.Phrases[0].Prosody!.Id;
        var unitId = line.Phrases[0].Prosody!.Units[0].Id;

        line.SetProsodicWeight(
            phraseId,
            syllableId,
            ProsodicWeight.Strong,
            ProsodyProvenance.Imported);

        var unit = Assert.Single(line.Phrases[0].Prosody!.Units);
        Assert.Equal(patternId, line.Phrases[0].Prosody!.Id);
        Assert.Equal(unitId, unit.Id);
        Assert.Equal(ProsodicWeight.Strong, unit.Weight);
        Assert.Equal(ProsodyProvenance.Imported, unit.Provenance);
    }

    [Fact]
    public void PhraseSplitAndJoin_PartitionAndRestoreProsodicUnitsWithoutChangingUnitIds()
    {
        var line = LyricLine.Create("I thought the way out");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var originalPhrase = line.Phrases[0];
        var thoughtSyllable = line.Words[1].Syllables[0];
        var outSyllable = line.Words[4].Syllables[0];
        line.SetProsodicWeight(originalPhrase.Id, thoughtSyllable.Id, ProsodicWeight.Strong);
        line.SetProsodicWeight(originalPhrase.Id, outSyllable.Id, ProsodicWeight.Strong);
        var originalPatternId = line.Phrases[0].Prosody!.Id;
        var unitIds = line.Phrases[0].Prosody!.Units.Select(item => item.Id).ToList();

        line.SplitPhraseAfter(line.Words[2].Id);

        Assert.Equal(2, line.Phrases.Count);
        Assert.Equal(originalPatternId, line.Phrases[0].Prosody!.Id);
        Assert.NotEqual(originalPatternId, line.Phrases[1].Prosody!.Id);
        Assert.Equal(thoughtSyllable.Id, Assert.Single(line.Phrases[0].Prosody!.Units).SyllableId);
        Assert.Equal(outSyllable.Id, Assert.Single(line.Phrases[1].Prosody!.Units).SyllableId);
        Assert.Equal(unitIds, line.Phrases.SelectMany(item => item.Prosody!.Units).Select(item => item.Id));

        line.JoinPhraseWithPrevious(line.Phrases[1].Id);

        var joined = Assert.IsType<ProsodicPattern>(Assert.Single(line.Phrases).Prosody);
        Assert.Equal(originalPatternId, joined.Id);
        Assert.Equal(unitIds, joined.Units.Select(item => item.Id));
        Assert.Equal([0, 1], joined.Units.Select(item => item.Position));
    }

    [Fact]
    public void PhraseSplit_KeepsPatternIdentityWithTheOnlySurvivingMappedSide()
    {
        var line = LyricLine.Create("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var originalPhrase = line.Phrases[0];
        var mappedSyllable = line.Words[1].Syllables[0];
        line.SetProsodicWeight(originalPhrase.Id, mappedSyllable.Id, ProsodicWeight.Strong);
        var patternId = line.Phrases[0].Prosody!.Id;
        var unitId = line.Phrases[0].Prosody!.Units[0].Id;

        line.SplitPhraseAfter(line.Words[0].Id);

        Assert.Null(line.Phrases[0].Prosody);
        var surviving = Assert.IsType<ProsodicPattern>(line.Phrases[1].Prosody);
        Assert.Equal(patternId, surviving.Id);
        Assert.Equal(unitId, Assert.Single(surviving.Units).Id);
    }

    [Fact]
    public void CompatibleSyllableAndLineEdits_PreserveSurvivingProsodicUnitIdentity()
    {
        var line = LyricLine.Create("Amazing grace");
        var amazing = line.Words[0];
        line.SetSyllables(amazing.Id, ["A", "maz", "ing"]);
        var phraseId = line.Phrases[0].Id;
        var mazId = amazing.Syllables[1].Id;
        line.SetProsodicWeight(phraseId, mazId, ProsodicWeight.Strong);
        var patternId = line.Phrases[0].Prosody!.Id;
        var unitId = line.Phrases[0].Prosody!.Units[0].Id;

        line.SetSyllables(amazing.Id, ["uh", "A", "maz", "ing"]);
        line.SetText("Oh Amazing grace");

        var pattern = Assert.IsType<ProsodicPattern>(line.Phrases[0].Prosody);
        var unit = Assert.Single(pattern.Units);
        Assert.Equal(patternId, pattern.Id);
        Assert.Equal(unitId, unit.Id);
        Assert.Equal(mazId, unit.SyllableId);
    }

    [Fact]
    public void ClearingTheLastProsodicUnit_RemovesTheEmptyPattern()
    {
        var line = LyricLine.Create("home");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["home"]);
        var phraseId = line.Phrases[0].Id;
        var syllableId = word.Syllables[0].Id;
        line.SetProsodicWeight(phraseId, syllableId, ProsodicWeight.Neutral);

        line.SetProsodicWeight(phraseId, syllableId, null);

        Assert.Null(line.Phrases[0].Prosody);
    }

    [Fact]
    public void NewLyricLine_StartsAsOneDefaultPhraseCoveringEveryWord()
    {
        var line = LyricLine.Create("I thought the way out");

        var phrase = Assert.Single(line.Phrases);
        Assert.Equal(0, phrase.Position);
        Assert.Equal(PhraseSource.Default, phrase.Source);
        Assert.Equal(line.Words.Select(item => item.Id), phrase.WordIds);
    }

    [Fact]
    public void ManualPhraseSplitAndJoin_PreserveTheOriginalPhraseIdentity()
    {
        var line = LyricLine.Create("I thought the way out was through pain");
        var originalPhraseId = line.Phrases[0].Id;
        var splitWord = line.Words.Single(item => item.Text == "out");

        line.SplitPhraseAfter(splitWord.Id);

        Assert.Equal(2, line.Phrases.Count);
        Assert.Equal(originalPhraseId, line.Phrases[0].Id);
        Assert.Equal(["I", "thought", "the", "way", "out"],
            line.Phrases[0].WordIds.Select(id => line.Words.Single(word => word.Id == id).Text));
        Assert.Equal(["was", "through", "pain"],
            line.Phrases[1].WordIds.Select(id => line.Words.Single(word => word.Id == id).Text));
        Assert.All(line.Phrases, item => Assert.Equal(PhraseSource.Manual, item.Source));

        line.JoinPhraseWithPrevious(line.Phrases[1].Id);

        var joined = Assert.Single(line.Phrases);
        Assert.Equal(originalPhraseId, joined.Id);
        Assert.Equal(PhraseSource.Manual, joined.Source);
        Assert.Equal(line.Words.Select(item => item.Id), joined.WordIds);
    }

    [Fact]
    public void LineEdit_PreservesPhraseAndPunctuationIdentitiesAroundInsertedWords()
    {
        var line = LyricLine.Create("I thought the way out, was through pain.");
        line.SplitPhraseAfter(line.Words.Single(item => item.Text == "out").Id);
        var phraseIds = line.Phrases.Select(item => item.Id).ToList();
        var punctuationIds = line.Punctuation.Select(item => item.Id).ToList();

        line.SetText("Tonight I thought the way out, was through pain.");

        Assert.Equal(phraseIds, line.Phrases.Select(item => item.Id));
        Assert.Equal(punctuationIds, line.Punctuation.Select(item => item.Id));
        Assert.Contains(line.Words.Single(item => item.Text == "Tonight").Id, line.Phrases[0].WordIds);
        Assert.Equal(line.Words.Select(item => item.Id), line.Phrases.SelectMany(item => item.WordIds));
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
    public void Stress_RejectsInvalidLevelsAndUnknownSyllables()
    {
        var line = LyricLine.Create("word");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["word"]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            line.SetStress(word.Id, word.Syllables[0].Id, (StressLevel)999));
        Assert.Throws<KeyNotFoundException>(() =>
            line.SetStress(word.Id, SyllableId.New(), StressLevel.Primary));
    }

    [Fact]
    public void Prosody_RejectsInvalidWeightsAndSyllablesOutsideThePhrase()
    {
        var line = LyricLine.Create("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        line.SplitPhraseAfter(line.Words[0].Id);
        var firstPhraseId = line.Phrases[0].Id;
        var firstSyllableId = line.Words[0].Syllables[0].Id;
        var secondSyllableId = line.Words[1].Syllables[0].Id;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            line.SetProsodicWeight(firstPhraseId, firstSyllableId, (ProsodicWeight)999));
        Assert.Throws<InvalidOperationException>(() =>
            line.SetProsodicWeight(firstPhraseId, secondSyllableId, ProsodicWeight.Strong));
    }

    [Fact]
    public void LyricLine_RejectsSerializedProsodyThatReferencesAnUnknownSyllable()
    {
        var source = LyricLine.Create("pain");
        source.SetSyllables(source.Words[0].Id, ["pain"]);
        var phrase = new LyricPhrase(
            LyricPhraseId.New(),
            0,
            [source.Words[0].Id],
            PhraseSource.Imported,
            new ProsodicPattern(
                ProsodicPatternId.New(),
                [new ProsodicUnit(
                    ProsodicUnitId.New(),
                    SyllableId.New(),
                    0,
                    ProsodicWeight.Strong,
                    ProsodyProvenance.Imported)]));

        Assert.Throws<ArgumentException>(() => new LyricLine(
            LyricLineId.New(),
            source.Text,
            source.Words,
            source.Punctuation,
            [phrase]));
    }

    [Fact]
    public void LyricWord_RejectsUnorderedSyllablePositions()
    {
        var syllables = new[]
        {
            new LyricSyllable(SyllableId.New(), "a", 0, SyllableSource.Manual),
            new LyricSyllable(SyllableId.New(), "maz", 2, SyllableSource.Manual)
        };

        Assert.Throws<ArgumentException>(() =>
            new LyricWord(LyricWordId.New(), "amaz", 0, 4, syllables));
    }

    [Fact]
    public void LyricLine_RejectsPhraseReferencesThatDoNotCoverWordsInOrder()
    {
        var source = LyricLine.Create("one two");
        var invalidPhrase = new LyricPhrase(
            LyricPhraseId.New(),
            0,
            [source.Words[1].Id],
            PhraseSource.Imported);

        Assert.Throws<ArgumentException>(() => new LyricLine(
            LyricLineId.New(),
            source.Text,
            source.Words,
            source.Punctuation,
            [invalidPhrase]));
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
