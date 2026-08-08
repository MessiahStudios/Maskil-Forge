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

public enum StressLevel
{
    None,
    Secondary,
    Primary,
    Emphasized
}

public enum StressProvenance
{
    Manual,
    Analyzer,
    Imported
}

public sealed class StressMark
{
    [JsonConstructor]
    public StressMark(StressLevel level, StressProvenance provenance)
    {
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level), "Stress level is invalid.");
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Stress provenance is invalid.");
        Level = level;
        Provenance = provenance;
    }

    public StressLevel Level { get; }
    public StressProvenance Provenance { get; }
}

public sealed class LyricSyllable
{
    [JsonConstructor]
    public LyricSyllable(
        SyllableId id,
        string text,
        int position,
        SyllableSource source,
        StressMark? stress = null)
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
        Stress = stress;
    }

    public SyllableId Id { get; }
    public string Text { get; }
    public int Position { get; }
    public SyllableSource Source { get; }
    public StressMark? Stress { get; }
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
            return new LyricSyllable(id, text, position, source, existing?.Stress);
        }).ToList();
        _syllables.Clear();
        _syllables.AddRange(replacements);
    }

    public void SetStress(
        SyllableId syllableId,
        StressLevel? level,
        StressProvenance provenance = StressProvenance.Manual)
    {
        if (level is not null && !Enum.IsDefined(level.Value))
            throw new ArgumentOutOfRangeException(nameof(level), "Stress level is invalid.");
        if (!Enum.IsDefined(provenance))
            throw new ArgumentOutOfRangeException(nameof(provenance), "Stress provenance is invalid.");
        var index = _syllables.FindIndex(item => item.Id == syllableId);
        if (index < 0) throw new KeyNotFoundException($"Syllable '{syllableId}' was not found.");
        var syllable = _syllables[index];
        var stress = level is null ? null : new StressMark(level.Value, provenance);
        _syllables[index] = new LyricSyllable(
            syllable.Id,
            syllable.Text,
            syllable.Position,
            syllable.Source,
            stress);
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

public enum ProsodicWeight
{
    Weak,
    Neutral,
    Strong
}

public enum ProsodyProvenance
{
    Manual,
    Analyzer,
    Imported
}

public sealed class ProsodicUnit
{
    [JsonConstructor]
    public ProsodicUnit(
        ProsodicUnitId id,
        SyllableId syllableId,
        int position,
        ProsodicWeight weight,
        ProsodyProvenance provenance)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A prosodic unit ID is required.", nameof(id));
        if (syllableId.Value == Guid.Empty) throw new ArgumentException("A syllable ID is required.", nameof(syllableId));
        if (position < 0) throw new ArgumentOutOfRangeException(nameof(position), "Prosodic unit position cannot be negative.");
        if (!Enum.IsDefined(weight)) throw new ArgumentOutOfRangeException(nameof(weight), "Prosodic weight is invalid.");
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance), "Prosody provenance is invalid.");
        Id = id;
        SyllableId = syllableId;
        Position = position;
        Weight = weight;
        Provenance = provenance;
    }

    public ProsodicUnitId Id { get; }
    public SyllableId SyllableId { get; }
    public int Position { get; }
    public ProsodicWeight Weight { get; }
    public ProsodyProvenance Provenance { get; }
}

public sealed class ProsodicPattern
{
    private readonly List<ProsodicUnit> _units;

    [JsonConstructor]
    public ProsodicPattern(ProsodicPatternId id, IReadOnlyList<ProsodicUnit> units)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A prosodic pattern ID is required.", nameof(id));
        ArgumentNullException.ThrowIfNull(units);
        if (units.Count == 0) throw new ArgumentException("A prosodic pattern must contain at least one unit.", nameof(units));
        if (units.Select(item => item.Id).Distinct().Count() != units.Count)
            throw new ArgumentException("Prosodic unit IDs must be unique within a pattern.", nameof(units));
        if (units.Select(item => item.SyllableId).Distinct().Count() != units.Count)
            throw new ArgumentException("A syllable can appear only once within a prosodic pattern.", nameof(units));
        if (units.Where((item, index) => item.Position != index).Any())
            throw new ArgumentException("Prosodic unit positions must be contiguous and ordered from zero.", nameof(units));
        Id = id;
        _units = units.ToList();
    }

    public ProsodicPatternId Id { get; }
    public IReadOnlyList<ProsodicUnit> Units => _units;
}

public sealed class LyricPhrase
{
    private readonly List<LyricWordId> _wordIds;

    [JsonConstructor]
    public LyricPhrase(
        LyricPhraseId id,
        int position,
        IReadOnlyList<LyricWordId> wordIds,
        PhraseSource source,
        ProsodicPattern? prosody = null)
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
        Prosody = prosody;
    }

    public LyricPhraseId Id { get; }
    public int Position { get; }
    public IReadOnlyList<LyricWordId> WordIds => _wordIds;
    public PhraseSource Source { get; }
    public ProsodicPattern? Prosody { get; }
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
        ReconcileAllPhraseProsody();
    }

    public void SetStress(
        LyricWordId wordId,
        SyllableId syllableId,
        StressLevel? level,
        StressProvenance provenance = StressProvenance.Manual)
    {
        var word = _words.SingleOrDefault(item => item.Id == wordId)
            ?? throw new KeyNotFoundException($"Lyric word '{wordId}' was not found.");
        word.SetStress(syllableId, level, provenance);
    }

    public void SetProsodicWeight(
        LyricPhraseId phraseId,
        SyllableId syllableId,
        ProsodicWeight? weight,
        ProsodyProvenance provenance = ProsodyProvenance.Manual)
    {
        if (weight is not null && !Enum.IsDefined(weight.Value))
            throw new ArgumentOutOfRangeException(nameof(weight), "Prosodic weight is invalid.");
        if (!Enum.IsDefined(provenance))
            throw new ArgumentOutOfRangeException(nameof(provenance), "Prosody provenance is invalid.");
        var phraseIndex = _phrases.FindIndex(item => item.Id == phraseId);
        if (phraseIndex < 0) throw new KeyNotFoundException($"Lyric phrase '{phraseId}' was not found.");
        var phrase = _phrases[phraseIndex];
        var orderedSyllableIds = SyllableIdsForWords(phrase.WordIds);
        if (!orderedSyllableIds.Contains(syllableId))
            throw new InvalidOperationException($"Syllable '{syllableId}' does not belong to phrase '{phraseId}'.");

        var units = phrase.Prosody?.Units.ToList() ?? [];
        var existingIndex = units.FindIndex(item => item.SyllableId == syllableId);
        if (weight is null)
        {
            if (existingIndex >= 0) units.RemoveAt(existingIndex);
        }
        else if (existingIndex >= 0)
        {
            var existing = units[existingIndex];
            units[existingIndex] = new ProsodicUnit(
                existing.Id,
                syllableId,
                existing.Position,
                weight.Value,
                provenance);
        }
        else
        {
            units.Add(new ProsodicUnit(
                ProsodicUnitId.New(),
                syllableId,
                units.Count,
                weight.Value,
                provenance));
        }

        var prosodyId = phrase.Prosody?.Id ?? ProsodicPatternId.New();
        var prosody = BuildProsody(prosodyId, units, orderedSyllableIds);
        _phrases[phraseIndex] = new LyricPhrase(
            phrase.Id,
            phrase.Position,
            phrase.WordIds,
            phrase.Source,
            prosody);
        ValidatePhraseCoverage();
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
        var leftProsody = FilterProsody(phrase.Prosody, left);
        var rightProsody = FilterProsody(
            phrase.Prosody,
            right,
            leftProsody is null ? phrase.Prosody?.Id : ProsodicPatternId.New());
        _phrases[phraseIndex] = new LyricPhrase(phrase.Id, phraseIndex, left, PhraseSource.Manual, leftProsody);
        _phrases.Insert(phraseIndex + 1, new LyricPhrase(
            LyricPhraseId.New(),
            phraseIndex + 1,
            right,
            PhraseSource.Manual,
            rightProsody));
        NormalizePhrasePositions();
    }

    public void JoinPhraseWithPrevious(LyricPhraseId phraseId)
    {
        var phraseIndex = _phrases.FindIndex(item => item.Id == phraseId);
        if (phraseIndex < 0) throw new KeyNotFoundException($"Lyric phrase '{phraseId}' was not found.");
        if (phraseIndex == 0) throw new InvalidOperationException("The first phrase has no previous phrase to join.");
        var previous = _phrases[phraseIndex - 1];
        var current = _phrases[phraseIndex];
        var joinedWordIds = previous.WordIds.Concat(current.WordIds).ToList();
        var joinedProsody = CombineProsody(previous.Prosody, current.Prosody, joinedWordIds);
        _phrases[phraseIndex - 1] = new LyricPhrase(
            previous.Id,
            phraseIndex - 1,
            joinedWordIds,
            PhraseSource.Manual,
            joinedProsody);
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
                _phrases.Add(new LyricPhrase(
                    existing.Id,
                    _phrases.Count,
                    surviving,
                    existing.Source,
                    FilterProsody(existing.Prosody, surviving)));
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
            _phrases.Add(new LyricPhrase(
                phrase.Id,
                _phrases.Count,
                ordered,
                phrase.Source,
                FilterProsody(phrase.Prosody, ordered)));
        }
        ValidatePhraseCoverage();
    }

    private void NormalizePhrasePositions()
    {
        var normalized = _phrases
            .Select((phrase, position) => new LyricPhrase(
                phrase.Id,
                position,
                phrase.WordIds,
                phrase.Source,
                CloneProsody(phrase.Prosody)))
            .ToList();
        _phrases.Clear();
        _phrases.AddRange(normalized);
        ValidatePhraseCoverage();
    }

    private static LyricPhrase ClonePhrase(LyricPhrase phrase) =>
        new(phrase.Id, phrase.Position, phrase.WordIds.ToList(), phrase.Source, CloneProsody(phrase.Prosody));

    private void ValidatePhraseCoverage()
    {
        if (_phrases.Select(item => item.Id).Distinct().Count() != _phrases.Count)
            throw new ArgumentException("Lyric phrase IDs must be unique within a line.");
        if (_phrases.Where((item, index) => item.Position != index).Any())
            throw new ArgumentException("Phrase positions must be contiguous and ordered from zero.");
        var phraseWords = _phrases.SelectMany(item => item.WordIds).ToList();
        if (!phraseWords.SequenceEqual(_words.Select(item => item.Id)))
            throw new ArgumentException("Phrases must contain every lyric word exactly once and in line order.");
        ValidateProsodyReferences(_words, _phrases);
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
        ValidateProsodyReferences(words, phrases);
    }

    private void ReconcileAllPhraseProsody()
    {
        var reconciled = _phrases.Select(phrase => new LyricPhrase(
            phrase.Id,
            phrase.Position,
            phrase.WordIds,
            phrase.Source,
            FilterProsody(phrase.Prosody, phrase.WordIds))).ToList();
        _phrases.Clear();
        _phrases.AddRange(reconciled);
        ValidatePhraseCoverage();
    }

    private IReadOnlyList<SyllableId> SyllableIdsForWords(IEnumerable<LyricWordId> wordIds)
    {
        var wordById = _words.ToDictionary(item => item.Id);
        return wordIds
            .SelectMany(wordId => wordById[wordId].Syllables)
            .Select(syllable => syllable.Id)
            .ToList();
    }

    private ProsodicPattern? FilterProsody(
        ProsodicPattern? pattern,
        IReadOnlyList<LyricWordId> wordIds,
        ProsodicPatternId? patternId = null) =>
        pattern is null
            ? null
            : BuildProsody(patternId ?? pattern.Id, pattern.Units, SyllableIdsForWords(wordIds));

    private ProsodicPattern? CombineProsody(
        ProsodicPattern? previous,
        ProsodicPattern? current,
        IReadOnlyList<LyricWordId> wordIds)
    {
        if (previous is null && current is null) return null;
        var patternId = previous?.Id ?? current!.Id;
        var units = (previous?.Units ?? []).Concat(current?.Units ?? []).ToList();
        return BuildProsody(patternId, units, SyllableIdsForWords(wordIds));
    }

    private static ProsodicPattern? BuildProsody(
        ProsodicPatternId patternId,
        IEnumerable<ProsodicUnit> units,
        IReadOnlyList<SyllableId> orderedSyllableIds)
    {
        var unitBySyllable = units.ToDictionary(item => item.SyllableId);
        var normalized = orderedSyllableIds
            .Where(unitBySyllable.ContainsKey)
            .Select((syllableId, position) =>
            {
                var unit = unitBySyllable[syllableId];
                return new ProsodicUnit(unit.Id, syllableId, position, unit.Weight, unit.Provenance);
            })
            .ToList();
        return normalized.Count == 0 ? null : new ProsodicPattern(patternId, normalized);
    }

    private static ProsodicPattern? CloneProsody(ProsodicPattern? pattern) => pattern is null
        ? null
        : new ProsodicPattern(
            pattern.Id,
            pattern.Units.Select(unit => new ProsodicUnit(
                unit.Id,
                unit.SyllableId,
                unit.Position,
                unit.Weight,
                unit.Provenance)).ToList());

    private static void ValidateProsodyReferences(
        IReadOnlyList<LyricWord> words,
        IReadOnlyList<LyricPhrase> phrases)
    {
        var wordById = words.ToDictionary(item => item.Id);
        foreach (var phrase in phrases.Where(item => item.Prosody is not null))
        {
            var syllableIds = phrase.WordIds
                .SelectMany(wordId => wordById[wordId].Syllables)
                .Select(syllable => syllable.Id)
                .ToList();
            var referenced = phrase.Prosody!.Units.Select(item => item.SyllableId).ToList();
            var referencedSet = referenced.ToHashSet();
            if (!referenced.SequenceEqual(syllableIds.Where(referencedSet.Contains)))
                throw new ArgumentException("Prosodic units must reference syllables from their phrase once and in order.");
        }
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
