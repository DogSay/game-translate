using System.Security.Cryptography;
using System.Text;

namespace GameTranslate.Core;

public static class PortableStorage
{
    public static string GameDirectory(string gameRoot, string? localAppDataRoot = null)
    {
        var localRoot = localAppDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localRoot)) throw new InvalidOperationException("LOCALAPPDATA is unavailable.");
        var fullGame = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        var normalizedGame = fullGame.ToUpperInvariant();
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedGame)));
        var destination = Path.Combine(Path.GetFullPath(localRoot), "GameTranslate", "games", identity);
        if (IsWithinGame(fullGame, destination) ||
            IsWithinGame(ResolveExistingLinks(fullGame), ResolveExistingLinks(destination)))
            throw new InvalidDataException("Portable storage cannot be inside the game directory.");
        RejectLinkedApplicationPath(Path.Combine(Path.GetFullPath(localRoot), "GameTranslate"), destination);
        return destination;
    }

    private static bool IsWithinGame(string fullGame, string destination)
    {
        var gamePrefix = Path.EndsInDirectorySeparator(fullGame) ? fullGame : fullGame + Path.DirectorySeparatorChar;
        return destination.Equals(fullGame, StringComparison.OrdinalIgnoreCase) ||
               destination.StartsWith(gamePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveExistingLinks(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var resolved = root;
        foreach (var component in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (component.Length == 0) continue;
            resolved = Path.Combine(resolved, component);
            if (Directory.Exists(resolved) &&
                (File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
                resolved = new DirectoryInfo(resolved).ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new InvalidDataException($"Cannot resolve linked directory {resolved}.");
        }
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolved));
    }

    public static string SessionLogDirectory(string gameRoot, string? localAppDataRoot = null)
    {
        var gameDirectory = GameDirectory(gameRoot, localAppDataRoot);
        var applicationRoot = Directory.GetParent(Directory.GetParent(gameDirectory)!.FullName)!.FullName;
        var logs = Path.Combine(applicationRoot, "session-logs", Path.GetFileName(gameDirectory));
        var fullGame = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        if (IsWithinGame(fullGame, logs) ||
            IsWithinGame(ResolveExistingLinks(fullGame), ResolveExistingLinks(logs)))
            throw new InvalidDataException("Session logs cannot be inside the game directory.");
        RejectLinkedApplicationPath(applicationRoot, logs);
        return logs;
    }

    public static string NewSessionLogPath(string gameRoot, string? localAppDataRoot = null) =>
        Path.Combine(SessionLogDirectory(gameRoot, localAppDataRoot),
            $"GameTranslate-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}-{Guid.NewGuid():N}.log");

    public static string PrepareSharedToolsDirectory(string gameRoot, string version, string? localAppDataRoot = null)
    {
        if (string.IsNullOrWhiteSpace(version) || Path.GetFileName(version) != version ||
            version is "." or ".." || version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Tool cache version must be one directory name.", nameof(version));

        using var operation = PortableGameOperationLease.Acquire(gameRoot, localAppDataRoot);
        var gameDirectory = PrepareGameDirectory(gameRoot, localAppDataRoot);
        var applicationRoot = Directory.GetParent(Directory.GetParent(gameDirectory)!.FullName)!.FullName;
        var shared = Path.Combine(applicationRoot, "shared", "tools", version);
        var fullGame = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameRoot));
        if (IsWithinGame(fullGame, shared) ||
            IsWithinGame(ResolveExistingLinks(fullGame), ResolveExistingLinks(shared)))
            throw new InvalidDataException("Shared tools cannot be inside the game directory.");
        RejectLinkedApplicationPath(applicationRoot, shared);
        if (File.Exists(shared)) throw new InvalidDataException($"Expected a directory at {shared}.");
        if (Directory.Exists(shared)) return shared;

        var oldTools = Path.Combine(gameDirectory, "tools", version);
        RejectLinkedApplicationPath(applicationRoot, oldTools);
        if (File.Exists(oldTools)) throw new InvalidDataException($"Expected a directory at {oldTools}.");
        Directory.CreateDirectory(Path.GetDirectoryName(shared)!);
        RejectLinkedApplicationPath(applicationRoot, shared);
        if (Directory.Exists(oldTools))
        {
            EnumerateSafeTree(oldTools);
            try { Directory.Move(oldTools, shared); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A concurrent instance or a locked old tool may prevent the move.
                // Keep the old cache untouched and re-extract verified tools instead.
                Directory.CreateDirectory(shared);
            }
        }
        else Directory.CreateDirectory(shared);
        RejectLinkedApplicationPath(applicationRoot, shared);
        return shared;
    }

    private static void RejectLinkedApplicationPath(string applicationRoot, string destination)
    {
        var relative = Path.GetRelativePath(applicationRoot, destination);
        if (Path.IsPathRooted(relative) || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Storage path escapes the application directory: {destination}");
        var current = applicationRoot;
        if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Refusing linked storage directory: {current}");
        foreach (var component in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (component.Length == 0) continue;
            current = Path.Combine(current, component);
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Refusing linked storage directory: {current}");
        }
    }

    public static string PrepareGameDirectory(string gameRoot, string? localAppDataRoot = null)
    {
        var fullGame = Path.GetFullPath(gameRoot);
        if (!Directory.Exists(fullGame)) throw new DirectoryNotFoundException($"Game directory does not exist: {fullGame}");
        var destination = GameDirectory(fullGame, localAppDataRoot);
        var legacy = Path.Combine(fullGame, ".game-translate");
        if (File.Exists(legacy)) throw new InvalidDataException($"Expected a directory at {legacy}");
        if (!Directory.Exists(legacy))
        {
            Directory.CreateDirectory(destination);
            return destination;
        }

        EnumerateSafeTree(legacy);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidDataException($"Portable storage already exists; legacy data was kept at {legacy}. Resolve the conflict before migrating to {destination}.");
        if (!Path.GetPathRoot(legacy)!.Equals(Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Safe automatic migration requires the game and LocalAppData to be on the same volume. Legacy data was kept at {legacy}.");
        // Same-volume directory rename is atomic. A late writer either moves with the
        // directory or recreates the old path; neither set of files is deleted here.
        Directory.Move(legacy, destination);
        return destination;
    }

    private static void EnumerateSafeTree(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Refusing to migrate a reparse point: {current}");
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Refusing to migrate a reparse point: {entry}");
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(entry);
            }
        }
    }
}
