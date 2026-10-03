using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using GameTranslate.Core;

namespace GameTranslate.App;

internal static class ToolInstaller
{
    private static readonly string[] Resources =
    [
        "UEExtractor.dll", "UEExtractor.exe", "UEExtractor.runtimeconfig.json", "UEExtractor-LICENSE.txt",
        "CUE4Parse-LICENSE.txt", "repak.exe", "repak-LICENSE-APACHE.txt", "repak-LICENSE-MIT.txt",
        "retoc.exe", "retoc-LICENSE.txt", "zlib-ng2.dll", "zlib-ng-LICENSE.txt",
    ];

    public static PortableToolPaths Ensure(string gameRoot)
    {
        var directory = Path.Combine(Path.GetFullPath(gameRoot), ".game-translate", "tools", "1.0.8.4-0.2.3");
        Directory.CreateDirectory(directory);
        var assembly = typeof(ToolInstaller).Assembly;
        foreach (var name in Resources) ExtractVerified(assembly, $"Tools.{name}", Path.Combine(directory, name));
        return new(Path.Combine(directory, "UEExtractor.exe"), Array.Empty<string>(),
            Path.Combine(directory, "repak.exe"), Path.Combine(directory, "retoc.exe"));
    }

    private static void ExtractVerified(Assembly assembly, string resourceName, string destination)
    {
        using var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new MissingManifestResourceException($"Missing embedded tool: {resourceName}");
        using var memory = new MemoryStream();
        resource.CopyTo(memory);
        var bytes = memory.ToArray();
        var expected = Convert.ToHexString(SHA256.HashData(bytes));
        if (File.Exists(destination) && FileHashes.Sha256(destination) == expected) return;
        var temporary = destination + $".extracting.{Guid.NewGuid():N}";
        File.WriteAllBytes(temporary, bytes);
        if (File.Exists(destination)) File.Move(destination, destination + $".corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");
        File.Move(temporary, destination);
    }
}
