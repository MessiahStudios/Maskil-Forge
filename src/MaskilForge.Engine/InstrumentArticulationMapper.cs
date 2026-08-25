using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// A neutral performance idea that later retargeters can adapt. It is not a
/// recorded gesture snapshot, MIDI event, or Song Graph assignment.
/// </summary>
public enum NeutralPerformanceGesture
{
    Swell,
    Slide,
    Hit
}

public sealed record InstrumentArticulationMapping(
    NeutralPerformanceGesture Gesture,
    bool Applicable,
    InstrumentArticulation? Articulation);

/// <summary>
/// A transient, inspectable map from neutral gestures onto one catalog
/// instrument's named articulations. It does not retarget, assign, or change
/// the Song Graph.
/// </summary>
public sealed record InstrumentArticulationMap(
    string InstrumentId,
    string InstrumentName,
    IReadOnlyList<InstrumentArticulationMapping> Mappings);

public sealed record InstrumentArticulationMapSet(IReadOnlyList<InstrumentArticulationMap> Maps);

public static class InstrumentArticulationMapper
{
    private static readonly NeutralPerformanceGesture[] Gestures = Enum.GetValues<NeutralPerformanceGesture>();

    private static readonly Dictionary<(string InstrumentId, NeutralPerformanceGesture Gesture), InstrumentArticulation> Known = new()
    {
        [("cello", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.BowExpression,
        [("cello", NeutralPerformanceGesture.Slide)] = InstrumentArticulation.Slide,
        [("acoustic-guitar", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.Picking,
        [("acoustic-guitar", NeutralPerformanceGesture.Slide)] = InstrumentArticulation.Bend,
        [("piano", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.Strike,
        [("electric-bass", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.Finger,
        [("drum-kit", NeutralPerformanceGesture.Hit)] = InstrumentArticulation.Hit,
        [("violin", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.BowExpression,
        [("violin", NeutralPerformanceGesture.Slide)] = InstrumentArticulation.Slide,
        [("flute", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.Breath,
        [("clarinet", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.Legato,
        [("trumpet", NeutralPerformanceGesture.Swell)] = InstrumentArticulation.Legato,
    };

    public static InstrumentArticulationMapSet Map(InstrumentProfileCatalog? catalog = null)
    {
        catalog ??= InstrumentProfileCatalogLoader.Current;
        var maps = catalog.Instruments.Select(MapInstrument).ToList();
        return new InstrumentArticulationMapSet(maps);
    }

    private static InstrumentArticulationMap MapInstrument(InstrumentProfile instrument)
    {
        var mappings = Gestures.Select(gesture => MapGesture(instrument, gesture)).ToList();
        return new InstrumentArticulationMap(instrument.Id, instrument.Name, mappings);
    }

    private static InstrumentArticulationMapping MapGesture(
        InstrumentProfile instrument,
        NeutralPerformanceGesture gesture)
    {
        if (!Known.TryGetValue((instrument.Id, gesture), out var articulation))
            return new InstrumentArticulationMapping(gesture, false, null);

        if (!instrument.Articulations.Contains(articulation))
        {
            throw new InvalidOperationException(
                $"Instrument '{instrument.Id}' cannot map {gesture} to {articulation} because that articulation is not on its catalog profile.");
        }

        return new InstrumentArticulationMapping(gesture, true, articulation);
    }
}
