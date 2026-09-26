using MaskilForge.Domain;

namespace MaskilForge.Engine;

public enum VocalMelodyRelation
{
    With,
    Above,
    Below
}

public sealed record VocalMelodyMoment(
    long StartMilliseconds,
    int SungMidi,
    int WrittenMidi,
    VocalMelodyRelation Relation,
    bool ArtistCorrected);

/// <summary>A transient comparison. It is not stored and does not change the take.</summary>
public sealed record VocalMelodyCheck(
    ProjectAssetId AssetId,
    int SungMoments,
    int WithMelody,
    int AboveMelody,
    int BelowMelody,
    int Unaligned,
    string Summary,
    IReadOnlyList<VocalMelodyMoment> Examples,
    int CorrectedMoments);

public static class VocalMelodyChecker
{
    public const string AnalyzerId = "maskil.browser.pitch-acf";
    public const string AnalyzerVersion = "1.0.0";
    public const string ObservationKind = "pitch.frame";
    public const string FrequencyMeasurementName = "frequencyHertz";

    public static VocalMelodyCheck Check(SongProject project, ProjectAssetId assetId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (project.Assets.All(asset => asset.Id != assetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new KeyNotFoundException($"Original vocal asset '{assetId}' was not found.");

        var frames = project.PerformanceObservations
            .Where(item => item.SourceAssetId == assetId
                && item.AnalyzerId == AnalyzerId
                && item.AnalyzerVersion == AnalyzerVersion
                && item.Kind == ObservationKind)
            .OrderBy(item => item.StartMilliseconds)
            .ThenBy(item => item.Id.Value)
            .ToList();
        if (frames.Count == 0)
            return new VocalMelodyCheck(assetId, 0, 0, 0, 0, 0,
                "Analyze pitch on this take first. The recording stays unchanged.", [], 0);
        if (project.NoteEvents.Count == 0)
            return new VocalMelodyCheck(assetId, frames.Count, 0, 0, 0, frames.Count,
                "Write the notes you want to sing. This check does not invent a melody.", [], 0);

        var takeStart = project.VocalTakeStartTick(assetId);
        var tempo = project.Tempo.BeatsPerMinute;
        var ticksPerQuarter = project.Timeline.TicksPerQuarterNote;
        var reviews = project.PerformanceObservationReviews.ToDictionary(item => item.ObservationId);
        var corrections = project.PerformanceObservationCorrections.ToDictionary(item => item.ObservationId);
        var with = 0;
        var above = 0;
        var below = 0;
        var unaligned = 0;
        var correctedMoments = 0;
        var awaitingCorrection = 0;
        var scored = new List<VocalMelodyMoment>();
        foreach (var frame in frames)
        {
            var artistCorrected = false;
            var frequency = frame.Measurements.FirstOrDefault(item =>
                string.Equals(item.Name, FrequencyMeasurementName, StringComparison.OrdinalIgnoreCase));
            if (reviews.TryGetValue(frame.Id, out var review)
                && review.Verdict == PerformanceObservationReviewVerdict.Inaccurate)
            {
                if (!corrections.TryGetValue(frame.Id, out var correction))
                {
                    awaitingCorrection++;
                    continue;
                }
                frequency = correction.Measurements.FirstOrDefault(item =>
                    string.Equals(item.Name, FrequencyMeasurementName, StringComparison.OrdinalIgnoreCase));
                artistCorrected = true;
            }
            if (frequency is null || frequency.Value <= 0) continue;
            var midpoint = frame.StartMilliseconds + frame.DurationMilliseconds / 2;
            var tick = checked(takeStart + MillisecondsToTicks(midpoint, tempo, ticksPerQuarter));
            var active = project.NoteEvents.Where(note => note.StartTick <= tick && tick < note.EndTickExclusive).ToList();
            if (active.Count == 0)
            {
                unaligned++;
                continue;
            }
            var sung = MidiFromFrequency(frequency.Value);
            var matched = active.FirstOrDefault(note => Math.Abs(sung - note.Pitch.MidiNumber) % 12 == 0);
            VocalMelodyRelation relation;
            int written;
            if (matched is not null)
            {
                relation = VocalMelodyRelation.With;
                written = matched.Pitch.MidiNumber;
                with++;
            }
            else
            {
                var nearest = active.OrderBy(note => Math.Abs(sung - note.Pitch.MidiNumber)).ThenBy(note => note.Pitch.MidiNumber).First();
                written = nearest.Pitch.MidiNumber;
                relation = sung > written ? VocalMelodyRelation.Above : VocalMelodyRelation.Below;
                if (relation == VocalMelodyRelation.Above) above++;
                else below++;
            }
            if (artistCorrected) correctedMoments++;
            scored.Add(new VocalMelodyMoment(frame.StartMilliseconds, sung, written, relation, artistCorrected));
        }

        var examples = scored.Where(item => item.Relation != VocalMelodyRelation.With).Take(6).ToList();
        if (examples.Count == 0) examples = scored.Take(3).ToList();
        return new VocalMelodyCheck(assetId, frames.Count, with, above, below, unaligned,
            Summarize(frames.Count, with, above, below, unaligned, correctedMoments, awaitingCorrection), examples, correctedMoments);
    }

    private static string Summarize(int sung, int with, int above, int below, int unaligned, int corrected, int awaitingCorrection)
    {
        var aligned = with + above + below;
        var alignedLabel = aligned == 1 ? "1 sung moment lines up" : $"{aligned} sung moments line up";
        var betweenLabel = unaligned == 1 ? "1 sung moment falls" : $"{unaligned} sung moments fall";
        var summary = $"{alignedLabel} with written notes. {with} {(with == 1 ? "sits" : "sit")} with those notes, {above} {(above == 1 ? "sits" : "sit")} above, and {below} {(below == 1 ? "sits" : "sit")} below. {betweenLabel} between the written notes.";
        if (corrected > 0)
            summary += corrected == 1
                ? " 1 sung moment uses a stored pitch correction. The original measurement stays on the take."
                : $" {corrected} sung moments use a stored pitch correction. The original measurements stay on the take.";
        if (awaitingCorrection > 0)
            summary += awaitingCorrection == 1
                ? " 1 reviewed pitch claim is waiting for a stored correction and was not scored."
                : $" {awaitingCorrection} reviewed pitch claims are waiting for a stored correction and were not scored.";
        return summary;
    }

    private static long MillisecondsToTicks(long milliseconds, decimal beatsPerMinute, int ticksPerQuarterNote)
    {
        var ticks = milliseconds * beatsPerMinute * ticksPerQuarterNote / 60_000m;
        return (long)decimal.Round(ticks, 0, MidpointRounding.AwayFromZero);
    }

    private static int MidiFromFrequency(decimal frequencyHertz)
    {
        var midi = (int)decimal.Round(69m + 12m * (decimal)Math.Log2((double)frequencyHertz / 440d), MidpointRounding.AwayFromZero);
        return Math.Clamp(midi, 0, 127);
    }
}
