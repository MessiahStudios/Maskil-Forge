using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class LyricSheetStructureTests
{
    [Fact]
    public void Parse_EssenceOfShadows_DetectsKnownSectionsAndExplicitDelivery()
    {
        const string lyrics = """
        Essence of Shadows
        [Intro – Spoken over ambient piano + distant pad]
        In the essence of shadows…
        Where my soul wandered among the living…
        [Verse 1 – Soft, talk-sung, grounded]
        Among the noise and neon glow,
        Faces laughing I don’t know.
        [Pre-Chorus – Slight widen, no lift]
        This soul… it yearns for something more—
        [Chorus – Midrange, widen tone, not higher]
        In the essence of shadows
        [Verse 2 – Storytelling, restrained build]
        Anger once my only shield,
        [Pre-Chorus – Slight tension]
        For God so loved…
        [Chorus – Wider instrumentation, subtle harmony]
        In the essence of shadows
        [Bridge – Drop drums, near spoken]
        Not polished.
        [Final Chorus – Controlled build, no soaring]
        Light was cast on me.
        [Outro – Soft piano, whispered]
        Among the living…
        I was found.
        """;

        var preview = LyricSheetStructureParser.Parse(lyrics);

        Assert.Equal(10, preview.Sections.Count);
        Assert.Equal([SectionKind.Intro, SectionKind.Verse, SectionKind.PreChorus, SectionKind.Chorus,
            SectionKind.Verse, SectionKind.PreChorus, SectionKind.Chorus, SectionKind.Bridge,
            SectionKind.Chorus, SectionKind.Outro], preview.Sections.Select(section => section.Kind));
        Assert.Equal(SectionDelivery.Spoken, preview.Sections[0].Delivery);
        Assert.Equal(SectionDelivery.TalkSung, preview.Sections[1].Delivery);
        Assert.Equal(SectionDelivery.Spoken, preview.Sections[7].Delivery);
        Assert.Equal(SectionDelivery.Whispered, preview.Sections[9].Delivery);
        Assert.Equal("Final Chorus", preview.Sections[8].Title);
        Assert.Equal("Chorus 1", preview.Sections[3].Title);
        Assert.Equal("Chorus 2", preview.Sections[6].Title);
        Assert.Equal("Controlled build, no soaring", preview.Sections[8].PerformanceNotes);
        Assert.All(preview.Sections, section => Assert.Equal(StructuralFunction.Unspecified, section.StructuralFunction));
        Assert.Equal(["Essence of Shadows"], preview.UnassignedLines);
    }

    [Fact]
    public void ImportSongStructure_IsOneUndoableDecisionWithStableRedoIdentities()
    {
        var project = SongProject.Create("Essence of Shadows");
        project.SetRawLyricDraft("[Intro – Spoken]\nI was found.\n[Chorus]\nLight was cast on me.");
        var preview = LyricSheetStructureParser.Parse(project.RawLyricDraft);
        var editor = new ProjectEditor(project);

        var proposals = preview.Sections.Select((section, index) => section with
        {
            StructuralFunction = index == 0 ? StructuralFunction.Setup : StructuralFunction.Payoff
        }).ToList();
        editor.Execute(new ImportSongStructureCommand(proposals));
        var identities = project.Sections.Select(section => section.Id).ToList();

        Assert.Equal(2, project.Sections.Count);
        Assert.Equal(SectionDelivery.Spoken, project.Sections[0].Delivery);
        Assert.Equal(StructuralFunction.Setup, project.Sections[0].StructuralFunction);
        Assert.Equal(StructuralFunction.Payoff, project.Sections[1].StructuralFunction);
        Assert.Equal("I was found.", Assert.Single(project.Sections[0].LyricLines).Text);
        Assert.Equal("[Intro – Spoken]\nI was found.\n[Chorus]\nLight was cast on me.", project.RawLyricDraft);

        editor.Undo();
        Assert.Empty(project.Sections);
        Assert.NotEmpty(project.RawLyricDraft);
        editor.Redo();
        Assert.Equal(identities, project.Sections.Select(section => section.Id));
    }

    [Fact]
    public void Parse_LeavesUnknownHeadingsVisibleInsteadOfGuessing()
    {
        var preview = LyricSheetStructureParser.Parse("[Invocation]\nUnknown opening\n[Verse]\nKnown line");

        Assert.Single(preview.Sections);
        Assert.Empty(preview.UnassignedLines);
        Assert.Equal(["[Invocation]"], preview.UnrecognizedHeadings);
        var unresolved = Assert.Single(preview.UnrecognizedSections);
        Assert.Equal("Invocation", unresolved.Heading);
        Assert.Equal(["Unknown opening"], unresolved.Lyrics);
        Assert.Equal(0, unresolved.InsertionIndex);
    }

    [Fact]
    public void Parse_UnknownHeadingAfterKnownSection_DoesNotBecomeLyricsOrCaptureFollowingLines()
    {
        var preview = LyricSheetStructureParser.Parse("[Verse]\nKnown line\n[Refrain]\nAmbiguous refrain\n[Outro]\nClosing line");

        Assert.Equal(2, preview.Sections.Count);
        Assert.Equal(["Known line"], preview.Sections[0].Lyrics);
        Assert.Equal(["Closing line"], preview.Sections[1].Lyrics);
        Assert.Equal(["[Refrain]"], preview.UnrecognizedHeadings);
        Assert.Empty(preview.UnassignedLines);
        var unresolved = Assert.Single(preview.UnrecognizedSections);
        Assert.Equal("Refrain", unresolved.Heading);
        Assert.Equal(["Ambiguous refrain"], unresolved.Lyrics);
        Assert.Equal(1, unresolved.InsertionIndex);
    }

    [Fact]
    public void Parse_AcceptsAPlainHyphenOnlyWhenItSeparatesHeadingDirection()
    {
        var preview = LyricSheetStructureParser.Parse("[Pre-Chorus - Slight tension]\nFor God so loved…");

        var section = Assert.Single(preview.Sections);
        Assert.Equal(SectionKind.PreChorus, section.Kind);
        Assert.Equal("Slight tension", section.PerformanceNotes);
    }
}
