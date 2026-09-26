namespace MaskilForge.Api;

public static class SystemGeneralMidiBank
{
    public const string FileName = "gs_instruments.dls";
    public const string DisplayName = "This Mac's General MIDI instruments";
    public const long MaximumBytes = 8 * 1024 * 1024;

    private const string MacOsPath = "/System/Library/Components/CoreAudio.component/Contents/Resources/gs_instruments.dls";

    public static FileInfo? Locate()
    {
        if (!OperatingSystem.IsMacOS()) return null;
        var info = new FileInfo(MacOsPath);
        if (!info.Exists || info.Length <= 0 || info.Length > MaximumBytes) return null;
        if (info.LinkTarget is not null) return null;
        if (!string.Equals(info.Name, FileName, StringComparison.Ordinal)) return null;
        return info;
    }
}
