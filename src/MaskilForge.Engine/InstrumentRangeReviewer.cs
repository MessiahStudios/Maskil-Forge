using MaskilForge.Domain;

namespace MaskilForge.Engine;

public enum RangeCollisionKind
{
    Below,
    Above
}

public sealed record InstrumentRangeReviewNote(NoteEventId Id, RegisteredPitch Pitch);

public sealed record InstrumentRangeCollision(
    NoteEventId NoteEventId,
    RegisteredPitch Pitch,
    RangeCollisionKind Kind);

/// <summary>
/// A transient, inspectable comparison of existing notes against one catalog
/// instrument's melodic range. It does not transpose, assign, or change the Song Graph.
/// </summary>
public sealed record InstrumentRangeReview(
    string InstrumentId,
    string InstrumentName,
    bool Applicable,
    int InRangeCount,
    IReadOnlyList<InstrumentRangeCollision> OutOfRange);

public sealed record InstrumentRangeReviewSet(IReadOnlyList<InstrumentRangeReview> Reviews);

public static class InstrumentRangeReviewer
{
    public static InstrumentRangeReviewSet Review(
        IReadOnlyList<InstrumentRangeReviewNote> notes,
        InstrumentProfileCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(notes);
        if (notes.Any(note => note.Id.Value == Guid.Empty))
            throw new ArgumentException("A note-event ID is required.", nameof(notes));
        if (notes.Any(note => note.Pitch is null))
            throw new ArgumentNullException(nameof(notes), "Each range-review note must include a pitch.");

        catalog ??= InstrumentProfileCatalogLoader.Current;
        var reviews = catalog.Instruments.Select(instrument => ReviewInstrument(instrument, notes)).ToList();
        return new InstrumentRangeReviewSet(reviews);
    }

    private static InstrumentRangeReview ReviewInstrument(
        InstrumentProfile instrument,
        IReadOnlyList<InstrumentRangeReviewNote> notes)
    {
        if (!instrument.Pitched)
            return new InstrumentRangeReview(instrument.Id, instrument.Name, false, 0, []);

        var minimum = instrument.MinimumPitch!.MidiNumber;
        var maximum = instrument.MaximumPitch!.MidiNumber;
        var collisions = notes
            .Select(note =>
            {
                var midi = note.Pitch.MidiNumber;
                if (midi < minimum) return new InstrumentRangeCollision(note.Id, note.Pitch, RangeCollisionKind.Below);
                if (midi > maximum) return new InstrumentRangeCollision(note.Id, note.Pitch, RangeCollisionKind.Above);
                return null;
            })
            .OfType<InstrumentRangeCollision>()
            .ToList();

        return new InstrumentRangeReview(
            instrument.Id,
            instrument.Name,
            true,
            notes.Count - collisions.Count,
            collisions);
    }
}
