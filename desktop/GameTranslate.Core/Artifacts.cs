using System.Security.Cryptography;

namespace GameTranslate.Core;

public sealed record CompanionArtifact(string Name, long Bytes, string Sha256);

public sealed record ArtifactRecord(
    string PatchName,
    string Sha256,
    string Mode,
    string TargetCulture,
    IReadOnlyList<string> VirtualPaths,
    IReadOnlyList<CompanionArtifact> Companions,
    PakInfo Pak)
{
    public bool RequiresIoStoreCompanions { get; init; }
    public string? SourceCulture { get; init; }
    public IReadOnlyDictionary<string, string> FileHashes { get; init; } = new Dictionary<string, string>();

    public ArtifactRecord(string patchName, string sha256, string mode, string targetCulture,
        IReadOnlyList<string> virtualPaths, PakInfo pak)
        : this(patchName, sha256, mode, targetCulture, virtualPaths, [], pak) { }
}

public sealed record InstallResult(string Destination, string? BackupPath);

public static class FileHashes
{
    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

public static class UnrealEngineVersions
{
    public static string ToRetocVersion(int major, int minor)
    {
        var supported = major == 4 && minor is >= 25 and <= 27
            || major == 5 && minor is >= 0 and <= 7;
        if (!supported)
            throw new NotSupportedException($"Unreal Engine {major}.{minor} is not supported by the bundled retoc.");
        return $"UE{major}_{minor}";
    }

    public static string? TryFromProbeOutput(string output)
    {
        // UEExtractor echoes the version it was INVOKED with; without an explicit argument it
        // prints the GAME_UE5_LATEST placeholder, which carries no concrete version information.
        var matches = System.Text.RegularExpressions.Regex.Matches(output ?? string.Empty,
            @"^UE::Version:\s*GAME_UE([45])_(\d+)\s*$", System.Text.RegularExpressions.RegexOptions.Multiline).Cast<System.Text.RegularExpressions.Match>()
            .Select(match => (Major: int.Parse(match.Groups[1].Value), Minor: int.Parse(match.Groups[2].Value)))
            .Distinct()
            .ToArray();
        return matches.Length == 1 ? ToRetocVersion(matches[0].Major, matches[0].Minor) : null;
    }

    public static string FromProbeOutput(string output) =>
        TryFromProbeOutput(output)
        ?? throw new NotSupportedException("Archive probe did not identify one unambiguous supported Unreal Engine version.");

    public static string FindShippingExecutable(string projectRoot)
    {
        var binaries = Path.Combine(projectRoot, "Binaries", "Win64");
        var candidates = Directory.Exists(binaries)
            ? Directory.EnumerateFiles(binaries, "*-Win64-Shipping.exe", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        if (candidates.Length != 1)
            throw new NotSupportedException($"Expected exactly one *-Win64-Shipping.exe under {binaries} for engine version detection, found {candidates.Length}.");
        return candidates[0];
    }

    public static (int Major, int Minor) ExecutableVersionParts(string projectRoot)
    {
        var executable = FindShippingExecutable(projectRoot);
        var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(executable);
        if (info.FileMajorPart <= 0)
            throw new NotSupportedException($"Shipping executable carries no usable engine version: {executable}");
        return (info.FileMajorPart, info.FileMinorPart);
    }

    public static string FromGameExecutable(string projectRoot)
    {
        var (major, minor) = ExecutableVersionParts(projectRoot);
        return ToRetocVersion(major, minor);
    }

    // UEExtractor mounts nothing on some engine versions unless told the exact version;
    // its GAME_UE5_LATEST default is not a detector.
    public static string UeExtractorVersionArgument(int major, int minor) => $"--version={major}.{minor}";
}

public static class UnrealPaths
{
    public static string ProjectRootFromPaks(string gameRoot, string paksDirectory)
    {
        var root = Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var paks = Path.GetFullPath(paksDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!paks.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unreal Paks directory is outside the selected game root.");
        var paksInfo = new DirectoryInfo(paks);
        if (!paksInfo.Name.Equals("Paks", StringComparison.OrdinalIgnoreCase) ||
            paksInfo.Parent is null || !paksInfo.Parent.Name.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
            paksInfo.Parent.Parent is null)
            throw new InvalidDataException("Expected Unreal layout <Project>/Content/Paks.");
        var projectRoot = paksInfo.Parent.Parent.FullName;
        if (!(projectRoot + Path.DirectorySeparatorChar).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unreal project root escaped the selected game root.");
        return projectRoot;
    }

    public static string ChangeCulture(string virtualPath, string sourceCulture, string targetCulture)
    {
        var segments = SafeSegments(virtualPath);
        var indexes = segments.Select((segment, index) => (segment, index))
            .Where(item => item.segment.Equals(sourceCulture, StringComparison.Ordinal))
            .Select(item => item.index)
            .ToArray();
        if (indexes.Length != 1)
            throw new InvalidDataException($"Expected exactly one {sourceCulture} segment in {virtualPath}.");
        segments[indexes[0]] = targetCulture;
        return string.Join('/', segments);
    }

    public static string StagePath(string stageDirectory, string virtualPath)
    {
        var segments = SafeSegments(virtualPath);
        var root = Path.GetFullPath(stageDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine([stageDirectory, .. segments]));
        if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Virtual path escaped the staging directory: {virtualPath}");
        return result;
    }

    private static string[] SafeSegments(string virtualPath)
    {
        var normalized = (virtualPath ?? string.Empty).Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || Path.IsPathRooted(normalized) || segments.Any(value => value is "." or ".."))
            throw new InvalidDataException($"Unsafe Unreal virtual path: {virtualPath}");
        return segments;
    }
}

public static class UnrealWorkPaths
{
    public static string TranslatedCsv(string translatedRoot, int index, string targetName)
    {
        var safeName = string.Concat((targetName ?? string.Empty).Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        if (string.IsNullOrWhiteSpace(safeName)) throw new InvalidDataException("Localization target name is empty or unsafe.");
        return Path.Combine(translatedRoot, $"{index:D3}-{safeName}", $"{safeName}.csv");
    }

    public static string TranslatedHashes(string translatedRoot, int index, string targetName) =>
        Path.ChangeExtension(TranslatedCsv(translatedRoot, index, targetName), ".locreshashes");
}
