using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record InstrumentPerformanceEvent(
    PerformanceObservationGestureId GestureId,
    PerformanceObservationId ObservationId,
    long StartTick,
    long DurationTicks,
    RegisteredPitch? Pitch,
    int? Value,
    RangeCollisionKind? RangeKind);

public sealed record InstrumentGesturePerformance(
    NeutralPerformanceGesture Gesture,
    bool Applicable,
    InstrumentArticulation? Articulation,
    IReadOnlyList<InstrumentPerformanceEvent> Events);

/// <summary>
/// A transient, inspectable projection of one original-vocal take onto one
/// catalog instrument. It does not assign an instrument or change the Song Graph.
/// </summary>
public sealed record InstrumentPerformanceSketch(
    string InstrumentId,
    string InstrumentName,
    InstrumentGesturePerformance Swell,
    InstrumentGesturePerformance Slide,
    InstrumentGesturePerformance Hit);

public sealed record InstrumentPerformanceRetargetSet(
    ProjectAssetId SourceAssetId,
    long StartTick,
    IReadOnlyList<InstrumentPerformanceSketch> Targets);

/// <summary>
/// Adapts approved pitch, loudness, and onset gestures onto the current catalog
/// using the host articulation map. Piano, bass, flute, clarinet, trumpet, and
/// synth-pad slides stay unused. Drum-kit swell and slide stay unused. Pitched
/// instruments do not take kit hits. Kit hits use General MIDI Acoustic Bass
/// Drum rather than a melodic C4. Synth-pad swell is pad, not cello bow.
/// </summary>
public static class InstrumentPerformanceRetargeter
{
    public const string CelloInstrumentId = "cello";
    public const string AcousticGuitarInstrumentId = "acoustic-guitar";
    public const string PianoInstrumentId = "piano";
    public const string ElectricBassInstrumentId = "electric-bass";
    public const string DrumKitInstrumentId = "drum-kit";
    public const string FrequencyMeasurementName = PitchGestureNoteSketcher.FrequencyMeasurementName;
    public const string RmsMeasurementName = LoudnessGestureExpressionSketcher.RmsMeasurementName;
    public const string LoudnessObservationKind = LoudnessGestureExpressionSketcher.LoudnessObservationKind;
    public const string StrengthMeasurementName = OnsetGestureNoteSketcher.StrengthMeasurementName;
    public const string OnsetObservationKind = OnsetGestureNoteSketcher.OnsetObservationKind;
    private static readonly RegisteredPitch HitPitch = DrumKitGeneralMidiMapper.AcousticBassDrumPitch;
    private const decimal MinimumRmsDecibels = -120m;
    private const decimal MaximumRmsDecibels = 0m;
    private const decimal ExpressionFloorDecibels = -60m;
    private const int FallbackExpression = 96;
    private const int FallbackHitVelocity = 96;

    public static InstrumentPerformanceRetargetSet Project(
        SongProject project,
        ProjectAssetId sourceAssetId,
        InstrumentProfileCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Assets.All(asset => asset.Id != sourceAssetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new KeyNotFoundException($"Original vocal asset '{sourceAssetId}' was not found.");

        catalog ??= InstrumentProfileCatalogLoader.Current;
        var observations = project.PerformanceObservations
            .Where(item => item.SourceAssetId == sourceAssetId)
            .ToDictionary(item => item.Id);
        var loudnessObservations = observations.Values
            .Where(item => string.Equals(item.Kind, LoudnessObservationKind, StringComparison.Ordinal))
            .ToDictionary(item => item.Id);
        var onsetObservations = observations.Values
            .Where(item => string.Equals(item.Kind, OnsetObservationKind, StringComparison.Ordinal))
            .ToDictionary(item => item.Id);
        var beatsPerMinute = project.Tempo.BeatsPerMinute;
        var ticksPerQuarterNote = project.Timeline.TicksPerQuarterNote;
        var takeStartTick = project.VocalTakeStartTick(sourceAssetId);
        var maps = InstrumentArticulationMapper.Map(catalog)
            .Maps
            .ToDictionary(item => item.InstrumentId, StringComparer.Ordinal);
        var swellEvents = new List<InstrumentPerformanceEvent>();
        var slideEvents = new List<InstrumentPerformanceEvent>();
        var hitEvents = new List<InstrumentPerformanceEvent>();

        foreach (var gesture in project.PerformanceObservationGestures)
        {
            if (!observations.TryGetValue(gesture.ObservationId, out var observation)) continue;

            var frequency = gesture.Measurements.FirstOrDefault(item =>
                string.Equals(item.Name, FrequencyMeasurementName, StringComparison.OrdinalIgnoreCase));
            if (frequency is not null)
            {
                if (frequency.Value <= 0)
                    throw new ArgumentOutOfRangeException(
                        nameof(sourceAssetId),
                        "Pitch-gesture frequency must be greater than zero.");

                slideEvents.Add(new InstrumentPerformanceEvent(
                    gesture.Id,
                    observation.Id,
                    checked(takeStartTick + MillisecondsToTicks(observation.StartMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                    Math.Max(1, MillisecondsToTicks(observation.DurationMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                    FromFrequency(frequency.Value),
                    null,
                    null));
                continue;
            }

            if (onsetObservations.ContainsKey(observation.Id))
            {
                var strength = gesture.Measurements.FirstOrDefault(item =>
                    string.Equals(item.Name, StrengthMeasurementName, StringComparison.OrdinalIgnoreCase));
                if (strength is not null && strength.Value is < 0 or > 1)
                    throw new ArgumentOutOfRangeException(
                        nameof(sourceAssetId),
                        "Onset-gesture strength must be between 0 and 1.");

                hitEvents.Add(new InstrumentPerformanceEvent(
                    gesture.Id,
                    observation.Id,
                    checked(takeStartTick + MillisecondsToTicks(observation.StartMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                    Math.Max(1, MillisecondsToTicks(observation.DurationMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                    HitPitch,
                    strength is null ? FallbackHitVelocity : ToVelocity(strength.Value),
                    null));
                continue;
            }

            if (!loudnessObservations.ContainsKey(observation.Id)) continue;
            var rms = gesture.Measurements.FirstOrDefault(item =>
                string.Equals(item.Name, RmsMeasurementName, StringComparison.OrdinalIgnoreCase));
            if (rms is not null && rms.Value is < MinimumRmsDecibels or > MaximumRmsDecibels)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceAssetId),
                    "Loudness-gesture RMS must be between -120 and 0 dBFS.");

            swellEvents.Add(new InstrumentPerformanceEvent(
                gesture.Id,
                observation.Id,
                checked(takeStartTick + MillisecondsToTicks(observation.StartMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                Math.Max(1, MillisecondsToTicks(observation.DurationMilliseconds, beatsPerMinute, ticksPerQuarterNote)),
                null,
                rms is null ? FallbackExpression : ToExpression(rms.Value),
                null));
        }

        if (swellEvents.Count == 0 && slideEvents.Count == 0 && hitEvents.Count == 0)
            throw new InvalidOperationException(
                "Promote at least one pitch, loudness, or onset claim to a gesture before preparing an instrument retarget.");

        var orderedSwell = swellEvents
            .OrderBy(item => item.StartTick)
            .ThenBy(item => item.ObservationId.Value)
            .ToList();
        var orderedSlide = slideEvents
            .OrderBy(item => item.StartTick)
            .ThenBy(item => item.ObservationId.Value)
            .ToList();
        var orderedHit = hitEvents
            .OrderBy(item => item.StartTick)
            .ThenBy(item => item.ObservationId.Value)
            .ToList();

        var targets = catalog.Instruments.Select(profile =>
        {
            if (!maps.TryGetValue(profile.Id, out var map))
                throw new InvalidOperationException($"Articulation map for '{profile.Id}' was not found.");

            var swellMap = Lookup(map, NeutralPerformanceGesture.Swell);
            var slideMap = Lookup(map, NeutralPerformanceGesture.Slide);
            var hitMap = Lookup(map, NeutralPerformanceGesture.Hit);
            return new InstrumentPerformanceSketch(
                profile.Id,
                profile.Name,
                new InstrumentGesturePerformance(
                    NeutralPerformanceGesture.Swell,
                    swellMap.Applicable,
                    swellMap.Articulation,
                    swellMap.Applicable ? orderedSwell : []),
                new InstrumentGesturePerformance(
                    NeutralPerformanceGesture.Slide,
                    slideMap.Applicable,
                    slideMap.Articulation,
                    slideMap.Applicable
                        ? orderedSlide.Select(item => WithRange(item, profile)).ToList()
                        : []),
                new InstrumentGesturePerformance(
                    NeutralPerformanceGesture.Hit,
                    hitMap.Applicable,
                    hitMap.Articulation,
                    hitMap.Applicable ? orderedHit : []));
        }).ToList();

        return new InstrumentPerformanceRetargetSet(sourceAssetId, takeStartTick, targets);
    }

    private static InstrumentPerformanceEvent WithRange(InstrumentPerformanceEvent item, InstrumentProfile profile)
    {
        var pitch = item.Pitch ?? throw new InvalidOperationException("A slide event must include a pitch.");
        return item with { RangeKind = InstrumentRangeReviewer.Classify(profile, pitch) };
    }

    private static InstrumentArticulationMapping Lookup(InstrumentArticulationMap map, NeutralPerformanceGesture gesture) =>
        map.Mappings.Single(item => item.Gesture == gesture);

    private static int ToExpression(decimal rmsDecibels)
    {
        var scaled = (rmsDecibels - ExpressionFloorDecibels) / (MaximumRmsDecibels - ExpressionFloorDecibels);
        var value = (int)decimal.Round(scaled * 127m, MidpointRounding.AwayFromZero);
        return Math.Clamp(value, 0, 127);
    }

    private static int ToVelocity(decimal strength)
    {
        var velocity = (int)decimal.Round(strength * 127m, MidpointRounding.AwayFromZero);
        return Math.Clamp(velocity, 1, 127);
    }

    private static long MillisecondsToTicks(long milliseconds, decimal beatsPerMinute, int ticksPerQuarterNote)
    {
        var ticks = milliseconds * beatsPerMinute * ticksPerQuarterNote / 60_000m;
        return (long)decimal.Round(ticks, 0, MidpointRounding.AwayFromZero);
    }

    private static RegisteredPitch FromFrequency(decimal frequencyHertz)
    {
        var midiNumber = (int)decimal.Round(
            69m + 12m * (decimal)Math.Log2((double)frequencyHertz / 440d),
            MidpointRounding.AwayFromZero);
        return FromMidiNumber(Math.Clamp(midiNumber, 0, 127));
    }

    private static RegisteredPitch FromMidiNumber(int midiNumber)
    {
        var octave = midiNumber / 12 - 1;
        return (midiNumber % 12) switch
        {
            0 => new RegisteredPitch(NoteLetter.C, Accidental.Natural, octave),
            1 => new RegisteredPitch(NoteLetter.C, Accidental.Sharp, octave),
            2 => new RegisteredPitch(NoteLetter.D, Accidental.Natural, octave),
            3 => new RegisteredPitch(NoteLetter.D, Accidental.Sharp, octave),
            4 => new RegisteredPitch(NoteLetter.E, Accidental.Natural, octave),
            5 => new RegisteredPitch(NoteLetter.F, Accidental.Natural, octave),
            6 => new RegisteredPitch(NoteLetter.F, Accidental.Sharp, octave),
            7 => new RegisteredPitch(NoteLetter.G, Accidental.Natural, octave),
            8 => new RegisteredPitch(NoteLetter.G, Accidental.Sharp, octave),
            9 => new RegisteredPitch(NoteLetter.A, Accidental.Natural, octave),
            10 => new RegisteredPitch(NoteLetter.A, Accidental.Sharp, octave),
            _ => new RegisteredPitch(NoteLetter.B, Accidental.Natural, octave),
        };
    }
}
