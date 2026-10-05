namespace GameTranslate.Core;

public sealed record CacheCleanupResult(int RemovedFiles, long ReclaimedBytes);

public static class PortableCacheCleaner
{
    private static readonly string[] DisposableDirectories = ["work", "dist", "tools", "logs"];

    public static CacheCleanupResult ClearGameCache(string gameRoot, string? activeLogPath = null,
        string? localAppDataRoot = null)
    {
        var fullGame = Path.GetFullPath(gameRoot);
        if (!Directory.Exists(fullGame)) throw new DirectoryNotFoundException($"Game directory does not exist: {fullGame}");
        using var operation = PortableGameOperationLease.Acquire(fullGame, localAppDataRoot);
        var legacy = Path.Combine(fullGame, ".game-translate");
        if (Directory.Exists(legacy) || File.Exists(legacy))
            throw new InvalidDataException($"Legacy game-local data must be migrated before clearing cache: {legacy}");

        var store = PortableStorage.GameDirectory(fullGame, localAppDataRoot);
        var sessionLogs = PortableStorage.SessionLogDirectory(fullGame, localAppDataRoot);
        if (File.Exists(store)) throw new InvalidDataException($"Expected a game-data directory at {store}.");
        var applicationRoot = Directory.GetParent(Directory.GetParent(store)!.FullName)!.FullName;
        RefuseLinkedPath(applicationRoot, store);
        RefuseLinkedPath(applicationRoot, sessionLogs);
        var targets = DisposableDirectories.Select(name => Path.Combine(store, name)).ToArray();
        foreach (var target in targets) RefuseLinkedTree(target);
        RefuseLinkedTree(sessionLogs);

        var removedFiles = 0;
        long reclaimedBytes = 0;
        foreach (var target in targets)
            DeleteTree(target, ref removedFiles, ref reclaimedBytes);

        var active = string.IsNullOrWhiteSpace(activeLogPath) ? null : Path.GetFullPath(activeLogPath);
        if (Directory.Exists(sessionLogs))
        {
            if ((File.GetAttributes(sessionLogs) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Refusing linked cache entry: {sessionLogs}");
            foreach (var file in Directory.EnumerateFiles(sessionLogs, "GameTranslate-*.log", SearchOption.TopDirectoryOnly))
            {
                if (file.Equals(active, StringComparison.OrdinalIgnoreCase)) continue;
                // Other UI instances keep their log open. Only an exclusively openable
                // file is old enough to remove; a live session must remain readable.
                try
                {
                    using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    DeleteFile(file, ref removedFiles, ref reclaimedBytes);
                }
                catch (IOException) { } // Open writer or a concurrent log rotation.
                catch (UnauthorizedAccessException) { } // Do not risk a live/foreign log.
            }
            if (!Directory.EnumerateFileSystemEntries(sessionLogs).Any()) Directory.Delete(sessionLogs);
        }
        return new CacheCleanupResult(removedFiles, reclaimedBytes);
    }

    private static void RefuseLinkedPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
            throw new InvalidDataException($"Cache path escapes the application directory: {path}");
        var current = root;
        if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Refusing linked cache path: {current}");
        foreach (var component in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (component.Length == 0) continue;
            current = Path.Combine(current, component);
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Refusing linked cache path: {current}");
        }
    }

    private static void RefuseLinkedTree(string root)
    {
        if (File.Exists(root)) throw new InvalidDataException($"Expected a cache directory at {root}.");
        if (!Directory.Exists(root)) return;
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Refusing linked cache entry: {current}");
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Refusing linked cache entry: {entry}");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
            }
        }
    }

    private static void DeleteTree(string path, ref int removedFiles, ref long reclaimedBytes)
    {
        if (!Directory.Exists(path)) return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Refusing linked cache entry: {path}");
        foreach (var entry in Directory.EnumerateFileSystemEntries(path))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Refusing linked cache entry: {entry}");
            if ((attributes & FileAttributes.Directory) != 0) DeleteTree(entry, ref removedFiles, ref reclaimedBytes);
            else DeleteFile(entry, ref removedFiles, ref reclaimedBytes);
        }
        Directory.Delete(path);
    }

    private static void DeleteFile(string path, ref int removedFiles, ref long reclaimedBytes)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Refusing linked cache entry: {path}");
        var length = new FileInfo(path).Length;
        File.Delete(path);
        removedFiles++;
        reclaimedBytes = checked(reclaimedBytes + length);
    }
}
