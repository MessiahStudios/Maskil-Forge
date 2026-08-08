using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MaskilForge.Domain;

public enum SyllableSource
{
    Manual,
    Analyzer,
    Imported
}

public enum PhraseSource
{
    Default,
    Manual,
    Analyzer,
    Imported
}

public sealed class LyricSyllable
{
    [JsonConstructor]
    public LyricSyllable(SyllableId id, string text, int position, SyllableSource source)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A syllable ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Syllable text is required.", nameof(text));
        if (text.Length > 100) throw new ArgumentOutOfRangeException(nameof(text), "Syllable text cannot exceed 100 characters.");
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position), "Syllable position cannot be negative.");
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source), "Syllable source is invalid.");
        Id = id;
        Text = text;
        Position = position;
        Source = source;
    }

    public SyllableId Id { get; }
    public string Text { get; }
    public int Position { get; }
    public SyllableSource Source { get; }
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
        if (_syllables.Count > 32)
            throw new ArgumentOutOfRangeException(nameof(syllables), "A word cannot contain more than 32 syllables.");
        if (_syllables.Select(item => item.Id).Distinct().Count() != _syllables.Count)
            throw new ArgumentException("Syllable IDs must be unique within a word.", nameof(syllables));
        if (_syllables.Where((item, index) => item.Position != index).Any())
            throw new ArgumentException("Syllable positions must be contiguous and ordered from zero.", nameof(syllables));
    }

    public LyricWordId Id { get; }
    public string Text { get; }
    public int Start { get; }
    public int Length { get; }
    public IReadOnlyList<LyricSyllable> Syllables => _syllables;

    public void SetSyllables(IEnumerable<string> syllables, SyllableSource source = SyllableSource.Manual)
    {
        ArgumentNullException.ThrowIfNull(syllables);
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source), "Syllable source is invalid.");
        var values = syllables.ToList();
        if (values.Count > 32) throw new ArgumentOutOfRangeException(nameof(syllables), "A word cannot contain more than 32 syllables.");
        var matches = MatchExistingSyllables(_syllables, values);
        var replacements = values.Select((text, position) =>
        {
            var id = matches.TryGetValue(position, out var existing) ? existing.Id : SyllableId.New();
            return new LyricSyllable(id, text, position, source);
        }).ToList();
        _syllables.Clear();
        _syllables.AddRange(replacements);
    }

    private static Dictionary<int, LyricSyllable> MatchExistingSyllables(
        IReadOnlyList<LyricSyllable> existing,
        IReadOnlyList<string> replacements)
    {
        var lengths = new int[existing.Count + 1, replacements.Count + 1];
        for (var oldIndex = existing.Count - 1; oldIndex >= 0; oldIndex--)
        for (var newIndex = replacements.Count - 1; newIndex >= 0; newIndex--)
            lengths[oldIndex, newIndex] = existing[oldIndex].Text == replacements[newIndex]
                ? lengths[oldIndex + 1, newIndex + 1] + 1
                : Math.Max(lengths[oldIndex + 1, newIndex], lengths[oldIndex, newIndex + 1]);

        var matches = new Dictionary<int, LyricSyllable>();
        var oldCursor = 0;
        var newCursor = 0;
        while (oldCursor < existing.Count && newCursor < replacements.Count)
        {
            if (existing[oldCursor].Text == replacements[newCursor])
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
}

public sealed class LyricPunctuation
{
    [JsonConstructor]
    public LyricPunctuation(PunctuationId id, string text, int start, int length)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A punctuation ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Punctuation text is required.", nameof(text));
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (length != text.Length) throw new ArgumentException("Punctuation length must match its text.", nameof(length));
        Id = id;
        Text = text;
        Start = start;
        Length = length;
    }

    public PunctuationId Id { get; }
    public string Text { get; }
    public int Start { get; }
    public int Length { get; }
}

public sealed class LyricPhrase
{
    private readonly List<LyricWordId> _wordIds;

    [JsonConstructor]
    public LyricPhrase(
        LyricPhraseId id,
        int position,
        IReadOnlyList<LyricWordId> wordIds,
        PhraseSource source)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A lyric phrase ID is required.", nameof(id));
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position), "Phrase position cannot be negative.");
        ArgumentNullException.ThrowIfNull(wordIds);
        if (wordIds.Count == 0) throw new ArgumentException("A phrase must contain at least one word.", nameof(wordIds));
        if (wordIds.Any(item => item.Value == Guid.Empty)) throw new ArgumentException("Phrase word IDs are required.", nameof(wordIds));
        if (wordIds.Distinct().Count() != wordIds.Count) throw new ArgumentException("Phrase word IDs must be unique.", nameof(wordIds));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source), "Phrase source is invalid.");
        Id = id;
        Position = position;
        _wordIds = wordIds.ToList();
        Source = source;
    }

    public LyricPhraseId Id { get; }
    public int Position { get; }
    public IReadOnlyList<LyricWordId> WordIds => _wordIds;
    public PhraseSource Source { get; }
}

public sealed class LyricLine
{
    private static readonly Regex WordPattern = new(
        @"[\p{L}\p{N}]+(?:['’\-][\p{L}\p{N}]+)*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PunctuationPattern = new(
        @"[\p{P}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly List<LyricWord> _words = [];
    private readonly List<LyricPunctuation> _punctuation = [];
    private readonly List<LyricPhrase> _phrases = [];

    [JsonConstructor]
    public LyricLine(
        LyricLineId id,
        string text,
        IReadOnlyList<LyricWord>? words = null,
        IReadOnlyList<LyricPunctuation>? punctuation = null,
        IReadOnlyList<LyricPhrase>? phrases = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A lyric line ID is required.", nameof(id));
        ValidateSerializedPhraseCoverage(words ?? [], phrases ?? []);
        Id = id;
        SetText(text, words ?? [], punctuation ?? [], phrases ?? []);
    }

    public LyricLineId Id { get; }
    public string Text { get; private set; } = string.Empty;
    public IReadOnlyList<LyricWord> Words => _words;
    public IReadOnlyList<LyricPunctuation> Punctuation => _punctuation;
    public IReadOnlyList<LyricPhrase> Phrases => _phrases;

    public static LyricLine Create(string text = "") => new(LyricLineId.New(), text);

    public static IReadOnlyList<(string Text, int Start, int Length)> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WordPattern.Matches(text)
            .Select(match => (match.Value, match.Index, match.Length))
            .ToList();
    }

    public static IReadOnlyList<(string Text, int Start, int Length)> TokenizePunctuation(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = Tokenize(text);
        return PunctuationPattern.Matches(text)
            .Where(match => !words.Any(word => match.Index < word.Start + word.Length && match.Index + match.Length > word.Start))
            .Select(match => (match.Value, match.Index, match.Length))
            .ToList();
    }

    public void SetText(string text) => SetText(text, _words.ToList(), _punctuation.ToList(), _phrases.ToList());

    public void SetSyllables(
        LyricWordId wordId,
        IEnumerable<string> syllables,
        SyllableSource source = SyllableSource.Manual)
    {
        var word = _words.SingleOrDefault(item => item.Id == wordId)
            ?? throw new KeyNotFoundException($"Lyric word '{wordId}' was not found.");
        word.SetSyllables(syllables, source);
    }

    public void SplitPhraseAfter(LyricWordId wordId)
    {
        var phraseIndex = _phrases.FindIndex(item => item.WordIds.Contains(wordId));
        if (phraseIndex < 0) throw new KeyNotFoundException($"Lyric word '{wordId}' does not belong to a phrase.");
        var phrase = _phrases[phraseIndex];
        var splitIndex = phrase.WordIds.ToList().IndexOf(wordId);
        if (splitIndex == phrase.WordIds.Count - 1)
            throw new InvalidOperationException("A phrase cannot be split after its final word.");

        var left = phrase.WordIds.Take(splitIndex + 1).ToList();
        var right = phrase.WordIds.Skip(splitIndex + 1).ToList();
        _phrases[phraseIndex] = new LyricPhrase(phrase.Id, phraseIndex, left, PhraseSource.Manual);
        _phrases.Insert(phraseIndex + 1, new LyricPhrase(LyricPhraseId.New(), phraseIndex + 1, right, PhraseSource.Manual));
        NormalizePhrasePositions();
    }

    public void JoinPhraseWithPrevious(LyricPhraseId phraseId)
    {
        var phraseIndex = _phrases.FindIndex(item => item.Id == phraseId);
        if (phraseIndex < 0) throw new KeyNotFoundException($"Lyric phrase '{phraseId}' was not found.");
        if (phraseIndex == 0) throw new InvalidOperationException("The first phrase has no previous phrase to join.");
        var previous = _phrases[phraseIndex - 1];
        var current = _phrases[phraseIndex];
        _phrases[phraseIndex - 1] = new LyricPhrase(
            previous.Id,
            phraseIndex - 1,
            previous.WordIds.Concat(current.WordIds).ToList(),
            PhraseSource.Manual);
        _phrases.RemoveAt(phraseIndex);
        NormalizePhrasePositions();
    }

    public void RestorePhrases(IReadOnlyList<LyricPhrase> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        if (_words.Count > 0 && phrases.Count == 0)
            throw new ArgumentException("A lyric line with words must have at least one phrase.", nameof(phrases));
        ValidateSerializedPhraseCoverage(_words, phrases);
        _phrases.Clear();
        _phrases.AddRange(phrases.Select(ClonePhrase));
    }

    private void SetText(
        string text,
        IReadOnlyList<LyricWord> existingWords,
        IReadOnlyList<LyricPunctuation> existingPunctuation,
        IReadOnlyList<LyricPhrase> existingPhrases)
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
        var punctuationTokens = TokenizePunctuation(text);
        var punctuationMatches = MatchExistingPunctuation(existingPunctuation, punctuationTokens);
        _punctuation.Clear();
        for (var index = 0; index < punctuationTokens.Count; index++)
        {
            var token = punctuationTokens[index];
            var id = punctuationMatches.TryGetValue(index, out var existing)
                ? existing.Id
                : PunctuationId.New();
            _punctuation.Add(new LyricPunctuation(id, token.Text, token.Start, token.Length));
        }
        ReconcilePhrases(existingPhrases);
        Text = text;
    }

    private void ReconcilePhrases(IReadOnlyList<LyricPhrase> existingPhrases)
    {
        _phrases.Clear();
        if (_words.Count == 0) return;
        var currentWordIds = _words.Select(item => item.Id).ToHashSet();
        foreach (var existing in existingPhrases)
        {
            var surviving = existing.WordIds.Where(currentWordIds.Contains).ToList();
            if (surviving.Count > 0)
                _phrases.Add(new LyricPhrase(existing.Id, _phrases.Count, surviving, existing.Source));
        }

        if (_phrases.Count == 0)
        {
            _phrases.Add(new LyricPhrase(LyricPhraseId.New(), 0, _words.Select(item => item.Id).ToList(), PhraseSource.Default));
            return;
        }

        var phraseByWord = _phrases
            .SelectMany(phrase => phrase.WordIds.Select(wordId => (wordId, phrase.Id)))
            .ToDictionary(item => item.wordId, item => item.Id);
        var rebuilt = _phrases.ToDictionary(item => item.Id, item => item.WordIds.ToList());
        for (var index = 0; index < _words.Count; index++)
        {
            var wordId = _words[index].Id;
            if (phraseByWord.ContainsKey(wordId)) continue;
            var previousPhrase = index > 0 && phraseByWord.TryGetValue(_words[index - 1].Id, out var previousId)
                ? previousId
                : default;
            var nextPhrase = index + 1 < _words.Count && phraseByWord.TryGetValue(_words[index + 1].Id, out var nextId)
                ? nextId
                : default;
            var target = previousPhrase.Value != Guid.Empty ? previousPhrase : nextPhrase;
            if (target.Value == Guid.Empty) target = _phrases[0].Id;
            rebuilt[target].Add(wordId);
            phraseByWord[wordId] = target;
        }

        var wordOrder = _words.Select((word, index) => (word.Id, index)).ToDictionary(item => item.Id, item => item.index);
        _phrases.Clear();
        foreach (var phrase in existingPhrases.Where(item => rebuilt.ContainsKey(item.Id)))
        {
            var ordered = rebuilt[phrase.Id].OrderBy(id => wordOrder[id]).ToList();
            _phrases.Add(new LyricPhrase(phrase.Id, _phrases.Count, ordered, phrase.Source));
        }
        ValidatePhraseCoverage();
    }

    private void NormalizePhrasePositions()
    {
        var normalized = _phrases
            .Select((phrase, position) => new LyricPhrase(phrase.Id, position, phrase.WordIds, phrase.Source))
            .ToList();
        _phrases.Clear();
        _phrases.AddRange(normalized);
        ValidatePhraseCoverage();
    }

    private static LyricPhrase ClonePhrase(LyricPhrase phrase) =>
        new(phrase.Id, phrase.Position, phrase.WordIds.ToList(), phrase.Source);

    private void ValidatePhraseCoverage()
    {
        if (_phrases.Select(item => item.Id).Distinct().Count() != _phrases.Count)
            throw new ArgumentException("Lyric phrase IDs must be unique within a line.");
        if (_phrases.Where((item, index) => item.Position != index).Any())
            throw new ArgumentException("Phrase positions must be contiguous and ordered from zero.");
        var phraseWords = _phrases.SelectMany(item => item.WordIds).ToList();
        if (!phraseWords.SequenceEqual(_words.Select(item => item.Id)))
            throw new ArgumentException("Phrases must contain every lyric word exactly once and in line order.");
    }

    private static void ValidateSerializedPhraseCoverage(
        IReadOnlyList<LyricWord> words,
        IReadOnlyList<LyricPhrase> phrases)
    {
        if (phrases.Count == 0) return;
        if (phrases.Select(item => item.Id).Distinct().Count() != phrases.Count)
            throw new ArgumentException("Lyric phrase IDs must be unique within a line.", nameof(phrases));
        if (phrases.Where((item, index) => item.Position != index).Any())
            throw new ArgumentException("Phrase positions must be contiguous and ordered from zero.", nameof(phrases));
        if (!phrases.SelectMany(item => item.WordIds).SequenceEqual(words.Select(item => item.Id)))
            throw new ArgumentException("Serialized phrases must reference every lyric word exactly once and in line order.", nameof(phrases));
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

    private static Dictionary<int, LyricPunctuation> MatchExistingPunctuation(
        IReadOnlyList<LyricPunctuation> existing,
        IReadOnlyList<(string Text, int Start, int Length)> tokens)
    {
        var lengths = new int[existing.Count + 1, tokens.Count + 1];
        for (var oldIndex = existing.Count - 1; oldIndex >= 0; oldIndex--)
        for (var newIndex = tokens.Count - 1; newIndex >= 0; newIndex--)
            lengths[oldIndex, newIndex] = existing[oldIndex].Text == tokens[newIndex].Text
                ? lengths[oldIndex + 1, newIndex + 1] + 1
                : Math.Max(lengths[oldIndex + 1, newIndex], lengths[oldIndex, newIndex + 1]);

        var matches = new Dictionary<int, LyricPunctuation>();
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

    public static PunctuationId CreateMigratedPunctuationId(LyricLineId lineId, int index, string text) =>
        new(CreateDeterministicGuid($"maskil-forge/lyric-punctuation/v1/{lineId}/{index}/{text}"));

    public static LyricPhraseId CreateMigratedPhraseId(LyricLineId lineId, int index) =>
        new(CreateDeterministicGuid($"maskil-forge/lyric-phrase/v1/{lineId}/{index}"));

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
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
