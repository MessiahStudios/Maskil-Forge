using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// An inspectable MIDI continuous controller for tagged dynamics on one catalog
/// instrument. It follows that instrument's swell articulation. Drum kit has no
/// dynamics controller. Untagged curves stay on Expression (CC 11).
/// </summary>
public sealed record InstrumentMidiControllerAssignment(
    string InstrumentId,
    string InstrumentName,
    bool Applicable,
    InstrumentArticulation? Articulation,
    string? ControllerName,
    int? ControllerNumber);

public sealed record InstrumentMidiControllerMapSet(
    IReadOnlyList<InstrumentMidiControllerAssignment> Assignments);

public static class InstrumentMidiControllerMapper
{
    public const int ExpressionControllerNumber = 11;
    public const string ExpressionControllerName = "Expression";
    public const int BreathControllerNumber = 2;
    public const string BreathControllerName = "Breath Controller";
    public const int BrightnessControllerNumber = 74;
    public const string BrightnessControllerName = "Brightness";

    public static InstrumentMidiControllerMapSet Map(InstrumentProfileCatalog? catalog = null)
    {
        catalog ??= InstrumentProfileCatalogLoader.Current;
        var articulations = InstrumentArticulationMapper.Map(catalog);
        var assignments = catalog.Instruments.Select(instrument =>
        {
            var swell = articulations.Maps
                .Single(item => string.Equals(item.InstrumentId, instrument.Id, StringComparison.Ordinal))
                .Mappings.Single(item => item.Gesture == NeutralPerformanceGesture.Swell);
            if (!swell.Applicable || swell.Articulation is null)
            {
                return new InstrumentMidiControllerAssignment(
                    instrument.Id, instrument.Name, false, null, null, null);
            }

            var (name, number) = ControllerFor(swell.Articulation.Value);
            return new InstrumentMidiControllerAssignment(
                instrument.Id, instrument.Name, true, swell.Articulation, name, number);
        }).ToList();
        return new InstrumentMidiControllerMapSet(assignments);
    }

    private static (string Name, int Number) ControllerFor(InstrumentArticulation articulation) =>
        articulation switch
        {
            InstrumentArticulation.Breath => (BreathControllerName, BreathControllerNumber),
            InstrumentArticulation.Filter => (BrightnessControllerName, BrightnessControllerNumber),
            _ => (ExpressionControllerName, ExpressionControllerNumber)
        };
}
