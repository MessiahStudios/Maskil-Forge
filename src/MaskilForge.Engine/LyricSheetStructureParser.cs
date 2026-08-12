using System.Text.RegularExpressions;
using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record ProposedSongSection(
    SectionKind Kind,
    string Title,
    SectionDelivery Delivery,
    string PerformanceNotes,
    IReadOnlyList<string> Lyrics);

public sealed record LyricSheetStructurePreview(
    IReadOnlyList<ProposedSongSection> Sections,
    IReadOnlyList<string> UnassignedLines);

public static partial class LyricSheetStructureParser
{
    [GeneratedRegex(@"^\s*\[(?<heading>.+?)\]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    public static LyricSheetStructurePreview Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sections = new List<ProposedSongSection>();
        var unassigned = new List<string>();
        Draft? current = null;

        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.Trim();
            var match = HeadingPattern().Match(line);
            if (match.Success && TryParseHeading(match.Groups["heading"].Value, out var heading))
            {
                if (current is not null) sections.Add(current.Build());
                current = heading;
            }
            else if (line.Length > 0)
            {
                if (current is null) unassigned.Add(line);
                else current.Lyrics.Add(line);
            }
        }

        if (current is not null) sections.Add(current.Build());
        return new LyricSheetStructurePreview(sections, unassigned);
    }

    private static bool TryParseHeading(string value, out Draft draft)
    {
        var pieces = Regex.Split(value, @"\s+[–—-]\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (pieces.Length > 2) pieces = [pieces[0], string.Join(" – ", pieces.Skip(1))];
        var title = pieces[0].Trim();
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

        var notes = pieces.Length > 1 ? pieces[1].Trim() : string.Empty;
        var intent = notes.ToLowerInvariant();
        var delivery = intent.Contains("whisper", StringComparison.Ordinal) ? SectionDelivery.Whispered
            : intent.Contains("talk-sung", StringComparison.Ordinal) || intent.Contains("talk sung", StringComparison.Ordinal) ? SectionDelivery.TalkSung
            : intent.Contains("spoken", StringComparison.Ordinal) || intent.Contains("near spoken", StringComparison.Ordinal) ? SectionDelivery.Spoken
            : SectionDelivery.Sung;
        draft = new Draft(kind.Value, title, delivery, notes);
        return true;
    }

    private sealed record Draft(SectionKind Kind, string Title, SectionDelivery Delivery, string Notes)
    {
        public List<string> Lyrics { get; } = [];
        public ProposedSongSection Build() => new(Kind, Title, Delivery, Notes, Lyrics.ToList());
    }
}
