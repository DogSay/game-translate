using System.Text.RegularExpressions;

namespace GameTranslate.Core;

public sealed record LocresCandidate(string Name, string VirtualPath, string SourceArchive, string Culture = "zh-Hans");

public static partial class UnrealProbeParser
{
    [GeneratedRegex(@"Reading:\s*(?<path>[^\r\n]+?/(?<culture>zh-Hans|zh-CN)/[^\r\n/]+\.locres)\s*\(from\s+(?<archive>[^)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ReadingLine();

    public static IReadOnlyList<LocresCandidate> ParseSimplifiedChineseCandidates(string output)
    {
        var found = new Dictionary<string, LocresCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ReadingLine().Matches(output ?? string.Empty))
        {
            var virtualPath = match.Groups["path"].Value.Trim().Replace('\\', '/');
            var archive = match.Groups["archive"].Value.Trim();
            if (PatchManager.IsOwnedPatchName(archive) || archive.EndsWith("_P.pak", StringComparison.OrdinalIgnoreCase)) continue;
            var name = Path.GetFileNameWithoutExtension(virtualPath);
            found.TryAdd(virtualPath, new(name, virtualPath, archive, match.Groups["culture"].Value));
        }
        return found.Values.OrderBy(item => item.VirtualPath, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public static class SimplifiedCultures
{
    public static string Dominant(IReadOnlyList<string> cultures)
    {
        if (cultures.Count == 0) throw new InvalidDataException("No simplified-Chinese cultures detected.");
        return cultures.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .First().Key;
    }

    public static string PatchName(string culture) =>
        $"pakchunk99-GameTranslate_{culture.Replace("-", "", StringComparison.Ordinal)}_P.pak";

    public static string InstalledTargetCulture(string gameRoot)
    {
        try
        {
            var path = Path.Combine(gameRoot, ".game-translate", "install-state.json");
            if (!File.Exists(path)) return "zh-Hans";
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path).TrimStart('﻿'));
            if (document.RootElement.TryGetProperty("artifact", out var artifact)
                && artifact.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                // Restore must point the player back at a culture that still has data, so the
                // recorded source culture beats the (possibly tool-added) target culture.
                foreach (var key in new[] { "SourceCulture", "TargetCulture" })
                {
                    if (artifact.TryGetProperty(key, out var culture)
                        && culture.ValueKind == System.Text.Json.JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(culture.GetString()))
                        return culture.GetString()!;
                }
            }
        }
        catch
        {
            // Unreadable state falls back to the default source culture below.
        }
        return "zh-Hans";
    }
}
