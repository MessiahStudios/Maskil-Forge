using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MaskilForge.Domain;

public sealed class LyricSyllable
{
    [JsonConstructor]
    public LyricSyllable(SyllableId id, string text)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A syllable ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Syllable text is required.", nameof(text));
        if (text.Length > 100) throw new ArgumentOutOfRangeException(nameof(text), "Syllable text cannot exceed 100 characters.");
        Id = id;
        Text = text;
    }

    public SyllableId Id { get; }
    public string Text { get; }
}

public sealed class LyricWord
{
    private readonly List<LyricSyllable> _syllables;

    [JsonConstructor]
    public LyricWord(
        LyricWordId id,
        string text,
        int start,
        int length,
        IReadOnlyList<LyricSyllable>? syllables = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A lyric word ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Word text is required.", nameof(text));
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (length != text.Length) throw new ArgumentException("Word length must match its text.", nameof(length));
        Id = id;
        Text = text;
        Start = start;
        Length = length;
        _syllables = syllables?.ToList() ?? [];
        if (_syllables.Select(item => item.Id).Distinct().Count() != _syllables.Count)
            throw new ArgumentException("Syllable IDs must be unique within a word.", nameof(syllables));
    }

    public LyricWordId Id { get; }
    public string Text { get; }
    public int Start { get; }
    public int Length { get; }
    public IReadOnlyList<LyricSyllable> Syllables => _syllables;

    public void SetSyllables(IEnumerable<string> syllables)
    {
        ArgumentNullException.ThrowIfNull(syllables);
        var values = syllables.ToList();
        if (values.Count > 32) throw new ArgumentOutOfRangeException(nameof(syllables), "A word cannot contain more than 32 syllables.");
        var replacements = values.Select((text, index) =>
        {
            var existing = index < _syllables.Count && _syllables[index].Text == text ? _syllables[index] : null;
            return existing ?? new LyricSyllable(SyllableId.New(), text);
        }).ToList();
        _syllables.Clear();
        _syllables.AddRange(replacements);
    }
}

public sealed class LyricLine
{
    private static readonly Regex WordPattern = new(
        @"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly List<LyricWord> _words = [];

    [JsonConstructor]
    public LyricLine(LyricLineId id, string text, IReadOnlyList<LyricWord>? words = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A lyric line ID is required.", nameof(id));
        Id = id;
        SetText(text, words ?? []);
    }

    public LyricLineId Id { get; }
    public string Text { get; private set; } = string.Empty;
    public IReadOnlyList<LyricWord> Words => _words;

    public static LyricLine Create(string text = "") => new(LyricLineId.New(), text);

    public static IReadOnlyList<(string Text, int Start, int Length)> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WordPattern.Matches(text)
            .Select(match => (match.Value, match.Index, match.Length))
            .ToList();
    }

    public void SetText(string text) => SetText(text, _words);

    public void SetSyllables(LyricWordId wordId, IEnumerable<string> syllables)
    {
        var word = _words.SingleOrDefault(item => item.Id == wordId)
            ?? throw new KeyNotFoundException($"Lyric word '{wordId}' was not found.");
        word.SetSyllables(syllables);
    }

    private void SetText(string text, IReadOnlyList<LyricWord> existingWords)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > 2_000) throw new ArgumentOutOfRangeException(nameof(text), "A lyric line cannot exceed 2,000 characters.");
        if (existingWords.Select(item => item.Id).Distinct().Count() != existingWords.Count)
            throw new ArgumentException("Lyric word IDs must be unique within a line.", nameof(existingWords));
        var tokens = Tokenize(text);
        var matches = MatchExistingWords(existingWords, tokens);
        _words.Clear();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (matches.TryGetValue(index, out var existing))
                _words.Add(new LyricWord(existing.Id, token.Text, token.Start, token.Length, existing.Syllables));
            else
                _words.Add(new LyricWord(LyricWordId.New(), token.Text, token.Start, token.Length));
        }
        Text = text;
    }

    private static Dictionary<int, LyricWord> MatchExistingWords(
        IReadOnlyList<LyricWord> existing,
        IReadOnlyList<(string Text, int Start, int Length)> tokens)
    {
        var lengths = new int[existing.Count + 1, tokens.Count + 1];
        for (var oldIndex = existing.Count - 1; oldIndex >= 0; oldIndex--)
        for (var newIndex = tokens.Count - 1; newIndex >= 0; newIndex--)
            lengths[oldIndex, newIndex] = existing[oldIndex].Text == tokens[newIndex].Text
                ? lengths[oldIndex + 1, newIndex + 1] + 1
                : Math.Max(lengths[oldIndex + 1, newIndex], lengths[oldIndex, newIndex + 1]);

        var matches = new Dictionary<int, LyricWord>();
        var oldCursor = 0;
        var newCursor = 0;
        while (oldCursor < existing.Count && newCursor < tokens.Count)
        {
            if (existing[oldCursor].Text == tokens[newCursor].Text)
            {
                matches[newCursor] = existing[oldCursor];
                oldCursor++;
                newCursor++;
            }
            else if (lengths[oldCursor + 1, newCursor] >= lengths[oldCursor, newCursor + 1]) oldCursor++;
            else newCursor++;
        }
        return matches;
    }

    public static LyricWordId CreateMigratedWordId(LyricLineId lineId, int index, string text)
    {
        var input = Encoding.UTF8.GetBytes($"maskil-forge/lyric-word/v1/{lineId}/{index}/{text}");
        var hash = SHA256.HashData(input);
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new LyricWordId(new Guid(bytes));
    }
}

public sealed record LyricDocumentLine(SectionId SectionId, LyricLine Line);

public sealed class LyricDocument
{
    public LyricDocument(string rawDraft, IReadOnlyList<LyricDocumentLine> lines)
    {
        RawDraft = rawDraft ?? throw new ArgumentNullException(nameof(rawDraft));
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
    }

    public string RawDraft { get; }
    public IReadOnlyList<LyricDocumentLine> Lines { get; }
}
