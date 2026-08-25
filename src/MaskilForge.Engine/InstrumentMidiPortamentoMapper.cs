using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// An inspectable MIDI portamento controller for one catalog instrument. It
/// follows that instrument's slide articulation. Synth-lead Portamento is CC 65.
/// MIDI export emits Portamento Off so stored notes stay discrete. The host does
/// not turn portamento on or invent a glide between pitches.
/// </summary>
public sealed record InstrumentMidiPortamentoAssignment(
    string InstrumentId,
    string InstrumentName,
    bool Applicable,
    InstrumentArticulation? Articulation,
    string? ControllerName,
    int? ControllerNumber);

public sealed record InstrumentMidiPortamentoMapSet(
    IReadOnlyList<InstrumentMidiPortamentoAssignment> Assignments);

public static class InstrumentMidiPortamentoMapper
{
    public const int PortamentoControllerNumber = 65;
    public const string PortamentoControllerName = "Portamento";
    public const byte PortamentoOffValue = 0;

    public static InstrumentMidiPortamentoMapSet Map(InstrumentProfileCatalog? catalog = null)
    {
        catalog ??= InstrumentProfileCatalogLoader.Current;
        var articulations = InstrumentArticulationMapper.Map(catalog);
        var assignments = catalog.Instruments.Select(instrument =>
        {
            var slide = articulations.Maps
                .Single(item => string.Equals(item.InstrumentId, instrument.Id, StringComparison.Ordinal))
                .Mappings.Single(item => item.Gesture == NeutralPerformanceGesture.Slide);
            if (slide.Applicable && slide.Articulation is InstrumentArticulation.Portamento)
            {
                return new InstrumentMidiPortamentoAssignment(
                    instrument.Id,
                    instrument.Name,
                    true,
                    slide.Articulation,
                    PortamentoControllerName,
                    PortamentoControllerNumber);
            }

            return new InstrumentMidiPortamentoAssignment(
                instrument.Id, instrument.Name, false, null, null, null);
        }).ToList();
        return new InstrumentMidiPortamentoMapSet(assignments);
    }
}
