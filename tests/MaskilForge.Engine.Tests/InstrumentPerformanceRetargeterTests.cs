using System.Security.Cryptography;
using System.Text;
using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentPerformanceRetargeterTests
{
    [Fact]
    public void Project_MapsTheSameSwellOntoCelloBowExpressionAndGuitarPicking()
    {
        var project = SongProject.Create("Swell retarget");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "loudness.frame", "maskil.browser.loudness", 200, 80,
            [
                new PerformanceMeasurement("rmsDbfs", -18.2m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        var gesture = project.SetPerformanceObservationGesture(observation.Id, now);

        var set = InstrumentPerformanceRetargeter.Project(project, asset.Id);
        var cello = Target(set, "cello");
        var guitar = Target(set, "acoustic-guitar");
        var celloSwell = Assert.Single(cello.Swell.Events);
        var guitarSwell = Assert.Single(guitar.Swell.Events);

        Assert.Equal(asset.Id, set.SourceAssetId);
        Assert.Equal(["cello", "acoustic-guitar", "piano", "electric-bass", "drum-kit", "violin", "flute", "clarinet", "trumpet", "synth-pad", "synth-lead", "electric-guitar"], set.Targets.Select(item => item.InstrumentId));

        Assert.Equal("Cello", cello.InstrumentName);
        Assert.True(cello.Swell.Applicable);
        Assert.Equal(InstrumentArticulation.BowExpression, cello.Swell.Articulation);
        Assert.Equal(gesture.Id, celloSwell.GestureId);
        Assert.Equal(192, celloSwell.StartTick);
        Assert.Equal(77, celloSwell.DurationTicks);
        Assert.Null(celloSwell.Pitch);
        Assert.Equal(88, celloSwell.Value);
        Assert.Null(celloSwell.RangeKind);

        Assert.Equal("Acoustic Guitar", guitar.InstrumentName);
        Assert.True(guitar.Swell.Applicable);
        Assert.Equal(InstrumentArticulation.Picking, guitar.Swell.Articulation);
        Assert.Equal(celloSwell.StartTick, guitarSwell.StartTick);
        Assert.Equal(celloSwell.Value, guitarSwell.Value);
        Assert.Empty(cello.Slide.Events);
        Assert.Empty(guitar.Slide.Events);
        Assert.True(Target(set, "violin").Swell.Applicable);
        Assert.Equal(InstrumentArticulation.BowExpression, Target(set, "violin").Swell.Articulation);
        Assert.Equal(InstrumentArticulation.Breath, Target(set, "flute").Swell.Articulation);
        Assert.Equal(InstrumentArticulation.Legato, Target(set, "clarinet").Swell.Articulation);
        Assert.Equal(InstrumentArticulation.Legato, Target(set, "trumpet").Swell.Articulation);
        Assert.False(Target(set, "flute").Slide.Applicable);
        Assert.False(Target(set, "clarinet").Hit.Applicable);
        Assert.Empty(Target(set, "trumpet").Hit.Events);
        Assert.False(Target(set, "synth-pad").Swell.Applicable);
        Assert.False(Target(set, "synth-lead").Slide.Applicable);
        Assert.False(Target(set, "electric-guitar").Hit.Applicable);
        Assert.Empty(Target(set, "synth-pad").Swell.Events);
        Assert.Empty(Target(set, "electric-guitar").Hit.Events);
    }

    [Fact]
    public void Project_MapsPianoStrikeAndBassFingerWithoutInventingSlidesOrKitHits()
    {
        var project = SongProject.Create("Proof-set retarget");
        var asset = CreateAsset();
        var loudness = CreateObservation(asset.Id, "loudness.frame", "maskil.browser.loudness", 200, 80,
            [
                new PerformanceMeasurement("rmsDbfs", -18.2m, "dBFS"),
                new PerformanceMeasurement("peakDbfs", -4.1m, "dBFS")
            ]);
        var pitch = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(loudness);
        project.RegisterPerformanceObservation(pitch);
        project.SetPerformanceObservationReview(loudness.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationReview(pitch.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(loudness.Id, now);
        project.SetPerformanceObservationGesture(pitch.Id, now);

        var set = InstrumentPerformanceRetargeter.Project(project, asset.Id);
        var piano = Target(set, "piano");
        var bass = Target(set, "electric-bass");
        var kit = Target(set, "drum-kit");

        Assert.True(piano.Swell.Applicable);
        Assert.Equal(InstrumentArticulation.Strike, piano.Swell.Articulation);
        Assert.Equal(88, Assert.Single(piano.Swell.Events).Value);
        Assert.False(piano.Slide.Applicable);
        Assert.Empty(piano.Slide.Events);

        Assert.True(bass.Swell.Applicable);
        Assert.Equal(InstrumentArticulation.Finger, bass.Swell.Articulation);
        Assert.Equal(88, Assert.Single(bass.Swell.Events).Value);
        Assert.False(bass.Slide.Applicable);
        Assert.Empty(bass.Slide.Events);

        Assert.False(kit.Swell.Applicable);
        Assert.Null(kit.Swell.Articulation);
        Assert.Empty(kit.Swell.Events);
        Assert.False(kit.Slide.Applicable);
        Assert.Null(kit.Slide.Articulation);
        Assert.Empty(kit.Slide.Events);
        Assert.True(kit.Hit.Applicable);
        Assert.Equal(InstrumentArticulation.Hit, kit.Hit.Articulation);
        Assert.Empty(kit.Hit.Events);
        Assert.False(piano.Hit.Applicable);
        Assert.Empty(piano.Hit.Events);
        Assert.False(bass.Hit.Applicable);
        Assert.Empty(bass.Hit.Events);
        Assert.Equal(69, Assert.Single(Target(set, "cello").Slide.Events).Pitch!.MidiNumber);
        Assert.Empty(Target(set, "cello").Hit.Events);
    }

    [Fact]
    public void Project_MapsTheSameSlideOntoCelloSlideAndGuitarBend()
    {
        var project = SongProject.Create("Slide retarget");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        var gesture = project.SetPerformanceObservationGesture(observation.Id, now);

        var set = InstrumentPerformanceRetargeter.Project(project, asset.Id);
        var celloSlide = Assert.Single(Target(set, "cello").Slide.Events);
        var guitarSlide = Assert.Single(Target(set, "acoustic-guitar").Slide.Events);

        Assert.Equal(InstrumentArticulation.Slide, Target(set, "cello").Slide.Articulation);
        Assert.Equal(InstrumentArticulation.Bend, Target(set, "acoustic-guitar").Slide.Articulation);
        Assert.Equal(gesture.Id, celloSlide.GestureId);
        Assert.Equal(69, celloSlide.Pitch!.MidiNumber);
        Assert.Equal(192, celloSlide.StartTick);
        Assert.Null(celloSlide.Value);
        Assert.Null(celloSlide.RangeKind);
        Assert.Equal(celloSlide.Pitch.MidiNumber, guitarSlide.Pitch!.MidiNumber);
        Assert.Equal(celloSlide.StartTick, guitarSlide.StartTick);
        Assert.Empty(Target(set, "cello").Swell.Events);
        Assert.Empty(Target(set, "acoustic-guitar").Swell.Events);
        Assert.Equal(InstrumentArticulation.Slide, Target(set, "violin").Slide.Articulation);
        Assert.Equal(69, Assert.Single(Target(set, "violin").Slide.Events).Pitch!.MidiNumber);
        Assert.False(Target(set, "flute").Slide.Applicable);
        Assert.Empty(Target(set, "flute").Slide.Events);
        Assert.False(Target(set, "clarinet").Slide.Applicable);
        Assert.False(Target(set, "trumpet").Slide.Applicable);
    }

    [Fact]
    public void Project_ReportsRangeWithoutTransposing()
    {
        var project = SongProject.Create("Range retarget");
        var asset = CreateAsset();
        var high = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 0, 80,
            [new PerformanceMeasurement("frequencyHertz", 987.77m, "hertz")]);
        var low = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 100, 80,
            [new PerformanceMeasurement("frequencyHertz", 73.42m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(high);
        project.RegisterPerformanceObservation(low);
        project.SetPerformanceObservationReview(high.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationReview(low.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(high.Id, now);
        project.SetPerformanceObservationGesture(low.Id, now);

        var set = InstrumentPerformanceRetargeter.Project(project, asset.Id);
        var cello = Target(set, "cello").Slide.Events;
        var guitar = Target(set, "acoustic-guitar").Slide.Events;
        var celloHigh = Assert.Single(cello, item => item.Pitch!.MidiNumber == 83);
        var celloLow = Assert.Single(cello, item => item.Pitch!.MidiNumber == 38);
        var guitarHigh = Assert.Single(guitar, item => item.Pitch!.MidiNumber == 83);
        var guitarLow = Assert.Single(guitar, item => item.Pitch!.MidiNumber == 38);

        Assert.Equal(RangeCollisionKind.Above, celloHigh.RangeKind);
        Assert.Null(celloLow.RangeKind);
        Assert.Null(guitarHigh.RangeKind);
        Assert.Equal(RangeCollisionKind.Below, guitarLow.RangeKind);
        Assert.Equal(83, celloHigh.Pitch!.MidiNumber);
        Assert.Equal(38, guitarLow.Pitch!.MidiNumber);
        var violin = Target(set, "violin").Slide.Events;
        Assert.Null(Assert.Single(violin, item => item.Pitch!.MidiNumber == 83).RangeKind);
        Assert.Equal(RangeCollisionKind.Below, Assert.Single(violin, item => item.Pitch!.MidiNumber == 38).RangeKind);
        Assert.Empty(Target(set, "flute").Slide.Events);
        Assert.Empty(Target(set, "clarinet").Slide.Events);
        Assert.Empty(Target(set, "trumpet").Slide.Events);
    }

    [Fact]
    public void Project_MapsOnsetOntoKitHitWithoutInventingHitsOnPitchedInstruments()
    {
        var project = SongProject.Create("Kit hit retarget");
        var asset = CreateAsset();
        var onset = CreateObservation(asset.Id, "onset.event", "maskil.browser.onset-energy", 96, 32,
            [
                new PerformanceMeasurement("strength", 0.8m, "normalized"),
                new PerformanceMeasurement("confidence", 0.9m, "normalized")
            ]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(onset);
        project.SetPerformanceObservationReview(onset.Id, PerformanceObservationReviewVerdict.Accurate, now);
        var gesture = project.SetPerformanceObservationGesture(onset.Id, now);

        var set = InstrumentPerformanceRetargeter.Project(project, asset.Id);
        var kit = Target(set, "drum-kit");
        var hit = Assert.Single(kit.Hit.Events);

        Assert.True(kit.Hit.Applicable);
        Assert.Equal(InstrumentArticulation.Hit, kit.Hit.Articulation);
        Assert.Equal(gesture.Id, hit.GestureId);
        Assert.Equal(60, hit.Pitch!.MidiNumber);
        Assert.Equal(92, hit.StartTick);
        Assert.Equal(31, hit.DurationTicks);
        Assert.Equal(102, hit.Value);
        Assert.Null(hit.RangeKind);
        Assert.False(kit.Swell.Applicable);
        Assert.Empty(kit.Swell.Events);
        Assert.False(kit.Slide.Applicable);
        Assert.Empty(kit.Slide.Events);

        Assert.All(set.Targets.Where(item => item.InstrumentId != "drum-kit"), target =>
        {
            Assert.False(target.Hit.Applicable);
            Assert.Empty(target.Hit.Events);
        });
    }

    [Fact]
    public void Project_RequiresPitchLoudnessOrOnset()
    {
        var project = SongProject.Create("No gestures");
        var asset = CreateAsset();
        project.RegisterAsset(asset);

        var error = Assert.Throws<InvalidOperationException>(() =>
            InstrumentPerformanceRetargeter.Project(project, asset.Id));

        Assert.Contains("Promote at least one pitch, loudness, or onset claim", error.Message);
    }

    [Fact]
    public void Project_RequiresAnExistingOriginalVocalTake()
    {
        var project = SongProject.Create("Missing take");

        Assert.Throws<KeyNotFoundException>(() =>
            InstrumentPerformanceRetargeter.Project(project, ProjectAssetId.New()));
    }

    [Fact]
    public void Project_OffsetsTakeRelativeTicksByTheArtistPlacement()
    {
        var project = SongProject.Create("Placed retarget");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        project.SetVocalTakePlacement(asset.Id, new MusicalPosition(9, 1, 0));

        var set = InstrumentPerformanceRetargeter.Project(project, asset.Id);
        var slide = Assert.Single(Target(set, "cello").Slide.Events);

        Assert.Equal(15_360, set.StartTick);
        Assert.Equal(15_552, slide.StartTick);
        Assert.Equal(77, slide.DurationTicks);
    }

    [Fact]
    public void Project_DoesNotMutateTheSongGraph()
    {
        var project = SongProject.Create("Stable retarget");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 200, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        var gesture = project.SetPerformanceObservationGesture(observation.Id, now);
        var note = project.AddNoteEvent(new RegisteredPitch(NoteLetter.C, Accidental.Natural, 4), 0, 120, 70);

        InstrumentPerformanceRetargeter.Project(project, asset.Id);

        Assert.Equal(note.Id, Assert.Single(project.NoteEvents).Id);
        Assert.Equal(gesture.Id, Assert.Single(project.PerformanceObservationGestures).Id);
        Assert.Empty(project.MusicalParts);
        Assert.Empty(project.ExpressionCurves);
    }

    [Fact]
    public void Project_LeavesUnmappedCatalogInstrumentsNotApplicable()
    {
        var project = SongProject.Create("Unknown instrument");
        var asset = CreateAsset();
        var observation = CreateObservation(asset.Id, "pitch.frame", "maskil.browser.pitch-acf", 0, 80,
            [new PerformanceMeasurement("frequencyHertz", 440m, "hertz")]);
        var now = DateTimeOffset.UtcNow;
        project.RegisterAsset(asset);
        project.RegisterPerformanceObservation(observation);
        project.SetPerformanceObservationReview(observation.Id, PerformanceObservationReviewVerdict.Accurate, now);
        project.SetPerformanceObservationGesture(observation.Id, now);
        var catalog = new InstrumentProfileCatalog(2, [
            new InstrumentProfile(
                "oboe",
                "Oboe",
                true,
                new RegisteredPitch(NoteLetter.G, Accidental.Natural, 3),
                new RegisteredPitch(NoteLetter.A, Accidental.Natural, 7),
                [ArrangementRole.Countermelody],
                [InstrumentArticulation.Legato, InstrumentArticulation.Slide],
                [InstrumentExpressiveQuality.Intimate]),
        ]);

        var set = InstrumentPerformanceRetargeter.Project(project, asset.Id, catalog);
        var oboe = Assert.Single(set.Targets);

        Assert.Equal("oboe", oboe.InstrumentId);
        Assert.False(oboe.Swell.Applicable);
        Assert.Empty(oboe.Swell.Events);
        Assert.False(oboe.Slide.Applicable);
        Assert.Empty(oboe.Slide.Events);
        Assert.False(oboe.Hit.Applicable);
        Assert.Empty(oboe.Hit.Events);
    }

    private static InstrumentPerformanceSketch Target(InstrumentPerformanceRetargetSet set, string instrumentId) =>
        Assert.Single(set.Targets, item => item.InstrumentId == instrumentId);

    private static ProjectAsset CreateAsset()
    {
        var content = Encoding.UTF8.GetBytes("artist-retargeted source performance");
        return new ProjectAsset(
            ProjectAssetId.New(),
            ProjectAssetKind.OriginalVocalTake,
            "audio/webm",
            content.LongLength,
            Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            "Retarget take");
    }

    private static PerformanceObservation CreateObservation(
        ProjectAssetId assetId,
        string kind,
        string analyzerId,
        long startMilliseconds,
        long durationMilliseconds,
        IReadOnlyList<PerformanceMeasurement> measurements) => new(
        PerformanceObservationId.New(),
        assetId,
        kind,
        startMilliseconds,
        durationMilliseconds,
        measurements,
        0.8m,
        analyzerId,
        "1.0.0",
        PerformanceObservationProvenance.DeterministicAnalyzer,
        DateTimeOffset.UtcNow);
}
