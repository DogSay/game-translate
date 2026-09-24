namespace GameTranslate.Core;

public enum EngineKind { Unknown, Unreal, Unity }
public enum PackagingKind { Unknown, Pak, IoStore, UnityAssets }

public sealed record GameDetection(
    string GameRoot,
    EngineKind Engine,
    PackagingKind Packaging,
    string? ProjectName,
    string? PaksDirectory,
    IReadOnlyList<string> Archives)
{
    public bool TranslationSupported => Engine == EngineKind.Unreal;
}

public static class EngineDetector
{
    public static GameDetection Detect(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
            return new(fullRoot, EngineKind.Unknown, PackagingKind.Unknown, null, null, []);

        var unrealLayouts = new List<(string Directory, string Project, PackagingKind Packaging, string[] Archives)>();
        foreach (var directory in WalkDirectories(fullRoot))
        {
            if (!directory.EndsWith($"{Path.DirectorySeparatorChar}Content{Path.DirectorySeparatorChar}Paks", StringComparison.OrdinalIgnoreCase))
                continue;

            var archives = SafeFiles(directory)
                .Where(path => IsArchive(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (archives.Length == 0) continue;

            var contentDirectory = Directory.GetParent(directory)!;
            var projectDirectory = contentDirectory.Parent;
            var packaging = archives.Any(path => path.EndsWith(".utoc", StringComparison.OrdinalIgnoreCase))
                ? PackagingKind.IoStore
                : PackagingKind.Pak;
            unrealLayouts.Add((Path.GetFullPath(directory), projectDirectory?.Name ?? string.Empty, packaging, archives));
        }

        var unreal = unrealLayouts.OrderByDescending(item => item.Archives.Length)
            .ThenByDescending(item => item.Archives.Sum(path => new FileInfo(path).Length))
            .FirstOrDefault();
        if (unreal.Directory is not null)
            return new(fullRoot, EngineKind.Unreal, unreal.Packaging, unreal.Project, unreal.Directory, unreal.Archives);

        var unityPlayer = SafeFiles(fullRoot).FirstOrDefault(path => Path.GetFileName(path).Equals("UnityPlayer.dll", StringComparison.OrdinalIgnoreCase));
        if (unityPlayer is not null)
        {
            foreach (var directory in SafeDirectories(fullRoot).Where(path => path.EndsWith("_Data", StringComparison.OrdinalIgnoreCase)))
            {
                if (!File.Exists(Path.Combine(directory, "globalgamemanagers"))) continue;
                var name = Path.GetFileName(directory);
                return new(fullRoot, EngineKind.Unity, PackagingKind.UnityAssets, name[..^5], null, []);
            }
        }

        return new(fullRoot, EngineKind.Unknown, PackagingKind.Unknown, null, null, []);
    }

    private static bool IsArchive(string path) => Path.GetExtension(path).ToLowerInvariant() is ".pak" or ".utoc" or ".ucas";

    private static IEnumerable<string> WalkDirectories(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;
            foreach (var child in SafeDirectories(current))
            {
                if (Path.GetFileName(child).Equals(".game-translate", StringComparison.OrdinalIgnoreCase)) continue;
                pending.Push(child);
            }
        }
    }

    private static IEnumerable<string> SafeDirectories(string directory)
    {
        try { return Directory.EnumerateDirectories(directory).ToArray(); }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    private static IEnumerable<string> SafeFiles(string directory)
    {
        try { return Directory.EnumerateFiles(directory).ToArray(); }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }
}
