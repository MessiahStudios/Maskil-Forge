using System.Text.RegularExpressions;
using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record ProposedSongSection(
    SectionKind Kind,
    string Title,
    SectionDelivery Delivery,
    string PerformanceNotes,
    IReadOnlyList<string> Lyrics,
    StructuralFunction StructuralFunction = StructuralFunction.Unspecified);

public sealed record LyricSheetStructurePreview(
    IReadOnlyList<ProposedSongSection> Sections,
    IReadOnlyList<string> UnassignedLines,
    IReadOnlyList<string> UnrecognizedHeadings,
    IReadOnlyList<UnrecognizedSongSection> UnrecognizedSections);

public sealed record UnrecognizedSongSection(
    string Heading,
    SectionDelivery Delivery,
    string PerformanceNotes,
    IReadOnlyList<string> Lyrics,
    int InsertionIndex);

public static partial class LyricSheetStructureParser
{
    [GeneratedRegex(@"^\s*\[(?<heading>.+?)\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    public static LyricSheetStructurePreview Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sections = new List<ProposedSongSection>();
        var unassigned = new List<string>();
        var unrecognizedHeadings = new List<string>();
        var unrecognizedSections = new List<UnrecognizedSongSection>();
        Draft? current = null;
        UnknownDraft? unknown = null;

        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            var match = HeadingPattern().Match(line);
            if (match.Success)
            {
                if (current is not null) sections.Add(current.Build());
                if (unknown is not null) unrecognizedSections.Add(unknown.Build());
                current = null;
                unknown = null;
                if (TryParseHeading(match.Groups["heading"].Value, out var heading)) current = heading;
                else
                {
                    var (title, notes, delivery) = ParseHeadingDetails(match.Groups["heading"].Value);
                    unrecognizedHeadings.Add(line);
                    unknown = new UnknownDraft(title, delivery, notes, sections.Count);
                }
            }
            else if (line.Length > 0)
            {
                if (current is not null) current.Lyrics.Add(line);
                else if (unknown is not null) unknown.Lyrics.Add(line);
                else unassigned.Add(line);
            }
        }

        if (current is not null) sections.Add(current.Build());
        if (unknown is not null) unrecognizedSections.Add(unknown.Build());
        return new LyricSheetStructurePreview(DisambiguateRepeatedTitles(sections), unassigned, unrecognizedHeadings, unrecognizedSections);
    }

    private static IReadOnlyList<ProposedSongSection> DisambiguateRepeatedTitles(IReadOnlyList<ProposedSongSection> sections)
    {
        var repeatedTitles = sections.GroupBy(section => section.Title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        return sections.Select(section =>
        {
            if (!repeatedTitles.Contains(section.Title)) return section;
            ordinals[section.Title] = ordinals.GetValueOrDefault(section.Title) + 1;
            return section with { Title = $"{section.Title} {ordinals[section.Title]}" };
        }).ToList();
    }

    private static bool TryParseHeading(string value, out Draft draft)
    {
        var (title, notes, delivery) = ParseHeadingDetails(value);
        var normalized = title.ToLowerInvariant().Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        var kind = normalized switch
        {
            var item when item.StartsWith("intro", StringComparison.Ordinal) => SectionKind.Intro,
            var item when item.StartsWith("verse", StringComparison.Ordinal) => SectionKind.Verse,
            var item when item.StartsWith("prechorus", StringComparison.Ordinal) => SectionKind.PreChorus,
            var item when item.Contains("chorus", StringComparison.Ordinal) => SectionKind.Chorus,
            var item when item.StartsWith("bridge", StringComparison.Ordinal) => SectionKind.Bridge,
            var item when item.StartsWith("outro", StringComparison.Ordinal) => SectionKind.Outro,
            _ => (SectionKind?)null
        };
        if (kind is null) { draft = null!; return false; }
        draft = new Draft(kind.Value, title, delivery, notes);
        return true;
    }

    private static (string Title, string Notes, SectionDelivery Delivery) ParseHeadingDetails(string value)
    {
        var pieces = Regex.Split(value, @"\s+[–—-]\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (pieces.Length > 2) pieces = [pieces[0], string.Join(" – ", pieces.Skip(1))];
        var title = pieces[0].Trim();
        var notes = pieces.Length > 1 ? pieces[1].Trim() : string.Empty;
        var intent = notes.ToLowerInvariant();
        var delivery = intent.Contains("whisper", StringComparison.Ordinal) ? SectionDelivery.Whispered
            : intent.Contains("talk-sung", StringComparison.Ordinal) || intent.Contains("talk sung", StringComparison.Ordinal) ? SectionDelivery.TalkSung
            : intent.Contains("spoken", StringComparison.Ordinal) || intent.Contains("near spoken", StringComparison.Ordinal) ? SectionDelivery.Spoken
            : SectionDelivery.Sung;
        return (title, notes, delivery);
    }

    private sealed record Draft(SectionKind Kind, string Title, SectionDelivery Delivery, string Notes)
    {
        public List<string> Lyrics { get; } = [];
        public ProposedSongSection Build() => new(Kind, Title, Delivery, Notes, Lyrics.ToList(), StructuralFunction.Unspecified);
    }

    private sealed record UnknownDraft(string Heading, SectionDelivery Delivery, string Notes, int InsertionIndex)
    {
        public List<string> Lyrics { get; } = [];
        public UnrecognizedSongSection Build() => new(Heading, Delivery, Notes, Lyrics.ToList(), InsertionIndex);
    }
}
