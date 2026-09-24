using System.Globalization;
using System.Text.Json;

namespace GameTranslate.Core;

public enum TranslationMode { Compatibility, NativeTraditional }

public static class TranslationModes
{
    public static string TargetCulture(TranslationMode mode, string sourceCulture) =>
        mode == TranslationMode.NativeTraditional ? "zh-Hant" : sourceCulture;
}

public sealed record PortableToolPaths(string UeExtractorHost, IReadOnlyList<string> UeExtractorPrefix, string Repak, string Retoc)
{
    // UEExtractor (CUE4Parse) drops its native libraries into the process working directory,
    // so hosted runs must start in the bundled tools directory, not beside GameTranslate.exe.
    public string UeExtractorWorkingDirectory()
    {
        var assemblyPath = UeExtractorPrefix.LastOrDefault(value => value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        return Path.GetDirectoryName(assemblyPath ?? UeExtractorHost)!;
    }
}
public sealed record WorkflowResult(ArtifactRecord Artifact, InstallResult Installation, int Targets, int Entries,
    string WorkDirectory, string? CultureConfigPath, string? CultureConfigBackup);

public sealed class UnrealTranslationWorkflow
{
    private readonly IProcessRunner _processes;
    private readonly ITranslationApi _translationApi;
    private readonly PortableToolPaths _tools;
    private readonly Action<string> _log;
    private readonly string _localAppDataRoot;

    public UnrealTranslationWorkflow(IProcessRunner processes, ITranslationApi translationApi, PortableToolPaths tools, Action<string> log,
        string? localAppDataRoot = null)
    {
        _processes = processes;
        _translationApi = translationApi;
        _tools = tools;
        _log = log;
        _localAppDataRoot = localAppDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    public async Task<WorkflowResult> BuildAndInstallAsync(GameDetection game, TranslationMode mode, CancellationToken cancellationToken)
    {
        if (game.Engine != EngineKind.Unreal || game.PaksDirectory is null)
            throw new NotSupportedException("目前只有 Unreal 外加 patch 流程可安全執行。");

        var portableRoot = Path.Combine(game.GameRoot, ".game-translate");
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var runRoot = Path.Combine(portableRoot, "work", runId);
        var probeDirectory = Path.Combine(runRoot, "probe");
        Directory.CreateDirectory(probeDirectory);
        var unrealProjectRoot = UnrealPaths.ProjectRootFromPaks(game.GameRoot, game.PaksDirectory);

        string? versionArgument = null;
        (int Major, int Minor)? engineVersion = null;
        try
        {
            var parts = UnrealEngineVersions.ExecutableVersionParts(unrealProjectRoot);
            engineVersion = parts;
            versionArgument = UnrealEngineVersions.UeExtractorVersionArgument(parts.Major, parts.Minor);
            Log($"Shipping executable 引擎版本：{parts.Major}.{parts.Minor}");
        }
        catch (NotSupportedException)
        {
            Log("搵唔到 shipping executable 版本；改用 UEExtractor 預設版本偵測。", warning: true);
        }

        Log($"正在列出 Unreal localization 資源：{unrealProjectRoot}");
        string[] probeArguments = versionArgument is null
            ? [unrealProjectRoot, SlashDirectory(probeDirectory), "--extract-locres", "--no-parallel", "--verbose"]
            : [unrealProjectRoot, SlashDirectory(probeDirectory), versionArgument, "--extract-locres", "--no-parallel", "--verbose"];
        var probe = await Retry.OnceAsync(
            () => _processes.RunAsync(_tools.UeExtractorHost, UeArguments(probeArguments), _tools.UeExtractorWorkingDirectory(), cancellationToken),
            result => UnrealProbeParser.ParseSimplifiedChineseCandidates(result.Output).Count > 0,
            _ => Log("列舉未搵到簡中 locres（可能係暫時性 archive 存取失敗），2 秒後重試一次……", warning: true));
        await File.WriteAllTextAsync(Path.Combine(probeDirectory, "probe-output.log"), probe.Output, cancellationToken);
        var candidates = UnrealProbeParser.ParseSimplifiedChineseCandidates(probe.Output).ToArray();
        if (candidates.Length == 0)
            throw new InvalidDataException("偵測到 Unreal，但搵唔到可轉換嘅簡中（zh-Hans／zh-CN）locres；遊戲可能冇簡中、已加密，或者格式未支援。");
        if (!probe.Success) Log("全域列舉工具回傳非零狀態，但已完整取得候選清單；改用逐項安全抽取。", warning: true);
        Log($"找到 {candidates.Length} 個簡中 localization 目標。");
        var retocVersion = game.Packaging == PackagingKind.IoStore
            ? (engineVersion is { } engine
                ? UnrealEngineVersions.ToRetocVersion(engine.Major, engine.Minor)
                : UnrealEngineVersions.FromProbeOutput(probe.Output))
            : null;
        if (retocVersion is not null) Log($"IoStore target 版本：{retocVersion}");

        var basePak = ChooseBasePak(game.PaksDirectory, candidates);
        var baseInfoResult = await _processes.RunAsync(_tools.Repak, ["info", basePak], Path.GetDirectoryName(_tools.Repak), cancellationToken);
        if (!baseInfoResult.Success) throw new InvalidDataException("無法讀取 base pak metadata；可能已加密或 pak 格式未支援。\n" + baseInfoResult.Output);
        var baseInfo = RepakParser.ParseInfo(baseInfoResult.Output);
        if (baseInfo.MountPoint != "../../../") throw new InvalidDataException($"未支援嘅 pak mount point：{baseInfo.MountPoint}");

        var sourceCulture = SimplifiedCultures.Dominant(candidates.Select(candidate => candidate.Culture).ToArray());
        var targetCulture = TranslationModes.TargetCulture(mode, sourceCulture);
        var stageDirectory = Path.Combine(runRoot, "stage");
        var translatedDirectory = Path.Combine(runRoot, "translated");
        var builtDirectory = Path.Combine(runRoot, "built");
        var templateDirectory = Path.Combine(builtDirectory, "templates");
        Directory.CreateDirectory(stageDirectory);
        Directory.CreateDirectory(translatedDirectory);
        Directory.CreateDirectory(builtDirectory);
        var virtualPaths = new List<string>();
        var totalEntries = 0;

        for (var index = 0; index < candidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[index];
            Log($"[{index + 1}/{candidates.Length}] 抽取 {candidate.Name}……");
            var targetRoot = Path.Combine(runRoot, "source", $"{index:D3}-{SafeName(candidate.Name)}");
            Directory.CreateDirectory(targetRoot);
            var parentPath = candidate.VirtualPath[..candidate.VirtualPath.LastIndexOf('/')];
            string[] extractionArguments = versionArgument is null
                ? [unrealProjectRoot, SlashDirectory(targetRoot), $"--path={parentPath}/", "--extract-locres", "--no-parallel"]
                : [unrealProjectRoot, SlashDirectory(targetRoot), versionArgument, $"--path={parentPath}/", "--extract-locres", "--no-parallel"];
            var extraction = await Retry.OnceAsync(
                () => _processes.RunAsync(_tools.UeExtractorHost, UeArguments(extractionArguments), _tools.UeExtractorWorkingDirectory(), cancellationToken),
                result => result.Success,
                _ => Log($"{candidate.Name} 抽取失敗（可能係暫時性 archive 存取失敗），2 秒後重試一次……", warning: true));
            if (!extraction.Success)
            {
                await File.WriteAllTextAsync(Path.Combine(targetRoot, "extract-output.log"), extraction.Output, cancellationToken);
                throw new InvalidDataException($"{candidate.Name} 抽取失敗（已重試一次）。\n{extraction.Output}");
            }

            var archiveBase = Path.GetFileNameWithoutExtension(candidate.SourceArchive);
            var extractedFiles = UeExtractionFiles.FindLocalization(targetRoot, candidate.Name, archiveBase);
            var csv = extractedFiles.Csv;
            var hashes = extractedFiles.Hashes;
            var sourceText = await File.ReadAllTextAsync(csv, cancellationToken);
            var locresKeys = hashes is null ? new HashSet<string>(StringComparer.Ordinal) : ReadHashKeys(hashes);
            var normalized = UeExtractorCsv.Normalize(sourceText, locresKeys);
            if (normalized.Repairs > 0) Log($"{candidate.Name}: 修復 {normalized.Repairs} 個 multiline key。", warning: true);
            var sourceDocument = LocalizationCsv.Parse(normalized.Text);
            var expectedEntries = sourceDocument.Rows.Skip(1).Count(row => row.Any(cell => !string.IsNullOrEmpty(cell)));
            if (expectedEntries == 0) throw new InvalidDataException($"{candidate.Name} 抽取結果冇文字。");

            Log($"[{index + 1}/{candidates.Length}] 呼叫繁化姬轉換 {expectedEntries} 條文字……");
            var converted = await LocalizationTranslator.ConvertAsync(normalized.Text, _translationApi, 100, cancellationToken,
                (done, total) => { if (done == total || done % 500 == 0) Log($"{candidate.Name}: {done}/{total}"); });
            var translatedCsv = UnrealWorkPaths.TranslatedCsv(translatedDirectory, index, candidate.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(translatedCsv)!);
            await File.WriteAllTextAsync(translatedCsv, converted.Text, new System.Text.UTF8Encoding(false), cancellationToken);
            if (hashes is not null)
                File.Copy(hashes, UnrealWorkPaths.TranslatedHashes(translatedDirectory, index, candidate.Name), overwrite: true);

            var templateExtract = await _processes.RunAsync(_tools.Repak,
                ["unpack", "-o", templateDirectory, "-i", candidate.VirtualPath, basePak],
                Path.GetDirectoryName(_tools.Repak), cancellationToken);
            if (!templateExtract.Success)
                throw new InvalidDataException($"{candidate.Name} 原版 locres 擷取失敗。\n{templateExtract.Output}");
            var templatePath = UnrealPaths.StagePath(templateDirectory, candidate.VirtualPath);
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"{candidate.Name} 原版 locres 不存在於 base pak。", templatePath);
            var memory = LoadTranslationMemory(Path.Combine(portableRoot, "translation-memory", "zhconvert-taiwan.json"));
            var convertedTranslations = LocresTranslations.FromCsv(converted.Text, memory);
            var translations = mode == TranslationMode.Compatibility
                ? LocresOverrides.Apply(convertedTranslations)
                : convertedTranslations;
            var templateBytes = await File.ReadAllBytesAsync(templatePath, cancellationToken);
            var surgery = UnrealLocresSurgery.Patch(templateBytes, translations);
            var protectedLength = checked((int)BitConverter.ToInt64(templateBytes, 17) + 4);
            if (!templateBytes.AsSpan(0, protectedLength).SequenceEqual(surgery.Bytes.AsSpan(0, protectedLength)))
                throw new InvalidDataException($"{candidate.Name} locres 手術改動了 header 或 hash tables。");

            var targetVirtualPath = UnrealPaths.ChangeCulture(candidate.VirtualPath, candidate.Culture,
                mode == TranslationMode.NativeTraditional ? "zh-Hant" : candidate.Culture);
            var staged = UnrealPaths.StagePath(stageDirectory, targetVirtualPath);
            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            await File.WriteAllBytesAsync(staged, surgery.Bytes, cancellationToken);
            virtualPaths.Add(targetVirtualPath);
            totalEntries += expectedEntries;
            Log($"{candidate.Name}: locres v{surgery.Version}，{surgery.Replaced}/{surgery.StringCount} strings 已繁化。");
        }

        var modeName = mode == TranslationMode.NativeTraditional ? "native" : "compat";
        var patchName = SimplifiedCultures.PatchName(targetCulture);
        var distDirectory = Path.Combine(portableRoot, "dist", modeName);
        Directory.CreateDirectory(distDirectory);
        var patchPath = Path.Combine(distDirectory, patchName);
        var seedDecimal = RepakParser.SeedDecimal(baseInfo.PathHashSeed);
        Log("正在建立外加 pak patch……");
        var pack = await _processes.RunAsync(_tools.Repak,
            ["pack", "--version", baseInfo.Version, "--path-hash-seed", seedDecimal, "--compression", "Zlib", stageDirectory, patchPath],
            Path.GetDirectoryName(_tools.Repak), cancellationToken);
        if (!pack.Success) throw new InvalidDataException("repak 建立 patch 失敗。\n" + pack.Output);

        var packedInfoResult = await _processes.RunAsync(_tools.Repak, ["info", patchPath], Path.GetDirectoryName(_tools.Repak), cancellationToken);
        if (!packedInfoResult.Success) throw new InvalidDataException("無法驗證生成嘅 patch metadata。\n" + packedInfoResult.Output);
        var packedInfo = RepakParser.ParseInfo(packedInfoResult.Output);
        VerifyPackedInfo(packedInfo, baseInfo.Version, baseInfo.PathHashSeed, virtualPaths.Count);
        var list = await _processes.RunAsync(_tools.Repak, ["list", patchPath], Path.GetDirectoryName(_tools.Repak), cancellationToken);
        if (!list.Success) throw new InvalidDataException("無法驗證 patch 路徑。\n" + list.Output);
        UnrealVerification.ExactPaths(list.Output, virtualPaths);

        var unpackDirectory = Path.Combine(runRoot, "verification-unpack");
        Directory.CreateDirectory(unpackDirectory);
        var unpack = await _processes.RunAsync(_tools.Repak, ["unpack", "-o", unpackDirectory, patchPath], Path.GetDirectoryName(_tools.Repak), cancellationToken);
        if (!unpack.Success) throw new InvalidDataException("patch round-trip 解包失敗。\n" + unpack.Output);
        var roundTripHashes = VerifyRoundTrip(stageDirectory, unpackDirectory, virtualPaths);

        var companions = new List<CompanionArtifact>();
        if (game.Packaging == PackagingKind.IoStore)
        {
            var companionWork = Path.Combine(builtDirectory, "companions");
            Directory.CreateDirectory(companionWork);
            var generatedUtoc = Path.Combine(companionWork, Path.ChangeExtension(patchName, ".utoc"));
            Log($"建立 IoStore companions（{retocVersion}）……");
            var retoc = await _processes.RunAsync(_tools.Retoc,
                ["to-zen", patchPath, generatedUtoc, "--version", retocVersion!],
                Path.GetDirectoryName(_tools.Retoc), cancellationToken);
            if (!retoc.Success) throw new InvalidDataException("retoc 建立 IoStore companions 失敗。\n" + retoc.Output);
            foreach (var extension in new[] { ".utoc", ".ucas" })
            {
                var generated = Path.ChangeExtension(generatedUtoc, extension);
                if (!File.Exists(generated)) throw new FileNotFoundException("retoc 未有建立所需 companion。", generated);
                var destination = Path.ChangeExtension(patchPath, extension);
                File.Copy(generated, destination, overwrite: true);
                companions.Add(new(Path.GetFileName(destination), new FileInfo(destination).Length, FileHashes.Sha256(destination)));
            }
        }

        var record = new ArtifactRecord(patchName, FileHashes.Sha256(patchPath),
            modeName, targetCulture, virtualPaths.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            companions.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray(), packedInfo)
        {
            RequiresIoStoreCompanions = game.Packaging == PackagingKind.IoStore,
            SourceCulture = sourceCulture,
            FileHashes = roundTripHashes,
        };
        var recordPath = Path.Combine(distDirectory, "verification.json");
        await File.WriteAllTextAsync(recordPath, JsonSerializer.Serialize(record, JsonOptions), cancellationToken);

        var configPath = CultureConfig.ForMode(game, _localAppDataRoot, mode);
        using var cultureChange = configPath is null ? null : CultureConfig.Prepare(configPath, targetCulture);
        var disabled = PatchManager.DisableOwned(game.PaksDirectory);
        InstallResult installation;
        try
        {
            installation = PatchManager.InstallVerified(patchPath, record, game.PaksDirectory);
            var installedInfoResult = await _processes.RunAsync(_tools.Repak, ["info", installation.Destination], Path.GetDirectoryName(_tools.Repak), cancellationToken);
            if (!installedInfoResult.Success) throw new InvalidDataException("安裝後無法重新讀取 patch metadata。\n" + installedInfoResult.Output);
            VerifyPackedInfo(RepakParser.ParseInfo(installedInfoResult.Output), baseInfo.Version, baseInfo.PathHashSeed, virtualPaths.Count);
            var installedList = await _processes.RunAsync(_tools.Repak, ["list", installation.Destination], Path.GetDirectoryName(_tools.Repak), cancellationToken);
            if (!installedList.Success) throw new InvalidDataException("安裝後無法重新讀取 patch 路徑。\n" + installedList.Output);
            UnrealVerification.ExactPaths(installedList.Output, virtualPaths);
            cultureChange?.Commit();
            if (configPath is not null) Log($"已切換玩家語言設定：{targetCulture}（{configPath}）");
            await WriteInstallState(portableRoot, record, installation, disabled, configPath, cultureChange?.BackupPath, cancellationToken);
            try
            {
                foreach (var residual in PatchManager.DeleteResiduals(game.PaksDirectory))
                    Log($"已清除舊版 patch 檔案：{Path.GetFileName(residual)}");
            }
            catch (Exception cleanupError)
            {
                Log("清除舊版 patch 檔案失敗（不影響本次安裝）：" + cleanupError.Message, warning: true);
            }
        }
        catch (Exception installError)
        {
            var rollbackErrors = new List<Exception>();
            try { cultureChange?.Rollback(); }
            catch (Exception error) { rollbackErrors.Add(error); }
            try
            {
                foreach (var installed in new[] { new CompanionArtifact(record.PatchName, new FileInfo(patchPath).Length, record.Sha256) }.Concat(record.Companions))
                {
                    var destination = Path.Combine(game.PaksDirectory, installed.Name);
                    if (File.Exists(destination) && FileHashes.Sha256(destination).Equals(installed.Sha256, StringComparison.OrdinalIgnoreCase))
                        File.Move(destination, destination + $".failed.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");
                }
            }
            catch (Exception error) { rollbackErrors.Add(error); }
            try { PatchManager.RestoreDisabled(disabled); }
            catch (Exception error) { rollbackErrors.Add(error); }
            if (rollbackErrors.Count > 0) throw new AggregateException("Patch 安裝失敗，而且 rollback 未能完整完成。", [installError, .. rollbackErrors]);
            throw;
        }
        Log($"已安裝：{installation.Destination}");
        Log($"SHA-256：{record.Sha256}");
        return new(record, installation, candidates.Length, totalEntries, runRoot, configPath, cultureChange?.BackupPath);
    }

    private void Log(string message, bool warning = false) => _log($"{(warning ? "警告：" : string.Empty)}{message}");

    private string[] UeArguments(params string[] arguments) => [.. _tools.UeExtractorPrefix, .. arguments];

    private static string ChooseBasePak(string paksDirectory, IReadOnlyList<LocresCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!candidate.SourceArchive.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)) continue;
            if (candidate.SourceArchive.EndsWith("_P.pak", StringComparison.OrdinalIgnoreCase)) continue;
            var exact = Path.Combine(paksDirectory, candidate.SourceArchive);
            if (File.Exists(exact) && !PatchManager.IsOwnedPatchName(exact)) return exact;
        }
        var fallback = Directory.EnumerateFiles(paksDirectory, "*.pak", SearchOption.TopDirectoryOnly)
            .Where(path => !PatchManager.IsOwnedPatchName(path) && !Path.GetFileName(path).EndsWith("_P.pak", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(path => new FileInfo(path).Length)
            .FirstOrDefault();
        return fallback ?? throw new NotSupportedException("搵唔到可讀取 metadata 嘅 base .pak；純 IoStore 遊戲暫未支援自動建立外加 patch。");
    }

    private static HashSet<string> ReadHashKeys(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path).TrimStart('\uFEFF'));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("locreshashes 不是 JSON object。");
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> LoadTranslationMemory(string path)
    {
        if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.Ordinal);
        return TranslationMemoryDocuments.Parse(File.ReadAllText(path));
    }

    private static void VerifyPackedInfo(PakInfo actual, string version, string seed, int files)
    {
        if (actual.MountPoint != "../../../" || actual.Version != version || actual.Compression != "Zlib" ||
            !actual.PathHashSeed.Equals(seed, StringComparison.OrdinalIgnoreCase) || actual.FileCount != files)
            throw new InvalidDataException("生成嘅 patch metadata 與預期不符。");
    }

    private static IReadOnlyDictionary<string, string> VerifyRoundTrip(string stage, string unpacked, IReadOnlyList<string> paths)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var virtualPath in paths)
        {
            var staged = UnrealPaths.StagePath(stage, virtualPath);
            var extracted = UnrealPaths.StagePath(unpacked, virtualPath);
            var stagedHash = FileHashes.Sha256(staged);
            if (!File.Exists(extracted) || !stagedHash.Equals(FileHashes.Sha256(extracted), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"patch round-trip hash 不一致：{virtualPath}");
            hashes[virtualPath] = stagedHash;
        }
        return hashes;
    }

    private static async Task WriteInstallState(string root, ArtifactRecord record, InstallResult install,
        IReadOnlyList<string> disabled, string? configPath, string? configBackup, CancellationToken token)
    {
        var state = new { version = 3, active = true, installedAt = DateTimeOffset.UtcNow, artifact = record, install, disabled,
            culture = new { configPath, configBackup, record.TargetCulture } };
        await File.WriteAllTextAsync(Path.Combine(root, "install-state.json"), JsonSerializer.Serialize(state, JsonOptions), token);
    }

    private static string SlashDirectory(string directory) => directory.Replace('\\', '/').TrimEnd('/') + "/";
    private static string SafeName(string name) => string.Concat(name.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
