namespace MaskilForge.Domain;

public sealed record TempoEvent
{
    public TempoEvent(int beat, decimal beatsPerMinute)
    {
        if (beat < 0) throw new ArgumentOutOfRangeException(nameof(beat), "Beat cannot be negative.");
        if (beatsPerMinute is < 20 or > 300) throw new ArgumentOutOfRangeException(nameof(beatsPerMinute), "Tempo must be between 20 and 300 BPM.");
        Beat = beat;
        BeatsPerMinute = beatsPerMinute;
    }

    public int Beat { get; }
    public decimal BeatsPerMinute { get; }
}

public sealed record TimeSignatureEvent
{
    private static readonly int[] ValidDenominators = [1, 2, 4, 8, 16, 32];

    public TimeSignatureEvent(int beat, int numerator, int denominator)
    {
        if (beat < 0) throw new ArgumentOutOfRangeException(nameof(beat), "Beat cannot be negative.");
        if (numerator is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(numerator), "Numerator must be between 1 and 32.");
        if (!ValidDenominators.Contains(denominator)) throw new ArgumentOutOfRangeException(nameof(denominator), "Denominator must be 1, 2, 4, 8, 16, or 32.");
        Beat = beat;
        Numerator = numerator;
        Denominator = denominator;
    }

    public int Beat { get; }
    public int Numerator { get; }
    public int Denominator { get; }
}
