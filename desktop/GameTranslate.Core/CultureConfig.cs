using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GameTranslate.Core;

public sealed class CultureConfigChange : IDisposable
{
    private readonly string _temporaryPath;
    private readonly string _timestamp;
    private bool _committed;
    private bool _rolledBack;

    internal CultureConfigChange(string configPath, string temporaryPath, string timestamp)
    {
        ConfigPath = configPath;
        _temporaryPath = temporaryPath;
        _timestamp = timestamp;
    }

    public string ConfigPath { get; }
    public string? BackupPath { get; private set; }

    public void Commit()
    {
        if (_committed || _rolledBack) throw new InvalidOperationException("Culture config change is no longer pending.");
        BackupPath = CultureConfig.AvailablePath($"{ConfigPath}.game-translate-backup-{_timestamp}");
        try
        {
            File.Replace(_temporaryPath, ConfigPath, BackupPath, ignoreMetadataErrors: true);
            _committed = true;
        }
        catch
        {
            BackupPath = null;
            throw;
        }
    }

    public void Rollback()
    {
        if (_rolledBack) return;
        if (_committed)
        {
            if (BackupPath is not null && File.Exists(BackupPath))
            {
                if (File.Exists(ConfigPath)) File.Replace(BackupPath, ConfigPath, null, ignoreMetadataErrors: true);
                else File.Move(BackupPath, ConfigPath);
            }
        }
        else if (File.Exists(_temporaryPath))
        {
            File.Delete(_temporaryPath);
        }
        _rolledBack = true;
    }

    public void Dispose()
    {
        if (!_committed && !_rolledBack && File.Exists(_temporaryPath)) File.Delete(_temporaryPath);
    }
}

public static partial class CultureConfig
{
    [GeneratedRegex(@"^[A-Za-z0-9-]+$")]
    private static partial Regex CultureName();

    public static string? FindExisting(GameDetection game, string localAppDataRoot)
    {
        if (string.IsNullOrWhiteSpace(game.ProjectName) ||
            !Path.GetFileName(game.ProjectName).Equals(game.ProjectName, StringComparison.Ordinal)) return null;
        var root = Path.GetFullPath(localAppDataRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var platform in new[] { "Windows", "WindowsNoEditor", "WinGDK" })
        {
            var candidate = Path.GetFullPath(Path.Combine(localAppDataRoot, game.ProjectName, "Saved", "Config", platform, "GameUserSettings.ini"));
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unreal user config escaped LOCALAPPDATA.");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public static string? ForMode(GameDetection game, string localAppDataRoot, TranslationMode mode)
    {
        var path = FindExisting(game, localAppDataRoot);
        if (mode == TranslationMode.NativeTraditional && path is null)
            throw new NotSupportedException("原生 zh-Hant 模式需要現有 GameUserSettings.ini；工具未能安全定位玩家語言設定。請先啟動遊戲一次，或使用兼容模式。");
        return path;
    }

    public static string UpdateText(string contents, string culture)
    {
        if (!CultureName().IsMatch(culture)) throw new InvalidDataException($"Unsafe culture name: {culture}");
        var newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = contents.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        var sectionStart = lines.FindIndex(line => line.Trim().Equals("[Internationalization]", StringComparison.OrdinalIgnoreCase));
        if (sectionStart < 0)
        {
            if (lines.Count > 0 && lines[^1] != string.Empty) lines.Add(string.Empty);
            lines.Add("[Internationalization]");
            lines.Add($"Language={culture}");
            lines.Add($"Locale={culture}");
            return string.Join(newline, lines);
        }

        var sectionEnd = lines.FindIndex(sectionStart + 1, line => Regex.IsMatch(line, @"^\s*\[[^\]]+\]\s*$"));
        if (sectionEnd < 0) sectionEnd = lines.Count;
        // "Culture" overrides Language/Locale in UE, so an existing key must be rewritten too —
        // but never invented, because games that don't write it don't expect it.
        foreach (var (key, insertWhenMissing) in new[] { ("Language", true), ("Locale", true), ("Culture", false) })
        {
            var keyPattern = new Regex($@"^\s*{Regex.Escape(key)}\s*=", RegexOptions.IgnoreCase);
            var keyIndexes = new List<int>();
            for (var index = sectionStart + 1; index < sectionEnd; index++)
            {
                if (!keyPattern.IsMatch(lines[index])) continue;
                keyIndexes.Add(index);
            }
            if (keyIndexes.Count > 0)
            {
                foreach (var keyIndex in keyIndexes) lines[keyIndex] = $"{key}={culture}";
            }
            else if (insertWhenMissing)
            {
                lines.Insert(sectionEnd, $"{key}={culture}");
                sectionEnd++;
            }
        }
        return string.Join(newline, lines);
    }

    public static CultureConfigChange Prepare(string configPath, string culture, string? timestamp = null)
    {
        var fullPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Unreal GameUserSettings.ini is required for culture activation.", fullPath);
        var token = string.IsNullOrWhiteSpace(timestamp) ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") : timestamp;
        if (token.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
            throw new InvalidDataException("Unsafe culture backup timestamp.");
        var temporaryPath = AvailablePath($"{fullPath}.game-translate-{token}.tmp");
        var updated = UpdateText(File.ReadAllText(fullPath), culture);
        using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(updated);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        return new(fullPath, temporaryPath, token);
    }

    internal static string AvailablePath(string preferred)
    {
        if (!File.Exists(preferred)) return preferred;
        var suffix = 1;
        while (File.Exists($"{preferred}.{suffix}")) suffix++;
        return $"{preferred}.{suffix}";
    }
}

public sealed record PortableDisableResult(IReadOnlyList<string> DisabledPatches, string? ConfigPath, string? ConfigBackup, string? StorageWarning = null);

public sealed record PortableDeleteResult(IReadOnlyList<string> DeletedPaths, string? ConfigPath, string? ConfigBackup, string? StorageWarning = null);

public static class PortableModeManager
{
    public static PortableDisableResult DisableAndReset(GameDetection game, string localAppDataRoot, string sourceCulture = "zh-Hans")
    {
        if (game.PaksDirectory is null) throw new NotSupportedException("Detected game has no writable Unreal Paks directory.");
        var (storage, warning) = TryPrepareStorage(game.GameRoot, localAppDataRoot);
        var configPath = CultureConfig.FindExisting(game, localAppDataRoot);
        using var config = configPath is null ? null : CultureConfig.Prepare(configPath, sourceCulture);
        var disabled = PatchManager.DisableOwned(game.PaksDirectory);
        try
        {
            config?.Commit();
            if (storage is not null) WriteDisabledState(storage, disabled);
            return new(disabled, configPath, config?.BackupPath, warning);
        }
        catch
        {
            config?.Rollback();
            PatchManager.RestoreDisabled(disabled);
            throw;
        }
    }

    public static PortableDeleteResult DeleteAndReset(GameDetection game, string localAppDataRoot, string sourceCulture = "zh-Hans")
    {
        if (game.PaksDirectory is null) throw new NotSupportedException("Detected game has no writable Unreal Paks directory.");
        var (storage, warning) = TryPrepareStorage(game.GameRoot, localAppDataRoot);
        var configPath = CultureConfig.FindExisting(game, localAppDataRoot);
        using var config = configPath is null ? null : CultureConfig.Prepare(configPath, sourceCulture);
        // Commit the culture reset before deleting: deletion is irreversible, so never leave
        // the player pointed at a culture whose data has just been removed.
        config?.Commit();
        var deleted = PatchManager.DeleteOwned(game.PaksDirectory);
        if (storage is not null) WriteDeletedState(storage, deleted);
        return new(deleted, configPath, config?.BackupPath, warning);
    }

    private static (string? Storage, string? Warning) TryPrepareStorage(string gameRoot, string localAppDataRoot)
    {
        try { return (PortableStorage.PrepareGameDirectory(gameRoot, localAppDataRoot), null); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException)
        {
            // Patch state is also represented by the physical .pak/.disabled files.
            // Keep management usable when old work data cannot safely migrate; never
            // overwrite either the legacy tree or a conflicting AppData destination.
            return (null, error.Message);
        }
    }

    private static void WriteDeletedState(string directory, IReadOnlyList<string> deleted)
    {
        var destination = Path.Combine(directory, "install-state.json");
        var state = new { version = 3, active = false, deletedAt = DateTimeOffset.UtcNow, deleted };
        File.WriteAllText(destination, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void WriteDisabledState(string directory, IReadOnlyList<string> disabled)
    {
        var destination = Path.Combine(directory, "install-state.json");
        JsonElement? artifact = null;
        if (File.Exists(destination))
        {
            using var previous = JsonDocument.Parse(File.ReadAllText(destination));
            if (previous.RootElement.TryGetProperty("artifact", out var existing)) artifact = existing.Clone();
        }
        var state = new { version = 3, active = false, disabledAt = DateTimeOffset.UtcNow, artifact, disabled };
        var temporary = destination + $".writing.{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, destination, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }
}
