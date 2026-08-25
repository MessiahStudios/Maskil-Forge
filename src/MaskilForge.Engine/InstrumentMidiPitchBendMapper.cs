using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// An inspectable MIDI pitch-bend range for one catalog instrument. It follows
/// that instrument's slide articulation. Slide and Bend use ±2 semitones.
/// Portamento is not pitch bend. Drum kit and instruments without a slide have
/// no range. The host does not move the pitch wheel.
/// </summary>
public sealed record InstrumentMidiPitchBendAssignment(
    string InstrumentId,
    string InstrumentName,
    bool Applicable,
    InstrumentArticulation? Articulation,
    int? RangeSemitones);

public sealed record InstrumentMidiPitchBendMapSet(
    IReadOnlyList<InstrumentMidiPitchBendAssignment> Assignments);

public static class InstrumentMidiPitchBendMapper
{
    public const int RangeSemitones = 2;
    public const int RpnMsbController = 101;
    public const int RpnLsbController = 100;
    public const int DataEntryMsbController = 6;

    public static InstrumentMidiPitchBendMapSet Map(InstrumentProfileCatalog? catalog = null)
    {
        catalog ??= InstrumentProfileCatalogLoader.Current;
        var articulations = InstrumentArticulationMapper.Map(catalog);
        var assignments = catalog.Instruments.Select(instrument =>
        {
            var slide = articulations.Maps
                .Single(item => string.Equals(item.InstrumentId, instrument.Id, StringComparison.Ordinal))
                .Mappings.Single(item => item.Gesture == NeutralPerformanceGesture.Slide);
            if (!slide.Applicable || slide.Articulation is null)
            {
                return new InstrumentMidiPitchBendAssignment(
                    instrument.Id, instrument.Name, false, null, null);
            }

            if (slide.Articulation is InstrumentArticulation.Slide or InstrumentArticulation.Bend)
            {
                return new InstrumentMidiPitchBendAssignment(
                    instrument.Id, instrument.Name, true, slide.Articulation, RangeSemitones);
            }

            return new InstrumentMidiPitchBendAssignment(
                instrument.Id, instrument.Name, false, slide.Articulation, null);
        }).ToList();
        return new InstrumentMidiPitchBendMapSet(assignments);
    }
}
