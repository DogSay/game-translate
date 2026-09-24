using System.Globalization;
using System.Text.RegularExpressions;

namespace GameTranslate.Core;

public sealed record PakInfo(string MountPoint, string Version, string Compression, string PathHashSeed, int FileCount);

public static partial class RepakParser
{
    [GeneratedRegex(@"^mount point:\s*(?<value>[^\r\n]+)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex MountPointLine();
    [GeneratedRegex(@"^version:\s*(?<value>[^\r\n]+)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex VersionLine();
    [GeneratedRegex(@"^compression:\s*(?<value>[^\r\n]+)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CompressionLine();
    [GeneratedRegex(@"^path hash seed:\s*Some\((?<value>[0-9A-F]+)\)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SeedLine();
    [GeneratedRegex(@"^(?<value>\d+) file entries\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex FileCountLine();

    public static PakInfo ParseInfo(string output)
    {
        var mountPoint = Value(MountPointLine(), output, "mount point");
        var version = Value(VersionLine(), output, "version");
        var compression = Value(CompressionLine(), output, "compression");
        var seed = Value(SeedLine(), output, "path hash seed").ToUpperInvariant();
        var countText = Value(FileCountLine(), output, "file count");
        if (!int.TryParse(countText, out var count)) throw new InvalidDataException("Invalid pak file count.");
        return new(mountPoint, version, compression, seed, count);
    }

    public static string SeedDecimal(string hexadecimalSeed) =>
        ulong.Parse(hexadecimalSeed, NumberStyles.HexNumber, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    private static string Value(Regex regex, string output, string label)
    {
        var match = regex.Match(output ?? string.Empty);
        if (!match.Success) throw new InvalidDataException($"repak output did not contain {label}.");
        return match.Groups["value"].Value.Trim();
    }
}
