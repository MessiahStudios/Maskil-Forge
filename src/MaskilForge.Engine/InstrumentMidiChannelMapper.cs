using MaskilForge.Domain;

namespace MaskilForge.Engine;

/// <summary>
/// An inspectable MIDI channel for one catalog instrument. Channel numbers are
/// musician-facing 1-16. The map does not emit program changes.
/// </summary>
public sealed record InstrumentMidiChannelAssignment(
    string InstrumentId,
    string InstrumentName,
    int MidiChannel);

public sealed record InstrumentMidiChannelMapSet(
    int UnassignedMidiChannel,
    IReadOnlyList<InstrumentMidiChannelAssignment> Assignments);

public static class InstrumentMidiChannelMapper
{
    public const int UnassignedMidiChannel = 1;
    public const int DrumKitMidiChannel = 10;
    private const int FirstPitchedMidiChannel = 2;
    private const int LastMidiChannel = 16;

    public static InstrumentMidiChannelMapSet Map(InstrumentProfileCatalog? catalog = null)
    {
        catalog ??= InstrumentProfileCatalogLoader.Current;
        var nextPitched = FirstPitchedMidiChannel;
        var assignments = new List<InstrumentMidiChannelAssignment>(catalog.Instruments.Count);
        foreach (var instrument in catalog.Instruments)
        {
            int channel;
            if (string.Equals(instrument.Id, DrumKitGeneralMidiMapper.DrumKitInstrumentId, StringComparison.Ordinal))
            {
                channel = DrumKitMidiChannel;
            }
            else
            {
                if (nextPitched == DrumKitMidiChannel) nextPitched++;
                if (nextPitched > LastMidiChannel)
                {
                    throw new InvalidOperationException(
                        "The catalog has more pitched instruments than available MIDI channels.");
                }

                channel = nextPitched++;
            }

            assignments.Add(new InstrumentMidiChannelAssignment(instrument.Id, instrument.Name, channel));
        }

        return new InstrumentMidiChannelMapSet(UnassignedMidiChannel, assignments);
    }

    public static byte ZeroBasedChannel(int midiChannel) => checked((byte)(midiChannel - 1));
}
