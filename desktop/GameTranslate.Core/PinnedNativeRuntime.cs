using System.IO.Compression;
using System.Security.Cryptography;

namespace GameTranslate.Core;

public sealed record PinnedZipAsset(string FileName, string ArchiveEntry, Uri Url, string ArchiveSha256, string FileSha256,
    string? LegacyFileSha256 = null);

public static class PinnedZipRuntime
{
    private const int MaximumArchiveBytes = 16 * 1024 * 1024;
    private const int MaximumRuntimeBytes = 4 * 1024 * 1024;

    public static async Task EnsureAsync(string directory, PinnedZipAsset asset, HttpClient client, CancellationToken cancellationToken)
    {
        if (Path.GetFileName(asset.FileName) != asset.FileName || asset.Url.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Native runtime must use a plain filename and HTTPS URL.", nameof(asset));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, asset.FileName);
        if (File.Exists(destination))
        {
            var existingHash = FileHashes.Sha256(destination);
            if (existingHash.Equals(asset.FileSha256, StringComparison.OrdinalIgnoreCase)) return;
            if (asset.LegacyFileSha256 is null ||
                !existingHash.Equals(asset.LegacyFileSha256, StringComparison.OrdinalIgnoreCase))
                VerifyFile(destination, asset.FileSha256);
        }

        using var response = await client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumArchiveBytes)
            throw new InvalidDataException($"Native runtime archive exceeds {MaximumArchiveBytes} bytes.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var archiveBuffer = new MemoryStream();
        await CopyLimitedAsync(source, archiveBuffer, MaximumArchiveBytes, cancellationToken);
        if (!Matches(archiveBuffer.ToArray(), asset.ArchiveSha256))
            throw new InvalidDataException($"Native runtime archive SHA-256 mismatch: {asset.Url}");

        archiveBuffer.Position = 0;
        using var archive = new ZipArchive(archiveBuffer, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry(asset.ArchiveEntry)
            ?? throw new InvalidDataException($"Native runtime archive is missing {asset.ArchiveEntry}.");
        if (entry.Length > MaximumRuntimeBytes)
            throw new InvalidDataException($"Native runtime DLL exceeds {MaximumRuntimeBytes} bytes.");
        using var runtimeBuffer = new MemoryStream();
        await using (var entryStream = entry.Open())
            await CopyLimitedAsync(entryStream, runtimeBuffer, MaximumRuntimeBytes, cancellationToken);
        var runtimeBytes = runtimeBuffer.ToArray();
        if (!Matches(runtimeBytes, asset.FileSha256))
            throw new InvalidDataException($"Native runtime DLL SHA-256 mismatch: {asset.ArchiveEntry}");

        var temporary = destination + $".extracting.{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(temporary, runtimeBytes, cancellationToken);
            VerifyFile(temporary, asset.FileSha256);
            if (File.Exists(destination))
            {
                var existingHash = FileHashes.Sha256(destination);
                if (existingHash.Equals(asset.FileSha256, StringComparison.OrdinalIgnoreCase)) return;
                if (asset.LegacyFileSha256 is null ||
                    !existingHash.Equals(asset.LegacyFileSha256, StringComparison.OrdinalIgnoreCase))
                    VerifyFile(destination, asset.FileSha256);
                var backup = destination + $".previous.{Guid.NewGuid():N}";
                File.Replace(temporary, destination, backup);
                try { VerifyFile(destination, asset.FileSha256); }
                catch
                {
                    File.Replace(backup, destination, null);
                    throw;
                }
                File.Delete(backup);
            }
            else File.Move(temporary, destination);
            VerifyFile(destination, asset.FileSha256);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    internal static void VerifyFile(string path, string expectedSha256)
    {
        var actual = FileHashes.Sha256(path);
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unverified native runtime DLL: {path}. Expected SHA-256 {expectedSha256}, got {actual}. Do not load this file; remove it only after checking its origin.");
    }

    private static bool Matches(byte[] bytes, string expected) =>
        Convert.ToHexString(SHA256.HashData(bytes)).Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static async Task CopyLimitedAsync(Stream source, Stream destination, int maximum, CancellationToken token)
    {
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, token)) != 0)
        {
            total = checked(total + read);
            if (total > maximum) throw new InvalidDataException($"Native runtime download exceeds {maximum} bytes.");
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
        }
    }
}

public static class NativeRuntimePreflight
{
    public static readonly PinnedZipAsset UeExtractorOodle = new(
        "oodle-data-shared.dll", "bin/oodle-data-shared.dll",
        new Uri("https://github.com/WorkingRobot/OodleUE/releases/download/2026-06-04-1357/clang-cl-x64-release.zip"),
        "37F816A3E8F5A65A11F6DEFC8C28C0508287602767741BA4803C6A21A62A877B",
        "CBA19529D0A3B5EC9C630E95652AF01E123AE29A34A8A5F7507F5BCF23D9E82B",
        LegacyFileSha256: "8F6C0D487573B5A7EA8548407CBF96F5CD833FE7138769914961B6697D74B3B3");

    // repak 0.2.3 validates its own download, but does not validate a pre-existing DLL.
    public static void VerifyExistingRepakRuntime(string toolsDirectory)
    {
        var path = Path.Combine(toolsDirectory, "oo2core_9_win64.dll");
        if (File.Exists(path))
            PinnedZipRuntime.VerifyFile(path, "6F5D41A7892EA6B2DB420F2458DAD2F84A63901C9A93CE9497337B16C195F457");
    }
}
