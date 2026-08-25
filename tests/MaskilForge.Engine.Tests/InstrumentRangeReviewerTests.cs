using MaskilForge.Domain;
using MaskilForge.Engine;

namespace MaskilForge.Engine.Tests;

public sealed class InstrumentRangeReviewerTests
{
    [Fact]
    public void Review_MarksInclusiveBoundsAndDirectionWithoutTransposing()
    {
        var below = Note(NoteLetter.B, 1);
        var low = Note(NoteLetter.C, 2);
        var high = Note(NoteLetter.A, 5);
        var above = Note(NoteLetter.C, 6);

        var set = InstrumentRangeReviewer.Review([below, low, high, above]);
        var cello = Assert.Single(set.Reviews, item => item.InstrumentId == "cello");

        Assert.True(cello.Applicable);
        Assert.Equal("Cello", cello.InstrumentName);
        Assert.Equal(2, cello.InRangeCount);
        Assert.Equal(
            [below.Id, above.Id],
            cello.OutOfRange.Select(item => item.NoteEventId));
        Assert.Equal(
            [RangeCollisionKind.Below, RangeCollisionKind.Above],
            cello.OutOfRange.Select(item => item.Kind));
    }

    [Fact]
    public void Review_TreatsDrumKitAsNotApplicable()
    {
        var set = InstrumentRangeReviewer.Review([Note(NoteLetter.C, 4)]);
        var drums = Assert.Single(set.Reviews, item => item.InstrumentId == "drum-kit");

        Assert.False(drums.Applicable);
        Assert.Equal(0, drums.InRangeCount);
        Assert.Empty(drums.OutOfRange);
    }

    [Fact]
    public void Review_KeepsCatalogOrderAndCountsEmptyNotesAsInRange()
    {
        var set = InstrumentRangeReviewer.Review([]);

        Assert.Equal(
            ["cello", "acoustic-guitar", "piano", "electric-bass", "drum-kit", "violin", "flute", "clarinet", "trumpet", "synth-pad", "synth-lead", "electric-guitar"],
            set.Reviews.Select(item => item.InstrumentId));
        Assert.All(set.Reviews.Where(item => item.Applicable), item =>
        {
            Assert.Equal(0, item.InRangeCount);
            Assert.Empty(item.OutOfRange);
        });
    }

    [Fact]
    public void Review_FitsPianoExtremesAndRejectsBassAboveG4()
    {
        var low = Note(NoteLetter.A, 0);
        var high = Note(NoteLetter.C, 8);

        var set = InstrumentRangeReviewer.Review([low, high]);
        var piano = Assert.Single(set.Reviews, item => item.InstrumentId == "piano");
        var bass = Assert.Single(set.Reviews, item => item.InstrumentId == "electric-bass");

        Assert.Equal(2, piano.InRangeCount);
        Assert.Empty(piano.OutOfRange);
        Assert.Equal(0, bass.InRangeCount);
        Assert.Equal([RangeCollisionKind.Below, RangeCollisionKind.Above], bass.OutOfRange.Select(item => item.Kind));
    }

    [Fact]
    public void Review_RequiresNoteIds()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            InstrumentRangeReviewer.Review([new InstrumentRangeReviewNote(default, Pitch(NoteLetter.C, 4))]));
        Assert.Contains("A note-event ID is required", error.Message);
    }

    private static InstrumentRangeReviewNote Note(NoteLetter letter, int octave) =>
        new(NoteEventId.New(), Pitch(letter, octave));

    private static RegisteredPitch Pitch(NoteLetter letter, int octave) =>
        new(letter, Accidental.Natural, octave);
}
