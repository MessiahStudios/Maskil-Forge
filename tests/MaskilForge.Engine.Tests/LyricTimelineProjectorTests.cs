using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class LyricTimelineProjectorTests
{
    [Fact]
    public void Project_MapsSectionSpansAndSyllableMarkersOntoAbsoluteTicks()
    {
        var project = SongProject.Create("Timeline UI");
        var verse = project.AddSection(SectionKind.Verse);
        var chorus = project.AddSection(SectionKind.Chorus);
        project.SetSectionDuration(verse.Id, 4);
        project.SetSectionDuration(chorus.Id, 4);

        var line = verse.AddLyricLine("hold on");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var first = line.Words[0].Syllables[0].Id;
        var second = line.Words[1].Syllables[0].Id;
        project.SetSyllablePlacement(verse.Id, line.Id, first, new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(verse.Id, line.Id, second, new BeatPosition(2, 3, 0));
        line.SetBreathPoint(first, true);

        var view = LyricTimelineProjector.Project(project);

        Assert.Equal(2, view.Sections.Count);
        Assert.Equal(4, view.BeatsPerBar);
        Assert.Equal(480, view.TicksPerBeat);
        Assert.Equal(8L * 4 * 480, view.TotalTicks);

        var verseSpan = Assert.Single(view.Sections, item => item.SectionId == verse.Id);
        Assert.Equal(0, verseSpan.StartTick);
        Assert.Equal(4L * 4 * 480, verseSpan.EndTickExclusive);

        var chorusSpan = Assert.Single(view.Sections, item => item.SectionId == chorus.Id);
        Assert.Equal(4L * 4 * 480, chorusSpan.StartTick);

        var active = view.Markers.Where(item => item.Kind == LyricTimelineMarkerKind.ActivePlacement).ToList();
        Assert.Equal(2, active.Count);
        Assert.Equal(0, active[0].AbsoluteTick);
        Assert.Equal(new MusicalPosition(1, 1, 0), active[0].SongPosition);
        Assert.True(active[0].HasBreathAfter);
        Assert.Equal((1L * 4 + 2) * 480, active[1].AbsoluteTick);
        Assert.Equal(new MusicalPosition(2, 3, 0), active[1].SongPosition);

        var breath = Assert.Single(view.Markers, item => item.Kind == LyricTimelineMarkerKind.BreathAfter);
        Assert.Equal(first, breath.SyllableId);
        Assert.True(breath.AbsoluteTick > active[0].AbsoluteTick);
    }

    [Fact]
    public void Project_CanOverlayARhythmCandidateWithoutChangingActiveMarkers()
    {
        var project = SongProject.Create("Timeline UI");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("one two");
        foreach (var word in line.Words) line.SetSyllables(word.Id, [word.Text]);
        var first = line.Words[0].Syllables[0].Id;
        var second = line.Words[1].Syllables[0].Id;
        project.SetSyllablePlacement(section.Id, line.Id, first, new BeatPosition(1, 1, 0));
        project.SetSyllablePlacement(section.Id, line.Id, second, new BeatPosition(1, 3, 0));
        var candidate = project.CaptureRhythmCandidate(
            section.Id,
            line.Id,
            line.Phrases[0].Id,
            "Option A");
        project.SetSyllablePlacement(section.Id, line.Id, second, new BeatPosition(2, 1, 0));

        var view = LyricTimelineProjector.Project(project, candidate.Id);
        Assert.Equal(2, view.Markers.Count(item => item.Kind == LyricTimelineMarkerKind.ActivePlacement));
        var ghosts = view.Markers.Where(item => item.Kind == LyricTimelineMarkerKind.RhythmCandidate).ToList();
        Assert.Equal(2, ghosts.Count);
        Assert.All(ghosts, item => Assert.Equal(candidate.Id, item.RhythmCandidateId));
        Assert.Contains(ghosts, item => item.SyllableId == second && item.SectionRelative == new BeatPosition(1, 3, 0));
        Assert.Contains(
            view.Markers.Where(item => item.Kind == LyricTimelineMarkerKind.ActivePlacement),
            item => item.SyllableId == second && item.SectionRelative == new BeatPosition(2, 1, 0));
    }

    [Fact]
    public void Project_IncludesStressAndProsodicWeightWhenPresent()
    {
        var project = SongProject.Create("Timeline UI");
        var section = project.AddSection(SectionKind.Verse);
        var line = section.AddLyricLine("fire");
        var word = line.Words[0];
        line.SetSyllables(word.Id, ["fire"]);
        var syllableId = word.Syllables[0].Id;
        line.SetStress(word.Id, syllableId, StressLevel.Primary);
        line.SetProsodicWeight(line.Phrases[0].Id, syllableId, ProsodicWeight.Strong);
        project.SetSyllablePlacement(section.Id, line.Id, syllableId, new BeatPosition(1, 2, 0));

        var marker = Assert.Single(
            LyricTimelineProjector.Project(project).Markers,
            item => item.Kind == LyricTimelineMarkerKind.ActivePlacement);
        Assert.Equal(StressLevel.Primary, marker.StressLevel);
        Assert.Equal(ProsodicWeight.Strong, marker.ProsodicWeight);
        Assert.Equal(line.Phrases[0].Id, marker.PhraseId);
    }
}
