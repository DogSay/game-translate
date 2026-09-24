using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameTranslate.Core;

public sealed record PatchInspection(IReadOnlyList<string> OwnedActive, IReadOnlyList<string> ForeignActive,
    IReadOnlyList<string>? OwnedResidual = null)
{
    public bool CanRestore => OwnedActive.Count > 0;
    public IReadOnlyList<string> Residual => OwnedResidual ?? [];
    public bool CanDelete => OwnedActive.Count > 0 || Residual.Count > 0;
}

public sealed record PortableUiState(string TranslateButtonText, string ToggleButtonText, bool CanToggle,
    bool CanRestore, bool CanDelete, string PatchStatusLine);

public static class PortableUi
{
    public static PortableUiState From(PatchInspection inspection)
    {
        if (inspection.CanRestore)
        {
            var names = string.Join("、", inspection.OwnedActive.Select(Path.GetFileName));
            return new("重新翻譯並安裝 Patch", "停用翻譯 Patch", true, true, true,
                $"已安裝 Game Translate 翻譯 patch：{names}（重新翻譯會取代佢）。");
        }
        var hasDisabled = inspection.Residual.Any(path =>
            Path.GetFileName(path).Contains(".disabled", StringComparison.OrdinalIgnoreCase));
        if (hasDisabled)
            return new("開始翻譯並安裝 Patch", "啟用翻譯 Patch", true, false, true,
                "偵測到已停用嘅 Game Translate 翻譯 patch；可以直接啟用，唔使重新翻譯。");
        if (inspection.Residual.Count > 0)
            return new("開始翻譯並安裝 Patch", "停用翻譯 Patch", false, false, true,
                $"未安裝翻譯 patch；偵測到 {inspection.Residual.Count} 個舊版 Game Translate 檔案（可刪除）。");
        return new("開始翻譯並安裝 Patch", "停用翻譯 Patch", false, false, false, "未安裝 Game Translate 翻譯 patch。");
    }
}

public static partial class PatchManager
{
    private sealed record LegacyOwnershipRecord(int Version, string PatchName, string Sha256);

    [GeneratedRegex(@"^pakchunk\d+-GameTranslate_[A-Za-z0-9_-]+_P\.pak$", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentName();

    [GeneratedRegex(@"^pakchunk\d+-GameTranslate_[A-Za-z0-9_-]+_P\.(?:pak|utoc|ucas)\..+$", RegexOptions.IgnoreCase)]
    private static partial Regex ResidualName();

    [GeneratedRegex(@"^pakchunk99-(?:ZhHant|ZhTW)_P\.pak$", RegexOptions.IgnoreCase)]
    private static partial Regex LegacyName();

    public static bool IsOwnedPatchName(string name)
    {
        var fileName = Path.GetFileName(name);
        return CurrentName().IsMatch(fileName) || LegacyName().IsMatch(fileName);
    }

    public static PatchInspection Inspect(string paksDirectory)
    {
        if (!Directory.Exists(paksDirectory)) return new([], []);
        var active = Directory.EnumerateFiles(paksDirectory, "*.pak", SearchOption.TopDirectoryOnly).ToArray();
        var owned = active.Where(IsMutablyOwned).Order().ToArray();
        var foreign = active.Where(path => Path.GetFileName(path).EndsWith("_P.pak", StringComparison.OrdinalIgnoreCase) && !IsMutablyOwned(path)).Order().ToArray();
        var residual = Directory.EnumerateFiles(paksDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => ResidualName().IsMatch(Path.GetFileName(path)))
            .Order().ToArray();
        return new(owned, foreign, residual);
    }

    [GeneratedRegex(@"^(?<stem>pakchunk\d+-GameTranslate_[A-Za-z0-9_-]+_P)\.(?<ext>pak|utoc|ucas)\.disabled(?<gen>(?:\..+)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex DisabledName();

    public static IReadOnlyList<string> EnableDisabled(string paksDirectory)
    {
        // Group by patch stem and disable-generation so one enable never mixes files from
        // different builds, then enable the newest complete generation transactionally.
        var groups = new Dictionary<(string Stem, string Generation), Dictionary<string, string>>();
        foreach (var path in Inspect(paksDirectory).Residual)
        {
            var match = DisabledName().Match(Path.GetFileName(path));
            if (!match.Success) continue;
            var key = (match.Groups["stem"].Value, match.Groups["gen"].Value);
            if (!groups.TryGetValue(key, out var files)) groups[key] = files = new(StringComparer.OrdinalIgnoreCase);
            files[match.Groups["ext"].Value.ToLowerInvariant()] = path;
        }

        var enabled = new List<string>();
        foreach (var stem in groups.Keys.Select(key => key.Stem).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // A stem with any active file is skipped entirely: enabling into it could mix builds.
            if (new[] { ".pak", ".utoc", ".ucas" }.Any(extension => File.Exists(Path.Combine(paksDirectory, stem + extension))))
                continue;
            var chosen = groups
                .Where(group => string.Equals(group.Key.Stem, stem, StringComparison.OrdinalIgnoreCase) && IsCompleteGeneration(group.Value))
                .OrderBy(group => group.Key.Generation, StringComparer.Ordinal)
                .LastOrDefault();
            if (chosen.Value is null) continue;

            var moved = new List<(string Source, string Target)>();
            try
            {
                foreach (var file in chosen.Value.OrderBy(file => file.Key, StringComparer.Ordinal))
                {
                    var target = Path.Combine(paksDirectory, stem + "." + file.Key);
                    File.Move(file.Value, target);
                    moved.Add((file.Value, target));
                }
            }
            catch
            {
                foreach (var (source, target) in moved.AsEnumerable().Reverse())
                {
                    try { File.Move(target, source); }
                    catch { /* Preserve the original failure; leftovers remain visible for manual recovery. */ }
                }
                throw;
            }
            enabled.AddRange(moved.Select(item => item.Target));
        }
        return enabled;
    }

    private static bool IsCompleteGeneration(IReadOnlyDictionary<string, string> files)
    {
        // A generation needs its .pak, and IoStore companions must come as a pair or not at all.
        if (!files.ContainsKey("pak")) return false;
        return files.ContainsKey("utoc") == files.ContainsKey("ucas");
    }

    public static IReadOnlyList<string> DeleteResiduals(string paksDirectory)
    {
        var deleted = new List<string>();
        foreach (var path in Inspect(paksDirectory).Residual.Where(File.Exists))
        {
            File.Delete(path);
            deleted.Add(path);
        }
        return deleted;
    }

    public static IReadOnlyList<string> DeleteOwned(string paksDirectory)
    {
        var inspection = Inspect(paksDirectory);
        var targets = inspection.OwnedActive.SelectMany(OwnedTriplet)
            .Concat(inspection.Residual)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Order()
            .ToArray();
        var deleted = new List<string>();
        foreach (var path in targets)
        {
            File.Delete(path);
            deleted.Add(path);
        }
        return deleted;
    }

    public static string LegacyOwnershipMarkerPath(string patchPath) => patchPath + ".game-translate-owner.json";

    public static void RecordLegacyOwnership(string patchPath, string expectedSha256)
    {
        if (!File.Exists(patchPath)) throw new FileNotFoundException("Legacy patch is missing.", patchPath);
        var patchName = Path.GetFileName(patchPath);
        if (!LegacyName().IsMatch(patchName)) throw new InvalidDataException("Ownership markers are only valid for legacy Game Translate patch names.");
        var actual = FileHashes.Sha256(patchPath);
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Legacy patch SHA-256 does not match the verified artifact.");
        var marker = new LegacyOwnershipRecord(1, patchName, actual);
        File.WriteAllText(LegacyOwnershipMarkerPath(patchPath), JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static IReadOnlyList<string> DisableOwned(string paksDirectory)
    {
        var inspection = Inspect(paksDirectory);
        var disabled = new List<string>();
        try
        {
            foreach (var patch in inspection.OwnedActive)
            {
                var files = OwnedTriplet(patch).Where(File.Exists).ToArray();
                // One suffix per triplet keeps every file of a disable operation in the same
                // generation, so a later enable can never mix builds.
                var suffix = files.Any(path => File.Exists(path + ".disabled"))
                    ? $".disabled.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}"
                    : ".disabled";
                foreach (var ownedPath in files)
                {
                    var destination = ownedPath + suffix;
                    File.Move(ownedPath, destination);
                    disabled.Add(destination);
                }
            }
            return disabled;
        }
        catch (Exception disableError)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var path in disabled.AsEnumerable().Reverse())
            {
                try
                {
                    var marker = path.IndexOf(".disabled", StringComparison.OrdinalIgnoreCase);
                    if (marker >= 0 && File.Exists(path)) File.Move(path, path[..marker]);
                }
                catch (Exception error) { rollbackErrors.Add(error); }
            }
            if (rollbackErrors.Count > 0)
                throw new AggregateException("Patch disabling failed and rollback was incomplete.", [disableError, .. rollbackErrors]);
            throw;
        }
    }

    public static void RestoreDisabled(IReadOnlyList<string> disabled)
    {
        foreach (var path in disabled.Reverse())
        {
            var marker = path.IndexOf(".disabled", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) continue;
            var original = path[..marker];
            if (!File.Exists(original) && File.Exists(path)) File.Move(path, original);
        }
    }

    private static bool IsMutablyOwned(string patchPath)
    {
        var patchName = Path.GetFileName(patchPath);
        if (CurrentName().IsMatch(patchName)) return true;
        if (!LegacyName().IsMatch(patchName)) return false;
        try
        {
            var markerPath = LegacyOwnershipMarkerPath(patchPath);
            if (!File.Exists(markerPath)) return false;
            var marker = JsonSerializer.Deserialize<LegacyOwnershipRecord>(File.ReadAllText(markerPath));
            return marker is { Version: 1 } &&
                marker.PatchName.Equals(patchName, StringComparison.OrdinalIgnoreCase) &&
                marker.Sha256.Equals(FileHashes.Sha256(patchPath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static InstallResult InstallVerified(string artifactPath, ArtifactRecord record, string paksDirectory)
    {
        if (!File.Exists(artifactPath)) throw new FileNotFoundException("Verified patch artifact is missing.", artifactPath);
        if (!Directory.Exists(paksDirectory)) throw new DirectoryNotFoundException(paksDirectory);
        if (!IsOwnedPatchName(record.PatchName) || !Path.GetFileName(record.PatchName).Equals(record.PatchName, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsafe or unmanaged patch name: {record.PatchName}");
        if (!FileHashes.Sha256(artifactPath).Equals(record.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Patch SHA-256 does not match its verification record.");

        var patchStem = Path.GetFileNameWithoutExtension(record.PatchName);
        var companions = record.Companions.Select(companion =>
        {
            if (!Path.GetFileName(companion.Name).Equals(companion.Name, StringComparison.Ordinal)
                || !Path.GetFileNameWithoutExtension(companion.Name).Equals(patchStem, StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(companion.Name).ToLowerInvariant() is not (".utoc" or ".ucas"))
                throw new InvalidDataException($"Unsafe or mismatched companion name: {companion.Name}");
            var source = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(artifactPath))!, companion.Name);
            if (!File.Exists(source)) throw new FileNotFoundException("Verified IoStore companion is missing.", source);
            if (new FileInfo(source).Length != companion.Bytes
                || !FileHashes.Sha256(source).Equals(companion.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"IoStore companion does not match its verification record: {companion.Name}");
            return (Record: companion, Source: source);
        }).ToArray();
        if (record.RequiresIoStoreCompanions && companions.Length != 2)
            throw new InvalidDataException("This verified artifact requires both IoStore companions.");
        if (companions.Length is not (0 or 2)
            || companions.Length == 2 && companions.Select(item => Path.GetExtension(item.Record.Name).ToLowerInvariant()).Distinct().Count() != 2)
            throw new InvalidDataException("IoStore artifacts require exactly one .utoc and one .ucas companion.");

        var root = Path.GetFullPath(paksDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(paksDirectory, record.PatchName));
        if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Patch destination escaped the game Paks directory.");

        string? backup = null;
        var files = new[] { (Source: artifactPath, Destination: destination, Hash: record.Sha256) }
            .Concat(companions.Select(item => (Source: item.Source, Destination: Path.Combine(paksDirectory, item.Record.Name), Hash: item.Record.Sha256)))
            .ToArray();
        var temporaries = new List<(string Temporary, string Destination, string Hash)>();
        var backups = new List<(string Backup, string Destination)>();
        var committed = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var temporary = file.Destination + $".installing.{Guid.NewGuid():N}";
                File.Copy(file.Source, temporary, overwrite: false);
                if (!FileHashes.Sha256(temporary).Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Copied artifact failed SHA-256 verification: {Path.GetFileName(file.Destination)}");
                temporaries.Add((temporary, file.Destination, file.Hash));
            }
            foreach (var file in temporaries)
            {
                if (File.Exists(file.Destination))
                {
                    var existingBackup = file.Destination + $".backup.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
                    File.Move(file.Destination, existingBackup);
                    backups.Add((existingBackup, file.Destination));
                    if (file.Destination.Equals(destination, StringComparison.OrdinalIgnoreCase)) backup = existingBackup;
                }
                File.Move(file.Temporary, file.Destination);
                committed.Add(file.Destination);
            }
            return new(destination, backup);
        }
        catch
        {
            foreach (var file in temporaries)
            {
                if (File.Exists(file.Temporary)) File.Delete(file.Temporary);
            }
            foreach (var destinationPath in committed.AsEnumerable().Reverse())
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
            foreach (var item in backups.AsEnumerable().Reverse())
                if (File.Exists(item.Backup) && !File.Exists(item.Destination)) File.Move(item.Backup, item.Destination);
            throw;
        }
    }

    private static string[] OwnedTriplet(string patch) =>
        [patch, Path.ChangeExtension(patch, ".utoc"), Path.ChangeExtension(patch, ".ucas")];
}
